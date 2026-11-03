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
// De kliffen (StylizedCliffs onder het terrein) groeien live mee terwijl je schildert. Alles via Undo.
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

        Mode strokeMode;
        float strokeTarget;
        Terrain strokeTerrain;
        StylizedTerrainCliffs strokeCliffs;
        Vector3 lastDab;

        [MenuItem("Conductor Chaos/Art/Terrain/Cliff Painter")]
        public static void Open() => GetWindow<CliffPainterWindow>("Cliff Painter");

        void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
        void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

        void OnGUI()
        {
            var on = GUILayout.Toggle(painting, painting ? "■  Kliffen schilderen AAN  (klik om te stoppen)" : "▶  Kliffen schilderen", "Button", GUILayout.Height(30));
            if (on != painting) { painting = on; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("LMB = trede omhoog (plateau met klif)\nShift+LMB = trede omlaag (kuil/sloot)\n" +
                                    "Ctrl+LMB = vlak maken op de trede waar je begint\n[ ] = kwastgrootte · Alt = camera", MessageType.None);

            EditorGUILayout.Space();
            radius = EditorGUILayout.Slider("Kwast (m)", radius, 0.5f, 25f);
            stepHeight = EditorGUILayout.Slider(new GUIContent("Hoogte per trede (m)", "zo hoog wordt één klif"), stepHeight, 0.5f, 8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("1,2", "halve trede"))) stepHeight = 1.2f;
                if (GUILayout.Button(new GUIContent("2,4", "Cliff_Pillar_Small / Waterfall_Small"))) stepHeight = 2.4f;
                if (GUILayout.Button(new GUIContent("3,0", "Cliff_Module_B / Waterfall_Medium"))) stepHeight = 3.0f;
                if (GUILayout.Button(new GUIContent("4,6", "Cliff_Pillar_Tall / Waterfall_Large"))) stepHeight = 4.6f;
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Hoek, plooien en grasrand stel je in op het object 'StylizedCliffs' onder het terrein. " +
                                    "Werkt op elk Unity-terrein; een nieuw vlak terrein maak je met Create Flat Terrain.", MessageType.Info);
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
                strokeMode = mode;
                float step = Mathf.Max(0.1f, stepHeight);
                LevelsUnderBrush(terrain, point, step, out int low, out int high);
                int level = Mathf.RoundToInt(point.y / step);
                strokeTarget = mode == Mode.Raise ? (low + 1) * step : mode == Mode.Lower ? (high - 1) * step : level * step;
                strokeCliffs = terrain.GetComponentInChildren<StylizedTerrainCliffs>();
                if (strokeCliffs == null) strokeCliffs = TerrainCliffsEditor.AddTo(terrain);
                Undo.RegisterCompleteObjectUndo(new Object[] { terrain.terrainData, strokeCliffs }, "Cliff Painter");
                Dab(terrain, point);
                lastDab = point;
                GUIUtility.hotControl = id;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && ev.button == 0 && GUIUtility.hotControl == id)
            {
                if (terrain == strokeTerrain)
                {
                    // tussenliggende stappen: bij snel slepen een doorlopende rand in plaats van losse rondjes
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(lastDab, point) / Mathf.Max(0.25f, radius * 0.25f)));
                    for (int k = 1; k <= steps; k++) Dab(terrain, Vector3.Lerp(lastDab, point, k / (float)steps));
                    lastDab = point;
                }
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && GUIUtility.hotControl == id)
            {
                if (strokeTerrain != null)
                {
                    strokeTerrain.ApplyDelayedHeightmapModification();
                    TerrainCliffsEditor.Schedule(strokeTerrain);
                }
                strokeTerrain = null;
                strokeCliffs = null;
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
                    if (strokeCliffs != null && r2 <= (radius + 1f / sx) * (radius + 1f / sx)) strokeCliffs.MarkCliff(x0 + i, z0 + j, res);
                    if (r2 > radius * radius) continue;                        // harde rand: zo ontstaat een echte klif
                    float cur = hs[j, i];
                    hs[j, i] = strokeMode == Mode.Raise ? Mathf.Max(cur, target) : strokeMode == Mode.Lower ? Mathf.Min(cur, target) : target;
                }
            td.SetHeightsDelayLOD(x0, z0, hs);
            TerrainCliffsEditor.ScheduleLive(strokeCliffs);              // live: de rand groeit mee met de kwast
            TerrainCliffsEditor.Schedule(t);                             // props meenemen zodra je even stilhoudt
        }
    }
}
