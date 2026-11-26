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
        // De grasdekken liggen iets boven de achterliggende Unity Terrain,
        // zodat geen terreintriangles zichtbaar door de klif steken.
        public const float TerrainCapClearance = 0.65f;
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
                Load(mass.desert ? (mass.stone ? "M_Env_DesertCliffStone" : "M_Env_DesertCliff") : mass.stone ? "M_Env_CliffStone" : "M_Env_Cliff"),
                Load(mass.snow ? "M_Env_SnowSoft" : mass.desert ? "M_Env_DesertSand" : "M_Env_Grass"),
                Load(mass.snow ? "M_Env_SnowSoft" : mass.desert ? "M_Env_DesertScrub" : "M_Env_Bush"),
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

        // Herstelt een bestaande Level 2-scene zonder de hele Level02Builder
        // opnieuw te draaien (die zou handmatig geplaatste props overschrijven).
        // Alleen expliciet vanuit dit menu: nooit op scene-open of automatisch.
        [MenuItem("Conductor Chaos/Art/Kloof: terrein onder geselecteerde wand herstellen")]
        public static void RepairSelectedCanyonTerrain()
        {
            var selected = Selection.activeGameObject;
            var mass = selected ? selected.GetComponentInParent<CliffMass>() : null;
            if (!mass || mass.name != "Kloofwanden" || mass.gameObject.scene.name != "ServiceLevel02")
            {
                EditorUtility.DisplayDialog("Kloof-terrein", "Open ServiceLevel02 en selecteer het object 'Kloofwanden'.", "OK");
                return;
            }
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Klif Massa] Herstel het terrein buiten Play Mode.");
                return;
            }

            Terrain terrain = null;
            foreach (var candidate in Terrain.activeTerrains)
                if (candidate && candidate.gameObject.scene == mass.gameObject.scene)
                {
                    terrain = candidate;
                    break;
                }
            if (!terrain || !terrain.terrainData)
            {
                Debug.LogWarning("[Klif Massa] Geen Terrain in dezelfde scene gevonden.", mass);
                return;
            }
            var data = terrain.terrainData;
            string assetPath = AssetDatabase.GetAssetPath(data);
            if (!assetPath.StartsWith("Assets/Scenes/ServiceLevel02_Data/"))
            {
                Debug.LogWarning("[Klif Massa] TerrainData hoort niet bij Level 2; niets aangepast: " + assetPath, terrain);
                return;
            }
            if (!EditorUtility.DisplayDialog("Kloof-terrein herstellen",
                "Het bestaande Level 2-terrein wordt onder de geschilderde klifcellen op de juiste terrashoogte gezet. " +
                "De rivier, brug, props en geschilderde cellen blijven behouden. Maak voor de zekerheid eerst een scene-backup.",
                "Herstellen", "Annuleren")) return;

            var p = terrain.transform.position;
            var size = data.size;
            int res = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, res, res);
            int changed = 0;
            float maxDelta = 0f;
            Undo.RegisterCompleteObjectUndo(data, "Kloof-terrein onder CliffMass herstellen");

            // Dezelfde rasterpunten als de Unity heightmap, maar de hoogtes komen
            // uit de al opgeslagen CliffMass-cellen; canyonlayout hoeft niet te
            // worden gereconstrueerd en de scene hoeft niet opnieuw te worden gebouwd.
            for (int z = 0; z < res; z++)
            {
                float wz = p.z + z / (float)(res - 1) * size.z;
                for (int x = 0; x < res; x++)
                {
                    float wx = p.x + x / (float)(res - 1) * size.x;
                    Vector3 local = mass.transform.InverseTransformPoint(new Vector3(wx, mass.transform.position.y, wz));
                    int tier = mass.GetLevel(mass.CellAt(local));
                    if (tier <= 0) continue;

                    float target = mass.transform.TransformPoint(
                        new Vector3(local.x, tier * mass.stepHeight - TerrainCapClearance, local.z)).y;
                    float before = p.y + heights[z, x] * size.y;
                    float after = Mathf.Clamp01((target - p.y) / size.y);
                    if (Mathf.Abs(before - (p.y + after * size.y)) < 0.06f) continue;
                    maxDelta = Mathf.Max(maxDelta, Mathf.Abs(before - target));
                    heights[z, x] = after;
                    changed++;
                }
            }
            if (changed == 0)
            {
                Debug.Log("[Klif Massa] Terrain ligt al onder de geschilderde klifdekken; niets aangepast.", mass);
                return;
            }
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
            Physics.SyncTransforms();
            Rebuild(mass);
            EditorSceneManager.MarkSceneDirty(mass.gameObject.scene);
            Debug.Log($"[Klif Massa] Bestaand level-2-terrein hersteld op {changed} heightmap-punten (max. correctie {maxDelta:0.0} m). " +
                      "Controleer de kloof in Scene View en sla de scene + TerrainData op.", mass);
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
                if (t == null || t.terrainData == null || t.gameObject.scene != tr.gameObject.scene) continue;
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
                if (h.collider.gameObject.scene != tr.gameObject.scene || self.Contains(h.collider) ||
                    h.collider.GetComponentInParent<CliffMass>() != null ||
                    h.collider.GetComponentInParent<PaintedProp>() != null) continue;      // geen props of andere scenes als grond
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
