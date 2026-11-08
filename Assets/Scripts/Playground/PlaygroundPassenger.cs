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
        private Rigidbody body, leftArm, rightArm;
        private Transform leftArmVisual, rightArmVisual;
        private ConfigurableJoint leftShoulder, rightShoulder;
        private CapsuleCollider leftCollider, rightCollider;
        private PlaygroundLegRig legs;
        private float nextPose;

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
            leftArmVisual = left;
            rightArmVisual = right;
            leftArm = MakeArm(left, out leftShoulder, out leftCollider);
            rightArm = MakeArm(right, out rightShoulder, out rightCollider);
            if (leftCollider && rightCollider) Physics.IgnoreCollision(leftCollider, rightCollider);
            legs = PlaygroundLegRig.Create(model, body, IsServer);
            if (IsServer)
            {
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

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !body || !leftArm || !rightArm) return;
            bool grounded = false;
            int count = Physics.SphereCastNonAlloc(body.position + Vector3.up * 0.3f, 0.16f, Vector3.down,
                groundHits, 0.34f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!groundHits[i].collider.transform.IsChildOf(transform) && groundHits[i].normal.y > 0.55f) { grounded = true; break; }
            bool standing = grounded && Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > 0.55f;
            body.AddTorque(Vector3.up * (-body.angularVelocity.y * 4f), ForceMode.Acceleration);
            if (standing)
            {
                Vector3 tilt = Vector3.Cross(body.rotation * Vector3.up, Vector3.up);
                body.AddTorque(tilt * 28f - new Vector3(body.angularVelocity.x, 0f, body.angularVelocity.z) * 7f, ForceMode.Acceleration);
                legs?.Step(body.linearVelocity, Vector2.zero, true, 0f, false, out _);
            }
            else legs?.ReleaseSupport();
            if (Time.time < nextPose) return;
            nextPose = Time.time + 0.05f;
            leftArmPose.Value = leftArm.transform.localRotation;
            rightArmPose.Value = rightArm.transform.localRotation;
            if (legs != null) { leftLegPose.Value = legs.LeftRotation; rightLegPose.Value = legs.RightRotation; }
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
