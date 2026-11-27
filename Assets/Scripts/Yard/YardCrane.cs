using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Yard
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class YardCrane : NetworkBehaviour
    {
        [SerializeField, Min(1f)] private float slewSpeed = 24f;
        [SerializeField, Min(.1f)] private float hoistSpeed = 1.4f;

        private readonly NetworkVariable<Vector4> pose = new();
        private readonly Collider[] hookContacts = new Collider[24];
        private readonly RaycastHit[] floorContacts = new RaycastHit[16];
        private Transform slew, hook, tip, cable;
        private Rigidbody slewBody, hookBody, cargo;
        private HandLever turn, lift;
        private Quaternion slewRest, hookRest;
        private Vector3 hookTop, hookBottom, cableRest;
        private float travel, cableLength;
        private ConfigurableJoint sling;
        private Collider[] loadColliders;
        private float pickedAt, landedFor;
        private bool readyToCatch = true, lifted;

        public static bool IsLeverCollider(string name) => name is "Col_Lever_Slew" or "Col_Lever_Hoist";

        private void Awake()
        {
            slew = Find("Move_Slew");
            hook = Find("Move_Hook");
            tip = Find("Point_HookTip");
            cable = Find("Cable");
            var top = Find("Pose_Hook_Top");
            var bottom = Find("Pose_Hook_Bottom");
            if (!slew || !hook || !tip || !cable || !top || !bottom ||
                !Find("Move_Lever_Slew") || !Find("Move_Lever_Hoist") ||
                !Find("Pose_Lever_Slew_Neutral") || !Find("Pose_Lever_Slew_Forward") || !Find("Pose_Lever_Slew_Back") ||
                !Find("Pose_Lever_Hoist_Neutral") || !Find("Pose_Lever_Hoist_Forward") || !Find("Pose_Lever_Hoist_Back"))
            {
                Debug.LogError("Yard_Crane mist een scharnier, hendelstand of haakpunt.", this);
                enabled = false;
                return;
            }

            slewRest = slew.localRotation;
            hookRest = hook.localRotation;
            hookTop = top.localPosition;
            hookBottom = bottom.localPosition;
            travel = Mathf.Abs(hookTop.y - hookBottom.y) * slew.lossyScale.y;
            cableRest = cable.localScale;
            cableLength = Mathf.Max(.01f, Mathf.Abs(cable.localPosition.y - hook.localPosition.y));


            // Raw yard models and generated prefabs share the same Col_ collision markers.
            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.name.StartsWith("Col_", System.StringComparison.Ordinal)) continue;
                if (filter.TryGetComponent<MeshRenderer>(out var renderer)) renderer.enabled = false;
                if (!filter.sharedMesh || filter.GetComponent<Collider>() || IsLeverCollider(filter.name)) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            slewBody = AddKinematicBody(slew);
            hookBody = AddKinematicBody(hook);
            turn = CreateLever("Slew");
            lift = CreateLever("Hoist");
        }

        public override void OnNetworkSpawn()
        {
            if (!enabled) return;
            if (IsServer)
            {
                float initial = Mathf.InverseLerp(hookTop.y, hookBottom.y, hook.localPosition.y);
                pose.Value = new Vector4(0f, initial, 0f, 0f);
            }
            // Clients present slew and hook through their transforms; interpolation would write stale physics poses over them.
            slewBody.interpolation = hookBody.interpolation = IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            turn.GetComponent<Rigidbody>().isKinematic = !IsServer;
            lift.GetComponent<Rigidbody>().isKinematic = !IsServer;
            Present(pose.Value, false);
        }

        public override void OnNetworkDespawn()
        {
            Drop();
            if (turn) turn.GetComponent<Rigidbody>().isKinematic = true;
            if (lift) lift.GetComponent<Rigidbody>().isKinematic = true;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            Vector4 before = pose.Value;
            float turnInput = InputFrom(turn);
            float liftInput = InputFrom(lift);
            bool lowering = liftInput > 0f;
            if (!lowering && !sling) readyToCatch = true;
            if (sling && !cargo) Drop();
            if (!sling && lowering && readyToCatch) Catch();

            if (sling && cargo)
            {
                lifted |= tip.position.y - pickedAt > .35f;
                landedFor = lowering && lifted && HasSupport() ? landedFor + Time.fixedDeltaTime : 0f;
                if (landedFor >= .18f) Drop();
            }

            float depth = Mathf.Clamp01(before.y + liftInput * hoistSpeed * Time.fixedDeltaTime / Mathf.Max(.1f, travel));
            float yaw = Mathf.Repeat(before.x + turnInput * slewSpeed * Time.fixedDeltaTime + 180f, 360f) - 180f;
            Vector4 next = new(yaw, depth, turn.Angle, lift.Angle);
            if (next != before) pose.Value = next;
            Present(next, true);
        }

        private void LateUpdate()
        {
            if (IsSpawned && !IsServer) Present(pose.Value, false);
        }

        private void Present(Vector4 state, bool simulate)
        {
            Quaternion rotation = slewRest * Quaternion.AngleAxis(state.x, Vector3.up);
            Vector3 position = Vector3.Lerp(hookTop, hookBottom, state.y);
            if (simulate)
            {
                Quaternion world = slew.parent.rotation * rotation;
                slewBody.MoveRotation(world);
                hookBody.MovePosition(slewBody.position + world * Vector3.Scale(position, slew.lossyScale));
                hookBody.MoveRotation(world * hookRest);
            }
            else
            {
                slew.localRotation = rotation;
                hook.localPosition = position;
                turn.PresentAngle(state.z);
                lift.PresentAngle(state.w);
            }

            Vector3 scale = cableRest;
            scale.y *= Mathf.Max(.02f, Mathf.Abs(cable.localPosition.y - position.y)) / cableLength;
            cable.localScale = scale;
        }

        private void Catch()
        {
            const float reach = .36f;
            float best = reach * reach;
            Transform ring = null;
            Rigidbody closest = null;
            int count = Physics.OverlapSphereNonAlloc(tip.position, reach, hookContacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var candidate = hookContacts[i].attachedRigidbody;
                if (!candidate || candidate.isKinematic || candidate.linearVelocity.sqrMagnitude > 4f ||
                    !candidate.TryGetComponent<NetworkObject>(out var network) || !network.IsSpawned || !network.IsOwnedByServer)
                    continue;
                var point = candidate.transform.Find("Point_Loop");
                if (!point) continue;
                float distance = (point.position - tip.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                closest = candidate;
                ring = point;
            }
            if (!closest) return;

            cargo = closest;
            loadColliders = cargo.GetComponentsInChildren<Collider>();
            pickedAt = tip.position.y;
            readyToCatch = lifted = false;
            landedFor = 0f;
            sling = cargo.gameObject.AddComponent<ConfigurableJoint>();
            sling.autoConfigureConnectedAnchor = false;
            sling.connectedBody = hookBody;
            sling.anchor = cargo.transform.InverseTransformPoint(ring.position);
            sling.connectedAnchor = hook.InverseTransformPoint(tip.position);
            sling.xMotion = sling.yMotion = sling.zMotion = ConfigurableJointMotion.Locked;
            sling.angularXMotion = sling.angularYMotion = sling.angularZMotion = ConfigurableJointMotion.Free;
            sling.enableCollision = false;
        }

        private bool HasSupport()
        {
            float lowest = float.PositiveInfinity;
            foreach (var col in loadColliders)
                if (col && col.enabled && !col.isTrigger) lowest = Mathf.Min(lowest, col.bounds.min.y);
            Vector3 point = cargo.worldCenterOfMass;
            if (!float.IsFinite(lowest)) return false;
            int count = Physics.RaycastNonAlloc(point, Vector3.down, floorContacts,
                Mathf.Max(0f, point.y - lowest) + .16f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = floorContacts[i];
                if (hit.rigidbody != cargo && !hit.transform.IsChildOf(transform) && hit.normal.y > .6f) return true;
            }
            return false;
        }

        private void Drop()
        {
            if (sling) Destroy(sling);
            sling = null;
            cargo = null;
            loadColliders = null;
        }

        private HandLever CreateLever(string name)
        {
            var node = Find("Move_Lever_" + name);
            var marker = Find("Col_Lever_" + name);
            if (marker && !marker.GetComponent<BoxCollider>() && marker.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
            {
                if (marker.TryGetComponent<MeshCollider>(out var mesh))
                {
                    mesh.enabled = false;
                    mesh.sharedMesh = null;
                    Destroy(mesh);
                }
                var box = marker.gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
            var body = AddKinematicBody(node);
            body.mass = 4f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var lever = node.gameObject.AddComponent<HandLever>();
            lever.Configure(Find("Pose_Lever_" + name + "_Neutral").localRotation,
                Find("Pose_Lever_" + name + "_Forward").localRotation, true,
                Find("Pose_Lever_" + name + "_Back").localRotation);
            return lever;
        }

        private static Rigidbody AddKinematicBody(Transform target)
        {
            var body = target.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return body;
        }

        private static float InputFrom(HandLever lever)
        {
            float value = lever.Normalized;
            return Mathf.Sign(value) * Mathf.InverseLerp(.18f, 1f, Mathf.Abs(value));
        }

        private Transform Find(string key)
        {
            foreach (var node in GetComponentsInChildren<Transform>(true))
                if (node.name == key) return node;
            return null;
        }
    }
}
