using System.Linq;
using ConductorChaos.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Editor
{
    /// <summary>Opt-in editor setup. Does not rewrite the physics test scene.</summary>
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
            GameObject background = new GameObject("Menu preview background");
            Camera camera = background.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ChaosUI.Dark;
            background.tag = "MainCamera";

            GameObject menu = new GameObject("Conductor Chaos | Interactive UI Preview");
            menu.AddComponent<ChaosMenuPreview>();
            EditorSceneManager.SaveScene(scene, PreviewPath);
            if (!EditorBuildSettings.scenes.Any(s => s.path == PreviewPath))
            {
                EditorBuildSettings.scenes = EditorBuildSettings.scenes
                    .Concat(new[] { new EditorBuildSettingsScene(PreviewPath, true) })
                    .ToArray();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Conductor Chaos menu preview created. Press Play to use the UI. " +
                      "TrainPlayground stays build scene #0; the menu is a separate showcase.");
        }
    }
}
