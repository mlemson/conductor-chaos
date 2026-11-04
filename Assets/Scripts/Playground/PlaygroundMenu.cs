using System;
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
            Build();
            bool client = Array.IndexOf(Environment.GetCommandLineArgs(), "-playground-client") >= 0;
            SetScreen(client ? Screen.Game : Screen.Main);
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

            mainPanel = ChaosUI.Panel(root, "Main menu", V(.047f, .16f), V(.275f, .74f),
                ChaosUI.Slate, 5, ChaosUI.Dark);
            playButton = MenuAction("Play", "Spelen", .755f, .945f, ChaosUI.Gold, ChaosUIIcon.Symbol.Play, () => SetScreen(Screen.Game), 42);
            MenuAction("Host", "Spel hosten", .579f, .724f, ChaosUI.Paper, ChaosUIIcon.Symbol.Crew, null, 32);
            MenuAction("Join", "Deelnemen", .405f, .55f, ChaosUI.Paper, ChaosUIIcon.Symbol.Link, null, 32);
            MenuAction("Settings", "Instellingen", .23f, .375f, ChaosUI.Paper, ChaosUIIcon.Symbol.Settings, ShowSettings, 32);
            MenuAction("Quit", "Afsluiten", .055f, .20f, ChaosUI.TrainRed, ChaosUIIcon.Symbol.Power, Application.Quit, 32);

            logoPanel = ChaosUI.Rect(root, "Title badge", V(.045f, .715f), V(.28f, .965f), V(0, 0), V(0, 0));
            ChaosUI.Panel(logoPanel, "Badge top", V(.08f, .31f), V(.92f, .96f), ChaosUI.Paper);
            ChaosUI.Panel(logoPanel, "Badge base", V(.055f, .12f), V(.945f, .72f), ChaosUI.Paper);
            Text conductor = ChaosUI.Label(logoPanel, "Conductor", "Conductor", 52, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold, V(.055f, .51f), V(.945f, .94f));
            Text chaos = ChaosUI.Label(logoPanel, "Chaos", "Chaos", 76, ChaosUI.TrainRed, TextAnchor.MiddleCenter, FontStyle.Bold, V(.055f, .10f), V(.945f, .64f));
            conductor.font = chaos.font = ChaosUI.DisplayFont;
            conductor.resizeTextForBestFit = chaos.resizeTextForBestFit = true;
            conductor.resizeTextMinSize = chaos.resizeTextMinSize = 25;
            conductor.resizeTextMaxSize = 52;
            chaos.resizeTextMaxSize = 76;
            ChaosUIIcon.Add(logoPanel, "Train emblem", ChaosUIIcon.Symbol.Train, V(.42f, .05f), V(.58f, .24f), ChaosUI.Ink);

            lobbyPanel = ChaosUI.Panel(root, "Lobby", V(.597f, .16f), V(.978f, .94f), ChaosUI.Slate, 5, ChaosUI.Dark);
            RectTransform heading = ChaosUI.Panel(lobbyPanel, "Heading paper", V(.035f, .875f), V(.61f, .97f), ChaosUI.Paper);
            ChaosUI.Label(heading, "Heading", "Multiplayerlobby", 30, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            ChaosUI.Label(lobbyPanel, "Availability", "Online volgt later", 16, ChaosUI.Paper, TextAnchor.MiddleRight, FontStyle.Bold, V(.62f, .89f), V(.96f, .955f));
            for (int i = 0; i < 4; i++)
            {
                float y = .75f - i * .115f;
                RectTransform row = ChaosUI.Panel(lobbyPanel, "Player slot " + (i + 1), V(.035f, y), V(.965f, y + .095f), ChaosUI.Dark, 1, ChaosUI.Muted);
                ChaosUIIcon.Add(row, "Avatar", ChaosUIIcon.Symbol.Person, V(.025f, .12f), V(.11f, .88f), ChaosUI.Muted);
                ChaosUI.Label(row, "Slot", "Lege plek", 19, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.14f, .42f), V(.95f, .95f));
                ChaosUI.Label(row, "Status", "Nog niemand verbonden", 14, ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(.14f, .04f), V(.95f, .48f));
            }
            RectTransform route = ChaosUI.Panel(lobbyPanel, "Route", V(.035f, .13f), V(.965f, .275f), ChaosUI.Dark, 1, ChaosUI.Muted);
            ChaosUIIcon.Add(route, "Route icon", ChaosUIIcon.Symbol.Train, V(.04f, .16f), V(.19f, .84f), ChaosUI.Gold);
            ChaosUI.Label(route, "Route label", "ROUTE", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.23f, .48f), V(.96f, .94f));
            ChaosUI.Label(route, "Route status", "Routekeuze volgt later", 15, ChaosUI.Muted, TextAnchor.MiddleLeft, FontStyle.Normal, V(.23f, .05f), V(.96f, .52f));
            ChaosUI.Action(lobbyPanel, "Play", "Start lokale test", V(.035f, .025f), V(.965f, .105f), ChaosUI.Gold, ChaosUI.Ink, () => SetScreen(Screen.Game), 21);

            settingsPanel = ChaosUI.Panel(root, "Settings", V(.34f, .18f), V(.965f, .75f), ChaosUI.Slate, 3, ChaosUI.Dark);
            RectTransform settingsHeading = ChaosUI.Panel(settingsPanel, "Heading paper", V(.035f, .86f), V(.32f, .98f), ChaosUI.Paper);
            ChaosUI.Label(settingsHeading, "Heading", "INSTELLINGEN", 24, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform audioTab = ChaosUI.Panel(settingsPanel, "Selected audio tab", V(.035f, .73f), V(.25f, .84f), ChaosUI.Gold);
            ChaosUI.Label(audioTab, "Label", "GELUID", 18, ChaosUI.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            ChaosUI.Action(settingsPanel, "Controls tab", "BESTURING · LATER", V(.26f, .73f), V(.49f, .84f), ChaosUI.Paper, ChaosUI.Ink, null, 16, false);
            ChaosUI.Action(settingsPanel, "Accessibility tab", "TOEGANKELIJKHEID · LATER", V(.50f, .73f), V(.84f, .84f), ChaosUI.Paper, ChaosUI.Ink, null, 15, false);
            ChaosUI.Label(settingsPanel, "Audio", "MASTER VOLUME", 19, ChaosUI.GoldLight, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .62f), V(.96f, .70f));
            ChaosUI.Label(settingsPanel, "Volume", "Volume", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .52f), V(.34f, .64f));
            ChaosUI.VolumeSlider(settingsPanel, V(.36f, .555f), V(.78f, .60f), AudioListener.volume, SetVolume);
            volumeText = ChaosUI.Label(settingsPanel, "Volume value", Mathf.RoundToInt(AudioListener.volume * 100) + "%", 18, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.8f, .53f), V(.96f, .625f));
            ChaosUI.Label(settingsPanel, "Sound", "Geluid", 20, ChaosUI.Paper, TextAnchor.MiddleLeft, FontStyle.Bold, V(.04f, .36f), V(.34f, .48f));
            Button soundSwitch = ChaosUI.Action(settingsPanel, "Sound switch", muted ? "Uit" : "Aan",
                V(.36f, .36f), V(.52f, .48f), ChaosUI.Paper, ChaosUI.Ink, ToggleMute, 23);
            muteText = soundSwitch.GetComponentInChildren<Text>();
            settingsBack = ChaosUI.Action(settingsPanel, "Back", "Terug", V(.69f, .035f), V(.96f, .125f), ChaosUI.Paper, ChaosUI.Ink, ShowLobby, 18);

            pausePanel = ChaosUI.Panel(root, "Pause", V(.36f, .19f), V(.64f, .81f), ChaosUI.Slate, 2, ChaosUI.Muted);
            ChaosUI.Label(pausePanel, "Heading", "GEPAUZEERD", 32, ChaosUI.Paper, TextAnchor.MiddleCenter, FontStyle.Bold, V(.05f, .8f), V(.95f, .95f));
            resumeButton = ChaosUI.Action(pausePanel, "Resume", "Verder spelen", V(.08f, .59f), V(.92f, .73f), ChaosUI.Gold, ChaosUI.Ink, () => SetScreen(Screen.Game), 22);
            ChaosUI.Action(pausePanel, "Settings", "INSTELLINGEN", V(.08f, .42f), V(.92f, .56f), ChaosUI.Paper, ChaosUI.Ink, ShowSettings, 20);
            ChaosUI.Action(pausePanel, "Main", "HOOFDMENU", V(.08f, .25f), V(.92f, .39f), ChaosUI.Paper, ChaosUI.Ink, () => SetScreen(Screen.Main), 20);
            ChaosUI.Label(pausePanel, "Notice", "De wereld blijft actief terwijl dit menu open is.", 15, ChaosUI.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, V(.05f, .05f), V(.95f, .2f));
        }

        private Button MenuAction(string name, string caption, float bottom, float top, Color color,
            ChaosUIIcon.Symbol symbol, UnityEngine.Events.UnityAction action, int size)
        {
            Color ink = color == ChaosUI.TrainRed ? ChaosUI.Paper : ChaosUI.Ink;
            Button button = ChaosUI.Action(mainPanel, name, caption, V(.085f, bottom), V(.915f, top),
                color, ink, action, size, action != null);
            RectTransform face = (RectTransform)button.targetGraphic.transform.parent;
            var label = face.GetComponentInChildren<Text>().rectTransform;
            label.anchorMin = V(.23f, 0f);
            label.anchorMax = V(.96f, 1f);
            ChaosUIIcon.Add(face, "Icon", symbol, V(.065f, .14f), V(.23f, .86f), ink);
            return button;
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
        private void SetVolume(float value) { AudioListener.volume = value; PlayerPrefs.SetFloat("ChaosUI.Volume", value); if (volumeText) volumeText.text = Mathf.RoundToInt(value * 100) + "%"; }
        private void ToggleMute() { muted = !muted; AudioListener.pause = muted; PlayerPrefs.SetInt("ChaosUI.Muted", muted ? 1 : 0); if (muteText) muteText.text = muted ? "Uit" : "Aan"; }
        private void Update()
        {
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
