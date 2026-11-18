using System;
using System.Linq;
using ConductorChaos.Art;
using ConductorChaos.Nature;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ConductorChaos.EditorTools
{
    internal static class WinterServiceAuthoring
    {
        const string Models = "Assets/Art/Models/Environment/";
        internal const string PlacedRoot = "Winter · sneeuw en stammen op het spoor";

        internal static void Apply(Scene scene, bool winter)
        {
            var transforms = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            foreach (var t in transforms)
            {
                if (t.name.StartsWith("Log_Fallen_", StringComparison.Ordinal))
                    MakeMovable(t.gameObject, 34f, true);
                else if (t.name.StartsWith("Omgevallen stam ·", StringComparison.Ordinal))
                    MakeMovable(t.gameObject, 34f, true);
                else if (t.name.StartsWith("Opgewaaide sneeuw ·", StringComparison.Ordinal))
                    MakeMovable(t.gameObject, 7f, true);
                else if (t.name.StartsWith("Rowboat_01", StringComparison.Ordinal))
                    MakeBoat(t.gameObject);
                else if (t.TryGetComponent<Rigidbody>(out var body) &&
                         (t.name.StartsWith("Sneeuwlawine", StringComparison.Ordinal) ||
                          t.name.StartsWith("Stammen ", StringComparison.Ordinal)))
                {
                    body.mass = winter ? 7f : 34f;
                    MarkLow(t.gameObject);
                }
            }
            if (!winter) return;
            var run = transforms.Select(t => t.GetComponent<ServiceRun>()).First(r => r);
            var season = transforms.Select(t => t.GetComponent<WinterLevel>()).First(r => r);
            var winterRoot = season.transform.parent;
            var settings = new SerializedObject(season);
            settings.FindProperty("openDoorFactor").floatValue = 1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            EnsureWarmHall(run.Arrival.transform);
            AddIcicles(run.Departure.transform.Find(CliffLevelBuilder.EaglecrestBuilding), run.Arrival.transform,
                transforms.FirstOrDefault(t => t.name == "Sneeuwpas · spoortunnel"));
            AddIceBoat(winterRoot, transforms);
            if (transforms.Any(t => t.name == PlacedRoot)) return;

            var parent = new GameObject(PlacedRoot).transform;
            SceneManager.MoveGameObjectToScene(parent.gameObject, scene);
            var route = transforms.Single(t => t.name == "Spoor · route");
            PlaceObstacle(route, parent, "climb1", .56f, "Winter/Snow_Pile_Track", "Opgewaaide sneeuw · flank", .94f, 7f);
            PlaceObstacle(route, parent, "descent", .77f, "Wood/Log_Fallen_01", "Omgevallen stam · afdaling", .92f, 34f);
            PlaceObstacle(route, parent, "finalClimb", .49f, "Winter/Snow_Pile_Track", "Opgewaaide sneeuw · boven het dal", 1f, 7f);
        }

        static void PlaceObstacle(Transform route, Transform parent, string tag, float fraction, string asset, string name, float scale, float mass)
        {
            var pieces = route.Cast<Transform>().Where(t => t.name.EndsWith(" · " + tag, StringComparison.Ordinal))
                .Select(t => t.GetComponent<TrackPiece>()).Where(t => t && t.HasSockets).ToArray();
            if (pieces.Length == 0) throw new InvalidOperationException($"Spoorsectie {tag} ontbreekt.");
            var at = pieces[Mathf.FloorToInt(pieces.Length * fraction)];
            at.Evaluate(.48f, out var rail, out var direction);
            var go = Spawn(asset, parent, name);
            go.transform.localScale = Vector3.one * scale;
            go.transform.SetPositionAndRotation(rail + Vector3.up * .08f,
                Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, mass >= 30f ? 7f : 19f, 0f));
            MakeMovable(go, mass, true);
        }

        static void AddIceBoat(Transform winterRoot, Transform[] transforms)
        {
            if (transforms.Any(t => t.name == "Roeiboot · ijsmeer")) return;
            var lake = transforms.FirstOrDefault(t => t.name.StartsWith("Frozen_Lake", StringComparison.Ordinal));
            if (!lake) return;
            var boat = Spawn("Water/Rowboat_01", winterRoot, "Roeiboot · ijsmeer");
            boat.transform.SetPositionAndRotation(lake.TransformPoint(new Vector3(-2.65f, .22f, -.65f)),
                lake.rotation * Quaternion.Euler(0f, 24f, 0f));
            MakeBoat(boat);
        }

        static void EnsureWarmHall(Transform station)
        {
            const string name = "Warme stationshal · Frostbury";
            if (station.Find(name)) return;
            var zone = new GameObject(name);
            zone.transform.SetParent(station, false);
            var area = zone.AddComponent<BoxCollider>();
            area.isTrigger = true;
            area.center = new Vector3(0f, 2.07f, -2.60f);
            area.size = new Vector3(5.9f, 2.1f, 4.9f);
            zone.AddComponent<WarmZone>();
        }

        static void AddIcicles(Transform departure, Transform arrival, Transform tunnel)
        {
            foreach (var station in new[] { departure, arrival })
            {
                if (!station || station.Find("IJspegels · linkerdakrand")) continue;
                var roof = station.Find("Roof");
                if (!roof || !roof.TryGetComponent<MeshFilter>(out var filter)) continue;
                var bounds = filter.sharedMesh.bounds;
                Strip(station, "IJspegels · linkerdakrand", new Vector3(bounds.min.x + .13f, bounds.min.y + .16f, -.89f), 90f);
                Strip(station, "IJspegels · rechterdakrand", new Vector3(bounds.max.x - .13f, bounds.min.y + .16f, -1.44f), 90f);
            }
            if (!tunnel || tunnel.Find("IJspegels · tunnelinrit")) return;
            Strip(tunnel, "IJspegels · tunnelinrit", new Vector3(-1f, 5.93f, -.23f), 0f);
            Strip(tunnel, "IJspegels · tunneluitrit", new Vector3(1f, 5.93f, 60.23f), 180f);

            void Strip(Transform parent, string name, Vector3 position, float yaw)
            {
                var strip = Spawn("Winter/Icicles_2m", parent, name);
                strip.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                strip.transform.localScale = new Vector3(1f, .65f, .85f);
            }
        }

        static GameObject Spawn(string asset, Transform parent, string name)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}{asset}.fbx");
            if (!model) throw new InvalidOperationException($"Model ontbreekt: {asset}");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.name = name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            return go;
        }

        static void MakeBoat(GameObject boat)
        {
            if (boat.TryGetComponent<WaterBob>(out var bob)) Object.DestroyImmediate(bob);
            MakeMovable(boat, 26f, false);
            if (!boat.GetComponent<RowboatFloat>()) boat.AddComponent<RowboatFloat>();
        }

        static void MakeMovable(GameObject go, float mass, bool low)
        {
            if (go.TryGetComponent<Rigidbody>(out var existing))
            {
                if (low && go.TryGetComponent<BoxCollider>(out var oldBox) && !go.GetComponent<CapsuleCollider>())
                {
                    Object.DestroyImmediate(oldBox);
                    FitCollider(go, LocalBounds(go.transform), true);
                    if (go.name.StartsWith("Opgewaaide sneeuw ·", StringComparison.Ordinal) ||
                        go.name.StartsWith("Omgevallen stam ·", StringComparison.Ordinal))
                    {
                        existing.position += Vector3.up * .32f;
                        go.transform.position = existing.position;
                    }
                }
                return;
            }
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(go))
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (go.TryGetComponent<PaintedProp>(out var planted)) planted.followGround = false;
            if (go.isStatic) go.isStatic = false;
            foreach (var old in go.GetComponentsInChildren<Collider>(true)) old.enabled = false;
            FitCollider(go, LocalBounds(go.transform), low);
            var body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = .18f;
            body.angularDamping = .9f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetworkTransform>();
            go.AddComponent<NetworkRigidbody>();
            if (low) MarkLow(go);
        }

        static void FitCollider(GameObject go, Bounds bounds, bool low)
        {
            if (!low)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = Vector3.Max(bounds.size, Vector3.one * .12f);
                return;
            }
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 0;
            capsule.radius = Mathf.Max(.15f, Mathf.Min(bounds.extents.y, bounds.extents.z));
            capsule.height = Mathf.Max(bounds.size.x, capsule.radius * 2f);
            capsule.center = new Vector3(bounds.center.x, bounds.min.y + capsule.radius, bounds.center.z);
        }

        static void MarkLow(GameObject go)
        {
            if (!go.GetComponent<LowTrackObstacle>()) go.AddComponent<LowTrackObstacle>();
        }

        static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.sharedMesh) continue;
                var local = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var signs = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    bounds.Encapsulate(local.MultiplyPoint3x4(b.center + Vector3.Scale(signs, b.extents)));
                }
            }
            return bounds;
        }
    }
}
