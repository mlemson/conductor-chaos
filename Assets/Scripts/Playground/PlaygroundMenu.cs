using ConductorChaos.Depot;
using ConductorChaos.UI;
using ConductorChaos.UI.Depot;
using ConductorChaos.Stations;
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
        private enum Screen { Main, Routes, Game, Pause, Service }
        private const string ViewDistanceKey = "ChaosUI.ViewDistance";
        private const int MinimumViewDistance = 400;
        private const int DefaultViewDistance = 1500;
        private const int MaximumViewDistance = 2200;

        private static PlaygroundMenu instance;
        private Screen screen;
        private Canvas canvas;
        private RectTransform logoPanel, mainPanel, pausePanel, lobbyPanel, settingsPanel;
        private Text volumeText, muteText, musicVolumeText, musicEnabledText, cameraBobText, viewDistanceText;
        private int viewDistance;
        private bool muted, musicEnabled;
        private Button playButton, resumeButton, settingsBack, routesBack;
        private RectTransform routesPanel;
        private Text routesStatus;
        private readonly Button[] routeButtons = new Button[ServiceRun.ServiceScenes.Length];
        private Button hostButton, joinButton, beginButton, leaveButton, copyButton;
        private Text lobbyStatus, lobbyAvailability, lobbyCode;
        private RectTransform lobbySeats, lobbyShare;
        private readonly Text[] lobbySeatLabels = new Text[PlaygroundHost.Capacity];
        private int displayedLobbyCount = -1;
        private InputField joinCodeInput;
        private PlaygroundHost networkHost;
        private ServicePresentation service;
        private DepotScreens depot;
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
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("ChaosUI.Volume", 0.8f));
            muted = PlayerPrefs.GetInt("ChaosUI.Muted", 0) != 0;
            AudioListener.pause = muted;
            musicEnabled = PlayerPrefs.GetInt(ServiceMusic.MusicEnabledKey, 1) != 0;
            viewDistance = Mathf.Clamp(PlayerPrefs.GetInt(ViewDistanceKey, DefaultViewDistance), MinimumViewDistance, MaximumViewDistance);
            ApplyViewDistance();
            gameObject.AddComponent<ServiceMusic>();
            gameObject.AddComponent<TrainAnnouncer>();
            networkHost = FindAnyObjectByType<PlaygroundHost>();
            Build();
            service = new ServicePresentation(transform, networkHost);
            // Levelkeuze en depot (#239): kies dienst, trein en wagons voordat een level start.
            depot = DepotScreens.Create(transform, networkHost);
            depot.OnBack = () => SetScreen(Screen.Main);
            depot.OnStart = StartRoute;
            service.OpenDepot = level => depot.ShowDepot(level, true);
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
            playButton = MenuAction("Play", "Spelen", .73f, .91f, ChaosUI.Gold, "play", ChooseLocalRoute, 34);
            hostButton = MenuAction("Host", "Online hosten", .565f, .70f, ChaosUI.Paper, "player-group", () => networkHost?.HostLobby(), 27);
            joinButton = MenuAction("Join", "Deelnemen", .40f, .535f, ChaosUI.Paper, "key", JoinLobby, 27);
            MenuAction("Settings", "Instellingen", .235f, .37f, ChaosUI.Paper, "options", ShowSettings, 27);
            MenuAction("Quit", "Afsluiten", .07f, .205f, ChaosUI.TrainRed, "quit", Application.Quit, 27);
            logoPanel = ChaosUI.MenuLogo(root, V(.055f, .709f), V(.295f, .98f));

            BuildLobby(root);
            BuildRoutes(root);

            settingsPanel = ChaosUI.MenuPanel(root, "Settings", V(.36f, .07f), V(.96f, .90f));
            RectTransform settingsHeading = ChaosUI.Panel(settingsPanel, "Heading paper", V(.035f, .895f), V(.32f, .98f), ChaosUI.Paper);
            ChaosUI.Label(settingsHeading, "Heading", "INSTELLINGEN", 24, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform audioTab = ChaosUI.Panel(settingsPanel, "Selected audio tab", V(.035f, .77f), V(.25f, .875f), ChaosUI.Gold);
            ChaosUISpriteCatalog.Add(audioTab, "Speaker", "audio", V(.04f, .08f), V(.26f, .92f), ChaosUI.Ink);
            ChaosUI.Label(audioTab, "Label", "Geluid", 18, ChaosUI.Ink, TextAnchor.MiddleCenter,
                FontStyle.Bold, V(.26f, 0f), V(1f, 1f));
            ChaosUI.Action(settingsPanel, "Controls tab", "Besturing", V(.26f, .77f), V(.49f, .875f), ChaosUI.Paper, ChaosUI.Ink, null, 16, false, "controls");
            ChaosUI.Action(settingsPanel, "Accessibility tab", "Toegankelijkheid", V(.50f, .77f), V(.84f, .875f), ChaosUI.Paper, ChaosUI.Ink, null, 15, false, "accessibility");

            ChaosUI.Label(settingsPanel, "Master volume", "Mastervolume", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .650f), V(.34f, .745f));
            ChaosUI.VolumeSlider(settingsPanel, V(.36f, .676f), V(.78f, .719f), AudioListener.volume, SetVolume);
            volumeText = ChaosUI.Label(settingsPanel, "Master volume value", Mathf.RoundToInt(AudioListener.volume * 100) + "%", 18, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.8f, .650f), V(.96f, .745f));

            float musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(ServiceMusic.MusicVolumeKey, ServiceMusic.DefaultMusicVolume));
            ChaosUI.Label(settingsPanel, "Music volume", "Muziekvolume", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .545f), V(.34f, .640f));
            ChaosUI.VolumeSlider(settingsPanel, V(.36f, .571f), V(.78f, .614f), musicVolume, SetMusicVolume);
            musicVolumeText = ChaosUI.Label(settingsPanel, "Music volume value", Mathf.RoundToInt(musicVolume * 100) + "%", 18, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.8f, .545f), V(.96f, .640f));

            ChaosUI.Label(settingsPanel, "Music", "Muziek", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .440f), V(.34f, .535f));
            Button musicSwitch = ChaosUI.Action(settingsPanel, "Music switch", musicEnabled ? "Aan" : "Uit",
                V(.36f, .445f), V(.52f, .530f), ChaosUI.Paper, ChaosUI.Ink, ToggleMusic, 20);
            musicEnabledText = musicSwitch.GetComponentInChildren<Text>();

            ChaosUI.Label(settingsPanel, "Sound", "Geluid", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .335f), V(.34f, .430f));
            Button soundSwitch = ChaosUI.Action(settingsPanel, "Sound switch", muted ? "Uit" : "Aan",
                V(.36f, .340f), V(.52f, .425f), ChaosUI.Paper, ChaosUI.Ink, ToggleMute, 20);
            muteText = soundSwitch.GetComponentInChildren<Text>();

            ChaosUI.Label(settingsPanel, "Camera motion", "Camerabeweging", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .230f), V(.34f, .325f));
            Button cameraBobSwitch = ChaosUI.Action(settingsPanel, "Camera motion switch", PlayerPrefs.GetInt("ChaosUI.CameraBob", 1) != 0 ? "Aan" : "Uit",
                V(.36f, .235f), V(.52f, .320f), ChaosUI.Paper, ChaosUI.Ink, ToggleCameraBob, 20);
            cameraBobText = cameraBobSwitch.GetComponentInChildren<Text>();

            ChaosUI.Label(settingsPanel, "View distance", "Zichtafstand", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .125f), V(.34f, .220f));
            ChaosUI.VolumeSlider(settingsPanel, V(.36f, .151f), V(.78f, .194f),
                Mathf.InverseLerp(MinimumViewDistance, MaximumViewDistance, viewDistance), SetViewDistance);
            viewDistanceText = ChaosUI.Label(settingsPanel, "View distance value", $"{viewDistance} m", 18, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.8f, .125f), V(.96f, .220f));
            settingsBack = ChaosUI.Action(settingsPanel, "Back", "Terug", V(.69f, .025f), V(.96f, .115f), ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18, iconKey: "back");

            pausePanel = ChaosUI.MenuPanel(root, "Pause", V(.35f, .19f), V(.65f, .81f));
            ChaosUI.Label(pausePanel, "Heading", "GEPAUZEERD", 32, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.05f, .8f), V(.95f, .95f));
            resumeButton = ChaosUI.Action(pausePanel, "Resume", "Verder spelen", V(.08f, .62f), V(.92f, .74f), ChaosUI.Gold, ChaosUI.Ink, () => SetScreen(Screen.Game), 22, iconKey: "play");
            ChaosUI.Action(pausePanel, "Unstuck", "Terug op de trein", V(.08f, .47f), V(.92f, .59f), ChaosUI.Paper, ChaosUI.Ink, Unstuck, 20);
            ChaosUI.Action(pausePanel, "Settings", "INSTELLINGEN", V(.08f, .32f), V(.92f, .44f), ChaosUI.Paper, ChaosUI.Ink, ShowSettings, 20, iconKey: "options");
            ChaosUI.Action(pausePanel, "Main", "HOOFDMENU", V(.08f, .17f), V(.92f, .29f), ChaosUI.Paper, ChaosUI.Ink, () =>
            {
                if (ServiceRun.Active) networkHost?.Leave();
                SetScreen(Screen.Main);
            }, 20, iconKey: "back");
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
                ChooseOnlineRoute, 21, iconKey: "play");
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
            routesPanel.gameObject.SetActive(false); // vervangen door de levelkeuze in DepotScreens (#239)
            settingsPanel.gameObject.SetActive(false);
            pausePanel.gameObject.SetActive(screen == Screen.Pause);
            Select(screen == Screen.Routes ? null :
                screen == Screen.Main ? playButton : screen == Screen.Pause ? resumeButton : null);
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

        private void BuildRoutes(RectTransform root)
        {
            routesPanel = ChaosUI.MenuPanel(root, "Route picker", V(.15f, .065f), V(.85f, .935f));
            ChaosUI.Label(routesPanel, "Heading", "Kies een rit", 32, ChaosUI.Paper, TextAnchor.MiddleLeft,
                FontStyle.Bold, V(.045f, .875f), V(.62f, .965f));
            routesBack = ChaosUI.Action(routesPanel, "Back", "Terug", V(.79f, .88f), V(.95f, .955f),
                ChaosUI.Paper, ChaosUI.Ink, () => SetScreen(Screen.Main), 21, iconKey: "back");
            routesStatus = ChaosUI.Label(routesPanel, "Status", "", 17, ChaosUI.Paper, TextAnchor.MiddleCenter,
                FontStyle.Normal, V(.08f, .015f), V(.92f, .065f));

            for (int i = 0; i < ServiceRun.ServiceScenes.Length; i++)
            {
                string scene = ServiceRun.ServiceScenes[i];
                string from = ServiceRun.PlannedStationName(scene, ServiceStation.StationRole.Vertrek);
                string to = ServiceRun.PlannedStationName(scene, ServiceStation.StationRole.Aankomst);
                bool available = Application.CanStreamedLevelBeLoaded(scene);
                // raster dat met het aantal ritten meegroeit (2 kolommen tot 4 ritten, daarna 3)
                int count = ServiceRun.ServiceScenes.Length, cols = count <= 4 ? 2 : 3, rows = (count + cols - 1) / cols;
                float cellW = (.91f - .03f * (cols - 1)) / cols, cellH = (.76f - .03f * (rows - 1)) / rows;
                float left = .045f + (i % cols) * (cellW + .03f);
                float bottom = .085f + (rows - 1 - i / cols) * (cellH + .03f);
                string caption = i == 0 ? "Intro" : $"Level {i}";
                Button card = ChaosUI.Action(routesPanel, caption, "", V(left, bottom), V(left + cellW, bottom + cellH),
                    ChaosUI.Paper, ChaosUI.Ink, () => StartRoute(scene), 22, available);
                routeButtons[i] = card;
                var photoArea = ChaosUI.Panel(card.transform, "Station preview", V(.025f, .275f), V(.975f, .97f), ChaosUI.Navy);
                var texture = Resources.Load<Texture2D>("Routes/" + from);
                if (texture)
                {
                    var photo = ChaosUI.Rect(photoArea, from, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                    var raw = photo.gameObject.AddComponent<RawImage>();
                    raw.texture = texture;
                    raw.raycastTarget = false;
                    if (!available) raw.color = ChaosUI.Muted;
                    var fit = photo.gameObject.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fit.aspectRatio = (float)texture.width / texture.height;
                }
                else ChaosUISpriteCatalog.Add(photoArea, "Station icon", "route", V(.40f, .17f), V(.60f, .83f), ChaosUI.Muted);
                ChaosUI.Label(card.transform, "Ride", caption, 23, ChaosUI.Ink, TextAnchor.MiddleLeft, FontStyle.Bold,
                    V(.03f, .14f), V(.98f, .268f));
                ChaosUI.Label(card.transform, "Stations", available ? to == null ? $"{from} · oefenrit" : $"{from} → {to}" : "Niet in deze build",
                    18, ChaosUI.Ink, TextAnchor.MiddleLeft, FontStyle.Normal, V(.03f, .022f), V(.98f, .152f));
            }
        }

        private void ChooseLocalRoute()
        {
            if (!networkHost) return;
            if (networkHost.Phase == PlaygroundHost.SessionPhase.Playing) SetScreen(Screen.Game);
            else if (networkHost.HasServiceScenes) SetScreen(Screen.Routes);
            else networkHost.StartLocal(null);
        }

        private void ChooseOnlineRoute()
        {
            if (!networkHost || !networkHost.IsHost || networkHost.Phase != PlaygroundHost.SessionPhase.Lobby) return;
            if (networkHost.HasServiceScenes) SetScreen(Screen.Routes);
            else networkHost.BeginGame(null);
        }

        private void StartRoute(string scene)
        {
            if (!networkHost) return;
            depot?.Hide();
            if (networkHost.IsHost && networkHost.Phase == PlaygroundHost.SessionPhase.Playing)
            {
                networkHost.LoadServiceScene(scene); // volgende level na een gehaalde dienst
                return;
            }
            if (networkHost.CanStart) networkHost.StartLocal(scene);
            else if (networkHost.IsHost && networkHost.Phase == PlaygroundHost.SessionPhase.Lobby) networkHost.BeginGame(scene);
            if (networkHost.Phase is PlaygroundHost.SessionPhase.Offline or PlaygroundHost.SessionPhase.Lobby && screen == Screen.Routes)
                SetScreen(Screen.Main);
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
                else if (offline)
                {
                    depot.Hide();
                    if (screen != Screen.Main) SetScreen(Screen.Main);
                }
                previousPhase = phase;
            }
            routesStatus.text = networkHost.Message;
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
        private void SetMusicVolume(float value) { value = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ServiceMusic.MusicVolumeKey, value); if (musicVolumeText) musicVolumeText.text = Mathf.RoundToInt(value * 100) + "%"; }
        private void ToggleMusic() { musicEnabled = !musicEnabled; PlayerPrefs.SetInt(ServiceMusic.MusicEnabledKey, musicEnabled ? 1 : 0); if (musicEnabledText) musicEnabledText.text = musicEnabled ? "Aan" : "Uit"; }
        private void ToggleMute() { muted = !muted; AudioListener.pause = muted; PlayerPrefs.SetInt("ChaosUI.Muted", muted ? 1 : 0); if (muteText) muteText.text = muted ? "Uit" : "Aan"; }
        private void ToggleCameraBob() { bool enabled = PlayerPrefs.GetInt("ChaosUI.CameraBob", 1) == 0; PlayerPrefs.SetInt("ChaosUI.CameraBob", enabled ? 1 : 0); if (cameraBobText) cameraBobText.text = enabled ? "Aan" : "Uit"; }
        private void SetViewDistance(float value)
        {
            viewDistance = 25 * Mathf.RoundToInt(Mathf.Lerp(MinimumViewDistance, MaximumViewDistance, value) / 25f);
            PlayerPrefs.SetInt(ViewDistanceKey, viewDistance);
            viewDistanceText.text = $"{viewDistance} m";
            ApplyViewDistance();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnActiveSceneChanged;
        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        private void OnActiveSceneChanged(Scene previous, Scene current) => ApplyViewDistance();

        private void ApplyViewDistance()
        {
            Camera camera = Camera.main;
            if (camera) camera.farClipPlane = viewDistance;
            if (RenderSettings.fog && RenderSettings.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = viewDistance * .24f;
                RenderSettings.fogEndDistance = viewDistance * (14f / 15f);
            }
        }

        private void Unstuck()
        {
            foreach (var player in FindObjectsByType<PlaygroundPlayer>())
                if (player.IsOwner && player.IsSpawned) { player.RequestUnstuck(); SetScreen(Screen.Game); break; }
        }
        private void Update()
        {
            UpdateLobby();
            DepotSync.Tick();
            if (networkHost && !networkHost.IsHost && networkHost.Phase is PlaygroundHost.SessionPhase.Lobby or PlaygroundHost.SessionPhase.Playing)
            {
                // Crew: kijk live mee met het depot van de host.
                if (DepotSync.IsOpen) depot.ShowCrewDepot();
                else if (depot.Current != DepotScreens.Mode.Hidden) depot.Hide();
            }
            var run = ServiceRun.Active;
            if (ServicePresentation.IsModal(networkHost, run))
            {
                if (screen != Screen.Service) SetScreen(Screen.Service);
            }
            else if (screen == Screen.Service)
                SetScreen(networkHost && networkHost.Phase == PlaygroundHost.SessionPhase.Playing ? Screen.Game : Screen.Main);
            service.Present(networkHost, run, screen == Screen.Service, screen == Screen.Game && run, depot.Current != DepotScreens.Mode.Hidden);
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (screen == Screen.Routes) { if (!depot.Back()) SetScreen(Screen.Main); }
            else if (screen == Screen.Service && depot.Current != DepotScreens.Mode.Hidden) depot.Back();
            else if (screen == Screen.Game) SetScreen(Screen.Pause);
            else if (settingsPanel.gameObject.activeSelf) ShowLobby();
            else if (screen == Screen.Pause) SetScreen(Screen.Game);
        }
        private void SetScreen(Screen next)
        {
            screen = next;
            if (depot)
            {
                if (next == Screen.Routes) { if (depot.Current == DepotScreens.Mode.Hidden) depot.ShowLevels(); }
                else if (next != Screen.Service && (networkHost == null || networkHost.IsHost || !DepotSync.IsOpen)) depot.Hide();
            }
            canvas.gameObject.SetActive(next is Screen.Main or Screen.Routes or Screen.Pause);
            mainPanel.gameObject.SetActive(next == Screen.Main);
            logoPanel.gameObject.SetActive(next == Screen.Main);
            ShowLobby();
            Cursor.lockState = next == Screen.Game ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = next != Screen.Game;
        }
        private void OnDestroy() { if (instance == this) instance = null; }
    }
}
