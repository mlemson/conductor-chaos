using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class ServiceRun : NetworkBehaviour
    {
        public const string IntroScene = "ServiceIntroduction";
        public const string FirstLevelScene = "ServiceLevel01";

        public enum RunPhase { Loading, Briefing, Training, Departure, Travelling, Arrival, Won, Failed }
        public enum Lesson { Walk, Carry, Ticket, OpenDoor, CloseDoor, Power, Bell, Drive, Finished }

        [SerializeField] private bool introduction;
        [SerializeField] private ServiceStation departure;
        [SerializeField] private ServiceStation arrival;
        [SerializeField] private TrainDrive train;
        [SerializeField] private Rigidbody practiceLuggage;
        [SerializeField] private BoxCollider practiceDrop;
        [SerializeField] private ServiceObjective practiceTicket;
        [SerializeField] private ServiceObjective[] objectives;

        private readonly NetworkVariable<RunPhase> phase = new();
        private readonly NetworkVariable<Lesson> lesson = new();
        private readonly NetworkVariable<int> selected = new(-1);
        private readonly NetworkVariable<int> progress = new();
        private readonly NetworkVariable<double> deadline = new();
        private readonly NetworkVariable<int> resultTime = new();
        private readonly NetworkVariable<bool> bellRung = new();
        private readonly NetworkVariable<bool> doorsClosed = new();
        private readonly NetworkVariable<bool> powerOn = new();
        private readonly NetworkVariable<bool> offTrack = new();
        private Vector3 initialTrainPosition;
        private float stopSeconds;
        private double resultReadyAt;

        public static ServiceRun Active { get; private set; }
        public bool Introduction => introduction;
        public ServiceStation Departure => departure;
        public ServiceStation Arrival => arrival;
        public RunPhase Phase => phase.Value;
        public Lesson CurrentLesson => lesson.Value;
        public ServiceObjective Objective => selected.Value >= 0 && selected.Value < (objectives?.Length ?? 0) ? objectives[selected.Value] : null;
        public int Progress => progress.Value;
        public bool BellRung => bellRung.Value;
        public bool DoorsClosed => doorsClosed.Value;
        public bool PowerOn => powerOn.Value;
        public bool OffTrack => offTrack.Value;
        public bool TimerRunning => deadline.Value > 0;
        internal bool CanContinue => Phase == RunPhase.Briefing ||
            (Phase is RunPhase.Won or RunPhase.Failed) && NetworkManager.ServerTime.Time >= resultReadyAt;
        public int TimeLeft => Phase is RunPhase.Won or RunPhase.Failed ? resultTime.Value :
            Mathf.Max(0, Mathf.CeilToInt((float)(deadline.Value - NetworkManager.ServerTime.Time)));

        // Only the campaign's train is subject to the departure checklist.
        internal bool AllowsMotion(TrainDrive candidate) => candidate != train ||
            (doorsClosed.Value && (introduction
                ? Phase == RunPhase.Training && CurrentLesson == Lesson.Drive
                : (Phase is RunPhase.Departure or RunPhase.Travelling or RunPhase.Arrival) &&
                  TimerRunning && bellRung.Value && (Objective?.ReadyToDepart ?? false)));

        public bool IsConfigured => departure && departure.Role == ServiceStation.StationRole.Vertrek &&
            departure.CrewSpawn && departure.Platform && departure.TrainStop && departure.DepartureBell && train &&
            (introduction ? practiceLuggage && practiceDrop && practiceTicket && practiceTicket.IsConfigured :
                arrival && arrival.Role == ServiceStation.StationRole.Aankomst &&
                arrival.TrainStop && objectives != null && System.Array.Exists(objectives, o => o && o.IsConfigured));

        public override void OnNetworkSpawn() => Active = this;

        public override void OnNetworkDespawn()
        {
            if (Active == this) Active = null;
        }

        internal bool BeginOnServer()
        {
            if (!IsServer || !IsSpawned || !IsConfigured || Phase != RunPhase.Loading) return false;
            if (!introduction)
            {
                var eligible = System.Array.FindAll(objectives, o => o && o.IsConfigured);
                var objective = eligible[Random.Range(0, eligible.Length)];
                selected.Value = System.Array.IndexOf(objectives, objective);
                objective.BeginOnServer();
            }
            initialTrainPosition = train.transform.position;
            phase.Value = RunPhase.Briefing;
            return true;
        }

        public void Continue() => ContinueRpc();
        public void RingBell() => RingBellRpc();
        public void AssistPassenger() => AssistPassengerRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ContinueRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || !CanContinue) return;
            if (Phase == RunPhase.Briefing)
                phase.Value = introduction ? RunPhase.Training : RunPhase.Departure;
            else
            {
                var host = NetworkManager.GetComponent<PlaygroundHost>();
                if (host) host.LoadServiceScene(introduction && Phase == RunPhase.Won ? FirstLevelScene : gameObject.scene.name);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RingBellRpc(RpcParams rpc = default)
        {
            bool readyToRing = Phase == RunPhase.Departure && TimerRunning ||
                Phase == RunPhase.Training && CurrentLesson == Lesson.Bell;
            if (!readyToRing || bellRung.Value ||
                !NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var client) ||
                !client.PlayerObject ||
                (client.PlayerObject.transform.position - departure.DepartureBell.position).sqrMagnitude > 2.2f * 2.2f)
                return;
            bellRung.Value = true;
        }

        public bool CanRingFrom(Vector3 position) => departure && departure.DepartureBell && !BellRung &&
            (Phase == RunPhase.Departure && TimerRunning || Phase == RunPhase.Training && CurrentLesson == Lesson.Bell) &&
            (position - departure.DepartureBell.position).sqrMagnitude <= 2.2f * 2.2f;

        public bool CanAssistFrom(Vector3 position) => Objective is ServiceEscort escort && train.IsServiceDoorOpen(escort.DoorLeaf) &&
            (Phase == RunPhase.Departure && TimerRunning && Progress == 0 || Phase == RunPhase.Arrival && Progress == 1) &&
            escort.IsNear(position);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void AssistPassengerRpc(RpcParams rpc = default)
        {
            if (train.ServiceSpeed >= .22f ||
                !NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var client) ||
                !client.PlayerObject || !CanAssistFrom(client.PlayerObject.transform.position)) return;
            ((ServiceEscort)Objective).AssistOnServer(Phase == RunPhase.Arrival);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Phase is RunPhase.Loading or RunPhase.Briefing or RunPhase.Won or RunPhase.Failed)
                return;

            doorsClosed.Value = train.ServiceDoorsClosed;
            powerOn.Value = train.ServicePowerOn;
            if (train.ServiceDerailed)
            {
                offTrack.Value = true;
                Finish(false);
                return;
            }

            if (introduction)
            {
                UpdateTraining();
                return;
            }

            if (!TimerRunning)
            {
                if (CrewAt(departure.Platform))
                    deadline.Value = NetworkManager.ServerTime.Time + TimeLimit(NetworkManager.ConnectedClientsIds.Count);
                return;
            }
            if (NetworkManager.ServerTime.Time >= deadline.Value)
            {
                Finish(false);
                return;
            }
            if (Phase == RunPhase.Departure && !ServiceStation.Contains(departure.TrainStop, train.ServiceRailPosition))
                phase.Value = RunPhase.Travelling;

            bool stopped = ServiceStation.Contains(arrival.TrainStop, train.ServiceRailPosition) && train.ServiceSpeed < .22f;
            stopSeconds = stopped ? stopSeconds + Time.fixedDeltaTime : 0;
            bool docked = stopSeconds >= 1f;
            if (Phase is RunPhase.Travelling or RunPhase.Arrival)
                phase.Value = docked ? RunPhase.Arrival : RunPhase.Travelling;
            if (Phase == RunPhase.Travelling && train.ServiceSpeed >= .22f) Objective.ObserveTravelOnServer();
            progress.Value = Mathf.Clamp(Objective.ReadProgress(docked), 0, Objective.Target);
            if (docked && progress.Value >= Objective.Target) Finish(true);
        }

        private void UpdateTraining()
        {
            switch (CurrentLesson)
            {
                case Lesson.Walk when CrewAt(departure.Platform):
                    lesson.Value = Lesson.Carry;
                    break;
                case Lesson.Carry when ServiceStation.Contains(practiceDrop, practiceLuggage.worldCenterOfMass):
                    lesson.Value = Lesson.Ticket;
                    break;
                case Lesson.Ticket when practiceTicket.ReadProgress(false) >= practiceTicket.Target:
                    lesson.Value = Lesson.OpenDoor;
                    break;
                case Lesson.OpenDoor when train.ServiceDoorOpened:
                    lesson.Value = Lesson.CloseDoor;
                    break;
                case Lesson.CloseDoor when DoorsClosed:
                    lesson.Value = Lesson.Power;
                    break;
                case Lesson.Power when PowerOn:
                    lesson.Value = Lesson.Bell;
                    break;
                case Lesson.Bell when BellRung:
                    lesson.Value = Lesson.Drive;
                    break;
                case Lesson.Drive when (train.transform.position - initialTrainPosition).sqrMagnitude >= 25f && train.ServiceSpeed < .22f:
                    lesson.Value = Lesson.Finished;
                    Finish(true);
                    break;
            }
        }

        private bool CrewAt(BoxCollider zone)
        {
            foreach (var client in NetworkManager.ConnectedClientsList)
                if (client.PlayerObject && ServiceStation.Contains(zone, client.PlayerObject.transform.position))
                    return true;
            return false;
        }

        private void Finish(bool succeeded)
        {
            resultTime.Value = TimerRunning ? TimeLeft : 0;
            resultReadyAt = NetworkManager.ServerTime.Time + .75;
            phase.Value = succeeded ? RunPhase.Won : RunPhase.Failed;
        }

        internal static int TimeLimit(int crew) => crew switch { <= 1 => 300, 2 => 270, 3 => 240, _ => 210 };
    }
}
