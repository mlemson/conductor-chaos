using System.Collections;
using System.Collections.Generic;
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
        public const string SecondLevelScene = "ServiceLevel02";
        public const string ThirdLevelScene = "ServiceLevel03";

        /// <summary>Alle dienstscènes in speelvolgorde (introductie, level 1, 2, 3).</summary>
        public static readonly string[] ServiceScenes = { IntroScene, FirstLevelScene, SecondLevelScene, ThirdLevelScene };
        public static bool IsServiceScene(string scene) => System.Array.IndexOf(ServiceScenes, scene) >= 0;
        private static readonly string[] CampaignStops = { "Whistleton", "Bumblewick", "Puddleford", "Craggleton", "Eaglecrest" };

        internal static string PlannedStationName(string scene, ServiceStation.StationRole role)
        {
            int index = System.Array.IndexOf(ServiceScenes, scene);
            if (index < 0 || (index == 0 && role == ServiceStation.StationRole.Aankomst)) return null;
            return CampaignStops[index + (role == ServiceStation.StationRole.Aankomst ? 1 : 0)];
        }

        /// <summary>De scène na een gewonnen dienst, of null als dit het laatste level is.</summary>
        public static string NextScene(string scene)
        {
            int index = System.Array.IndexOf(ServiceScenes, scene);
            return index >= 0 && index + 1 < ServiceScenes.Length ? ServiceScenes[index + 1] : null;
        }

        public enum RunPhase { Loading, Briefing, Training, Departure, Travelling, Arrival, Won, Failed }
        public enum Lesson { Walk, Carry, Ticket, OpenDoor, CloseDoor, Bell, Power, Drive, Finished }

        [SerializeField] private bool introduction;
        [SerializeField, Min(0)] private int extraTravelSeconds;
        [SerializeField] private ServiceStation departure;
        [SerializeField] private ServiceStation arrival;
        [SerializeField] private TrainDrive train;
        [SerializeField] private Rigidbody practiceLuggage;
        [SerializeField] private BoxCollider practiceDrop;
        [SerializeField] private ServiceObjective practiceTicket;
        [SerializeField] private ServiceObjective[] objectives;
        [Tooltip("Level 2+: alle ingestelde opdrachten tegelijk in plaats van één willekeurige.")]
        [SerializeField] private bool combineObjectives;
        [Tooltip("Kies per rit twee of drie opdrachten op het vertrekperron.")]
        [SerializeField] private bool rotateDepartureObjectives;
        [Tooltip("Tekst in de briefing, bv. LEVEL 2.")]
        [SerializeField] private string levelLabel = "LEVEL 1";
        [Tooltip("Tijdslimiet per crewgrootte (1, 2, 3, 4+ spelers) in seconden. Leeg = de standaard van level 1.")]
        [SerializeField] private int[] timeLimits;

        private readonly NetworkVariable<RunPhase> phase = new();
        private readonly NetworkVariable<Lesson> lesson = new();
        private readonly NetworkVariable<int> selected = new(-1);
        private readonly NetworkVariable<int> objectiveSelection = new(-1);
        private readonly NetworkVariable<int> progress = new();
        private readonly NetworkVariable<int> cargoLoaded = new();
        private readonly NetworkVariable<int> cargoTravelled = new();
        private readonly NetworkVariable<byte> suitcaseStatus = new();
        private readonly NetworkVariable<Vector3> suitcasePosition = new();
        private readonly NetworkVariable<int> ticketAboard = new();
        internal const int WrongTicketPenaltySeconds = 10;
        private readonly NetworkVariable<double> deadline = new();
        private readonly NetworkVariable<int> ticketPenalty = new();
        // The server freezes the crew's selected soundtrack duration when the timer starts.
        private readonly NetworkVariable<int> musicDuration = new();
        private readonly NetworkVariable<double> musicStart = new();
        private readonly NetworkVariable<int> resultTime = new();
        private readonly NetworkVariable<bool> bellRung = new();
        private readonly NetworkVariable<bool> doorsClosed = new();
        private readonly NetworkVariable<bool> powerOn = new();
        private readonly NetworkVariable<bool> offTrack = new();
        // Voortgang per opdracht (index = objectives[]), voor de checklist op alle clients.
        private readonly NetworkList<int> objectiveProgress = new();
        // Per opdracht: hoeveel er nu aan boord is (vracht) of 1 = aan boord (oma); -1 = niet van toepassing.
        private readonly NetworkList<int> objectiveAboard = new();
        private readonly List<ServiceObjective> activeObjectives = new();
        private int activeFor = int.MinValue;      // voor welke 'selected' de lijst hierboven is opgebouwd
        private Vector3 initialTrainPosition;
        private float stopSeconds;
        private double resultReadyAt;
        private BoxCollider bellButton;
        private AudioClip bellSound;
        private readonly Collider[] bellContacts = new Collider[12];

        public static ServiceRun Active { get; private set; }
        public bool Introduction => introduction;
        public ServiceStation Departure => departure;
        public ServiceStation Arrival => arrival;
        public RunPhase Phase => phase.Value;
        public Lesson CurrentLesson => lesson.Value;
        public bool CombinesObjectives => combineObjectives && !introduction;
        internal bool RotatesDepartureObjectives => CombinesObjectives && rotateDepartureObjectives;
        public string LevelLabel => string.IsNullOrWhiteSpace(levelLabel) ? "LEVEL 1" : levelLabel;
        internal TrainDrive Train => train;

        /// <summary>Eén opdracht (level 1) of, bij gecombineerde opdrachten, de eerste die nog niet af is.</summary>
        public ServiceObjective Objective
        {
            get
            {
                if (!CombinesObjectives)
                    return selected.Value >= 0 && selected.Value < (objectives?.Length ?? 0) ? objectives[selected.Value] : null;
                ServiceObjective last = null;
                foreach (var objective in ActiveObjectives)
                {
                    last = objective;
                    if (ProgressOf(objective) < objective.Target) return objective;
                }
                return last;
            }
        }

        /// <summary>Alle opdrachten van deze dienst (op server en clients dezelfde volgorde).</summary>
        public IReadOnlyList<ServiceObjective> ActiveObjectives
        {
            get
            {
                // Eén keer opbouwen (de opdrachten veranderen niet tijdens een dienst), zodat iemand die de lijst
                // doorloopt en ondertussen Objective opvraagt nooit een lijst ziet die verandert.
                int key = CombinesObjectives ? objectiveSelection.Value : selected.Value;
                if (activeFor == key) return activeObjectives;
                activeFor = key;
                activeObjectives.Clear();
                if (objectives == null) return activeObjectives;
                if (CombinesObjectives)
                {
                    for (int i = 0; i < objectives.Length; i++)
                        if (objectives[i] && objectives[i].IsConfigured &&
                            (!RotatesDepartureObjectives || (i < 32 && (key & (1 << i)) != 0)))
                            activeObjectives.Add(objectives[i]);
                }
                else if (selected.Value >= 0 && selected.Value < objectives.Length && objectives[selected.Value])
                    activeObjectives.Add(objectives[selected.Value]);
                return activeObjectives;
            }
        }

        /// <summary>Voortgang van één opdracht zoals de server die laatst doorgaf.</summary>
        public int ProgressOf(ServiceObjective objective)
        {
            int index = objectives == null ? -1 : System.Array.IndexOf(objectives, objective);
            if (!CombinesObjectives) return objective && objective == Objective ? progress.Value : 0;
            return index >= 0 && index < objectiveProgress.Count ? objectiveProgress[index] : 0;
        }

        /// <summary>Aantal aan boord voor deze opdracht (vracht), of -1 als dat niet bekend is.</summary>
        public int AboardOf(ServiceObjective objective)
        {
            if (!CombinesObjectives) return objective is ServiceCargo ? cargoLoaded.Value : objective is ServiceTickets ? ticketAboard.Value : -1;
            int index = objectives == null ? -1 : System.Array.IndexOf(objectives, objective);
            return index >= 0 && index < objectiveAboard.Count ? objectiveAboard[index] : -1;
        }

        public int TotalTarget
        {
            get
            {
                if (!CombinesObjectives) return Objective ? Objective.Target : 0;
                int total = 0;
                foreach (var objective in ActiveObjectives) total += objective.Target;
                return total;
            }
        }

        public int Progress => progress.Value;
        public int CargoLoaded => cargoLoaded.Value;
        public int CargoTravelled => cargoTravelled.Value;
        internal byte SuitcaseStatus => suitcaseStatus.Value;
        internal Vector3 SuitcasePosition => suitcasePosition.Value;
        internal string SteamWarning => train ? train.SteamWarning : null;
        public bool BellRung => bellRung.Value;
        public bool DoorsClosed => doorsClosed.Value;
        public bool PowerOn => powerOn.Value;
        public bool NeedsCoal => train.HasCoalFurnace && train.FuelFraction <= 0f;
        public bool EscortDoorOpen
        {
            get
            {
                foreach (var objective in ActiveObjectives)
                    if (objective is ServiceEscort escort) return train.IsServiceDoorOpen(escort.Doorway);
                return false;
            }
        }
        internal bool CanBoardAt(Transform doorway) => train.ServiceSpeed < .22f && train.IsServiceDoorOpen(doorway);
        public bool OffTrack => offTrack.Value;
        public bool TimerRunning => deadline.Value > 0;
        public int MusicDuration => musicDuration.Value;
        public double MusicStartedAt => musicStart.Value;
        internal bool CanContinue => Phase == RunPhase.Briefing ||
            (Phase is RunPhase.Won or RunPhase.Failed) && NetworkManager.ServerTime.Time >= resultReadyAt;
        public int TimeLeft => Phase is RunPhase.Won or RunPhase.Failed ? resultTime.Value :
            Mathf.Max(0, Mathf.CeilToInt((float)(deadline.Value - ticketPenalty.Value - NetworkManager.ServerTime.Time)));

        internal bool TryApplyWrongTicketPenalty()
        {
            if (!IsServer || Phase is not (RunPhase.Departure or RunPhase.Travelling or RunPhase.Arrival)) return false;
            ticketPenalty.Value += WrongTicketPenaltySeconds;
            return true;
        }

        // Only the campaign's train is subject to the departure checklist.
        internal bool AllowsMotion(TrainDrive candidate) => candidate != train ||
            (doorsClosed.Value && (introduction
                ? Phase == RunPhase.Training && CurrentLesson == Lesson.Drive
                : (Phase is RunPhase.Departure or RunPhase.Travelling or RunPhase.Arrival) &&
                  TimerRunning && bellRung.Value &&
                  // Loading is a departure checklist item, not a mid-journey
                  // brake. Cargo can move or be unloaded after departure.
                  (Phase != RunPhase.Departure || ReadyToDepart)));

        // Server: alle opdrachten van deze dienst klaar om te vertrekken (kaartjes geknipt, vracht en oma aan boord).
        private bool ReadyToDepart
        {
            get
            {
                if (!CombinesObjectives) return Objective?.ReadyToDepart ?? false;
                foreach (var objective in ActiveObjectives)
                    if (!objective.ReadyToDepart)
                        return false;
                return ActiveObjectives.Count > 0;
            }
        }
        internal bool IsTrainingTrain(TrainDrive candidate) => introduction && candidate == train;

        public bool IsConfigured => departure && departure.Role == ServiceStation.StationRole.Vertrek &&
            departure.CrewSpawn && departure.Platform && departure.TrainStop && departure.DepartureBell &&
            departure.DepartureBell.TryGetComponent<BoxCollider>(out _) && train &&
            (introduction ? practiceLuggage && practiceDrop && practiceTicket && practiceTicket.IsConfigured :
                arrival && arrival.Role == ServiceStation.StationRole.Aankomst &&
                arrival.TrainStop && objectives != null && System.Array.Exists(objectives, o => o && o.IsConfigured));

        private void Start() => ServiceStation.ApplyRouteSigns(departure, arrival);

        public override void OnNetworkSpawn()
        {
            Active = this;
            if (departure && departure.DepartureBell) bellButton = departure.DepartureBell.GetComponent<BoxCollider>();
            bellSound = Resources.Load<AudioClip>("ServiceDepartureBell");
            bellRung.OnValueChanged += PresentBell;
        }

        public override void OnNetworkDespawn()
        {
            if (Active == this) Active = null;

            bellRung.OnValueChanged -= PresentBell;
        }

        internal bool BeginOnServer()
        {
            if (!IsServer || !IsSpawned || !IsConfigured || Phase != RunPhase.Loading) return false;
            if (!introduction)
                foreach (var objective in objectives)
                    if (objective is ServiceTickets tickets) tickets.AssignWrongTicket();
            if (CombinesObjectives)
            {
                selected.Value = -1;
                if (RotatesDepartureObjectives) objectiveSelection.Value = PickDepartureObjectives();
                objectiveProgress.Clear();
                objectiveAboard.Clear();
                for (int i = 0; i < objectives.Length; i++)
                {
                    objectiveProgress.Add(0);
                    objectiveAboard.Add(-1);
                }
                foreach (var objective in ActiveObjectives) objective.BeginOnServer();
                cargoLoaded.Value = 0;
                cargoTravelled.Value = 0;
            }
            else if (!introduction)
            {
                var eligible = System.Array.FindAll(objectives, o => o && o.IsConfigured);
                var objective = eligible[Random.Range(0, eligible.Length)];
                selected.Value = System.Array.IndexOf(objectives, objective);
                objective.BeginOnServer();
                cargoLoaded.Value = 0;
                cargoTravelled.Value = 0;
            }
            initialTrainPosition = train.transform.position;
            phase.Value = RunPhase.Briefing;
            return true;
        }

        private int PickDepartureObjectives()
        {
            var pool = new List<int>(3);
            int mask = 0;
            for (int i = 0; i < Mathf.Min(32, objectives.Length); i++)
            {
                var objective = objectives[i];
                if (!objective || !objective.IsConfigured) continue;
                if (objective is ServiceTickets or ServiceEscort || objective is ServiceCargo && objective.Target > 1)
                    pool.Add(i);
                else if (objective is not (ServiceCargo or ServiceRepair or ServiceTrackRepair))
                    mask |= 1 << i;
            }

            int count = Mathf.Min(pool.Count, Random.Range(2, 4));
            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, pool.Count);
                mask |= 1 << pool[pick];
                pool.RemoveAt(pick);
            }
            return mask;
        }

        public void Continue() => ContinueRpc();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ContinueRpc(RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != NetworkManager.ServerClientId || !CanContinue) return;
            if (Phase == RunPhase.Briefing)
                phase.Value = introduction ? RunPhase.Training : RunPhase.Departure;
            else
            {
                var host = NetworkManager.GetComponent<PlaygroundHost>();
                string next = Phase == RunPhase.Won ? NextScene(gameObject.scene.name) : null;
                if (host) host.LoadServiceScene(next != null && Application.CanStreamedLevelBeLoaded(next) ? next : gameObject.scene.name);
            }
        }

        private void CheckBell()
        {
            bool readyToRing = Phase == RunPhase.Departure ||
                Phase == RunPhase.Training && CurrentLesson == Lesson.Bell;
            if (!readyToRing || bellRung.Value || !bellButton) return;
            Transform button = bellButton.transform;
            Vector3 half = Vector3.Scale(bellButton.size * .5f, button.lossyScale);
            Vector3 top = button.TransformPoint(bellButton.center) + button.up * half.y;
            int count = Physics.OverlapBoxNonAlloc(top + button.up * .035f, new Vector3(half.x * .8f, .045f, half.z * .8f),
                bellContacts, button.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hand = bellContacts[i];
                var player = hand.GetComponentInParent<PlaygroundPlayer>();
                if (!player || !player.IsSpawned || !player.IsHandCollider(hand) || !hand.attachedRigidbody) continue;
                if (Vector3.Dot(hand.attachedRigidbody.GetPointVelocity(hand.bounds.center), button.up) >= -.12f) continue;
                bellRung.Value = true;
                break;
            }
        }

        private void PresentBell(bool previous, bool rung)
        {
            if (!rung || !bellButton || Application.isBatchMode) return;
            if (bellSound) AudioSource.PlayClipAtPoint(bellSound, bellButton.transform.position, .8f);
            StartCoroutine(DepressBell(bellButton.transform));
        }

        private static IEnumerator DepressBell(Transform button)
        {
            Vector3 resting = button.localPosition;
            button.position -= button.up * .055f;
            yield return new WaitForSeconds(.14f);
            if (button) button.localPosition = resting;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || Phase is RunPhase.Loading or RunPhase.Briefing or RunPhase.Won or RunPhase.Failed)
                return;

            doorsClosed.Value = train.ServiceDoorsClosed;
            powerOn.Value = train.ServicePowerOn;
            CheckBell();
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

            if (CombinesObjectives)
            {
                UpdateCombinedOnServer();
                return;
            }

            if (Objective is ServiceTickets tickets) ticketAboard.Value = tickets.BoardedCount;
            var cargo = Objective as ServiceCargo;
            if (cargo != null)
            {
                cargo.ObserveLoadingOnServer();
                cargoLoaded.Value = cargo.AboardCount;
            }

            if (!TimerRunning)
            {
                if (CrewAt(departure.Platform))
                {
                    StartTimer();
                }
                progress.Value = Mathf.Clamp(Objective.ReadProgress(false), 0, Objective.Target);
                return;
            }
            if (TimeLeft == 0)
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
            if (train.ServiceSpeed >= .22f) Objective.ObserveTravelOnServer();
            if (cargo != null)
            {
                cargoTravelled.Value = cargo.TravelledCount;
                suitcaseStatus.Value = cargo.SuitcaseStatus(Phase == RunPhase.Travelling, out var position);
                if (suitcaseStatus.Value == 1 && (position - suitcasePosition.Value).sqrMagnitude > .25f)
                    suitcasePosition.Value = position;
            }
            progress.Value = Mathf.Clamp(Objective.ReadProgress(docked), 0, Objective.Target);
            if (docked && progress.Value >= Objective.Target) Finish(true);
        }

        // Level 2+: alle opdrachten tegelijk. Zelfde regels als hierboven, maar winnen pas als alles af is.
        private void UpdateCombinedOnServer()
        {
            var active = ActiveObjectives;
            ServiceCargo firstCargo = null;
            foreach (var objective in active)
                if (objective is ServiceCargo cargo)
                {
                    cargo.ObserveLoadingOnServer();
                    if (!firstCargo) firstCargo = cargo;
                    SetListValue(objectiveAboard, System.Array.IndexOf(objectives, objective), cargo.AboardCount);
                }
                else if (objective is ServiceTickets tickets)
                    SetListValue(objectiveAboard, System.Array.IndexOf(objectives, objective), tickets.BoardedCount);
            if (firstCargo) cargoLoaded.Value = firstCargo.AboardCount;

            bool docked = false;
            if (TimerRunning)
            {
                if (TimeLeft == 0)
                {
                    Finish(false);
                    return;
                }
                if (Phase == RunPhase.Departure && !ServiceStation.Contains(departure.TrainStop, train.ServiceRailPosition))
                    phase.Value = RunPhase.Travelling;
                bool stopped = ServiceStation.Contains(arrival.TrainStop, train.ServiceRailPosition) && train.ServiceSpeed < .22f;
                stopSeconds = stopped ? stopSeconds + Time.fixedDeltaTime : 0;
                docked = stopSeconds >= 1f;
                if (Phase is RunPhase.Travelling or RunPhase.Arrival)
                    phase.Value = docked ? RunPhase.Arrival : RunPhase.Travelling;
                if (train.ServiceSpeed >= .22f)
                    foreach (var objective in active) objective.ObserveTravelOnServer();
                if (firstCargo) cargoTravelled.Value = firstCargo.TravelledCount;
            }
            else if (CrewAt(departure.Platform))
            {
                StartTimer();
            }

            int total = 0;
            bool complete = active.Count > 0;
            foreach (var objective in active)
            {
                int value = Mathf.Clamp(objective.ReadProgress(docked), 0, objective.Target);
                SetListValue(objectiveProgress, System.Array.IndexOf(objectives, objective), value);
                total += value;
                if (value < objective.Target) complete = false;
            }
            progress.Value = total;
            if (TimerRunning && docked && complete) Finish(true);
        }

        private static void SetListValue(NetworkList<int> list, int index, int value)
        {
            if (index < 0 || index >= list.Count || list[index] == value) return;
            list[index] = value;
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
                    lesson.Value = Lesson.Bell;
                    break;
                case Lesson.Bell when BellRung:
                    lesson.Value = Lesson.Power;
                    break;
                case Lesson.Power when PowerOn:
                    lesson.Value = Lesson.Drive;
                    break;
                case Lesson.Drive:
                    progress.Value = Mathf.Max(progress.Value, Mathf.Min(5, Mathf.FloorToInt(Vector3.Distance(train.transform.position, initialTrainPosition))));
                    if (progress.Value == 5 && train.ServiceSpeed < .22f)
                    {
                        lesson.Value = Lesson.Finished;
                        Finish(true);
                    }
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

        /// <summary>Tijdslimiet voor deze dienst bij een crew van deze grootte (s).</summary>
        private void StartTimer()
        {
            int crew = NetworkManager.ConnectedClientsIds.Count;
            bool soloLevelOne = IsSoloLevelOne(crew);
            double now = NetworkManager.ServerTime.Time;
            musicStart.Value = now;
            musicDuration.Value = soloLevelOne ? 300 : BaseTimeLimit(crew);
            deadline.Value = now + TimeLimit(crew);
        }

        private bool IsSoloLevelOne(int crew) => crew <= 1 && gameObject.scene.name == FirstLevelScene;

        internal int TimeLimit(int crew) => IsSoloLevelOne(crew) ? 300 : BaseTimeLimit(crew) + extraTravelSeconds;

        private int BaseTimeLimit(int crew)
        {
            if (timeLimits != null && timeLimits.Length > 0)
                return Mathf.Max(30, timeLimits[Mathf.Clamp(crew, 1, timeLimits.Length) - 1]);
            return DefaultTimeLimit(crew);
        }

        internal static int DefaultTimeLimit(int crew) => crew switch { <= 1 => 300, 2 => 270, 3 => 240, _ => 210 };
    }
}
