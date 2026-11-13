// Conductor Chaos — moersleutel (issue #215): pak hem op met je hand en ram de kop hard tegen de uitstekende bout van
// een kapotte wissel of een kapot spoorstuk. Lomp en fysiek: alleen een echte klap met de kop telt (de server meet de
// botssnelheid en kijkt of de bout bij de kop van de sleutel geraakt is). Zelf geen netwerkstate: de RepairPoint telt.
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(10)]
    public sealed class RepairWrench : NetworkBehaviour
    {
        [Tooltip("Hoe ver het raakpunt van de kop van de sleutel mag liggen (m).")]
        [SerializeField] float headRadius = 0.16f;

        Transform head;
        PlaygroundPlayer holder;
        Transform holdingHand;
        Rigidbody holdingPalm;
        Collider[] toolColliders, holderColliders;
        Vector3 gripAnchor;
        Quaternion headFrame;
        float freeAngularSpeed;
        readonly ContactPoint[] points = new ContactPoint[8];

        internal Rigidbody Body { get; private set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            head = RepairPoint.FindDeep(transform, "Wrench_Head");
            if (!head) return;
            Vector3 headOffset = transform.InverseTransformPoint(head.position);
            // The centre of the yellow grip is 0.045 m behind the model origin.
            Vector3 axis = headOffset.normalized;
            gripAnchor = -axis * 0.045f;
            headFrame = Quaternion.LookRotation(axis, Vector3.up);
            toolColliders = GetComponentsInChildren<Collider>();
        }

        internal bool TryHold(PlaygroundPlayer player, Transform hand)
        {
            if (!IsServer || !IsSpawned || !head || !player || holdingHand is not null) return false;
            holder = player;
            holdingHand = hand;
            holdingPalm = hand.GetComponent<Rigidbody>();
            freeAngularSpeed = Body.maxAngularVelocity;
            Body.maxAngularVelocity = 18f;
            holderColliders = player.GetComponentsInChildren<Collider>();
            SetHolderCollisions(true);
            return true;
        }

        internal void Release(Transform hand)
        {
            if (holdingHand != hand) return;
            SetHolderCollisions(false);
            Body.maxAngularVelocity = freeAngularSpeed;
            holderColliders = null;
            holder = null;
            holdingHand = null;
            holdingPalm = null;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && holdingHand is not null) Release(holdingHand);
        }

        void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || holdingHand is null) return;
            if (!holder || !holder.IsSpawned ||
                !holder.TryGetControlHand(Body, out var hand, out var palm, out _) || hand != holdingHand)
            {
                Release(holdingHand);
                return;
            }

            Quaternion target = Quaternion.LookRotation(holder.CabAimForward, Vector3.up) * Quaternion.Inverse(headFrame);
            Quaternion error = target * Quaternion.Inverse(Body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            Body.angularVelocity = Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * 18f), 18f);
            Vector3 grip = Body.position + Body.rotation * Vector3.Scale(gripAnchor, transform.lossyScale);
            // Drive the grip to the palm while leaving the body dynamic for real bolt impacts.
            Vector3 palmVelocity = holdingPalm.GetPointVelocity(palm);
            Body.linearVelocity = palmVelocity + Vector3.ClampMagnitude((palm - grip) * 22f, 7f)
                - Vector3.Cross(Body.angularVelocity, grip - Body.worldCenterOfMass);
        }

        void SetHolderCollisions(bool ignore)
        {
            foreach (var part in holderColliders)
                foreach (var toolCollider in toolColliders)
                    if (part && toolCollider) Physics.IgnoreCollision(part, toolCollider, ignore);
        }

        void OnCollisionEnter(Collision collision) => Check(collision);

        void OnCollisionStay(Collision collision)
        {
            // een sleutel die al tegen de bout rust en dan een nieuwe klap krijgt, telt ook
            if (collision.relativeVelocity.sqrMagnitude > 1f) Check(collision);
        }

        void Check(Collision collision)
        {
            if (!IsServer || !IsSpawned) return;
            var point = collision.collider.GetComponentInParent<RepairPoint>();
            if (!point || collision.collider != point.BoltCollider) return;
            if (head)
            {
                bool nearHead = false;
                int n = collision.GetContacts(points);
                for (int i = 0; i < n && !nearHead; i++)
                    nearHead = (points[i].point - head.position).sqrMagnitude <= headRadius * headRadius;
                if (!nearHead) return;
            }
            point.ServerHit(collision.relativeVelocity.magnitude);
        }
    }
}
