// Conductor Chaos — kliffen op het terrein live bijwerken tijdens het boetseren (issue #38).
// Kliffen komen alleen waar je schildert en het terrein steil genoeg is; tijdens een streek groeien ze live mee (ScheduleLive).
// Na elke terreinwijziging (0,25 s stil) worden ze nogmaals opgebouwd en zakken/stijgen geschilderde props mee met de grond (PropGroundSnap).
// Conductor Chaos > Art > Terrain > Auto Cliffs While Sculpting (standaard aan) doet dit ook bij Unity's penselen, zoals Raise or Lower.
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
        static readonly HashSet<StylizedTerrainCliffs> Live = new HashSet<StylizedTerrainCliffs>();
        static double liveDue;

        const string AutoKey = "ConductorChaos.TerrainCliffs.AutoLive";
        const string AutoMenu = "Conductor Chaos/Art/Terrain/Auto Cliffs While Sculpting";
        static readonly Dictionary<Terrain, RectInt> BrushRegions = new Dictionary<Terrain, RectInt>();
        static readonly HashSet<Terrain> Changed = new HashSet<Terrain>();

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
            if (Undo.isProcessing || !EditorPrefs.GetBool(AutoKey, true)) return;
            // penseelstreek (ook van Unity zelf): dit stuk mag klif worden, en de rand groeit live mee
            if (BrushRegions.TryGetValue(terrain, out var r))
            {
                int xMin = Mathf.Min(r.xMin, heightRegion.xMin), yMin = Mathf.Min(r.yMin, heightRegion.yMin);
                int xMax = Mathf.Max(r.xMax, heightRegion.xMax), yMax = Mathf.Max(r.yMax, heightRegion.yMax);
                heightRegion = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            }
            BrushRegions[terrain] = heightRegion;
        }

        /// <summary>Kliffen van dit terrein over 0,25 s opnieuw opbouwen.</summary>
        public static void Schedule(Terrain terrain)
        {
            if (terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Changed.Add(terrain);
            foreach (var c in terrain.GetComponentsInChildren<StylizedTerrainCliffs>()) Pending.Add(c);
            due = EditorApplication.timeSinceStartup + 0.25;       // pas bouwen als je even stilstaat
        }

        /// <summary>Tijdens een streek: deze kliffen zo snel mogelijk opnieuw opbouwen, zonder op stilstand te wachten.</summary>
        public static void ScheduleLive(StylizedTerrainCliffs cliffs)
        {
            if (cliffs != null) Live.Add(cliffs);
        }

        static void TickLive()
        {
            if ((Live.Count == 0 && BrushRegions.Count == 0) || EditorApplication.timeSinceStartup < liveDue) return;
            double start = EditorApplication.timeSinceStartup;
            if (BrushRegions.Count > 0)
            {
                var regions = new List<KeyValuePair<Terrain, RectInt>>(BrushRegions);
                BrushRegions.Clear();
                foreach (var kv in regions)
                {
                    if (kv.Key == null) continue;
                    var cliffs = kv.Key.GetComponentInChildren<StylizedTerrainCliffs>();
                    if (cliffs == null) cliffs = AddTo(kv.Key);
                    cliffs.MarkRegion(kv.Value, kv.Key.terrainData.heightmapResolution);
                    EditorUtility.SetDirty(cliffs);                    // masker bewaren met de scène (Unity's penselen lopen buiten onze Undo om)
                    Live.Add(cliffs);
                }
            }
            var list = new List<StylizedTerrainCliffs>(Live);
            Live.Clear();
            foreach (var c in list)
                if (c != null && c.isActiveAndEnabled) c.Rebuild();
            double took = EditorApplication.timeSinceStartup - start;
            liveDue = start + took + (took > 0.03 ? took : 0.03);  // minstens de helft van de tijd blijft voor de kwast
            SceneView.RepaintAll();
        }

        static void Tick()
        {
            TickLive();
            if ((Pending.Count == 0 && Changed.Count == 0) || EditorApplication.timeSinceStartup < due) return;
            var list = new List<StylizedTerrainCliffs>(Pending);
            Pending.Clear();
            foreach (var c in list)
                if (c != null && c.isActiveAndEnabled) c.Rebuild();
            var changed = new List<Terrain>(Changed);
            Changed.Clear();
            foreach (var t in changed)
                if (t != null) PropGroundSnap.SnapOver(t);             // geschilderde props zakken/stijgen mee met de grond
            SceneView.RepaintAll();
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
            go.SetActive(false);                                  // pas bouwen als alles is ingesteld (OnEnable bouwt meteen)
            var c = go.AddComponent<StylizedTerrainCliffs>();
            c.cliffMaterial = AssetDatabase.LoadAssetAtPath<Material>(CliffMat);
            c.grassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GrassMat);
            if (c.cliffMaterial == null || c.grassMaterial == null)
                Debug.LogWarning("[Terrain] M_Env_Cliff of M_Env_Grass ontbreekt in Assets/Art/Materials/Environment.");
            c.EnsureMask(t.terrainData.heightmapResolution);      // leeg beginnen: bestaande treden krijgen niet ineens allemaal een rand
            go.SetActive(true);
            Undo.RegisterCreatedObjectUndo(go, "Add terrain cliffs");
            return c;
        }
    }
}
