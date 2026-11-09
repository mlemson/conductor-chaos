// Conductor Chaos — stationnetje + perronkit in Unity (issue #182).
// 1) Prefabs: van elk model in Assets/Art/Models/Environment/Station (ook Platform/ en Props/) komt een prefab in
//    Assets/Art/Prefabs/Station/... met:
//      - een MeshCollider op elke verborgen botsvorm Col_* (renderer uit), zoals de spoorkit;
//      - een warm puntlicht op elk anker Light_* (lantaarns, hanglampen in de hal);
//      - static flags op alles behalve bewegende deuren, slagbomen en losse meubels;
//      - StationSign op elk naambord Sign_* (naam in te typen, letters uit SignGlyphs; issue #209).
//    Gebeurt automatisch als prefabs ontbreken of na het (her)importeren van de modellen;
//    opnieuw via Conductor Chaos > Art > Station > Rebuild Station Prefabs.
// 2) Conductor Chaos > Art > Station > Build Station Example Scene: perron langs drie spoorstukken, het gebouw,
//    props, trein en personages (layout Assets/Art/Layouts/StationExample.json uit ArtSource/Render/station_scenes.py).
// Plaatsing: perronmodules hebben dezelfde pivot als een spoorstuk (hart spoor, maaiveld) -> zelfde positie en
// draaiing als het spoorstuk ernaast. Het gebouw: voorgevel tegen de achterrand van het perron, 180 graden gedraaid.
// Losse stationsprops krijgen physics en server-authoritatieve netwerksynchronisatie.
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ConductorChaos.Playground;
using Unity.Netcode;
using Unity.Netcode.Components;
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
        static readonly string[] Moving = { "Door", "Door_L", "Door_R", "Boom" };
        static readonly string[] Buildings = { "Station_Building", "Station_Main" };
        const string LoosePrefix = "Loose_";
        static readonly Dictionary<string, float> MovableMasses = new()
        {
            { "Prop_Barrel", 2f },
            { "Prop_Bench", 6f },
            { "Prop_Crate", 1f },
            { "Prop_CrateStack", 5f },
            { "Prop_LuggageCart", 3.5f },
            { "Prop_Planter", 8f },
            { "Prop_TrashBin", 1.5f },
            { "Prop_PottedPlant", 3f },
            { "Prop_Stool", 1.5f },
            { "Prop_Suitcase", 1f },
            { "Prop_Bell", 0.35f },
            { "Prop_Mug", 0.25f },
            { "Prop_Wrench", 1.2f },
            { "Prop_TicketPunch", 0.5f }
        };

        static StationKitPrefabs()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!AssetDatabase.IsValidFolder(ModelDir)) return;
                int models = AssetDatabase.FindAssets("t:Model", new[] { ModelDir }).Length;
                int prefabs = AssetDatabase.IsValidFolder(PrefabDir) ? AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }).Length : 0;
                if (models > 0 && prefabs < models) Build();
                else if (MovableMasses.Keys.Any(NeedsPhysics) || Buildings.Any(NeedsBuildingPhysics))
                    BuildPrefabs(movableOnly: true);
            };
        }

        internal static void BuildDeferred()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Build();
        }

        [MenuItem("Conductor Chaos/Art/Station/Rebuild Station Prefabs")]
        public static void Build() => BuildPrefabs(movableOnly: false);

        static bool NeedsPhysics(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Props/{name}.prefab");
            return prefab && !prefab.GetComponent<NetworkRigidbody>();
        }

        static bool NeedsBuildingPhysics(string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            return model && prefab && model.transform.Cast<Transform>().Any(child =>
                child.name.StartsWith(LoosePrefix) &&
                !(prefab.transform.Find(child.name)?.GetComponent<NetworkRigidbody>()));
        }

        static void BuildPrefabs(bool movableOnly)
        {
            EnsureFolder(PrefabDir);
            int made = 0;
            var signFont = StationSignTools.GetFont();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                var sub = Path.GetDirectoryName(path).Replace('\\', '/').Substring(ModelDir.Length).TrimStart('/');
                float mass = 0f;
                bool movable = sub == "Props" && MovableMasses.TryGetValue(name, out mass);
                if (movableOnly && !(movable && NeedsPhysics(name)) &&
                    !(Buildings.Contains(name) && NeedsBuildingPhysics(name))) continue;
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
                                col.convex = movable || t.parent.name.StartsWith(LoosePrefix);
                                col.sharedMesh = mf.sharedMesh;
                            }
                        }
                        else if (t.name.StartsWith("Light_") && t.GetComponent<Light>() == null)
                        {
                            var light = t.gameObject.AddComponent<Light>();
                            light.type = LightType.Point;
                            light.color = LampColor;
                            bool hall = t.name.StartsWith("Light_Hall") || t.name.StartsWith("Light_Pendant") ||
                                        t.name.StartsWith("Light_Fanlight");
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
                        bool moving = movable;
                        for (var p = t; !moving && p != null && p != go.transform.parent; p = p.parent)
                            if (Moving.Contains(p.name) || p.name.StartsWith(LoosePrefix)) { moving = true; break; }
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, moving ? (StaticEditorFlags)0 : flags);
                    }
                    if (movable) AddPhysics(go, name, mass, inLocalSpace: false);
                    foreach (Transform child in go.transform)
                    {
                        if (!child.name.StartsWith(LoosePrefix)) continue;
                        var prop = "Prop_" + child.name.Split('_')[1];
                        AddPhysics(child.gameObject, prop, MovableMasses[prop], inLocalSpace: true);
                    }
                    if (signFont) StationSignTools.Attach(go, name, signFont);      // Sign_*: aanpasbare naam (#209)
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

        static void AddPhysics(GameObject go, string prop, float mass, bool inLocalSpace)
        {
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (prop == "Prop_LuggageCart")
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Furniture inside a static station is synchronized as individual in-scene objects.
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>().InLocalSpace = inLocalSpace;
            go.AddComponent<NetworkRigidbody>();
            if (prop == "Prop_Wrench") go.AddComponent<RepairWrench>();
            if (prop == "Prop_TicketPunch") go.AddComponent<TicketPunch>();
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
            bool letters = imported.Any(p => p.StartsWith(StationSignTools.GlyphDir + "/") &&
                                             (p.EndsWith(".fbx") || p.EndsWith(".json")));      // niet het .asset zelf
            if (letters) EditorApplication.delayCall += () => StationSignTools.BuildFont();
            if (!letters && !imported.Any(p => p.StartsWith(StationKitPrefabs.ModelDir + "/") && p.EndsWith(".fbx"))) return;
            EditorApplication.delayCall -= StationKitPrefabs.BuildDeferred;
            EditorApplication.delayCall += StationKitPrefabs.BuildDeferred;
        }
    }
}
