// Conductor Chaos — Cliff Painter: kliffen live op het terrein schilderen (issue #38).
// Menu: Conductor Chaos > Art > Terrain > Cliff Painter.
//   - LMB slepen          = een trede omhoog: het geschilderde stuk wordt een plateau met een klifrand eromheen.
//                           Vertrekpunt is de laagste trede onder de kwast: een plateau verbreden houdt dezelfde hoogte.
//   - Shift + LMB slepen  = een trede omlaag: kuil, sloot of ravijn met klifwanden.
//                           Vertrekpunt is de hoogste trede onder de kwast: een rivier verbreden houdt dezelfde diepte.
//   - Ctrl + LMB slepen   = vlak maken op de trede waar je begint (randen bijwerken, gaten dichten).
//   - [ en ]              = kwast kleiner/groter. Alt = camera.
// De hoogte springt per trede (standaard 2,4 m = Cliff_Pillar_Small), dus alles wat je schildert sluit netjes op elkaar aan.
// Alleen waar je met deze kwast schildert komen kliffen (klifmasker); ander boetseerwerk krijgt geen randen.
// Na elke streek wordt de hellingtextuur op het bestaande terrein vernieuwd. Geen extra rotsmesh of verhoogde grasrand.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public class CliffPainterWindow : EditorWindow
    {
        enum Mode { Raise, Lower, Flatten }

        [SerializeField] bool painting;
        [SerializeField] float radius = 3f;
        [SerializeField] float stepHeight = 2.4f;
        [SerializeField] float edgeSoftness = 1.4f;
        [SerializeField] float edgeVariation = 0.12f;
        [SerializeField] int existingSmoothPasses = 2;

        // Per muisstreek onthouden we de originele hoogte en de hoogste borsteldekking.
        // Anders maakt herhaald slepen over dezelfde rand deze alsnog steeds steiler.
        struct StrokeSample
        {
            public float initial;
            public float strength;
        }
        readonly Dictionary<int, StrokeSample> strokeSamples = new Dictionary<int, StrokeSample>();
        Vector3 lastDabPoint;
        bool hasLastDab;

        Mode strokeMode;
        float strokeTarget;
        Terrain strokeTerrain;
        StylizedTerrainCliffs strokeCliffs;
        RectInt strokeDirtyRegion;
        bool strokeRegionValid;

        [MenuItem("Conductor Chaos/Art/Terrain/Cliff Painter")]
        public static void Open() => GetWindow<CliffPainterWindow>("Cliff Painter");

        void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
        void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; strokeSamples.Clear(); hasLastDab = false; }

        void OnGUI()
        {
            var on = GUILayout.Toggle(painting, painting ? "■  Kliffen schilderen AAN  (klik om te stoppen)" : "▶  Kliffen schilderen", "Button", GUILayout.Height(30));
            if (on != painting) { painting = on; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("LMB = trede omhoog (plateau met klif)\nShift+LMB = trede omlaag (kuil/sloot)\n" +
                                    "Ctrl+LMB = vlak maken op de trede waar je begint\n[ ] = kwastgrootte · Alt = camera", MessageType.None);

            EditorGUILayout.Space();
            radius = EditorGUILayout.Slider("Kwast (m)", radius, 0.5f, 25f);
            stepHeight = EditorGUILayout.Slider(new GUIContent("Hoogte per trede (m)", "zo hoog wordt één klif"), stepHeight, 0.5f, 8f);
            edgeSoftness = EditorGUILayout.Slider(new GUIContent("Zachte hellingrand (m)", "Breedte waar de hoogte naar het plateau toe vloeiend overloopt"), edgeSoftness, 0f, 3f);
            edgeVariation = EditorGUILayout.Slider(new GUIContent("Natuurlijke rand (m)", "Kleine, vloeiende variatie zodat de rand minder geometrisch oogt"), edgeVariation, 0f, 0.4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("1,2", "halve trede"))) stepHeight = 1.2f;
                if (GUILayout.Button(new GUIContent("2,4", "Cliff_Pillar_Small / Waterfall_Small"))) stepHeight = 2.4f;
                if (GUILayout.Button(new GUIContent("3,0", "Cliff_Module_B / Waterfall_Medium"))) stepHeight = 3.0f;
                if (GUILayout.Button(new GUIContent("4,6", "Cliff_Pillar_Tall / Waterfall_Large"))) stepHeight = 4.6f;
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Een zachte kwastrand maakt nieuwe kliffen minder hoekig; de bovenkant blijft vlak. " +
                                     "De grondkleur volgt de steilte via 'StylizedCliffs'.", MessageType.Info);
            if (GUILayout.Button("Grondtextuur opnieuw berekenen (geselecteerd Terrain)"))
                RebuildSelectedTerrainSlope();
            existingSmoothPasses = EditorGUILayout.IntSlider("Bestaande rand: passes", existingSmoothPasses, 1, 4);
            if (GUILayout.Button("Verzacht bestaande klifranden (geselecteerd Terrain)"))
                SmoothExistingSelectedTerrain();
        }

        void OnSceneGUI(SceneView view)
        {
            if (!painting) return;
            var ev = Event.current;
            if (ev.alt) return;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (ev.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.LeftBracket || ev.keyCode == KeyCode.RightBracket))
            {
                radius = Mathf.Clamp(radius * (ev.keyCode == KeyCode.RightBracket ? 1.15f : 1f / 1.15f), 0.5f, 25f);
                ev.Use();
                Repaint();
            }

            var ray = HandleUtility.GUIPointToWorldRay(ev.mousePosition);
            if (!RaycastTerrain(ray, out var terrain, out var point)) return;

            var mode = GUIUtility.hotControl == id ? strokeMode : ev.shift ? Mode.Lower : (ev.control || ev.command) ? Mode.Flatten : Mode.Raise;
            Handles.color = mode == Mode.Raise ? new Color(1f, 0.85f, 0.3f, 0.95f) : mode == Mode.Lower ? new Color(0.35f, 0.65f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawWireDisc(point, Vector3.up, radius, 2f);
            Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.07f);
            Handles.DrawSolidDisc(point, Vector3.up, radius);

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                strokeTerrain = terrain;
                strokeRegionValid = false;
                strokeSamples.Clear();
                lastDabPoint = point;
                hasLastDab = true;
                strokeMode = mode;
                float step = Mathf.Max(0.1f, stepHeight);
                LevelsUnderBrush(terrain, point, step, out int low, out int high);
                int level = Mathf.RoundToInt(point.y / step);
                strokeTarget = mode == Mode.Raise ? (low + 1) * step : mode == Mode.Lower ? (high - 1) * step : level * step;
                strokeCliffs = terrain.GetComponentInChildren<StylizedTerrainCliffs>();
                if (strokeCliffs == null) strokeCliffs = TerrainCliffsEditor.AddTo(terrain);
                Undo.RegisterCompleteObjectUndo(new Object[] { terrain.terrainData, strokeCliffs }, "Cliff Painter");
                Dab(terrain, point);
                GUIUtility.hotControl = id;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && ev.button == 0 && GUIUtility.hotControl == id)
            {
                if (terrain == strokeTerrain)
                {
                    // Interpoleer stempels langs de muisbeweging; anders ontstaan gekartelde
                    // "cirkels" wanneer de muis sneller beweegt dan OnSceneGUI wordt aangeroepen.
                    if (!hasLastDab) lastDabPoint = point;
                    float distance = Vector2.Distance(new Vector2(lastDabPoint.x, lastDabPoint.z),
                                                      new Vector2(point.x, point.z));
                    float spacing = Mathf.Clamp(radius * 0.22f, 0.12f, 0.7f);
                    int samples = Mathf.Clamp(Mathf.CeilToInt(distance / spacing), 1, 128);
                    for (int sample = 1; sample <= samples; sample++)
                        Dab(terrain, Vector3.Lerp(lastDabPoint, point, sample / (float)samples));
                    lastDabPoint = point;
                    hasLastDab = true;
                }
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && GUIUtility.hotControl == id)
            {
                if (strokeTerrain != null)
                {
                    strokeTerrain.terrainData.SyncHeightmap();
                    if (strokeRegionValid) TerrainCliffsEditor.Schedule(strokeTerrain, strokeDirtyRegion);
                }
                strokeTerrain = null;
                strokeCliffs = null;
                strokeSamples.Clear();
                hasLastDab = false;
                GUIUtility.hotControl = 0;
                ev.Use();
            }
            if (ev.type == EventType.MouseMove || ev.type == EventType.MouseDrag) view.Repaint();
        }

        static bool RaycastTerrain(Ray ray, out Terrain terrain, out Vector3 point)
        {
            terrain = null;
            point = default;
            float best = float.MaxValue;
            foreach (var t in Terrain.activeTerrains)
            {
                var tc = t.GetComponent<TerrainCollider>();
                if (tc != null && tc.Raycast(ray, out var hit, 10000f) && hit.distance < best)
                {
                    best = hit.distance;
                    terrain = t;
                    point = hit.point;
                }
            }
            return terrain != null;
        }

        // laagste en hoogste trede onder de kwast (bij het begin van een streek)
        void LevelsUnderBrush(Terrain t, Vector3 world, float step, out int low, out int high)
        {
            var td = t.terrainData;
            var size = td.size;
            int res = td.heightmapResolution;
            var local = world - t.transform.position;
            float sx = (res - 1) / size.x, sz = (res - 1) / size.z;
            int cx = Mathf.RoundToInt(local.x * sx), cz = Mathf.RoundToInt(local.z * sz);
            int rx = Mathf.CeilToInt(radius * sx) + 1, rz = Mathf.CeilToInt(radius * sz) + 1;
            int x0 = Mathf.Clamp(cx - rx, 0, res - 1), x1 = Mathf.Clamp(cx + rx, 0, res - 1);
            int z0 = Mathf.Clamp(cz - rz, 0, res - 1), z1 = Mathf.Clamp(cz + rz, 0, res - 1);
            int here = Mathf.RoundToInt(world.y / step);
            low = here;
            high = here;
            var hs = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int j = 0; j <= z1 - z0; j++)
                for (int i = 0; i <= x1 - x0; i++)
                {
                    float dx = (x0 + i) / sx - local.x, dz = (z0 + j) / sz - local.z;
                    if (dx * dx + dz * dz > radius * radius) continue;
                    int lvl = Mathf.RoundToInt((hs[j, i] * size.y + t.transform.position.y) / step);
                    low = Mathf.Min(low, lvl);
                    high = Mathf.Max(high, lvl);
                }
        }

        void Dab(Terrain t, Vector3 world)
        {
            var td = t.terrainData;
            var size = td.size;
            int res = td.heightmapResolution;
            var local = world - t.transform.position;
            float sx = (res - 1) / size.x, sz = (res - 1) / size.z;
            int cx = Mathf.RoundToInt(local.x * sx), cz = Mathf.RoundToInt(local.z * sz);
            int rx = Mathf.CeilToInt(radius * sx) + 1, rz = Mathf.CeilToInt(radius * sz) + 1;
            int x0 = Mathf.Clamp(cx - rx, 0, res - 1), x1 = Mathf.Clamp(cx + rx, 0, res - 1);
            int z0 = Mathf.Clamp(cz - rz, 0, res - 1), z1 = Mathf.Clamp(cz + rz, 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            if (w <= 0 || h <= 0) return;

            var hs = td.GetHeights(x0, z0, w, h);
            float target = Mathf.Clamp01((strokeTarget - t.transform.position.y) / size.y);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float dx = (x0 + i) / sx - local.x, dz = (z0 + j) / sz - local.z;
                    float r2 = dx * dx + dz * dz;
                    if (strokeCliffs != null && r2 <= (radius + 1f / sx) * (radius + 1f / sx))
                        strokeCliffs.MarkCliff(x0 + i, z0 + j, res);
                    float worldX = (x0 + i) / sx + t.transform.position.x;
                    float worldZ = (z0 + j) / sz + t.transform.position.z;
                    float variation = (Mathf.PerlinNoise(worldX * 0.36f + 11.3f, worldZ * 0.36f + 4.7f) - 0.5f)
                                      * 2f * edgeVariation;
                    float distance = Mathf.Sqrt(r2) + variation;
                    float width = Mathf.Min(edgeSoftness, radius * 0.80f);
                    if (distance > radius) continue;
                    float strength = width < 0.001f ? 1f :
                        Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - distance) / width));
                    if (strength <= 0f) continue;
                    int key = (z0 + j) * res + x0 + i;
                    if (!strokeSamples.TryGetValue(key, out var sample))
                        sample = new StrokeSample { initial = hs[j, i], strength = 0f };
                    sample.strength = Mathf.Max(sample.strength, strength);
                    float blended = Mathf.Lerp(sample.initial, target, sample.strength);
                    hs[j, i] = strokeMode == Mode.Raise ? Mathf.Max(sample.initial, blended) :
                                strokeMode == Mode.Lower ? Mathf.Min(sample.initial, blended) : blended;
                    strokeSamples[key] = sample;
                }
            td.SetHeightsDelayLOD(x0, z0, hs);
            var region = new RectInt(x0, z0, w, h);
            if (!strokeRegionValid) { strokeDirtyRegion = region; strokeRegionValid = true; }
            else
            {
                int xmin = Mathf.Min(strokeDirtyRegion.xMin, region.xMin);
                int zmin = Mathf.Min(strokeDirtyRegion.yMin, region.yMin);
                int xmax = Mathf.Max(strokeDirtyRegion.xMax, region.xMax);
                int zmax = Mathf.Max(strokeDirtyRegion.yMax, region.yMax);
                strokeDirtyRegion = new RectInt(xmin, zmin, xmax - xmin, zmax - zmin);
            }
            TerrainCliffsEditor.Schedule(t, region); // live, na de editor-debounce
        }


        void RebuildSelectedTerrainSlope()
        {
            var selected = Selection.activeGameObject;
            var terrain = selected != null ? selected.GetComponentInParent<Terrain>() : null;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Geen Terrain geselecteerd", "Selecteer het Terrain in de Hierarchy om de hellingtextuur opnieuw te berekenen.", "OK");
                return;
            }
            var cliffs = TerrainCliffsEditor.AddTo(terrain);
            if (cliffs != null)
            {
                cliffs.Rebuild();
                SceneView.RepaintAll();
                Debug.Log("[Cliff Painter] Hellingtextuur opnieuw berekend voor " + terrain.name + ".", terrain);
            }
        }

        // Eenmalige, undo-bare correctie van bestaande hoogtekartels in de geschilderde
        // klifzone. Bewaart de grote vlakke bovenkanten: alleen punten naast een steile
        // hoogtesprong krijgen een lokale Gaussian-verzachting.
        void SmoothExistingSelectedTerrain()
        {
            var selected = Selection.activeGameObject;
            var terrain = selected != null ? selected.GetComponentInParent<Terrain>() : null;
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Geen Terrain geselecteerd", "Selecteer eerst het Terrain of het child-object StylizedCliffs in de Hierarchy.", "OK");
                return;
            }
            var cliff = terrain.GetComponentInChildren<StylizedTerrainCliffs>();
            if (cliff == null)
            {
                EditorUtility.DisplayDialog("Geen Cliff Painter-data", "Dit terrein heeft nog geen StylizedCliffs. Voeg de slope-textuur eerst toe via de Cliff Painter.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Bestaande klifranden verzachten?",
                "Deze actie verandert de hoogtekaart van het geselecteerde Terrain eenmalig. " +
                "Alleen steile punten binnen het Cliff Painter-gebied worden aangepast. " +
                "De brede vlakke bovenkanten blijven behouden. Undo (Ctrl+Z) is mogelijk.",
                "Verzachten", "Annuleren")) return;

            var td = terrain.terrainData;
            int res = td.heightmapResolution;
            var source = td.GetHeights(0, 0, res, res);
            var output = (float[,])source.Clone();
            float sx = td.size.x / Mathf.Max(1, res - 1);
            float sz = td.size.z / Mathf.Max(1, res - 1);
            int passes = Mathf.Clamp(existingSmoothPasses, 1, 4);
            bool modified = false;

            for (int pass = 0; pass < passes; pass++)
            {
                for (int z = 1; z < res - 1; z++)
                    for (int x = 1; x < res - 1; x++)
                    {
                        if (!cliff.IsPainted(x / (float)(res - 1), z / (float)(res - 1), res))
                        {
                            output[z, x] = source[z, x];
                            continue;
                        }

                        float gx = Mathf.Max(Mathf.Abs(source[z, x + 1] - source[z, x]),
                                             Mathf.Abs(source[z, x] - source[z, x - 1])) * td.size.y / sx;
                        float gz = Mathf.Max(Mathf.Abs(source[z + 1, x] - source[z, x]),
                                             Mathf.Abs(source[z, x] - source[z - 1, x])) * td.size.y / sz;
                        if (Mathf.Max(gx, gz) < 0.30f)
                        {
                            output[z, x] = source[z, x];
                            continue;
                        }
                        // Gaussiaanse 3x3-kernel; afvlakken beperkt tot de scherpe
                        // overgang, zodat plateaus niet over het hele vlak inzakken.
                        float average = (source[z - 1, x - 1] + 2f * source[z - 1, x] + source[z - 1, x + 1] +
                                         2f * source[z, x - 1] + 4f * source[z, x] + 2f * source[z, x + 1] +
                                         source[z + 1, x - 1] + 2f * source[z + 1, x] + source[z + 1, x + 1]) / 16f;
                        output[z, x] = Mathf.Lerp(source[z, x], average, 0.8f);
                        modified = true;
                    }

                var temp = source;
                source = output;
                output = temp;
            }

            if (!modified) return;
            Undo.RegisterCompleteObjectUndo(td, "Smooth painted cliff edges");
            td.SetHeights(0, 0, source);
            EditorUtility.SetDirty(td);
            TerrainCliffsEditor.Schedule(terrain);
            Debug.Log("[Cliff Painter] Bestaande geschilderde klifranden verzacht; Ctrl+Z maakt dit ongedaan.", terrain);
        }

    }
}
