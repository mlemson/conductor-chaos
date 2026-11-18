using ConductorChaos.Characters;
using ConductorChaos.Nature;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlaygroundPassenger : NetworkBehaviour
    {
        private readonly NetworkVariable<Quaternion> leftArmPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightArmPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> leftLegPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightLegPose = new(Quaternion.identity);
        private readonly RaycastHit[] groundHits = new RaycastHit[24];
        // Winter (#243): hoe koud (0..100) en of de reiziger bevroren is. De server rekent, iedereen toont het ijs.
        private readonly NetworkVariable<byte> coldPercent = new();
        private readonly NetworkVariable<bool> frozen = new();
        private float cold;
        private GameObject iceShell;
        private Rigidbody body, leftArm, rightArm;
        private Transform leftArmVisual, rightArmVisual;
        private Pose leftArmRest, rightArmRest;
        private bool boarding, seated;
        private float boardingStride;
        private ConfigurableJoint leftShoulder, rightShoulder;
        private CapsuleCollider leftCollider, rightCollider;
        private PlaygroundLegRig legs;
        private CharacterFace face;
        private Vector3 waitingForward;
        private float nextPose;
        private float fallenSince = -1f;
        private const float GetUpDelay = 1.2f;

        /// <summary>Bevroren: staat stijf als een blok ijs, stapt niet in en moet eerst naar de warmte.</summary>
        internal bool IsFrozen => IsSpawned && frozen.Value;
        /// <summary>Hoe koud (0..1), zoals de server het doorgeeft.</summary>
        internal float Cold => coldPercent.Value / 100f;

        private void Awake()
        {
            if (name != "RailMechanic" || transform.childCount == 0 || !GetComponent<PassengerTicket>()) return;
            // Older service scenes have a mechanic authored in this passenger slot.
            var model = Resources.Load<GameObject>("ServiceBusinessTraveler");
            if (!model) { Debug.LogError("Vervangend reizigersmodel ontbreekt.", this); return; }
            Transform previous = transform.GetChild(0);
            var replacement = Instantiate(model, transform, false).transform;
            replacement.SetLocalPositionAndRotation(previous.localPosition, previous.localRotation);
            replacement.localScale = previous.localScale;
            replacement.SetSiblingIndex(0);
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
            name = "BusinessTraveler";
        }

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            Transform model = transform.childCount > 0 ? transform.GetChild(0) : null;
            Transform left = null, right = null;
            if (model)
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                {
                    if (part.name == "Arm_L") left = part;
                    if (part.name == "Arm_R") right = part;
                }
            if (!model || !left || !right) { Debug.LogError("Passenger model or arm anchors missing.", this); return; }
            face = CharacterFace.OnModel(model);
            waitingForward = transform.forward;
            leftArmVisual = left;
            rightArmVisual = right;
            leftArmRest = new Pose(left.localPosition, left.localRotation);
            rightArmRest = new Pose(right.localPosition, right.localRotation);
            boarding = false;
            seated = false;
            boardingStride = 0f;
            leftArm = MakeArm(left, out leftShoulder, out leftCollider);
            rightArm = MakeArm(right, out rightShoulder, out rightCollider);
            if (leftCollider && rightCollider) Physics.IgnoreCollision(leftCollider, rightCollider);
            legs = PlaygroundLegRig.Create(model, body, IsServer);
            frozen.OnValueChanged += OnFrozenChanged;
            if (frozen.Value) ShowFrozen(true);
            if (IsServer)
            {
                // Passengers stand on their own balance: they can be dragged, pushed over and get back up.
                body.constraints = RigidbodyConstraints.None;
                body.centerOfMass = new Vector3(0f, 0.35f, 0f);
                leftArmPose.Value = left.localRotation;
                rightArmPose.Value = right.localRotation;
                if (legs != null) { leftLegPose.Value = legs.LeftRotation; rightLegPose.Value = legs.RightRotation; }
            }
        }

        private Rigidbody MakeArm(Transform arm, out ConfigurableJoint shoulder, out CapsuleCollider capsule)
        {
            shoulder = null;
            capsule = null;
            if (!IsServer) return null;
            var rigid = arm.gameObject.AddComponent<Rigidbody>();
            rigid.mass = 0.35f;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;
            capsule = arm.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, -0.18f, 0f);
            capsule.height = 0.27f;
            capsule.radius = 0.07f;
            Physics.IgnoreCollision(capsule, GetComponent<CapsuleCollider>());
            shoulder = arm.gameObject.AddComponent<ConfigurableJoint>();
            shoulder.connectedBody = body;
            shoulder.autoConfigureConnectedAnchor = false;
            shoulder.connectedAnchor = body.transform.InverseTransformPoint(arm.position);
            shoulder.xMotion = shoulder.yMotion = shoulder.zMotion = ConfigurableJointMotion.Locked;
            shoulder.angularXMotion = shoulder.angularYMotion = shoulder.angularZMotion = ConfigurableJointMotion.Limited;
            shoulder.lowAngularXLimit = new SoftJointLimit { limit = -80f };
            shoulder.highAngularXLimit = new SoftJointLimit { limit = 80f };
            shoulder.angularYLimit = new SoftJointLimit { limit = 45f };
            shoulder.angularZLimit = new SoftJointLimit { limit = 65f };
            return rigid;
        }

        internal void BeginBoarding()
        {
            boarding = true;
            body.isKinematic = true;
            ReleaseArm(leftArm, leftShoulder, leftCollider, leftArmRest);
            ReleaseArm(rightArm, rightShoulder, rightCollider, rightArmRest);
            leftArmPose.Value = leftArmRest.rotation;
            rightArmPose.Value = rightArmRest.rotation;
            legs?.EndPassengerPhysics();
            PoseLegs(0f, 0f);
        }

        private static void ReleaseArm(Rigidbody arm, ConfigurableJoint shoulder, CapsuleCollider collider, Pose rest)
        {
            if (!arm) return;
            if (collider) { collider.enabled = false; Destroy(collider); }
            if (shoulder) Destroy(shoulder);
            arm.interpolation = RigidbodyInterpolation.None;
            arm.isKinematic = true;
            arm.transform.SetLocalPositionAndRotation(rest.position, rest.rotation);
            Destroy(arm);
        }

        internal void PoseSeated()
        {
            seated = true;
            PoseLegs(-72f, -72f);
        }

        private void PoseLegs(float left, float right)
        {
            if (legs == null) return;
            legs.PosePassenger(left, right);
            leftLegPose.Value = legs.LeftRotation;
            rightLegPose.Value = legs.RightRotation;
        }

        internal bool WalkToward(Vector3 destination)
        {
            if (IsFrozen) return false;
            if (!body.isKinematic) body.isKinematic = true;
            Vector3 from = body.position;
            Vector3 delta = destination - from;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > .008f)
                body.MoveRotation(Quaternion.RotateTowards(body.rotation, Quaternion.LookRotation(flat), 360f * Time.fixedDeltaTime));
            Vector3 next = Vector3.MoveTowards(from, destination, .9f * Time.fixedDeltaTime);
            body.MovePosition(next);
            boardingStride += (next - from).magnitude * 12f;
            float swing = Mathf.Sin(boardingStride) * 18f;
            PoseLegs(swing, -swing);
            return delta.sqrMagnitude < .018f * .018f;
        }

        private void OnFrozenChanged(bool before, bool now) => ShowFrozen(now);

        private void ShowFrozen(bool on)
        {
            if (!iceShell && on) iceShell = IceShell.For(transform);
            if (iceShell) iceShell.SetActive(on);
        }

        // Server: buiten in de kou loopt de kou op, in de warmte (WarmZone, of een wagon met dichte deuren) zakt hij.
        private void UpdateCold()
        {
            var winter = WinterLevel.Active;
            if (!winter || !body) return;
            float rate;
            if (WarmZone.Contains(body.position)) rate = -1f / winter.ThawSeconds;
            else
            {
                var floor = ShelteredVehicle();
                var train = TrainDrive.FindControlOwner(floor);
                rate = !floor ? 1f / winter.FreezeSeconds
                    : train && train.DoorsClosedOn(floor) ? -1f / winter.ThawSeconds : winter.OpenDoorFactor / winter.FreezeSeconds;
            }
            cold = Mathf.Clamp01(cold + rate * Time.fixedDeltaTime);
            if (!frozen.Value && cold >= 1f)
            {
                frozen.Value = true;
                if (boarding && !seated) body.isKinematic = false;
            }
            else if (frozen.Value && cold <= .55f) frozen.Value = false;
            byte percent = (byte)Mathf.RoundToInt(cold * 100f);
            if (Mathf.Abs(percent - coldPercent.Value) >= 2 || (percent == 0) != (coldPercent.Value == 0)) coldPercent.Value = percent;
        }

        private Rigidbody ShelteredVehicle()
        {
            int count = Physics.RaycastNonAlloc(body.position + Vector3.up * .4f, Vector3.down, groundHits, 1.3f, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody floor = null;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.distance >= nearest || hit.normal.y < .45f || !TrainDrive.IsTrainBody(hit.rigidbody)) continue;
                floor = hit.rigidbody;
                nearest = hit.distance;
            }
            if (!floor) return null;
            count = Physics.RaycastNonAlloc(body.position + Vector3.up * .45f, Vector3.up, groundHits, 3.5f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (groundHits[i].rigidbody == floor) return floor;
            return null;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            UpdateCold();
            if (boarding || !body || !leftArm || !rightArm) return;
            bool grounded = false;
            int count = Physics.SphereCastNonAlloc(body.position + Vector3.up * 0.3f, 0.16f, Vector3.down,
                groundHits, 0.34f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!groundHits[i].collider.transform.IsChildOf(transform) && groundHits[i].normal.y > 0.55f) { grounded = true; break; }
            bool upright = Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > 0.55f;
            bool standing = grounded && upright && !frozen.Value;          // bevroren: stijf, geen evenwicht meer
            if (!body.isKinematic)
            {
                float turn = standing && body.linearVelocity.sqrMagnitude < .09f ? GazeTurn() : 0f;
                body.AddTorque(Vector3.up * (turn - body.angularVelocity.y * 4f), ForceMode.Acceleration);
            }
            if (standing && !body.isKinematic)
            {
                Vector3 tilt = Vector3.Cross(body.rotation * Vector3.up, Vector3.up);
                body.AddTorque(tilt * 28f - new Vector3(body.angularVelocity.x, 0f, body.angularVelocity.z) * 7f, ForceMode.Acceleration);
                legs?.Step(body.linearVelocity, Vector2.zero, true, 0f, false, out _);
            }
            else legs?.ReleaseSupport();
            // Knocked over and left alone: after a moment the passenger rolls back onto their feet.
            if (upright || body.isKinematic || frozen.Value) fallenSince = -1f;
            else if (fallenSince < 0f)
            {
                if (body.linearVelocity.sqrMagnitude < 0.6f * 0.6f) fallenSince = Time.time;   // wait until they lie still
            }
            else if (Time.time - fallenSince >= GetUpDelay)
            {
                Vector3 tilt = Vector3.Cross(body.rotation * Vector3.up, Vector3.up);
                if (tilt.sqrMagnitude < 0.0004f) tilt = body.rotation * Vector3.right * 0.2f;   // flat on the head: pick a side
                body.AddTorque(tilt * 55f - new Vector3(body.angularVelocity.x, 0f, body.angularVelocity.z) * 5f, ForceMode.Acceleration);
            }
            if (Time.time < nextPose) return;
            nextPose = Time.time + 0.05f;
            leftArmPose.Value = leftArm.transform.localRotation;
            rightArmPose.Value = rightArm.transform.localRotation;
            if (legs != null) { leftLegPose.Value = legs.LeftRotation; rightLegPose.Value = legs.RightRotation; }
        }

        private float GazeTurn()
        {
            if (!face || !face.TryGetGazeOffset(out var offset) || offset.sqrMagnitude > 9f) return 0f;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && (player.TryGetControlHand(body, out _, out _, out _) ||
                    player.TryGetControlHand(leftArm, out _, out _, out _) ||
                    player.TryGetControlHand(rightArm, out _, out _, out _))) return 0f;

            float bearing = Vector3.SignedAngle(waitingForward, Vector3.ProjectOnPlane(offset, Vector3.up), Vector3.up);
            if (Mathf.Abs(bearing) > 110f) return 0f;
            Vector3 wanted = Quaternion.AngleAxis(Mathf.Clamp(bearing, -24f, 24f), Vector3.up) * waitingForward;
            float error = Vector3.SignedAngle(Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up), wanted, Vector3.up);
            return Mathf.Clamp(error * Mathf.Deg2Rad * 8f, -3f, 3f);
        }

        private void LateUpdate()
        {
            if (!IsSpawned || IsServer || !leftArmVisual || !rightArmVisual) return;
            leftArmVisual.localRotation = leftArmPose.Value;
            rightArmVisual.localRotation = rightArmPose.Value;
            legs?.ShowRemote(leftLegPose.Value, rightLegPose.Value);
        }

        public override void OnNetworkDespawn()
        {
            frozen.OnValueChanged -= OnFrozenChanged;
            legs?.Dispose();
            legs = null;
            if (leftShoulder) Destroy(leftShoulder);
            if (rightShoulder) Destroy(rightShoulder);
            if (leftCollider) Destroy(leftCollider);
            if (rightCollider) Destroy(rightCollider);
            if (leftArm) Destroy(leftArm);
            if (rightArm) Destroy(rightArm);
        }
    }
}
