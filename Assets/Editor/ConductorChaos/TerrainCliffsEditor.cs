// Conductor Chaos — native Terrain-hellingtextuur automatisch bijwerken (issue #38).
// Geen rotsmesh of graslip: enkel een gereserveerde alphamaplaag op steile hellingen.
// Debounce na hoogtekaartwijzigingen; verf alleen de gewijzigde regio en laat props volgen.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class TerrainCliffsEditor
    {
        const string SlopeLayer = "Assets/Art/Terrain/Layers/TL_CliffSlopeDirt.terrainlayer";
        const string ChildName = "StylizedCliffs";

        // null betekent: volledige repaint; anders de unie van gewijzigde heightmapgebieden.
        static readonly Dictionary<StylizedTerrainCliffs, RectInt?> Pending = new Dictionary<StylizedTerrainCliffs, RectInt?>();
        static double due;

        const string AutoKey = "ConductorChaos.TerrainCliffs.Auto";
        const string AutoMenu = "Conductor Chaos/Art/Terrain/Auto Slope Texturing While Sculpting";
        static readonly HashSet<Terrain> NeedsCliffs = new HashSet<Terrain>();
        static readonly HashSet<Terrain> Changed = new HashSet<Terrain>();

        static TerrainCliffsEditor()
        {
            TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
            EditorApplication.update += Tick;
            Undo.undoRedoPerformed += () => { foreach (var t in Terrain.activeTerrains) Schedule(t); };
        }

        [MenuItem(AutoMenu)]
        static void ToggleAuto() => EditorPrefs.SetBool(AutoKey, !EditorPrefs.GetBool(AutoKey, false));

        [MenuItem(AutoMenu, true)]
        static bool ToggleAutoValidate()
        {
            Menu.SetChecked(AutoMenu, EditorPrefs.GetBool(AutoKey, false));
            return true;
        }

        static void OnHeightmapChanged(Terrain terrain, RectInt heightRegion, bool synched)
        {
            if (StylizedTerrainCliffs.IsBuilding || terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Schedule(terrain, heightRegion);
        }

        public static void Schedule(Terrain terrain) => ScheduleInternal(terrain, null);

        public static void Schedule(Terrain terrain, RectInt changedHeightRegion) =>
            ScheduleInternal(terrain, changedHeightRegion);

        static void ScheduleInternal(Terrain terrain, RectInt? region)
        {
            if (terrain == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Changed.Add(terrain);
            var found = terrain.GetComponentsInChildren<StylizedTerrainCliffs>();
            if (found.Length == 0 && EditorPrefs.GetBool(AutoKey, false))
                NeedsCliffs.Add(terrain);
            foreach (var c in found)
            {
                if (!Pending.TryGetValue(c, out var previous))
                {
                    Pending[c] = region;
                }
                else if (!previous.HasValue || !region.HasValue)
                {
                    Pending[c] = null;
                }
                else
                {
                    var a = previous.Value;
                    var b = region.Value;
                    int xmin = Mathf.Min(a.xMin, b.xMin), zmin = Mathf.Min(a.yMin, b.yMin);
                    int xmax = Mathf.Max(a.xMax, b.xMax), zmax = Mathf.Max(a.yMax, b.yMax);
                    Pending[c] = new RectInt(xmin, zmin, xmax - xmin, zmax - zmin);
                }
            }
            due = EditorApplication.timeSinceStartup + 0.25;
        }

        static void Tick()
        {
            if ((Pending.Count == 0 && NeedsCliffs.Count == 0 && Changed.Count == 0)
                || EditorApplication.timeSinceStartup < due) return;

            if (NeedsCliffs.Count > 0)
            {
                var added = new List<Terrain>(NeedsCliffs);
                NeedsCliffs.Clear();
                foreach (var terrain in added) if (terrain != null) AddTo(terrain);
            }

            var todo = new List<KeyValuePair<StylizedTerrainCliffs, RectInt?>>(Pending);
            Pending.Clear();
            foreach (var p in todo)
            {
                if (p.Key == null || !p.Key.isActiveAndEnabled) continue;
                if (p.Value.HasValue) p.Key.Rebuild(p.Value.Value);
                else p.Key.Rebuild();
            }

            var changed = new List<Terrain>(Changed);
            Changed.Clear();
            foreach (var t in changed)
                if (t != null) PropGroundSnap.SnapOver(t);
        }

        public static StylizedTerrainCliffs AddTo(Terrain t)
        {
            if (t.heightmapPixelError > 1f)
            {
                Undo.RecordObject(t, "Terrain pixel error");
                t.heightmapPixelError = 1f;
            }

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SlopeLayer);
            if (layer == null)
            {
                Debug.LogWarning("[Terrain] Gereserveerde laag TL_CliffSlopeDirt ontbreekt. Controleer Assets/Art/Terrain/Layers.", t);
            }

            var existing = t.GetComponentInChildren<StylizedTerrainCliffs>();
            if (existing != null)
            {
                if (existing.slopeLayer == null && layer != null)
                {
                    Undo.RecordObject(existing, "Assign slope layer");
                    existing.slopeLayer = layer;
                }
                existing.Rebuild();
                return existing;
            }

            var go = new GameObject(ChildName);
            go.transform.SetParent(t.transform, false);
            var c = go.AddComponent<StylizedTerrainCliffs>();
            c.slopeLayer = layer;
            c.Rebuild();
            Undo.RegisterCreatedObjectUndo(go, "Add terrain slope texturing");
            return c;
        }
    }
}
