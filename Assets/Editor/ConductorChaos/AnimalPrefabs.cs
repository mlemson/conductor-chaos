// Conductor Chaos — prefabs voor de dieren (issue #236): Conductor Chaos > Art > Animals > Build Animal Prefabs.
//   Cow, Sheep        lopen rond (PlaygroundAnimal, Wander)
//   Cow_Blocker       koppige koe voor op het spoor (Blocker): zwaarder, zet zich harder schrap
//   Prop_HayBale      losse hooibaal (pakken en dragen) die dieren lokt (AnimalLure)
// Elk dier is een eigen netwerkobject (NetworkObject + NetworkTransform + NetworkRigidbody): zet ze gewoon in de
// scène. Botsvormen komen uit de modellen: een capsule om de romp en een bol om de kop; de poten hebben er geen,
// het dier 'staat' op een veer (zie PlaygroundAnimal).
using ConductorChaos.Animals;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    public static class AnimalPrefabs
    {
        const string ModelDir = "Assets/Art/Models/Animals";
        const string PrefabDir = "Assets/Art/Prefabs/Animals";

        [MenuItem("Conductor Chaos/Art/Animals/Build Animal Prefabs")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Art/Prefabs", "Animals");
            int made = 0;
            made += MakeAnimal("Cow", "Cow", 140f, PlaygroundAnimal.Temperament.Wander, 1.6f, 0.65f, 0.42f) ? 1 : 0;
            made += MakeAnimal("Cow", "Cow_Blocker", 180f, PlaygroundAnimal.Temperament.Blocker, 3.2f, 0.45f, 0.42f) ? 1 : 0;
            made += MakeAnimal("Sheep", "Sheep", 45f, PlaygroundAnimal.Temperament.Wander, 1.2f, 0.75f, 0.30f) ? 1 : 0;
            made += MakeHayBale() ? 1 : 0;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Animals] {made} prefabs gemaakt in {PrefabDir}.");
        }

        static bool MakeAnimal(string model, string name, float mass, PlaygroundAnimal.Temperament temperament, float brace,
            float walkSpeed, float stride)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{model}/{model}.fbx");
            if (!asset) { Debug.LogWarning($"[Animals] Model ontbreekt: {model}"); return false; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AddShape(go, "Body", capsule: true);
                AddShape(go, "Head", capsule: false);
                var body = go.AddComponent<Rigidbody>();
                body.mass = mass;
                body.linearDamping = 0.2f;
                body.angularDamping = 0.6f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                go.AddComponent<NetworkRigidbody>();
                var animal = go.AddComponent<PlaygroundAnimal>();
                var so = new SerializedObject(animal);
                so.FindProperty("temperament").enumValueIndex = (int)temperament;
                so.FindProperty("brace").floatValue = brace;
                so.FindProperty("walkSpeed").floatValue = walkSpeed;
                so.FindProperty("stride").floatValue = stride;
                if (model == "Sheep") so.FindProperty("roamRadius").floatValue = 4f;
                so.ApplyModifiedPropertiesWithoutUndo();
                go.name = name;
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Botsvorm om het mesh van knoop `node`, op de wortel (één samengesteld lijf).</summary>
        static void AddShape(GameObject root, string node, bool capsule)
        {
            Transform part = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == node) { part = t; break; }
            var filter = part ? part.GetComponent<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh) { Debug.LogWarning($"[Animals] {root.name}: geen mesh op {node}"); return; }
            var b = filter.sharedMesh.bounds;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var p = root.transform.InverseTransformPoint(part.TransformPoint(c));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 centre = (min + max) * 0.5f, size = max - min;
            if (capsule)
            {
                var col = root.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.center = centre;
                col.radius = Mathf.Min(size.x, size.y) * 0.46f;
                col.height = Mathf.Max(size.z * 0.95f, col.radius * 2f);
            }
            else
            {
                var col = root.AddComponent<SphereCollider>();
                col.center = centre;
                col.radius = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.48f;
            }
        }

        static bool MakeHayBale()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/Prop_HayBale/Prop_HayBale.fbx");
            if (!asset) return false;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                bool hasCollider = false;
                foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.sharedMesh) continue;
                    if (filter.name.StartsWith("Col_"))
                    {
                        var mr = filter.GetComponent<MeshRenderer>();
                        if (mr) mr.enabled = false;
                    }
                    else if (hasCollider) continue;
                    AddHayBaleCollider(filter.gameObject);
                    hasCollider = true;
                }
                var body = go.AddComponent<Rigidbody>();
                body.mass = 12f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                go.AddComponent<NetworkRigidbody>();
                go.AddComponent<AnimalLure>();
                go.name = "Prop_HayBale";
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Prop_HayBale.prefab");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
        internal static BoxCollider AddHayBaleCollider(GameObject target)
        {
            var box = target.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, .208f, 0f);
            box.size = new Vector3(.84f, .4f, .52f);
            return box;
        }
    }

    internal sealed class HayBaleSceneBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            // Older scenes contain the baked collider directly on the imported visual.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var lure in root.GetComponentsInChildren<AnimalLure>(true))
                    foreach (var hull in lure.GetComponentsInChildren<MeshCollider>(true))
                    {
                        if (!hull.sharedMesh || hull.sharedMesh.name != "HayBale") continue;
                        var box = AnimalPrefabs.AddHayBaleCollider(hull.gameObject);
                        box.sharedMaterial = hull.sharedMaterial;
                        Object.DestroyImmediate(hull);
                    }
        }
    }
}
