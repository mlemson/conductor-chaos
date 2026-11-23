using System.Collections.Generic;
using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    public sealed class ServiceTickets : ServiceObjective
    {
        private enum Boarding { Waiting, ToDoor, ThroughDoor, ToAisle, ToRow, Riding, Returning }
        private sealed class Boarder
        {
            internal readonly PassengerTicket Ticket;
            internal readonly ServiceStop Stop;
            internal readonly Vector3 Home;
            internal Boarding Stage;
            internal int Seat = -1;
            internal bool Started;

            internal Boarder(PassengerTicket ticket, ServiceStop stop = null)
            {
                Ticket = ticket;
                Stop = stop;
                Home = ticket ? ticket.transform.position : default;
            }
        }

        internal const string BonusPassengerResource = "ServiceBonusPassenger";
        private static readonly Vector2[] BonusSeats =
        {
            new(-2.8f, -2.04f), new(2.75f, -2.04f), new(-1.70f, -.56f), new(1.45f, -.56f),
            new(-2.8f, -.56f), new(2.75f, -.56f), new(-.45f, -2.04f), new(-.45f, -.56f)
        };

        [SerializeField] private PassengerTicket[] passengers;
        [SerializeField] private PassengerTicket[] boardingPassengers;
        [SerializeField] private Transform outside, doorway, seat;
        private Transform otherOutside, otherDoorway;
        private readonly List<Boarder> roster = new();
        private bool rosterInitialized;
        private int RequiredBoarders => boardingPassengers?.Length ?? 0;
        public Transform BoardingApproach => outside;

        private void Start() => InitializeRoster();

        private void InitializeRoster()
        {
            if (rosterInitialized) return;
            rosterInitialized = true;
            if (boardingPassengers != null)
                foreach (var ticket in boardingPassengers)
                    roster.Add(new Boarder(ticket));
        }

        private static NetworkObject BonusPrefab()
        {
            var asset = Resources.Load<GameObject>(BonusPassengerResource);
            return asset ? asset.GetComponent<NetworkObject>() : null;
        }

        internal void SpawnBonusPassengers(BoxCollider platform, int maximum, ServiceManifest manifest)
        {
            var prefab = BonusPrefab();
            if (!prefab || !platform || !seat || !outside || !doorway || RequiredBoarders == 0) return;
            InitializeRoster();
            Transform reference = passengers[0].transform;
            Vector3 along = seat.parent.right;
            Physics.SyncTransforms();
            int count = 0;
            for (int i = 0; i < 16 && count < maximum; i++)
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
                var extra = passenger.GetComponent<PassengerTicket>();
                if (manifest.IsReady) extra.ServerSetFare(manifest.PickFare(true), false);
                roster.Add(new Boarder(extra));
                count++;
            }
        }

        internal void SpawnAtStop(ServiceStop stop, Transform waiting, ServiceManifest manifest)
        {
            var prefab = BonusPrefab();
            if (!prefab || !seat || !outside || !doorway) return;
            InitializeRoster();
            var rotation = Quaternion.LookRotation(-waiting.right);
            int[] appearances = { 0, 1, 2, 3 };
            int count = Random.value < .40f ? 3 : 4;
            int skipped = count == 3 ? (manifest.ExcludedTraveller + Random.Range(1, 4)) % 4 : -1;
            int invalidCount = Random.Range(0, count - 1);
            int place = 0;
            for (int i = 0; i < appearances.Length; i++)
            {
                int draw = Random.Range(i, appearances.Length);
                (appearances[i], appearances[draw]) = (appearances[draw], appearances[i]);
                int appearance = appearances[i];
                if (appearance == skipped) continue;
                bool excluded = appearance == manifest.ExcludedTraveller;
                bool badFare = !excluded && invalidCount > 0;
                if (badFare) invalidCount--;
                var item = stop.Spawn(prefab, waiting.position + waiting.forward * ((place++ - (count - 1) * .5f) * 1.12f),
                    rotation, (byte)appearance);
                var ticket = item.GetComponent<PassengerTicket>();
                ticket.ServerSetFare(manifest.PickFare(!badFare), excluded || badFare);
                roster.Add(new Boarder(ticket, stop));
            }
        }

        private int FreeSeat()
        {
            int taken = 0;
            for (int i = RequiredBoarders; i < roster.Count; i++)
                if (roster[i].Seat >= 0)
                    taken |= 1 << roster[i].Seat;
            for (int i = 0; i < BonusSeats.Length; i++)
                if ((taken & (1 << i)) == 0)
                    return i;
            return -1;
        }

        private Transform MirrorBoardingPoint(Transform point)
        {
            var marker = new GameObject(point.name + " · overzijde").transform;
            marker.SetParent(point.parent, false);
            var local = point.localPosition;
            local.z = (-1.30f * 2f) - local.z;
            marker.localPosition = local;
            return marker;
        }

        internal bool TryStopDoor(ServiceStop stop, out Transform approach, out Transform entrance)
        {
            approach = outside;
            entrance = doorway;
            if (stop.DoorAtPlatform(approach)) return true;
            if (!otherOutside) otherOutside = MirrorBoardingPoint(outside);
            if (!otherDoorway) otherDoorway = MirrorBoardingPoint(doorway);
            approach = otherOutside;
            entrance = otherDoorway;
            return stop.DoorAtPlatform(approach);
        }

        private void FixedUpdate()
        {
            if (!outside || !doorway || !seat || !NetworkManager.Singleton || !NetworkManager.Singleton.IsServer) return;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene != gameObject.scene || run.Phase is ServiceRun.RunPhase.Won or ServiceRun.RunPhase.Failed) return;
            Transform wagon = seat.parent;
            bool busy = roster.Exists(entry => entry.Stage is Boarding.ToDoor or Boarding.ThroughDoor or Boarding.ToAisle or Boarding.ToRow);
            for (int i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                var ticket = entry.Ticket;
                if (!ticket || !ticket.IsSpawned || !ticket.IsPunched) continue;
                bool required = i < RequiredBoarders;
                var approach = outside;
                var entrance = doorway;
                bool atPlatform = entry.Stop
                    ? run.Phase == ServiceRun.RunPhase.Travelling && TryStopDoor(entry.Stop, out approach, out entrance)
                    : run.Phase == ServiceRun.RunPhase.Departure;
                bool canBoard = atPlatform && run.CanBoardAt(entrance);
                var passenger = ticket.GetComponent<PlaygroundPassenger>();
                Vector2 sitting = entry.Seat < 0 ? new Vector2(i == 0 ? -1.70f : 1.45f, -2.04f) : BonusSeats[entry.Seat];
                switch (entry.Stage)
                {
                    case Boarding.Waiting when !busy && canBoard && !passenger.IsFrozen:
                        if (!required)
                        {
                            entry.Seat = FreeSeat();
                            if (entry.Seat < 0) break;
                        }
                        if (!entry.Started) passenger.BeginBoarding();
                        entry.Started = true;
                        entry.Stage = Boarding.ToDoor;
                        busy = true;
                        break;
                    case Boarding.ToDoor or Boarding.ThroughDoor when !required && !atPlatform:
                        entry.Seat = -1;
                        entry.Stage = Boarding.Returning;
                        break;
                    case Boarding.ToDoor when canBoard && passenger.WalkToward(approach.position):
                        entry.Stage = Boarding.ThroughDoor;
                        break;
                    case Boarding.ThroughDoor when canBoard && passenger.WalkToward(entrance.position):
                        entry.Stage = Boarding.ToAisle;
                        break;
                    case Boarding.ToAisle when passenger.WalkToward(wagon.TransformPoint(doorway.localPosition.x, .13f, -1.30f)):
                        entry.Stage = Boarding.ToRow;
                        break;
                    case Boarding.ToRow when passenger.WalkToward(wagon.TransformPoint(sitting.x, .13f, -1.30f)):
                        passenger.PoseSeated();
                        entry.Stage = Boarding.Riding;
                        goto case Boarding.Riding;
                    case Boarding.Riding:
                        var body = ticket.GetComponent<Rigidbody>();
                        body.MovePosition(wagon.TransformPoint(sitting.x, .26f, sitting.y));
                        body.MoveRotation(Quaternion.LookRotation(wagon.right * (sitting.x < 0f ? -1f : 1f), wagon.up));
                        break;
                    case Boarding.Returning when passenger.WalkToward(entry.Home):
                        entry.Stage = Boarding.Waiting;
                        break;
                }
            }
        }

        internal void AssignWrongTicket(ServiceManifest manifest)
        {
            if (!IsConfigured || passengers.Length < 2) return;
            int wrong = Random.Range(0, passengers.Length);
            for (int i = 0; i < passengers.Length; i++)
            {
                if (manifest.IsReady)
                    passengers[i].ServerSetFare(manifest.PickFare(i != wrong), i == wrong);
                else if (i == wrong) passengers[i].ServerSetWrong();
            }
            Debug.Assert(Target == passengers.Length - 1, "De dienst heeft precies één verkeerd kaartje.", this);
        }

        internal int BoardingTarget => CountValid(boardingPassengers);
        internal int BoardedCount => CountBoarded(0, RequiredBoarders);
        internal int BonusBoardedCount => CountBoarded(RequiredBoarders, roster.Count);

        private int CountBoarded(int start, int end)
        {
            end = Mathf.Min(end, roster.Count);
            int count = 0;
            for (int i = start; i < end; i++)
                if (roster[i].Stage == Boarding.Riding) count++;
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
