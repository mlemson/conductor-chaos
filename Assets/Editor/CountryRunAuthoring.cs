using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConductorChaos.Art;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

internal sealed class CountryRunAuthoring
{
    private const string ScenePath = "Assets/Scenes/ServiceLevel01.unity";
    private const string TerrainPath = "Assets/Art/Terrain/Level01_CountryRun.asset";
    private const string Tracks = "Assets/Art/Prefabs/Track/";
    private const string Farm = "Assets/Art/Prefabs/Station/Farm/";
    private const string Models = "Assets/Art/Models/Environment/";
    private readonly List<TrackPiece> added = new();
    private readonly List<Vector3> samples = new();
    private readonly Random random = new(21701);
    private readonly Dictionary<string, GameObject> assets = new();
    private Scene scene;
    private TrackPiece current;
    private Transform scenery;
    private Terrain terrain;
    private Vector3 first, river, farm, last;
    private int forestLimit, ridgeStart, farmStart;

    [MenuItem("Tools/Conductor Chaos/Level 1/Generate Country Run")]
    private static void Generate()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForAuthoring = !scene.isLoaded;
        if (openedForAuthoring)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try { new CountryRunAuthoring { scene = scene }.Build(); }
        finally
        {
            if (openedForAuthoring && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private void Build()
    {
        var roots = scene.GetRootGameObjects();
        var pieces = roots.SelectMany(r => r.GetComponentsInChildren<TrackPiece>()).Where(t => t.HasSockets).ToArray();
        var run = roots.Select(r => r.GetComponent<ServiceRun>()).Single(r => r);
        var drive = roots.Select(r => r.GetComponent<TrainDrive>()).Single(t => t);
        var startTrack = (TrackPiece)new SerializedObject(drive).FindProperty("firstTrack").objectReferenceValue;
        var oldRoute = MainLine(startTrack, pieces);
        var pending = roots.SingleOrDefault(r => r.name == "Landroute · nieuw spoor");
        if (pending && !AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath))
        {
            added.AddRange(pending.GetComponentsInChildren<TrackPiece>());
            first = added[0].StartPosition;
            last = added[^1].EndPosition;
            var bridge = pending.transform.Find("Houten rivierbrug").GetComponentsInChildren<TrackPiece>();
            river = (bridge[0].StartPosition + bridge[^1].EndPosition) * .5f;
            var farmTrack = pending.transform.Find("Boerderijspoor").GetComponentInChildren<TrackPiece>();
            farm = farmTrack.StartPosition + Vector3.right * 52;
            forestLimit = added.IndexOf(pending.transform.Find("Aanloop naar de rivier").GetComponentInChildren<TrackPiece>());
            ridgeStart = added.IndexOf(pending.transform.Find("Klim naar de rotskam").GetComponentInChildren<TrackPiece>());
            farmStart = added.IndexOf(farmTrack);
            FinishRoute(oldRoute, run, roots);
            return;
        }
        if (scene.isDirty || pending || AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath))
            throw new InvalidOperationException("COUNTRY RUN: Level 1 is already generated or has unsaved edits.");
        var join = oldRoute.First(t => t.EndPosition.x > 100 && t.name.Contains("Track_Straight"));
        int joinIndex = oldRoute.IndexOf(join);
        var tail = oldRoute[joinIndex + 1];
        var oldArrival = run.Arrival.TrainStop.transform.TransformPoint(run.Arrival.TrainStop.center);
        first = tail.StartPosition;
        if (Vector3.Dot(tail.StartDirection, Vector3.right) < .995f || Vector3.Distance(join.EndPosition, first) > .05f)
            throw new InvalidOperationException("COUNTRY RUN: existing station lead is incompatible with this splice.");
        var tailRoots = roots.Where(r => r.transform.position.x >= first.x - .1f)
            .Concat(oldRoute.Skip(joinIndex + 1).Select(t => t.transform.root.gameObject)).Distinct().ToArray();
        if (!tailRoots.Contains(run.Arrival.transform.root.gameObject))
            throw new InvalidOperationException("COUNTRY RUN: arrival station is outside the translated group.");

        current = join;
        var railRoot = Root("Landroute · nieuw spoor");
        Segment("Naaldbos", 4, "Straight_12m", railRoot);
        Segment("Klim naar het bos", 3, "Ramp_Up_12m", railRoot);
        Segment("Bosbocht", 2, "Curve_R20_15_Left", railRoot);
        Segment("Bosspoor", 7, "Straight_12m", railRoot);
        Segment("Bosuitgang", 2, "Curve_R20_15_Right", railRoot);
        Segment("Dalrand", 3, "Straight_12m", railRoot);
        forestLimit = added.Count;
        Segment("Aanloop naar de rivier", 6, "Straight_12m", railRoot);
        Vector3 bridgeStart = current.EndPosition;
        Segment("Houten rivierbrug", 8, "Bridge_Trestle_6m", railRoot);
        river = (bridgeStart + current.EndPosition) * .5f;
        Segment("Overkant", 6, "Straight_12m", railRoot);

        ridgeStart = added.Count;
        Segment("Klim naar de rotskam", 2, "Ramp_Up_12m", railRoot);
        Segment("Bocht naar de rotskam", 2, "Curve_R20_15_Right", railRoot);
        Segment("Langs de rotskam", 8, "Straight_12m", railRoot);
        Segment("Van de rotskam", 2, "Curve_R20_15_Left", railRoot);
        Segment("Uitzicht over het dal", 7, "Straight_12m", railRoot);
        Segment("Afdaling naar de akkers", 5, "Ramp_Down_12m", railRoot);
        farmStart = added.Count;

        farm = current.EndPosition + Vector3.right * 52;
        Segment("Boerderijspoor", 8, "Straight_12m", railRoot);
        Segment("Bocht langs de akkers", 1, "Curve_R20_15_Left", railRoot);
        Segment("Akkerspoor", 9, "Straight_12m", railRoot);
        Segment("Uit de akkers", 1, "Curve_R20_15_Right", railRoot);
        Segment("Weideland", 10, "Straight_12m", railRoot);
        Segment("Bocht naar Puddleford", 1, "Curve_R20_15_Right", railRoot);
        Segment("Dorpstocht", 12, "Straight_12m", railRoot);
        Segment("Aanloop Puddleford", 1, "Curve_R20_15_Left", railRoot);
        Segment("Laatste rechte stuk", 9, "Straight_12m", railRoot);

        last = current.EndPosition;
        Vector3 shift = last - first;
        if (Mathf.Abs(shift.y) > .015f || Vector3.Dot(current.EndDirection, tail.StartDirection) < .998f)
            throw new InvalidOperationException("COUNTRY RUN: extension does not finish level and aligned.");
        foreach (var root in tailRoots)
        {
            root.transform.position += shift;
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        }
        Link(current, tail);

        var main = oldRoute.Take(joinIndex + 1).Concat(added).Concat(oldRoute.Skip(joinIndex + 1)).ToList();
        FinishRoute(main, run, roots);
    }

    private void FinishRoute(List<TrackPiece> main, ServiceRun run, GameObject[] roots)
    {
        var arrival = run.Arrival.TrainStop.transform.TransformPoint(run.Arrival.TrainStop.center);
        Validate(main, run, arrival);
        if (main.Min(t => Vector3.Distance(t.StartPosition, arrival)) > 14)
            throw new InvalidOperationException($"COUNTRY RUN: the arrival stop is disconnected at {arrival}.");
        foreach (var piece in main)
        {
            for (int i = 0; i < 4; i++)
            {
                piece.Evaluate(i * .25f, out Vector3 point, out _);
                if (samples.Count == 0 || point.x > samples[^1].x + .005f) samples.Add(point);
            }
        }
        samples.Add(main[^1].EndPosition);
        var layer = new SerializedObject(run);
        layer.FindProperty("extraTravelSeconds").intValue = 240;
        layer.ApplyModifiedPropertiesWithoutUndo();
        terrain = MakeTerrain(roots);
        scenery = Root("Landroute · landschap");
        MakeWater();
        DressRoute();
        MakeFarm();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("COUNTRY RUN: scene did not save.");
        AssetDatabase.SaveAssets();
        Debug.Log($"COUNTRY RUN READY: {added.Count} new rail pieces; {added.Sum(t => t.Length):F1} m added; " +
            $"{main.Sum(t => t.Length):F1} m from starting section to buffer; Puddleford={run.Arrival.TrainStop.transform.TransformPoint(run.Arrival.TrainStop.center)}; 240 s extra.");
    }

    private static List<TrackPiece> MainLine(TrackPiece firstTrack, TrackPiece[] pieces)
    {
        var route = new List<TrackPiece>();
        for (var previous = firstTrack; previous && !route.Contains(previous);)
        {
            route.Add(previous);
            if (previous.name.Contains("BufferStop")) break;
            var straight = previous.GetComponent<TrackSwitch>()?.Straight;
            var piece = straight ? straight : previous;
            previous = piece.next ? piece.next : pieces.Where(t => t != piece && t.name != "Route_Diverge" &&
                    Vector3.Dot(piece.EndDirection, t.StartDirection) > .9f &&
                    Vector3.Distance(piece.EndPosition, t.StartPosition) < .25f)
                .OrderBy(t => (piece.EndPosition - t.StartPosition).sqrMagnitude).FirstOrDefault();
        }
        if (route.Count < 10 || !route[^1].name.Contains("BufferStop"))
            throw new InvalidOperationException("COUNTRY RUN: baseline track chain does not reach its buffer.");
        return route;
    }

    private static void Validate(List<TrackPiece> track, ServiceRun run, Vector3 arrival)
    {
        if (!run.IsConfigured || Vector3.Distance(arrival, run.Arrival.TrainStop.transform.TransformPoint(run.Arrival.TrainStop.center)) > .025f)
            throw new InvalidOperationException($"COUNTRY RUN: original stop or service data was lost; configured={run.IsConfigured}, expected={arrival}, actual={run.Arrival.TrainStop.transform.TransformPoint(run.Arrival.TrainStop.center)}");
        float measured = 0;
        for (int i = 1; i < track.Count; i++)
        {
            var a = track[i - 1];
            var b = track[i];
            float gap = Vector3.Distance(a.EndPosition, b.StartPosition);
            if (gap > .25f || Vector3.Dot(a.EndDirection, b.StartDirection) < .97f)
                throw new InvalidOperationException($"COUNTRY RUN: gap at {a.name} to {b.name}: {gap:F3} m.");
            measured += a.Length;
        }
        if (measured < 1400 || measured > 1550)
            throw new InvalidOperationException($"COUNTRY RUN: expected 1.4–1.55 km, got {measured:F1} m.");
    }

    private Transform Root(string name)
    {
        var root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }

    private GameObject Place(string path, Transform parent)
    {
        if (!assets.TryGetValue(path, out var asset))
        {
            asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new InvalidOperationException("COUNTRY RUN: missing asset " + path);
            assets.Add(path, asset);
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static void Link(TrackPiece from, TrackPiece to)
    {
        from.next = to;
        EditorUtility.SetDirty(from);
        PrefabUtility.RecordPrefabInstancePropertyModifications(from);
    }

    private void Segment(string name, int count, string key, Transform parent)
    {
        Transform group = Root(name);
        group.SetParent(parent, false);
        for (int i = 0; i < count; i++)
        {
            var rail = Place(Tracks + "Track_" + key + ".prefab", group);
            var piece = rail.GetComponent<TrackPiece>();
            if (!piece || !piece.HasSockets) throw new InvalidOperationException($"COUNTRY RUN: {key} missing sockets.");
            float yaw = Vector3.SignedAngle(piece.StartDirection, current.EndDirection, Vector3.up);
            piece.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * piece.transform.rotation;
            piece.transform.position += current.EndPosition - piece.StartPosition;
            if (Vector3.Distance(current.EndPosition, piece.StartPosition) > .008f)
                throw new InvalidOperationException($"COUNTRY RUN: {key} failed to snap to the previous socket.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(piece.transform);
            Link(current, piece);
            current = piece;
            added.Add(piece);
        }
    }

    private Terrain MakeTerrain(GameObject[] roots)
    {
        const float bottom = -10;
        const int res = 1025, splat = 128;
        var origin = new Vector3(-150, bottom, Mathf.Min(-230, samples.Min(p => p.z) - 165));
        var size = new Vector3(samples[^1].x + 320, 54, samples.Max(p => p.z) - origin.z + 195);
        var data = new TerrainData { heightmapResolution = res, alphamapResolution = splat, baseMapResolution = 256, size = size };
        data.terrainLayers = new[] { "TL_Grass", "TL_Dirt", "TL_GrassFlowers" }
            .Select(layer => AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/Art/Terrain/Layers/" + layer + ".terrainlayer")).ToArray();
        var heights = new float[res, res];
        int segment = 0;
        for (int x = 0; x < res; x++)
        {
            float wx = origin.x + x * size.x / (res - 1);
            while (segment + 2 < samples.Count && samples[segment + 1].x < wx) segment++;
            var a = samples[segment];
            var b = samples[segment + 1];
            Vector3 rail = Vector3.Lerp(a, b, Mathf.InverseLerp(a.x, b.x, wx));
            float scenic = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(first.x, first.x + 80, wx)) *
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(last.x, last.x - 105, wx));
            for (int z = 0; z < res; z++)
            {
                float wz = origin.z + z * size.z / (res - 1);
                float away = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4, 52, Mathf.Abs(wz - rail.z)));
                float rollingHills = Mathf.Pow(Mathf.PerlinNoise(wx * .012f, wz * .014f), 1.55f) * 22 +
                    (Mathf.PerlinNoise(wx * .045f + 19, wz * .05f) - .5f) * 4;
                float height = rail.y - .43f + rollingHills * scenic * away;
                float farmEdge = Mathf.Max(Mathf.Abs(wx - farm.x) - 36, Mathf.Abs(wz - (farm.z + 28)) - 21);
                if (farmEdge < 12) height = Mathf.Lerp(farm.y - .43f, height, Mathf.SmoothStep(0, 1, Mathf.Clamp01(farmEdge / 12)));
                float riverDistance = RiverDistance(wx, wz);
                if (riverDistance < 28)
                    height = Mathf.Lerp(-5.4f, height, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(14, 28, riverDistance)));
                heights[z, x] = Mathf.Clamp01((height - bottom) / size.y);
            }
        }
        data.SetHeights(0, 0, heights);
        var layers = new float[splat, splat, 3];
        for (int z = 0; z < splat; z++)
            for (int x = 0; x < splat; x++)
            {
                float wx = origin.x + x * size.x / (splat - 1), wz = origin.z + z * size.z / (splat - 1);
                float soil = RiverDistance(wx, wz) < 21 ? .78f : 0;
                float flowers = Mathf.InverseLerp(.48f, .8f, Mathf.PerlinNoise(wx * .07f, wz * .07f)) * .52f;
                layers[z, x, 0] = (1 - soil) * (1 - flowers);
                layers[z, x, 1] = soil;
                layers[z, x, 2] = (1 - soil) * flowers;
            }
        data.SetAlphamaps(0, 0, layers);
        AssetDatabase.CreateAsset(data, TerrainPath);
        var ground = Terrain.CreateTerrainGameObject(data);
        SceneManager.MoveGameObjectToScene(ground, scene);
        ground.name = "Landroute · glooiend terrein";
        ground.transform.position = origin;
        var result = ground.GetComponent<Terrain>();
        result.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Terrain/M_StylizedTerrain.mat");
        result.heightmapPixelError = 3;
        result.drawInstanced = true;
        roots.Single(r => r.name == "Grasland").SetActive(false);
        return result;
    }

    private float RiverDistance(float x, float z) =>
        Mathf.Abs(x - river.x - 7 * Mathf.Sin((z - river.z) * .027f) * Mathf.Clamp01(Mathf.Abs(z - river.z) / 30));

    private void MakeWater()
    {
        var go = new GameObject("Rivier onder de houten brug");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.SetParent(scenery, false);
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        go.transform.position = new Vector3(river.x, -1.35f, terrain.transform.position.z + terrain.terrainData.size.z * .5f);
        var water = go.AddComponent<StylizedWater>();
        water.size = new Vector2(72, terrain.terrainData.size.z - 1);
        water.depth = 5;
        water.facetSize = 3.1f;
        water.surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_Env_WaterSurface.mat");
        water.Rebuild();
    }

    private void DressRoute()
    {
        string[] trees = { "Trees/Tree_Pine_Small_01", "Trees/Tree_Pine_Medium_01", "Trees/Tree_Pine_Large_01", "Trees/Tree_Pine_Variant_02" };
        for (int i = 0; i < added.Count; i += 2)
        {
            added[i].Evaluate(.42f, out Vector3 position, out Vector3 direction);
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            bool forest = i < forestLimit || i >= ridgeStart && i < farmStart;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int n = 0; n < (forest ? 6 : 3); n++)
                {
                    Vector3 spot = position + direction * Range(-10, 10) + right * side * (Range(8, 16) + n * Range(3, 6));
                    if (RiverDistance(spot.x, spot.z) < 24 || NearFarm(spot)) continue;
                    Dress(Models + trees[random.Next(trees.Length)] + ".fbx", spot, Range(.85f, 1.8f));
                }
                Vector3 outcrop = position + right * side * Range(8, 17) + direction * Range(-6, 6);
                if (!NearFarm(outcrop) && RiverDistance(outcrop.x, outcrop.z) > 14)
                    Dress(Models + (i >= ridgeStart && i < farmStart && i % 6 == 0
                        ? "Cliffs/Cliff_Cluster_Step.fbx" : "Rocks/Rock_Medium_01.fbx"), outcrop, Range(.68f, 1.85f));
                Vector3 flower = position + right * side * Range(3.5f, 6.4f);
                if (RiverDistance(flower.x, flower.z) > 23)
                    Dress(Models + "Plants/Flower_Patch_01.fbx", flower, Range(.8f, 1.35f));
            }
        }
        for (int i = -5; i <= 5; i++)
            foreach (int side in new[] { -1, 1 })
                Dress(Models + "Rocks/Rock_Large_01.fbx", river + new Vector3(side * Range(18, 27), 0, i * 7 + Range(-2, 2)), Range(1.2f, 2.5f));
    }

    private bool NearFarm(Vector3 p) => Mathf.Abs(p.x - farm.x) < 43 && p.z > farm.z + 6 && p.z < farm.z + 54;

    private void MakeFarm()
    {
        Transform oldParent = scenery;
        scenery = Root("Landroute · boerderij en korenveld");
        scenery.SetParent(oldParent, false);
        FarmProp("Barn", -14, 32, 180, 1.3f);
        FarmProp("Shed", 0, 36, 170, 1.15f);
        FarmProp("Trough", -13, 22, 0);
        FarmProp("Wheelbarrow_Hay", -5, 20, 22);
        FarmProp("Haystack", -3, 28, 30);
        FarmProp("HayRack", -21, 24, 12);
        FarmProp("FieldSign", 11, 11.5f, 180);
        FarmProp("Scarecrow", 19, 19, 190);
        FarmProp("GrainSacks", 7, 12.5f, 19);
        FarmProp("Gate", -10, 11.5f, 0);
        for (int i = -9; i <= 12; i++)
        {
            if (i is -4 or -3 or 3) continue;
            FarmProp("Fence_3m", i * 3, 11.5f, 0);
        }
        for (int r = 0; r < 5; r++)
            for (int c = 0; c < 9; c++)
            {
                if (r == 2 && c == 3) continue;
                FarmProp(c >= 7 ? "Wheat_Harvested_3m" : "Wheat_Straight_3m", 12 + c * 3, 15 + r * 3, 0);
            }
        for (int i = 0; i < 5; i++) FarmProp("FieldPath_3m", 9, 15 + i * 3, 90);
        FarmProp("MilkStand", -26, 20, 30);
        Dress("Assets/Art/Models/Animals/Cow/Cow.fbx", farm + new Vector3(-24, 0, 20), 1f, 145);
        Dress("Assets/Art/Models/Animals/Sheep/Sheep.fbx", farm + new Vector3(-16, 0, 24), 1.15f, 220);
        Dress("Assets/Art/Models/Animals/Sheep/Sheep.fbx", farm + new Vector3(-19, 0, 19), .82f, 30);
        scenery = oldParent;
    }

    private void FarmProp(string asset, float x, float z, float yaw, float scale = 1) =>
        Dress(Farm + "Farm_" + asset + ".prefab", farm + new Vector3(x, 0, z), scale, yaw);

    private void Dress(string asset, Vector3 position, float scale, float? yaw = null)
    {
        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
        var prop = Place(asset, scenery);
        prop.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw ?? Range(0, 360), 0));
        prop.transform.localScale *= scale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
    }

    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
}