using ConductorChaos.Characters;
using ConductorChaos.Playground;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceEscort : ServiceObjective
    {
        [SerializeField] private Rigidbody passenger;
        [SerializeField] private Transform outside, doorway, seat, destination;
        private Rigidbody wagon;
        private readonly RaycastHit[] floorHits = new RaycastHit[12];
        private bool aboard, travelled, delivered;
        // Depot (#239): oma mag in of op elk deel van de trein mee, niet alleen op haar stoel in het rijtuig.
        private TrainDrive train;
        internal void SetTrain(TrainDrive value) => train = value;

        public override int Target => 2;
        public override bool IsConfigured => passenger && outside && doorway && seat && destination;
        public Transform Doorway => doorway;
        public override bool ReadyToDepart => aboard;

        private void Start()
        {
            if (passenger && passenger.transform.childCount > 0)
                CharacterFace.OnModel(passenger.transform.GetChild(0));
        }

        public override void BeginOnServer()
        {
            wagon = seat.GetComponentInParent<Rigidbody>();
            aboard = travelled = delivered = false;
        }

        public override void ObserveTravelOnServer() => travelled |= aboard;

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            aboard = IsAboard();
            if (trainStoppedAtDestination && travelled && !aboard)
            {
                Vector3 offset = passenger.position - destination.position;
                if (Mathf.Abs(offset.y) <= .75f && Vector3.ProjectOnPlane(offset, Vector3.up).sqrMagnitude <= 2f * 2f)
                    delivered = true;
            }
            return delivered ? 2 : aboard || travelled ? 1 : 0;
        }

        private bool IsAboard()
        {
            if (train) return OnAnyTrainFloor();
            if (!wagon || Mathf.Abs(passenger.position.y - seat.position.y) > .45f) return false;
            Vector3 inward = Vector3.ProjectOnPlane(doorway.position - outside.position, Vector3.up).normalized;
            if (Vector3.Dot(passenger.position - doorway.position, inward) < .25f) return false;
            int count = Physics.RaycastNonAlloc(passenger.position + Vector3.up * .25f, Vector3.down, floorHits, .8f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (floorHits[i].rigidbody == wagon && floorHits[i].normal.y > .55f) return true;
            return false;
        }

        private bool OnAnyTrainFloor()
        {
            int count = Physics.RaycastNonAlloc(passenger.position + Vector3.up * .25f, Vector3.down, floorHits, .8f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody hit = floorHits[i].rigidbody;
                if (!hit || floorHits[i].normal.y <= .55f) continue;
                if (hit == train.LocomotiveBody) return true;
                foreach (var coupled in train.CoupledWagons)
                    if (coupled == hit) return true;
            }
            return false;
        }
    }
}
