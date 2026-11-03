// Conductor Chaos — kliffen op het terrein live bijwerken tijdens het boetseren (issue #38).
// Zodra je een terrein vormt (Cliff Painter of Unity's Raise or Lower), krijgt het vanzelf kliffen; zodra je even stopt
// met trekken (0,25 s) worden ze opnieuw opgebouwd. Uitzetten: Conductor Chaos > Art > Terrain > Auto Cliffs While Sculpting.
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

        const string AutoKey = "ConductorChaos.TerrainCliffs.Auto";
        const string AutoMenu = "Conductor Chaos/Art/Terrain/Auto Cliffs While Sculpting";
        static readonly HashSet<Terrain> NeedsCliffs = new HashSet<Terrain>();

        static TerrainCliffsEditor()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += () => { foreach (var t in Terrain.activeTerrains) Schedule(t); };
        }

        [MenuItem(AutoMenu)]
        static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, true));

        [MenuItem(AutoMenu, true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked(AutoMenu, EditorPrefs.GetBool(AutoKey, true));
            return true;
        }

        static void OnHeightmapChanged(Terrain terrain, RectInt heightRegion, bool synched)
        {
            if (StylizedTerrainCliffs.IsBuilding || terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Schedule(terrain);
        }

        /// <summary>Kliffen van dit terrein over 0,25 s opnieuw opbouwen (en ze toevoegen als ze er nog niet zijn).</summary>
        public static void Schedule(Terrain terrain)
        {
            if (terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var found = terrain.GetComponentsInChildren<StylizedTerrainCliffs>();
            if (found.Length == 0 && EditorPrefs.GetBool(AutoKey, true)) NeedsCliffs.Add(terrain);
            foreach (var c in found) Pending.Add(c);
            due = EditorApplication.timeSinceStartup + 0.25;       // pas bouwen als je even stilstaat
        }

        static void Tick()
        {
            if ((Pending.Count == 0 && NeedsCliffs.Count == 0) || EditorApplication.timeSinceStartup < due) return;
            if (NeedsCliffs.Count > 0)
            {
                var add = new List<Terrain>(NeedsCliffs);
                NeedsCliffs.Clear();
                foreach (var t in add) if (t != null) AddTo(t);
            }
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
