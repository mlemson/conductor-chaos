// Conductor Chaos — Klif Massa Painter: grote kliffen schilderen die samensmelten tot één klif (issue #223).
// Menu: Conductor Chaos > Art > Klif Massa Painter (ook via de Prop Painter, preset Kliffen).
//   - LMB slepen          = klif schilderen. Naast/over een bestaande klif groeit díe klif mee op dezelfde hoogte:
//                           breder schilderen = één bredere klif, geen stapel losse modellen.
//   - Ctrl + LMB          = een trede hoger (stapelen/ophogen; nieuwe trede staat iets naar binnen met een grasrichel).
//   - Shift + LMB         = gummen.
//   - Shift + Ctrl + LMB  = een trede lager.
//   - [ en ]              = kwast kleiner/groter. Alt = camera.
// Raak je met een streek een tweede klif, dan worden ze samengevoegd tot één object (één mesh, één collider).
// Elke klif staat onder "PaintedProps/Cliffs", dus bomen kun je er met de Prop Painter gewoon op schilderen.
// Editor-only, statisch decor: geen gameplay- of netwerkstatus.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public class CliffMassPainterWindow : EditorWindow
    {
        enum Mode { Paint, Raise, Lower, Erase }

        [SerializeField] bool painting;
        [SerializeField] float radius = 3f;
        [SerializeField] int newTiers = 2;
        [SerializeField] float stepHeight = 2.4f;
        [SerializeField] float cellSize = 1f;
        [SerializeField] bool tufts = true;
        [SerializeField] bool stone;
        [SerializeField] bool mergeTouching = true;
        [SerializeField] string rootName = "PaintedProps";

        bool stroking;
        Mode strokeMode;
        int strokeLevel;
        CliffMass strokeMass;
        Mesh strokeMesh;
        Vector3 lastDab;
        int dabsSinceBuild;

        [MenuItem("Conductor Chaos/Art/Klif Massa Painter")]
        public static void Open() => GetWindow<CliffMassPainterWindow>("Klif Massa");

        void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
        void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

        // ---------------------------------------------------------------- UI
        void OnGUI()
        {
            var on = GUILayout.Toggle(painting, painting ? "■  Kliffen schilderen AAN  (klik om te stoppen)" : "▶  Grote kliffen schilderen", "Button", GUILayout.Height(30));
            if (on != painting) { painting = on; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("LMB = klif schilderen (groeit mee met de klif waar je begint)\n" +
                                    "Ctrl+LMB = trede hoger · Shift+LMB = gummen · Shift+Ctrl+LMB = trede lager\n" +
                                    "[ ] = kwastgrootte · Alt = camera", MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kwast", EditorStyles.boldLabel);
            radius = EditorGUILayout.Slider("Straal (m)", radius, 0.5f, 25f);
            newTiers = EditorGUILayout.IntSlider(new GUIContent("Hoogte nieuwe klif (treden)",
                "Hoe hoog een klif wordt die je op lege grond begint. Daarna: Ctrl = hoger."), newTiers, 1, 6);
            mergeTouching = EditorGUILayout.ToggleLeft(new GUIContent("Kliffen die je raakt samenvoegen",
                "Uit: elke klif blijft een eigen object (ze mogen dan wel in elkaar steken)."), mergeTouching);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Nieuwe kliffen", EditorStyles.boldLabel);
            stepHeight = EditorGUILayout.Slider(new GUIContent("Hoogte per trede (m)", "2,4 = Waterfall_Small, 3,0 = Medium, 4,6 = Large"), stepHeight, 0.8f, 6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1,2")) stepHeight = 1.2f;
                if (GUILayout.Button(new GUIContent("2,4", "Waterfall_Small"))) stepHeight = 2.4f;
                if (GUILayout.Button(new GUIContent("3,0", "Waterfall_Medium"))) stepHeight = 3.0f;
                if (GUILayout.Button(new GUIContent("4,6", "Waterfall_Large"))) stepHeight = 4.6f;
            }
            cellSize = EditorGUILayout.Slider(new GUIContent("Rastermaat (m)", "Kleiner = fijnere rand, groter = grovere blokken"), cellSize, 0.5f, 2f);
            tufts = EditorGUILayout.ToggleLeft("Grasbolletjes bovenop", tufts);
            stone = EditorGUILayout.ToggleLeft("Stenen wanden (M_Env_CliffStone)", stone);

            EditorGUILayout.Space();
            var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<CliffMass>() : null;
            using (new EditorGUI.DisabledScope(sel == null))
            {
                if (GUILayout.Button("Instellingen hierboven toepassen op geselecteerde klif"))
                {
                    RecordForRebuild(sel, "Klif Massa: instellingen");
                    sel.stepHeight = stepHeight; sel.tufts = tufts; sel.stone = stone;
                    CliffMassBuilder.Rebuild(sel);
                }
            }
            if (GUILayout.Button(new GUIContent("Alle kliffen opnieuw op de grond zetten", "na het boetseren van het terrein")))
            {
                int n = 0;
                foreach (var m in FindObjectsByType<CliffMass>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    RecordForRebuild(m, "Klif Massa: opnieuw bouwen");
                    CliffMassBuilder.Rebuild(m);
                    n++;
                }
                ShowNotification(new GUIContent($"{n} kliffen opnieuw gebouwd"));
            }
        }

        // --------------------------------------------------------- scene view
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

            if (!PointUnderMouse(ev.mousePosition, out var point)) return;
            var mode = stroking ? strokeMode : ModeFor(ev);
            var col = mode == Mode.Paint ? new Color(1f, 0.85f, 0.3f) : mode == Mode.Raise ? new Color(0.55f, 1f, 0.4f) :
                      mode == Mode.Lower ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.35f, 0.3f);
            Handles.color = new Color(col.r, col.g, col.b, 0.95f);
            Handles.DrawWireDisc(point, Vector3.up, radius, 2f);
            Handles.color = new Color(col.r, col.g, col.b, 0.07f);
            Handles.DrawSolidDisc(point, Vector3.up, radius);
            if (ev.type == EventType.Repaint)
            {
                string label = mode == Mode.Erase ? "gummen" : stroking ? $"trede {strokeLevel}" :
                               mode == Mode.Raise ? "trede hoger" : mode == Mode.Lower ? "trede lager" : "klif";
                Handles.Label(point + Vector3.up * 0.5f, label, EditorStyles.boldLabel);
            }

            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                BeginStroke(mode, point);
                GUIUtility.hotControl = id;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && ev.button == 0 && GUIUtility.hotControl == id)
            {
                if (stroking && strokeMass != null)
                {
                    // stempels langs de muisbeweging, anders gaten bij snel slepen
                    float dist = Vector2.Distance(new Vector2(lastDab.x, lastDab.z), new Vector2(point.x, point.z));
                    float spacing = Mathf.Max(0.25f, radius * 0.3f);
                    if (dist >= spacing)
                    {
                        int steps = Mathf.Clamp(Mathf.CeilToInt(dist / spacing), 1, 64);
                        for (int s = 1; s <= steps; s++) Dab(Vector3.Lerp(lastDab, point, s / (float)steps));
                        lastDab = point;
                        RebuildLive();
                    }
                }
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && GUIUtility.hotControl == id)
            {
                EndStroke();
                GUIUtility.hotControl = 0;
                ev.Use();
            }
            if (ev.type == EventType.MouseMove || ev.type == EventType.MouseDrag) view.Repaint();
        }

        static Mode ModeFor(Event ev)
        {
            bool ctrl = ev.control || ev.command;
            if (ev.shift) return ctrl ? Mode.Lower : Mode.Erase;
            return ctrl ? Mode.Raise : Mode.Paint;
        }

        bool PointUnderMouse(Vector2 mouse, out Vector3 point)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            float best = float.MaxValue;
            point = default;
            bool found = false;
            foreach (var h in Physics.RaycastAll(ray, 10000f, ~0, QueryTriggerInteraction.Ignore))
                if (h.distance < best) { best = h.distance; point = h.point; found = true; }
            if (found) return true;
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return false;
            point = ray.GetPoint(d);
            return true;
        }

        // ------------------------------------------------------------ streek
        void BeginStroke(Mode mode, Vector3 point)
        {
            Undo.IncrementCurrentGroup();
            stroking = true;
            strokeMode = mode;
            lastDab = point;
            dabsSinceBuild = 0;

            // De klif onder het midden van de kwast wint; anders de hoogste klif die de kwast raakt.
            CliffMass centerMass = null, touchMass = null;
            int centerLevel = 0, touchLevel = 0;
            foreach (var m in FindObjectsByType<CliffMass>(FindObjectsSortMode.None))
            {
                int c = m.GetLevel(m.CellAt(m.transform.InverseTransformPoint(point)));
                if (c > centerLevel) { centerMass = m; centerLevel = c; }
                int t = MaxUnderBrush(m, point);
                if (t > touchLevel) { touchMass = m; touchLevel = t; }
            }
            var target = centerMass != null ? centerMass : touchMass;
            int baseLevel = centerLevel > 0 ? centerLevel : touchLevel;

            switch (mode)
            {
                case Mode.Paint: strokeLevel = baseLevel > 0 ? baseLevel : newTiers; break;
                case Mode.Raise: strokeLevel = baseLevel > 0 ? baseLevel + 1 : newTiers; break;
                case Mode.Lower: strokeLevel = Mathf.Max(0, baseLevel - 1); break;
                default: strokeLevel = 0; break;
            }

            if (target == null)
            {
                if (mode == Mode.Erase || mode == Mode.Lower) { strokeMass = null; return; }
                target = CreateMass(point);
            }
            strokeMass = target;
            strokeMesh = null;
            RecordForRebuild(strokeMass, "Klif Massa: schilderen");
            Dab(point);
            CliffMassBuilder.Rebuild(strokeMass, collider: false);
            strokeMesh = strokeMass.GetComponent<MeshFilter>().sharedMesh;   // nieuwe mesh; de oude blijft voor Undo
        }

        void EndStroke()
        {
            stroking = false;
            if (strokeMass != null)
            {
                if (strokeMass.CellCount == 0) Undo.DestroyObjectImmediate(strokeMass.gameObject);
                else CliffMassBuilder.Rebuild(strokeMass, true, strokeMesh);      // nu ook de collider
            }
            strokeMass = null;
            strokeMesh = null;
            Undo.SetCurrentGroupName("Klif Massa Painter");
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        void RebuildLive()
        {
            if (strokeMass == null) return;
            // grote kliffen niet bij elke muisbeweging volledig opnieuw opbouwen
            dabsSinceBuild++;
            if (strokeMass.CellCount > 4000 && dabsSinceBuild < 3) return;
            dabsSinceBuild = 0;
            CliffMassBuilder.Rebuild(strokeMass, false, strokeMesh);
        }

        void Dab(Vector3 world)
        {
            var m = strokeMass;
            if (m == null) return;
            var local = m.transform.InverseTransformPoint(world);
            float cs = m.cellSize;
            float r = radius / Mathf.Max(0.01f, m.transform.lossyScale.x);
            var center = m.CellAt(local);
            int reach = Mathf.CeilToInt(r / cs) + 1;
            for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dz);
                    float ox = c.x * cs - local.x, oz = c.y * cs - local.z;
                    if (ox * ox + oz * oz > r * r) continue;
                    int cur = m.GetLevel(c);
                    switch (strokeMode)
                    {
                        case Mode.Paint:
                        case Mode.Raise: if (strokeLevel > cur) m.SetLevel(c, strokeLevel); break;
                        case Mode.Lower: if (cur > strokeLevel) m.SetLevel(c, strokeLevel); break;
                        case Mode.Erase: if (cur > 0) m.SetLevel(c, 0); break;
                    }
                }
            if (mergeTouching && (strokeMode == Mode.Paint || strokeMode == Mode.Raise))
                MergeTouching(m, world);
        }

        // Andere kliffen die de kwast raakt opnemen in de huidige klif (zelfde raster nodig).
        void MergeTouching(CliffMass into, Vector3 world)
        {
            foreach (var other in FindObjectsByType<CliffMass>(FindObjectsSortMode.None))
            {
                if (other == into || MaxUnderBrush(other, world) == 0) continue;
                if (!Compatible(into, other, out int levelShift)) continue;
                foreach (var kv in other.Cells)
                {
                    var w = other.transform.TransformPoint(new Vector3(kv.Key.x * other.cellSize, 0f, kv.Key.y * other.cellSize));
                    var c = into.CellAt(into.transform.InverseTransformPoint(w));
                    int lvl = kv.Value + levelShift;
                    if (lvl > into.GetLevel(c)) into.SetLevel(c, lvl);
                }
                Undo.DestroyObjectImmediate(other.gameObject);
            }
        }

        static bool Compatible(CliffMass a, CliffMass b, out int levelShift)
        {
            levelShift = 0;
            if (Mathf.Abs(a.cellSize - b.cellSize) > 0.001f) return false;
            if (Quaternion.Angle(a.transform.rotation, Quaternion.identity) > 0.5f ||
                Quaternion.Angle(b.transform.rotation, Quaternion.identity) > 0.5f) return false;
            if ((a.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-4f || (b.transform.lossyScale - Vector3.one).sqrMagnitude > 1e-4f) return false;
            var d = b.transform.position - a.transform.position;
            float fx = d.x / a.cellSize, fz = d.z / a.cellSize;
            if (Mathf.Abs(fx - Mathf.Round(fx)) > 0.02f || Mathf.Abs(fz - Mathf.Round(fz)) > 0.02f) return false;
            float steps = d.y / a.stepHeight;
            if (Mathf.Abs(b.stepHeight - a.stepHeight) > 0.01f || Mathf.Abs(steps - Mathf.Round(steps)) > 0.15f) return false;
            levelShift = Mathf.RoundToInt(steps);
            return true;
        }

        int MaxUnderBrush(CliffMass m, Vector3 world)
        {
            var local = m.transform.InverseTransformPoint(world);
            float cs = m.cellSize, r = radius + cs * 0.5f;
            var center = m.CellAt(local);
            int reach = Mathf.CeilToInt(r / cs) + 1, best = 0;
            for (int dz = -reach; dz <= reach; dz++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dz);
                    float ox = c.x * cs - local.x, oz = c.y * cs - local.z;
                    if (ox * ox + oz * oz <= r * r) best = Mathf.Max(best, m.GetLevel(c));
                }
            return best;
        }

        CliffMass CreateMass(Vector3 point)
        {
            var go = new GameObject("CliffMass");
            Undo.RegisterCreatedObjectUndo(go, "Klif Massa");
            go.transform.SetParent(Container(), true);
            // raster in wereldcoördinaten, zodat losse kliffen later naadloos samen te voegen zijn
            go.transform.SetPositionAndRotation(new Vector3(Mathf.Round(point.x / cellSize) * cellSize, point.y,
                                                            Mathf.Round(point.z / cellSize) * cellSize), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var m = go.AddComponent<CliffMass>();
            m.cellSize = cellSize;
            m.stepHeight = stepHeight;
            m.tufts = tufts;
            m.stone = stone;
            m.seed = Random.Range(1, 100000);
            var marker = go.AddComponent<PaintedProp>();
            marker.followGround = false;          // de klif volgt de grond zelf (CliffMassBuilder), niet als geheel
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.AddComponent<MeshCollider>();
            return m;
        }

        Transform Container()
        {
            var root = GameObject.Find(rootName);
            if (!root)
            {
                root = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(root, "Klif Massa root");
            }
            var c = root.transform.Find("Cliffs");
            if (!c)
            {
                var go = new GameObject("Cliffs");
                Undo.RegisterCreatedObjectUndo(go, "Klif Massa category");
                go.transform.SetParent(root.transform, false);
                c = go.transform;
            }
            return c;
        }

        static void RecordForRebuild(CliffMass m, string name)
        {
            if (m == null) return;
            var list = new List<Object> { m };
            var mf = m.GetComponent<MeshFilter>(); if (mf) list.Add(mf);
            var mr = m.GetComponent<MeshRenderer>(); if (mr) list.Add(mr);
            var mc = m.GetComponent<MeshCollider>(); if (mc) list.Add(mc);
            Undo.RecordObjects(list.ToArray(), name);
        }
    }
}
