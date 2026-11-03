// Conductor Chaos — Prop Painter: natuur-assets met een kwast willekeurig over de map schilderen (issue #35).
// Menu: Conductor Chaos > Art > Prop Painter.
//   - LMB slepen in de Scene view = schilderen, Shift + LMB = gummen (alleen geschilderde objecten), Alt = camera.
//   - [ en ] = kwast kleiner/groter.
//   - "Los plaatsen": elke klik zet precies één object onder de muis (geen strooien).
//   - Rotsen zakken altijd extra de grond in (percentage van hun hoogte), zodat ze er niet bovenop liggen.
//   - Colliders staan standaard aan (behalve bloemen/gras en de achtergrond); nooit op iets met een Rigidbody.
// Geschilderde objecten komen onder "PaintedProps/<categorie>" (prefab-instanties, dus aanpassingen aan het model
// komen overal door). Alles gaat via Undo. Editor-only: statisch decor, geen gameplay/netwerkstatus.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public class PropPainterWindow : EditorWindow
    {
        const string EnvRoot = "Assets/Art/Models/Environment";

        [Serializable]
        class Entry
        {
            public GameObject prefab;
            public float weight = 1f;
            public bool enabled = true;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();
        [SerializeField] bool painting;
        [SerializeField] float radius = 5f;
        [SerializeField] int perDab = 3;
        [SerializeField] float minDistance = 1.8f;
        [SerializeField] float spacing = 0.6f;              // stap tussen 'dabs' als fractie van de straal
        [SerializeField] Vector2 scaleRange = new Vector2(0.85f, 1.2f);
        [SerializeField] Vector2 heightRange = new Vector2(1f, 1f);
        [SerializeField] float yawSnap;                       // 0 = vrij, anders stappen in graden
        [SerializeField] float alignToNormal;                 // 0 = rechtop, 1 = volledig op de ondergrond
        [SerializeField] float sink = 0.05f;
        [SerializeField] bool gridSnap;
        [SerializeField] float gridSize = 2f;
        [SerializeField] bool addColliders = true;
        [SerializeField] bool singleMode;                     // één object per klik, precies onder de muis
        [SerializeField] float rockSink = 0.22f;              // rotsen: extra inzakken als fractie van hun hoogte
        [SerializeField] bool paintOnPainted = true;          // ook óp geschilderde objecten (met collider) schilderen
        [SerializeField] LayerMask surfaceMask = ~0;
        [SerializeField] string rootName = "PaintedProps";

        Vector2 scroll;
        Vector3 lastDab;
        bool hasLastDab;
        System.Random rng = new System.Random();

        [MenuItem("Conductor Chaos/Art/Prop Painter")]
        public static void Open()
        {
            var w = GetWindow<PropPainterWindow>("Prop Painter");
            if (w.entries.Count == 0) w.LoadPreset("Trees");
        }

        void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
        void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

        // ---------------------------------------------------------------- UI
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var on = GUILayout.Toggle(painting, painting ? "■  Schilderen AAN  (klik om te stoppen)" : "▶  Schilderen starten", "Button", GUILayout.Height(30));
            if (on != painting) { painting = on; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("Scene view: LMB slepen = schilderen · Shift+LMB = gummen · Alt = camera · [ ] = kwastgrootte", MessageType.None);
            singleMode = EditorGUILayout.ToggleLeft(new GUIContent("Los plaatsen (1 object per klik, onder de muis)",
                "Geen strooien: elke klik zet precies één object. Zet in het palet alleen het model aan dat je wilt."), singleMode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bomen")) LoadPreset("Trees");
                if (GUILayout.Button("Rotsen")) LoadPreset("Rocks");
                if (GUILayout.Button("Kliffen")) LoadPreset("Cliffs");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Water")) LoadPreset("Water");
                if (GUILayout.Button("Achtergrond")) LoadPreset("Background");
                if (GUILayout.Button("Bos + rotsen")) LoadPreset("Mixed");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pad (keien)")) LoadPreset("Path");
                if (GUILayout.Button("Bloemen + gras")) LoadPreset("Plants");
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kwast", EditorStyles.boldLabel);
            radius = EditorGUILayout.Slider("Straal (m)", radius, 0.5f, 60f);
            perDab = EditorGUILayout.IntSlider("Objecten per stip", perDab, 1, 30);
            minDistance = EditorGUILayout.Slider("Min. afstand (m)", minDistance, 0f, 30f);
            spacing = EditorGUILayout.Slider("Stap (× straal)", spacing, 0.1f, 2f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variatie", EditorStyles.boldLabel);
            MinMax("Schaal", ref scaleRange, 0.2f, 3f);
            MinMax("Extra hoogte (Y)", ref heightRange, 0.3f, 3f);
            yawSnap = EditorGUILayout.Slider(new GUIContent("Draaistap (°)", "0 = vrij draaien; 90 = alleen 0/90/180/270 (handig voor kliffen)"), yawSnap, 0f, 180f);
            alignToNormal = EditorGUILayout.Slider(new GUIContent("Volg ondergrond", "0 = rechtop (bomen), 1 = helemaal schuin mee (rotsen)"), alignToNormal, 0f, 1f);
            sink = EditorGUILayout.Slider(new GUIContent("Inzakken (m)", "zet objecten iets in de grond zodat er geen kier is"), sink, 0f, 1f);
            rockSink = EditorGUILayout.Slider(new GUIContent("Rotsen extra in de grond (× hoogte)", "rotsen zakken dit deel van hun hoogte extra weg"), rockSink, 0f, 0.6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                gridSnap = EditorGUILayout.ToggleLeft("Op raster", gridSnap, GUILayout.Width(90));
                using (new EditorGUI.DisabledScope(!gridSnap)) gridSize = EditorGUILayout.FloatField(gridSize);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ondergrond", EditorStyles.boldLabel);
            surfaceMask = LayerMaskField("Lagen", surfaceMask);
            addColliders = EditorGUILayout.ToggleLeft(new GUIContent("Collider toevoegen aan geschilderde objecten", "dan kun je er later bomen óp schilderen (bv. op kliffen) en lopen spelers er niet doorheen"), addColliders);
            if (GUILayout.Button(new GUIContent("Colliders op alle bestaande geschilderde objecten", "voor objecten die zonder collider zijn geschilderd (bloemen/gras uitgezonderd)")))
                AddCollidersToExisting();
            paintOnPainted = EditorGUILayout.ToggleLeft("Op geschilderde objecten schilderen", paintOnPainted);
            rootName = EditorGUILayout.TextField("Root-object", rootName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Palet ({entries.Count(e => e.enabled && e.prefab)} actief)", EditorStyles.boldLabel);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    e.enabled = EditorGUILayout.Toggle(e.enabled, GUILayout.Width(16));
                    e.prefab = (GameObject)EditorGUILayout.ObjectField(e.prefab, typeof(GameObject), false);
                    e.weight = EditorGUILayout.Slider(e.weight, 0f, 5f, GUILayout.Width(140));
                    if (GUILayout.Button("×", GUILayout.Width(22))) { entries.RemoveAt(i); i--; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Leeg vak")) entries.Add(new Entry());
                if (GUILayout.Button("+ Selectie uit Project")) AddSelection();
                if (GUILayout.Button("Leegmaken")) entries.Clear();
            }
            DropArea();

            EditorGUILayout.Space();
            if (GUILayout.Button("Alle geschilderde objecten verwijderen"))
            {
                var root = GameObject.Find(rootName);
                if (root && EditorUtility.DisplayDialog("Prop Painter", $"Alles onder '{rootName}' verwijderen?", "Verwijderen", "Annuleren"))
                    Undo.DestroyObjectImmediate(root);
            }
            EditorGUILayout.EndScrollView();
        }

        static void MinMax(string label, ref Vector2 v, float lo, float hi)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                v.x = EditorGUILayout.FloatField(v.x, GUILayout.Width(44));
                EditorGUILayout.MinMaxSlider(ref v.x, ref v.y, lo, hi);
                v.y = EditorGUILayout.FloatField(v.y, GUILayout.Width(44));
            }
        }

        static LayerMask LayerMaskField(string label, LayerMask mask)
        {
            var names = Enumerable.Range(0, 32).Select(LayerMask.LayerToName).ToArray();
            var shown = names.Where(n => !string.IsNullOrEmpty(n)).ToArray();
            int compact = 0;
            for (int i = 0; i < shown.Length; i++)
                if ((mask.value & (1 << LayerMask.NameToLayer(shown[i]))) != 0) compact |= 1 << i;
            compact = EditorGUILayout.MaskField(label, compact, shown);
            int full = 0;
            for (int i = 0; i < shown.Length; i++)
                if ((compact & (1 << i)) != 0) full |= 1 << LayerMask.NameToLayer(shown[i]);
            if (compact == -1) full = ~0;
            return full;
        }

        void AddSelection()
        {
            foreach (var o in Selection.objects.OfType<GameObject>())
                if (EditorUtility.IsPersistent(o) && entries.All(e => e.prefab != o)) entries.Add(new Entry { prefab = o });
        }

        void DropArea()
        {
            var r = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            GUI.Box(r, "Sleep modellen/prefabs hierheen", EditorStyles.helpBox);
            var ev = Event.current;
            if (!r.Contains(ev.mousePosition)) return;
            if (ev.type == EventType.DragUpdated) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; ev.Use(); }
            else if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (var o in DragAndDrop.objectReferences.OfType<GameObject>())
                    if (EditorUtility.IsPersistent(o) && entries.All(e => e.prefab != o)) entries.Add(new Entry { prefab = o });
                ev.Use();
            }
        }

        // ------------------------------------------------------------ presets
        void LoadPreset(string preset)
        {
            entries.Clear();
            void Add(string folder, string filter = null, float weight = 1f)
            {
                var dir = $"{EnvRoot}/{folder}";
                if (!AssetDatabase.IsValidFolder(dir)) return;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (filter != null && !Path.GetFileName(path).Contains(filter)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go && entries.All(e => e.prefab != go)) entries.Add(new Entry { prefab = go, weight = weight });
                }
            }
            switch (preset)
            {
                case "Trees":
                    Add("Trees", "Tree_Pine"); Add("Trees", "Bush", 0.6f);
                    entries.RemoveAll(e => e.prefab.name.Contains("Background"));
                    Set(radius: 6, per: 3, dist: 1.8f, scale: new Vector2(0.8f, 1.25f), height: new Vector2(0.9f, 1.2f), yaw: 0, align: 0f, sink: 0.08f, colliders: true);
                    break;
                case "Rocks":
                    Add("Rocks");                                   // alleen rotsen (struiken zitten in 'Bos + rotsen')
                    Set(radius: 4, per: 3, dist: 1.0f, scale: new Vector2(0.6f, 1.5f), height: new Vector2(0.8f, 1.2f), yaw: 0, align: 0.6f, sink: 0.05f, colliders: true);
                    break;
                case "Cliffs":
                    Add("Cliffs");
                    entries.RemoveAll(e => e.prefab.name.Contains("Hill"));
                    foreach (var e in entries) e.weight = e.prefab.name.Contains("Big") ? 0.4f : e.prefab.name.Contains("Cluster") ? 0.8f : 1f;
                    Set(radius: 6, per: 1, dist: 3.0f, scale: new Vector2(0.9f, 1.15f), height: new Vector2(0.8f, 1.4f), yaw: 90, align: 0f, sink: 0.2f, colliders: true);
                    break;
                case "Water":
                    Add("Water");
                    // zelfde inzakken als de kliffen en geen schaalvariatie: dan valt de stroom precies op de kliftop
                    // (Waterfall_Small = 2,4 m: Cliff_Pillar_Small; Medium = 3,0 m: Cliff_Module_B; Large = 4,6 m: Cliff_Pillar_Tall)
                    Set(radius: 1, per: 1, dist: 4f, scale: new Vector2(1f, 1f), height: new Vector2(1f, 1f), yaw: 0, align: 0f, sink: 0.2f, colliders: false);
                    singleMode = true;
                    break;
                case "Background":
                    Add("Mountains"); Add("Trees", "Background", 1.5f);
                    Set(radius: 40, per: 2, dist: 25f, scale: new Vector2(0.8f, 1.5f), height: new Vector2(0.8f, 1.3f), yaw: 0, align: 0f, sink: 1f, colliders: false);
                    break;
                case "Path":
                    Add("Paths", "Stone_Single");
                    Set(radius: 0.9f, per: 2, dist: 0.55f, scale: new Vector2(0.75f, 1.25f), height: new Vector2(0.8f, 1.3f), yaw: 0, align: 1f, sink: 0.03f, colliders: true);
                    spacing = 0.5f;
                    break;
                case "Plants":
                    Add("Plants", "Grass_Tuft", 1.5f); Add("Plants", "Flower", 1f);
                    Set(radius: 3, per: 6, dist: 0.35f, scale: new Vector2(0.8f, 1.3f), height: new Vector2(0.8f, 1.3f), yaw: 0, align: 0.5f, sink: 0.02f, colliders: false);
                    break;
                case "Mixed":
                    Add("Trees", "Tree_Pine", 1.2f); Add("Trees", "Bush", 0.7f); Add("Rocks", null, 0.5f); Add("Plants", "Grass_Tuft", 0.6f);
                    entries.RemoveAll(e => e.prefab.name.Contains("Background"));
                    Set(radius: 8, per: 4, dist: 1.6f, scale: new Vector2(0.75f, 1.3f), height: new Vector2(0.9f, 1.2f), yaw: 0, align: 0.2f, sink: 0.08f, colliders: true);
                    break;
            }
            Repaint();
        }

        void Set(float radius, int per, float dist, Vector2 scale, Vector2 height, float yaw, float align, float sink, bool colliders)
        {
            singleMode = false;
            this.radius = radius; perDab = per; minDistance = dist; scaleRange = scale; heightRange = height;
            yawSnap = yaw; alignToNormal = align; this.sink = sink; addColliders = colliders;
        }

        // --------------------------------------------------------- scene view
        void OnSceneGUI(SceneView view)
        {
            if (!painting) return;
            var ev = Event.current;
            if (ev.alt) return;                                   // camera-navigatie
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (ev.type == EventType.Layout) HandleUtility.AddDefaultControl(id);

            if (ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.LeftBracket || ev.keyCode == KeyCode.RightBracket))
            {
                radius = Mathf.Clamp(radius * (ev.keyCode == KeyCode.RightBracket ? 1.15f : 1f / 1.15f), 0.5f, 60f);
                ev.Use(); Repaint();
            }

            if (!SurfaceUnderMouse(ev.mousePosition, null, out var hit)) return;
            bool erase = ev.shift;
            Handles.color = erase ? new Color(1f, 0.35f, 0.3f, 0.9f) : new Color(1f, 0.85f, 0.3f, 0.9f);
            Handles.DrawWireDisc(hit.point, hit.normal, singleMode && !erase ? 0.4f : radius, 2f);
            Handles.color = erase ? new Color(1f, 0.35f, 0.3f, 0.08f) : new Color(1f, 0.85f, 0.3f, 0.06f);
            Handles.DrawSolidDisc(hit.point, hit.normal, singleMode && !erase ? 0.4f : radius);

            if ((ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0)
            {
                if (ev.type == EventType.MouseDown) { Undo.IncrementCurrentGroup(); hasLastDab = false; }
                if (singleMode && !erase)
                {
                    if (ev.type == EventType.MouseDown) PlaceOne(hit, null);
                }
                else if (!hasLastDab || Vector3.Distance(lastDab, hit.point) >= radius * spacing)
                {
                    if (erase) Erase(hit.point); else Dab(hit.point);
                    lastDab = hit.point;
                    hasLastDab = true;
                }
                GUIUtility.hotControl = id;
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0)
            {
                Undo.SetCurrentGroupName(erase ? "Prop Painter: gummen" : "Prop Painter: schilderen");
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                hasLastDab = false;
                GUIUtility.hotControl = 0;
                ev.Use();
            }
            if (ev.type == EventType.MouseMove || ev.type == EventType.MouseDrag) view.Repaint();
        }

        bool SurfaceUnderMouse(Vector2 mouse, HashSet<GameObject> ignore, out RaycastHit hit)
        {
            var ray = HandleUtility.GUIPointToWorldRay(mouse);
            if (Raycast(ray, ignore, out hit)) return true;
            var plane = new Plane(Vector3.up, Vector3.zero);           // geen collider? dan het grondvlak y = 0
            if (plane.Raycast(ray, out float d))
            {
                hit = new RaycastHit { point = ray.GetPoint(d), normal = Vector3.up };
                return true;
            }
            return false;
        }

        bool Raycast(Ray ray, HashSet<GameObject> ignore, out RaycastHit best)
        {
            best = default;
            var root = GameObject.Find(rootName);
            float bestDist = float.MaxValue;
            bool found = false;
            foreach (var h in Physics.RaycastAll(ray, 10000f, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                var go = h.collider.gameObject;
                if (ignore != null && ignore.Any(g => g && go.transform.IsChildOf(g.transform))) continue;
                if (!paintOnPainted && root && go.transform.IsChildOf(root.transform)) continue;
                if (h.distance < bestDist) { best = h; bestDist = h.distance; found = true; }
            }
            return found;
        }

        Entry PickEntry()
        {
            var live = entries.Where(e => e.enabled && e.prefab && e.weight > 0f).ToList();
            if (live.Count == 0) return null;
            double total = live.Sum(e => e.weight), r = rng.NextDouble() * total;
            foreach (var e in live) { r -= e.weight; if (r <= 0) return e; }
            return live[live.Count - 1];
        }

        static string CategoryOf(GameObject prefab)
        {
            var path = AssetDatabase.GetAssetPath(prefab).Replace('\\', '/');
            if (path.StartsWith(EnvRoot + "/"))
            {
                var parts = path.Substring(EnvRoot.Length + 1).Split('/');
                if (parts.Length > 1) return parts[0];
            }
            return "Props";
        }

        Transform Container(string category)
        {
            var root = GameObject.Find(rootName);
            if (!root)
            {
                root = new GameObject(rootName);
                Undo.RegisterCreatedObjectUndo(root, "Prop Painter root");
            }
            var c = root.transform.Find(category);
            if (!c)
            {
                var go = new GameObject(category);
                Undo.RegisterCreatedObjectUndo(go, "Prop Painter category");
                go.transform.SetParent(root.transform, false);
                c = go.transform;
            }
            return c;
        }

        float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        void Dab(Vector3 center)
        {
            if (entries.All(e => !e.enabled || !e.prefab)) { ShowNotification(new GUIContent("Palet is leeg")); return; }
            var root = GameObject.Find(rootName);
            var existing = new List<Vector3>();
            if (root)
                foreach (Transform cat in root.transform)
                    foreach (Transform t in cat)
                        if ((t.position - center).sqrMagnitude < (radius + minDistance) * (radius + minDistance)) existing.Add(t.position);

            var created = new HashSet<GameObject>();
            int placed = 0;
            for (int attempt = 0; attempt < perDab * 8 && placed < perDab; attempt++)
            {
                var a = Rand(0f, Mathf.PI * 2f);
                var r = radius * Mathf.Sqrt((float)rng.NextDouble());
                var p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (gridSnap && gridSize > 0.01f)
                    p = new Vector3(Mathf.Round(p.x / gridSize) * gridSize, p.y, Mathf.Round(p.z / gridSize) * gridSize);
                var down = new Ray(p + Vector3.up * 200f, Vector3.down);
                RaycastHit hit;
                if (!Raycast(down, created, out hit))
                {
                    var plane = new Plane(Vector3.up, new Vector3(0f, center.y, 0f));
                    if (!plane.Raycast(down, out float d)) continue;
                    hit = new RaycastHit { point = down.GetPoint(d), normal = Vector3.up };
                }
                var flat = new Vector2(hit.point.x, hit.point.z);
                if (minDistance > 0f && existing.Any(q => (new Vector2(q.x, q.z) - flat).sqrMagnitude < minDistance * minDistance)) continue;

                var go = PlaceOne(hit, created);
                if (go == null) break;
                created.Add(go);
                existing.Add(go.transform.position);
                placed++;
            }
        }

        GameObject PlaceOne(RaycastHit hit, HashSet<GameObject> created)
        {
            var entry = PickEntry();
            if (entry == null) { ShowNotification(new GUIContent("Palet is leeg")); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
            Undo.RegisterCreatedObjectUndo(go, "Prop Painter");
            string category = CategoryOf(entry.prefab);
            go.transform.SetParent(Container(category), true);

            float s = Rand(scaleRange.x, scaleRange.y);
            float h = Rand(heightRange.x, heightRange.y);
            float yaw = Rand(0f, 360f);
            if (yawSnap > 0.5f) yaw = Mathf.Round(yaw / yawSnap) * yawSnap;
            var up = Vector3.Slerp(Vector3.up, hit.normal, alignToNormal);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(s, s * h, s);
            float down = sink * s;
            if (category == "Rocks" && rockSink > 0f)                   // rotsen liggen ín de grond, niet erop
            {
                go.transform.position = hit.point;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    down += b.size.y * rockSink;
                }
            }
            go.transform.position = hit.point - up * down;

            if (addColliders && category != "Plants") AddColliders(go);
            return go;
        }

        static void AddColliders(GameObject go)
        {
            if (go.GetComponentInParent<Rigidbody>()) return;            // nooit een holle mesh collider op een Rigidbody
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh && !mf.GetComponent<Collider>() && !mf.GetComponentInParent<Rigidbody>())
                    Undo.AddComponent<MeshCollider>(mf.gameObject).sharedMesh = mf.sharedMesh;
        }

        void AddCollidersToExisting()
        {
            var root = GameObject.Find(rootName);
            if (!root) return;
            Undo.IncrementCurrentGroup();
            int n = 0;
            foreach (Transform cat in root.transform)
            {
                if (cat.name == "Plants") continue;
                foreach (Transform t in cat) { int before = t.GetComponentsInChildren<Collider>().Length; AddColliders(t.gameObject); if (t.GetComponentsInChildren<Collider>().Length > before) n++; }
            }
            Undo.SetCurrentGroupName("Prop Painter: colliders");
            ShowNotification(new GUIContent($"{n} objecten kregen een collider"));
        }

        void Erase(Vector3 center)
        {
            var root = GameObject.Find(rootName);
            if (!root) return;
            var doomed = new List<GameObject>();
            foreach (Transform cat in root.transform)
                foreach (Transform t in cat)
                {
                    var d = t.position - center;
                    d.y = 0f;
                    if (d.sqrMagnitude <= radius * radius) doomed.Add(t.gameObject);
                }
            foreach (var go in doomed) Undo.DestroyObjectImmediate(go);
        }
    }
}
