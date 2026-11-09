// Conductor Chaos — kaartjestang (issue #216): pak hem op en tik met de bek hard genoeg tegen het kaartje dat een
// reiziger omhoog houdt; dan zit er een gaatje in. Lomp maar echt: mis je of tik je te zacht, dan gebeurt er niets.
// De server meet de klap; de reiziger (PassengerTicket) houdt de stand bij.
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TicketPunch : NetworkBehaviour
    {
        [Tooltip("Hoe ver het raakpunt van de bek van de tang mag liggen (m).")]
        [SerializeField] float jawRadius = 0.11f;
        [Tooltip("Minimale botssnelheid tegen het kaartje (m/s).")]
        [SerializeField] float minImpactSpeed = 0.8f;

        Transform jaw;
        readonly ContactPoint[] points = new ContactPoint[8];

        void Awake()
        {
            jaw = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Jaw");
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || !IsSpawned || collision.relativeVelocity.magnitude < minImpactSpeed) return;
            var ticket = collision.collider.GetComponentInParent<PassengerTicket>();
            if (!ticket || !ticket.IsTicketCollider(collision.collider)) return;
            if (jaw)
            {
                bool nearJaw = false;
                int n = collision.GetContacts(points);
                for (int i = 0; i < n && !nearJaw; i++)
                    nearJaw = (points[i].point - jaw.position).sqrMagnitude <= jawRadius * jawRadius;
                if (!nearJaw) return;
            }
            ticket.ServerPunch();
        }
    }
}
