using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceTickets : ServiceObjective
    {
        private enum Boarding { Waiting, ToDoor, ThroughDoor, ToAisle, ToRow, Riding }

        [SerializeField] private PassengerTicket[] passengers;
        [SerializeField] private PassengerTicket[] boardingPassengers;
        [SerializeField] private Transform outside, doorway, seat;
        private Boarding[] stages;

        private void Start() => stages = new Boarding[boardingPassengers?.Length ?? 0];

        private void FixedUpdate()
        {
            if (!outside || !doorway || !seat || stages == null || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene != gameObject.scene) return;
            Transform wagon = seat.parent;
            bool busy = System.Array.Exists(stages, stage => stage is not (Boarding.Waiting or Boarding.Riding));
            for (int i = 0; i < stages.Length; i++)
            {
                var ticket = boardingPassengers[i];
                if (!ticket || !ticket.IsSpawned || !ticket.IsPunched) continue;
                var passenger = ticket.GetComponent<PlaygroundPassenger>();
                var body = ticket.GetComponent<Rigidbody>();
                // RegionalWagon aisle z = -1.3, seat top y = .50.
                float row = i == 0 ? -1.70f : 1.45f;
                Vector3 aisle = wagon.TransformPoint(doorway.localPosition.x, .13f, -1.30f);
                Vector3 rowEntry = wagon.TransformPoint(row, .13f, -1.30f);
                Vector3 seatPosition = wagon.TransformPoint(row, .26f, -2.04f);
                Quaternion seatRotation = Quaternion.LookRotation(wagon.right * (i == 0 ? -1f : 1f), wagon.up);
                switch (stages[i])
                {
                    case Boarding.Waiting when !busy && run.CanBoardAt(doorway):
                        passenger.BeginBoarding();
                        stages[i] = Boarding.ToDoor;
                        busy = true;
                        break;
                    case Boarding.ToDoor when run.CanBoardAt(doorway) && passenger.WalkToward(outside.position):
                        stages[i] = Boarding.ThroughDoor;
                        break;
                    case Boarding.ThroughDoor when run.CanBoardAt(doorway) && passenger.WalkToward(doorway.position):
                        stages[i] = Boarding.ToAisle;
                        break;
                    case Boarding.ToAisle when passenger.WalkToward(aisle):
                        stages[i] = Boarding.ToRow;
                        break;
                    case Boarding.ToRow when passenger.WalkToward(rowEntry):
                        passenger.PoseSeated();
                        stages[i] = Boarding.Riding;
                        goto case Boarding.Riding;
                    case Boarding.Riding:
                        body.MovePosition(seatPosition);
                        body.MoveRotation(seatRotation);
                        break;
                }
            }
        }

        internal void AssignWrongTicket()
        {
            if (!IsConfigured || passengers.Length < 2) return;
            passengers[Random.Range(0, passengers.Length)].ServerSetWrong();
            Debug.Assert(Target == passengers.Length - 1, "De dienst heeft precies één verkeerd kaartje.", this);
        }

        internal int BoardingTarget => CountValid(boardingPassengers);
        internal int BoardedCount
        {
            get
            {
                int count = 0;
                if (stages != null)
                    foreach (var stage in stages)
                        if (stage == Boarding.Riding) count++;
                return count;
            }
        }

        private static int CountValid(PassengerTicket[] tickets)
        {
            int count = 0;
            if (tickets != null)
                foreach (var ticket in tickets)
                    if (ticket && !ticket.IsWrong) count++;
            return count;
        }

        public override int Target => CountValid(passengers);
        public override bool IsConfigured => passengers is { Length: > 0 } && System.Array.TrueForAll(passengers, ticket => ticket);
        public override bool ReadyToDepart => ReadProgress(false) >= Target && BoardedCount >= BoardingTarget;

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            int count = 0;
            foreach (var ticket in passengers)
                if (ticket && ticket.IsPunched) count++;
            return count;
        }
    }
}
