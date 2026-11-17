// Conductor Chaos — natuur v3 aanvullen in bestaande levels (issues #240, #241). Zet loofbomen, struiken, varens,
// paddenstoelen, omgevallen stammen, bloemenperken en waterprops (lelies, riet, eendjes, steiger, roeibootje) in de open
// scène, zonder het level opnieuw te bouwen. Alles komt onder één root ('Natuur v3'); opnieuw draaien vervangt die root,
// dus het is veilig om het vaker te doen. Menu: Tools > Conductor Chaos > Natuur v3 aanvullen.
using System;
using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Art;
using ConductorChaos.Nature;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ConductorChaos.EditorTools
{
    public static class NatureDresser
    {
        public const string RootName = "Natuur v3";
        const string EnvDir = "Assets/Art/Models/Environment";

        [MenuItem("Tools/Conductor Chaos/Natuur v3 aanvullen (open scène)")]
        public static void DressOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            string log = Dress(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(log);
        }

        [MenuItem("Tools/Conductor Chaos/Natuur v3 aanvullen (level 1-3, opslaan)")]
        public static void DressLevels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var logs = new List<string>();
            foreach (var name in new[] { ServiceRun.FirstLevelScene, ServiceRun.SecondLevelScene, ServiceRun.ThirdLevelScene })
            {
                string path = $"Assets/Scenes/{name}.unity";
                if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) { logs.Add($"{name}: scène ontbreekt."); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                logs.Add(Dress(scene));
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log(string.Join("\n", logs));
        }

        static GameObject Model(string folder, string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{folder}/{name}.fbx");

        public static string Dress(Scene scene)
        {
            foreach (var old in scene.GetRootGameObjects().Where(r => r.name == RootName).ToArray()) Object.DestroyImmediate(old);
            Physics.SyncTransforms();
            var d = new Context(scene);
            d.Run();
            return $"{scene.name}: {d.Summary}";
        }

        sealed class Context
        {
            readonly Scene scene;
            readonly Random rng;
            readonly Transform root;
            readonly List<Vector3> track = new();
            readonly List<Vector3> stations = new();
            readonly List<StylizedWater> waters = new();
            readonly Dictionary<string, Transform> groups = new();
            readonly List<Vector3> taken = new();
            Bounds area;
            readonly Dictionary<string, int> counts = new();

            public Context(Scene scene)
            {
                this.scene = scene;
                rng = new Random(scene.name.GetHashCode() ^ 0x240241);
                var go = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(go, scene);
                root = go.transform;
            }

            public string Summary => string.Join(", ", counts.Select(kv => $"{kv.Value} {kv.Key}"));

            T[] InScene<T>() where T : Component =>
                scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

            public void Run()
            {
                foreach (var piece in InScene<TrackPiece>())
                {
                    if (piece.HasSockets) { track.Add(piece.StartPosition); track.Add(piece.end.position); }
                    if (piece.pathPoints != null) track.AddRange(piece.pathPoints.Where(p => p).Select(p => p.position));
                    track.Add(piece.transform.position);
                }
                stations.AddRange(InScene<ServiceStation>().Select(s => s.transform.position));
                waters.AddRange(InScene<StylizedWater>().Where(w => w.isActiveAndEnabled || w.gameObject.activeInHierarchy));
                var terrains = InScene<Terrain>();
                if (terrains.Length > 0)
                {
                    area = new Bounds(terrains[0].transform.position + terrains[0].terrainData.size * .5f, terrains[0].terrainData.size);
                    foreach (var t in terrains.Skip(1)) area.Encapsulate(new Bounds(t.transform.position + t.terrainData.size * .5f, t.terrainData.size));
                }
                else
                {
                    area = new Bounds(track.Count > 0 ? track[0] : Vector3.zero, Vector3.zero);
                    foreach (var p in track) area.Encapsulate(p);
                    area.Expand(new Vector3(160f, 0f, 160f));
                }
                Trees();
                Undergrowth();
                FlowerBeds();
                Water();
            }

            // ---------------------------------------------------------------- hulpjes
            Transform Group(string name)
            {
                if (groups.TryGetValue(name, out var t)) return t;
                t = new GameObject(name).transform;
                t.SetParent(root, false);
                groups[name] = t;
                return t;
            }

            void Count(string what) => counts[what] = counts.TryGetValue(what, out int n) ? n + 1 : 1;

            float TrackDistance(Vector3 p)
            {
                float best = float.PositiveInfinity;
                foreach (var t in track)
                {
                    float dx = t.x - p.x, dz = t.z - p.z;
                    float d = dx * dx + dz * dz;
                    if (d < best) best = d;
                }
                return Mathf.Sqrt(best);
            }

            bool NearStation(Vector3 p, float r) => stations.Any(s => (new Vector2(s.x - p.x, s.z - p.z)).sqrMagnitude < r * r);

            bool Free(Vector3 p, float r)
            {
                foreach (var t in taken)
                    if ((new Vector2(t.x - p.x, t.z - p.z)).sqrMagnitude < r * r) return false;
                return true;
            }

            Vector3 RandomPoint() => new(area.min.x + 6f + (float)rng.NextDouble() * (area.size.x - 12f), 0f,
                                         area.min.z + 6f + (float)rng.NextDouble() * (area.size.z - 12f));

            // Grond onder p: terrein of klif, niet op spoor, perron, gebouw of water. Geeft ook de helling.
            bool Ground(ref Vector3 p, out float slope)
            {
                slope = 90f;
                var hits = Physics.RaycastAll(new Vector3(p.x, area.max.y + 300f, p.z), Vector3.down, area.size.y + 900f, ~(1 << 2),
                    QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) return false;
                var hit = hits.OrderBy(h => h.distance).First();
                bool land = hit.collider is TerrainCollider || hit.collider.GetComponentInParent<CliffMass>();
                if (!land) return false;
                if (WaterAt(p, out float waterY) && waterY > hit.point.y - .05f) return false;
                p.y = hit.point.y;
                slope = Vector3.Angle(hit.normal, Vector3.up);
                return true;
            }

            bool WaterAt(Vector3 p, out float y)
            {
                y = 0f;
                if (!Physics.Raycast(new Vector3(p.x, area.max.y + 300f, p.z), Vector3.down, out var hit, area.size.y + 900f, 1 << 2,
                        QueryTriggerInteraction.Collide) || !hit.collider.GetComponentInParent<StylizedWater>()) return false;
                y = hit.collider.GetComponentInParent<StylizedWater>().transform.position.y;
                return true;
            }

            GameObject Place(GameObject model, string group, Vector3 position, float yaw, float scale, bool collider, float sink = .05f)
            {
                if (!model) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                go.transform.SetParent(Group(group), true);
                go.transform.SetPositionAndRotation(position + Vector3.down * sink, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = Vector3.one * scale;
                var marker = go.AddComponent<PaintedProp>();
                marker.groundOffset = -sink;
                if (collider)
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh && !mf.GetComponent<Collider>())
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                taken.Add(position);
                return go;
            }

            float Noise(Vector3 p, float scale, float ox) => Mathf.PerlinNoise(p.x / scale + ox, p.z / scale + ox * .7f);

            T Pick<T>(IList<(T item, float weight)> items)
            {
                float total = items.Sum(i => i.weight), r = (float)rng.NextDouble() * total;
                foreach (var (item, weight) in items) { r -= weight; if (r <= 0f) return item; }
                return items[items.Count - 1].item;
            }

            // ---------------------------------------------------------------- bomen en struiken
            void Trees()
            {
                var trees = new List<(GameObject, float)>
                {
                    (Model("Trees", "Tree_Oak_01"), 1.0f), (Model("Trees", "Tree_Oak_Autumn_01"), .35f),
                    (Model("Trees", "Tree_Oak_Autumn_02"), .25f), (Model("Trees", "Tree_Birch_01"), .8f),
                    (Model("Trees", "Tree_Birch_Autumn_01"), .3f), (Model("Trees", "Bush_Round_02"), .7f),
                    (Model("Trees", "Bush_Berry_01"), .45f), (Model("Trees", "Bush_Blossom_01"), .4f),
                }.Where(t => t.Item1).ToList();
                if (trees.Count == 0) return;
                int max = Mathf.RoundToInt(Mathf.Clamp(area.size.x * area.size.z / 1500f, 60f, 260f));
                float ox = (float)rng.NextDouble() * 100f;
                for (int i = 0, placed = 0; i < max * 40 && placed < max; i++)
                {
                    var p = RandomPoint();
                    // loofbos in plukken, vooral op de randen van de bestaande bossen en langs weides
                    float grove = Noise(p, 38f, ox);
                    if (grove < .52f || rng.NextDouble() > (grove - .52f) * 3.2f) continue;
                    float dTrack = TrackDistance(p);
                    var model = Pick(trees);
                    bool bush = model.name.StartsWith("Bush");
                    if (dTrack < (bush ? 4.5f : 7.5f) || NearStation(p, 10f) || !Free(p, bush ? 2f : 3.4f)) continue;
                    if (!Ground(ref p, out float slope) || slope > 24f) continue;
                    if (Place(model, bush ? "Struiken" : "Loofbomen", p, (float)rng.NextDouble() * 360f, .85f + (float)rng.NextDouble() * .4f,
                            dTrack < 40f, bush ? .08f : .1f))
                    { placed++; Count(bush ? "struiken" : "loofbomen"); }
                }
                // treurwilgen aan het water
                var willow = Model("Trees", "Tree_Willow_01");
                foreach (var water in waters.Take(6))
                {
                    if (!willow) break;
                    for (int k = 0; k < 8; k++)
                    {
                        var p = EdgePoint(water, 2.2f);
                        if (TrackDistance(p) < 8f || !Free(p, 4f) || !Ground(ref p, out float slope) || slope > 20f) continue;
                        Place(willow, "Loofbomen", p, (float)rng.NextDouble() * 360f, .9f + (float)rng.NextDouble() * .25f, TrackDistance(p) < 40f, .1f);
                        Count("wilgen");
                        break;
                    }
                }
            }

            // ---------------------------------------------------------------- varens, paddenstoelen, stammen
            void Undergrowth()
            {
                var small = new List<(GameObject, float)>
                {
                    (Model("Plants", "Fern_01"), 1f), (Model("Plants", "Fern_02"), .7f), (Model("Plants", "Mushroom_Red_01"), .35f),
                    (Model("Plants", "Mushroom_Cluster_01"), .45f), (Model("Wood", "Stump_01"), .25f),
                }.Where(t => t.Item1).ToList();
                var logs = new[] { Model("Wood", "Log_Fallen_01"), Model("Wood", "Log_Fallen_02") }.Where(m => m).ToArray();
                // bij bomen (oude en nieuwe): kijk rond elke boom
                var trees = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name.StartsWith("Tree_") && t.GetComponent<PaintedProp>()).Select(t => t.position).ToList();
                if (trees.Count == 0) trees.AddRange(taken);
                int maxSmall = Mathf.Clamp(trees.Count / 3, 40, 220), maxLogs = Mathf.Clamp(trees.Count / 40, 6, 18);
                for (int i = 0, placed = 0; i < maxSmall * 10 && placed < maxSmall && small.Count > 0; i++)
                {
                    var t = trees[rng.Next(trees.Count)];
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 1.6f + (float)rng.NextDouble() * 2.6f;
                    var p = t + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (TrackDistance(p) < 3.8f || NearStation(p, 6f) || !Free(p, .9f) || !Ground(ref p, out float slope) || slope > 22f) continue;
                    var model = Pick(small);
                    if (Place(model, "Bosgrond", p, (float)rng.NextDouble() * 360f, .8f + (float)rng.NextDouble() * .5f, model.name.StartsWith("Stump"), .03f))
                    { placed++; Count(model.name.StartsWith("Fern") ? "varens" : model.name.StartsWith("Stump") ? "stronken" : "paddenstoelen"); }
                }
                for (int i = 0, placed = 0; i < maxLogs * 30 && placed < maxLogs && logs.Length > 0; i++)
                {
                    var t = trees[rng.Next(trees.Count)];
                    var p = t + new Vector3((float)rng.NextDouble() * 6f - 3f, 0f, (float)rng.NextDouble() * 6f - 3f);
                    if (TrackDistance(p) < 6f || NearStation(p, 12f) || !Free(p, 3f) || !Ground(ref p, out float slope) || slope > 12f) continue;
                    if (Place(logs[rng.Next(logs.Length)], "Omgevallen stammen", p, (float)rng.NextDouble() * 360f, .9f + (float)rng.NextDouble() * .3f, true, .06f))
                    { placed++; Count("stammen"); }
                }
            }

            // ---------------------------------------------------------------- bloemen
            void FlowerBeds()
            {
                var beds = new[] { Model("Plants", "FlowerBed_Round_01"), Model("Plants", "FlowerBed_Long_01") }.Where(m => m).ToArray();
                var patches = new[] { Model("Plants", "Flower_Tulips_01"), Model("Plants", "Flower_Wild_01") }.Where(m => m).ToArray();
                foreach (var s in stations)
                {
                    int done = 0;
                    for (int k = 0; k < 60 && done < 3 && beds.Length > 0; k++)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 7f + (float)rng.NextDouble() * 7f;
                        var p = s + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        if (TrackDistance(p) < 4.2f || !Free(p, 2.2f) || !Ground(ref p, out float slope) || slope > 8f) continue;
                        if (Place(beds[rng.Next(beds.Length)], "Bloemenperken", p, (float)rng.NextDouble() * 360f, 1f, true, .02f)) { done++; Count("bloemenperken"); }
                    }
                }
                int max = Mathf.RoundToInt(Mathf.Clamp(track.Count / 3f, 30f, 120f));
                for (int i = 0, placed = 0; i < max * 25 && placed < max && patches.Length > 0 && track.Count > 0; i++)
                {
                    var t = track[rng.Next(track.Count)];
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 4.5f + (float)rng.NextDouble() * 14f;
                    var p = t + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    if (TrackDistance(p) < 4.2f || !Free(p, 1.2f) || !Ground(ref p, out float slope) || slope > 14f) continue;
                    if (Place(patches[rng.Next(patches.Length)], "Bloemen", p, (float)rng.NextDouble() * 360f, .9f + (float)rng.NextDouble() * .4f, false, .02f))
                    { placed++; Count("bloemplukjes"); }
                }
            }

            // ---------------------------------------------------------------- water
            Vector3 EdgePoint(StylizedWater water, float outside)
            {
                var t = water.transform;
                float hx = water.size.x * .5f, hz = water.size.y * .5f;
                bool alongX = rng.NextDouble() < (hx / (hx + hz));
                float s = rng.NextDouble() < .5 ? -1f : 1f;
                var local = alongX ? new Vector3(((float)rng.NextDouble() * 2f - 1f) * hx, 0f, s * (hz + outside))
                                   : new Vector3(s * (hx + outside), 0f, ((float)rng.NextDouble() * 2f - 1f) * hz);
                return t.TransformPoint(local);
            }

            Vector3 OnWater(StylizedWater water, float inset)
            {
                float hx = Mathf.Max(.2f, water.size.x * .5f - inset), hz = Mathf.Max(.2f, water.size.y * .5f - inset);
                return water.transform.TransformPoint(new Vector3(((float)rng.NextDouble() * 2f - 1f) * hx, 0f, ((float)rng.NextDouble() * 2f - 1f) * hz));
            }

            void Water()
            {
                if (waters.Count == 0) return;
                var lilies = new[] { Model("Water", "Lily_Pads_01"), Model("Water", "Lily_Pad_Single") }.Where(m => m).ToArray();
                var reeds = new[] { Model("Water", "Reeds_01"), Model("Water", "Cattails_01") }.Where(m => m).ToArray();
                var ducks = new[] { Model("Water", "Duck_01"), Model("Water", "Duck_02") }.Where(m => m).ToArray();
                foreach (var water in waters)
                {
                    float a = water.size.x * water.size.y;
                    float y = water.transform.position.y;
                    int nLily = Mathf.Clamp(Mathf.RoundToInt(a / 60f), 1, 8), nReed = Mathf.Clamp(Mathf.RoundToInt((water.size.x + water.size.y) / 6f), 2, 12);
                    for (int k = 0; k < nLily && lilies.Length > 0; k++)
                    {
                        var p = OnWater(water, .6f);
                        if (TrackDistance(p) < 3f || !Free(p, 1.2f)) continue;
                        var go = Place(lilies[rng.Next(lilies.Length)], "Waterlelies", new Vector3(p.x, y + .01f, p.z), (float)rng.NextDouble() * 360f, .9f + (float)rng.NextDouble() * .4f, false, 0f);
                        if (go) { go.GetComponent<PaintedProp>().followGround = false; go.AddComponent<WaterBob>().Configure(.012f, 3.4f, 1.2f); Count("waterlelies"); }
                    }
                    for (int k = 0; k < nReed && reeds.Length > 0; k++)
                    {
                        var p = EdgePoint(water, -.25f);
                        if (TrackDistance(p) < 3.5f || !Free(p, .9f)) continue;
                        var go = Place(reeds[rng.Next(reeds.Length)], "Riet", new Vector3(p.x, y, p.z), (float)rng.NextDouble() * 360f, .9f + (float)rng.NextDouble() * .4f, false, .05f);
                        if (go) { go.GetComponent<PaintedProp>().followGround = false; Count("rietpollen"); }
                    }
                    if (a >= 40f && ducks.Length > 0)
                        for (int k = 0; k < Mathf.Clamp(Mathf.RoundToInt(a / 120f), 1, 3); k++)
                        {
                            var p = OnWater(water, 1f);
                            var go = Place(ducks[rng.Next(ducks.Length)], "Eendjes", new Vector3(p.x, y, p.z), (float)rng.NextDouble() * 360f, 1f, false, 0f);
                            if (!go) continue;
                            go.GetComponent<PaintedProp>().followGround = false;
                            go.AddComponent<DuckSwim>();
                            Count("eendjes");
                        }
                }
                // steiger en roeibootje bij het grootste water, weg van het spoor
                var biggest = waters.OrderByDescending(w => w.size.x * w.size.y).First();
                var jetty = Model("Water", "Jetty_01");
                var boat = Model("Water", "Rowboat_01");
                if (biggest.size.x * biggest.size.y < 80f || !jetty) return;
                for (int k = 0; k < 30; k++)
                {
                    var shore = EdgePoint(biggest, .4f);
                    if (TrackDistance(shore) < 9f || !Free(shore, 4f)) continue;
                    var into = biggest.transform.position - shore;
                    into.y = 0f;
                    float wy = biggest.transform.position.y;
                    var go = Place(jetty, "Steiger", new Vector3(shore.x, wy - .02f, shore.z), Quaternion.LookRotation(into.normalized).eulerAngles.y, 1f, true, 0f);
                    if (!go) break;
                    go.GetComponent<PaintedProp>().followGround = false;
                    Count("steigers");
                    if (boat)
                    {
                        var side = Vector3.Cross(Vector3.up, into.normalized);
                        var at = shore + into.normalized * 2.6f + side * 1.5f;
                        var b = Place(boat, "Steiger", new Vector3(at.x, wy, at.z), Quaternion.LookRotation(into.normalized).eulerAngles.y + 8f, 1f, false, 0f);
                        if (b) { b.GetComponent<PaintedProp>().followGround = false; b.AddComponent<WaterBob>().Configure(.035f, 3f, 2.5f); Count("bootjes"); }
                    }
                    break;
                }
            }
        }
    }
}
