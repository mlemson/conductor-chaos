// Conductor Chaos — bouwt de mesh + collider van een CliffMass (issue #223). Editor-only.
// De mesh wordt in de scene opgeslagen (geen los asset); oude meshes blijven bestaan zodat Undo ze terug kan zetten.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class CliffMassBuilder
    {
        const string MatDir = "Assets/Art/Materials/Environment";
        /// <summary>Ophogen als de vorm verandert: kliffen in geopende scenes worden dan vanzelf opnieuw gebouwd.</summary>
        public const int GeometryVersion = 2;      // 2 = grasbolletjes alleen langs de randen (#223)

        static CliffMassBuilder()
        {
            EditorApplication.delayCall += UpgradeOpenScenes;
            EditorSceneManager.sceneOpened += (s, m) => UpgradeOpenScenes();
        }

        static void UpgradeOpenScenes()
        {
            if (Application.isPlaying) return;
            int n = 0;
            foreach (var m in Object.FindObjectsByType<CliffMass>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (m != null && m.geometryVersion < GeometryVersion && m.CellCount > 0) { Rebuild(m); n++; }
            if (n > 0) Debug.Log($"[Klif Massa] {n} klif(fen) opnieuw gebouwd met de nieuwe vorm.");
        }

        /// <param name="reuse">tijdens een streek steeds dezelfde mesh vullen i.p.v. per stempel een nieuwe</param>
        public static void Rebuild(CliffMass mass, bool collider = true, Mesh reuse = null)
        {
            if (mass == null) return;
            var mf = mass.GetComponent<MeshFilter>();
            if (mf == null) mf = mass.gameObject.AddComponent<MeshFilter>();
            var mr = mass.GetComponent<MeshRenderer>();
            if (mr == null) mr = mass.gameObject.AddComponent<MeshRenderer>();

            var tr = mass.transform;
            var self = new HashSet<Collider>(mass.GetComponentsInChildren<Collider>());
            var data = CliffMassGeometry.Build(mass.Cells, mass.cellSize, mass.stepHeight, mass.seed, mass.tufts,
                (x, z) => GroundLocal(tr, x, z, self));

            Mesh mesh;
            if (reuse != null && mf.sharedMesh == reuse) { mesh = reuse; mesh.Clear(); }
            else mesh = new Mesh { name = "CliffMass" };
            mesh.indexFormat = data.vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(data.vertices);
            mesh.SetNormals(data.normals);
            mesh.SetColors(data.colors);
            mesh.subMeshCount = 3;
            for (int s = 0; s < 3; s++) mesh.SetTriangles(data.triangles[s], s, false);
            mesh.RecalculateBounds();
            if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;

            var mats = new[]
            {
                Load(mass.stone ? "M_Env_CliffStone" : "M_Env_Cliff"),
                Load(mass.snow ? "M_Env_SnowSoft" : "M_Env_Grass"),
                Load(mass.snow ? "M_Env_SnowSoft" : "M_Env_Bush"),
            };
            if (!SameMaterials(mr.sharedMaterials, mats)) mr.sharedMaterials = mats;

            if (collider) UpdateCollider(mass);
            mass.groundSignature = GroundSignature(mass);
            mass.geometryVersion = GeometryVersion;
            EditorUtility.SetDirty(mf);
            EditorUtility.SetDirty(mass);
            if (!Application.isPlaying && mass.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(mass.gameObject.scene);
        }

        public static void UpdateCollider(CliffMass mass)
        {
            var mf = mass.GetComponent<MeshFilter>();
            var mc = mass.GetComponent<MeshCollider>();
            if (mc == null) mc = mass.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = null;                       // forceer opnieuw 'cooken'
            mc.sharedMesh = mf != null ? mf.sharedMesh : null;
            EditorUtility.SetDirty(mc);
        }

        /// <summary>Na boetseren van het terrein: alleen opnieuw bouwen als de grond onder de klif echt veranderd is.</summary>
        public static bool RebuildIfGroundChanged(CliffMass mass)
        {
            if (mass == null || mass.CellCount == 0) return false;
            if (Mathf.Abs(GroundSignature(mass) - mass.groundSignature) < 0.01f) return false;
            Rebuild(mass);
            return true;
        }

        static float GroundSignature(CliffMass mass)
        {
            var self = new HashSet<Collider>(mass.GetComponentsInChildren<Collider>());
            float sum = 0f;
            int i = 0;
            foreach (var kv in mass.Cells)
                if ((i++ & 3) == 0)
                    sum += GroundLocal(mass.transform, kv.Key.x * mass.cellSize, kv.Key.y * mass.cellSize, self) * (1 + (i & 7));
            return sum;
        }

        // Grondhoogte (lokaal) onder een lokaal punt: Unity Terrain, anders iets anders met een collider, anders y = 0.
        static float GroundLocal(Transform tr, float x, float z, HashSet<Collider> self)
        {
            var w = tr.TransformPoint(new Vector3(x, 0f, z));
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var p = t.transform.position;
                var size = t.terrainData.size;
                if (w.x < p.x || w.z < p.z || w.x > p.x + size.x || w.z > p.z + size.z) continue;
                w.y = t.SampleHeight(w) + p.y;
                return tr.InverseTransformPoint(w).y;
            }
            float best = float.NegativeInfinity;
            foreach (var h in Physics.RaycastAll(new Vector3(w.x, tr.position.y + 50f, w.z), Vector3.down, 120f, ~0,
                                                 QueryTriggerInteraction.Ignore))
            {
                if (self.Contains(h.collider) || h.collider.GetComponentInParent<CliffMass>() != null ||
                    h.collider.GetComponentInParent<PaintedProp>() != null) continue;      // geen bomen/rotsen als grond
                best = Mathf.Max(best, h.point.y);
            }
            if (float.IsNegativeInfinity(best)) return 0f;
            w.y = best;
            return tr.InverseTransformPoint(w).y;
        }

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material Load(string name)
        {
            if (cache.TryGetValue(name, out var m) && m != null) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
            if (m == null) Debug.LogWarning($"[Klif Massa] Materiaal {MatDir}/{name}.mat niet gevonden.");
            cache[name] = m;
            return m;
        }

        static bool SameMaterials(Material[] a, Material[] b)
        {
            if (a == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
