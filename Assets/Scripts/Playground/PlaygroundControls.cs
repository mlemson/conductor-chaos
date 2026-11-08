using UnityEngine;
using UnityEngine.InputSystem;

namespace ConductorChaos.Playground
{
    internal static class PlaygroundControls
    {
        public const float DefaultCameraDistance = 4f;

        public static float ZoomDistance(float distance, float scroll)
        {
            if (InputSystem.settings.scrollDeltaBehavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                && (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer))
                scroll /= 120f;

            return Mathf.Clamp(distance - scroll * 0.35f, 1.5f, 6f);
        }

        public static float ZoomFieldOfView(float original, float distance)
        {
            float halfAngle = original * 0.5f * Mathf.Deg2Rad;
            float zoomed = 2f * Mathf.Atan(Mathf.Tan(halfAngle) * distance / DefaultCameraDistance) * Mathf.Rad2Deg;
            return Mathf.Clamp(zoomed, 30f, 85f);
        }

        public static float HandPushAcceleration(SpringJoint grip, Collider surface, float press, float upSpeed)
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
