using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TicketPunch : NetworkBehaviour
    {
        private const float JawContact = .027f;
        private readonly NetworkVariable<bool> squeezed = new();
        private readonly Collider[] contacts = new Collider[24];
        private Rigidbody body;
        private Transform jaw, upper, upperHandle;
        private PlaygroundPlayer holder;
        private Transform holdingHand;
        private Vector3 gripAnchor;
        private Vector3 jawPoint;
        private Quaternion jawFrame;
        private Quaternion upperRest;
        private bool restoreCollisions;
        private float closed;
        private bool cutThisSqueeze;
        private AudioClip click;

        internal Transform PressHandle => upperHandle;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            jaw = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Jaw");
            upper = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_Upper");
            upperHandle = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_HandleUpper");
            if (jaw)
            {
                var lowerHandle = ConductorChaos.Track.RepairPoint.FindDeep(transform, "Punch_HandleLower");
                if (lowerHandle) gripAnchor = transform.InverseTransformPoint(lowerHandle.position);
                jawPoint = transform.InverseTransformPoint(jaw.position);
                Vector3 direction = Vector3.ProjectOnPlane(jawPoint - gripAnchor, Vector3.up);
                jawFrame = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction, Vector3.up) : Quaternion.identity;
            }
            if (upper) upperRest = upper.localRotation;
            click = Resources.Load<AudioClip>("TicketPunchClick");
        }

        internal bool TryHold(PlaygroundPlayer player, Transform hand)
        {
            if (!IsServer || !IsSpawned || !jaw || !player || holdingHand is not null) return false;
            holder = player;
            holdingHand = hand;
            restoreCollisions = body.detectCollisions;
            body.detectCollisions = false; // The carried hull must not push the ticket out of reach.
            body.isKinematic = true;
            return true;
        }

        internal void Release(Transform hand)
        {
            if (holdingHand != hand) return;
            EndHold(IsSpawned);
        }

        private void EndHold(bool drop)
        {
            body.detectCollisions = restoreCollisions;
            if (drop)
            {
                body.isKinematic = false;
                body.linearVelocity = holdingHand && holdingHand.TryGetComponent<Rigidbody>(out var palm)
                    ? Vector3.ClampMagnitude(palm.linearVelocity, 8f) : Vector3.zero;
            }
            holder = null;
            holdingHand = null;
            cutThisSqueeze = false;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && holdingHand is not null) EndHold(false);
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !jaw) return;
            if (!holder || !holder.IsSpawned || !holder.TryGetPunchGrip(this, out var palm, out bool pressing))
            {
                if (holdingHand is not null) EndHold(true);
                if (squeezed.Value) squeezed.Value = false;
                cutThisSqueeze = false;
                return;
            }

            Vector3 aim = holder.CabAimForward;
            Quaternion rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Inverse(jawFrame);
            Vector3 scale = transform.lossyScale;
            Vector3 position = palm - rotation * Vector3.Scale(gripAnchor, scale);
            body.MovePosition(position);
            body.MoveRotation(rotation);

            if (squeezed.Value != pressing) squeezed.Value = pressing;
            if (!pressing)
            {
                cutThisSqueeze = false;
                return;
            }
            if (cutThisSqueeze) return;

            Vector3 tip = position + rotation * Vector3.Scale(jawPoint, scale);
            int count = Physics.OverlapCapsuleNonAlloc(tip - aim * .04f, tip + aim * .012f, JawContact,
                contacts, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var ticket = contacts[i].GetComponentInParent<PassengerTicket>();
                if (!ticket || !ticket.IsSpawned || ticket.IsPunched || !ticket.IsTicketCollider(contacts[i])) continue;
                ticket.ServerPunch();
                cutThisSqueeze = true;
                PlayCutRpc(tip);
                break;
            }
        }

        void Update()
        {
            if (!IsSpawned || !upper || !jaw) return;
            closed = Mathf.MoveTowards(closed, squeezed.Value ? 1f : 0f, Time.deltaTime * 18f);
            float angle = Mathf.Sign(jawPoint.x - gripAnchor.x) * closed * 14f;
            upper.localRotation = upperRest * Quaternion.AngleAxis(angle, Vector3.forward);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayCutRpc(Vector3 position)
        {
            if (click && !Application.isBatchMode) AudioSource.PlayClipAtPoint(click, position, .65f);
        }
    }
}
