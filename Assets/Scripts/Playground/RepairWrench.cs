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
    public sealed class RepairWrench : NetworkBehaviour
    {
        [Tooltip("Hoe ver het raakpunt van de kop van de sleutel mag liggen (m).")]
        [SerializeField] float headRadius = 0.16f;

        Transform head;
        readonly ContactPoint[] points = new ContactPoint[8];

        void Awake()
        {
            head = RepairPoint.FindDeep(transform, "Wrench_Head");
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
