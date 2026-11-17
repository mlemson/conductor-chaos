// Conductor Chaos — collider-hulp (issue #40).
// Menu: Conductor Chaos > Tools > Fix Mesh Colliders On Rigidbodies
// Unity ondersteunt geen concave (niet-convexe) MeshCollider onder een dynamische (niet-kinematische) Rigidbody.
// Dit menu loopt de open scènes door en lost elk zo'n geval op:
//   - er zit al een andere collider op hetzelfde object  -> de overbodige MeshCollider gaat weg;
//   - de mesh is een Unity-primitief (Cube/Sphere/Capsule) -> vervangen door Box/Sphere/CapsuleCollider;
//   - anders                                               -> Convex aan.
// Statische objecten (zonder Rigidbody, of met een kinematische) blijven ongemoeid. Alles met Undo.
// Daarnaast gebeurt dit automatisch vlak voordat je op Play drukt (uit te zetten via
// Conductor Chaos > Tools > Auto-fix Mesh Colliders On Play), zodat de foutmelding niet meer terugkomt.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class ColliderTools
    {
        const string AutoKey = "ConductorChaos.ColliderTools.AutoFixOnPlay";
        const string AutoMenu = "Conductor Chaos/Tools/Auto-fix Mesh Colliders On Play";

        static ColliderTools()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode && EditorPrefs.GetBool(AutoKey, true)) Fix(false);
            };
        }

        [MenuItem(AutoMenu)]
        static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, true));

        [MenuItem(AutoMenu, true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked(AutoMenu, EditorPrefs.GetBool(AutoKey, true));
            return true;
        }

        [MenuItem("Conductor Chaos/Tools/Fix Mesh Colliders On Rigidbodies")]
        public static void FixMeshCollidersOnRigidbodies() => Fix(true);

        static void Fix(bool ask)
        {
            var bad = FindProblemColliders();
            if (bad.Count == 0)
            {
                if (ask) Debug.Log("[Colliders] Geen concave MeshColliders onder dynamische Rigidbodies gevonden.");
                return;
            }
            var names = string.Join("\n", bad.Take(15).Select(c => "• " + Path(c.transform)));
            if (bad.Count > 15) names += $"\n… en nog {bad.Count - 15}";
            if (ask && !EditorUtility.DisplayDialog("Mesh colliders repareren",
                    $"{bad.Count} concave MeshCollider(s) onder een dynamische Rigidbody:\n\n{names}\n\n" +
                    "Overbodige worden verwijderd, primitieven worden Box/Sphere/Capsule, de rest wordt convex.",
                    "Repareren", "Annuleren"))
                return;

            Undo.SetCurrentGroupName("Fix Mesh Colliders On Rigidbodies");
            int group = Undo.GetCurrentGroup();
            int removed = 0, replaced = 0, convexed = 0;
            foreach (var mc in bad)
            {
                var go = mc.gameObject;
                string where = Path(go.transform);
                bool hasOther = go.GetComponents<Collider>().Any(c => c != mc && !(c is MeshCollider));
                var mesh = mc.sharedMesh;
                string prim = mesh != null && IsBuiltin(mesh) ? mesh.name : null;

                if (hasOther)
                {
                    Undo.DestroyObjectImmediate(mc);
                    removed++;
                    Debug.Log($"[Colliders] {where}: overbodige MeshCollider verwijderd (er zit al een andere collider op).", go);
                }
                else if (prim == "Cube" || prim == "Sphere" || prim == "Capsule")
                {
                    bool trigger = mc.isTrigger;
                    var mat = mc.sharedMaterial;
                    Undo.DestroyObjectImmediate(mc);
                    Collider c = prim == "Cube" ? Undo.AddComponent<BoxCollider>(go)
                               : prim == "Sphere" ? (Collider)Undo.AddComponent<SphereCollider>(go)
                               : Undo.AddComponent<CapsuleCollider>(go);
                    c.isTrigger = trigger;
                    c.sharedMaterial = mat;
                    replaced++;
                    Debug.Log($"[Colliders] {where}: MeshCollider ({prim}) vervangen door {c.GetType().Name}.", go);
                }
                else
                {
                    Undo.RecordObject(mc, "Make MeshCollider convex");
                    mc.convex = true;
                    convexed++;
                    Debug.Log($"[Colliders] {where}: MeshCollider op Convex gezet.", go);
                }
                EditorSceneManager.MarkSceneDirty(go.scene);
            }
            Undo.CollapseUndoOperations(group);
            Debug.Log($"[Colliders] Klaar: {removed} verwijderd, {replaced} vervangen, {convexed} convex. Sla de scène op om het te bewaren.");
        }

        static List<MeshCollider> FindProblemColliders()
        {
            var list = new List<MeshCollider>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
                    {
                        if (mc.convex) continue;
                        var rb = mc.GetComponentInParent<Rigidbody>(true);   // ook een Rigidbody hoger in de hiërarchie telt
                        if (rb != null && !rb.isKinematic) list.Add(mc);
                    }
            }
            return list;
        }

        static bool IsBuiltin(Mesh mesh)
        {
            var p = AssetDatabase.GetAssetPath(mesh);
            return p == "Library/unity default resources" || p.StartsWith("Resources/unity_builtin");
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }

    // Models dragged straight from the Project window bypass the generated station and track prefabs.
    internal sealed class StationPropColliders : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) => Repair(scene, withUndo: false);

        [MenuItem("Conductor Chaos/Tools/Add Missing Station Prop Colliders")]
        private static void RepairActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Missing Station Prop Colliders");
            int group = Undo.GetCurrentGroup();
            int count = Repair(scene, withUndo: true);
            Undo.CollapseUndoOperations(group);
            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Colliders] {count} botsvormen toegevoegd in {scene.name}.");
        }

        internal static int Repair(Scene scene, bool withUndo)
        {
            int count = 0;
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                var props = sceneRoot.GetComponentsInChildren<MeshFilter>(true).Where(IsAuthoredPropShape)
                    .GroupBy(shape => PrefabUtility.GetNearestPrefabInstanceRoot(shape.gameObject) ??
                        shape.transform.parent.gameObject);
                foreach (var prop in props)
                {
                    if (prop.Key.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger)) continue;
                    foreach (var shape in prop)
                    {
                        if (shape.GetComponentInParent<Rigidbody>(true)) continue;
                        if (shape.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
                        {
                            if (withUndo) Undo.RecordObject(renderer, "Hide prop collision mesh");
                            renderer.enabled = false;
                            if (withUndo) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                        }
                        var collider = withUndo ? Undo.AddComponent<MeshCollider>(shape.gameObject) :
                            shape.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh = shape.sharedMesh;
                        count++;
                    }
                }
            }
            return count;
        }

        private static bool IsAuthoredPropShape(MeshFilter shape)
        {
            if (!shape.transform.parent || !shape.sharedMesh || !shape.name.StartsWith("Col_", System.StringComparison.Ordinal))
                return false;
            string path = AssetDatabase.GetAssetPath(shape.sharedMesh);
            return path.StartsWith(StationKitPrefabs.ModelDir + "/Props/", System.StringComparison.Ordinal) ||
                path.StartsWith(StationKitPrefabs.ModelDir + "/Platform/", System.StringComparison.Ordinal) ||
                path.StartsWith(StationKitPrefabs.ModelDir + "/Station_", System.StringComparison.Ordinal) ||
                path == "Assets/Art/Models/Environment/Track/Prop_WarningSign.fbx";
        }
    }
}
