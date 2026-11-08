using System.Linq;
using ConductorChaos.Playground;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Editor
{
    public static class PassengerSceneSetup
    {
        [MenuItem("Tools/Conductor Chaos/Place Physics Passengers")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TrainPlayground.unity" || scene.isDirty)
            {
                Debug.LogError("Open and save TrainPlayground before placing passengers; no scene changes were made.");
                return;
            }
            var player = GameObject.Find("Playground Player");
            if (!player || GameObject.Find("Playground Passenger - Elderly"))
            {
                Debug.LogError("Playground player missing or passengers already exist; no scene changes were made.");
                return;
            }
            string[] models = { "ElderlyPassenger", "Tourist", "RailMechanic" };
            foreach (string name in models)
            {
                string path = $"Assets/Art/Models/Characters/{name}/{name}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) { Debug.LogError("Passenger model missing: " + path); return; }
                if (!importer.isReadable) { importer.isReadable = true; importer.SaveAndReimport(); }
            }
            var assets = models.Select(name => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Models/Characters/{name}/{name}.fbx")).ToArray();
            if (assets.Any(asset => !asset)) { Debug.LogError("A passenger model could not be imported."); return; }
            foreach (var asset in assets)
            {
                var parts = asset.GetComponentsInChildren<Transform>(true);
                var bodyPart = parts.FirstOrDefault(part => part.name == "Body");
                if (!bodyPart || !bodyPart.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh || !filter.sharedMesh.isReadable ||
                    !parts.Any(part => part.name == "Arm_L") || !parts.Any(part => part.name == "Arm_R") ||
                    !parts.Any(part => part.name == "Leg_L") || !parts.Any(part => part.name == "Leg_R"))
                {
                    Debug.LogError("Passenger model is missing its readable body or limb anchors: " + asset.name);
                    return;
                }
            }
            for (int i = 0; i < assets.Length; i++)
            {
                var root = new GameObject("Playground Passenger - " + (i == 0 ? "Elderly" : models[i]));
                Undo.RegisterCreatedObjectUndo(root, "Place passenger");
                Vector3 place = player.transform.position + player.transform.right * 3f + player.transform.forward * (i * 1.6f - 1.6f);
                foreach (var hit in Physics.RaycastAll(place + Vector3.up * 10f, Vector3.down, 25f, ~0, QueryTriggerInteraction.Ignore).OrderBy(hit => hit.distance))
                    if (!hit.rigidbody && hit.normal.y > 0.55f) { place.y = hit.point.y + 0.03f; break; }
                root.transform.SetPositionAndRotation(place, player.transform.rotation);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(assets[i], scene);
                model.transform.SetParent(root.transform, false);
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                    if (part.name.StartsWith("Mouth_") && part.name != "Mouth_Neutral") part.gameObject.SetActive(false);
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.62f, 0f);
                capsule.height = 1.24f;
                capsule.radius = 0.22f;
                var body = root.AddComponent<Rigidbody>();
                body.mass = 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.centerOfMass = new Vector3(0f, 0.35f, 0f);
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<NetworkRigidbody>();
                root.AddComponent<PlaygroundPassenger>();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Three physical passengers placed in TrainPlayground.");
        }
    }
}
