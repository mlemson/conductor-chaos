using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Nature;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ConductorChaos.EditorTools.CampaignLevelBake;

namespace ConductorChaos.EditorTools
{
    internal sealed class CampaignStopBake : IProcessSceneWithReport
    {
        private const string StationAssets = "Assets/Art/Prefabs/Station/";
        private const string Materials = "Assets/Art/Materials/Environment/Station/";
        private const string ContainerName = "Tussenhaltes";

        public int callbackOrder => 20;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            string[] names = scene.name switch
            {
                ServiceRun.SecondLevelScene => new[] { "Kloofzicht" },
                ServiceRun.ThirdLevelScene => new[] { "Bergweide", "Windkam" },
                ServiceRun.FourthLevelScene => new[] { "Sneeuwzicht", "IJsdal" },
                ServiceRun.FifthLevelScene => new[] { "Beukenwacht" },
                ServiceRun.SixthLevelScene => new[] { "Bizonvlakte" },
                ServiceRun.SeventhLevelScene => new[] { "Windmolenput", "Mijnkamp" },
                _ => null
            };
            if (names == null) return;
            var roots = scene.GetRootGameObjects();
            if (roots.Any(root => root.name == ContainerName)) return;
            var routeRoot = roots.FirstOrDefault(root => root.name == "Spoor · route");
            var run = roots.SelectMany(root => root.GetComponentsInChildren<ServiceRun>()).FirstOrDefault();
            var ticketing = roots.SelectMany(root => root.GetComponentsInChildren<ServiceTickets>()).FirstOrDefault();
            if (!routeRoot || !run || !ticketing || !ticketing.BoardingApproach) return;

            var route = new List<TrackPiece>();
            foreach (Transform child in routeRoot.transform)
            {
                if (child.name.EndsWith(" · zijspoor") || !child.TryGetComponent<TrackPiece>(out var piece) || !piece.HasSockets) continue;
                if (child.TryGetComponent<TrackSwitch>(out var junction))
                    piece = junction.IsDiverging ? junction.Straight : junction.Diverge;
                if (piece) route.Add(piece);
            }
            if (route.Count < 3) return;
            float length = route.Sum(piece => piece.Length);
            var terrain = roots.SelectMany(root => root.GetComponentsInChildren<Terrain>()).FirstOrDefault();
            var container = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(container, scene);
            var boardingApproach = ticketing.BoardingApproach;
            var boardingAxis = boardingApproach.localPosition;
            boardingAxis.z = -1.30f;
            var departureRail = boardingApproach.parent.TransformPoint(boardingAxis);
            var departureForward = route[0].StartDirection;
            float doorSide = Mathf.Sign(Vector3.Dot(ticketing.BoardingApproach.position - departureRail,
                Vector3.Cross(Vector3.up, departureForward)));
            var exclusions = roots.SelectMany(root => root.GetComponentsInChildren<CampaignShortcut>()).Select(x => x.Switch.transform.position)
                .Concat(roots.SelectMany(root => root.GetComponentsInChildren<Yard.LiquidSource>())
                    .Where(x => x.name == "Yard_WaterTower").Select(x => x.transform.position)).ToArray();

            float last = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                float target = length * (i + 1) / (names.Length + 1);
                float limit = i == names.Length - 1 ? length - 95f : target + length / (2 * (names.Length + 1));
                if (!FindLocation(route, terrain, doorSide, Mathf.Max(95f, last + 120f), limit, target, exclusions,
                        out var rail, out var forward, out float distance))
                {
                    Debug.LogError($"[Tussenhaltes] Geen geschikte perronlocatie voor {names[i]} in {scene.name}.");
                    continue;
                }

                last = distance;
                BuildStop(container.transform, names[i], rail, forward, doorSide, terrain, scene.name == ServiceRun.FourthLevelScene);
                Sample(route, distance - 38f, out var approach, out var direction);
                Sign(container.transform, terrain, approach, direction, $"HALTE {names[i].ToUpperInvariant()}\nREIZIGERS EN VRACHT");
                Debug.Log($"[Tussenhaltes] {scene.name}: {names[i]} op {distance:F0}/{length:F0} m.");
            }
        }

        private static bool FindLocation(List<TrackPiece> route, Terrain terrain, float side, float earliest, float latest, float target,
            Vector3[] exclusions, out Vector3 rail, out Vector3 forward, out float bestDistance)
        {
            rail = forward = default;
            bestDistance = 0f;
            float traveled = 0f, best = float.PositiveInfinity;
            foreach (var piece in route)
            {
                float half = piece.Length * .5f;
                float distance = traveled + half;
                traveled += piece.Length;
                if (distance < earliest || distance > latest || !piece.name.Contains("Straight") ||
                    piece.name.Contains("tunnel") || piece.name.Contains("voorportaal")) continue;

                piece.Evaluate(.5f, out var center, out var direction);
                if (Mathf.Abs(direction.y) > .12f || Mathf.Abs(piece.TurnDegrees) > 5f ||
                    exclusions.Any(p => (p - center).sqrMagnitude < 46f * 46f)) continue;
                var along = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                var across = Vector3.Cross(Vector3.up, along) * side;
                float unevenness = 0f;
                bool fits = true;
                for (int end = -1; end <= 1; end++)
                {
                    float y = Ground(terrain, center + along * (end * 6f) + across * 3.8f, center.y - .5f);
                    if (y > center.y + .42f || y < center.y - 3.5f) { fits = false; break; }
                    unevenness += Mathf.Abs(y - center.y);
                }
                if (!fits) continue;
                float score = Mathf.Abs(distance - target) + unevenness * 24f + Mathf.Abs(direction.y) * 450f;
                if (score >= best) continue;
                best = score;
                rail = center;
                forward = along;
                bestDistance = distance;
            }
            return bestDistance > 0f;
        }

        private static void Sample(List<TrackPiece> route, float distance, out Vector3 position, out Vector3 direction)
        {
            foreach (var piece in route)
            {
                float length = piece.Length;
                if (length > 0f && distance <= length)
                {
                    piece.Evaluate(Mathf.Clamp01(distance / length), out position, out direction);
                    return;
                }
                distance -= length;
            }
            position = route[0].StartPosition;
            direction = route[0].StartDirection;
        }

        private static void BuildStop(Transform container, string name, Vector3 rail, Vector3 forward, float side, Terrain terrain, bool winter)
        {
            var station = new GameObject(name).transform;
            station.SetParent(container, false);
            station.SetPositionAndRotation(rail, Quaternion.LookRotation(forward * side));
            Vector3 right = station.right, along = station.forward;
            const float deckY = .59f;
            var wood = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Stn_WoodLight.mat");
            var dark = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Stn_Dark.mat");
            var yellow = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Stn_Yellow.mat");
            Box(station, "Perron", rail + right * 3.72f + Vector3.up * (deckY - .20f), station.rotation,
                new Vector3(4.05f, .40f, 15f), wood, true);
            Box(station, "Perronrand", rail + right * 1.90f + Vector3.up * (deckY + .005f), station.rotation,
                new Vector3(.16f, .015f, 14.75f), yellow, false);
            foreach (float post in new[] { -6f, -2f, 2f, 6f })
            {
                var footing = rail + right * 4.6f + along * post;
                float ground = Ground(terrain, footing, rail.y - .5f);
                float height = Mathf.Max(.2f, rail.y + deckY - .22f - ground);
                footing.y = ground + height * .5f;
                Box(station, "Perronsteun", footing, station.rotation, new Vector3(.30f, height, .30f), dark, true);
            }
            for (int i = 0; i < 3; i++)
                Box(station, "Perrontrede", rail + right * (5.7f + (i + 1) * .32f) + Vector3.up * (deckY - .12f - i * .16f),
                    station.rotation, new Vector3(.58f, .22f, 1.85f), wood, true);

            var shelter = AssetDatabase.LoadAssetAtPath<GameObject>(StationAssets + "Props/Prop_Shelter.prefab");
            if (shelter)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(shelter, station);
                instance.transform.SetLocalPositionAndRotation(new Vector3(4.82f, deckY, -2.7f), Quaternion.Euler(0f, -90f, 0f));
            }
            Sign(station, null, rail + right * 1.25f + along * 5f + Vector3.up * .65f, along, $"{name.ToUpperInvariant()}\nTUSSENHALTE");
            var waiting = new GameObject("Instap · post").transform;
            waiting.SetParent(station, false);
            waiting.localPosition = new Vector3(3.42f, deckY + .04f, 1f);

            var zone = new GameObject("Instapzone");
            zone.transform.SetParent(station, false);
            var platform = zone.AddComponent<BoxCollider>();
            platform.isTrigger = true;
            platform.center = new Vector3(3.65f, 1.22f, 0f);
            platform.size = new Vector3(4.36f, 2.62f, 14.7f);
            if (winter) zone.AddComponent<WarmZone>();
            var freight = new[] { "Prop_Crate", "Prop_Barrel", "Prop_CargoCrate" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(StationAssets + $"Props/{name}.prefab")).ToArray();
            if (freight.Any(prefab => !prefab || !prefab.GetComponent<Unity.Netcode.NetworkObject>() ||
                !prefab.GetComponent<Rigidbody>()))
                throw new BuildFailedException($"Vrachtprefabs ontbreken voor {station.name}.");
            station.gameObject.AddComponent<ServiceStop>().Configure(platform, waiting, freight);
        }
    }
}
