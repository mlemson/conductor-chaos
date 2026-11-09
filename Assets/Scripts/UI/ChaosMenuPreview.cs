using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Interactive UI design sandbox. This is NOT an online lobby: the real multiplayer
    /// trial still lives in TrainPlayground. Settings are functional; routes are labeled previews.
    /// </summary>
    public sealed class ChaosMenuPreview : MonoBehaviour
    {
        private RectTransform lobby;
        private RectTransform settings;
        private RectTransform hudExample;
        private Text routeName;
        private Text info;
        private Text volumeValue;
        private Text muteValue;
        private bool muted;
        private int routeIndex;
        private readonly string[] routes = { "Quiet Valley", "Mountain Pass", "Snowy Tunnel" };

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private void Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("ChaosUI.Volume", 0.8f));
            muted = PlayerPrefs.GetInt("ChaosUI.Muted", 0) != 0;
            AudioListener.pause = muted;

            Canvas canvas = ChaosUI.MakeCanvas(transform, "Conductor Chaos | Menu Preview", true);
            RectTransform root = (RectTransform)canvas.transform;

            Color softScrim = ChaosUI.Dark;
            softScrim.a = 0.20f;
            ChaosUI.Panel(root, "Background", V(0, 0), V(1, 1), softScrim);
            ChaosUI.Label(root, "Scene status", "UI-VOORBEELD", 15, ChaosUI.Muted,
                TextAnchor.MiddleRight, FontStyle.Bold, V(0.55f, 0.92f), V(0.96f, 0.975f));

            RectTransform left = ChaosUI.MenuPanel(root, "Left menu", V(0.04f, 0.105f), V(0.31f, 0.745f));
            ChaosUI.MenuLogo(root, V(0.055f, 0.709f), V(0.295f, 0.98f));

            ChaosUI.Action(left, "Play trial", "PLAY LOCAL TRIAL", V(0.085f, 0.73f), V(0.915f, 0.91f),
                ChaosUI.Gold, ChaosUI.Ink, StartLocalTrial, 22, iconKey: "play");
            ChaosUI.Action(left, "Lobby tab", "LOBBY PREVIEW", V(0.085f, 0.565f), V(0.915f, 0.70f),
                ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 21, iconKey: "player-group");
            ChaosUI.Action(left, "Settings tab", "SETTINGS", V(0.085f, 0.40f), V(0.915f, 0.535f),
                ChaosUI.Paper, ChaosUI.Ink, ShowSettings, 21, iconKey: "options");
            ChaosUI.Action(left, "HUD preview tab", "HUD PREVIEW", V(0.085f, 0.235f), V(0.915f, 0.37f),
                ChaosUI.Paper, ChaosUI.Ink, ShowHud, 21, iconKey: "controls");
            ChaosUI.Action(left, "Quit", "QUIT", V(0.085f, 0.07f), V(0.915f, 0.205f),
                ChaosUI.TrainRed, ChaosUI.Paper, QuitGame, 20, iconKey: "quit");

            // Action() now places the approved artwork inside each button once.
            info = ChaosUI.Label(root, "Feedback", "Join/Relay komt later. Probeer de lokale physics-test.",
                16, ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(0.35f, 0.035f), V(0.97f, 0.105f));
            BuildLobby(root);
            BuildSettings(root);
            BuildHudExample(root);
            ShowLobby();
        }

        private void BuildLobby(RectTransform root)
        {
            lobby = ChaosUI.MenuPanel(root, "Lobby (concept only)", V(0.595f, 0.105f), V(0.96f, 0.91f));
            RectTransform heading = ChaosUI.Panel(lobby, "Heading paper", V(0.045f, 0.855f), V(0.72f, 0.96f), ChaosUI.Paper);
            ChaosUI.Label(heading, "Lobby title", "Samen op reis", 30, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform badge = ChaosUI.Panel(lobby, "Preview badge", V(0.75f, 0.88f), V(0.955f, 0.935f), ChaosUI.Dark);
            ChaosUI.Label(badge, "Lobby context", "VOORBEELD", 14, ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);

            Row(lobby, "Host", 0.75f, "LOCAL HOST", "Not started · play trial to host", "player");
            Row(lobby, "Friend 1", 0.645f, "PLAYER SLOT 2", "Empty · inviting friends not implemented", "invite");
            Row(lobby, "Friend 2", 0.54f, "PLAYER SLOT 3", "Empty", "player-group");
            Row(lobby, "Friend 3", 0.435f, "PLAYER SLOT 4", "Empty", "player-group");

            RectTransform route = ChaosUI.Panel(lobby, "Route card", V(0.035f, 0.18f), V(0.965f, 0.415f),
                ChaosUI.Dark, 2, ChaosUI.TrainRedDark);
            ChaosUISpriteCatalog.Add(route, "Route sprite", "route",
                V(0.04f, 0.21f), V(0.17f, 0.66f), ChaosUI.Gold);
            ChaosUI.Label(route, "Eyebrow", "ROUTE CONCEPT · VISUAL PREVIEW ONLY", 14, ChaosUI.Gold,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.04f, 0.68f), V(0.96f, 0.98f));
            routeName = ChaosUI.Label(route, "Route name", routes[0], 28, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.19f, 0.25f), V(0.72f, 0.72f));
            ChaosUI.Action(route, "Change route", "CHANGE", V(0.73f, 0.25f), V(0.96f, 0.73f),
                ChaosUI.Paper, ChaosUI.Ink, NextRoute, 17);
            ChaosUI.Action(lobby, "Start real trial", "START LOCAL PHYSICS TEST", V(0.035f, 0.045f), V(0.965f, 0.15f),
                ChaosUI.Gold, ChaosUI.Ink, StartLocalTrial, 21, iconKey: "play");
        }

        private static void Row(RectTransform parent, string name, float y, string title, string subtitle, string iconKey)
        {
            RectTransform row = ChaosUI.Panel(parent, name, V(0.035f, y), V(0.965f, y + 0.092f), ChaosUI.Dark);
            ChaosUISpriteCatalog.Add(row, "Avatar icon", iconKey,
                V(0.025f, 0.07f), V(0.145f, 0.93f), ChaosUI.Paper);
            ChaosUI.Label(row, "Title", title, 20, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.16f, 0.45f), V(0.76f, 0.98f));
            ChaosUI.Label(row, "State", subtitle, 14, ChaosUI.Muted,
                TextAnchor.MiddleLeft, FontStyle.Normal, V(0.16f, 0.04f), V(0.94f, 0.48f));
        }

        private void BuildSettings(RectTransform root)
        {
            settings = ChaosUI.MenuPanel(root, "Settings", V(0.595f, 0.105f), V(0.96f, 0.91f));
            ChaosUISpriteCatalog.Add(settings, "Settings sprite", "options",
                V(0.035f, 0.87f), V(0.12f, 0.97f), ChaosUI.Paper);
            ChaosUI.Label(settings, "Heading", "SETTINGS", 32, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.13f, 0.88f), V(0.90f, 0.97f));
            ChaosUI.Label(settings, "Subtitle", "Simple controls. No chrome, no glossy effects.", 17,
                ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(0.035f, 0.80f), V(0.96f, 0.87f));
            ChaosUISpriteCatalog.Add(settings, "Audio sprite", "audio",
                V(0.04f, 0.665f), V(0.115f, 0.75f), ChaosUI.Gold);
            ChaosUI.Label(settings, "Audio heading", "AUDIO", 24, ChaosUI.Gold,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.12f, 0.66f), V(0.96f, 0.75f));
            ChaosUI.Label(settings, "Volume label", "Master volume", 20, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.04f, 0.52f), V(0.34f, 0.64f));
            ChaosUI.VolumeSlider(settings, V(0.36f, 0.555f), V(0.78f, 0.60f),
                AudioListener.volume, SetVolume);
            volumeValue = ChaosUI.Label(settings, "Volume value",
                Mathf.RoundToInt(AudioListener.volume * 100f) + "%", 18, ChaosUI.Paper,
                TextAnchor.MiddleCenter, FontStyle.Bold, V(0.8f, 0.53f), V(0.96f, 0.625f));
            ChaosUI.Label(settings, "Mute label", "Mute all sound", 20, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.04f, 0.39f), V(0.55f, 0.51f));
            ChaosUI.Action(settings, "Mute toggle", "TOGGLE", V(0.65f, 0.395f), V(0.96f, 0.495f),
                ChaosUI.Paper, ChaosUI.Ink, ToggleMute, 18, iconKey: "audio");
            muteValue = ChaosUI.Label(settings, "Mute state", muted ? "Muted" : "Sound on",
                17, ChaosUI.GoldLight, TextAnchor.MiddleLeft, FontStyle.Normal,
                V(0.04f, 0.30f), V(0.55f, 0.38f));
            ChaosUI.Label(settings, "Future systems", "Controller binding and accessibility options follow after the input design.",
                16, ChaosUI.Muted, TextAnchor.UpperLeft, FontStyle.Normal, V(0.04f, 0.15f), V(0.94f, 0.28f));
            ChaosUI.Action(settings, "Back", "BACK TO LOBBY", V(0.69f, 0.035f), V(0.96f, 0.125f),
                ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18);
        }

        private void BuildHudExample(RectTransform root)
        {
            hudExample = ChaosUI.Panel(root, "HUD style examples", V(0.575f, 0.13f), V(0.965f, 0.9f),
                ChaosUI.Slate, 2, ChaosUI.Muted);
            ChaosUI.Label(hudExample, "Title", "HUD COMPONENT PREVIEW", 30, ChaosUI.Paper,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.035f, 0.87f), V(0.96f, 0.97f));
            ChaosUI.Label(hudExample, "Disclaimer", "Sample values only. Not connected to train gameplay.",
                17, ChaosUI.Gold, TextAnchor.MiddleLeft, FontStyle.Normal, V(0.035f, 0.79f), V(0.95f, 0.86f));
            RectTransform task = ChaosUI.Panel(hudExample, "Objective", V(0.04f, 0.48f),
                V(0.53f, 0.76f), ChaosUI.Paper);
            ChaosUISpriteCatalog.Add(task, "Ticket sprite", "ticket",
                V(0.04f, 0.63f), V(0.22f, 0.94f), ChaosUI.Ink);
            ChaosUI.Label(task, "Label", "ROUND OBJECTIVES", 20, ChaosUI.Ink,
                TextAnchor.MiddleLeft, FontStyle.Bold, V(0.23f, 0.63f), V(0.97f, 0.94f));
            ChaosUI.Label(task, "Rows", "[ ] Check 3 tickets\n[ ] Close train doors", 17, ChaosUI.Ink,
                TextAnchor.MiddleLeft, FontStyle.Normal, V(0.035f, 0.08f), V(0.96f, 0.64f));
            RectTransform warning = ChaosUI.Panel(hudExample, "Warning", V(0.56f, 0.48f),
                V(0.96f, 0.76f), ChaosUI.TrainRed);
            ChaosUISpriteCatalog.Add(warning, "Warning sprite", "warning",
                V(0.035f, 0.52f), V(0.20f, 0.92f), ChaosUI.Paper);
            ChaosUI.Label(warning, "Warning title", "TRAIN NEEDS ATTENTION", 18,
                ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(0.19f, 0.46f), V(0.96f, 0.95f));
            ChaosUI.Label(warning, "Warning explanation", "Show this only when a real event exists.",
                15, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Normal, V(0.07f, 0.08f), V(0.93f, 0.53f));
            RectTransform prompt = ChaosUI.Panel(hudExample, "Prompt", V(0.23f, 0.27f),
                V(0.76f, 0.39f), ChaosUI.Paper);
            ChaosUISpriteCatalog.Add(prompt, "Door sprite", "door",
                V(0.045f, 0.12f), V(0.19f, 0.88f), ChaosUI.Ink);
            ChaosUI.Label(prompt, "Prompt label", "[ E ]  INTERACT  ·  door lock",
                19, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold,
                V(0.19f, 0f), V(0.96f, 1f));
            ChaosUI.Action(hudExample, "Back to lobby", "BACK TO LOBBY", V(0.65f, 0.05f), V(0.96f, 0.14f),
                ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18);
        }

        private void ShowLobby()
        {
            lobby.gameObject.SetActive(true);
            settings.gameObject.SetActive(false);
            hudExample.gameObject.SetActive(false);
        }

        private void ShowSettings()
        {
            lobby.gameObject.SetActive(false);
            settings.gameObject.SetActive(true);
            hudExample.gameObject.SetActive(false);
        }

        private void ShowHud()
        {
            lobby.gameObject.SetActive(false);
            settings.gameObject.SetActive(false);
            hudExample.gameObject.SetActive(true);
        }

        private void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat("ChaosUI.Volume", AudioListener.volume);
            if (volumeValue) volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void ToggleMute()
        {
            muted = !muted;
            AudioListener.pause = muted;
            PlayerPrefs.SetInt("ChaosUI.Muted", muted ? 1 : 0);
            muteValue.text = muted ? "Muted" : "Sound on";
        }

        private void NextRoute()
        {
            routeIndex = (routeIndex + 1) % routes.Length;
            routeName.text = routes[routeIndex];
            info.text = "This changes only the menu preview; route gameplay is not implemented.";
        }

        private void StartLocalTrial()
        {
            if (!Application.CanStreamedLevelBeLoaded("TrainPlayground"))
            {
                info.text = "TrainPlayground is missing from Build Settings.";
                Debug.LogWarning("Chaos UI: TrainPlayground scene isn't available in Build Settings.");
                return;
            }
            AudioListener.pause = false; // The trial may resume sound; saved mute preference is retained.
            SceneManager.LoadScene("TrainPlayground");
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
