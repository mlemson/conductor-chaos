using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Read-only HUD for the existing physics trial. Never sends input or network RPCs;
    /// no invented train speed, objectives, coal, damage, or player health.
    /// </summary>
    public sealed class ChaosTrialHud : MonoBehaviour
    {
        private Text networkText;
        private Text playerText;
        private float nextRefresh;

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForPlayground()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "TrainPlayground" || Application.isBatchMode) return;
            if (Object.FindAnyObjectByType<ChaosTrialHud>()) return;
            new GameObject("Chaos UI — Physics trial HUD").AddComponent<ChaosTrialHud>();
        }

        private void Start()
        {
            Canvas canvas = ChaosUI.MakeCanvas(transform, "Gameplay HUD / local only", false);
            canvas.sortingOrder = 20;
            RectTransform root = (RectTransform)canvas.transform;

            RectTransform header = ChaosUI.Panel(root, "Trial status", V(0.022f, 0.815f), V(0.255f, 0.976f),
                ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(header, "Train sprite", "train",
                V(0.035f, 0.64f), V(0.19f, 0.95f), ChaosUI.GoldLight);
            ChaosUI.Label(header, "Mode", "PHYSICS PLAYGROUND", 20, ChaosUI.GoldLight,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.20f, 0.57f), V(0.945f, 0.95f));
            ChaosUISpriteCatalog.Add(header, "Crew sprite", "player-group",
                V(0.035f, 0.10f), V(0.16f, 0.50f), ChaosUI.Paper);
            networkText = ChaosUI.Label(header, "Network state", "Starting network...", 17,
                ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(0.17f, 0.27f), V(0.94f, 0.62f));
            playerText = ChaosUI.Label(header, "Connected players", "Waiting for local trial", 15,
                ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(0.17f, 0.05f), V(0.94f, 0.33f));

            RectTransform controls = ChaosUI.Panel(root, "Controls", V(0.245f, 0.015f), V(0.755f, 0.095f),
                ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(controls, "Controls sprite", "controls",
                V(0.025f, 0.12f), V(0.105f, 0.88f), ChaosUI.Paper);
            ChaosUI.Label(controls, "Controls label",
                "WASD MOVE   MOUSE LOOK   LMB/RMB GRIP   SPACE JUMP   ESC CURSOR",
                15, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold,
                V(0.115f, 0.13f), V(0.975f, 0.87f));

            RectTransform note = ChaosUI.Panel(root, "Unimplemented state reminder", V(0.74f, 0.915f),
                V(0.98f, 0.973f), ChaosUI.Dark);
            ChaosUI.Label(note, "Note", "UI TRIAL · NO TRAIN SYSTEMS YET", 13,
                ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void Refresh()
        {
            if (!networkText || !playerText) return;
            var network = NetworkManager.Singleton;
            if (!network || !network.IsListening)
            {
                networkText.text = "NETWORK OFFLINE";
                playerText.text = "No active test session";
            }
            else if (network.IsServer)
            {
                networkText.text = network.IsHost ? "LOCAL HOST" : "SERVER";
                playerText.text = network.ConnectedClientsIds.Count + " connected to host";
            }
            else if (network.IsConnectedClient)
            {
                networkText.text = "CONNECTED CLIENT";
                playerText.text = "Remote test · no real lobby";
            }
            else
            {
                networkText.text = "CONNECTING...";
                playerText.text = "Waiting for server";
            }
        }
    }
}
