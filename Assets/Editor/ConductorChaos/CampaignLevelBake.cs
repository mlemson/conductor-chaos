using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using ConductorChaos.Yard;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    // Apply to the scene copies used by builds and Play Mode. Keep the authored LFS scenes intact.
    internal sealed class CampaignLevelBake : IProcessSceneWithReport
    {
        public int callbackOrder => 10;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (scene.name is not (ServiceRun.SecondLevelScene or ServiceRun.ThirdLevelScene or ServiceRun.FourthLevelScene or ServiceRun.FifthLevelScene)) return;
            if (scene.GetRootGameObjects().Any(root => root.name == "Dienstaanvullingen")) return;
            var pieces = Components<TrackPiece>(scene).Where(p => p.HasSockets).ToArray();
            var terrain = Components<Terrain>(scene).FirstOrDefault();
            var root = new GameObject("Dienstaanvullingen");
            SceneManager.MoveGameObjectToScene(root, scene);
            switch (scene.name)
            {
                case ServiceRun.SecondLevelScene:
                    AddIncidentSigns(scene, root.transform, terrain, false);
                    break;
                case ServiceRun.ThirdLevelScene:
                    AddShortcut(scene, root.transform, terrain);
                    break;
                case ServiceRun.FourthLevelScene:
                    AddIncidentSigns(scene, root.transform, terrain, true);
                    break;
                case ServiceRun.FifthLevelScene:
                    MoveWaterTower(scene, pieces, root.transform, terrain);
                    foreach (var piece in pieces.Where(x => Mathf.Abs(x.TurnDegrees) >= 25f &&
                        !pieces.Any(before => before.next == x && Mathf.Abs(before.TurnDegrees) >= 25f)))
                        Sign(root.transform, terrain, piece.StartPosition - piece.StartDirection * 14f, piece.StartDirection, "SCHERPE BOCHT\nGAS TERUG");
                    break;
            }
        }

        private static IEnumerable<T> Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>());

        private static void AddIncidentSigns(Scene scene, Transform root, Terrain terrain, bool snow)
        {
            var incidents = Components<ServiceIncidents>(scene).FirstOrDefault();
            if (!incidents) return;
            var config = new SerializedObject(incidents);
            var rockfalls = config.FindProperty("rockfalls");
            for (int i = 0; i < rockfalls.arraySize; i++)
            {
                var entry = rockfalls.GetArrayElementAtIndex(i);
                var site = entry.FindPropertyRelative("site").objectReferenceValue as Transform;
                var trigger = entry.FindPropertyRelative("trigger").objectReferenceValue as BoxCollider;
                if (site && trigger) Sign(root, terrain, trigger.bounds.center - site.forward * 8f, site.forward,
                    snow ? "SNEEUW\nSCHEP OF PLOEG" : "GEVAAR\nSTEENSLAG");
            }
            if (snow) return;
            var damages = config.FindProperty("damages");
            for (int i = 0; i < damages.arraySize; i++)
            {
                var point = damages.GetArrayElementAtIndex(i).FindPropertyRelative("repair").objectReferenceValue as RepairPoint;
                if (point && !point.GetComponent<TrackSwitch>() && point.TryGetComponent<TrackPiece>(out var piece) && piece.HasSockets)
                    Sign(root, terrain, piece.StartPosition - piece.StartDirection * 24f, piece.StartDirection, "REM\nSPOOR CONTROLEREN");
            }
        }

        private static void MoveWaterTower(Scene scene, TrackPiece[] pieces, Transform root, Terrain terrain)
        {
            var tower = Components<LiquidSource>(scene).FirstOrDefault(source => source.name == "Yard_WaterTower");
            var track = pieces.Where(x => x.name.EndsWith(" · beukenlaan") && x.name.Contains("Straight") &&
                x.next && x.next.name.EndsWith(" · beukenlaan") && x.next.name.Contains("Straight")).Skip(1).FirstOrDefault();
            if (!tower || !track) return;
            var nodes = tower.GetComponentsInChildren<Transform>(true);
            var pipe = nodes.FirstOrDefault(t => t.name == "Move_Spout");
            var down = nodes.FirstOrDefault(t => t.name == "Pose_Spout_Down");
            var model = new SerializedObject(tower);
            var nozzle = (Transform)model.FindProperty("outlet").objectReferenceValue;
            if (!pipe || !down || !nozzle) return;
            track.Evaluate(.5f, out var rail, out var forward);
            var startRotation = pipe.localRotation;
            pipe.localRotation = down.localRotation;
            Vector3 aim = Vector3.ProjectOnPlane(tower.OutletPosition - tower.transform.position, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            if (aim.sqrMagnitude < 1f) { pipe.localRotation = startRotation; return; }
            float left = Ground(terrain, rail - side * aim.magnitude, rail.y);
            float right = Ground(terrain, rail + side * aim.magnitude, rail.y);
            if (Mathf.Abs(left - rail.y) < Mathf.Abs(right - rail.y)) side = -side;
            tower.transform.rotation = Quaternion.FromToRotation(aim, -side) * tower.transform.rotation;
            Vector3 basePoint = tower.transform.position + Vector3.ProjectOnPlane(rail - tower.OutletPosition, Vector3.up);
            float ground = Ground(terrain, basePoint, rail.y - .5f);
            const float tankInletAboveRail = TrackPiece.RailToTrainPivot + 2.73f;
            float outletAboveBase = tower.OutletPosition.y - tower.transform.position.y;
            basePoint.y = Mathf.Max(ground, rail.y + tankInletAboveRail + .48f - outletAboveBase);
            tower.transform.position = basePoint;
            Vector3 velocity = nozzle.TransformDirection(model.FindProperty("localDirection").vector3Value.normalized) * model.FindProperty("exitSpeed").floatValue;
            float gravity = -Physics.gravity.y;
            float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * (nozzle.position.y - rail.y - tankInletAboveRail))) / gravity;
            tower.transform.position += Vector3.ProjectOnPlane(rail - (nozzle.position + velocity * time), Vector3.up);
            float rise = basePoint.y - ground;
            if (rise > .1f)
            {
                var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/Station/M_Stn_WoodLight.mat");
                Box(root, "Bordes watertoren", tower.transform.position - Vector3.up * rise * .5f,
                    tower.transform.rotation, new Vector3(5.3f, rise, 5.3f), timber, true);
                for (int step = 1; step <= 3; step++)
                    Box(root, "Trede watertoren", tower.transform.position + side * (2.5f + step * .28f) - Vector3.up * (rise * step / 4f),
                        Quaternion.LookRotation(side), new Vector3(1.4f, rise * (1f - step / 4f), .56f), timber, true);
            }
            Debug.Log($"[Campaign] Watertoren bij {track.name}, uitloop {tower.OutletPosition.y - rail.y:F1} m boven de rail.", tower);
            pipe.localRotation = startRotation;
            Sign(root, terrain, rail - forward * 14f, forward, "WATERSTOP\nKETELWAGON");
        }

        private static void AddShortcut(Scene scene, Transform root, Terrain terrain)
        {
            TrackSwitch bestSwitch = null;
            TrackPiece bestEntry = null, bestPrevious = null, bestMerge = null;
            List<Vector3> bestPoints = null;
            float saving = 3f, bestLength = 0f, bestSafeDistance = 0f;
            foreach (string name in new[] { "kamwissel", "uitzichtwissel", "goederenwissel" })
            {
                var junction = Components<TrackSwitch>(scene).FirstOrDefault(x => x.name.EndsWith(" · " + name));
                if (!junction || !junction.Straight || !junction.Diverge) continue;
                var safe = junction.IsDiverging ? junction.Straight : junction.Diverge;
                var fast = junction.IsDiverging ? junction.Diverge : junction.Straight;
                float safeDistance = safe.Length;
                for (var previous = safe; previous.next && safeDistance < 250f; previous = previous.next)
                {
                    var merge = previous.next;
                    if (merge.name.Contains("Broken") || merge.name.Contains("Switch")) break;
                    if (!merge.name.Contains("Bridge") && safeDistance > 44f &&
                        TryShortcut(fast, merge, terrain, safeDistance, out var points, out float length) &&
                        safeDistance - fast.Length - length > saving)
                    {
                        saving = safeDistance - fast.Length - length;
                        bestSwitch = junction;
                        bestEntry = fast;
                        bestPrevious = previous;
                        bestMerge = merge;
                        bestPoints = points;
                        bestLength = fast.Length + length;
                        bestSafeDistance = safeDistance;
                    }
                    safeDistance += merge.Length;
                }
            }
            if (!bestSwitch)
            {
                Debug.LogWarning("[Campaign] Geen berijdbaar korter spoor met vrije aansluiting gevonden in level 3.");
                return;
            }
            var siding = bestEntry.next;
            while (siding && siding.name.EndsWith(" · zijspoor"))
            {
                siding.gameObject.SetActive(false);
                siding = siding.next;
            }
            var piece = BuildTrack(root, bestPoints, bestPrevious);
            bestEntry.next = piece;
            piece.next = bestMerge;
            bestSwitch.gameObject.AddComponent<CampaignShortcut>().Configure(bestSwitch, bestEntry, bestPrevious, piece, bestMerge);
            string setting = bestEntry == bestSwitch.Diverge ? "AFBUIGEND" : "RECHTDOOR";
            Sign(root, terrain, bestEntry.StartPosition - bestEntry.StartDirection * 12f, bestEntry.StartDirection, $"{setting}: SNELROUTE\nGEVAAR • GAS TERUG");
            Debug.Log($"[Campaign] Hoogvlakte: snelroute {bestLength:F1} m, hoofdspoor {bestSafeDistance:F1} m bij {bestSwitch.name}.");
        }

        private static bool TryShortcut(TrackPiece entry, TrackPiece merge, Terrain terrain, float safeDistance,
            out List<Vector3> points, out float length)
        {
            points = new List<Vector3>();
            length = 0f;
            Vector3 a = entry.EndPosition, d = merge.StartPosition;
            float gap = Vector3.Distance(a, d);
            if (gap < 14f || gap + entry.Length >= safeDistance - 3f) return false;
            foreach (float reach in new[] { .35f, .5f, .65f })
            {
                Vector3 b = a + entry.EndDirection * gap * reach, c = d - merge.StartDirection * gap * reach;
                points.Clear();
                points.Add(a);
                length = 0f;
                bool clear = true;
                for (int i = 1; i <= 36; i++)
                {
                    float t = i / 36f, u = 1f - t;
                    Vector3 p = u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
                    Vector3 step = p - points[points.Count - 1];
                    if (p.y < Ground(terrain, p, p.y - .5f) + .04f || Mathf.Abs(step.y) > step.magnitude * .21f)
                    { clear = false; break; }
                    points.Add(p);
                    length += step.magnitude;
                }
                if (clear && entry.Length + length < safeDistance - 3f) return true;
            }
            return false;
        }

        private static TrackPiece BuildTrack(Transform root, List<Vector3> points, TrackPiece sample)
        {
            var path = new GameObject("Spoor · snelle doorsteek").transform;
            path.SetParent(root, false);
            Transform Point(string name, Vector3 p)
            {
                var t = new GameObject(name).transform;
                t.SetParent(path);
                t.position = p;
                return t;
            }
            Vector3 first = points[0], last = points[points.Count - 1];
            var start = Point("Socket_Start", first);
            var startForward = Point("Socket_Start_Fwd", first + (points[1] - first).normalized);
            var end = Point("Socket_End", last);
            var endForward = Point("Socket_End_Fwd", last + (last - points[points.Count - 2]).normalized);
            var waypoints = points.Select((p, i) => Point("Path_" + i.ToString("00"), p)).ToArray();
            var track = path.gameObject.AddComponent<TrackPiece>();
            track.start = start; track.startForward = startForward; track.end = end; track.endForward = endForward;
            track.pathPoints = waypoints;
            var materials = sample.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Where(m => m).ToArray();
            var rail = materials.FirstOrDefault(m => m.name.Contains("Rail")) ?? materials.FirstOrDefault();
            var timber = materials.FirstOrDefault(m => m.name.Contains("Sleeper")) ?? rail;
            for (int i = 1; i < points.Count; i++)
            {
                Vector3 dir = points[i] - points[i - 1];
                var rotation = Quaternion.LookRotation(dir);
                Vector3 center = (points[i] + points[i - 1]) * .5f;
                Box(path, "Dwarsligger", center - Vector3.up * .18f, rotation, new Vector3(2.35f, .28f, dir.magnitude + .04f), timber, true);
                for (int side = -1; side <= 1; side += 2)
                    Box(path, "Rail", center + rotation * Vector3.right * (.74f * side), rotation,
                        new Vector3(.09f, .10f, dir.magnitude + .025f), rail, false);
            }
            return track;
        }

        private static float Ground(Terrain terrain, Vector3 p, float fallback) => terrain
            ? terrain.SampleHeight(p) + terrain.transform.position.y : fallback;

        private static void Sign(Transform root, Terrain terrain, Vector3 rail, Vector3 forward, string message)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return;
            Vector3 place = rail + Vector3.Cross(Vector3.up, forward) * 3.2f;
            place.y = Ground(terrain, place, rail.y - .5f);
            var rotation = Quaternion.LookRotation(forward);
            var boardMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/Station/M_Stn_Yellow.mat");
            var postMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/Station/M_Stn_Dark.mat");
            Box(root, "Seinpaal", place + Vector3.up, rotation, new Vector3(.14f, 2f, .14f), postMat, false);
            var board = Box(root, message.Replace('\n', ' '), place + Vector3.up * 2f, rotation, new Vector3(2.5f, .82f, .09f), boardMat, false);
            var label = new GameObject("Seintekst").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.SetPositionAndRotation(board.transform.position - forward * .052f, rotation);
            label.text = message;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 80;
            label.characterSize = .035f;
            label.color = new Color(.11f, .07f, .045f);
        }

        private static GameObject Box(Transform root, string name, Vector3 position, Quaternion rotation,
            Vector3 size, Material material, bool solid)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root, false);
            box.transform.SetPositionAndRotation(position, rotation);
            box.transform.localScale = size;
            box.GetComponent<BoxCollider>().enabled = solid;
            if (material) box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
