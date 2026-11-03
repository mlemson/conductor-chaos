using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace ConductorChaos.Playground
{
    // Direct transport is only for the local physics trial; Relay/Lobby is separate work.
    [RequireComponent(typeof(NetworkManager))]
    public sealed class PlaygroundHost : MonoBehaviour
    {
        [SerializeField] private GameObject remotePlayerPrefab;
        private NetworkManager manager;

        public void Configure(GameObject prefab) => remotePlayerPrefab = prefab;

        private void Start()
        {
            manager = GetComponent<NetworkManager>();
            if (!remotePlayerPrefab)
            {
                Debug.LogError("Playground: configure the remote player prefab before starting.");
                return;
            }
            manager.OnClientConnectedCallback += OnConnected;
            string[] args = Environment.GetCommandLineArgs();
            bool client = Array.IndexOf(args, "-playground-client") >= 0;
            int addressIndex = Array.IndexOf(args, "-playground-address");
            if (client && addressIndex >= 0 && addressIndex + 1 < args.Length)
                GetComponent<UnityTransport>().SetConnectionData(args[addressIndex + 1], 7777);
            bool started = client ? manager.StartClient() : manager.StartHost();
            if (!started) Debug.LogError("Playground: network session could not start.");
        }

        private void OnConnected(ulong clientId)
        {
            if (!manager.IsServer || clientId == NetworkManager.ServerClientId) return;
            Vector3 position = new Vector3(11f + 1.2f * (manager.ConnectedClientsIds.Count - 2), 0.3f, 3f);
            GameObject instance = Instantiate(remotePlayerPrefab, position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
        }

        private void OnDestroy()
        {
            if (manager) manager.OnClientConnectedCallback -= OnConnected;
        }
    }
}
