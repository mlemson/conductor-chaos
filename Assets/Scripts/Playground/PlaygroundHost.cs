using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConductorChaos.Stations;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkManager))]
    public sealed class PlaygroundHost : MonoBehaviour
    {
        [SerializeField] private GameObject remotePlayerPrefab;
        private const ushort SoloPort = 7777;
        internal const int Capacity = 6;
        private const string RelayProtocol = "dtls";
        private const string LobbyStateMessage = "Playground.LobbyState";
        private const string LobbyScene = "TrainPlayground";
        private static PlaygroundHost persistentHost;
        private NetworkManager manager;
        private UnityTransport transport;
        private bool preparingOnline;
        private string pendingHostCode;
        private float connectionDeadline;
        private bool returningToLobby;
        public enum SessionPhase : byte { Offline, Connecting, Lobby, Loading, Playing }
        public SessionPhase Phase { get; private set; }
        public string JoinCode { get; private set; }
        public string Message { get; private set; }
        public int PlayerCount { get; private set; }
        public bool IsHost => manager && manager.IsHost;
        public bool HasServiceScenes => Application.CanStreamedLevelBeLoaded(ServiceRun.IntroScene) &&
                                        Application.CanStreamedLevelBeLoaded(ServiceRun.FirstLevelScene);
        public bool CanStart => Phase == SessionPhase.Offline && !preparingOnline &&
                                !returningToLobby && manager && !manager.IsListening && !manager.ShutdownInProgress;

        public void Configure(GameObject prefab) => remotePlayerPrefab = prefab;

        private void Awake()
        {
            if (persistentHost && persistentHost != this)
            {
                Destroy(gameObject);
                return;
            }
            persistentHost = this;
            manager = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            manager.OnClientConnectedCallback += OnConnected;
            manager.OnClientDisconnectCallback += OnDisconnected;
            manager.OnTransportFailure += OnTransportFailure;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.ConnectionApprovalCallback += ApproveConnection;
        }

        private void Start()
        {
            if (Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-playground-host") >= 0)
                StartLocalTrial();
        }

        public void StartLocal()
        {
            if (!CanStart) return;
            if (!HasServiceScenes)
            {
                StartLocalTrial();
                return;
            }
            transport.SetConnectionData(true, "127.0.0.1", SoloPort, "127.0.0.1");
            if (StartHost(SessionPhase.Lobby, "De solo-dienst kon niet starten."))
                LoadServiceScene(ServiceRun.IntroScene);
        }

        private void StartLocalTrial()
        {
            if (!CanStart) return;
            transport.SetConnectionData(true, "127.0.0.1", SoloPort, "127.0.0.1");
            StartHost(SessionPhase.Playing, "Solo spelen kon niet worden gestart.");
        }

        public void HostLobby() => _ = ConnectOnline(null);

        public void JoinLobby(string code)
        {
            if (!CanStart) return;
            code = code?.Trim();
            if (string.IsNullOrEmpty(code) || code.Length < 6 || code.Length > 12)
            {
                Message = "Voer de online lobbycode van de host in.";
                return;
            }
            _ = ConnectOnline(code);
        }

        private async Task ConnectOnline(string code)
        {
            if (!CanStart) return;
            if (string.IsNullOrEmpty(Application.cloudProjectId))
            {
                Message = "Online spelen is nog niet ingesteld voor deze build.";
                Debug.LogWarning("Koppel het Unity Cloud-project via Project Settings > Services en bouw de client opnieuw.", this);
                return;
            }

            preparingOnline = true;
            Phase = SessionPhase.Connecting;
            connectionDeadline = Time.unscaledTime + 30f;
            Message = "Aanmelden bij Unity…";
            string failure = "Online aanmelden mislukt. Controleer je internetverbinding.";
            try
            {
                await UnityServices.InitializeAsync();
                if (!this || Phase != SessionPhase.Connecting) return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (!this || Phase != SessionPhase.Connecting) return;

                if (code == null)
                {
                    Message = "Online lobby maken…";
                    failure = "De online lobby kon niet worden gemaakt.";
                    var allocation = await RelayService.Instance.CreateAllocationAsync(Capacity - 1);
                    if (!this || Phase != SessionPhase.Connecting) return;
                    string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if (!this || Phase != SessionPhase.Connecting) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, RelayProtocol));
                    if (StartHost(SessionPhase.Connecting, failure))
                    {
                        pendingHostCode = joinCode;
                        Message = "Lobby verbinden met Relay…";
                        connectionDeadline = Time.unscaledTime + 15f;
                    }
                }
                else
                {
                    Message = "Online lobby zoeken…";
                    failure = "De online lobby is niet bereikbaar. Controleer de code en vraag de host of de lobby nog open is.";
                    var allocation = await RelayService.Instance.JoinAllocationAsync(code);
                    if (!this || Phase != SessionPhase.Connecting) return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, RelayProtocol));
                    StartGuest();
                }
            }
            catch (Exception ex)
            {
                if (!this || Phase == SessionPhase.Offline) return;
                string error = ex is RequestFailedException request ? $"{ex.GetType().Name}, {request.ErrorCode}" : ex.GetType().Name;
                Debug.LogWarning($"Online sessie mislukt ({error}).", this);
                Leave();
                Message = failure;
            }
            finally
            {
                preparingOnline = false;
            }
        }

        private bool StartHost(SessionPhase phase, string failure)
        {
            if (!remotePlayerPrefab)
            {
                ResetSession("De spelersprefab ontbreekt in deze scène.");
                return false;
            }
            Phase = phase;
            Message = null;
            if (manager.StartHost())
            {
                PlayerCount = manager.ConnectedClientsIds.Count;
                return true;
            }
            ResetSession(failure);
            return false;
        }

        private void StartGuest()
        {
            Message = "Verbinden met de host…";
            connectionDeadline = Time.unscaledTime + 15f;
            if (!manager.StartClient())
            {
                ResetSession("De verbinding kon niet worden gestart.");
                return;
            }
            manager.CustomMessagingManager.RegisterNamedMessageHandler(LobbyStateMessage, ReceiveLobbyState);
        }

        public void BeginGame()
        {
            if (!IsHost || Phase != SessionPhase.Lobby) return;
            if (HasServiceScenes)
            {
                LoadServiceScene(ServiceRun.IntroScene);
                return;
            }
            Phase = SessionPhase.Playing;
            BroadcastLobbyState();
        }

        internal void LoadServiceScene(string scene)
        {
            if (!IsHost || Phase is not (SessionPhase.Lobby or SessionPhase.Playing)) return;
            if (scene != ServiceRun.IntroScene && scene != ServiceRun.FirstLevelScene) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Message = "De dienstscène ontbreekt in deze build.";
                return;
            }

            var scenes = manager.SceneManager;
            scenes.OnLoadEventCompleted -= OnServiceLoaded;
            scenes.OnLoadEventCompleted += OnServiceLoaded;
            var previous = Phase;
            Phase = SessionPhase.Loading;
            if (scenes.LoadScene(scene, LoadSceneMode.Single) != SceneEventProgressStatus.Started)
            {
                scenes.OnLoadEventCompleted -= OnServiceLoaded;
                Phase = previous;
                Message = "De volgende scène kon niet laden.";
            }
            BroadcastLobbyState();
        }

        private void OnServiceLoaded(string scene, LoadSceneMode mode, List<ulong> loaded, List<ulong> timedOut)
        {
            if (!IsHost || Phase != SessionPhase.Loading) return;
            manager.SceneManager.OnLoadEventCompleted -= OnServiceLoaded;
            var run = ServiceRun.Active;
            if (!run || run.gameObject.scene.name != scene || !run.IsConfigured || !loaded.Contains(NetworkManager.ServerClientId))
            {
                Leave();
                Message = "De dienstscène heeft onvolledige stations- of routegegevens.";
                return;
            }
            foreach (ulong id in timedOut)
                if (id != NetworkManager.ServerClientId && manager.ConnectedClients.ContainsKey(id))
                    manager.DisconnectClient(id, "Je scène kon niet op tijd laden.");

            Transform spawn = run.Departure.CrewSpawn;
            int seat = 0;
            foreach (ulong id in loaded)
            {
                if (!manager.ConnectedClients.ContainsKey(id)) continue;
                float column = seat % 3 - 1f;
                float row = seat / 3;
                Vector3 position = spawn.position + spawn.right * column * .75f - spawn.forward * row * .8f;
                SpawnPlayer(id, position, spawn.rotation);
                seat++;
            }
            if (!run.BeginOnServer())
            {
                Leave();
                Message = "De dienst kon niet worden gestart.";
                return;
            }
            Phase = SessionPhase.Playing;
            BroadcastLobbyState();
        }

        public void Leave()
        {
            ResetSession(null);
            returningToLobby = SceneManager.GetActiveScene().name != LobbyScene;
            if (manager && manager.IsListening) manager.Shutdown();
        }

        private void ResetSession(string message)
        {
            Phase = SessionPhase.Offline;
            JoinCode = null;
            pendingHostCode = null;
            PlayerCount = 0;
            Message = message;
        }

        private void Update()
        {
            if (!manager) return;
            if (returningToLobby && !manager.IsListening && !manager.ShutdownInProgress)
            {
                returningToLobby = false;
                SceneManager.LoadScene(LobbyScene);
                return;
            }
            if (pendingHostCode != null && IsHost &&
                transport.GetNetworkDriver().GetRelayConnectionStatus() == RelayConnectionStatus.Established)
            {
                JoinCode = pendingHostCode;
                pendingHostCode = null;
                Phase = SessionPhase.Lobby;
                Message = null;
            }
            if (Phase == SessionPhase.Connecting && Time.unscaledTime >= connectionDeadline)
            {
                Leave();
                Message = "De verbinding duurde te lang. Controleer je internetverbinding en probeer opnieuw.";
            }
            if (IsHost && manager.ConnectedClientsIds.Count != PlayerCount) BroadcastLobbyState();
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool online = transport.Protocol == UnityTransport.ProtocolType.RelayUnityTransport;
            response.Approved = request.ClientNetworkId == NetworkManager.ServerClientId ||
                                (online && (Phase is SessionPhase.Connecting or SessionPhase.Lobby) &&
                                 manager.ConnectedClientsIds.Count < Capacity);
            response.CreatePlayerObject = false;
            if (!response.Approved) response.Reason = !online ? "Deze sessie is alleen voor solo spelen."
                : Phase is SessionPhase.Loading or SessionPhase.Playing ? "Deze dienst is al gestart." : "De lobby is vol.";
        }

        private void BroadcastLobbyState()
        {
            PlayerCount = manager.ConnectedClientsIds.Count;
            using var writer = new FastBufferWriter(sizeof(byte) + sizeof(int), Allocator.Temp);
            writer.WriteValueSafe((byte)Phase);
            writer.WriteValueSafe(PlayerCount);
            foreach (ulong clientId in manager.ConnectedClientsIds)
                if (clientId != NetworkManager.ServerClientId)
                    manager.CustomMessagingManager.SendNamedMessage(LobbyStateMessage, clientId, writer);
        }

        private void ReceiveLobbyState(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || Phase == SessionPhase.Offline ||
                !reader.TryBeginRead(sizeof(byte) + sizeof(int))) return;
            reader.ReadValue(out byte serverPhase);
            reader.ReadValue(out int count);
            PlayerCount = Mathf.Clamp(count, 1, Capacity);
            Phase = (SessionPhase)serverPhase switch
            {
                SessionPhase.Loading => SessionPhase.Loading,
                SessionPhase.Playing => SessionPhase.Playing,
                _ => SessionPhase.Lobby
            };
            Message = null;
        }

        private void OnConnected(ulong clientId)
        {
            if (!manager.IsServer || clientId == NetworkManager.ServerClientId) return;
            if (!HasServiceScenes)
            {
                Vector3 position = new Vector3(11f + 1.2f * (manager.ConnectedClientsIds.Count - 2), 0.3f, 3f);
                SpawnPlayer(clientId, position, Quaternion.identity);
            }
            BroadcastLobbyState();
        }

        private void SpawnPlayer(ulong clientId, Vector3 position, Quaternion rotation)
        {
            if (manager.ConnectedClients[clientId].PlayerObject is { IsSpawned: true } old) old.Despawn();
            var instance = Instantiate(remotePlayerPrefab, position, rotation);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }

        private void OnDisconnected(ulong clientId)
        {
            if (IsHost || Phase == SessionPhase.Offline) return;
            ResetSession(string.IsNullOrEmpty(manager.DisconnectReason) ? "Verbinding met de host verbroken." : manager.DisconnectReason);
            returningToLobby = SceneManager.GetActiveScene().name != LobbyScene;
        }

        private void OnTransportFailure()
        {
            if (Phase == SessionPhase.Offline) return;
            Leave();
            Message = "De netwerkverbinding is weggevallen.";
        }

        private void OnDestroy()
        {
            if (persistentHost == this) persistentHost = null;
            if (!manager) return;
            manager.OnClientConnectedCallback -= OnConnected;
            manager.OnClientDisconnectCallback -= OnDisconnected;
            manager.OnTransportFailure -= OnTransportFailure;
            manager.ConnectionApprovalCallback -= ApproveConnection;
        }
    }
}
