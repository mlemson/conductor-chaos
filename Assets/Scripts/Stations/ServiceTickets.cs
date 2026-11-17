using System.Collections.Generic;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    public sealed class ServiceTickets : ServiceObjective
    {
        private enum Boarding { Waiting, ToDoor, ThroughDoor, ToAisle, ToRow, Riding }
        internal const string BonusPassengerResource = "ServiceBonusPassenger";

        [SerializeField] private PassengerTicket[] passengers;
        [SerializeField] private PassengerTicket[] boardingPassengers;
        [SerializeField] private Transform outside, doorway, seat;
        private PassengerTicket[] boardingRoster;
        private Boarding[] stages;
        private int RequiredBoarders => boardingPassengers?.Length ?? 0;

        private void Start()
        {
            if (stages != null) return;
            boardingRoster = boardingPassengers ?? System.Array.Empty<PassengerTicket>();
            stages = new Boarding[boardingRoster.Length];
        }

        internal void SpawnBonusPassengers(BoxCollider platform, int maximum)
        {
            var asset = Resources.Load<GameObject>(BonusPassengerResource);
            if (!asset || !asset.TryGetComponent<NetworkObject>(out var prefab) ||
                !platform || !seat || !outside || !doorway || RequiredBoarders == 0) return;
            var roster = new List<PassengerTicket>(boardingPassengers);
            Transform reference = passengers[0].transform;
            Vector3 along = seat.parent.right;
            Physics.SyncTransforms();
            for (int i = 0; i < 16 && roster.Count < RequiredBoarders + maximum; i++)
            {
                float distance = (i / 2 + 1) * 1.25f * (i % 2 == 0 ? 1f : -1f);
                Vector3 probe = reference.position + along * distance + Vector3.up * .8f;
                if (!Physics.Raycast(probe, Vector3.down, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore) ||
                    hit.rigidbody || hit.normal.y < .85f || !ServiceStation.Contains(platform, hit.point + Vector3.up * .3f) ||
                    Physics.CheckCapsule(hit.point + Vector3.up * .34f, hit.point + Vector3.up * 1.02f, .25f, ~0,
                        QueryTriggerInteraction.Ignore)) continue;

                var passenger = Instantiate(prefab, hit.point + Vector3.up * .04f, reference.rotation);
                SceneManager.MoveGameObjectToScene(passenger.gameObject, gameObject.scene);
                passenger.Spawn(destroyWithScene: true);
                roster.Add(passenger.GetComponent<PassengerTicket>());
            }
            boardingRoster = roster.ToArray();
            stages = new Boarding[boardingRoster.Length];
        }

        private void FixedUpdate()
        {
            if (!outside || !doorway || !seat || stages == null || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene != gameObject.scene) return;
            Transform wagon = seat.parent;
            bool busy = System.Array.Exists(stages, stage => stage is not (Boarding.Waiting or Boarding.Riding));
            for (int i = 0; i < stages.Length; i++)
            {
                var ticket = boardingRoster[i];
                if (!ticket || !ticket.IsSpawned || !ticket.IsPunched) continue;
                var passenger = ticket.GetComponent<PlaygroundPassenger>();
                var body = ticket.GetComponent<Rigidbody>();
                // RegionalWagon aisle z = -1.3, seat top y = .50.
                int extra = i - RequiredBoarders;
                bool otherSide = extra is 2 or 3;
                float row = extra switch { 0 => -2.8f, 1 => 2.75f, 2 => -1.70f, 3 => 1.45f, _ => i == 0 ? -1.70f : 1.45f };
                Vector3 aisle = wagon.TransformPoint(doorway.localPosition.x, .13f, -1.30f);
                Vector3 rowEntry = wagon.TransformPoint(row, .13f, -1.30f);
                Vector3 seatPosition = wagon.TransformPoint(row, .26f, otherSide ? -.56f : -2.04f);
                Quaternion seatRotation = Quaternion.LookRotation(wagon.right * (row < 0f ? -1f : 1f), wagon.up);
                switch (stages[i])
                {
                    case Boarding.Waiting when !busy && (extra < 0 || run.Phase == ServiceRun.RunPhase.Departure) && run.CanBoardAt(doorway) &&
                        !passenger.IsFrozen:
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
        internal int BoardedCount => CountBoarded(0, RequiredBoarders);
        internal int BonusBoardedCount => CountBoarded(RequiredBoarders, stages?.Length ?? 0);

        private int CountBoarded(int start, int end)
        {
            if (stages == null) return 0;
            int count = 0;
            for (int i = start; i < end; i++)
                if (stages[i] == Boarding.Riding) count++;
            return count;
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
