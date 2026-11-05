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
        public static bool IsVisible { get; private set; }

        private Text networkText;
        private Text playerText;
        private float nextRefresh;

        private void OnEnable() => IsVisible = true;
        private void OnDisable() => IsVisible = false;

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

            RectTransform header = ChaosUI.Panel(root, "Trial status", V(.018f, .865f), V(.245f, .978f), ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(header, "Train", "train", V(.025f, .20f), V(.25f, .94f), ChaosUI.Paper);
            ChaosUI.Label(header, "Mode", "TRAIN PLAYGROUND", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.26f, .56f), V(.98f, .96f));
            networkText = ChaosUI.Label(header, "Network state", "", 17, ChaosUI.GoldLight, TextAnchor.MiddleLeft, FontStyle.Bold, V(.26f, .29f), V(.97f, .61f));
            playerText = ChaosUI.Label(header, "Connected players", "", 15, ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(.26f, .03f), V(.97f, .32f));

            RectTransform controls = ChaosUI.Panel(root, "Controls", V(.21f, .018f), V(.79f, .086f), ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(controls, "Controls", "controls", V(.02f, .12f), V(.10f, .88f), ChaosUI.Paper);
            ChaosUI.Label(controls, "Controls label", "WASD  Lopen     Muis  Kijken     LMB / RMB  Grijpen     Spatie  Springen", 16,
                ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.11f, .1f), V(.99f, .9f));
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
