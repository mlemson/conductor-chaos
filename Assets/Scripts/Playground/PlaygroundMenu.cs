using ConductorChaos.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ConductorChaos.Playground
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PlaygroundMenu : MonoBehaviour
    {
        private enum Screen { Main, Game, Pause }
        private static PlaygroundMenu instance;
        private Screen screen;
        private Canvas canvas;
        private RectTransform logoPanel, mainPanel, pausePanel, lobbyPanel, settingsPanel;
        private Text volumeText, muteText;
        private bool muted;
        private Button playButton, resumeButton, settingsBack;
        private Button hostButton, joinButton, beginButton, leaveButton, copyButton;
        private Text lobbyStatus, lobbyCode;
        private InputField joinCodeInput;
        private PlaygroundHost networkHost;
        private PlaygroundHost.SessionPhase previousPhase;

        public static bool BlocksInput => instance && instance.screen != Screen.Game;
        private static Vector2 V(float x, float y) => new Vector2(x, y);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (SceneManager.GetActiveScene().name != "TrainPlayground" || instance || Application.isBatchMode) return;
            new GameObject("Playground Menu").AddComponent<PlaygroundMenu>();
        }

        private void Awake()
        {
            instance = this;
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("ChaosUI.Volume", 0.8f));
            muted = PlayerPrefs.GetInt("ChaosUI.Muted", 0) != 0;
            AudioListener.pause = muted;
            networkHost = FindAnyObjectByType<PlaygroundHost>();
            Build();
            SetScreen(Screen.Main);
        }

        private void Build()
        {
            canvas = ChaosUI.MakeCanvas(transform, "Conductor Chaos menu", true);
            RectTransform root = (RectTransform)canvas.transform;
            Color scrim = ChaosUI.Dark; scrim.a = 0.34f;
            ChaosUI.Panel(root, "Background", V(0, 0), V(1, 1), scrim);
            RectTransform stage = ChaosUI.Rect(root, "Menu composition", V(0, 0), V(1, 1), V(0, 0), V(0, 0));
            var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;
            root = stage;

            mainPanel = ChaosUI.Panel(root, "Main menu", V(.047f, .16f), V(.275f, .74f),
                ChaosUI.Slate, 5, ChaosUI.Dark);
            playButton = MenuAction("Play", "Spelen", .755f, .945f, ChaosUI.Gold, "play", StartLocalPlay, 28);
            hostButton = MenuAction("Host", "Online hosten", .579f, .724f, ChaosUI.Paper, "player-group", () => networkHost?.HostLobby(), 23);
            joinButton = MenuAction("Join", "Deelnemen", .405f, .55f, ChaosUI.Paper, "key", JoinLobby, 23);
            MenuAction("Settings", "Instellingen", .23f, .375f, ChaosUI.Paper, "options", ShowSettings, 23);
            MenuAction("Quit", "Afsluiten", .055f, .20f, ChaosUI.TrainRed, "quit", Application.Quit, 23);

            logoPanel = ChaosUI.Rect(root, "Title badge", V(.045f, .715f), V(.28f, .965f), V(0, 0), V(0, 0));
            ChaosUI.Panel(logoPanel, "Badge", V(.055f, .12f), V(.945f, .96f), ChaosUI.Paper, 2, ChaosUI.Dark);
            Text conductor = ChaosUI.Label(logoPanel, "Conductor", "Conductor", 40, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold, V(.055f, .53f), V(.945f, .91f));
            Text chaos = ChaosUI.Label(logoPanel, "Chaos", "Chaos", 56, ChaosUI.TrainRed, TextAnchor.MiddleCenter, FontStyle.Bold, V(.055f, .18f), V(.945f, .60f));
            conductor.font = chaos.font = ChaosUI.Font;
            conductor.resizeTextForBestFit = chaos.resizeTextForBestFit = true;
            conductor.resizeTextMinSize = chaos.resizeTextMinSize = 25;
            conductor.resizeTextMaxSize = 40;
            chaos.resizeTextMaxSize = 56;

            lobbyPanel = ChaosUI.Panel(root, "Lobby", V(.597f, .16f), V(.978f, .94f), ChaosUI.Slate, 5, ChaosUI.Dark);
            RectTransform heading = ChaosUI.Panel(lobbyPanel, "Heading paper", V(.035f, .875f), V(.61f, .97f), ChaosUI.Paper);
            ChaosUI.Label(heading, "Heading", "Samen op reis", 26, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            ChaosUI.Label(lobbyPanel, "Availability", "ONLINE", 15, ChaosUI.GoldLight, TextAnchor.MiddleRight, FontStyle.Bold, V(.62f, .89f), V(.96f, .955f));
            lobbyStatus = ChaosUI.Label(lobbyPanel, "Status", "", 21, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Normal, V(.055f, .66f), V(.945f, .83f));
            RectTransform codeBox = ChaosUI.Panel(lobbyPanel, "Join code field", V(.055f, .50f), V(.945f, .63f), ChaosUI.Paper);
            Image codeBackground = codeBox.GetComponent<Image>();
            codeBackground.raycastTarget = true;
            joinCodeInput = codeBox.gameObject.AddComponent<InputField>();
            joinCodeInput.targetGraphic = codeBackground;
            joinCodeInput.textComponent = ChaosUI.Label(codeBox, "Code", "", 24, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            joinCodeInput.placeholder = ChaosUI.Label(codeBox, "Placeholder", "Voer lobbycode in", 20, ChaosUI.Ink, TextAnchor.MiddleCenter);
            joinCodeInput.contentType = InputField.ContentType.Alphanumeric;
            joinCodeInput.characterLimit = 12;
            lobbyCode = ChaosUI.Label(lobbyPanel, "Share code", "", 30, ChaosUI.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold, V(.055f, .50f), V(.945f, .63f));
            copyButton = ChaosUI.Action(lobbyPanel, "Copy code", "Kopieer code", V(.055f, .35f), V(.945f, .46f), ChaosUI.Paper, ChaosUI.Ink,
                () => GUIUtility.systemCopyBuffer = networkHost.JoinCode, 20, iconKey: "key");
            beginButton = ChaosUI.Action(lobbyPanel, "Begin", "Start samen", V(.055f, .19f), V(.945f, .30f), ChaosUI.Gold, ChaosUI.Ink,
                () => networkHost?.BeginGame(), 21, iconKey: "play");
            leaveButton = ChaosUI.Action(lobbyPanel, "Leave", "Verlaat sessie", V(.055f, .045f), V(.945f, .155f), ChaosUI.Paper, ChaosUI.Ink,
                () => networkHost?.Leave(), 20, iconKey: "back");

            settingsPanel = ChaosUI.Panel(root, "Settings", V(.34f, .18f), V(.965f, .75f), ChaosUI.Slate, 3, ChaosUI.Dark);
            RectTransform settingsHeading = ChaosUI.Panel(settingsPanel, "Heading paper", V(.035f, .86f), V(.32f, .98f), ChaosUI.Paper);
            ChaosUI.Label(settingsHeading, "Heading", "INSTELLINGEN", 24, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform audioTab = ChaosUI.Panel(settingsPanel, "Selected audio tab", V(.035f, .73f), V(.25f, .84f), ChaosUI.Gold);
            ChaosUISpriteCatalog.Add(audioTab, "Speaker", "audio", V(.04f, .08f), V(.26f, .92f), ChaosUI.Ink);
            ChaosUI.Label(audioTab, "Label", "Geluid", 18, ChaosUI.Ink, TextAnchor.MiddleCenter,
                FontStyle.Bold, V(.26f, 0f), V(1f, 1f));
            ChaosUI.Action(settingsPanel, "Controls tab", "Besturing", V(.26f, .73f), V(.49f, .84f), ChaosUI.Paper, ChaosUI.Ink, null, 16, false, "controls");
            ChaosUI.Action(settingsPanel, "Accessibility tab", "Toegankelijkheid", V(.50f, .73f), V(.84f, .84f), ChaosUI.Paper, ChaosUI.Ink, null, 15, false, "accessibility");
            ChaosUI.Label(settingsPanel, "Audio", "MASTER VOLUME", 19, ChaosUI.GoldLight, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .62f), V(.96f, .70f));
            ChaosUI.Label(settingsPanel, "Volume", "Volume", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .52f), V(.34f, .64f));
            ChaosUI.VolumeSlider(settingsPanel, V(.36f, .555f), V(.78f, .60f), AudioListener.volume, SetVolume);
            volumeText = ChaosUI.Label(settingsPanel, "Volume value", Mathf.RoundToInt(AudioListener.volume * 100) + "%", 18, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.8f, .53f), V(.96f, .625f));
            ChaosUI.Label(settingsPanel, "Sound", "Geluid", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .36f), V(.34f, .48f));
            Button soundSwitch = ChaosUI.Action(settingsPanel, "Sound switch", muted ? "Uit" : "Aan",
                V(.36f, .36f), V(.52f, .48f), ChaosUI.Paper, ChaosUI.Ink, ToggleMute, 23);
            muteText = soundSwitch.GetComponentInChildren<Text>();
            settingsBack = ChaosUI.Action(settingsPanel, "Back", "Terug", V(.69f, .035f), V(.96f, .125f), ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18, iconKey: "back");

            pausePanel = ChaosUI.Panel(root, "Pause", V(.36f, .19f), V(.64f, .81f), ChaosUI.Slate, 2, ChaosUI.Muted);
            ChaosUI.Label(pausePanel, "Heading", "GEPAUZEERD", 32, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.05f, .8f), V(.95f, .95f));
            resumeButton = ChaosUI.Action(pausePanel, "Resume", "Verder spelen", V(.08f, .62f), V(.92f, .74f), ChaosUI.Gold, ChaosUI.Ink, () => SetScreen(Screen.Game), 22, iconKey: "play");
            ChaosUI.Action(pausePanel, "Unstuck", "Terug op de trein", V(.08f, .47f), V(.92f, .59f), ChaosUI.Paper, ChaosUI.Ink, Unstuck, 20);
            ChaosUI.Action(pausePanel, "Settings", "INSTELLINGEN", V(.08f, .32f), V(.92f, .44f), ChaosUI.Paper, ChaosUI.Ink, ShowSettings, 20, iconKey: "options");
            ChaosUI.Action(pausePanel, "Main", "HOOFDMENU", V(.08f, .17f), V(.92f, .29f), ChaosUI.Paper, ChaosUI.Ink, () => SetScreen(Screen.Main), 20, iconKey: "back");
            ChaosUI.Label(pausePanel, "Notice", "De wereld blijft actief terwijl dit menu open is.", 15, ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, V(.05f, .02f), V(.95f, .13f));
        }

        private Button MenuAction(string name, string caption, float bottom, float top, Color color,
            string iconKey, UnityEngine.Events.UnityAction action, int size)
        {
            Color ink = color == ChaosUI.TrainRed ? ChaosUI.Paper : ChaosUI.Ink;
            return ChaosUI.Action(mainPanel, name, caption, V(.085f, bottom), V(.915f, top),
                color, ink, action, size, action != null, iconKey);
        }

        private void ShowLobby()
        {
            lobbyPanel.gameObject.SetActive(screen == Screen.Main);
            settingsPanel.gameObject.SetActive(false);
            pausePanel.gameObject.SetActive(screen == Screen.Pause);
            Select(screen == Screen.Main ? playButton : screen == Screen.Pause ? resumeButton : null);
        }

        private void ShowSettings()
        {
            lobbyPanel.gameObject.SetActive(false);
            pausePanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
            Select(settingsBack);
        }

        private static void Select(Button button)
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(button ? button.gameObject : null);
        }

        private void StartLocalPlay()
        {
            if (!networkHost) return;
            if (networkHost.Phase == PlaygroundHost.SessionPhase.Playing) SetScreen(Screen.Game);
            else networkHost.StartLocal();
        }

        private void JoinLobby()
        {
            if (!networkHost) return;
            if (string.IsNullOrEmpty(joinCodeInput.text)) joinCodeInput.ActivateInputField();
            else networkHost.JoinLobby(joinCodeInput.text);
        }

        private void UpdateLobby()
        {
            if (!networkHost) return;
            var phase = networkHost.Phase;
            bool offline = phase == PlaygroundHost.SessionPhase.Offline;
            bool inLobby = phase == PlaygroundHost.SessionPhase.Lobby;
            bool playing = phase == PlaygroundHost.SessionPhase.Playing;
            if (phase != previousPhase)
            {
                if (playing) SetScreen(Screen.Game);
                else if (offline && screen != Screen.Main) SetScreen(Screen.Main);
                previousPhase = phase;
            }
            hostButton.interactable = joinButton.interactable = networkHost.CanStart;
            playButton.interactable = networkHost.CanStart || playing;
            joinCodeInput.gameObject.SetActive(offline);
            bool showCode = !string.IsNullOrEmpty(networkHost.JoinCode);
            lobbyCode.gameObject.SetActive(showCode);
            lobbyCode.text = networkHost.JoinCode;
            copyButton.gameObject.SetActive(showCode);
            beginButton.gameObject.SetActive(inLobby && networkHost.IsHost);
            leaveButton.gameObject.SetActive(!offline);
            lobbyStatus.text = networkHost.Message ?? (offline
                ? "Maak een online lobby of doe mee met de lobbycode van een vriend."
                : $"{networkHost.PlayerCount} / 6 spelers verbonden" +
                  (inLobby && !networkHost.IsHost ? "\nWachten tot de host start." : ""));
        }
        private void SetVolume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat("ChaosUI.Volume", value); if (volumeText) volumeText.text = Mathf.RoundToInt(value * 100) + "%"; }
        private void ToggleMute() { muted = !muted; AudioListener.pause = muted; PlayerPrefs.SetInt("ChaosUI.Muted", muted ? 1 : 0); if (muteText) muteText.text = muted ? "Uit" : "Aan"; }
        private void Unstuck()
        {
            foreach (var player in FindObjectsByType<PlaygroundPlayer>())
                if (player.IsOwner && player.IsSpawned) { player.RequestUnstuck(); SetScreen(Screen.Game); break; }
        }
        private void Update()
        {
            UpdateLobby();
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (screen == Screen.Game) SetScreen(Screen.Pause);
            else if (settingsPanel.gameObject.activeSelf) ShowLobby();
            else if (screen == Screen.Pause) SetScreen(Screen.Game);
        }
        private void SetScreen(Screen next)
        {
            screen = next;
            canvas.gameObject.SetActive(next != Screen.Game);
            mainPanel.gameObject.SetActive(next == Screen.Main);
            logoPanel.gameObject.SetActive(next == Screen.Main);
            ShowLobby();
            Cursor.lockState = next == Screen.Game ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = next != Screen.Game;
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
