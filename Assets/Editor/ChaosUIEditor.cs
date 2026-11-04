using System.Linq;
using ConductorChaos.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Editor
{
    /// <summary>Creates a reviewable menu scene without changing the physics playground.</summary>
    public static class ChaosUIEditor
    {
        private const string PreviewPath = "Assets/Scenes/ChaosMenuPreview.unity";
        private const string CatalogPath = "Assets/Resources/ChaosUISpriteCatalog.asset";

        // Uses the approved source PNGs. Never copies or re-slices them.
        private static readonly string[] ApprovedIconKeys =
        {
            "accessibility", "audio", "back", "chat", "close", "coal",
            "controls", "door", "invite", "key", "lantern", "options",
            "play", "player-group", "player", "quit", "ready", "route",
            "speed-gauge", "ticket", "train", "upgrades", "warning", "wrench"
        };

        [MenuItem("Tools/Conductor Chaos/UI/Refresh Approved Icons")]
        public static void RefreshApprovedIcons()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ChaosUISpriteCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<ChaosUISpriteCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var entries = new ChaosUISpriteCatalog.Entry[ApprovedIconKeys.Length];
            int available = 0;
            for (int i = 0; i < ApprovedIconKeys.Length; i++)
            {
                string key = ApprovedIconKeys[i];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Icons/" + key + ".png");
                entries[i] = new ChaosUISpriteCatalog.Entry { key = key, sprite = sprite };
                if (sprite) available++;
                else Debug.LogWarning("Conductor Chaos UI: missing sprite " + key);
            }

            catalog.SetEntries(entries);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Conductor Chaos UI: linked " + available + "/" + entries.Length +
                      " approved sprites to " + CatalogPath);
        }

        [MenuItem("Tools/Conductor Chaos/UI/Create or Open Menu Preview")]
        public static void OpenOrCreate()
        {
            // Recover stale or missing YAML sprite references using actual imported assets.
            if (!ChaosUISpriteCatalog.Find("play")) RefreshApprovedIcons();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath))
            {
                EditorSceneManager.OpenScene(PreviewPath);
                Debug.Log("Conductor Chaos: opened existing menu preview.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraRoot = new GameObject("Menu backdrop camera");
            Camera camera = cameraRoot.AddComponent<Camera>();
            cameraRoot.AddComponent<AudioListener>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ChaosUI.Hex("#85A4EC");
            camera.fieldOfView = 52f;
            camera.transform.position = new Vector3(9f, 4.5f, 12f);
            camera.transform.LookAt(new Vector3(-1f, 1.0f, -1.0f));
            cameraRoot.tag = "MainCamera";

            GameObject sun = new GameObject("Warm menu sunlight");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = ChaosUI.Hex("#FFE3BC");
            light.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45f, -45f, 0);

            // Lightweight platform and track for a real environment rather than a floating train.
            // These are decorative editor-generated primitives with existing art materials.
            Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Materials/Environment/M_Env_Grass.mat");
            Material railMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Materials/Train/Locomotive/M_Loco_Frame.mat");
            Material sleeperMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Materials/Environment/M_Env_Trunk.mat");
            PreviewBlock("Grass plane", new Vector3(-5f, -1.36f, -1f), new Vector3(55f, 0.4f, 35f), groundMat);
            for (int i = 0; i < 24; i++)
            {
                PreviewBlock("Rail sleeper " + i,
                    new Vector3(-18f + i, -1.07f, -1.35f), new Vector3(0.60f, 0.12f, 3.1f), sleeperMat);
            }
            PreviewBlock("Rail left", new Vector3(-6f, -0.95f, -2.1f),
                new Vector3(27f, 0.12f, 0.17f), railMat);
            PreviewBlock("Rail right", new Vector3(-6f, -0.95f, -0.6f),
                new Vector3(27f, 0.12f, 0.17f), railMat);

            // Reuse already approved FBX assets, not disconnected mockup art or new gameplay objects.
            // The models remain static decorations in this preview scene.
            SpawnArt("Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx",
                "Locomotive (menu backdrop)", new Vector3(0, 0, 0));
            SpawnArt("Assets/Art/Models/Train/Tender/Tender_Blockout.fbx",
                "Tender (menu backdrop)", new Vector3(-6.4f, 0, 0));
            SpawnArt("Assets/Art/Models/Train/RegionalWagon/RegionalWagon_Blockout.fbx",
                "Passenger wagon (menu backdrop)", new Vector3(-12.0f, 0, 0));
            SpawnArt("Assets/Art/Models/Characters/Conductor/Conductor.fbx",
                "Conductor (menu backdrop)", new Vector3(-1.8f, 0.6f, 0.25f));
            SpawnArt("Assets/Art/Models/Environment/Trees/Tree_Pine_Large_01.fbx",
                "Pine A (menu backdrop)", new Vector3(4.0f, -0.5f, 7.0f));
            SpawnArt("Assets/Art/Models/Environment/Trees/Tree_Pine_Medium_01.fbx",
                "Pine B (menu backdrop)", new Vector3(-7.0f, -0.5f, 7.0f));
            SpawnArt("Assets/Art/Models/Environment/Cliffs/Hill_Grass_01.fbx",
                "Hill (menu backdrop)", new Vector3(-4.0f, -2.4f, 11.0f));

            new GameObject("Conductor Chaos | Interactive UI Preview")
                .AddComponent<ChaosMenuPreview>();
            EditorSceneManager.SaveScene(scene, PreviewPath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == PreviewPath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Concat(new[] { new EditorBuildSettingsScene(PreviewPath, true) }).ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Conductor Chaos menu preview created with existing train/environment models. " +
                      "TrainPlayground remains the first build scene. Press Play to review UI.");
        }

        private static void PreviewBlock(string name, Vector3 pos, Vector3 scale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = pos;
            block.transform.localScale = scale;
            if (material) block.GetComponent<MeshRenderer>().sharedMaterial = material;
            Collider collider = block.GetComponent<Collider>();
            if (collider) Object.DestroyImmediate(collider); // visual only
        }

        private static void SpawnArt(string path, string label, Vector3 position)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset)
            {
                Debug.LogWarning("Menu preview: optional background model missing: " + path);
                return;
            }
            GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (!instance) return;
            instance.name = label;
            instance.transform.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
