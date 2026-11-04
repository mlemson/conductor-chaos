using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Playground
{
    [DefaultExecutionOrder(-1000)]
    public sealed class PlaygroundMenu : MonoBehaviour
    {
        private enum Screen { Main, Game, Pause }

        private static PlaygroundMenu instance;
        private Screen screen;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle noteStyle;

        public static bool BlocksInput => instance && instance.screen != Screen.Game;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (SceneManager.GetActiveScene().name != "TrainPlayground" || instance) return;
            new GameObject("Playground Menu").AddComponent<PlaygroundMenu>();
        }

        private void Awake()
        {
            instance = this;
            bool client = Array.IndexOf(Environment.GetCommandLineArgs(), "-playground-client") >= 0;
            SetScreen(client ? Screen.Game : Screen.Main);
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (screen == Screen.Game) SetScreen(Screen.Pause);
            else if (screen == Screen.Pause) SetScreen(Screen.Game);
        }

        private void SetScreen(Screen next)
        {
            screen = next;
            Cursor.lockState = next == Screen.Game ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = next != Screen.Game;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnGUI()
        {
            if (screen == Screen.Game) return;
            if (titleStyle == null) CreateStyles();

            float scale = Mathf.Min(ScreenWidth() / 1280f, ScreenHeight() / 720f);
            scale = Mathf.Clamp(scale, 0.65f, 1.8f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = ScreenWidth() / scale;
            float height = ScreenHeight() / scale;
            var oldColor = GUI.color;
            GUI.color = new Color(0.10f, 0.09f, 0.16f, 0.86f);
            GUI.DrawTexture(new Rect(0, 0, width, height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            float panelWidth = Mathf.Min(440f, width - 30f);
            float x = (width - panelWidth) * 0.5f;
            float y = Mathf.Max(30f, (height - 480f) * 0.5f);
            GUI.Label(new Rect(x, y, panelWidth, 68), "CONDUCTOR CHAOS", titleStyle);
            GUI.Label(new Rect(x, y + 68, panelWidth, 32), screen == Screen.Main ? "Samen op reis" : "Even pauze", subtitleStyle);
            y += 122f;

            if (screen == Screen.Main)
            {
                if (GUI.Button(new Rect(x, y, panelWidth, 46), "PLAY", buttonStyle)) SetScreen(Screen.Game);
                y += 58f;
                DisabledButton(x, ref y, panelWidth, "VRIENDEN UITNODIGEN");
                DisabledButton(x, ref y, panelWidth, "INSTELLINGEN");
                DisabledButton(x, ref y, panelWidth, "CREDITS");
                if (GUI.Button(new Rect(x, y, panelWidth, 46), "AFSLUITEN", buttonStyle)) Application.Quit();
            }
            else
            {
                if (GUI.Button(new Rect(x, y, panelWidth, 46), "VERDER", buttonStyle)) SetScreen(Screen.Game);
                y += 58f;
                DisabledButton(x, ref y, panelWidth, "INSTELLINGEN");
                if (GUI.Button(new Rect(x, y, panelWidth, 46), "HOOFDMENU", buttonStyle)) SetScreen(Screen.Main);
                y += 58f;
                GUI.Label(new Rect(x, y, panelWidth, 48), "De wereld blijft actief terwijl dit menu open is.", noteStyle);
            }
            GUI.matrix = Matrix4x4.identity;
        }

        private void DisabledButton(float x, ref float y, float width, string label)
        {
            GUI.enabled = false;
            GUI.Button(new Rect(x, y, width, 46), label + "  ·  binnenkort", buttonStyle);
            GUI.enabled = true;
            y += 58f;
        }

        private void CreateStyles()
        {
            Color cream = new Color(0.98f, 0.91f, 0.80f);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            titleStyle.normal.textColor = cream;
            subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            subtitleStyle.normal.textColor = new Color(1f, 0.72f, 0.18f);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, wordWrap = true };
            noteStyle.normal.textColor = cream;
        }

        private static float ScreenWidth() => UnityEngine.Screen.width;
        private static float ScreenHeight() => UnityEngine.Screen.height;
    }
}
