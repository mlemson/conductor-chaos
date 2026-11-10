using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TicketPunch : NetworkBehaviour
    {
        private const float HandleReach = .085f;
        private const float JawContact = .027f;
        private readonly NetworkVariable<bool> squeezed = new();
        private readonly Collider[] contacts = new Collider[24];
        private Rigidbody body;
        private Transform jaw, upper, lower, upperHandle, lowerHandle;
        private PlaygroundPlayer holder;
        private Transform holdingHand;
        private Vector3 gripAnchor;
        private Quaternion jawFrame;
        private Quaternion upperRest, lowerRest;
        private float closed;
        private bool cutThisSqueeze;
        private AudioClip click;

        internal Transform PressHandle => upperHandle;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            jaw = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Jaw");
            upper = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Upper");
            lower = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Lower");
            upperHandle = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_HandleUpper");
            lowerHandle = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_HandleLower");
            if (jaw)
            {
                if (lowerHandle) gripAnchor = transform.InverseTransformPoint(lowerHandle.position);
                Vector3 direction = Vector3.ProjectOnPlane(transform.InverseTransformPoint(jaw.position) - gripAnchor, Vector3.up);
                jawFrame = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            }
            if (upper) upperRest = upper.localRotation;
            if (lower) lowerRest = lower.localRotation;
            click = Resources.Load<AudioClip>("TicketPunchClick");
        }

        internal bool TryHold(PlaygroundPlayer player, Transform hand, ref Vector3 point)
        {
            if (!IsServer || !IsSpawned || !player || (holder && holder.IsSpawned && holdingHand != hand)) return false;
            holder = player;
            holdingHand = hand;
            if (lowerHandle) point = transform.TransformPoint(gripAnchor);
            return true;
        }

        internal void Release(Transform hand)
        {
            if (holdingHand != hand) return;
            holder = null;
            holdingHand = null;
        }

        public override void OnNetworkDespawn()
        {
            holder = null;
            holdingHand = null;
            cutThisSqueeze = false;
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !jaw) return;
            bool held = holder && holder.IsSpawned &&
                holder.TryGetControlHand(body, out var hand, out _, out _) && hand == holdingHand;
            if (held)
            {
                Quaternion rotation = Quaternion.LookRotation(holder.CabAimForward, Vector3.up) * Quaternion.Inverse(jawFrame);
                Quaternion error = rotation * Quaternion.Inverse(body.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                Vector3 torque = axis * (angle * Mathf.Deg2Rad * 190f) - body.angularVelocity * 25f;
                body.AddTorque(Vector3.ClampMagnitude(torque, 160f), ForceMode.Acceleration);
                if (body.useGravity) body.AddForce(-Physics.gravity, ForceMode.Acceleration);
            }

            bool pressing = held && HandPresses(upperHandle);
            if (squeezed.Value != pressing) squeezed.Value = pressing;
            if (!pressing)
            {
                cutThisSqueeze = false;
                return;
            }
            if (cutThisSqueeze) return;

            int count = Physics.OverlapSphereNonAlloc(jaw.position, JawContact, contacts, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var ticket = contacts[i].GetComponentInParent<PassengerTicket>();
                if (!ticket || !ticket.IsSpawned || ticket.IsPunched || !ticket.IsTicketCollider(contacts[i])) continue;
                ticket.ServerPunch();
                cutThisSqueeze = true;
                PlayCutRpc(jaw.position);
                break;
            }
        }

        private bool HandPresses(Transform handle)
        {
            if (!handle) return false;
            int count = Physics.OverlapSphereNonAlloc(handle.position, HandleReach, contacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (holder.SqueezesWithOtherHand(contacts[i], body)) return true;
            }
            return false;
        }

        void Update()
        {
            if (!IsSpawned || !upper || !lower || !jaw) return;
            closed = Mathf.MoveTowards(closed, squeezed.Value ? 1f : 0f, Time.deltaTime * 18f);
            float angle = Mathf.Sign(jaw.localPosition.x) * closed * 7f;
            upper.localRotation = upperRest * Quaternion.AngleAxis(angle, Vector3.forward);
            lower.localRotation = lowerRest * Quaternion.AngleAxis(-angle, Vector3.forward);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayCutRpc(Vector3 position)
        {
            if (click && !Application.isBatchMode) AudioSource.PlayClipAtPoint(click, position, .65f);
        }
    }
}
