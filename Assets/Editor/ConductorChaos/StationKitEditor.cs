// Conductor Chaos — stationnetje + perronkit in Unity (issue #182).
// 1) Prefabs: van elk model in Assets/Art/Models/Environment/Station (ook Platform/ en Props/) komt een prefab in
//    Assets/Art/Prefabs/Station/... met:
//      - een MeshCollider op elke verborgen botsvorm Col_* (renderer uit), zoals de spoorkit;
//      - een warm puntlicht op elk anker Light_* (lantaarns, hanglampen in de hal);
//      - static flags op alles behalve bewegende delen (Door van het gebouw, Boom van de overweg).
//    Gebeurt automatisch als prefabs ontbreken of na het (her)importeren van de modellen;
//    opnieuw via Conductor Chaos > Art > Station > Rebuild Station Prefabs.
// 2) Conductor Chaos > Art > Station > Build Station Example Scene: perron langs drie spoorstukken, het gebouw,
//    props, trein en personages (layout Assets/Art/Layouts/StationExample.json uit ArtSource/Render/station_scenes.py).
// Plaatsing: perronmodules hebben dezelfde pivot als een spoorstuk (hart spoor, maaiveld) -> zelfde positie en
// draaiing als het spoorstuk ernaast. Het gebouw: voorgevel tegen de achterrand van het perron, 180 graden gedraaid.
// Statische art: geen netwerkstatus (multiplayer niet van toepassing).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class StationKitPrefabs
    {
        public const string ModelDir = "Assets/Art/Models/Environment/Station";
        public const string PrefabDir = "Assets/Art/Prefabs/Station";
        const string ExampleLayout = "Assets/Art/Layouts/StationExample.json";
        const string ExampleScene = "Assets/Scenes/Art/StationExample.unity";
        static readonly Color LampColor = new Color(1.0f, 0.85f, 0.55f);
        static readonly string[] Moving = { "Door", "Boom" };

        static StationKitPrefabs()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!AssetDatabase.IsValidFolder(ModelDir)) return;
                int models = AssetDatabase.FindAssets("t:Model", new[] { ModelDir }).Length;
                int prefabs = AssetDatabase.IsValidFolder(PrefabDir) ? AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }).Length : 0;
                if (models > 0 && prefabs < models) Build();
            };
        }

        internal static void BuildDeferred()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Build();
        }

        [MenuItem("Conductor Chaos/Art/Station/Rebuild Station Prefabs")]
        public static void Build()
        {
            EnsureFolder(PrefabDir);
            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var sub = Path.GetDirectoryName(path).Replace('\\', '/').Substring(ModelDir.Length).TrimStart('/');
                var outDir = string.IsNullOrEmpty(sub) ? PrefabDir : $"{PrefabDir}/{sub}";
                EnsureFolder(outDir);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.StartsWith("Col_"))
                        {
                            var mf = t.GetComponent<MeshFilter>();
                            var mr = t.GetComponent<MeshRenderer>();
                            if (mr != null) mr.enabled = false;
                            if (mf != null && mf.sharedMesh != null)
                            {
                                var col = t.gameObject.AddComponent<MeshCollider>();
                                col.sharedMesh = mf.sharedMesh;
                            }
                        }
                        else if (t.name.StartsWith("Light_") && t.GetComponent<Light>() == null)
                        {
                            var light = t.gameObject.AddComponent<Light>();
                            light.type = LightType.Point;
                            light.color = LampColor;
                            bool hall = t.name.StartsWith("Light_Hall");
                            light.range = hall ? 5.5f : 4.0f;
                            light.intensity = hall ? 1.6f : 1.2f;
                            light.shadows = LightShadows.None;
                        }
                    }
                    var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic |
                                StaticEditorFlags.ContributeGI;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        bool moving = false;
                        for (var p = t; p != null && p != go.transform.parent; p = p.parent)
                            if (Moving.Contains(p.name)) { moving = true; break; }
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? (StaticEditorFlags)0 : flags);
                    }
                    go.name = name;
                    PrefabUtility.SaveAsPrefabAsset(go, $"{outDir}/{name}.prefab");
                    made++;
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Station] {made} stationsprefabs gemaakt in {PrefabDir}. Voorbeeld: Conductor Chaos > Art > Station > Build Station Example Scene.");
        }

        [MenuItem("Conductor Chaos/Art/Station/Build Station Example Scene")]
        public static void BuildExample()
        {
            Build();
            if (AssetDatabase.FindAssets("t:Prefab", new[] { TrackKitPrefabs.PrefabDir }).Length == 0) TrackKitPrefabs.Build();
            ArtShowcaseBuilder.BuildFrom(ExampleLayout, ExampleScene, "StationExample");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>Na het (her)importeren van stationsmodellen de prefabs bijwerken.</summary>
    class StationKitPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(p => p.StartsWith(StationKitPrefabs.ModelDir) && p.EndsWith(".fbx"))) return;
            EditorApplication.delayCall -= StationKitPrefabs.BuildDeferred;
            EditorApplication.delayCall += StationKitPrefabs.BuildDeferred;
        }
    }
}
