// Conductor Chaos — Grand Whistleton: groot station uit losse modules (ArtSource/Environment/GrandStation).
// 1) Rebuild Module Prefabs: elk GS_*-model in Assets/Art/Models/Environment/GrandStation wordt een prefab in
//    Assets/Art/Prefabs/GrandStation met BoxColliders uit GrandStationKit.json (geen MeshColliders), warme puntlampen
//    op Light_*-ankers, StationSign op Sign_*-borden, static flags, GPU-instancing op de M_GS-materialen en een
//    LODGroup die kleine modules op afstand wegsnijdt.
// 2) Build Station Prefab: Station_GrandWhistleton met drie takken: Architecture (statisch, modules), Props
//    (bestaande stationsprops met hun eigen physics/netwerk) en Anchors (gameplay: spawn, briefing, wachten, ...).
//    Opstelling uit Assets/Art/Layouts/GrandStation.json. De FBX-import spiegelt x (rechts- naar linkshandig),
//    dus posities krijgen -x en draaiingen -yaw; het beeld blijft gelijk aan de bron.
// 3) Build Validation Scene: Assets/Scenes/Art/GrandStation.unity met grond, licht uit de introductie, twee sporen,
//    perron 1, een geparkeerde en een naderende trein en camera's voor de zeven controlebeelden (ShotCam_*).
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ConductorChaos.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ConductorChaos.EditorTools
{
    public static class GrandStationBuilder
    {
        public const string ModelDir = "Assets/Art/Models/Environment/GrandStation";
        public const string PrefabDir = "Assets/Art/Prefabs/GrandStation";
        public const string StationPrefabPath = PrefabDir + "/Station_GrandWhistleton.prefab";
        const string KitJson = "Assets/Art/Layouts/GrandStationKit.json";
        const string LayoutJson = "Assets/Art/Layouts/GrandStation.json";
        const string ScenePath = "Assets/Scenes/Art/GrandStation.unity";
        const string StationProps = "Assets/Art/Prefabs/Station";
        static readonly Color Warm = new(1.0f, 0.84f, 0.58f);

        [Serializable] class KitModule { public string name; public float[] boxes; }
        [Serializable] class Kit { public KitModule[] modules; }
        [Serializable] public class Placement { public string module, prop, anchor, sign, sign_back; public float[] position; public float yaw; }
        [Serializable] public class Layout { public Placement[] modules, props, anchors; }

        public static Vector3 ToUnity(float[] p) => new(-p[0], p[1], p[2]);
        public static Quaternion YawToUnity(float yaw) => Quaternion.Euler(0f, -yaw, 0f);

        // ------------------------------------------------------------------ 1) moduleprefabs
        [MenuItem("Conductor Chaos/Art/Grand Station/Rebuild Module Prefabs")]
        public static int BuildModules()
        {
            AssetDatabase.Refresh();
            var kit = JsonUtility.FromJson<Kit>(File.ReadAllText(KitJson));
            EnsureFolder(PrefabDir);
            var font = StationSignTools.GetFont();
            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                                StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
                        if (t.name.StartsWith("Light_") && !t.GetComponent<Light>()) AddLamp(t);
                    }
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    {
                        r.shadowCastingMode = ShadowCastingMode.On;
                        foreach (var m in r.sharedMaterials)
                            if (m && m.name.StartsWith("M_GS_") && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                    }
                    AddColliders(go, kit.modules.FirstOrDefault(m => m.name == name));
                    if (font) StationSignTools.Attach(go, name, font);
                    AddCulling(go);
                    go.name = name;
                    PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                    made++;
                }
                finally { Object.DestroyImmediate(go); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GrandStation] {made} moduleprefabs in {PrefabDir}.");
            return made;
        }

        static void AddLamp(Transform t)
        {
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Warm;
            string n = t.name;
            bool big = n.StartsWith("Light_Hall");
            light.range = big ? 11f : n.StartsWith("Light_Canopy") || n.StartsWith("Light_Porch") ? 6.5f : n.StartsWith("Light_Lamp") ? 7f : 5f;
            light.intensity = big ? 2.2f : 1.4f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
        }

        static void AddColliders(GameObject go, KitModule module)
        {
            if (module == null || module.boxes == null || module.boxes.Length < 8) return;
            var holder = new GameObject("Colliders");
            holder.transform.SetParent(go.transform, false);
            GameObjectUtility.SetStaticEditorFlags(holder, StaticEditorFlags.BatchingStatic);
            for (int i = 0; i + 7 < module.boxes.Length; i += 8)
            {
                var b = module.boxes;
                var center = new Vector3(-b[i], b[i + 1], b[i + 2]);           // bron is rechtshandig: x spiegelen
                var size = new Vector3(b[i + 3], b[i + 4], b[i + 5]);
                float rx = b[i + 6], rz = -b[i + 7];
                if (Mathf.Approximately(rx, 0f) && Mathf.Approximately(rz, 0f))
                {
                    var box = holder.AddComponent<BoxCollider>();
                    box.center = center;
                    box.size = size;
                    continue;
                }
                var tilted = new GameObject("Slope");
                tilted.transform.SetParent(holder.transform, false);
                tilted.transform.SetLocalPositionAndRotation(center, Quaternion.Euler(rx, 0f, rz));
                tilted.AddComponent<BoxCollider>().size = size;
            }
        }

        static void AddCulling(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.magnitude > 9f) return;                            // gebouwdelen blijven altijd zichtbaar
            var lod = go.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(0.012f, renderers) });
            lod.RecalculateBounds();
        }

        // ------------------------------------------------------------------ 2) stationsprefab
        [MenuItem("Conductor Chaos/Art/Grand Station/Build Station Prefab")]
        public static GameObject BuildStation()
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutJson));
            var root = new GameObject("Station_GrandWhistleton");
            try
            {
                var arch = Child(root.transform, "Architecture");
                var props = Child(root.transform, "Props");
                var anchors = Child(root.transform, "Anchors");
                foreach (var m in layout.modules)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{m.module}.prefab");
                    if (!prefab) { Debug.LogWarning($"[GrandStation] module ontbreekt: {m.module}"); continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, arch);
                    inst.transform.SetLocalPositionAndRotation(ToUnity(m.position), YawToUnity(m.yaw));
                    if (!string.IsNullOrEmpty(m.sign)) SetSigns(inst, m.sign, string.IsNullOrEmpty(m.sign_back) ? m.sign : m.sign_back);
                }
                foreach (var p in layout.props)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{StationProps}/{p.prop}.prefab");
                    if (!prefab) { Debug.LogWarning($"[GrandStation] prop ontbreekt: {p.prop}"); continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, props);
                    inst.transform.SetLocalPositionAndRotation(ToUnity(p.position), YawToUnity(p.yaw));
                }
                foreach (var a in layout.anchors)
                {
                    var t = Child(anchors, a.anchor);
                    t.SetLocalPositionAndRotation(ToUnity(a.position), YawToUnity(a.yaw));
                }
                CrewWardrobeSetup.DressBriefing(root.transform);
                EnsureFolder(PrefabDir);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, StationPrefabPath);
                Debug.Log($"[GrandStation] {StationPrefabPath}: {layout.modules.Length} modules, {layout.props.Length} props, {layout.anchors.Length} ankers.");
                return saved;
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void SetSigns(GameObject inst, string front, string back)
        {
            foreach (var sign in inst.GetComponentsInChildren<StationSign>(true))
            {
                sign.Text = sign.name.EndsWith("_Back") ? back : front;
                PrefabUtility.RecordPrefabInstancePropertyModifications(sign);
            }
        }

        // ------------------------------------------------------------------ 3) validatiescène
        [MenuItem("Conductor Chaos/Art/Grand Station/Build Validation Scene")]
        public static void BuildScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("[GrandStation] Sla eerst de open scène op; de validatiescène vervangt de geopende scènes.");
                    return;
                }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(StationPrefabPath)) BuildStation();
            // licht, lucht en grond overnemen uit de introductie (dezelfde sfeer als in het spel)
            var intro = EditorSceneManager.OpenScene("Assets/Scenes/ServiceIntroduction.unity", OpenSceneMode.Single);
            var copies = new List<GameObject>();
            foreach (var go in intro.GetRootGameObjects())
                if (go.name == "Ochtendzon" || go.name == "Grasland") copies.Add(Object.Instantiate(go));
            var ambient = (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor, RenderSettings.ambientLight, RenderSettings.skybox, RenderSettings.fog,
                RenderSettings.fogColor, RenderSettings.fogMode, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance,
                RenderSettings.fogDensity, RenderSettings.sun);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            foreach (var c in copies) { c.name = c.name.Replace("(Clone)", ""); SceneManager.MoveGameObjectToScene(c, scene); }
            EditorSceneManager.CloseScene(intro, true);
            SceneManager.SetActiveScene(scene);
            (RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                RenderSettings.ambientGroundColor, RenderSettings.ambientLight, RenderSettings.skybox, RenderSettings.fog,
                RenderSettings.fogColor, RenderSettings.fogMode, RenderSettings.fogStartDistance, RenderSettings.fogEndDistance,
                RenderSettings.fogDensity, _) = ambient;
            var sun = copies.FirstOrDefault(c => c.GetComponent<Light>());
            if (sun) RenderSettings.sun = sun.GetComponent<Light>();
            var grass = copies.FirstOrDefault(c => c.name == "Grasland");
            if (grass) { grass.transform.position = new Vector3(0f, -0.22f, 0f); grass.transform.localScale = new Vector3(260f, grass.transform.localScale.y, 220f); }

            var station = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StationPrefabPath));
            station.transform.position = Vector3.zero;

            // sporen 1 en 2 (zelfde stukken en draaiing als in de introductie) en perron 1 uit de perronkit
            var track = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Track/Track_Straight_12m.prefab");
            var tracks = new GameObject("Tracks").transform;
            foreach (float z in new[] { 0f, -9.3f })
                for (float x = -84f; x < 108f; x += 12f)
                    Place(track, tracks, new Vector3(x, 0f, z), Quaternion.Euler(0f, 180f, 0f));
            var platform = new GameObject("Platform 1").transform;
            var straight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Straight_6m.prefab");
            for (float x = -54f; x < 54f; x += 6f) PlaceSpan(straight, platform, x);
            var stairsLow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Stairs_End.prefab");
            var stairsHigh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Stairs_Start.prefab");
            PlaceSpanEndingAt(stairsLow, platform, -54f);
            PlaceSpan(stairsHigh, platform, 54f);

            // treinen: één geparkeerd aan perron 1, één naderend op spoor 2 (voor het machinistenbeeld)
            var trains = new GameObject("Trains").transform;
            var loco = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx");
            var tender = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Train/Tender/Tender_Blockout.fbx");
            var wagon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx");
            // maten uit de introductie: loc (29, 1.05, -1.35), tender 23.58, wagon 16.96 (draaiing 180)
            var r180 = Quaternion.Euler(0f, 180f, 0f);
            Place(loco, trains, new Vector3(26f, 1.05f, -1.35f), r180);
            Place(tender, trains, new Vector3(20.58f, 1.05f, -1.35f), r180);
            Place(wagon, trains, new Vector3(13.96f, 1.05f, -1.30f), r180);
            Place(wagon, trains, new Vector3(5.8f, 1.05f, -1.30f), r180);
            Place(wagon, trains, new Vector3(-2.36f, 1.05f, -1.30f), r180);
            var approaching = Place(loco, trains, new Vector3(50f, 1.05f, -9.3f + 1.35f), Quaternion.identity);

            // camera's voor de zeven controlebeelden (uit; de capture schakelt ze per beeld in)
            var shots = new GameObject("Shots").transform;
            Shot(shots, "ShotCam_SquareExterior", new Vector3(-14f, 4.2f, 70f), new Vector3(0f, 8f, 28f), 50f);
            Shot(shots, "ShotCam_RailSide", new Vector3(16f, 3.4f, -15.5f), new Vector3(-4f, 6.5f, 14f), 55f);
            Shot(shots, "ShotCam_Elevated", new Vector3(-58f, 46f, 78f), new Vector3(0f, 3f, 14f), 45f);
            Shot(shots, "ShotCam_HallFirstPerson", new Vector3(3.5f, 2.0f, 29.2f), new Vector3(-0.5f, 5.0f, 12f), 70f);
            Shot(shots, "ShotCam_BriefingFirstPerson", new Vector3(13.2f, 2.0f, 17.5f), new Vector3(22f, 1.7f, 22.5f), 70f);
            Shot(shots, "ShotCam_PlatformFirstPerson", new Vector3(-28f, 2.0f, 3.6f), new Vector3(10f, 2.4f, 3.0f), 65f);
            var cab = Shot(shots, "ShotCam_ApproachingTrain", approaching.transform.position + new Vector3(1.4f, 3.25f, -1.35f),
                new Vector3(0f, 4f, -6f), 62f);
            cab.transform.LookAt(new Vector3(-6f, 3.6f, -1.5f));

            EnsureFolder(Path.GetDirectoryName(ScenePath).Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[GrandStation] validatiescène opgeslagen: {ScenePath}");
        }

        // ------------------------------------------------------------------ 4) introductie
        public const string IntroScene = "Assets/Scenes/ServiceIntroduction.unity";
        const float IntroX = 30f;                         // midden van de hal langs het spoor (trein staat op x 13..33)
        static readonly string[] IntroKeep = { "Crew Spawn", "Vertrekgebied trein", "Uitgang briefing / perron", "Vertrekbel", "Belpaal", "OEFENVAK" };

        /// <summary>Vervangt het kleine hoofdstation in de introductie door Grand Whistleton: de ServiceStation-root en
        /// zijn gameplaykinderen blijven (zelfde verwijzingen), het oude gebouw gaat eruit, perron 1 wordt verlengd,
        /// spoor 2 komt erbij voor het eilandperron en de crew start in de briefingruimte.</summary>
        [MenuItem("Conductor Chaos/Art/Grand Station/Place In Introduction")]
        public static void PlaceInIntroduction()
        {
            var scene = EditorSceneManager.OpenScene(IntroScene, OpenSceneMode.Single);
            var stationRoot = Object.FindObjectsByType<ServiceStation>(FindObjectsInactive.Include)
                .FirstOrDefault(s => s.gameObject.scene == scene)?.transform;
            if (!stationRoot) { Debug.LogError("[GrandStation] geen ServiceStation in de introductie."); return; }
            if (stationRoot.Find("Station_GrandWhistleton")) { Debug.Log("[GrandStation] staat er al."); return; }

            // gameplaykinderen tijdelijk los (wereldstand blijft), oude gebouw weg, root recht en op het spoor
            // geen Transform.Find: "Uitgang briefing / perron" bevat een '/', dat Find als pad leest
            var keep = stationRoot.Cast<Transform>().Where(t => IntroKeep.Contains(t.name)).ToList();
            foreach (var t in keep) t.SetParent(null, true);
            if (PrefabUtility.IsPartOfPrefabInstance(stationRoot.gameObject))
                PrefabUtility.UnpackPrefabInstance(stationRoot.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            for (int i = stationRoot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(stationRoot.GetChild(i).gameObject);
            foreach (var c in stationRoot.GetComponents<Component>())
                if (c is MeshRenderer || c is MeshFilter || c is Collider) Object.DestroyImmediate(c);
            stationRoot.SetPositionAndRotation(new Vector3(IntroX, 0f, 0f), Quaternion.identity);
            foreach (var t in keep) t.SetParent(stationRoot, true);
            var grand = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StationPrefabPath), stationRoot);
            grand.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            // crew begint in de briefingruimte, kijkend naar de stationschef
            var spawn = stationRoot.Cast<Transform>().FirstOrDefault(t => t.name == "Crew Spawn");
            var anchor = grand.transform.Find("Anchors/Spawn_Crew");
            if (spawn && anchor) spawn.SetPositionAndRotation(anchor.position + Vector3.up * 0.06f, anchor.rotation);

            // perron 1 opnieuw leggen over de hele lengte van het station, spoor 2 erbij
            var roots = scene.GetRootGameObjects();
            foreach (var go in roots.Where(g => g.name.StartsWith("Platform_"))) Object.DestroyImmediate(go);
            var straight = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Straight_6m.prefab");
            var stairsLow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Stairs_End.prefab");
            var stairsHigh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Station/Platform/Platform_Stairs_Start.prefab");
            var platform = new GameObject("Perron 1 (Grand Whistleton)").transform;
            float x0 = IntroX - 48f, x1 = IntroX + 36f;
            for (float x = x0; x < x1 - 0.01f; x += 6f) PlaceSpan(straight, platform, x);
            PlaceSpanEndingAt(stairsLow, platform, x0);
            PlaceSpan(stairsHigh, platform, x1);
            var track = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Track/Track_Straight_12m.prefab");
            var track2 = new GameObject("Spoor 2 (eilandperron)").transform;
            for (float x = IntroX - 72f; x < IntroX + 60f; x += 12f)
                Place(track, track2, new Vector3(x, 0f, -9.3f), Quaternion.Euler(0f, 180f, 0f));

            // bomen en heuvels die in het station of op het plein staan opzij; grond doortrekken onder het plein
            var footprint = new Bounds(new Vector3(IntroX, 0f, 27f), new Vector3(100f, 40f, 84f));
            foreach (var go in roots.Where(g => g && (g.name.StartsWith("Tree_") || g.name.StartsWith("Hill_"))))
                if (footprint.Contains(new Vector3(go.transform.position.x, 0f, go.transform.position.z))) Object.DestroyImmediate(go);
            var grass = roots.FirstOrDefault(g => g && g.name == "Grasland");
            if (grass) { grass.transform.position = new Vector3(IntroX, -0.22f, 10f); grass.transform.localScale = new Vector3(240f, grass.transform.localScale.y, 190f); }
            var trees = new GameObject("Bomen rond het plein").transform;
            string[] kinds = { "Tree_Linden_01", "Tree_Oak_01", "Tree_Chestnut_01", "Tree_Maple_01" };
            int k = 0;
            foreach (float z in new[] { 42f, 52f, 62f, 72f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Models/Environment/Trees/{kinds[k++ % kinds.Length]}.fbx");
                    Place(model, trees, new Vector3(IntroX + side * (52f + (k % 3) * 4f), 0f, z), Quaternion.Euler(0f, k * 47f, 0f));
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GrandStation] introductie bijgewerkt: Grand Whistleton staat op x = " + IntroX);
        }

        static GameObject Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            if (!prefab) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.SetPositionAndRotation(position, rotation);
            return inst;
        }

        /// <summary>Zet een perronmodule zo neer dat zijn renderbereik langs x bij `start` begint.</summary>
        static void PlaceSpan(GameObject prefab, Transform parent, float start)
        {
            var inst = Place(prefab, parent, Vector3.zero, Quaternion.identity);
            if (!inst) return;
            var b = Bounds(inst);
            inst.transform.position = new Vector3(start - b.min.x, 0f, 0f);
        }

        static void PlaceSpanEndingAt(GameObject prefab, Transform parent, float end)
        {
            var inst = Place(prefab, parent, Vector3.zero, Quaternion.identity);
            if (!inst) return;
            var b = Bounds(inst);
            inst.transform.position = new Vector3(end - b.max.x, 0f, 0f);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Camera Shot(Transform parent, string name, Vector3 position, Vector3 target, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.LookAt(target);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 900f;
            cam.enabled = false;
            return cam;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
