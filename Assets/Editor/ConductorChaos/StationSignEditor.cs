// Conductor Chaos — aanpasbare stationsborden (issue #209).
// * StationSignTools: maakt StationGlyphFont.asset uit de bordletters (SignGlyphs/StationGlyphs.fbx + .json) en zet
//   StationSign op elke Sign_*-knoop van de stationsprefabs (aangeroepen door de prefabbouwer in StationKitEditor).
// * Inspector voor StationSign: naam intypen of een voorbeeldnaam kiezen; het bord wordt direct opnieuw opgebouwd.
using System;
using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Stations;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class StationSignTools
    {
        public const string GlyphDir = "Assets/Art/Models/Environment/SignGlyphs";
        const string FontPath = GlyphDir + "/StationGlyphFont.asset";
        const string FbxPath = GlyphDir + "/StationGlyphs.fbx";
        const string JsonPath = GlyphDir + "/StationGlyphs.json";

#pragma warning disable 0649 // velden worden door JsonUtility gevuld
        [Serializable] class GlyphData { public string c; public float advance; }

        [Serializable]
        class SignData
        {
            public string asset, node, text;
            public float letterHeight, room, iconWidth, iconGap, iconScale = 1f, textZ, textDepth;
        }

        [Serializable] class Data { public float spacing = 0.3f, space = 0.5f; public GlyphData[] glyphs; public SignData[] signs; }
#pragma warning restore 0649

        static Data Load()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
            return json ? JsonUtility.FromJson<Data>(json.text) : null;
        }

        /// <summary>Bestaand letterasset, of opnieuw maken als het ontbreekt of leeg is.</summary>
        public static StationGlyphFont GetFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<StationGlyphFont>(FontPath);
            return font && font.glyphs.Count > 0 && font.glyphs.All(g => g.mesh) ? font : BuildFont();
        }

        [MenuItem("Conductor Chaos/Art/Station/Rebuild Sign Letters")]
        public static void BuildFontMenu() => BuildFont();

        public static StationGlyphFont BuildFont()
        {
            var data = Load();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (data == null || data.glyphs == null || !model)
            {
                Debug.LogWarning($"[Station] Bordletters ontbreken ({FbxPath} / {JsonPath}); borden houden hun vaste tekst.");
                return null;
            }
            var meshes = new Dictionary<string, Mesh>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh && !meshes.ContainsKey(mf.name)) meshes.Add(mf.name, mf.sharedMesh);

            var font = AssetDatabase.LoadAssetAtPath<StationGlyphFont>(FontPath);
            bool create = !font;
            if (create) font = ScriptableObject.CreateInstance<StationGlyphFont>();
            font.spacing = data.spacing;
            font.space = data.space;
            font.glyphs.Clear();
            foreach (var g in data.glyphs)
            {
                if (string.IsNullOrEmpty(g.c) || !meshes.TryGetValue("G_" + (int)g.c[0], out var mesh)) continue;
                font.glyphs.Add(new StationGlyphFont.Glyph { character = g.c, advance = g.advance, mesh = mesh });
            }
            // in ArtSource loopt de L van x = 0 naar 0,5; na de FBX-import kan X gespiegeld zijn
            var l = font.glyphs.FirstOrDefault(g => g.character == "L");
            font.xSign = l.mesh && l.mesh.bounds.center.x < 0f ? -1f : 1f;
            if (create) AssetDatabase.CreateAsset(font, FontPath);
            else EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Station] {font.glyphs.Count} bordletters in {FontPath}.");
            return font;
        }

        /// <summary>Zet StationSign op de Sign_*-knopen van een stationsmodel. Geeft het aantal borden terug.</summary>
        public static int Attach(GameObject root, string assetName, StationGlyphFont font)
        {
            var data = Load();
            if (data?.signs == null || !font) return 0;
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Sign_") || t.name.EndsWith("_Text") || t.name.EndsWith("_Icon")) continue;
                var s = data.signs.FirstOrDefault(x => x.asset == assetName && x.node == t.name);
                if (s == null) continue;
                var text = t.Find(t.name + "_Text");
                var icon = t.Find(t.name + "_Icon");
                var textR = text ? text.GetComponent<Renderer>() : null;
                var iconR = icon ? icon.GetComponent<Renderer>() : null;
                var sign = t.GetComponent<StationSign>();
                if (!sign) sign = t.gameObject.AddComponent<StationSign>();
                sign.Configure(font, s.text, s.letterHeight, s.room, s.iconWidth, s.iconGap, s.iconScale, s.textZ,
                               s.textDepth, textR, iconR);
                if (textR) textR.enabled = false;           // StationSign bouwt tekst en pictogram opnieuw op
                if (iconR) iconR.enabled = false;
                n++;
            }
            return n;
        }
    }

    [CustomEditor(typeof(StationSign))]
    [CanEditMultipleObjects]
    class StationSignInspector : UnityEditor.Editor
    {
        static readonly string[] Choices = new[] { "— kies een voorbeeldnaam —" }.Concat(StationSign.ExampleNames).ToArray();
        SerializedProperty text, showIcon;
        bool details;

        void OnEnable()
        {
            text = serializedObject.FindProperty("text");
            showIcon = serializedObject.FindProperty("showIcon");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(text, new GUIContent("Naam op het bord"));
            int pick = EditorGUILayout.Popup("Voorbeeldnaam", 0, Choices);
            if (pick > 0) text.stringValue = Choices[pick];
            var sign = (StationSign)target;
            if (!text.hasMultipleDifferentValues)
            {
                string shown = StationSign.Normalize(text.stringValue, sign.Font);
                if (string.IsNullOrEmpty(shown)) EditorGUILayout.HelpBox("Leeg bord (alleen het pictogram).", MessageType.Info);
                else if (shown != text.stringValue.Trim().ToUpperInvariant())
                    EditorGUILayout.HelpBox("Op het bord: " + shown, MessageType.None);
            }
            EditorGUILayout.PropertyField(showIcon, new GUIContent("Treinpictogram"));
            if (!sign.Font) EditorGUILayout.HelpBox("Geen bordletters gekoppeld: Conductor Chaos > Art > Station > Rebuild Station Prefabs.", MessageType.Warning);

            details = EditorGUILayout.Foldout(details, "Bordmaten (uit de generator)", true);
            if (details)
                using (new EditorGUI.IndentLevelScope())
                    DrawPropertiesExcluding(serializedObject, "m_Script", "text", "showIcon");

            if (serializedObject.ApplyModifiedProperties())
                foreach (var t in targets) ((StationSign)t).Rebuild();
        }
    }
}
