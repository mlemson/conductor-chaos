using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Read-only HUD for the physics trial, including the server's fuel state.
    /// </summary>
    public sealed class ChaosTrialHud : MonoBehaviour
    {
        public static bool IsVisible { get; private set; }

        private Canvas canvas;
        private Text networkText;
        private Text playerText;
        private RectTransform fuelPanel;
        private Text fuelText, stokingHint;
        private TrainDrive train;
        private PlaygroundPlayer localPlayer;
        private float nextRefresh;
        private float hideAt;

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
            hideAt = Time.unscaledTime + 10f;
            canvas = ChaosUI.MakeCanvas(transform, "Gameplay HUD / local only", false);
            canvas.sortingOrder = 20;
            RectTransform root = (RectTransform)canvas.transform;

            RectTransform header = ChaosUI.Panel(root, "Trial status", V(.018f, .865f), V(.245f, .978f),
                ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(header, "Train", "train", V(.025f, .20f), V(.25f, .94f), ChaosUI.Paper);
            ChaosUI.Label(header, "Mode", "TRAIN PLAYGROUND", 19, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(.26f, .56f), V(.98f, .96f));
            networkText = ChaosUI.Label(header, "Network state", "", 17,
                ChaosUI.GoldLight, TextAnchor.MiddleLeft, FontStyle.Bold, V(.26f, .29f), V(.97f, .61f));
            playerText = ChaosUI.Label(header, "Connected players", "", 15,
                ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(.26f, .03f), V(.97f, .32f));
            fuelPanel = ChaosUI.Panel(root, "Firebox status", V(.018f, .775f), V(.29f, .855f), ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(fuelPanel, "Coal", "coal", V(.025f, .13f), V(.18f, .87f), ChaosUI.Paper);
            fuelText = ChaosUI.Label(fuelPanel, "Fuel", "", 17, ChaosUI.GoldLight,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(.19f, .49f), V(.98f, .96f));
            stokingHint = ChaosUI.Label(fuelPanel, "Stoking hint", "", 15, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Normal, V(.19f, .05f), V(.98f, .51f));
            fuelPanel.gameObject.SetActive(false);

            RectTransform controls = ChaosUI.Panel(root, "Controls", V(.21f, .018f), V(.79f, .086f),
                ChaosUI.Slate, 2, ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(controls, "Controls", "controls", V(.012f, .08f), V(.075f, .92f), ChaosUI.Paper);
            Hint("WASD", "Lopen", .085f, .24f);
            Hint("Shift", "Rennen", .245f, .40f);
            Hint("Spatie", "Springen", .405f, .59f);
            Hint("LMB / RMB", "Grijpen", .595f, .815f);
            Hint("Esc", "Menu", .82f, .985f);

            void Hint(string key, string action, float left, float right)
            {
                float split = Mathf.Lerp(left, right, .52f);
                RectTransform cap = ChaosUI.Panel(controls, key, V(left, .21f), V(split, .79f), ChaosUI.Paper);
                Fit(ChaosUI.Label(cap, "Key", key, 17, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold), 17);
                Fit(ChaosUI.Label(controls, action, action, 17, ChaosUI.Paper, TextAnchor.MiddleLeft,
                    FontStyle.Normal, V(split, .12f), V(right, .88f)), 17);
            }

            canvas.enabled = !PlaygroundMenu.BlocksInput;
            Refresh();
        }

        private void Update()
        {
            canvas.enabled = !PlaygroundMenu.BlocksInput && Time.unscaledTime < hideAt;
            if (!canvas.enabled || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private static void Fit(Text text, int maxSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = maxSize;
            text.rectTransform.offsetMin = new Vector2(3f, 0f);
            text.rectTransform.offsetMax = new Vector2(-3f, 0f);
        }

        private void Refresh()
        {
            if (!networkText || !playerText) return;
            var network = NetworkManager.Singleton;
            if (!train) train = FindAnyObjectByType<TrainDrive>();
            if (!localPlayer || !localPlayer.IsSpawned)
            {
                foreach (var player in FindObjectsByType<PlaygroundPlayer>())
                    if (player.IsSpawned && player.IsOwner) { localPlayer = player; break; }
            }
            bool showFuel = train && train.HasCoalFurnace && localPlayer && localPlayer.IsSpawned &&
                            (localPlayer.transform.position - train.transform.position).sqrMagnitude < 16f;
            fuelPanel.gameObject.SetActive(showFuel);
            if (showFuel)
            {
                fuelText.text = $"Brandstof {Mathf.CeilToInt(train.FuelFraction * 100f)}%";
                stokingHint.text = "Pak een kool en gooi hem in de open oven.";
            }
            if (!network || !network.IsListening)
            {
                networkText.text = "Offline";
                playerText.text = "Geen actieve sessie";
            }
            else if (network.IsServer)
            {
                networkText.text = !network.IsHost ? "Server" :
                    network.TryGetComponent<PlaygroundHost>(out var host) && !string.IsNullOrEmpty(host.JoinCode) ? "Online host" : "Solo";
                playerText.text = network.ConnectedClientsIds.Count + " verbonden";
            }
            else if (network.IsConnectedClient)
            {
                networkText.text = "Verbonden";
                playerText.text = "Speelt via de host";
            }
            else
            {
                networkText.text = "Verbinding maken…";
                playerText.text = "Wacht op de server";
            }
        }
    }
}
