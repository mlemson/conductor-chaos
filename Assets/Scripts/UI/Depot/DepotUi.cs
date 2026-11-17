// Conductor Chaos — bouwstenen voor de depot- en levelschermen (issue #239). Alles in pixels op het
// referentiecanvas van 1600×900 (zie UI_STYLE_GUIDE); een StageFitter schaalt dat vlak in het scherm.
// Gebruikt ChaosUI voor panelen/knoppen en de goedgekeurde iconen via ChaosUISpriteCatalog.
using UnityEngine;
using UnityEngine.UI;

namespace ConductorChaos.UI.Depot
{
    /// <summary>Houdt een vast 1600×900-vlak passend en gecentreerd in de canvas.</summary>
    public sealed class StageFitter : MonoBehaviour
    {
        public Vector2 Size = new(1600f, 900f);
        private RectTransform rect, parent;
        private Vector2 lastParent;

        private void Awake()
        {
            rect = (RectTransform)transform;
            parent = (RectTransform)transform.parent;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = Size;
            rect.anchoredPosition = Vector2.zero;
        }

        private void LateUpdate()
        {
            if (!parent) return;
            Vector2 size = parent.rect.size;
            if (size == lastParent) return;
            lastParent = size;
            float scale = Mathf.Min(size.x / Size.x, size.y / Size.y);
            rect.localScale = new Vector3(scale, scale, 1f);
        }
    }

    public static class DepotUi
    {
        public static readonly Color Background = ChaosUI.Hex("#241D21");
        public static readonly Color PanelFace = ChaosUI.Dark;
        public static readonly Color Edge = ChaosUI.Hex("#4A3D40");
        public static readonly Color Inset = ChaosUI.Hex("#1F181B");
        public static readonly Color Tab = ChaosUI.Hex("#3A3034");
        public static readonly Color Locked = ChaosUI.Hex("#2A2226");
        public static readonly Color Disabled = ChaosUI.Hex("#4A4045");
        public static readonly Color DisabledText = ChaosUI.Hex("#A99A92");
        public static readonly Color Dim = ChaosUI.Hex("#8A7A72");
        public static readonly Color GoldDark = ChaosUI.Hex("#B9861C");
        public static readonly Color LostGold = ChaosUI.Hex("#8A6A2E");
        public static readonly Color GreenText = ChaosUI.Hex("#FFFBEF");
        public static readonly Color RedText = ChaosUI.Hex("#FFF4E6");
        public static readonly Color Rail = ChaosUI.Hex("#7A5A4E");
        public static readonly Color Sleeper = ChaosUI.Hex("#5A4038");
        public static readonly Color NegativeText = ChaosUI.Hex("#E8806F");

        /// <summary>Een leeg vlak op (x, y) vanaf linksboven van de ouder.</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = ChaosUI.Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, Vector2.zero);
            Place(rt, x, y, w, h);
            return rt;
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        public static RectTransform Fill(Transform parent, string name)
        {
            return ChaosUI.Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color, Color? edge = null, int border = 2)
        {
            RectTransform rt = edge.HasValue
                ? ChaosUI.Panel(parent, name, Vector2.zero, Vector2.one, color, border, edge.Value)
                : ChaosUI.Panel(parent, name, Vector2.zero, Vector2.one, color);
            Place(rt, x, y, w, h);
            return rt;
        }

        /// <summary>Tekst; heading = Baloo 2, anders Nunito.</summary>
        public static Text Txt(Transform parent, string caption, float x, float y, float w, float h, int size, Color color,
            bool heading = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text text = ChaosUI.Label(parent, "Tekst", caption, size, color, anchor, heading ? FontStyle.Bold : FontStyle.Normal);
            var rt = (RectTransform)text.transform;
            Place(rt, x, y, w, h);
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static float Measure(string caption, int size, bool heading)
        {
            var settings = new TextGenerationSettings
            {
                font = heading ? ChaosUI.HeadingFont : ChaosUI.BodyFont,
                fontSize = size,
                fontStyle = FontStyle.Normal,
                generationExtents = new Vector2(4000f, 200f),
                scaleFactor = 1f,
                richText = false,
                textAnchor = TextAnchor.MiddleLeft,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                verticalOverflow = VerticalWrapMode.Overflow,
                updateBounds = false,
                pivot = Vector2.zero,
                lineSpacing = 1f
            };
            var generator = new TextGenerator();
            return generator.GetPreferredWidth(caption ?? "", settings);
        }

        /// <summary>Hoogte van tekst die binnen een vaste breedte afbreekt.</summary>
        public static float MeasureHeight(string caption, int size, bool heading, float width)
        {
            var settings = new TextGenerationSettings
            {
                font = heading ? ChaosUI.HeadingFont : ChaosUI.BodyFont,
                fontSize = size,
                fontStyle = FontStyle.Normal,
                generationExtents = new Vector2(width, 2000f),
                scaleFactor = 1f,
                richText = false,
                textAnchor = TextAnchor.UpperLeft,
                horizontalOverflow = HorizontalWrapMode.Wrap,
                verticalOverflow = VerticalWrapMode.Overflow,
                updateBounds = false,
                pivot = Vector2.zero,
                lineSpacing = 1f
            };
            return new TextGenerator().GetPreferredHeight(caption ?? "", settings);
        }

        public static Image Pic(Transform parent, Sprite sprite, float x, float y, float w, float h, bool preserve = true, Color? tint = null)
        {
            var rt = Box(parent, sprite ? sprite.name : "Afbeelding", x, y, w, h);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = preserve;
            image.raycastTarget = false;
            if (tint.HasValue) image.color = tint.Value;
            if (!sprite) image.color = Color.clear;
            return image;
        }

        public static Image Icon(Transform parent, string key, float x, float y, float size, Color? tint = null)
        {
            return Pic(parent, ChaosUISpriteCatalog.Find(key), x, y, size, size, true, tint);
        }

        /// <summary>Een treinfoto (render) als sprite; op slot = grijs en donker, maar herkenbaar.</summary>
        public static Image Vehicle(Transform parent, string spriteName, float x, float y, float w, float h, bool locked = false)
        {
            var sprite = Resources.Load<UnityEngine.Sprite>("Depot/" + spriteName);
            var image = Pic(parent, sprite, x, y, w, h, true);
            if (sprite && locked) image.color = new Color(.42f, .40f, .42f, 1f);
            return image;
        }

        /// <summary>Spoorstaaf met dwarsliggers, onder een trein in een kaart.</summary>
        public static void Track(Transform parent, float x, float y, float w)
        {
            var rail = ChaosUI.Panel(parent, "Spoorstaaf", Vector2.zero, Vector2.one, Rail);
            Place(rail, x, y, w, 3f);
            rail.GetComponent<Image>().type = Image.Type.Simple;
            rail.GetComponent<Image>().sprite = null;
            for (float sx = x + 4f; sx < x + w - 6f; sx += 22f)
            {
                var sleeper = Box(parent, "Dwarsligger", sx, y + 3f, 10f, 7f);
                var image = sleeper.gameObject.AddComponent<Image>();
                image.color = Sleeper;
                image.raycastTarget = false;
            }
        }

        /// <summary>Label met optioneel icoon; breedte volgt de tekst. Geeft de breedte terug.</summary>
        public static float Chip(Transform parent, string caption, float x, float y, Color face, Color ink, string iconKey = null,
            int size = 13, float height = 30f, bool rightAligned = false)
        {
            float icon = string.IsNullOrEmpty(iconKey) ? 0f : height - 8f;
            float width = Measure(caption, size, false) + 20f + (icon > 0 ? icon + 4f : 0f);
            float left = rightAligned ? x - width : x;
            var chip = Panel(parent, "Chip " + caption, left, y, width, height, face);
            float textX = 10f;
            if (icon > 0f)
            {
                Icon(chip, iconKey, 5f, 4f, icon);
                textX = icon + 9f;
            }
            Txt(chip, caption, textX, 0f, width - textX - 4f, height, size, ink);
            return width;
        }

        public static void Segments(Transform parent, float x, float y, float w, float h, int filled, int total = 5, int lost = 0)
        {
            float gap = 5f;
            float each = (w - gap * (total - 1)) / total;
            for (int i = 0; i < total; i++)
            {
                Color color = i < filled ? ChaosUI.Gold : i < filled + lost ? LostGold : ChaosUI.Slate;
                Panel(parent, "Segment", x + i * (each + gap), y, each, h, color);
            }
        }

        public static Button Btn(Transform parent, string caption, float x, float y, float w, float h, Color face,
            UnityEngine.Events.UnityAction onClick, int size = 22, bool enabled = true, string iconKey = null)
        {
            Color ink = face == ChaosUI.TrainRed || face == ChaosUI.Green ? RedText : ChaosUI.Ink;
            if (!enabled) { face = Disabled; ink = DisabledText; }
            var button = ChaosUI.Action(parent, caption, caption, Vector2.zero, Vector2.one, face, ink, onClick, size, enabled, iconKey);
            Place((RectTransform)button.transform, x, y, w, h);
            if (!enabled)
            {
                var shadow = button.GetComponent<Shadow>();
                if (shadow) shadow.enabled = false;
            }
            return button;
        }

        /// <summary>Een onzichtbare klikbare laag over een kaart (hover/klik), zodat de hele kaart werkt.</summary>
        public static Button Hit(Transform parent, UnityEngine.Events.UnityAction onClick, Color? hover = null)
        {
            var rt = Fill(parent, "Klikvlak");
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = hover ?? new Color(1f, .96f, .89f, .07f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(0f, 0f, 0f, .12f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static string Time(int seconds) => seconds < 0 ? "-" : $"{seconds / 60}:{seconds % 60:00}";

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
