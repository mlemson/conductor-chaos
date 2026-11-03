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

        [MenuItem("Tools/Conductor Chaos/UI/Create or Open Menu Preview")]
        public static void OpenOrCreate()
        {
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
