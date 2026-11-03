// Conductor Chaos — collider-hulp (issue #40).
// Menu: Conductor Chaos > Tools > Fix Mesh Colliders On Rigidbodies
// Unity ondersteunt geen concave (niet-convexe) MeshCollider onder een dynamische (niet-kinematische) Rigidbody.
// Dit menu loopt de open scènes door en lost elk zo'n geval op:
//   - er zit al een andere collider op hetzelfde object  -> de overbodige MeshCollider gaat weg;
//   - de mesh is een Unity-primitief (Cube/Sphere/Capsule) -> vervangen door Box/Sphere/CapsuleCollider;
//   - anders                                               -> Convex aan.
// Statische objecten (zonder Rigidbody, of met een kinematische) blijven ongemoeid. Alles met Undo.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    public static class ColliderTools
    {
        [MenuItem("Conductor Chaos/Tools/Fix Mesh Colliders On Rigidbodies")]
        public static void FixMeshCollidersOnRigidbodies()
        {
            var bad = FindProblemColliders();
            if (bad.Count == 0)
            {
                Debug.Log("[Colliders] Geen concave MeshColliders onder dynamische Rigidbodies gevonden.");
                return;
            }
            var names = string.Join("\n", bad.Take(15).Select(c => "• " + Path(c.transform)));
            if (bad.Count > 15) names += $"\n… en nog {bad.Count - 15}";
            if (!EditorUtility.DisplayDialog("Mesh colliders repareren",
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
}
