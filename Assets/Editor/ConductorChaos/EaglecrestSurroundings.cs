using System;
using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ConductorChaos.EditorTools
{
    internal static class EaglecrestSurroundings
    {
        const string Snowbank = "Eaglecrest · sneeuwberm";
        const string PlatformPath = "Assets/Art/Prefabs/Station/Platform/";
        const string PinePath = "Assets/Art/Models/Environment/Winter/Tree_Pine_Snow_";
        static readonly string[] YardProps =
        {
            "Prop_CargoBarrels", "Prop_CargoPipes", "Prop_CargoCrate", "Prop_TicketBooth",
            "Prop_LampPost", "Prop_Timetable", "Prop_Planter", "Prop_Shelter", "Prop_Bench",
            "Prop_TicketMachine", "Prop_TrashBin", "Prop_Barrel", "Prop_CrateStack", "Prop_Crate",
            "RailMechanic (1)", "BaggageHelper"
        };

        internal static void Apply(ServiceStation station, Terrain terrain)
        {
            var anchor = station.transform.root.Find(CliffLevelBuilder.EaglecrestBuilding);
            if (!anchor) throw new InvalidOperationException("Eaglecrest mist het stationsgebouw.");
            var scene = station.gameObject.scene;
            var roots = scene.GetRootGameObjects();
            if (roots.Any(r => r.name == Snowbank)) return;
            if (station.Role == ServiceStation.StationRole.Vertrek) SimplifyDeparture(roots, anchor, scene);

            foreach (var root in roots.Where(g => g && (g.name == "PaintedProps" || g.name == "Winterdecor")))
                foreach (var part in root.GetComponentsInChildren<Transform>().Where(t => t != root.transform &&
                             Near(anchor.InverseTransformPoint(t.position), 37f, 42f, 29f) &&
                             IsGroundDecoration(t.name) && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray())
                    if (part) Object.DestroyImmediate(part.gameObject);

            ShapeGround(terrain, anchor);
            var landscape = new GameObject(Snowbank);
            SceneManager.MoveGameObjectToScene(landscape, scene);
            landscape.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            PlantPines(landscape.transform, terrain, scene);
        }

        static bool Near(Vector3 p, float side, float rear, float front) =>
            Mathf.Abs(p.x) < side && p.z > -rear && p.z < front;

        static bool IsGroundDecoration(string name) => name.StartsWith("Tree_") || name.StartsWith("Bush_") ||
            name.StartsWith("Rock_") || name.StartsWith("Snowman") || name.StartsWith("Snow_Drift") ||
            name.StartsWith("Grass_") || name.StartsWith("Flower_");

        static void SimplifyDeparture(GameObject[] roots, Transform anchor, Scene scene)
        {
            var sheds = roots.Where(r => r.name.StartsWith("Bumblewick · opstelspoor")).ToArray();
            var yardRails = new HashSet<TrackPiece>(sheds.SelectMany(r => r.GetComponentsInChildren<TrackPiece>(true)));
            if (roots.Where(r => !sheds.Contains(r)).SelectMany(r => r.GetComponentsInChildren<TrackPiece>(true))
                .Any(r => r.next && yardRails.Contains(r.next)))
                throw new InvalidOperationException("Een opstelspoor is aangesloten op de route van Eaglecrest.");
            foreach (var root in sheds) Object.DestroyImmediate(root);

            foreach (var root in roots.Where(g => g && Near(anchor.InverseTransformPoint(g.transform.position), 39f, 47f, 9f)))
            {
                if (root.name.StartsWith("Platform_") || root.name.StartsWith("Yard_") ||
                    YardProps.Any(name => root.name == name || root.name.StartsWith(name + " (")))
                    Object.DestroyImmediate(root);
            }

            var platform = new GameObject("Eaglecrest · perron");
            SceneManager.MoveGameObjectToScene(platform, scene);
            platform.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            for (int i = 0; i < 7; i++) PlacePlatform("Platform_Straight_6m", 13f - 6f * i);
            PlacePlatform("Platform_Stairs_End", 19f);
            PlacePlatform("Platform_Stairs_Start", -23f);

            var tools = roots.Where(g => g && (g.name.StartsWith("Prop_Wrench") || g.name == "Prop_TicketPunch"))
                .Where(g => Near(anchor.InverseTransformPoint(g.transform.position), 9f, 9f, 8f))
                .OrderBy(g => g.name, StringComparer.Ordinal).ToArray();
            for (int i = 0; i < tools.Length; i++)
            {
                tools[i].transform.position = anchor.TransformPoint(new Vector3(3.6f + .6f * i, 1.18f, 1.6f));
                PrefabUtility.RecordPrefabInstancePropertyModifications(tools[i].transform);
            }

            void PlacePlatform(string name, float x)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPath + name + ".prefab");
                var segment = (GameObject)PrefabUtility.InstantiatePrefab(prefab, platform.transform);
                segment.transform.SetLocalPositionAndRotation(new Vector3(x, 0f, 5.15f), Quaternion.Euler(0f, 180f, 0f));
                segment.AddComponent<ConductorChaos.Nature.IceSurface>();
            }
        }

        static void ShapeGround(Terrain terrain, Transform anchor)
        {
            var data = terrain.terrainData;
            Vector3 size = data.size, origin = terrain.transform.position;
            int end = data.heightmapResolution - 1;
            var bounds = new Bounds(anchor.TransformPoint(new Vector3(-52f, 0f, -56f)), Vector3.zero);
            foreach (float x in new[] { -52f, 52f })
                foreach (float z in new[] { -56f, 42f }) bounds.Encapsulate(anchor.TransformPoint(new Vector3(x, 0f, z)));
            int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - origin.x) / size.x * end), 0, end);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - origin.z) / size.z * end), 0, end);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - origin.x) / size.x * end), 0, end);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - origin.z) / size.z * end), 0, end);
            var heights = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = anchor.InverseTransformPoint(new Vector3(origin.x + size.x * x / end, anchor.position.y, origin.z + size.z * z / end));
                    float blend = (1f - Fade(27f, 52f, Mathf.Abs(p.x))) * (1f - Fade(34f, 56f, -p.z)) * (1f - Fade(18f, 42f, p.z));
                    float bank = Fade(7f, 31f, -p.z) * Mathf.Lerp(.5f, 15f, Fade(-10f, 27f, p.x));
                    float drift = Fade(5f, 20f, -p.z) * Mathf.PerlinNoise(p.x * .15f + 31f, p.z * .15f + 17f);
                    float height = (anchor.position.y - .035f + bank + drift - origin.y) / size.y;
                    ref float original = ref heights[z - z0, x - x0];
                    original = Mathf.Lerp(original, height, blend);
                }
            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }

        static float Fade(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        static void PlantPines(Transform landscape, Terrain terrain, Scene scene)
        {
            var pines = new (float x, float z, string model, float scale)[]
            {
                (-28f, -22f, "Medium", 1.12f), (-24f, -29f, "Small", .94f),
                (-17f, -26f, "Large", .88f), (-7f, -31f, "Medium", 1.1f),
                (1f, -23f, "Small", 1.12f), (9f, -32f, "Large", .87f),
                (17f, -29f, "Medium", 1.06f), (27f, -34f, "Medium", .90f)
            };
            foreach (var (x, z, model, scale) in pines)
            {
                var pine = AssetDatabase.LoadAssetAtPath<GameObject>(PinePath + model + ".fbx");
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(pine, scene);
                tree.transform.SetParent(landscape, false);
                Vector3 p = landscape.TransformPoint(new Vector3(x, 0f, z));
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - .04f;
                tree.transform.SetPositionAndRotation(p, landscape.rotation * Quaternion.Euler(0f, x * 19f, 0f));
                tree.transform.localScale = Vector3.one * scale;
                var trunk = tree.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, .81f, 0f);
                trunk.height = 1.65f;
                trunk.radius = .23f;
            }
        }
    }
}
