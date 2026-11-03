// Conductor Chaos — kliffen op het terrein live bijwerken tijdens het boetseren (issue #38).
// Menu: Conductor Chaos > Art > Terrain > Add Cliffs To Selected Terrain.
// Daarna: Raise or Lower Terrain; zodra je even stopt met trekken (0,25 s), worden de kliffen opnieuw opgebouwd.
// Instellingen (hoek, facetgrootte, plooien, grasrand) staan op het object 'StylizedCliffs' onder het terrein.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class TerrainCliffsEditor
    {
        const string CliffMat = "Assets/Art/Materials/Environment/M_Env_Cliff.mat";
        const string GrassMat = "Assets/Art/Materials/Environment/M_Env_Grass.mat";
        const string ChildName = "StylizedCliffs";

        static readonly HashSet<StylizedTerrainCliffs> Pending = new HashSet<StylizedTerrainCliffs>();
        static double due;

        static TerrainCliffsEditor()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            EditorApplication.update += Tick;
        }

        static void OnHeightmapChanged(Terrain terrain, RectInt heightRegion, bool synched)
        {
            if (StylizedTerrainCliffs.IsBuilding || terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var c in terrain.GetComponentsInChildren<StylizedTerrainCliffs>())
                Pending.Add(c);
            due = EditorApplication.timeSinceStartup + 0.25;       // pas bouwen als je even stilstaat
        }

        static void Tick()
        {
            if (Pending.Count == 0 || EditorApplication.timeSinceStartup < due) return;
            var list = new List<StylizedTerrainCliffs>(Pending);
            Pending.Clear();
            foreach (var c in list)
                if (c != null && c.isActiveAndEnabled) c.Rebuild();
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Add Cliffs To Selected Terrain")]
        static void AddToSelectedMenu()
        {
            var go = Selection.activeGameObject;
            var t = go ? go.GetComponentInParent<Terrain>() : null;
            if (t == null) { Debug.LogWarning("[Terrain] Selecteer eerst een Terrain."); return; }
            AddTo(t);
            Debug.Log("[Terrain] Kliffen staan aan op " + t.name + ". Trek de grond omhoog/omlaag met Raise or Lower Terrain: " +
                      "waar het steiler wordt dan de ingestelde hoek komt een rotswand met een grasrand. Instellingen: object 'StylizedCliffs'.", t);
        }

        public static StylizedTerrainCliffs AddTo(Terrain t)
        {
            if (t.heightmapPixelError > 1f)
            {
                Undo.RecordObject(t, "Terrain pixel error");
                t.heightmapPixelError = 1f;                       // terrein volgt de hoogtekaart nauwkeurig, zodat het niet door de wand prikt
            }
            var existing = t.GetComponentInChildren<StylizedTerrainCliffs>();
            if (existing != null) { existing.Rebuild(); return existing; }
            var go = new GameObject(ChildName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(t.transform, false);
            var c = go.AddComponent<StylizedTerrainCliffs>();
            c.cliffMaterial = AssetDatabase.LoadAssetAtPath<Material>(CliffMat);
            c.grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassMat);
            if (c.cliffMaterial == null || c.grassMaterial == null)
                Debug.LogWarning("[Terrain] M_Env_Cliff of M_Env_Grass ontbreekt in Assets/Art/Materials/Environment.");
            c.Rebuild();
            Undo.RegisterCreatedObjectUndo(go, "Add terrain cliffs");
            return c;
        }
    }
}
