using System;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ConductorChaos.Playground
{
    // Preserves the single continuous body mesh while deforming its two lower legs.
    // Server simulates the hip joints; clients display the replicated local rotations.
    internal sealed class PlaygroundLegRig : IDisposable
    {
        private readonly Transform model;
        private readonly Rigidbody torso;
        private readonly MeshRenderer original;
        private readonly SkinnedMeshRenderer skin;
        private readonly Mesh mesh;
        private readonly Leg left;
        private readonly Leg right;
        private readonly RaycastHit[] contacts = new RaycastHit[16];
        private float stride;
        private bool leftStance = true;
        private bool leftForwardInAir;

        public Quaternion LeftRotation => left.Rotation;
        public Quaternion RightRotation => right.Rotation;
        public Vector3 SupportVelocity => left.HasSupport ? left.SupportVelocity : right.SupportVelocity;

        private PlaygroundLegRig(Transform model, Rigidbody torso, MeshRenderer original,
            SkinnedMeshRenderer skin, Mesh mesh, Leg left, Leg right)
        {
            this.model = model;
            this.torso = torso;
            this.original = original;
            this.skin = skin;
            this.mesh = mesh;
            this.left = left;
            this.right = right;
        }

        public static PlaygroundLegRig Create(Transform model, Rigidbody torso, bool server)
        {
            if (!model || !torso) return null;
            Transform body = null, legL = null, legR = null;
            foreach (Transform item in model.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == "Body") body = item;
                else if (item.name == "Leg_L") legL = item;
                else if (item.name == "Leg_R") legR = item;
            }
            MeshFilter filter = body ? body.GetComponent<MeshFilter>() : null;
            MeshRenderer renderer = body ? body.GetComponent<MeshRenderer>() : null;
            if (!filter || !renderer || !filter.sharedMesh || !legL || !legR || !filter.sharedMesh.isReadable)
            {
                Debug.LogError("Playground: leg rig requires the conductor body mesh, both leg anchors, and Read/Write enabled on Conductor.fbx.");
                return null;
            }

            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name + " (playground leg rig)";
            var bones = new[] { body, legL, legR };
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * body.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            var weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = model.InverseTransformPoint(body.TransformPoint(vertices[i]));
                float legWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.12f, point.y));
                // Leg anchors are named from the model's local coordinate system.
                bool useLeft = Mathf.Abs(point.x - legL.localPosition.x) <= Mathf.Abs(point.x - legR.localPosition.x);
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f - legWeight,
                    boneIndex1 = useLeft ? 1 : 2, weight1 = legWeight };
            }
            mesh.bindposes = bindposes;
            mesh.boneWeights = weights;
            var skinObject = new GameObject("Playground skinned body");
            skinObject.transform.SetParent(body, false);
            var skin = skinObject.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = bones;
            skin.rootBone = model;
            skin.sharedMaterials = renderer.sharedMaterials;
            skin.shadowCastingMode = renderer.shadowCastingMode;
            skin.receiveShadows = renderer.receiveShadows;
            skin.localBounds = filter.sharedMesh.bounds;
            skin.updateWhenOffscreen = true;
            renderer.enabled = false;
            var left = new Leg(legL, torso, server);
            var right = new Leg(legR, torso, server);
            if (left.Collider && right.Collider) Physics.IgnoreCollision(left.Collider, right.Collider);
            return new PlaygroundLegRig(model, torso, renderer, skin, mesh, left, right);
        }

        public bool Step(Vector3 velocity, Vector2 input, bool grounded, float targetSpeed, bool sprinting, out bool stepped)
        {
            stepped = false;
            Vector3 localVelocity = torso.transform.InverseTransformDirection(velocity);
            float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            // Start the step from input so stationary feet can get the body moving.
            float amount = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(speed / 2.5f)) * Mathf.Clamp01(input.magnitude);
            bool moving = grounded && input.sqrMagnitude > 0.01f;
            if (moving) stride += Time.fixedDeltaTime * Mathf.Max(2f, targetSpeed / 0.32f * Mathf.PI);
            float phase = Mathf.Sin(stride) * amount;
            float airbornePhase = sprinting && input.sqrMagnitude > 0.01f ? (leftForwardInAir ? -1f : 1f) : 0f;
            float swing = grounded ? phase * (sprinting ? 52f : 32f) * input.y
                : (airbornePhase != 0f ? airbornePhase * 48f * input.y : -10f);
            float side = grounded ? phase * input.x * (sprinting ? 30f : 25f) : airbornePhase * input.x * 20f;
            left.Drive(swing, side);
            right.Drive(-swing, -side);
            RaycastHit leftHit = default, rightHit = default;
            bool leftContact = grounded && left.FindGround(contacts, torso.transform, out leftHit);
            bool rightContact = grounded && right.FindGround(contacts, torso.transform, out rightHit);
            if (!grounded)
            {
                ReleaseSupport();
                return false;
            }
            if (!moving)
            {
                left.SetSupport(leftContact, leftHit);
                right.SetSupport(rightContact, rightHit);
            }
            else
            {
                bool wantsLeft = Mathf.Sin(stride) >= 0f;
                bool nextLeft = leftStance;
                if ((wantsLeft && leftContact) || (!wantsLeft && rightContact))
                    nextLeft = wantsLeft;
                else if ((leftStance && !leftContact && rightContact) || (!leftStance && !rightContact && leftContact))
                    nextLeft = !leftStance;
                if (nextLeft != leftStance)
                {
                    stepped = true;
                    leftStance = nextLeft;
                }
                left.SetSupport(leftStance && leftContact, leftHit);
                right.SetSupport(!leftStance && rightContact, rightHit);
            }
            return left.HasSupport || right.HasSupport;
        }

        public void ReleaseSupport()
        {
            left.ReleaseSupport();
            right.ReleaseSupport();
        }

        public void SetContactEnabled(bool enabled)
        {
            if (left.Collider) left.Collider.enabled = enabled;
            if (right.Collider) right.Collider.enabled = enabled;
        }

        public bool HasFootContact() => left.TouchesGround(contacts, torso.transform) || right.TouchesGround(contacts, torso.transform);

        public void BeginSprintHop()
        {
            leftForwardInAir = !leftForwardInAir;
            ReleaseSupport();
        }

        public void BraceForClimb(bool mantling)
        {
            ReleaseSupport();
            // Drive the existing leg joints toward the face while leaving their contacts physical.
            float effort = Mathf.Sin(Time.time * 13f) * 13f;
            float forward = mantling ? 8f : -16f;
            left.Drive(forward + effort, -6f);
            right.Drive(forward - effort, 6f);
        }

        public void ShowRemote(Quaternion leftPose, Quaternion rightPose)
        {
            left.ShowRemote(leftPose);
            right.ShowRemote(rightPose);
        }

        public void Dispose()
        {
            left.Dispose();
            right.Dispose();
            if (original) original.enabled = true;
            if (skin) Object.Destroy(skin.gameObject);
            if (mesh) Object.Destroy(mesh);
        }

        private sealed class Leg : IDisposable
        {
            private readonly Transform bone;
            private readonly Quaternion rest;
            private readonly Rigidbody rigid;
            private readonly CapsuleCollider collider;
            private readonly ConfigurableJoint hip;
            private Collider supportSurface;
            private Rigidbody supportBody;
            private Vector3 supportAnchor;
            private Vector3 footAnchor;
            public Quaternion Rotation => bone ? bone.localRotation : Quaternion.identity;
            public Collider Collider => collider;
            public bool HasSupport => supportSurface;
            private Vector3 SupportPoint => supportBody ? supportBody.transform.TransformPoint(supportAnchor) : supportAnchor;
            public Vector3 SupportVelocity => supportSurface && supportBody
                ? supportBody.GetPointVelocity(SupportPoint)
                : Vector3.zero;

            public Leg(Transform bone, Rigidbody torso, bool server)
            {
                this.bone = bone;
                rest = bone.localRotation;
                if (!server) return;
                rigid = bone.gameObject.AddComponent<Rigidbody>();
                rigid.mass = 0.45f;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                collider = bone.gameObject.AddComponent<CapsuleCollider>();
                collider.radius = 0.08f;
                collider.height = 0.23f;
                collider.center = new Vector3(0f, -0.14f, 0f);
                Physics.IgnoreCollision(collider, torso.GetComponent<Collider>());
                hip = bone.gameObject.AddComponent<ConfigurableJoint>();
                hip.connectedBody = torso;
                hip.autoConfigureConnectedAnchor = false;
                hip.anchor = Vector3.zero;
                hip.connectedAnchor = torso.transform.InverseTransformPoint(bone.position);
                hip.xMotion = hip.yMotion = hip.zMotion = ConfigurableJointMotion.Locked;
                hip.angularXMotion = hip.angularYMotion = hip.angularZMotion = ConfigurableJointMotion.Limited;
                hip.lowAngularXLimit = new SoftJointLimit { limit = -55f };
                hip.highAngularXLimit = new SoftJointLimit { limit = 55f };
                hip.angularYLimit = new SoftJointLimit { limit = 15f };
                hip.angularZLimit = new SoftJointLimit { limit = 15f };
                hip.enableCollision = false;
            }

            public void Drive(float angle, float side)
            {
                if (!rigid) return;
                Quaternion desired = bone.parent.rotation * rest * Quaternion.Euler(angle, 0f, side);
                if (supportSurface)
                {
                    Vector3 desiredFoot = bone.parent.InverseTransformPoint(SupportPoint) - bone.localPosition;
                    Vector3 restingFoot = rest * footAnchor;
                    if (desiredFoot.sqrMagnitude > 0.001f && restingFoot.sqrMagnitude > 0.001f)
                        desired = bone.parent.rotation * Quaternion.FromToRotation(restingFoot, desiredFoot) * rest;
                }
                Quaternion error = desired * Quaternion.Inverse(rigid.rotation);
                error.ToAngleAxis(out float difference, out Vector3 axis);
                if (difference > 180f) difference -= 360f;
                if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                    rigid.AddTorque(Vector3.ClampMagnitude(axis * (difference * Mathf.Deg2Rad * 95f) -
                        rigid.angularVelocity * 12f, 100f), ForceMode.Acceleration);
            }

            public bool FindGround(RaycastHit[] buffer, Transform player, out RaycastHit nearest)
            {
                nearest = default;
                if (!collider || !collider.enabled) return false;
                Vector3 origin = collider.bounds.center + Vector3.up * 0.08f;
                int count = Physics.SphereCastNonAlloc(origin, 0.05f, Vector3.down, buffer, 0.18f, ~0, QueryTriggerInteraction.Ignore);
                float distance = float.PositiveInfinity;
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = buffer[i];
                    if (hit.collider.transform.IsChildOf(player) || hit.normal.y < 0.55f || hit.distance >= distance) continue;
                    if (hit.rigidbody && !hit.rigidbody.isKinematic)
                    {
                        NetworkObject target = hit.rigidbody.GetComponentInParent<NetworkObject>();
                        if (!target || !target.IsSpawned || !target.IsOwnedByServer) continue;
                    }
                    nearest = hit;
                    distance = hit.distance;
                    found = true;
                }
                return found;
            }

            public bool TouchesGround(RaycastHit[] buffer, Transform player)
                => FindGround(buffer, player, out RaycastHit hit) && hit.point.y >= collider.bounds.min.y - 0.06f;

            public void SetSupport(bool planted, RaycastHit hit)
            {
                if (!planted) { ReleaseSupport(); return; }
                // Keep a pose target, rather than fastening the leg to the terrain.
                if (supportSurface == hit.collider && (SupportPoint - hit.point).sqrMagnitude <= 0.16f * 0.16f) return;
                supportSurface = hit.collider;
                supportBody = hit.rigidbody;
                footAnchor = rigid.transform.InverseTransformPoint(hit.point);
                supportAnchor = supportBody ? supportBody.transform.InverseTransformPoint(hit.point) : hit.point;
            }

            public void ReleaseSupport()
            {
                supportSurface = null;
                supportBody = null;
            }

            public void ShowRemote(Quaternion pose)
            {
                if (bone) bone.localRotation = Quaternion.Slerp(bone.localRotation, pose, 15f * Time.deltaTime);
            }

            public void Dispose()
            {
                ReleaseSupport();
                if (hip) Object.Destroy(hip);
                if (collider) Object.Destroy(collider);
                if (rigid) Object.Destroy(rigid);
                if (bone) bone.localRotation = rest;
            }
        }
    }
}
