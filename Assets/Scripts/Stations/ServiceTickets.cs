using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceTickets : ServiceObjective
    {
        private enum Boarding { Waiting, ToDoor, ThroughDoor, ToSeat, Riding }

        [SerializeField] private PassengerTicket[] passengers;
        [SerializeField] private PassengerTicket[] boardingPassengers;
        [SerializeField] private Transform outside, doorway, seat;
        private Boarding[] stages;

        private void Start() => stages = new Boarding[boardingPassengers?.Length ?? 0];

        private void FixedUpdate()
        {
            if (!outside || !doorway || !seat || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene != gameObject.scene) return;
            for (int i = 0; i < stages.Length; i++)
            {
                var ticket = boardingPassengers[i];
                if (!ticket || !ticket.IsSpawned || !ticket.IsPunched) continue;
                var passenger = ticket.GetComponent<PlaygroundPassenger>();
                var body = ticket.GetComponent<Rigidbody>();
                Vector3 seatPosition = seat.position + seat.parent.right * (i == 0 ? -.6f : 1.3f);
                switch (stages[i])
                {
                    case Boarding.Waiting when run.CanBoardAt(doorway):
                        body.isKinematic = true;
                        stages[i] = Boarding.ToDoor;
                        break;
                    case Boarding.ToDoor when passenger.WalkToward(outside.position) && run.CanBoardAt(doorway):
                        stages[i] = Boarding.ThroughDoor;
                        break;
                    case Boarding.ThroughDoor when passenger.WalkToward(doorway.position):
                        stages[i] = Boarding.ToSeat;
                        break;
                    case Boarding.ToSeat when passenger.WalkToward(seatPosition):
                        stages[i] = Boarding.Riding;
                        break;
                    case Boarding.Riding:
                        body.MovePosition(seatPosition);
                        body.MoveRotation(seat.rotation);
                        break;
                }
                // Send one traveller through the narrow doorway at a time.
                if (stages[i] is not (Boarding.Waiting or Boarding.Riding)) break;
            }
        }

        public override int Target => passengers?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && System.Array.TrueForAll(passengers, ticket => ticket);
        public override bool ReadyToDepart => ReadProgress(false) >= Target;

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            int count = 0;
            foreach (var ticket in passengers)
                if (ticket && ticket.IsPunched) count++;
            return count;
        }
    }
}
