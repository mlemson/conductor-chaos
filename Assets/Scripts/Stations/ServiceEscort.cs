using ConductorChaos.Characters;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceEscort : ServiceObjective
    {
        private enum Stage { Waiting, ToDoor, Boarding, ToSeat, Riding, FromSeat, Leaving, ToPlatform, Done }

        [SerializeField] private Rigidbody passenger;
        [SerializeField] private Transform outside, doorway, seat, destination;
        [SerializeField] private Transform doorLeaf;
        private Stage stage;

        public override int Target => 2;
        public override bool IsConfigured => passenger && outside && doorway && seat && destination && doorLeaf;
        public Transform DoorLeaf => doorLeaf;
        public override bool ReadyToDepart => stage is Stage.Riding or Stage.Done;

        public bool IsNear(Vector3 position) => passenger && (passenger.transform.position - position).sqrMagnitude <= 2.1f * 2.1f;

        private void Start()
        {
            if (passenger && passenger.transform.childCount > 0)
                CharacterFace.OnModel(passenger.transform.GetChild(0));
        }

        public void AssistOnServer(bool arriving)
        {
            if (!arriving && stage == Stage.Waiting) stage = Stage.ToDoor;
            if (arriving && stage == Stage.Riding) stage = Stage.FromSeat;
        }

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            // Oma is a physical passenger you can drag and push. Only while she walks to her seat, rides
            // and walks off again does this script carry her.
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
