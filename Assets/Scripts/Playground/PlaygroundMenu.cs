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
        private Text volumeText, muteText, cameraBobText;
        private bool muted;
        private Button playButton, resumeButton, settingsBack;
        private Button hostButton, joinButton, beginButton, leaveButton, copyButton;
        private Text lobbyStatus, lobbyAvailability, lobbyCode;
        private RectTransform lobbySeats, lobbyShare;
        private readonly Text[] lobbySeatLabels = new Text[PlaygroundHost.Capacity];
        private int displayedLobbyCount = -1;
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
            Color scrim = ChaosUI.Dark; scrim.a = 0.20f;
            ChaosUI.Panel(root, "Background", V(0, 0), V(1, 1), scrim);
            RectTransform stage = ChaosUI.Rect(root, "Menu composition", V(0, 0), V(1, 1), V(0, 0), V(0, 0));
            var fit = stage.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 16f / 9f;
            root = stage;

            mainPanel = ChaosUI.MenuPanel(root, "Main menu", V(.04f, .105f), V(.31f, .745f));
            playButton = MenuAction("Play", "Spelen", .73f, .91f, ChaosUI.Gold, "play", StartLocalPlay, 34);
            hostButton = MenuAction("Host", "Online hosten", .565f, .70f, ChaosUI.Paper, "player-group", () => networkHost?.HostLobby(), 27);
            joinButton = MenuAction("Join", "Deelnemen", .40f, .535f, ChaosUI.Paper, "key", JoinLobby, 27);
            MenuAction("Settings", "Instellingen", .235f, .37f, ChaosUI.Paper, "options", ShowSettings, 27);
            MenuAction("Quit", "Afsluiten", .07f, .205f, ChaosUI.TrainRed, "quit", Application.Quit, 27);
            logoPanel = ChaosUI.MenuLogo(root, V(.055f, .709f), V(.295f, .98f));

            BuildLobby(root);

            settingsPanel = ChaosUI.MenuPanel(root, "Settings", V(.36f, .18f), V(.96f, .78f));
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
            ChaosUI.Label(settingsPanel, "Camera motion", "Camerabeweging", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .20f), V(.34f, .32f));
            Button cameraBobSwitch = ChaosUI.Action(settingsPanel, "Camera motion switch", PlayerPrefs.GetInt("ChaosUI.CameraBob", 1) != 0 ? "Aan" : "Uit",
                V(.36f, .20f), V(.52f, .32f), ChaosUI.Paper, ChaosUI.Ink, ToggleCameraBob, 23);
            cameraBobText = cameraBobSwitch.GetComponentInChildren<Text>();
            settingsBack = ChaosUI.Action(settingsPanel, "Back", "Terug", V(.69f, .035f), V(.96f, .125f), ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18, iconKey: "back");

            pausePanel = ChaosUI.MenuPanel(root, "Pause", V(.35f, .19f), V(.65f, .81f));
            ChaosUI.Label(pausePanel, "Heading", "GEPAUZEERD", 32, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.05f, .8f), V(.95f, .95f));
            resumeButton = ChaosUI.Action(pausePanel, "Resume", "Verder spelen", V(.08f, .62f), V(.92f, .74f), ChaosUI.Gold, ChaosUI.Ink, () => SetScreen(Screen.Game), 22, iconKey: "play");
            ChaosUI.Action(pausePanel, "Unstuck", "Terug op de trein", V(.08f, .47f), V(.92f, .59f), ChaosUI.Paper, ChaosUI.Ink, Unstuck, 20);
            ChaosUI.Action(pausePanel, "Settings", "INSTELLINGEN", V(.08f, .32f), V(.92f, .44f), ChaosUI.Paper, ChaosUI.Ink, ShowSettings, 20, iconKey: "options");
            ChaosUI.Action(pausePanel, "Main", "HOOFDMENU", V(.08f, .17f), V(.92f, .29f), ChaosUI.Paper, ChaosUI.Ink, () => SetScreen(Screen.Main), 20, iconKey: "back");
            ChaosUI.Label(pausePanel, "Notice", "De wereld blijft actief terwijl dit menu open is.", 15, ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, V(.05f, .02f), V(.95f, .13f));
        }

        private void BuildLobby(RectTransform root)
        {
            lobbyPanel = ChaosUI.MenuPanel(root, "Lobby", V(.595f, .105f), V(.96f, .91f));
            RectTransform heading = ChaosUI.Panel(lobbyPanel, "Heading paper", V(.045f, .855f), V(.72f, .96f), ChaosUI.Paper);
            ChaosUI.Label(heading, "Heading", "Samen op reis", 31, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform badge = ChaosUI.Panel(lobbyPanel, "Connection badge", V(.75f, .88f), V(.955f, .935f), ChaosUI.Dark);
            lobbyAvailability = ChaosUI.Label(badge, "Availability", "OFFLINE", 14, ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);

            Color recess = Color.Lerp(ChaosUI.Navy, ChaosUI.Dark, .55f);
            RectTransform body = ChaosUI.Panel(lobbyPanel, "Session details", V(.045f, .265f), V(.955f, .82f), recess);
            ChaosUISpriteCatalog.Add(body, "Crew icon", "player-group", V(.04f, .76f), V(.145f, .95f), ChaosUI.Paper);
            lobbyStatus = ChaosUI.Label(body, "Status", "Maak een online lobby of doe mee met de lobbycode van een vriend.",
                21, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Normal, V(.15f, .73f), V(.965f, .97f));

            lobbySeats = ChaosUI.Rect(body, "Player slots", V(0, 0), V(1, 1), V(0, 0), V(0, 0));
            lobbySeats.gameObject.SetActive(false);
            Color seatFace = Color.Lerp(ChaosUI.Navy, ChaosUI.Paper, .10f);
            for (int i = 0; i < lobbySeatLabels.Length; i++)
            {
                float y = .61f - i * .105f;
                RectTransform seat = ChaosUI.Panel(lobbySeats, "Player slot", V(.065f, y), V(.935f, y + .087f), seatFace);
                lobbySeatLabels[i] = ChaosUI.Label(seat, "Slot status", "Vrije plek", 17, ChaosUI.Muted,
                    TextAnchor.MiddleLeft, FontStyle.Normal, V(.03f, 0), V(.97f, 1));
            }

            RectTransform codeBox = ChaosUI.Panel(body, "Join code field", V(.065f, .36f), V(.935f, .54f), ChaosUI.Paper);
            Image codeBackground = codeBox.GetComponent<Image>();
            codeBackground.raycastTarget = true;
            joinCodeInput = codeBox.gameObject.AddComponent<InputField>();
            joinCodeInput.targetGraphic = codeBackground;
            joinCodeInput.textComponent = ChaosUI.Label(codeBox, "Code", "", 24, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            joinCodeInput.placeholder = ChaosUI.Label(codeBox, "Placeholder", "Voer lobbycode in", 20, ChaosUI.Ink, TextAnchor.MiddleCenter);
            joinCodeInput.contentType = InputField.ContentType.Alphanumeric;
            joinCodeInput.characterLimit = 12;

            lobbyShare = ChaosUI.Panel(lobbyPanel, "Share code field", V(.045f, .153f), V(.52f, .235f), recess);
            lobbyCode = ChaosUI.Label(lobbyShare, "Share code", "", 26, ChaosUI.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            copyButton = ChaosUI.Action(lobbyPanel, "Copy code", "Kopieer code", V(.55f, .153f), V(.955f, .235f), ChaosUI.Paper, ChaosUI.Ink,
                () => GUIUtility.systemCopyBuffer = networkHost.JoinCode, 20, iconKey: "key");
            beginButton = ChaosUI.Action(lobbyPanel, "Begin", "Start samen", V(.045f, .045f), V(.52f, .125f), ChaosUI.Gold, ChaosUI.Ink,
                () => networkHost?.BeginGame(), 21, iconKey: "play");
            leaveButton = ChaosUI.Action(lobbyPanel, "Leave", "Verlaat sessie", V(.55f, .045f), V(.955f, .125f), ChaosUI.Paper, ChaosUI.Ink,
                () => networkHost?.Leave(), 20, iconKey: "back");

            lobbyShare.gameObject.SetActive(false);
            copyButton.gameObject.SetActive(false);
            beginButton.gameObject.SetActive(false);
            leaveButton.gameObject.SetActive(false);
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
            lobbyShare.gameObject.SetActive(showCode);
            lobbyCode.text = networkHost.JoinCode;
            copyButton.gameObject.SetActive(showCode);
            beginButton.gameObject.SetActive(inLobby && networkHost.IsHost);
            leaveButton.gameObject.SetActive(!offline);
            lobbyAvailability.text = inLobby ? "ONLINE" : offline ? "OFFLINE" : playing ? "IN SPEL" : "VERBINDEN";
            lobbyAvailability.color = inLobby ? ChaosUI.Green : ChaosUI.Muted;
            lobbySeats.gameObject.SetActive(inLobby);
            int count = networkHost.PlayerCount;
            if (inLobby && displayedLobbyCount != count)
            {
                displayedLobbyCount = count;
                for (int i = 0; i < lobbySeatLabels.Length; i++)
                {
                    bool occupied = i < count;
                    lobbySeatLabels[i].text = occupied ? $"Speler {i + 1} · verbonden" : "Vrije plek";
                    lobbySeatLabels[i].color = occupied ? ChaosUI.Paper : ChaosUI.Muted;
                }
            }
            lobbyStatus.text = networkHost.Message ?? (offline
                ? "Maak een online lobby of doe mee met de lobbycode van een vriend."
                : $"{count} / {PlaygroundHost.Capacity} spelers verbonden" +
                  (inLobby && !networkHost.IsHost ? "\nWachten tot de host start." : ""));
        }
        private void SetVolume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat("ChaosUI.Volume", value); if (volumeText) volumeText.text = Mathf.RoundToInt(value * 100) + "%"; }
        private void ToggleMute() { muted = !muted; AudioListener.pause = muted; PlayerPrefs.SetInt("ChaosUI.Muted", muted ? 1 : 0); if (muteText) muteText.text = muted ? "Uit" : "Aan"; }
        private void ToggleCameraBob() { bool enabled = PlayerPrefs.GetInt("ChaosUI.CameraBob", 1) == 0; PlayerPrefs.SetInt("ChaosUI.CameraBob", enabled ? 1 : 0); if (cameraBobText) cameraBobText.text = enabled ? "Aan" : "Uit"; }
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
