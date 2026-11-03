using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ConductorChaos.UI
{
    /// <summary>
    /// Matte uGUI style primitives. Shared by the menu preview and the playable trial HUD.
    /// Values reflect Docs/References/palette.json; there are no external font/icon dependencies.
    /// </summary>
    public static class ChaosUI
    {
        public static readonly Color Paper = Hex("#F4DCC0");
        public static readonly Color Ink = Hex("#351B13");
        public static readonly Color TrainRed = Hex("#C54D3E");
        public static readonly Color TrainRedDark = Hex("#8D3432");
        public static readonly Color Gold = Hex("#F6BC33");
        public static readonly Color GoldLight = Hex("#FEECA1");
        public static readonly Color Slate = Hex("#403E4B");
        public static readonly Color Dark = Hex("#302629");
        public static readonly Color Navy = Hex("#34335B");
        public static readonly Color Muted = Hex("#DAC3B2");
        public static readonly Color Green = Hex("#8B9F3B");

        private static Font cachedFont;
        private static Sprite rounded;

        public static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color result) ? result : Color.white;
        }

        public static Font Font
        {
            get
            {
                if (!cachedFont)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }

        // One white nine-sliced rounded sprite; reused on every colored panel and button.
        // No blurry glow or texture allocations per widget.
        private static Sprite Rounded
        {
            get
            {
                if (rounded) return rounded;
                const int size = 64;
                const float radius = 15f;
                Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "ChaosUI-RoundedMask",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - (x + 0.5f), 0f, (x + 0.5f) - (size - radius));
                    float dy = Mathf.Max(radius - (y + 0.5f), 0f, (y + 0.5f) - (size - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(radius - 1.5f, radius + 0.5f, distance));
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(20f, 20f, 20f, 20f));
                rounded.name = "ChaosUI-RoundedNineSlice";
                rounded.hideFlags = HideFlags.HideAndDontSave;
                return rounded;
            }
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetsMin, Vector2 offsetsMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetsMin;
            rt.offsetMax = offsetsMax;
            rt.localScale = Vector3.one;
            return rt;
        }

        public static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max,
            Color color, int border = 0, Color outline = default)
        {
            RectTransform rt = Rect(parent, name, min, max, Vector2.zero, Vector2.zero);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            if (border > 0)
            {
                Color inner = color;
                image.color = outline == default ? Dark : outline;
                // A solid 2px rim, not a glow: panels stay legible over scenery.
                PanelInset(rt, inner, border);
            }
            return rt;
        }

        public static RectTransform PanelInset(Transform parent, Color color, float margin)
        {
            RectTransform rt = Rect(parent, "Inner", Vector2.zero, Vector2.one,
                new Vector2(margin, margin), new Vector2(-margin, -margin));
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rt;
        }

        public static Text Label(Transform parent, string name, string caption, int size, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal,
            Vector2? min = null, Vector2? max = null)
        {
            RectTransform rt = Rect(parent, name, min ?? Vector2.zero, max ?? Vector2.one,
                new Vector2(10f, 0f), new Vector2(-10f, 0f));
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.text = caption;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        public static Button Action(Transform parent, string name, string caption, Vector2 min,
            Vector2 max, Color face, Color foreground, UnityEngine.Events.UnityAction onClick,
            int size = 20, bool enabled = true)
        {
            RectTransform rt = Panel(parent, name, min, max, face);
            Image img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            Button button = rt.gameObject.AddComponent<Button>();
            button.interactable = enabled;
            button.transition = Selectable.Transition.ColorTint;
            var tint = button.colors;
            tint.normalColor = Color.white;
            tint.highlightedColor = new Color(1f, 0.96f, 0.89f, 1f);
            tint.pressedColor = new Color(0.84f, 0.79f, 0.72f, 1f);
            tint.disabledColor = new Color(0.61f, 0.58f, 0.62f, 0.7f);
            tint.colorMultiplier = 1f;
            button.colors = tint;
            if (onClick != null) button.onClick.AddListener(onClick);
            Label(rt, "Caption", caption, size, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        public static Canvas MakeCanvas(Transform parent, string name, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent) go.transform.SetParent(parent, false);
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.GetComponent<GraphicRaycaster>().enabled = interactive;
            if (interactive && !UnityEngine.Object.FindAnyObjectByType<EventSystem>())
            {
                var eventGo = new GameObject("Chaos UI EventSystem", typeof(EventSystem));
                if (parent) eventGo.transform.SetParent(parent, false);
                eventGo.AddComponent<InputSystemUIInputModule>(); // Uses Input System default UI actions.
            }
            return canvas;
        }

        public static Slider VolumeSlider(Transform parent, Vector2 min, Vector2 max, float value,
            UnityEngine.Events.UnityAction<float> onChange)
        {
            RectTransform baseRt = Panel(parent, "Volume track", min, max, Slate);
            baseRt.GetComponent<Image>().raycastTarget = true;
            var slider = baseRt.gameObject.AddComponent<Slider>();
            RectTransform fillArea = Rect(baseRt, "FillArea", Vector2.zero, Vector2.one,
                new Vector2(4, 4), new Vector2(-4, -4));
            RectTransform fill = Panel(fillArea, "Fill", Vector2.zero, Vector2.one, Gold);
            fill.anchorMax = new Vector2(0.5f, 1f);
            RectTransform handleArea = Rect(baseRt, "HandleArea", Vector2.zero, Vector2.one,
                new Vector2(9, 0), new Vector2(-9, 0));
            RectTransform handle = Panel(handleArea, "Handle", new Vector2(0.5f, 0), new Vector2(0.5f, 1), GoldLight);
            handle.sizeDelta = new Vector2(22, 4);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }
    }
}
