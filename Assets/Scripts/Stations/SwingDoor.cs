// Conductor Chaos — draaiend blad dat je met je handen open en dicht doet: stationsdeur, kluisdeurtje, staldeur,
// hekpoort. Het blad is een gewone Rigidbody aan een scharnier (Y-as van de knoop, pivot op het scharnier), net als
// de losse stationsprops: je duwt ertegen met je lijf of pakt het vast met een hand en trekt.
// Netwerk: zelfde opzet als de losse props (NetworkObject + NetworkTransform + NetworkRigidbody, gezet door de
// prefabbouwer in StationKitEditor). De server simuleert; op clients is het lijf kinematisch en doet dit script niets.
//
// De uiterste standen komen uit het model: Hinge_<naam>_Closed en Hinge_<naam>_Open (zie
// ArtSource/Environment/Station/station_props.py, swing_door). De prefabbouwer zet ze om naar closedRotation en
// openYaw; de stand waarin het blad in de scène staat is de beginstand.
using UnityEngine;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SwingDoor : MonoBehaviour
    {
        [Tooltip("Lokale rotatie van het blad als het dicht is.")]
        [SerializeField] Quaternion closedRotation = Quaternion.identity;
        [Tooltip("Hoek (graden om de eigen Y-as, met teken) van dicht naar helemaal open.")]
        [SerializeField] float openYaw = 100f;
        [Tooltip("Binnen deze hoek van 'dicht' valt het blad zacht in het slot.")]
        [SerializeField] float latchAngle = 7f;
        [Tooltip("Hoe stevig het slot het blad dichthoudt (hoekversnelling per radiaal).")]
        [SerializeField] float latchStrength = 45f;
        [Tooltip("Demping van de draaiing: hoger = het blad blijft eerder staan waar je het loslaat.")]
        [SerializeField] float damping = 2.5f;

        Rigidbody body;
        HingeJoint hinge;
        float lo, hi;
        bool driven;
        float driveTarget;

        /// <summary>Hoek vanaf dicht, in graden (teken zoals openYaw).</summary>
        public float Yaw
        {
            get
            {
                var rel = Quaternion.Inverse(closedRotation) * transform.localRotation;
                return Mathf.DeltaAngle(0f, rel.eulerAngles.y);
            }
        }

        /// <summary>0 = dicht, 1 = helemaal open.</summary>
        public float Openness => Mathf.Approximately(openYaw, 0f) ? 0f : Mathf.Clamp01(Yaw / openYaw);
        public bool IsClosed => Openness < 0.06f;
        public bool IsOpen => Openness > 0.5f;

        /// <summary>Gezet door de prefabbouwer (of vanuit code voor eigen deuren).</summary>
        public void Configure(Quaternion closedLocalRotation, float signedOpenYaw)
        {
            closedRotation = closedLocalRotation;
            openYaw = signedOpenYaw;
            lo = Mathf.Min(0f, openYaw);
            hi = Mathf.Max(0f, openYaw);
        }

        /// <summary>Server: laat het blad vanzelf open of dicht draaien (bijvoorbeeld voor een les of een script).</summary>
        public void Swing(bool open)
        {
            driven = true;
            driveTarget = open ? openYaw : 0f;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;                     // het scharnier draagt het blad
            body.angularDamping = damping;
            lo = Mathf.Min(0f, openYaw);
            hi = Mathf.Max(0f, openYaw);
        }

        void Start()
        {
            // Het blad draait vlak langs kozijn, kast en vloer: die vaste botsvormen mogen het niet klemzetten.
            // De uiterste standen bewaakt dit script zelf.
            var root = transform.parent ? transform.parent : transform;
            var mine = GetComponentsInChildren<Collider>(true);
            foreach (var other in root.GetComponentsInChildren<Collider>(true))
            {
                if (other.attachedRigidbody) continue;
                foreach (var own in mine) Physics.IgnoreCollision(own, other, true);
            }
        }

        void FixedUpdate()
        {
            if (!body || body.isKinematic) return;       // client of nog niet gespawnd: NetworkTransform bepaalt de stand
            if (!hinge) CreateHinge();

            Vector3 axis = Axis();
            float yaw = Yaw;
            float rate = Vector3.Dot(body.angularVelocity, axis);          // rad/s, positief = yaw neemt toe

            float clamped = Mathf.Clamp(yaw, lo, hi);
            if (!Mathf.Approximately(clamped, yaw))
            {
                if ((yaw < lo && rate < 0f) || (yaw > hi && rate > 0f)) body.angularVelocity -= axis * rate;
                var parentRotation = transform.parent ? transform.parent.rotation : Quaternion.identity;
                body.MoveRotation(parentRotation * closedRotation * Quaternion.Euler(0f, clamped, 0f));
                yaw = clamped;
            }

            if (driven)
            {
                float error = Mathf.DeltaAngle(yaw, driveTarget);
                if (Mathf.Abs(error) < 1.5f) driven = false;
                else body.AddTorque(axis * (Mathf.Clamp(error, -40f, 40f) * Mathf.Deg2Rad * 30f - rate * 6f), ForceMode.Acceleration);
            }
            else if (Mathf.Abs(yaw) < latchAngle)
            {
                body.AddTorque(axis * (-yaw * Mathf.Deg2Rad * latchStrength), ForceMode.Acceleration);
            }
        }

        Vector3 Axis()
        {
            Vector3 local = closedRotation * Vector3.up;
            return (transform.parent ? transform.parent.TransformDirection(local) : local).normalized;
        }

        void CreateHinge()
        {
            hinge = gameObject.AddComponent<HingeJoint>();
            hinge.autoConfigureConnectedAnchor = true;
            hinge.anchor = Vector3.zero;                 // pivot van de knoop = scharnier
            hinge.axis = Vector3.up;
            hinge.connectedBody = transform.parent ? transform.parent.GetComponentInParent<Rigidbody>() : null;
            hinge.enableCollision = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            var parentRotation = transform.parent ? transform.parent.rotation : Quaternion.identity;
            foreach (float a in new[] { 0f, openYaw })
                Gizmos.DrawRay(transform.position, parentRotation * closedRotation * Quaternion.Euler(0f, a, 0f) * Vector3.right * 0.8f);
        }
    }
}
