using ConductorChaos.Characters;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceEscort : ServiceObjective
    {
        private enum Stage { Waiting, ToDoor, Boarding, ToSeat, Riding, FromSeat, Leaving, ToPlatform, Done }

        [SerializeField] private Rigidbody passenger;
        [SerializeField] private Transform outside, doorway, seat, destination;
        private Stage stage;
        private Rigidbody wagon;
        private readonly RaycastHit[] floorHits = new RaycastHit[12];

        public override int Target => 2;
        public override bool IsConfigured => passenger && outside && doorway && seat && destination;
        public Transform Doorway => doorway;
        public override bool ReadyToDepart => stage is Stage.Riding or Stage.Done;

        public bool IsNear(Vector3 position) => passenger && (passenger.transform.position - position).sqrMagnitude <= 2.1f * 2.1f;

        private void Start()
        {
            if (passenger && passenger.transform.childCount > 0)
                CharacterFace.OnModel(passenger.transform.GetChild(0));
        }

        public override void BeginOnServer() => wagon = seat.GetComponentInParent<Rigidbody>();

        public void AssistOnServer(bool arriving)
        {
            if (!arriving && stage == Stage.Waiting) stage = IsAboard() ? Stage.ToSeat : Stage.ToDoor;
            if (arriving && stage == Stage.Riding) stage = Stage.FromSeat;
        }

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            // Oma is a physical passenger you can drag and push. Only while she walks to her seat, rides
            // and walks off again does this script carry her.
            if (stage == Stage.Waiting && IsAboard()) stage = Stage.ToSeat;
            bool scripted = stage is not (Stage.Waiting or Stage.Done);
            if (passenger.isKinematic != scripted) passenger.isKinematic = scripted;
            switch (stage)
            {
                case Stage.ToDoor when Step(outside.position): stage = Stage.Boarding; break;
                case Stage.Boarding when Step(doorway.position): stage = Stage.ToSeat; break;
                case Stage.ToSeat when Step(seat.position): stage = Stage.Riding; break;
                case Stage.Riding:
                    passenger.MovePosition(seat.position);
                    passenger.MoveRotation(seat.rotation);
                    break;
                case Stage.FromSeat when Step(doorway.position): stage = Stage.Leaving; break;
                case Stage.Leaving when Step(outside.position): stage = Stage.ToPlatform; break;
                case Stage.ToPlatform when Step(destination.position): stage = Stage.Done; break;
            }
            return stage == Stage.Done ? 2 : stage >= Stage.Riding ? 1 : 0;
        }

        private bool IsAboard()
        {
            if (!wagon || Mathf.Abs(passenger.position.y - seat.position.y) > .45f) return false;
            Vector3 inward = Vector3.ProjectOnPlane(doorway.position - outside.position, Vector3.up).normalized;
            if (Vector3.Dot(passenger.position - doorway.position, inward) < .25f) return false;
            int count = Physics.RaycastNonAlloc(passenger.position + Vector3.up * .25f, Vector3.down, floorHits, .8f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (floorHits[i].rigidbody == wagon && floorHits[i].normal.y > .55f) return true;
            return false;
        }

        private bool Step(Vector3 destination)
        {
            Vector3 delta = destination - passenger.position;
            var flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            if (flat.sqrMagnitude > .008f)
                passenger.MoveRotation(Quaternion.RotateTowards(passenger.rotation, Quaternion.LookRotation(flat), 360f * Time.fixedDeltaTime));
            passenger.MovePosition(Vector3.MoveTowards(passenger.position, destination, .9f * Time.fixedDeltaTime));
            return delta.sqrMagnitude < .018f * .018f;
        }
    }
}
