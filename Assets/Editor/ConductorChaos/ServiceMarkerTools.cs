// Conductor Chaos — doelvakken en bakens (issue #229). Vervangt de kale kubussen in een dienstscène door de modellen uit
// ArtSource/Environment/Station/service_markers.py:
//   'Vloermarkering' onder een doelvak (VRACHT / GEVONDEN BAGAGE / OEFENVAK)  -> Service_ZoneMark_Cargo/Luggage/Practice
//   'Groen stopbaken'                                                         -> Service_StopMarker
//   'Geel wisselbaken'                                                        -> Service_SwitchMarker
// Alleen beeld: de doelvakken zelf (BoxCollider-triggers) en de spelregels blijven zoals ze zijn.
// Menu: Conductor Chaos > Art > Station > Doelvakken en bakens vernieuwen (open scène). De kloof-levelbouwer doet het
// ook automatisch na het kopiëren van level 1.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ConductorChaos.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    public static class ServiceMarkerTools
    {
        const string PrefabDir = StationKitPrefabs.PrefabDir + "/Service";
        const string ModelDir = StationKitPrefabs.ModelDir + "/Service";

        // naam, nominale maat (x, z) van het model
        static readonly Dictionary<string, Vector2> Marks = new Dictionary<string, Vector2>
        {
            { "Service_ZoneMark_Cargo", new Vector2(3.2f, 1.7f) },
            { "Service_ZoneMark_Luggage", new Vector2(2.5f, 1.7f) },
            { "Service_ZoneMark_Practice", new Vector2(1.75f, 1.25f) },
        };

        [MenuItem("Conductor Chaos/Art/Station/Doelvakken en bakens vernieuwen (open scène)")]
        static void ReplaceInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            var log = new StringBuilder();
            int n = ReplaceIn(scene, log);
            if (n > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Doelvakken] {scene.name}: {n} vervangen.\n{log}");
            EditorUtility.DisplayDialog("Doelvakken en bakens", n > 0
                ? $"{n} kubus(sen) vervangen door de nieuwe modellen in {scene.name}. Sla de scène op."
                : "Geen oude kubussen gevonden (al vernieuwd?).", "OK");
        }

        /// <summary>Vervangt alle oude markeringskubussen in de scène; geeft het aantal terug.</summary>
        public static int ReplaceIn(Scene scene, StringBuilder log)
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            var targets = all.Where(t => t && IsOldCube(t) &&
                                         (t.name == "Vloermarkering" || t.name == "Groen stopbaken" || t.name == "Geel wisselbaken")).ToList();
            if (targets.Count == 0) return 0;
            var rails = RailPoints(scene);
            int replaced = 0;
            foreach (var old in targets)
            {
                bool floor = old.name == "Vloermarkering";
                string model = floor ? FloorModel(old) : old.name == "Groen stopbaken" ? "Service_StopMarker" : "Service_SwitchMarker";
                var source = Load(model);
                if (!source)
                {
                    log.AppendLine($"{model} ontbreekt; {old.name} overgeslagen.");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                go.name = old.name;
                go.transform.SetParent(old.parent, true);
                Undo.RegisterCreatedObjectUndo(go, "Doelvakken vernieuwen");
                var bounds = old.GetComponent<Renderer>() ? old.GetComponent<Renderer>().bounds : new Bounds(old.position, Vector3.zero);
                if (floor)
                {
                    var zone = old.parent ? old.parent.GetComponent<BoxCollider>() : null;
                    Vector3 center = zone ? zone.transform.TransformPoint(zone.center) : bounds.center;
                    float bottom = zone ? zone.transform.TransformPoint(zone.center - Vector3.up * zone.size.y * 0.5f).y : bounds.min.y;
                    Vector3 size = zone ? Vector3.Scale(zone.size, Abs(zone.transform.lossyScale)) : bounds.size;
                    var basis = zone ? zone.transform : old;
                    // lange kant van het vak langs X van het model; de tekst leest van de kant weg van het spoor
                    Vector3 along = size.x >= size.z ? basis.right : basis.forward;
                    Vector3 across = Vector3.Cross(along, Vector3.up);
                    Vector3 away = AwayFromTrack(rails, center);
                    if (Vector3.Dot(across, away) < 0f) { along = -along; across = -across; }
                    go.transform.SetPositionAndRotation(new Vector3(center.x, bottom, center.z), Quaternion.LookRotation(across, Vector3.up));
                    var nominal = Marks[model];
                    float sx = Mathf.Max(size.x, size.z), sz = Mathf.Min(size.x, size.z);
                    var parentScale = go.transform.parent ? go.transform.parent.lossyScale : Vector3.one;
                    go.transform.localScale = new Vector3(sx / nominal.x / Mathf.Abs(parentScale.x), 1f / Mathf.Abs(parentScale.y),
                                                          sz / nominal.y / Mathf.Abs(parentScale.z));
                }
                else
                {
                    // bord dwars op het spoor (zichtbaar voor de machinist), uitstekend van het spoor af (+X van het model)
                    Vector3 basePoint = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                    Vector3 away = AwayFromTrack(rails, basePoint, out Vector3 trackDir);
                    Vector3 forward = trackDir.sqrMagnitude > 0.01f ? trackDir : Vector3.forward;
                    if (Vector3.Dot(Vector3.Cross(Vector3.up, forward), away) < 0f) forward = -forward;
                    go.transform.SetPositionAndRotation(basePoint, Quaternion.LookRotation(forward, Vector3.up));
                    var parentScale = go.transform.parent ? go.transform.parent.lossyScale : Vector3.one;
                    go.transform.localScale = new Vector3(1f / Mathf.Abs(parentScale.x), 1f / Mathf.Abs(parentScale.y), 1f / Mathf.Abs(parentScale.z));
                }
                go.transform.SetSiblingIndex(old.GetSiblingIndex());
                log.AppendLine($"{old.name} onder {(old.parent ? old.parent.name : "(scène)")} -> {model}");
                Undo.DestroyObjectImmediate(old.gameObject);
                replaced++;
            }
            return replaced;
        }

        static bool IsOldCube(Transform t)
        {
            var mf = t.GetComponent<MeshFilter>();
            return mf && mf.sharedMesh && mf.sharedMesh.name == "Cube";
        }

        static string FloorModel(Transform marking)
        {
            string zone = marking.parent ? marking.parent.name.ToUpperInvariant() : "";
            if (zone.Contains("OEFEN")) return "Service_ZoneMark_Practice";
            if (zone.Contains("BAGAGE") || zone.Contains("KOFFER")) return "Service_ZoneMark_Luggage";
            if (zone.Contains("VRACHT") || zone.Contains("KRAT")) return "Service_ZoneMark_Cargo";
            var r = marking.GetComponent<Renderer>();
            string mat = r && r.sharedMaterial ? r.sharedMaterial.name : "";
            if (mat.Contains("Leaf")) return "Service_ZoneMark_Luggage";
            var box = marking.parent ? marking.parent.GetComponent<BoxCollider>() : null;
            return box && Mathf.Max(box.size.x, box.size.z) < 2.1f ? "Service_ZoneMark_Practice" : "Service_ZoneMark_Cargo";
        }

        static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            if (!prefab && AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx"))
            {
                StationKitPrefabs.Build();                    // nieuw model: eerst de prefabs (botsvormen) maken
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            }
            return prefab ? prefab : AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        static List<(Vector3 p, Vector3 d)> RailPoints(Scene scene)
        {
            var points = new List<(Vector3, Vector3)>();
            foreach (var piece in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TrackPiece>(true)))
            {
                if (!piece.HasSockets) piece.FindSockets();
                if (!piece.HasSockets || piece.Length < 0.01f) continue;
                int n = Mathf.Max(2, Mathf.CeilToInt(piece.Length / 0.5f));
                for (int i = 0; i <= n; i++)
                {
                    piece.Evaluate(i / (float)n, out var p, out var d);
                    points.Add((p, d));
                }
            }
            return points;
        }

        static Vector3 AwayFromTrack(List<(Vector3 p, Vector3 d)> rails, Vector3 at) => AwayFromTrack(rails, at, out _);

        // Horizontale richting van het dichtstbijzijnde spoorpunt naar 'at', en de rijrichting daar.
        static Vector3 AwayFromTrack(List<(Vector3 p, Vector3 d)> rails, Vector3 at, out Vector3 direction)
        {
            direction = Vector3.zero;
            float best = float.MaxValue;
            Vector3 nearest = at;
            foreach (var (p, d) in rails)
            {
                float dx = p.x - at.x, dz = p.z - at.z, dist = dx * dx + dz * dz;
                if (dist < best) { best = dist; nearest = p; direction = d; }
            }
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.zero;
            var away = at - nearest;
            away.y = 0f;
            // loodrecht op het spoor (alleen de zijwaartse component telt)
            if (direction != Vector3.zero) away -= direction * Vector3.Dot(away, direction);
            return away.sqrMagnitude > 1e-6f ? away.normalized : Vector3.right;
        }
    }
}
