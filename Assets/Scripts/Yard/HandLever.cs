// Conductor Chaos — hendel, handwiel of kraantje dat je met je handen verdraait (issues #232-#237). Zelfde opzet als
// SwingDoor: een Rigidbody aan een scharnier om één as, je pakt hem vast en trekt of duwt. Het bereik komt uit de
// lege Pose_-knopen in het model (zie ArtSource/Environment/Station/yard_props.py): Configure(van, naar) zet de
// beginstand, de as en de hoek. Normalized = 0 bij de eerste stand, 1 bij de tweede.
// springBack: veert terug naar de ruststand (kraanhendels); anders blijft hij staan waar je hem loslaat.
// Netwerk: NetworkObject + NetworkTransform (lokaal) + NetworkRigidbody, gezet door de prefabbouwer; de server
// simuleert, op clients is het lijf kinematisch en volgt dit script alleen de stand.
using UnityEngine;

namespace ConductorChaos.Yard
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HandLever : MonoBehaviour
    {
        [Tooltip("Lokale rotatie in de ruststand (0 graden).")]
        [SerializeField] Quaternion restRotation = Quaternion.identity;
        [Tooltip("Draaias in de eigen ruimte van de hendel.")]
        [SerializeField] Vector3 axis = Vector3.forward;
        [SerializeField] float minAngle;
        [SerializeField] float maxAngle = 90f;
        [Tooltip("Hoek die als 0 (Normalized) telt; de tweede stand is 1.")]
        [SerializeField] float fromAngle;
        [SerializeField] float toAngle = 90f;
        [SerializeField] bool springBack;
        [SerializeField] float springStrength = 30f;
        [SerializeField] float damping = 4f;

        Rigidbody body;
        HingeJoint hinge;

        /// <summary>Hoek vanaf de ruststand om de as (graden).</summary>
        public float Angle => TwistAngle(Quaternion.Inverse(restRotation) * transform.localRotation, axis.normalized);

        /// <summary>0 = eerste stand, 1 = tweede stand (kan buiten 0..1 komen bij een driestandenhendel).</summary>
        public float Normalized => Mathf.Approximately(toAngle, fromAngle) ? 0f : (Angle - fromAngle) / (toAngle - fromAngle);
        public float Normalized01 => Mathf.Clamp01(Normalized);

        /// <summary>Gezet door de prefabbouwer: van = eerste Pose (lokale rotatie), naar = tweede Pose.</summary>
        public void Configure(Quaternion from, Quaternion to, bool returnToFrom, Quaternion? extra = null)
        {
            restRotation = from;
            (Quaternion.Inverse(from) * to).ToAngleAxis(out float span, out Vector3 ax);
            if (span > 180f) { span = 360f - span; ax = -ax; }
            axis = ax.sqrMagnitude > 1e-6f ? ax.normalized : Vector3.forward;
            fromAngle = 0f;
            toAngle = span;
            minAngle = Mathf.Min(0f, span);
            maxAngle = Mathf.Max(0f, span);
            if (extra.HasValue)                          // derde stand aan de andere kant (bv. kraanhendel Back)
            {
                float other = TwistAngle(Quaternion.Inverse(from) * extra.Value, axis);
                minAngle = Mathf.Min(minAngle, other);
                maxAngle = Mathf.Max(maxAngle, other);
            }
            springBack = returnToFrom;
        }

        internal void PresentAngle(float angle) => transform.localRotation = restRotation * Quaternion.AngleAxis(angle, axis);

        public static float TwistAngle(Quaternion q, Vector3 axis)
        {
            Vector3 v = new(q.x, q.y, q.z);
            float p = Vector3.Dot(v, axis);
            float angle = 2f * Mathf.Atan2(p, q.w) * Mathf.Rad2Deg;
            return Mathf.DeltaAngle(0f, angle);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.angularDamping = damping;
        }

        void Start()
        {
            // vlak langs het vaste onderstel: die botsvormen mogen de hendel niet klemzetten
            var root = transform.parent ? transform.parent : transform;
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var other in root.GetComponentsInChildren<Collider>(true))
            {
                if (other.attachedRigidbody == body) continue;
                if (other.attachedRigidbody && other.attachedRigidbody != root.GetComponentInParent<Rigidbody>()) continue;
                foreach (var own in mine) Physics.IgnoreCollision(own, other, true);
            }
        }

        void FixedUpdate()
        {
            if (!body || body.isKinematic) return;
            if (!hinge) CreateHinge();
            Vector3 worldAxis = WorldAxis();
            float angle = Angle;
            float rate = Vector3.Dot(body.angularVelocity, worldAxis) * Mathf.Rad2Deg;
            float clamped = Mathf.Clamp(angle, minAngle, maxAngle);
            if (!Mathf.Approximately(clamped, angle))
            {
                if ((angle < minAngle && rate < 0f) || (angle > maxAngle && rate > 0f))
                    body.angularVelocity -= worldAxis * (rate * Mathf.Deg2Rad);
                var parentRotation = transform.parent ? transform.parent.rotation : Quaternion.identity;
                body.MoveRotation(parentRotation * restRotation * Quaternion.AngleAxis(clamped, axis.normalized));
                angle = clamped;
            }
            if (springBack)
                body.AddTorque(worldAxis * (-angle * Mathf.Deg2Rad * springStrength - rate * Mathf.Deg2Rad * 3f), ForceMode.Acceleration);
        }

        Vector3 WorldAxis()
        {
            var parentRotation = transform.parent ? transform.parent.rotation : Quaternion.identity;
            return (parentRotation * restRotation * axis).normalized;
        }

        void CreateHinge()
        {
            hinge = gameObject.AddComponent<HingeJoint>();
            hinge.autoConfigureConnectedAnchor = true;
            hinge.anchor = Vector3.zero;
            // as in de eigen ruimte: om de as draaien laat hem op zijn plek, dus de rust-as is ook nu de as
            hinge.axis = axis.normalized;
            hinge.connectedBody = transform.parent ? transform.parent.GetComponentInParent<Rigidbody>() : null;
            hinge.enableCollision = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Gizmos.DrawRay(transform.position, WorldAxis() * 0.3f);
        }
    }
}
