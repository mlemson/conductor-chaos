// Conductor Chaos — collider-hulp (issue #40).
// Menu: Conductor Chaos > Tools > Fix Mesh Colliders On Rigidbodies
// Unity ondersteunt geen concave (niet-convexe) MeshCollider onder een dynamische (niet-kinematische) Rigidbody.
// Dit menu loopt de open scènes door en lost elk zo'n geval op:
//   - er zit al een andere collider op hetzelfde object  -> de overbodige MeshCollider gaat weg;
//   - de mesh is een Unity-primitief (Cube/Sphere/Capsule) -> vervangen door Box/Sphere/CapsuleCollider;
//   - anders                                               -> Convex aan.
// Siergras en bloemen verliezen hun mesh collider. Andere statische objecten blijven ongemoeid. Alles met Undo.
// Daarnaast gebeurt dit automatisch vlak voordat je op Play drukt (uit te zetten via
// Conductor Chaos > Tools > Auto-fix Mesh Colliders On Play), zodat de foutmelding niet meer terugkomt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
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
                if (ask) Debug.Log("[Colliders] Geen ongewenste mesh colliders op gras, bloemen of dynamische rigidbodies gevonden.");
                return;
            }
            var names = string.Join("\n", bad.Take(15).Select(c => "• " + Path(c.transform)));
            if (bad.Count > 15) names += $"\n… en nog {bad.Count - 15}";
            if (ask && !EditorUtility.DisplayDialog("Mesh colliders repareren",
                    $"{bad.Count} mesh collider(s) om te repareren:\n\n{names}\n\n" +
                    "Mesh colliders van gras en bloemen worden verwijderd. Onder dynamische rigidbodies worden overbodige colliders verwijderd, primitieven vervangen en overige colliders convex gemaakt.",
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

                if (IsDecorativePlantCollider(mc))
                {
                    Undo.DestroyObjectImmediate(mc);
                    removed++;
                }
                else if (hasOther)
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
                        if (IsDecorativePlantCollider(mc)) { list.Add(mc); continue; }
                        if (mc.convex) continue;
                        var rb = mc.GetComponentInParent<Rigidbody>(true);   // ook een Rigidbody hoger in de hiërarchie telt
                        if (rb != null && !rb.isKinematic) list.Add(mc);
                    }
            }
            return list;
        }

        internal static bool IsDecorativePlantMesh(Mesh mesh)
        {
            if (!mesh) return false;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!path.StartsWith("Assets/Art/Models/Environment/Plants/", StringComparison.OrdinalIgnoreCase)) return false;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("Grass_Tuft_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Flower_", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsDecorativePlantCollider(MeshCollider collider)
            => IsDecorativePlantMesh(collider.sharedMesh)
                || (collider.TryGetComponent(out MeshFilter filter) && IsDecorativePlantMesh(filter.sharedMesh));

        internal static int RemoveDecorativePlantMeshColliders(GameObject root)
        {
            int removed = 0;
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true))
            {
                if (!IsDecorativePlantCollider(collider)) continue;
                Undo.DestroyObjectImmediate(collider);
                removed++;
            }
            if (removed > 0) EditorSceneManager.MarkSceneDirty(root.scene);
            return removed;
        }

        internal static int RemoveDecorativePlantMeshCollidersInOpenScenes()
        {
            int removed = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    removed += RemoveDecorativePlantMeshColliders(root);
            }
            return removed;
        }

        static bool IsBuiltin(Mesh mesh)
        {
            var p = AssetDatabase.GetAssetPath(mesh);
            return p == "Library/unity default resources" || p.StartsWith("Resources/unity_builtin");
        }

        static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
    }
}
