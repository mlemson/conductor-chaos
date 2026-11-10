using UnityEngine;
using UnityEngine.InputSystem;

namespace ConductorChaos.Playground
{
    internal static class PlaygroundControls
    {
        public const float DefaultCameraDistance = 4f;
        public const float FirstPersonDistance = 0.8f;
        public const float GripPalmOffset = 0.05f;
        public const float PlantingReach = 0.15f;

        public static float ZoomDistance(float distance, float scroll)
        {
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer))
                scroll /= 120f;

            return Mathf.Clamp(distance - scroll * 0.35f, 0f, 6f);
        }

        public static float ZoomFieldOfView(float original, float distance)
        {
            return Mathf.Clamp(original + (distance - DefaultCameraDistance) * 2.5f, 45f, 85f);
        }

        public static float EaseCameraDistance(float current, float target, ref float velocity)
        {
            float distance = Mathf.SmoothDamp(current, target, ref velocity, 0.22f, 9f, Time.unscaledDeltaTime);
            if (Mathf.Abs(distance - target) > 0.005f) return distance;
            velocity = 0f;
            return target;
        }

        public static float FirstPersonBlend(float distance)
        {
            return 1f - Mathf.SmoothStep(0f, 1f, distance / FirstPersonDistance);
        }

        public static Quaternion HandGripAim(Quaternion aim, float side, float closeView)
        {
            return aim * Quaternion.Euler(0f, -side * 12f * closeView, 0f);
        }

        public static Vector3 FirstPersonEyeOffset(float yaw)
        {
            return Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, -0.18f, -0.16f);
        }

        public static bool CanPlantHand(Vector3 palm, Vector3 point, Vector3 normal, float extraReach = 0f)
        {
            float reach = PlantingReach + extraReach;
            return (point + normal * GripPalmOffset - palm).sqrMagnitude <= reach * reach;
        }

        public static ConfigurableJoint PlantHand(GameObject hand, Rigidbody support, Vector3 handAnchor,
            Vector3 point, Vector3 normal, float breakForce)
        {
            // A planted palm has one position on the surface, with free wrist rotation.
            Vector3 palm = point + normal * GripPalmOffset;
            var grip = hand.AddComponent<ConfigurableJoint>();
            grip.autoConfigureConnectedAnchor = false;
            grip.connectedBody = support;
            grip.anchor = handAnchor;
            grip.connectedAnchor = support ? support.transform.InverseTransformPoint(palm) : palm;
            grip.xMotion = grip.yMotion = grip.zMotion = ConfigurableJointMotion.Locked;
            grip.angularXMotion = grip.angularYMotion = grip.angularZMotion = ConfigurableJointMotion.Free;
            grip.breakForce = breakForce;
            return grip;
        }

        public static void ReleaseGrip(Joint grip)
        {
            if (!grip) return;
            if (grip is SpringJoint spring)
            {
                spring.spring = 0f;
                spring.damper = 0f;
            }
            else if (grip is ConfigurableJoint planted)
            {
                planted.xMotion = planted.yMotion = planted.zMotion = ConfigurableJointMotion.Free;
            }
            Object.Destroy(grip);
        }

        public static void SlideWallGrip(Joint grip, Collider surface, ref Vector3 surfaceNormal)
        {
            if (!grip || !surface || !surface.enabled || !surface.gameObject.activeInHierarchy
                || surfaceNormal.sqrMagnitude < 0.5f) return;

            var support = grip.connectedBody;
            if (support && !support.isKinematic) return;
            Vector3 normal = surface.transform.TransformDirection(surfaceNormal);
            if (Mathf.Abs(normal.y) > 0.35f) return;

            Vector3 anchor = support ? support.transform.TransformPoint(grip.connectedAnchor) : grip.connectedAnchor;
            if ((grip.transform.TransformPoint(grip.anchor) - anchor).sqrMagnitude > 0.30f * 0.30f) return;
            Vector3 step = Vector3.ProjectOnPlane(Vector3.down, normal).normalized * (0.25f * Time.fixedDeltaTime);
            Vector3 point = grip is ConfigurableJoint ? anchor - normal * GripPalmOffset : anchor;
            var probe = new Ray(point + step + normal * 0.10f, -normal);
            // Stay on the same wall and do not carry the grip around a sharp edge or across a gap.
            if (!surface.Raycast(probe, out RaycastHit hit, 0.20f) || Mathf.Abs(hit.normal.y) > 0.35f
                || Vector3.Dot(normal, hit.normal) < 0.75f || hit.point.y >= point.y) return;

            Vector3 palm = grip is ConfigurableJoint ? hit.point + hit.normal * GripPalmOffset : hit.point;
            grip.connectedAnchor = support ? support.transform.InverseTransformPoint(palm) : palm;
            surfaceNormal = surface.transform.InverseTransformDirection(hit.normal);
        }

        public static float HandPushAcceleration(Joint grip, Collider surface, float press, float upSpeed)
        {
            if (press <= 0f || !grip || !surface || !surface.enabled || !surface.gameObject.activeInHierarchy)
                return 0f;

            var support = grip.connectedBody;
            if (support && !support.isKinematic) return 0f;

            Vector3 anchor = support ? support.transform.TransformPoint(grip.connectedAnchor) : grip.connectedAnchor;
            Vector3 hand = grip.transform.TransformPoint(grip.anchor);
            if ((hand - anchor).sqrMagnitude > 0.30f * 0.30f) return 0f;

            // Stop extending once the shoulder reaches the height the planted arm can support.
            Vector3 shoulderOffset = grip.transform.position - anchor;
            float armLength = grip.transform.TransformVector(grip.anchor).magnitude;
            float reach = armLength * Mathf.Lerp(0.55f, 0.96f, press);
            float horizontal = shoulderOffset.x * shoulderOffset.x + shoulderOffset.z * shoulderOffset.z;
            float gripSlack = armLength + 0.12f;
            if (horizontal > gripSlack * gripSlack) return 0f;

            // An almost horizontal arm can start pressing too; the grip draws the shoulder inward as it rises.
            float reachSquared = reach * reach;
            float rise = Mathf.Sqrt(reachSquared - Mathf.Min(horizontal, reachSquared * 0.85f)) - shoulderOffset.y;
            if (rise < -0.08f) return 0f;
            float targetSpeed = Mathf.Clamp(rise * 6f, -0.5f, 1.6f);
            float supportSpeed = support ? support.GetPointVelocity(anchor).y : 0f;
            return Mathf.Clamp((targetSpeed - (upSpeed - supportSpeed)) * 9f - Physics.gravity.y, 0f, 22f) * press;
        }
    }
}
