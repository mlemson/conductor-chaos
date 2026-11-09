using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Stationsbord met een in te typen naam (issue #209). Bouwt de tekst op uit de ronde bordletters
    /// (<see cref="StationGlyphFont"/>), centreert treinpictogram + tekst en maakt alles kleiner als de naam te lang is,
    /// net als de generator in ArtSource/Environment/Station/station_props.py.
    /// Zit op de knoop Sign_* van Station_Main, Station_Building en Prop_StationSign; de prefabbouwer
    /// (Conductor Chaos > Art > Station) zet hem er automatisch op en vult de maten in.
    /// De tekst is scènedata (lokaal, cosmetisch): geen netwerkstate.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Conductor Chaos/Station Sign")]
    public sealed class StationSign : MonoBehaviour
    {
        public const int MaxLength = 28;

        /// <summary>Voorbeeldnamen voor de stations (keuzelijst in de Inspector).</summary>
        public static readonly string[] ExampleNames =
        {
            "Bumblewick", "Whistleton", "Puddleford", "Wobblebridge", "Muddle Junction",
            "Brackenhill", "Tumbleton", "Crooked Creek", "Clatterbury", "Last Stop"
        };

        [Tooltip("Tekst op het bord. Hoofdletters A–Z, cijfers, spatie, - ' . (andere tekens vallen weg).")]
        [SerializeField] string text = "STATION";
        [SerializeField] bool showIcon = true;

        [Header("Bord (ingevuld door de prefabbouwer)")]
        [SerializeField] StationGlyphFont font;
        [Tooltip("Kaphoogte van de letters als de tekst past (m).")]
        [SerializeField] float letterHeight = 0.28f;
        [Tooltip("Vrije breedte tussen de klinknagels (m).")]
        [SerializeField] float room = 2.5f;
        [SerializeField] float iconWidth = 0.364f;
        [SerializeField] float iconGap = 0.126f;
        [Tooltip("Pictogram in het model = iconWidth × deze schaal.")]
        [SerializeField] float iconScale = 1f;
        [SerializeField] float textZ = 0.066f;
        [SerializeField] float textDepth = 0.035f;
        [Tooltip("Gebakken tekst uit het model (wordt verborgen; materiaal wordt overgenomen).")]
        [SerializeField] Renderer bakedText;
        [Tooltip("Gebakken treinpictogram uit het model (wordt verborgen en verschoven nagebouwd).")]
        [SerializeField] Renderer bakedIcon;

        GameObject runtime;
        Mesh textMesh;

        public string Text
        {
            get => text;
            set { text = value ?? ""; Rebuild(); }
        }

        public bool ShowIcon
        {
            get => showIcon;
            set { showIcon = value; Rebuild(); }
        }

        public StationGlyphFont Font => font;

        /// <summary>Door de prefabbouwer: maten van het bord en de gebakken onderdelen.</summary>
        public void Configure(StationGlyphFont glyphs, string defaultText, float height, float freeWidth, float icon,
                              float gap, float bakedIconScale, float z, float depth, Renderer textRenderer,
                              Renderer iconRenderer)
        {
            font = glyphs;
            text = defaultText;
            letterHeight = height;
            room = freeWidth;
            iconWidth = icon;
            iconGap = gap;
            iconScale = Mathf.Max(0.0001f, bakedIconScale);
            textZ = z;
            textDepth = depth;
            bakedText = textRenderer;
            bakedIcon = iconRenderer;
        }

        /// <summary>Hoofdletters, accenten weg (Ë -> E), alleen tekens uit de letterset, enkele spaties.</summary>
        public static string Normalize(string value, StationGlyphFont glyphs)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var plain = new StringBuilder();
            foreach (var c in value.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(c);
            var sb = new StringBuilder();
            foreach (var raw in plain.ToString().ToUpperInvariant())
            {
                char c = raw == '’' || raw == '`' ? '\'' : raw;
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                    continue;
                }
                if (glyphs == null || glyphs.Supports(c)) sb.Append(c);
                if (sb.Length >= MaxLength) break;
            }
            return sb.ToString().Trim();
        }

        void OnEnable() => Rebuild();

        void OnDisable() => Clear();

        void OnDestroy() => Clear();

#if UNITY_EDITOR
        void OnValidate()
        {
            // objecten maken mag niet tijdens OnValidate: even uitstellen
            UnityEditor.EditorApplication.delayCall += () => { if (this && isActiveAndEnabled) Rebuild(); };
        }
#endif

        /// <summary>Bouwt pictogram en tekst opnieuw op volgens <see cref="Text"/>.</summary>
        public void Rebuild()
        {
            if (!gameObject.scene.IsValid()) return;           // prefab-asset zelf: niets bouwen
            if (font == null || font.glyphs.Count == 0)
            {
                Clear();
                if (bakedText) bakedText.enabled = true;            // geen letters: gebakken tekst laten zien
                if (bakedIcon) bakedIcon.enabled = true;
                return;
            }
            if (bakedText) bakedText.enabled = false;
            if (bakedIcon) bakedIcon.enabled = false;
            EnsureRuntime();

            string t = Normalize(text, font);
            // indeling zoals station_board_parts(): breedte in kaphoogtes
            float x = 0f;
            bool any = false;
            var placed = new List<(Mesh mesh, float x)>();
            foreach (char c in t)
            {
                if (c == ' ') { x += font.space; continue; }
                if (!font.TryGet(c, out var g)) continue;
                placed.Add((g.mesh, x));
                x += g.advance + font.spacing;
                any = true;
            }
            float units = any ? x - font.spacing : 0f;

            float th = letterHeight;
            float iw = showIcon ? iconWidth : 0f;
            float gap = showIcon && any ? iconGap : 0f;
            float total = iw + gap + units * th;
            float k = total > room && total > 0f ? room / total : 1f;
            th *= k; iw *= k; gap *= k; total *= k;
            float x0 = -total / 2f;
            float xs = font.xSign;

            // pictogram
            var icon = runtime.transform.GetChild(0);
            icon.gameObject.SetActive(showIcon && bakedIcon);
            if (showIcon && bakedIcon)
            {
                var src = bakedIcon.transform;
                var p = src.localPosition;
                icon.localPosition = new Vector3(xs * (x0 + iw / 2f), p.y, p.z);
                icon.localRotation = src.localRotation;
                icon.localScale = src.localScale * (k / iconScale);
            }

            // tekst: letters samenvoegen in de ruimte van het bord
            float start = x0 + iw + gap;
            var parts = new CombineInstance[placed.Count];
            for (int i = 0; i < placed.Count; i++)
            {
                var pos = new Vector3(xs * (start + placed[i].x * th), -0.5f * th, textZ);
                parts[i] = new CombineInstance
                {
                    mesh = placed[i].mesh,
                    transform = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(th, th, textDepth))
                };
            }
            textMesh.Clear();
            if (parts.Length > 0) textMesh.CombineMeshes(parts, true, true);
            textMesh.RecalculateBounds();
        }

        void EnsureRuntime()
        {
            if (runtime) return;
            runtime = new GameObject("SignRuntime") { hideFlags = HideFlags.HideAndDontSave };
            runtime.transform.SetParent(transform, false);
            runtime.layer = gameObject.layer;

            var icon = new GameObject("Icon") { hideFlags = HideFlags.HideAndDontSave };
            icon.transform.SetParent(runtime.transform, false);
            icon.layer = gameObject.layer;
            if (bakedIcon)
            {
                var mf = bakedIcon.GetComponent<MeshFilter>();
                icon.AddComponent<MeshFilter>().sharedMesh = mf ? mf.sharedMesh : null;
                CopyRenderer(bakedIcon, icon.AddComponent<MeshRenderer>());
            }

            var label = new GameObject("Text") { hideFlags = HideFlags.HideAndDontSave };
            label.transform.SetParent(runtime.transform, false);
            label.layer = gameObject.layer;
            textMesh = new Mesh { name = "StationSignText", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            label.AddComponent<MeshFilter>().sharedMesh = textMesh;
            var mr = label.AddComponent<MeshRenderer>();
            if (bakedText) CopyRenderer(bakedText, mr);
            else if (bakedIcon) CopyRenderer(bakedIcon, mr);
        }

        static void CopyRenderer(Renderer from, MeshRenderer to)
        {
            to.sharedMaterials = from.sharedMaterials;
            to.shadowCastingMode = from.shadowCastingMode;
            to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage;
        }

        void Clear()
        {
            if (runtime) DestroyImmediateSafe(runtime);
            if (textMesh) DestroyImmediateSafe(textMesh);
            runtime = null;
            textMesh = null;
        }

        static void DestroyImmediateSafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
