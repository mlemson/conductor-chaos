// Conductor Chaos — bouwt level 2 'Langs de kloof' (issue #224, concept 'Cliffside Run'). Menu: Tools > Conductor Chaos >
// Level 2 bouwen. Bestaat ServiceLevel02 nog niet, dan staat er één melding in de Console (geen pop-up).
//
// Werkwijze (herhaalbaar; elke keer een verse kopie):
//   1. ServiceLevel01 wordt gekopieerd naar ServiceLevel02: trein, perrons, reizigers, vracht, oma, wissel, bel en
//      gereedschap zijn daarmee al goed ingesteld. Decor en de oude route ertussen gaan eruit.
//   2. Een nieuwe route wordt stuk voor stuk aan de spoorkit gekoppeld (sockets): door het bos, klimmen door een
//      insnijding, dan over de rand van een diepe kloof (hek aan de kloofkant, rotswand aan de andere kant), een
//      kapotte rail vlak voor een stenen boogbrug, over de kloof, weer langs de rand, afdalen door het bos naar het
//      eindstation (dat met stopvak, vrachtvakken en oma's eindpunt meeschuift).
//   3. De kloof slingert als een S onder de brug door: eerst rechts van het spoor, dan links. Hij is ~31 m diep, met
//      getrapte rotswanden (één CliffMass, 13 treden van 2,4 m), richels, rotspilaren, een rivier op de bodem en
//      watervallen. Terrein (Unity Terrain met de gestileerde shader) ligt vlak op randhoogte en loopt verderop op
//      naar heuvels rond de vallei; bergen en wolken in de verte.
//   4. Bos, rotsen, planten op het land, op richels en op de kloofbodem, onder PaintedProps (Prop Painter).
//   5. Gameplay: alle opdrachten tegelijk + 'Herstel de brugrail', incidenten (steenlawines, spoorschade), langere tijd.
// Alles wat later in Unity wordt aangepast, blijft gewoon in de scène. Opnieuw bouwen overschrijft ServiceLevel02.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ConductorChaos.Art;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class Level02Builder
    {
        const string SourceScene = "Assets/Scenes/" + ServiceRun.FirstLevelScene + ".unity";
        const string TargetScene = "Assets/Scenes/" + ServiceRun.SecondLevelScene + ".unity";
        const string DataFolder = "Assets/Scenes/ServiceLevel02_Data";
        const string TrackDir = "Assets/Art/Prefabs/Track";
        const string EnvDir = "Assets/Art/Models/Environment";
        const string PropDir = "Assets/Art/Prefabs/Station/Props";
        const string OfferedKey = "ConductorChaos.Level02.Offered";

        const float RailTop = 0.43f;            // sockets liggen op de bovenkant van de rails
        const float StepHeight = 2.4f;          // CliffMass-trede
        const float TerrainBottom = -34f;       // laagste punt dat het terrein kan hebben (wereld-y)
        const float TerrainHeight = 96f;
        const int CanyonTiers = 13;             // kloofdiepte in treden van StepHeight (~31 m)
        const float RiverHalf = 3.4f;           // halve breedte van de rivierbedding

        static Level02Builder()
        {
            EditorApplication.delayCall += OfferOnce;
        }

        // Geen pop-up (die zou je werk onderbreken): één melding in de Console zolang level 2 nog niet gebouwd is.
        static void OfferOnce()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) || !AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene)) return;
            if (SessionState.GetBool(OfferedKey, false)) return;
            SessionState.SetBool(OfferedKey, true);
            Debug.Log("[Level 2] Nog niet gebouwd. Kies Tools > Conductor Chaos > Level 2 bouwen wanneer het je uitkomt " +
                      "(kopieert ServiceLevel01 naar ServiceLevel02 en maakt daar route, terrein, kliffen, bos en opdrachten).");
        }

        [MenuItem("Tools/Conductor Chaos/Level 2 bouwen")]
        public static void BuildMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) &&
                !EditorUtility.DisplayDialog("Level 2 opnieuw bouwen",
                    "ServiceLevel02 bestaat al. Opnieuw bouwen maakt een verse kopie van level 1 en overschrijft je " +
                    "aanpassingen aan level 2. Doorgaan?", "Opnieuw bouwen", "Annuleren"))
                return;
            Build();
        }

        // ================================================================== hoofdroutine
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Level 2", "Stop eerst de Play-modus.", "OK");
                return;
            }
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene))
            {
                EditorUtility.DisplayDialog("Level 2", SourceScene + " ontbreekt; level 2 wordt daarvan afgeleid.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var log = new StringBuilder();
            try
            {
                Progress("Scène kopiëren", 0.02f);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene)) AssetDatabase.DeleteAsset(TargetScene);
                if (AssetDatabase.IsValidFolder(DataFolder)) AssetDatabase.DeleteAsset(DataFolder);
                if (!AssetDatabase.CopyAsset(SourceScene, TargetScene)) throw new Exception("Kopiëren van level 1 mislukt.");
                AssetDatabase.CreateFolder("Assets/Scenes", "ServiceLevel02_Data");
                var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

                var level = new LevelContext(scene, log);
                level.Run();

                Progress("Opslaan", 0.96f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                RefreshNetworkIds(scene, log);
                EditorSceneManager.SaveScene(scene);
                AddToBuildSettings(log);
                AssetDatabase.SaveAssets();
                Debug.Log("[Level 2] Gebouwd.\n" + log);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Level 2 gebouwd",
                    "ServiceLevel02 staat klaar en is toegevoegd aan de buildlijst.\n\n" +
                    "Speel level 1 uit en kies 'Naar level 2', of open ServiceLevel02 en verfijn met de Prop Painter " +
                    "en de Klif Massa Painter. Details staan in de Console.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                Debug.LogError("[Level 2] Bouwen afgebroken.\n" + log);
                EditorUtility.DisplayDialog("Level 2", "Bouwen is mislukt: " + e.Message + "\n\nDetails staan in de Console.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void Progress(string what, float t) =>
            EditorUtility.DisplayProgressBar("Level 2 bouwen", what, t);

        static void AddToBuildSettings(StringBuilder log)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == TargetScene)) return;
            int after = scenes.FindIndex(s => s.path == SourceScene);
            scenes.Insert(after >= 0 ? after + 1 : scenes.Count, new EditorBuildSettingsScene(TargetScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("Toegevoegd aan de buildlijst.");
        }

        // In-scene NetworkObjects hebben een unieke GlobalObjectIdHash nodig. Nieuwe en gekopieerde objecten krijgen die
        // pas na een OnValidate in de opgeslagen scène; daarom hier expliciet, en daarna controleren op dubbelen.
        static void RefreshNetworkIds(Scene scene, StringBuilder log)
        {
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkObject>(true)).ToArray();
            var validate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            foreach (var no in objects)
            {
                try { validate?.Invoke(no, null); } catch (Exception) { /* oudere/nieuwere NGO: Unity doet het bij opslaan */ }
                EditorUtility.SetDirty(no);
            }
            var seen = new Dictionary<long, NetworkObject>();
            int duplicates = 0;
            foreach (var no in objects)
            {
                var prop = new SerializedObject(no).FindProperty("GlobalObjectIdHash");
                if (prop == null) continue;
                long hash = prop.longValue;
                if (hash == 0 || seen.ContainsKey(hash)) duplicates++;
                else seen[hash] = no;
            }
            log.AppendLine($"NetworkObjects: {objects.Length}" + (duplicates > 0 ? $", {duplicates} zonder unieke id (sla de scène nog eens op)" : ", alle ids uniek."));
            if (duplicates > 0) Debug.LogWarning($"[Level 2] {duplicates} NetworkObject(s) zonder unieke GlobalObjectIdHash. Open ServiceLevel02 en sla hem nog één keer op.");
        }

        // ================================================================== één bouwbeurt
        sealed class Sample
        {
            public Vector3 rail;        // hartlijn op railhoogte
            public Vector3 dir;
            public float arc;           // afstand langs de route (alleen voor de nieuwe route; anders -1)
            public string tag;
            public bool bridge;
            public float Pivot => rail.y - RailTop;       // maaiveld van het spoor
        }

        sealed class Placed
        {
            public TrackPiece piece;
            public string tag;
            public float arcStart, arcEnd;
        }

        sealed class LevelContext
        {
            readonly Scene scene;
            readonly StringBuilder log;
            readonly Random rng = new Random(2026_11_15);

            ServiceRun run;
            TrainDrive train;
            ServiceStation departure, arrival;
            readonly List<Placed> route = new List<Placed>();
            readonly List<Sample> samples = new List<Sample>();
            SampleGrid grid;
            readonly List<Bounds> stationAreas = new List<Bounds>();
            Terrain terrain;
            TerrainData data;
            Transform propsRoot;

            // kloof
            Vector3 bridgeStart, bridgeEnd;
            float deckPivot, rimY, originY, floorY, waterY;
            Canyon canyon;
            CliffMass canyonWalls;
            float RimTop => originY + CanyonTiers * StepHeight;

            public LevelContext(Scene scene, StringBuilder log)
            {
                this.scene = scene;
                this.log = log;
            }

            public void Run()
            {
                Progress("Level 1 inlezen", 0.05f);
                FindCore();
                Progress("Oud decor opruimen", 0.08f);
                ClearScenery();
                Progress("Nieuwe route leggen", 0.12f);
                var arrivalGroup = SplitTrack(out var routeStart, out var arrivalFirst);
                LayRoute(routeStart);
                MoveArrival(arrivalGroup, arrivalFirst);
                Progress("Spoor bemonsteren", 0.2f);
                SampleTrack();
                MeasureStations(arrivalGroup);
                Progress("Kloof uitzetten", 0.22f);
                PlanCanyon();
                Progress("Terrein maken", 0.25f);
                BuildTerrain();
                Progress("Kloofwanden", 0.55f);
                propsRoot = Root("PaintedProps").transform;
                BuildCanyonWalls();
                BuildPillars();
                Progress("Kliffen", 0.6f);
                var cliffA = BuildCliff("Klifwand langs de klim", new[] { "climb", "rimA" }, left: true, 5.6f, 13.5f, 4.8f, false);
                var cliffB = BuildCliff("Klifwand na de brug", new[] { "rimB" }, left: false, 5.8f, 12f, 4.4f, false);
                BuildScatteredCliffs();
                Physics.SyncTransforms();
                BuildWaterfalls();
                BuildFences();
                Progress("Gameplay", 0.65f);
                SetupGameplay(cliffA, cliffB);
                Progress("Bos en rotsen", 0.72f);
                PlantNature();
                Progress("Water en achtergrond", 0.9f);
                BuildRiver();
                BuildBackdrop();
                Selection.activeGameObject = run.gameObject;
            }

            // -------------------------------------------------------------- level 1 inlezen
            void FindCore()
            {
                run = InScene<ServiceRun>().FirstOrDefault() ?? throw new Exception("Geen ServiceRun in de kopie van level 1.");
                var so = new SerializedObject(run);
                train = so.FindProperty("train").objectReferenceValue as TrainDrive;
                departure = so.FindProperty("departure").objectReferenceValue as ServiceStation;
                arrival = so.FindProperty("arrival").objectReferenceValue as ServiceStation;
                if (!train || !departure || !arrival) throw new Exception("De spelregels van level 1 missen trein of stations.");
                log.AppendLine($"Trein {train.name}, vertrek {departure.StationName}, aankomst {arrival.StationName}.");
            }

            IEnumerable<T> InScene<T>() where T : Component =>
                scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));

            void ClearScenery()
            {
                int removed = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    string n = root.name;
                    // decor van level 1 (ook een eigen landschap/terrein): level 2 krijgt nieuw decor
                    bool decor = n.StartsWith("Tree_") || n.StartsWith("Hill_Grass") || n.StartsWith("Farm_") || n == "Grasland" ||
                                 n.StartsWith("Bush_") || n.StartsWith("Rock_") || n == "PaintedProps" || n.StartsWith("Landroute") ||
                                 n.StartsWith("Cloud_") || n.StartsWith("Mountain_") || n.StartsWith("Grass_") || n.StartsWith("Flower_") ||
                                 root.GetComponentInChildren<Terrain>(true);
                    bool parkedTrain = n.Contains("_Blockout") && root != train.gameObject && !IsConsist(root);
                    if (!decor && !parkedTrain) continue;
                    Object.DestroyImmediate(root);
                    removed++;
                }
                log.AppendLine($"Decor en geparkeerde treinen van level 1 verwijderd: {removed} objecten.");
            }

            bool IsConsist(GameObject go)
            {
                var body = go.GetComponent<Rigidbody>();
                return body && train.HasConfiguredWagon(body);
            }

            static Vector3 StopPoint(ServiceStation station)
            {
                var zone = station.TrainStop;
                return zone ? zone.transform.TransformPoint(zone.center) : station.transform.position;
            }

            static float Flat2(Vector3 a, Vector3 b) => HorizontalDistance(a, b.x, b.z);

            // Spoor in drie delen: rond het vertrek blijft het liggen (met wissel en zijspoor), rond het eindstation schuift
            // het mee, alles ertussen (de route van level 1, hoe die ook loopt) gaat weg.
            List<Transform> SplitTrack(out TrackPiece routeStart, out TrackPiece arrivalFirst)
            {
                Vector3 departureStop = StopPoint(departure), arrivalStop = StopPoint(arrival);
                var keep = new List<TrackPiece>();
                var arrivalPieces = new List<TrackPiece>();
                var doomed = new HashSet<GameObject>();
                foreach (var piece in InScene<TrackPiece>().ToList())
                {
                    if (!piece) continue;
                    if (!piece.HasSockets) piece.FindSockets();
                    if (!piece.HasSockets) continue;
                    var root = piece.transform.root;
                    bool rootLevel = root == piece.transform || TrackSwitch.For(piece) || root.GetComponent<TrackSwitch>();
                    if (rootLevel && Flat2(departureStop, piece.StartPosition) < 55f) keep.Add(piece);
                    else if (root == piece.transform && Flat2(arrivalStop, piece.StartPosition) < 60f) arrivalPieces.Add(piece);
                    else doomed.Add(root.gameObject);
                }
                foreach (var piece in keep.Concat(arrivalPieces)) doomed.Remove(piece.transform.root.gameObject);
                foreach (var go in doomed) if (go) Object.DestroyImmediate(go);
                keep.RemoveAll(p => !p);
                arrivalPieces.RemoveAll(p => !p);

                bool Joined(Vector3 point, IEnumerable<TrackPiece> pieces, bool atStart) =>
                    pieces.Any(q => q && (atStart ? (q.StartPosition - point).sqrMagnitude : (q.EndPosition - point).sqrMagnitude) < 0.3f * 0.3f);
                // het losse eind van het vertrekspoor (niet het zijspoor naar het stootblok)
                routeStart = keep
                    .Where(p => !p.name.Contains("BufferStop") && !p.name.Contains("Diverge") && !Joined(p.EndPosition, keep, true))
                    .OrderByDescending(p => Flat2(departureStop, p.EndPosition)).FirstOrDefault();
                // het eerste stuk van het aankomstspoor: zijn begin sluit op niets aan
                arrivalFirst = arrivalPieces
                    .Where(p => !p.name.Contains("BufferStop") && !Joined(p.StartPosition, arrivalPieces, false))
                    .OrderByDescending(p => Flat2(arrivalStop, p.StartPosition)).FirstOrDefault();
                if (!routeStart || !arrivalFirst) throw new Exception("Begin of einde van de route in level 1 niet gevonden.");

                // alles van het eindstation (station met stopvak/vrachtvakken als kinderen, perrons, sporen, stootblok)
                var group = scene.GetRootGameObjects()
                    .Where(r => r.transform.position.sqrMagnitude > 1f && Flat2(arrivalStop, r.transform.position) < 60f &&
                                !r.GetComponentInChildren<TrainDrive>())
                    .Select(r => r.transform).ToList();
                var arrivalRoot = arrival.transform.root;
                if (!group.Contains(arrivalRoot)) group.Add(arrivalRoot);
                log.AppendLine($"Route begint na {routeStart.name}; eindstation: {group.Count} objecten, {arrivalPieces.Count} spoorstukken.");
                return group;
            }

            // -------------------------------------------------------------- de route
            // rimA en rimB liggen in één rechte lijn met de brugrail en de brug: de kloof wordt daaromheen uitgezet.
            static readonly (string prefab, string tag)[] RouteSpec =
            {
                ("Track_Straight_12m", "meadow"), ("Track_Straight_12m", "meadow"),
                ("Track_Curve_R20_15_Left", "forest"),
                ("Track_Straight_12m", "forest"),
                ("Track_Ramp_Up_18m_Steep", "climb"), ("Track_Ramp_Up_18m_Steep", "climb"),
                ("Track_Straight_12m", "rimA"), ("Track_Straight_12m", "rimA"), ("Track_Straight_12m", "rimA"),
                ("Track_Straight_12m", "rimA"),
                ("Track_Broken_6m", "bridgeRail"),
                ("Track_Bridge_Viaduct_12m", "bridge"), ("Track_Bridge_Viaduct_12m", "bridge"),
                ("Track_Bridge_Viaduct_12m", "bridge"), ("Track_Bridge_Viaduct_12m", "bridge"),
                ("Track_Straight_12m", "rimB"), ("Track_Straight_12m", "rimB"), ("Track_Straight_12m", "rimB"),
                ("Track_Straight_12m", "rimB"),
                ("Track_Ramp_Down_12m", "descent"), ("Track_Ramp_Down_12m", "descent"),
                ("Track_Ramp_Down_12m", "descent"), ("Track_Ramp_Down_12m", "descent"),
                ("Track_Straight_12m", "forest"), ("Track_Straight_12m", "forest"),
                ("Track_Broken_6m", "damage"),
                ("Track_Straight_12m", "forest"),
                ("Track_Curve_R20_15_Right", "forest"),
                ("Track_Straight_12m", "meadow"), ("Track_Straight_12m", "meadow"),
            };

            void LayRoute(TrackPiece start)
            {
                var parent = Root("Spoor · kloofroute").transform;
                Vector3 p = start.EndPosition, d = start.EndDirection;
                TrackPiece previous = start;
                float arc = 0f;
                foreach (var (prefabName, tag) in RouteSpec)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackDir}/{prefabName}.prefab");
                    if (!prefab && AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackKitPrefabs.ModelDir}/{prefabName}.fbx"))
                    {
                        TrackKitPrefabs.Build();               // nieuw model (stenen brug) nog zonder prefab
                        prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackDir}/{prefabName}.prefab");
                    }
                    if (!prefab) throw new Exception($"Spoorstuk {prefabName} ontbreekt in {TrackDir}.");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    var piece = go.GetComponent<TrackPiece>();
                    if (!piece) throw new Exception($"{prefabName} heeft geen TrackPiece.");
                    if (!piece.HasSockets) piece.FindSockets();
                    // alleen om de verticale as draaien (de stukken blijven rechtop), daarna begin op het vorige eind
                    float yaw = Vector3.SignedAngle(Flat(piece.StartDirection), Flat(d), Vector3.up);
                    go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;
                    go.transform.position += p - piece.StartPosition;
                    go.transform.SetParent(parent, true);
                    go.name = $"{prefabName} · {tag}";
                    previous.next = piece;
                    Dirty(previous);
                    float length = piece.Length;
                    route.Add(new Placed { piece = piece, tag = tag, arcStart = arc, arcEnd = arc + length });
                    arc += length;
                    p = piece.EndPosition;
                    d = piece.EndDirection;
                    previous = piece;
                }
                log.AppendLine($"Nieuwe route: {route.Count} spoorstukken, {arc:0} m, eindigt op hoogte {p.y - RailTop:0.00} m.");
            }

            void MoveArrival(List<Transform> group, TrackPiece arrivalFirst)
            {
                var last = route[route.Count - 1].piece;
                Vector3 from = arrivalFirst.StartPosition, to = last.EndPosition;
                float yaw = Vector3.SignedAngle(Flat(arrivalFirst.StartDirection), Flat(last.EndDirection), Vector3.up);
                var turn = Quaternion.AngleAxis(yaw, Vector3.up);
                foreach (var t in group)
                {
                    if (!t) continue;
                    t.SetPositionAndRotation(to + turn * (t.position - from), turn * t.rotation);
                }
                last.next = arrivalFirst;
                Dirty(last);
                log.AppendLine($"Eindstation verplaatst naar ({to.x:0.0}, {to.z:0.0}), {yaw:0.0}° gedraaid.");
            }

            // Wijziging aan een (prefab-)object via code vastleggen, anders verdwijnt hij bij opslaan.
            static void Dirty(Object target)
            {
                EditorUtility.SetDirty(target);
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            static Vector3 Flat(Vector3 v)
            {
                v.y = 0f;
                return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
            }

            // -------------------------------------------------------------- spoor bemonsteren
            void SampleTrack()
            {
                var tagOf = route.ToDictionary(r => r.piece, r => r);
                foreach (var piece in InScene<TrackPiece>())
                {
                    if (!piece.HasSockets || piece.Length < 0.01f) continue;
                    tagOf.TryGetValue(piece, out var placed);
                    bool bridge = piece.name.Contains("Bridge");
                    int n = Mathf.Max(2, Mathf.CeilToInt(piece.Length / 0.75f));
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n;
                        piece.Evaluate(t, out var pos, out var dir);
                        samples.Add(new Sample
                        {
                            rail = pos, dir = Flat(dir), bridge = bridge, tag = placed?.tag ?? "station",
                            arc = placed != null ? Mathf.Lerp(placed.arcStart, placed.arcEnd, t) : -1f,
                        });
                    }
                }
                grid = new SampleGrid(samples, 8f);

                var bridges = route.Where(r => r.tag == "bridge").ToList();
                if (bridges.Count == 0) throw new Exception("Geen brug in de route.");
                bridgeStart = bridges[0].piece.StartPosition;
                bridgeEnd = bridges[bridges.Count - 1].piece.EndPosition;
                deckPivot = bridgeStart.y - RailTop;
                rimY = deckPivot - 0.15f;                                    // land langs de rand, net onder het spoorbed
                originY = rimY + 0.12f - CanyonTiers * StepHeight;          // kloofwand-top steekt 12 cm boven het land
                floorY = originY + 0.6f;                                    // wandvoeten zakken in de bodem
                waterY = floorY + 0.05f;
                log.AppendLine($"Brug van ({bridgeStart.x:0}, {bridgeStart.z:0}) tot ({bridgeEnd.x:0}, {bridgeEnd.z:0}), dek {deckPivot:0.0} m, " +
                               $"kloofbodem {floorY:0.0} m.");
            }

            void MeasureStations(List<Transform> arrivalGroup)
            {
                Bounds? Area(IEnumerable<Transform> roots)
                {
                    Bounds? b = null;
                    foreach (var r in roots)
                        foreach (var renderer in r.GetComponentsInChildren<Renderer>())
                        {
                            if (renderer.GetComponentInParent<TrainDrive>() || renderer.GetComponentInParent<TrackPiece>()) continue;
                            if (b == null) b = renderer.bounds;
                            else { var c = b.Value; c.Encapsulate(renderer.bounds); b = c; }
                        }
                    return b;
                }
                Vector3 departureStop = StopPoint(departure);
                var departureRoots = scene.GetRootGameObjects().Where(r => Flat2(departureStop, r.transform.position) < 50f &&
                    (r.name.StartsWith("Platform") || r.transform == departure.transform.root)).Select(r => r.transform);
                foreach (var area in new[] { Area(departureRoots), Area(arrivalGroup.Where(t => t && !t.GetComponentInChildren<TrackPiece>())) })
                    if (area != null)
                    {
                        var b = area.Value;
                        b.Expand(new Vector3(6f, 0f, 6f));
                        stationAreas.Add(b);
                    }
                log.AppendLine($"Stationsterreinen: {stationAreas.Count}.");
            }

            // -------------------------------------------------------------- terrein
            Vector3 terrainOrigin, terrainSize;
            int res;
            float[,] heights;

            void BuildTerrain()
            {
                var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
                var max = new Vector3(float.MinValue, 0f, float.MinValue);
                foreach (var s in samples)
                {
                    min = Vector3.Min(min, s.rail);
                    max = Vector3.Max(max, s.rail);
                }
                // ruim: de kloofuitlopers en de heuvels rond de vallei moeten erop passen
                foreach (var p in canyon.line)
                {
                    min = Vector3.Min(min, p - new Vector3(40f, 0f, 40f));
                    max = Vector3.Max(max, p + new Vector3(40f, 0f, 40f));
                }
                const float margin = 110f;
                terrainOrigin = new Vector3(Mathf.Floor(min.x - margin), TerrainBottom, Mathf.Floor(min.z - margin));
                terrainSize = new Vector3(Mathf.Ceil(max.x - min.x + 2 * margin), TerrainHeight, Mathf.Ceil(max.z - min.z + 2 * margin));
                res = Mathf.Max(terrainSize.x, terrainSize.z) > 420f ? 1025 : 513;

                var layers = StylizedTerrainTools.EnsureLayers();
                data = new TerrainData { heightmapResolution = res, alphamapResolution = 1024 };
                data.size = terrainSize;
                data.terrainLayers = layers;

                var far = new FarField(samples, terrainOrigin, terrainSize, 6f);
                heights = new float[res, res];
                float noiseX = rng.Next(1000), noiseZ = rng.Next(1000);
                for (int z = 0; z < res; z++)
                {
                    if ((z & 31) == 0) Progress("Terrein: hoogtes", 0.25f + 0.2f * z / res);
                    for (int x = 0; x < res; x++)
                    {
                        var w = World(x, z);
                        float y = Height(w.x, w.z, far, noiseX, noiseZ);
                        heights[z, x] = Mathf.Clamp01((y - TerrainBottom) / TerrainHeight);
                    }
                }
                data.SetHeights(0, 0, heights);
                PaintLayers(layers.Length, far, noiseX, noiseZ);

                AssetDatabase.CreateAsset(data, DataFolder + "/Level02_Terrain.asset");
                var go = Terrain.CreateTerrainGameObject(data);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.name = "Terrein · kloofvallei";
                go.transform.position = terrainOrigin;
                terrain = go.GetComponent<Terrain>();
                terrain.materialTemplate = StylizedTerrainShaderTools.GetOrCreateMaterial();
                terrain.drawInstanced = false;
                terrain.basemapDistance = 5000f;
                terrain.heightmapPixelError = 1f;
                terrain.treeDistance = 0f;
                terrain.detailObjectDistance = 0f;
                TerrainCliffsEditor.AddTo(terrain);                // steile hellingen (taluds, heuvels) krijgen grond/rots
                Physics.SyncTransforms();
                log.AppendLine($"Terrein {terrainSize.x:0} × {terrainSize.z:0} m, heightmap {res}.");
            }

            Vector3 World(int x, int z) => new Vector3(
                terrainOrigin.x + x / (float)(res - 1) * terrainSize.x, 0f,
                terrainOrigin.z + z / (float)(res - 1) * terrainSize.z);

            float Noise(float x, float z, float scale, float ox, float oz) => Mathf.PerlinNoise(ox + x / scale, oz + z / scale);

            float Fbm(float x, float z, float ox, float oz) =>
                Noise(x, z, 90f, ox, oz) * 0.55f + Noise(x, z, 38f, ox + 31f, oz + 7f) * 0.3f + Noise(x, z, 15f, ox + 77f, oz + 51f) * 0.15f;

            float Height(float x, float z, FarField far, float ox, float oz)
            {
                float dAll = far.Distance(x, z);                                   // afstand tot het dichtstbijzijnde spoor
                float dHigh = far.DistanceHigh(x, z);                             // tot het hoge deel langs de kloof
                float n = Fbm(x, z, ox, oz);
                float away = Smooth(22f, 85f, dAll);
                // glooiend land, hoger en woester naarmate je verder van het spoor komt
                float h = (n - 0.45f) * (1.6f + 8f * away) + away * 7f * Mathf.Pow(Noise(x, z, 60f, ox + 300f, oz + 90f), 2f);
                // plateau rond het hoge spoor: het spoor klimt erop en daalt er weer af
                h = Mathf.Lerp(h, Mathf.Max(h, rimY + (n - 0.5f) * 1.6f), Smooth(60f, 24f, dHigh));
                // al het land rond de kloof ligt minstens op randhoogte
                float D = canyon.Dist(x, z), half = canyon.Half(x, z);
                h = Mathf.Lerp(h, Mathf.Max(h, rimY + (n - 0.5f) * 2.4f), Smooth(half + 34f, half + 14f, D));
                // heuvels langs de buitenrand sluiten de vallei af
                float edge = Mathf.Min(Mathf.Min(x - terrainOrigin.x, terrainOrigin.x + terrainSize.x - x),
                                       Mathf.Min(z - terrainOrigin.z, terrainOrigin.z + terrainSize.z - z));
                h += 26f * Mathf.Pow(Smooth(90f, 0f, edge), 1.4f) * (0.55f + 0.9f * Noise(x, z, 42f, ox + 600f, oz + 200f));
                // vlak langs de kloofrand: de wanden (CliffMass) sluiten daar precies op aan
                h = Mathf.Lerp(h, rimY, Smooth(half + 16f, half + 8f, D));
                bool inCanyon = D < half - 0.5f;

                // onder het spoor vlak, met een talud of insnijding naar het land ernaast (niet onder de brug, niet in de kloof)
                var near = inCanyon ? null : grid.Nearest(x, z, 14f, s => !s.bridge);
                if (near != null)
                {
                    float d = HorizontalDistance(near.rail, x, z);
                    // voorbij het eind van een spoorstuk (brughoofd): korter talud
                    float lengthwise = Mathf.Abs((x - near.rail.x) * near.dir.x + (z - near.rail.z) * near.dir.z);
                    d += 1.5f * Mathf.Max(0f, lengthwise - 0.6f);
                    float bed = near.Pivot - 0.03f;
                    h = Mathf.Lerp(bed, h, Smooth(3.4f, 12f, d));
                }
                // onder het brugdek: grond altijd onder het dek (bij de brughoofden zakt het dek in de grond)
                var deck = grid.Nearest(x, z, 6f, s => s.bridge);
                if (deck != null)
                    h = Mathf.Lerp(Mathf.Min(h, deck.Pivot - 0.6f), h, Smooth(3.6f, 6f, HorizontalDistance(deck.rail, x, z)));

                // De CliffMass heeft 13 afzonderlijke grasdekken/terrassen, maar voorheen
                // stortte de Terrain in slechts 1,1 m van rimY naar de rivierbodem.
                // Daardoor sneed de grond dwars door de klif of zweefden richels.
                // Gebruik exact hetzelfde celprofiel als BuildCanyonWalls en leg de
                // grond iets onder elk grasdek (de rotswand sluit het hoogteverschil).
                if (inCanyon)
                {
                    float r = canyon.RiverDist(x, z);
                    float bottom = floorY + 0.25f + Noise(x, z, 9f, ox + 70f, oz + 30f) * 0.5f;
                    bottom = Mathf.Lerp(waterY - 1.3f, bottom, Smooth(RiverHalf - 0.6f, RiverHalf + 1.2f, r));
                    int wallTier = canyon.Level(x, z, CanyonTiers);
                    bool nearTrack = grid.Nearest(x, z, 3.2f, s => !s.bridge) != null;
                    h = wallTier > 0 && !nearTrack
                        ? Mathf.Min(rimY, originY + wallTier * StepHeight - CliffMassBuilder.TerrainCapClearance)
                        : nearTrack ? h : bottom;
                }

                // rond de stations vlak op perronhoogte
                foreach (var area in stationAreas)
                {
                    float d = DistanceToBox(area, x, z);
                    if (d < 12f) h = Mathf.Lerp(-0.03f, h, Smooth(0f, 12f, d));
                }
                return h;
            }

            void PaintLayers(int layerCount, FarField far, float ox, float oz)
            {
                // lagen (StylizedTerrainTools): 0 gras, 1 gras met bloemen, 2 grond, 3 stenen pad, 4 rots
                int ar = data.alphamapResolution;
                var a = new float[ar, ar, layerCount];
                for (int z = 0; z < ar; z++)
                {
                    if ((z & 63) == 0) Progress("Terrein: kleuren", 0.45f + 0.08f * z / ar);
                    for (int x = 0; x < ar; x++)
                    {
                        float wx = terrainOrigin.x + (x + 0.5f) / ar * terrainSize.x;
                        float wz = terrainOrigin.z + (z + 0.5f) / ar * terrainSize.z;
                        float grass = 1f, flowers = 0f, dirt = 0f, stones = 0f, rock = 0f;
                        float d = far.Distance(wx, wz);
                        float f = Noise(wx, wz, 22f, ox + 500f, oz + 500f);
                        if (d > 7f && f > 0.6f) flowers = Mathf.Clamp01((f - 0.6f) * 6f);
                        if (d > 2.6f && d < 4.6f) dirt = 0.55f;                       // berm naast de bielzen
                        float D = canyon.Dist(wx, wz), half = canyon.Half(wx, wz);
                        if (D < half - 0.5f)
                        {
                            // kloofbodem: gras met kale plekken, grind en stenen langs de rivier
                            float r = canyon.RiverDist(wx, wz);
                            float g = Noise(wx, wz, 7f, ox + 90f, oz + 40f);
                            dirt = 0.25f + 0.5f * Mathf.Clamp01((0.55f - g) * 3f);
                            if (r < RiverHalf + 1.6f) { stones = 0.8f; dirt = 0.4f; grass = 0.15f; flowers = 0f; }
                            else if (r < RiverHalf + 3f) { stones = 0.3f; grass = 0.7f; }
                        }
                        else if (D < half + 2.5f) { dirt = Mathf.Max(dirt, 0.35f); flowers = 0f; }   // kale rand langs de kloof
                        foreach (var area in stationAreas)
                            if (DistanceToBox(area, wx, wz) < 3f) { stones = 0.5f; grass = 0.6f; }
                        float sum = grass + flowers + dirt + stones + rock;
                        if (layerCount > 0) a[z, x, 0] = grass / sum;
                        if (layerCount > 1) a[z, x, 1] = flowers / sum;
                        if (layerCount > 2) a[z, x, 2] = dirt / sum;
                        if (layerCount > 3) a[z, x, 3] = stones / sum;
                        if (layerCount > 4) a[z, x, 4] = rock / sum;
                    }
                }
                data.SetAlphamaps(0, 0, a);
            }

            float GroundAt(Vector3 world) => terrain ? terrain.SampleHeight(world) + terrain.transform.position.y : 0f;

            float SteepnessAt(Vector3 world)
            {
                float u = (world.x - terrainOrigin.x) / terrainSize.x, v = (world.z - terrainOrigin.z) / terrainSize.z;
                return u < 0f || v < 0f || u > 1f || v > 1f ? 90f : data.GetSteepness(u, v);
            }

            // -------------------------------------------------------------- kliffen (CliffMass)
            CliffMass NewMass(string name, Vector3 at, bool stone, int seed, float? y = null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(Category("Cliffs"), true);
                go.transform.SetPositionAndRotation(new Vector3(Mathf.Round(at.x), y ?? GroundAt(at), Mathf.Round(at.z)), Quaternion.identity);
                var mass = go.AddComponent<CliffMass>();
                mass.cellSize = 1f;
                mass.stepHeight = StepHeight;
                mass.stone = stone;
                mass.tufts = true;
                mass.seed = seed;
                go.AddComponent<PaintedProp>().followGround = false;
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshCollider>();
                return mass;
            }

            /// Een klifwand naast het spoor: cellen van 'from' tot 'to' meter opzij, top minstens 'above' boven de rails.
            CliffMass BuildCliff(string name, string[] tags, bool left, float from, float to, float above, bool stone)
            {
                var section = samples.Where(s => tags.Contains(s.tag)).OrderBy(s => s.arc).ToList();
                if (section.Count == 0) return null;
                Vector3 Side(Sample s) => left ? Vector3.Cross(s.dir, Vector3.up) : Vector3.Cross(Vector3.up, s.dir);
                var first = section[0];
                var mass = NewMass(name, first.rail + Side(first) * (from + 2f), stone, rng.Next(1, 99999));
                float originY = mass.transform.position.y;
                int cells = 0;
                foreach (var s in section)
                {
                    var side = Side(s);
                    // wand iets onregelmatig: per plek een paar meter meer of minder diep
                    float depth = to + (Mathf.PerlinNoise(s.arc * 0.09f, left ? 3f : 7f) - 0.5f) * 5f;
                    for (float l = from; l <= depth; l += 0.5f)
                    {
                        var w = s.rail + side * l;
                        if (grid.Nearest(w.x, w.z, from - 0.3f, q => true) != null) continue;   // nooit te dicht bij ander spoor
                        float top = s.Pivot + above + (l > from + 4f ? StepHeight : 0f);
                        int level = Mathf.Max(1, Mathf.CeilToInt((top - originY) / StepHeight));
                        var c = mass.CellAt(mass.transform.InverseTransformPoint(w));
                        if (level > mass.GetLevel(c)) { mass.SetLevel(c, level); cells++; }
                    }
                }
                CliffMassBuilder.Rebuild(mass);
                log.AppendLine($"{name}: {cells} cellen.");
                return mass;
            }

            // Een paar losse kliffen in het heuvelland: hoogteverschil en richtpunten in de verte.
            void BuildScatteredCliffs()
            {
                int made = 0;
                for (int attempt = 0; attempt < 80 && made < 7; attempt++)
                {
                    var p = new Vector3(terrainOrigin.x + 30f + (float)rng.NextDouble() * (terrainSize.x - 60f), 0f,
                                        terrainOrigin.z + 30f + (float)rng.NextDouble() * (terrainSize.z - 60f));
                    if (grid.Nearest(p.x, p.z, 24f, s => true) != null || InCanyon(p, 12f) || NearStation(p, 20f)) continue;
                    var mass = NewMass("Klif in het heuvelland", p, rng.NextDouble() < 0.35, rng.Next(1, 99999));
                    Blob(mass, p, 4f + (float)rng.NextDouble() * 4f, 1 + rng.Next(3), 12f);
                    CliffMassBuilder.Rebuild(mass);
                    made++;
                }
                log.AppendLine($"Losse kliffen in het landschap: {made}.");
            }

            // Ronde, onregelmatige vlek cellen; het midden hoger (treden), richting rand lager.
            void Blob(CliffMass mass, Vector3 center, float radius, int tiers, float trackClearance)
            {
                float seed = (float)rng.NextDouble() * 100f;
                int r = Mathf.CeilToInt(radius + 3f);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var w = center + new Vector3(dx, 0f, dz);
                        float angle = Mathf.Atan2(dz, dx);
                        float edge = radius * (0.75f + 0.5f * Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.3f, seed + Mathf.Sin(angle) * 1.3f));
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > edge) continue;
                        if (grid.Nearest(w.x, w.z, trackClearance, s => true) != null) continue;
                        int level = Mathf.Clamp(1 + Mathf.FloorToInt((1f - dist / edge) * tiers * 1.2f), 1, tiers);
                        var c = mass.CellAt(mass.transform.InverseTransformPoint(w));
                        if (level > mass.GetLevel(c)) mass.SetLevel(c, level);
                    }
            }

            bool InCanyon(Vector3 p, float extra) => canyon.Dist(p.x, p.z) < canyon.Half(p.x, p.z) + extra;

            // -------------------------------------------------------------- de kloof
            // Middenlijn: een S onder de brug door. Langs rimA ligt hij op afstand c rechts van het spoor, onder de brug
            // draait hij in een kwartcirkel om het begin van de brug (straal c) en in een tweede om het eind, langs rimB ligt
            // hij links. Met c = halve brug kruist hij het spoor precies haaks in het midden van de brug, en staat de rand
            // overal even ver van het spoor. Aan beide kanten loopt een uitloper van het spoor weg, met een kopwand.
            void PlanCanyon()
            {
                var rimA = route.Where(r => r.tag == "rimA").ToList();
                var rimB = route.Where(r => r.tag == "rimB").ToList();
                if (rimA.Count == 0 || rimB.Count == 0) throw new Exception("Route mist de stukken langs de kloofrand.");
                Vector3 a0 = Flat0(rimA[0].piece.StartPosition), b0 = Flat0(bridgeStart), b1 = Flat0(bridgeEnd);
                Vector3 e = Flat0(rimB[rimB.Count - 1].piece.EndPosition);
                Vector3 dir = Flat(b1 - b0), right = Vector3.Cross(Vector3.up, dir), left = -right;
                float c = Vector3.Distance(b0, b1) * 0.5f;

                var pts = new List<Vector3>();
                var west = Arm(a0 + right * c, -dir, right, 26f, 80f, 38f);
                west.Reverse();
                pts.AddRange(west);
                Segment(pts, a0 + right * c, b0 + right * c);
                Bend(pts, b0, c, right, dir);
                Bend(pts, b1, c, -dir, left);
                Segment(pts, b1 + left * c, e + left * c);
                pts.AddRange(Arm(e + left * c, dir, left, 26f, 70f, 44f));
                float westLength = 26f * 80f * Mathf.Deg2Rad + 38f, eastLength = 26f * 70f * Mathf.Deg2Rad + 44f;

                canyon = new Canyon(Resample(pts, 1f), (x, z) =>
                {
                    var s = grid.Nearest(x, z, 70f, q => !q.bridge);
                    return s != null ? HorizontalDistance(s.rail, x, z) : 70f;
                }, (float)rng.NextDouble() * 100f);

                // watervallen: één in de westelijke kopwand (bron van de rivier), twee in de zijwanden van de uitlopers
                var line = canyon.line;
                Vector3 headDir = (line[0] - line[1]).normalized;
                canyon.AddFall(line[0], headDir, 1.8f, true);
                foreach (float at in new[] { westLength * 0.45f, canyon.Length - eastLength * 0.5f })
                {
                    int i = canyon.IndexAt(at);
                    var n = canyon.Normal(i);
                    var track = grid.Nearest(line[i].x, line[i].z, 90f, q => true);
                    if (track != null && Vector3.Dot(n, Flat0(line[i] - track.rail)) < 0f) n = -n;   // de wand die je vanaf het spoor ziet
                    canyon.AddFall(line[i], n, 1.5f, false);
                }
                canyon.BuildFields();
                log.AppendLine($"Kloof: {canyon.Length:0} m lang, rand {c - canyon.half[canyon.IndexAt(westLength + 10f)]:0.0} m van het spoor, " +
                               $"{CanyonTiers * StepHeight:0} m diep, {canyon.falls.Count} watervallen.");
            }

            static Vector3 Flat0(Vector3 v) => new Vector3(v.x, 0f, v.z);

            static void Segment(List<Vector3> pts, Vector3 a, Vector3 b)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b)));
                for (int k = 0; k <= n; k++) pts.Add(Vector3.Lerp(a, b, k / (float)n));
            }

            // Kwartcirkel om 'center' met straal r, van richting 'from' naar richting 'to' (loodrecht op elkaar).
            static void Bend(List<Vector3> pts, Vector3 center, float r, Vector3 from, Vector3 to)
            {
                int n = Mathf.CeilToInt(r * Mathf.PI * 0.5f);
                for (int k = 0; k <= n; k++)
                {
                    float phi = k / (float)n * Mathf.PI * 0.5f;
                    pts.Add(center + r * (from * Mathf.Cos(phi) + to * Mathf.Sin(phi)));
                }
            }

            // Uitloper: vanaf 'start' in richting 'heading', in een boog (straal, graden) naar 'turn' toe, dan rechtdoor.
            static List<Vector3> Arm(Vector3 start, Vector3 heading, Vector3 turn, float radius, float degrees, float straight)
            {
                var pts = new List<Vector3>();
                var p = start;
                float arc = radius * degrees * Mathf.Deg2Rad;
                for (float s = 1f; s <= arc + straight; s += 1f)
                {
                    float phi = Mathf.Min(s, arc) / radius;
                    p += heading * Mathf.Cos(phi) + turn * Mathf.Sin(phi);
                    pts.Add(p);
                }
                return pts;
            }

            static Vector3[] Resample(List<Vector3> pts, float step)
            {
                var clean = new List<Vector3> { pts[0] };
                foreach (var q in pts) if (Vector3.Distance(q, clean[clean.Count - 1]) > 0.05f) clean.Add(q);
                var output = new List<Vector3> { clean[0] };
                float carry = 0f;
                for (int i = 1; i < clean.Count; i++)
                {
                    Vector3 a = clean[i - 1], b = clean[i];
                    float len = Vector3.Distance(a, b), t = step - carry;
                    while (t <= len) { output.Add(Vector3.Lerp(a, b, t / len)); t += step; }
                    carry = len - (t - step);
                }
                return output.ToArray();
            }

            // Eén CliffMass voor alle kloofwanden: getrapt profiel (rand, richels, puin), 13 treden boven de bodem.
            void BuildCanyonWalls()
            {
                var mass = NewMass("Kloofwanden", canyon.line[canyon.line.Length / 2], false, rng.Next(1, 99999), originY);
                int cells = 0;
                canyon.Bounds(out int x0, out int z0, out int x1, out int z1);
                for (int z = z0; z <= z1; z++)
                {
                    if ((z & 15) == 0) Progress("Kloofwanden", 0.55f + 0.04f * (z - z0) / Mathf.Max(1, z1 - z0));
                    for (int x = x0; x <= x1; x++)
                    {
                        int level = canyon.Level(x, z, CanyonTiers);
                        if (level <= 0) continue;
                        if (grid.Nearest(x, z, 3.2f, s => !s.bridge) != null) continue;      // veiligheid: nooit op het spoor
                        mass.SetLevel(mass.CellAt(mass.transform.InverseTransformPoint(new Vector3(x, originY, z))), level);
                        cells++;
                    }
                }
                CliffMassBuilder.Rebuild(mass);
                canyonWalls = mass;
                log.AppendLine($"Kloofwanden: {cells} cellen.");
            }

            // Rotspilaren op de bodem van de wijde uitlopers, weg van het spoor, de rivier en de watervallen.
            void BuildPillars()
            {
                var placed = new List<Vector3>();
                for (int attempt = 0; attempt < 120 && placed.Count < 4; attempt++)
                {
                    int i = rng.Next(4, canyon.line.Length - 4);
                    if (canyon.nearTrack[i] < 38f) continue;
                    float radius = 2.6f + (float)rng.NextDouble() * 1.8f;
                    float offset = RiverHalf + 2.2f + radius;
                    if (offset + radius > canyon.FloorHalf(i) - 0.8f) continue;
                    var center = canyon.line[i] + canyon.Normal(i) * (rng.NextDouble() < 0.5 ? -offset : offset);
                    if (placed.Any(q => Vector3.Distance(q, center) < 16f) || canyon.NearFall(center, radius + 6f)) continue;
                    var mass = NewMass("Rotspilaar in de kloof", center, true, rng.Next(1, 99999), originY);
                    int top = CanyonTiers - 2 - rng.Next(6);
                    float seed = (float)rng.NextDouble() * 100f;
                    int r = Mathf.CeilToInt(radius + 2f);
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            var w = new Vector3(Mathf.Round(center.x) + dx, 0f, Mathf.Round(center.z) + dz);
                            if (canyon.Level(w.x, w.z, CanyonTiers) > 0 || canyon.RiverDist(w.x, w.z) < RiverHalf + 0.8f) continue;
                            float angle = Mathf.Atan2(w.z - center.z, w.x - center.x);
                            float edge = radius * (0.8f + 0.4f * Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.4f, seed + Mathf.Sin(angle) * 1.4f));
                            float dist = HorizontalDistance(center, w.x, w.z);
                            int level = dist < edge * 0.62f ? top : dist < edge ? top - 2 - (Mathf.PerlinNoise(w.x * 0.5f, w.z * 0.5f) > 0.5f ? 1 : 0)
                                      : dist < edge + 1.1f ? 1 : 0;
                            if (level > 0) mass.SetLevel(mass.CellAt(mass.transform.InverseTransformPoint(w)), level);
                        }
                    CliffMassBuilder.Rebuild(mass);
                    placed.Add(center);
                }
                log.AppendLine($"Rotspilaren in de kloof: {placed.Count}.");
            }

            void BuildWaterfalls()
            {
                var model = Model("Water", "Waterfall_Large");
                var wall = canyonWalls ? canyonWalls.GetComponent<Collider>() : null;
                if (!model || !wall) { log.AppendLine("Watervallen overgeslagen (model of wand ontbreekt)."); return; }
                Physics.SyncTransforms();
                int made = 0;
                foreach (var fall in canyon.falls)
                {
                    // horizontaal vanuit de kloof naar de wand, een paar meter onder de rand
                    var from = new Vector3(fall.inside.x, RimTop - 3.5f, fall.inside.z);
                    if (!wall.Raycast(new Ray(from, fall.dir), out var hit, 80f)) continue;
                    var foot = hit.point - fall.dir * 0.55f;
                    float ground = GroundAt(foot);
                    float height = RimTop + 0.06f - ground;
                    var go = Place(model, "Water", new Vector3(foot.x, ground, foot.z), Quaternion.LookRotation(-fall.dir).eulerAngles.y,
                        1f, 1f, 0f, false);
                    if (!go) continue;
                    // Waterfall_Large is 4,6 m hoog: alleen in de hoogte rekken, zodat de lip precies op de kloofrand ligt
                    go.transform.localScale = new Vector3(fall.width, height / 4.6f, fall.width * 0.8f);
                    go.GetComponent<PaintedProp>().followGround = false;
                    go.name = fall.head ? "Waterval · bron van de rivier" : "Waterval in de kloofwand";
                    made++;
                }
                log.AppendLine($"Watervallen: {made}.");
            }

            // Houten hek langs de kloofkant van het spoor, bovenop de rotsrand.
            void BuildFences()
            {
                var fence = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Station/Farm/Farm_Fence_3m.fbx");
                if (!fence) { log.AppendLine("Hek overgeslagen: Farm_Fence_3m ontbreekt."); return; }
                Physics.SyncTransforms();
                int count = 0;
                foreach (var (tags, right) in new[] { (new[] { "rimA", "bridgeRail" }, true), (new[] { "rimB" }, false) })
                {
                    var section = route.Where(r => tags.Contains(r.tag)).ToList();
                    if (section.Count == 0) continue;
                    float from = section[0].arcStart + (right ? 1f : 1.5f), to = section[section.Count - 1].arcEnd - (right ? 1.5f : 1f);
                    // 3,06 m per stuk, om de 3,15 m: palen raken elkaar niet (geen dubbele vlakken)
                    for (float a = from; a + 3.1f <= to; a += 3.15f)
                    {
                        var s = SampleAt(a);
                        var side = right ? Vector3.Cross(Vector3.up, s.dir) : Vector3.Cross(s.dir, Vector3.up);
                        var p = s.rail + side * 5.2f;
                        float y = CliffTop(p, out var top, out _) ? Mathf.Max(top, GroundAt(p)) : GroundAt(p);
                        // lokale x langs het spoor
                        float yaw = Quaternion.LookRotation(Vector3.Cross(s.dir, Vector3.up)).eulerAngles.y;
                        var go = Place(fence, "Fences", new Vector3(p.x, y - 0.03f, p.z), yaw, 1f, 1f, -0.03f, true);
                        if (go) { go.GetComponent<PaintedProp>().followGround = false; count++; }
                    }
                }
                log.AppendLine($"Hek langs de kloofrand: {count} stukken.");
            }

            bool NearStation(Vector3 p, float margin) => stationAreas.Any(a => DistanceToBox(a, p.x, p.z) < margin);

            // -------------------------------------------------------------- gameplay
            void SetupGameplay(CliffMass cliffA, CliffMass cliffB)
            {
                // stations
                Rename(departure, "Puddleford", "Puddleford · vertrek");
                Rename(arrival, "Craggleton", "Craggleton · aankomst");

                // opdrachten: alle uit level 1 + brugrail
                var runSo = new SerializedObject(run);
                var list = runSo.FindProperty("objectives");
                var bridgeRail = route.First(r => r.tag == "bridgeRail").piece.GetComponent<RepairPoint>();
                if (!bridgeRail) throw new Exception("Track_Broken_6m heeft geen RepairPoint.");
                bridgeRail.Configure(true);
                Dirty(bridgeRail);
                var damagePoint = route.First(r => r.tag == "damage").piece.GetComponent<RepairPoint>();
                if (damagePoint)
                {
                    damagePoint.Configure(false);             // heel bij de start; een incident kan hem breken
                    Dirty(damagePoint);
                }

                var railGo = new GameObject("Herstel de brugrail");
                SceneManager.MoveGameObjectToScene(railGo, scene);
                var railObjective = railGo.AddComponent<ServiceTrackRepair>();
                railObjective.Configure(bridgeRail, true);
                var objSo = new SerializedObject(railObjective);
                objSo.FindProperty("title").stringValue = "Herstel de brugrail";
                objSo.FindProperty("instructions").stringValue =
                    "De rail vlak voor de stenen brug over de kloof is kapot. Stop ervoor en sla de gele bout vast met de moersleutel, anders ontspoort de trein.";
                objSo.FindProperty("icon").stringValue = "wrench";
                objSo.ApplyModifiedPropertiesWithoutUndo();

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = railObjective;
                runSo.FindProperty("combineObjectives").boolValue = true;
                runSo.FindProperty("levelLabel").stringValue = "LEVEL 2";
                var limits = runSo.FindProperty("timeLimits");
                int[] seconds = { 600, 540, 480, 420 };
                limits.arraySize = seconds.Length;
                for (int i = 0; i < seconds.Length; i++) limits.GetArrayElementAtIndex(i).intValue = seconds[i];
                runSo.ApplyModifiedPropertiesWithoutUndo();
                run.gameObject.name = "Level 2 · spelregels";
                var titles = new List<string>();
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is ServiceObjective o) titles.Add(o.Title);
                log.AppendLine("Opdrachten (alle tegelijk): " + string.Join(", ", titles) + ".");

                // tweede moersleutel: er zijn nu drie plekken om te repareren
                var wrench = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Wrench.prefab");
                var firstWrench = scene.GetRootGameObjects().FirstOrDefault(r => r.name.StartsWith("Prop_Wrench"));
                if (wrench && firstWrench)
                {
                    var extra = (GameObject)PrefabUtility.InstantiatePrefab(wrench, scene);
                    extra.transform.SetPositionAndRotation(firstWrench.transform.position + departure.transform.right * 1.3f,
                        firstWrench.transform.rotation);
                    extra.name = "Prop_Wrench (reserve)";
                    log.AppendLine("Tweede moersleutel op het vertrekperron.");
                }

                // incidenten
                var incidentsGo = new GameObject("Incidenten onderweg");
                SceneManager.MoveGameObjectToScene(incidentsGo, scene);
                incidentsGo.AddComponent<NetworkObject>();
                var incidents = incidentsGo.AddComponent<ServiceIncidents>();
                var rockfalls = new List<ServiceIncidents.Rockfall>();
                var a = MakeRockfall(incidentsGo.transform, "Steenlawine bij de klim", cliffA, "rimA", true);
                if (a != null) rockfalls.Add(a);
                var b = MakeRockfall(incidentsGo.transform, "Steenlawine na de brug", cliffB, "rimB", false);
                if (b != null) rockfalls.Add(b);
                var damages = new List<ServiceIncidents.TrackDamage>();
                if (damagePoint)
                {
                    var damageArc = route.First(r => r.tag == "damage").arcStart;
                    damages.Add(new ServiceIncidents.TrackDamage
                    {
                        name = "Spoorschade in het bos",
                        trigger = Trigger(incidentsGo.transform, "Trigger spoorschade", damageArc - 58f),
                        repair = damagePoint,
                        chance = 0.6f,
                    });
                }
                incidents.Configure(rockfalls.ToArray(), damages.ToArray(), 1);
                EditorUtility.SetDirty(incidents);
                log.AppendLine($"Incidenten: {rockfalls.Count} steenlawines, {damages.Count} spoorschade (minstens één per rit).");

                // waarschuwingsborden bij de brug
                var sign = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Track/Prop_WarningSign.fbx");
                if (sign)
                {
                    var railSample = SampleAt(route.First(r => r.tag == "bridgeRail").arcStart - 4f);
                    if (railSample != null)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(sign, scene);
                        go.transform.SetParent(Category("Props"), true);
                        var side = Vector3.Cross(railSample.dir, Vector3.up);       // bergkant; aan de kloofkant staat het hek
                        var pos = railSample.rail + side * 3.4f;
                        pos.y = GroundAt(pos);
                        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-railSample.dir));
                        go.name = "Waarschuwing · brugrail";
                    }
                }
            }

            void Rename(ServiceStation station, string displayName, string objectName)
            {
                var so = new SerializedObject(station);
                so.FindProperty("stationName").stringValue = displayName;
                so.ApplyModifiedPropertiesWithoutUndo();
                station.transform.root.name = objectName;
                Dirty(station.transform.root.gameObject);
            }

            Sample SampleAt(float arc)
            {
                Sample best = null;
                float bestD = float.MaxValue;
                foreach (var s in samples)
                {
                    if (s.arc < 0f) continue;
                    float d = Mathf.Abs(s.arc - arc);
                    if (d < bestD) { bestD = d; best = s; }
                }
                return best;
            }

            BoxCollider Trigger(Transform parent, string name, float arc)
            {
                var s = SampleAt(Mathf.Max(0f, arc));
                var go = new GameObject(name) { layer = 2 };
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(s.rail + Vector3.up * 1f, Quaternion.LookRotation(s.dir));
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(9f, 7f, 10f);
                return box;
            }

            ServiceIncidents.Rockfall MakeRockfall(Transform parent, string name, CliffMass cliff, string tag, bool left)
            {
                if (!cliff) return null;
                var section = route.Where(r => r.tag == tag).ToList();
                if (section.Count == 0) return null;
                float mid = (section[0].arcStart + section[section.Count - 1].arcEnd) * 0.5f;
                var site = SampleAt(mid);
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                // rotsblokken zelf niet onder het NetworkObject van de incidenten (geen geneste NetworkObjects)
                var rockRoot = Root("Rotsblokken · steenlawines").transform;
                var siteMarker = new GameObject("Plek op het spoor").transform;
                siteMarker.SetParent(group, false);
                siteMarker.SetPositionAndRotation(site.rail, Quaternion.LookRotation(site.dir));

                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Rocks/Rock_Medium_01.fbx");
                if (!model) throw new Exception("Rock_Medium_01.fbx ontbreekt.");
                var rocks = new List<Rigidbody>();
                var landing = new List<Transform>();
                for (int k = 0; k < 3; k++)
                {
                    var s = SampleAt(mid + (k - 1) * 3.2f);
                    var side = left ? Vector3.Cross(s.dir, Vector3.up) : Vector3.Cross(Vector3.up, s.dir);
                    // klaar op de klifrand: recht boven de wand, iets naar binnen
                    var perch = s.rail + side * 7.2f;
                    float top = TopOf(perch, cliff);
                    var rock = MakeRock(rockRoot, $"{name} · blok {k + 1}", model, new Vector3(perch.x, top, perch.z), 0.85f + 0.12f * k);
                    rocks.Add(rock);
                    var land = new GameObject($"Landing {k + 1}").transform;
                    land.SetParent(group, false);
                    land.position = s.rail + side * ((float)rng.NextDouble() * 0.8f - 0.2f) + Vector3.up * 0.35f;
                    landing.Add(land);
                }
                return new ServiceIncidents.Rockfall
                {
                    name = name,
                    trigger = Trigger(group, "Trigger " + name.ToLowerInvariant(), mid - 55f),
                    rocks = rocks.ToArray(),
                    landing = landing.ToArray(),
                    site = siteMarker,
                    chance = 0.75f,
                };
            }

            float TopOf(Vector3 at, CliffMass cliff)
            {
                Physics.SyncTransforms();
                var collider = cliff.GetComponent<Collider>();
                if (collider && collider.Raycast(new Ray(new Vector3(at.x, 200f, at.z), Vector3.down), out var hit, 400f))
                    return hit.point.y;
                return GroundAt(at);
            }

            Rigidbody MakeRock(Transform parent, string name, GameObject model, Vector3 groundPoint, float scale)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localScale = Vector3.one * scale;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                go.transform.position = groundPoint + Vector3.up * (go.transform.position.y - bounds.min.y + 0.02f);
                var body = go.AddComponent<Rigidbody>();
                body.mass = 3f;
                body.linearDamping = 0.15f;
                body.angularDamping = 1.2f;
                body.isKinematic = true;                  // ligt klaar; ServiceIncidents laat hem vallen
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                go.AddComponent<NetworkRigidbody>();
                return body;
            }

            // -------------------------------------------------------------- natuur
            Transform Category(string name)
            {
                if (!propsRoot) propsRoot = Root("PaintedProps").transform;
                var t = propsRoot.Find(name);
                if (t) return t;
                var go = new GameObject(name);
                go.transform.SetParent(propsRoot, false);
                return go.transform;
            }

            GameObject Root(string name)
            {
                var existing = scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
                if (existing) return existing;
                var go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                return go;
            }

            GameObject Model(string folder, string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/{folder}/{name}.fbx");

            void PlantNature()
            {
                var trees = new (GameObject model, float weight, float clearance)[]
                {
                    (Model("Trees", "Tree_Pine_Medium_01"), 1.0f, 6.5f),
                    (Model("Trees", "Tree_Pine_Large_01"), 0.65f, 7.5f),
                    (Model("Trees", "Tree_Pine_Small_01"), 0.8f, 5.5f),
                    (Model("Trees", "Tree_Pine_Variant_02"), 0.6f, 7f),
                    (Model("Trees", "Bush_01"), 0.7f, 4.2f),
                };
                trees = trees.Where(t => t.model).ToArray();
                int planted = 0, onCliffs = 0;
                var far = new FarField(samples, terrainOrigin, terrainSize, 6f);
                float ox = rng.Next(1000), oz = rng.Next(1000);
                var taken = new SampleFreeGrid(4f);
                int candidates = Mathf.RoundToInt(terrainSize.x * terrainSize.z / 9f);
                for (int i = 0; i < candidates && trees.Length > 0 && planted < 1400; i++)
                {
                    if ((i & 1023) == 0) Progress("Bomen planten", 0.72f + 0.12f * i / candidates);
                    var p = new Vector3(terrainOrigin.x + 4f + (float)rng.NextDouble() * (terrainSize.x - 8f), 0f,
                                        terrainOrigin.z + 4f + (float)rng.NextDouble() * (terrainSize.z - 8f));
                    float d = far.Distance(p.x, p.z);
                    // bos in plukken: dicht waar de ruis hoog is, open weides rond de stations
                    float forest = Noise(p.x, p.z, 55f, ox, oz) * 0.8f + Noise(p.x, p.z, 18f, ox + 40f, oz + 9f) * 0.35f;
                    float density = Mathf.Clamp01((forest - 0.48f) * 2.4f) * (0.35f + 0.65f * Smooth(6f, 30f, d));
                    if (NearStation(p, 14f)) density *= 0.15f;
                    if (rng.NextDouble() > density * 0.55f) continue;
                    var pick = Pick(trees, t => t.weight);
                    if (d < pick.clearance || NearStation(p, 4f)) continue;
                    if (!taken.TryTake(p, pick.model.name.StartsWith("Bush") ? 2f : 3.1f)) continue;

                    // op de grond of bovenop een klif (alleen ver van het spoor)
                    bool onCliff = false;
                    float y;
                    if (CliffTop(p, out var top, out var normal))
                    {
                        if (d < 10f || normal.y < 0.9f || rng.NextDouble() < 0.35) continue;
                        y = top;
                        onCliff = true;
                    }
                    else
                    {
                        p.y = GroundAt(p);
                        if (SteepnessAt(p) > 27f) continue;
                        // op de kloofbodem wat minder bomen, nooit in de rivierbedding
                        if (InCanyon(p, 0f) && (canyon.RiverDist(p.x, p.z) < RiverHalf + 1.6f || rng.NextDouble() < 0.4)) continue;
                        y = p.y;
                    }
                    float sink = 0.08f;
                    var go = Place(pick.model, "Trees", new Vector3(p.x, y - sink, p.z), (float)rng.NextDouble() * 360f,
                        0.85f + (float)rng.NextDouble() * 0.45f, 0.9f + (float)rng.NextDouble() * 0.3f, -sink, d < 45f || NearStation(p, 40f));
                    if (go) { planted++; if (onCliff) onCliffs++; }
                }

                // rotsen bij klifvoeten en ravijnranden
                var rockModels = new[] { Model("Rocks", "Rock_Small_01"), Model("Rocks", "Rock_Medium_01"), Model("Rocks", "Rock_Large_01") }
                    .Where(m => m).ToArray();
                int rocks = 0;
                for (int i = 0; i < 2600 && rocks < 140 && rockModels.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < 5.5f || NearStation(p, 6f)) continue;
                    p.y = GroundAt(p);
                    if (canyon.RiverDist(p.x, p.z) < RiverHalf + 0.4f) continue;
                    if (CliffTop(p, out var overhead, out _) && overhead > p.y + 0.3f) continue;      // onder een rotswand
                    bool interesting = InCanyon(p, 4f) || SteepnessAt(p) > 18f || NearCliff(p, 4f);
                    if (!interesting && rng.NextDouble() > 0.06) continue;
                    var model = rockModels[rng.Next(rockModels.Length)];
                    if (model.name.Contains("Large") && d < 9f) continue;
                    float scale = 0.6f + (float)rng.NextDouble() * 0.9f;
                    float sink = 0.22f * scale;
                    if (Place(model, "Rocks", new Vector3(p.x, p.y - sink, p.z), (float)rng.NextDouble() * 360f, scale, 0.8f + (float)rng.NextDouble() * 0.4f, -sink, d < 40f))
                        rocks++;
                }

                // gras en bloemen op vlakke stukken langs het spoor en rond de stations (geen colliders)
                var plants = new[] { Model("Plants", "Grass_Tuft_01"), Model("Plants", "Grass_Tuft_02"), Model("Plants", "Flower_Patch_01"), Model("Plants", "Flower_Daisy_01") }
                    .Where(m => m).ToArray();
                int plantCount = 0;
                for (int i = 0; i < 14000 && plantCount < 560 && plants.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    bool station = NearStation(p, 18f) && !NearStation(p, 1f);
                    bool canyonFloor = InCanyon(p, -2f) && canyon.RiverDist(p.x, p.z) > RiverHalf + 0.8f;
                    if (!(station || canyonFloor || (d > 4.5f && d < 28f))) continue;
                    if (rng.NextDouble() > (station ? 0.6 : canyonFloor ? 0.35 : 0.18)) continue;
                    p.y = GroundAt(p);
                    if (SteepnessAt(p) > 16f || InCanyon(p, 2f) && !canyonFloor) continue;
                    if (CliffTop(p, out var overhead, out _) && overhead > p.y + 0.3f) continue;
                    var model = plants[rng.Next(plants.Length)];
                    if (Place(model, "Plants", new Vector3(p.x, p.y - 0.02f, p.z), (float)rng.NextDouble() * 360f, 0.8f + (float)rng.NextDouble() * 0.5f, 1f, -0.02f, false))
                        plantCount++;
                }
                log.AppendLine($"Natuur: {planted} bomen en struiken ({onCliffs} op kliffen), {rocks} rotsen, {plantCount} planten.");
            }

            static bool CliffTop(Vector3 p, out float y, out Vector3 normal)
            {
                y = float.NegativeInfinity;
                normal = Vector3.up;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 250f, p.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.point.y > y && hit.collider.GetComponentInParent<CliffMass>()) { y = hit.point.y; normal = hit.normal; }
                return !float.IsNegativeInfinity(y);
            }

            bool NearCliff(Vector3 p, float margin)
            {
                foreach (var hit in Physics.OverlapSphere(p, margin, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.GetComponentInParent<CliffMass>()) return true;
                return false;
            }

            T Pick<T>(T[] items, Func<T, float> weight)
            {
                float total = items.Sum(weight), r = (float)rng.NextDouble() * total;
                foreach (var item in items) { r -= weight(item); if (r <= 0f) return item; }
                return items[items.Length - 1];
            }

            GameObject Place(GameObject model, string category, Vector3 position, float yaw, float scale, float height, float groundOffset, bool collider)
            {
                if (!model) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                go.transform.SetParent(Category(category), true);
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                go.transform.localScale = new Vector3(scale, scale * height, scale);
                var marker = go.AddComponent<PaintedProp>();
                marker.groundOffset = groundOffset;
                marker.followGround = category != "Sky";
                if (collider)
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh && !mf.GetComponent<Collider>())
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                return go;
            }

            // Rivier over de kloofbodem: korte rechthoekige stukken (StylizedWater) langs de rivierlijn. In bochten
            // overlappen ze een klein beetje; om en om 1,2 cm hoger, zodat er nooit twee vlakken op elkaar liggen.
            void BuildRiver()
            {
                var surfaceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_Env_WaterSurface.mat");
                var tint = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/UnderwaterTint.shader");
                var parent = Root("Rivier in de kloof").transform;
                var river = canyon.river;
                const int step = 6;                                  // punten liggen 1 m uit elkaar
                int count = 0;
                for (int k = 0; k < river.Count - 1; k += step)
                {
                    Vector3 a = river[k], b = river[Mathf.Min(k + step, river.Count - 1)];
                    float length = Vector3.Distance(a, b);
                    if (length < 0.5f) break;
                    var go = new GameObject($"Rivier {count + 1}", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
                    go.layer = 2;                                       // Ignore Raycast, zoals Create Water
                    go.transform.SetParent(parent, false);
                    var mid = (a + b) * 0.5f;
                    go.transform.SetPositionAndRotation(new Vector3(mid.x, waterY + ((count & 1) == 0 ? 0f : 0.012f), mid.z),
                        Quaternion.LookRotation(Flat(b - a), Vector3.up));
                    var water = go.AddComponent<StylizedWater>();
                    water.size = new Vector2(2f * (RiverHalf + 1.1f), length);
                    water.depth = 1.6f;
                    water.surfaceMaterial = surfaceMat;
                    water.tintShader = tint;
                    water.Rebuild();
                    count++;
                }
                log.AppendLine($"Rivier op de kloofbodem: {count} stukken.");
            }

            void BuildBackdrop()
            {
                var mountains = new[] { Model("Mountains", "Mountain_Back_01"), Model("Mountains", "Mountain_Back_02"), Model("Mountains", "Mountain_Back_03") }
                    .Where(m => m).ToArray();
                var center = terrainOrigin + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);
                float radius = Mathf.Max(terrainSize.x, terrainSize.z) * 0.5f + 70f;
                int count = 0;
                for (int i = 0; i < 16 && mountains.Length > 0; i++)
                {
                    float angle = i / 16f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
                    var p = center + new Vector3(Mathf.Cos(angle) * radius * 1.0f, -6f, Mathf.Sin(angle) * radius * 0.75f);
                    if (Place(mountains[rng.Next(mountains.Length)], "Mountains", p, (float)rng.NextDouble() * 360f,
                            1.2f + (float)rng.NextDouble() * 0.8f, 0.9f + (float)rng.NextDouble() * 0.5f, -6f, false))
                        count++;
                }
                var clouds = new[] { Model("Sky", "Cloud_01"), Model("Sky", "Cloud_02"), Model("Sky", "Cloud_03") }.Where(m => m).ToArray();
                for (int i = 0; i < 12 && clouds.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 34f + (float)rng.NextDouble() * 14f,
                                        terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    Place(clouds[rng.Next(clouds.Length)], "Sky", p, (float)rng.NextDouble() * 360f, 1.5f + (float)rng.NextDouble(), 1f, 0f, false);
                }
                log.AppendLine($"Achtergrond: {count} bergen, wolken.");
            }
        }

        // ================================================================== hulpstructuren
        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float HorizontalDistance(Vector3 p, float x, float z)
        {
            float dx = p.x - x, dz = p.z - z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float DistanceToBox(Bounds b, float x, float z)
        {
            float dx = Mathf.Max(b.min.x - x, 0f, x - b.max.x);
            float dz = Mathf.Max(b.min.z - z, 0f, z - b.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // Ruimtelijk raster van spoorpunten: snel het dichtstbijzijnde punt binnen een straal vinden.
        sealed class SampleGrid
        {
            readonly float cell;
            readonly Dictionary<long, List<Sample>> cells = new Dictionary<long, List<Sample>>();

            public SampleGrid(List<Sample> samples, float cellSize)
            {
                cell = cellSize;
                foreach (var s in samples)
                {
                    long k = Key(Mathf.FloorToInt(s.rail.x / cell), Mathf.FloorToInt(s.rail.z / cell));
                    if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<Sample>();
                    list.Add(s);
                }
            }

            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            public Sample Nearest(float x, float z, float radius, Func<Sample, bool> filter)
            {
                int r = Mathf.CeilToInt(radius / cell);
                int cx = Mathf.FloorToInt(x / cell), cz = Mathf.FloorToInt(z / cell);
                Sample best = null;
                float bestD = radius * radius;
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (!cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                        foreach (var s in list)
                        {
                            float ex = s.rail.x - x, ez = s.rail.z - z, d = ex * ex + ez * ez;
                            if (d < bestD && filter(s)) { bestD = d; best = s; }
                        }
                    }
                return best;
            }
        }

        // Grof afstandsveld (elke 'step' meter) tot het spoor, bilineair geïnterpoleerd; voor 'hoe ver weg' in heuvels/bos.
        sealed class FarField
        {
            readonly float[,] all, high;
            readonly Dictionary<float, float[,]> except = new Dictionary<float, float[,]>();
            readonly List<Sample> samples;
            readonly Vector3 origin;
            readonly float step;
            readonly int nx, nz;
            static readonly string[] HighTags = { "rimA", "bridgeRail", "bridge", "rimB" };

            public FarField(List<Sample> samples, Vector3 origin, Vector3 size, float step)
            {
                this.samples = samples;
                this.origin = origin;
                this.step = step;
                nx = Mathf.CeilToInt(size.x / step) + 2;
                nz = Mathf.CeilToInt(size.z / step) + 2;
                all = Field(s => true);
                high = Field(s => Array.IndexOf(HighTags, s.tag) >= 0);
            }

            float[,] Field(Func<Sample, bool> use)
            {
                var chosen = samples.Where(use).Where((s, i) => i % 2 == 0).ToArray();
                var f = new float[nz, nx];
                for (int z = 0; z < nz; z++)
                    for (int x = 0; x < nx; x++)
                    {
                        float wx = origin.x + x * step, wz = origin.z + z * step, best = float.MaxValue;
                        foreach (var s in chosen)
                        {
                            float dx = s.rail.x - wx, dz = s.rail.z - wz, d = dx * dx + dz * dz;
                            if (d < best) best = d;
                        }
                        f[z, x] = chosen.Length > 0 ? Mathf.Sqrt(best) : 1e4f;
                    }
                return f;
            }

            float Read(float[,] f, float x, float z)
            {
                float fx = Mathf.Clamp((x - origin.x) / step, 0f, nx - 1.001f), fz = Mathf.Clamp((z - origin.z) / step, 0f, nz - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(f[iz, ix], f[iz, ix + 1], tx), Mathf.Lerp(f[iz + 1, ix], f[iz + 1, ix + 1], tx), tz);
            }

            public float Distance(float x, float z) => Read(all, x, z);
            public float DistanceHigh(float x, float z) => Read(high, x, z);

            /// Afstand tot spoor dat niet bij de brug hoort (verder dan 'window' meter van boogafstand 'arc').
            public float DistanceExcept(float x, float z, float arc, float window)
            {
                if (!except.TryGetValue(arc, out var f))
                    except[arc] = f = Field(s => s.arc < 0f || Mathf.Abs(s.arc - arc) > window);
                return Read(f, x, z);
            }
        }

        // De kloof: middenlijn (punten 1 m uit elkaar) met per punt de halve breedte tot de rand en het wandprofiel, plus
        // afstandsvelden (1 m raster) naar de middenlijn en naar de rivierlijn. Level() geeft per cel de wandhoogte in
        // treden; dezelfde Dist/Half bepalen het terrein, zodat wand en grond precies op elkaar aansluiten.
        sealed class Canyon
        {
            public readonly Vector3[] line;
            public readonly float[] arc, half, nearTrack, rimBand, w1, w2, w3;
            public readonly int[] l1, l2, talus;
            public readonly List<Vector3> river = new List<Vector3>();
            public readonly List<Fall> falls = new List<Fall>();
            public float Length => arc[arc.Length - 1];
            readonly float seed;

            public sealed class Fall
            {
                public Vector3 inside, dir, sheer;
                public float width;
                public bool head;
            }

            // velden
            int gx0, gz0, nx, nz;
            float[] dist, riverDist;
            int[] nearest;

            public Canyon(Vector3[] line, Func<float, float, float> trackDistance, float seed)
            {
                this.line = line;
                this.seed = seed;
                int n = line.Length;
                arc = new float[n]; half = new float[n]; nearTrack = new float[n];
                rimBand = new float[n]; w1 = new float[n]; w2 = new float[n]; w3 = new float[n];
                l1 = new int[n]; l2 = new int[n]; talus = new int[n];
                for (int i = 1; i < n; i++) arc[i] = arc[i - 1] + Vector3.Distance(line[i - 1], line[i]);
                for (int i = 0; i < n; i++) nearTrack[i] = trackDistance(line[i].x, line[i].z);
                float total = arc[n - 1];
                var raw = new float[n];
                for (int i = 0; i < n; i++)
                {
                    // de rand blijft 4,5 m van het spoor; verder weg breder en grilliger; smal bij de kopwanden
                    float wild = Smooth(28f, 45f, nearTrack[i]);
                    float h = 19.5f + wild * (4f + 6f * (Mathf.PerlinNoise(arc[i] / 23f + seed, 3.7f) - 0.5f));
                    float end = Mathf.Min(arc[i], total - arc[i]);
                    h *= Mathf.Lerp(0.6f, 1f, Smooth(0f, 34f, end));
                    raw[i] = Mathf.Min(h, nearTrack[i] - 4.5f);
                }
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f; int count = 0;
                    for (int k = Mathf.Max(0, i - 6); k <= Mathf.Min(n - 1, i + 6); k++) { sum += raw[k]; count++; }
                    half[i] = Mathf.Max(9f, Mathf.Min(sum / count, nearTrack[i] - 4.5f));
                }
                for (int i = 0; i < n; i++)
                {
                    float N(int k) => Mathf.PerlinNoise(arc[i] / 11f + k * 17.3f + seed, k * 5.1f + 0.5f);
                    rimBand[i] = 1.9f + 0.7f * N(1);
                    w1[i] = Mathf.Round(Mathf.Clamp(N(2) * 4.2f - 1f, 0f, 3f));
                    l1[i] = CanyonTiers - 3 - Mathf.FloorToInt(N(3) * 2.99f);
                    w2[i] = Mathf.Round(Mathf.Clamp(N(4) * 3.2f - 0.8f, 0f, 2f));
                    l2[i] = Mathf.Max(3, l1[i] - 3 - Mathf.FloorToInt(N(5) * 2.99f));
                    w3[i] = 1f + Mathf.Round(N(6) * 1.5f);
                    talus[i] = N(7) > 0.5f ? 2 : 1;
                    // de bodem houdt altijd ruimte voor de rivier met oevers
                    float budget = half[i] - (RiverHalf + 2.5f) - (rimBand[i] + 0.6f);
                    w1[i] = Mathf.Min(w1[i], Mathf.Max(0f, budget)); budget -= w1[i];
                    w2[i] = Mathf.Min(w2[i], Mathf.Max(0f, budget)); budget -= w2[i];
                    w3[i] = Mathf.Min(w3[i], Mathf.Max(0f, budget));
                }
                // rivier: de middenlijn, aan de westkant doorgetrokken tot vlak onder de kopwaterval
                Vector3 back = (line[0] - line[1]).normalized;
                int extra = Mathf.FloorToInt(half[0] - rimBand[0] - 3f);
                for (int k = extra; k >= 1; k--) river.Add(line[0] + back * k);
                river.AddRange(line);
            }

            public int IndexAt(float at)
            {
                int i = Array.BinarySearch(arc, at);
                return Mathf.Clamp(i < 0 ? ~i : i, 0, line.Length - 1);
            }

            public Vector3 Normal(int i)
            {
                var t = line[Mathf.Min(i + 1, line.Length - 1)] - line[Mathf.Max(i - 1, 0)];
                t.y = 0f;
                return Vector3.Cross(Vector3.up, t.normalized);
            }

            public float FloorHalf(int i) => half[i] - (rimBand[i] + 0.6f + w1[i] + w2[i] + w3[i]);

            public void AddFall(Vector3 inside, Vector3 dir, float width, bool head)
            {
                int i = IndexAt(0f);
                float bestD = float.MaxValue;
                for (int k = 0; k < line.Length; k++)
                {
                    float d = (line[k] - inside).sqrMagnitude;
                    if (d < bestD) { bestD = d; i = k; }
                }
                falls.Add(new Fall { inside = inside, dir = dir.normalized, width = width, head = head,
                                     sheer = inside + dir.normalized * (half[i] - rimBand[i]) });
            }

            public bool NearFall(Vector3 p, float radius) =>
                falls.Any(f => HorizontalDistance(f.sheer, p.x, p.z) < radius);

            public void Bounds(out int x0, out int z0, out int x1, out int z1)
            {
                x0 = gx0; z0 = gz0; x1 = gx0 + nx - 1; z1 = gz0 + nz - 1;
            }

            public void BuildFields()
            {
                float maxHalf = half.Max() + 3f;
                float minX = line.Min(p => p.x), maxX = line.Max(p => p.x), minZ = line.Min(p => p.z), maxZ = line.Max(p => p.z);
                foreach (var p in river) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
                gx0 = Mathf.FloorToInt(minX - maxHalf); gz0 = Mathf.FloorToInt(minZ - maxHalf);
                nx = Mathf.CeilToInt(maxX + maxHalf) - gx0 + 1; nz = Mathf.CeilToInt(maxZ + maxHalf) - gz0 + 1;
                dist = new float[nx * nz]; riverDist = new float[nx * nz]; nearest = new int[nx * nz];
                for (int z = 0; z < nz; z++)
                    for (int x = 0; x < nx; x++)
                    {
                        float wx = gx0 + x, wz = gz0 + z, best = float.MaxValue, bestR = float.MaxValue;
                        int bi = 0;
                        for (int i = 0; i < line.Length; i++)
                        {
                            float dx = line[i].x - wx, dz = line[i].z - wz, d = dx * dx + dz * dz;
                            if (d < best) { best = d; bi = i; }
                        }
                        foreach (var q in river)
                        {
                            float dx = q.x - wx, dz = q.z - wz, d = dx * dx + dz * dz;
                            if (d < bestR) bestR = d;
                        }
                        dist[z * nx + x] = Mathf.Sqrt(best);
                        riverDist[z * nx + x] = Mathf.Sqrt(bestR);
                        nearest[z * nx + x] = bi;
                    }
            }

            float Read(float[] f, float x, float z)
            {
                float fx = x - gx0, fz = z - gz0;
                if (fx < 0f || fz < 0f || fx > nx - 1.001f || fz > nz - 1.001f) return 1e4f;
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(f[iz * nx + ix], f[iz * nx + ix + 1], tx),
                                  Mathf.Lerp(f[(iz + 1) * nx + ix], f[(iz + 1) * nx + ix + 1], tx), tz);
            }

            public float Dist(float x, float z) => Read(dist, x, z);
            public float RiverDist(float x, float z) => Read(riverDist, x, z);

            int Nearest(float x, float z)
            {
                int ix = Mathf.Clamp(Mathf.RoundToInt(x - gx0), 0, nx - 1), iz = Mathf.Clamp(Mathf.RoundToInt(z - gz0), 0, nz - 1);
                return nearest[iz * nx + ix];
            }

            public float Half(float x, float z) => half[Nearest(x, z)];

            /// Wandhoogte (in treden) van de cel op (x, z); 0 = geen wand (land buiten de kloof of de bodem).
            public int Level(float x, float z, int tiers)
            {
                float D = Dist(x, z);
                if (D > 1e3f) return 0;
                int i = Nearest(x, z);
                float t0 = half[i] - D;                       // hoe ver binnen de rand
                if (t0 < 0f) return 0;
                // binnenkant van de rand onregelmatig, maar altijd minstens 1,7 m breed (daaronder zakt het terrein weg)
                float j1 = (Mathf.PerlinNoise(x * 0.29f + 13.1f + seed, z * 0.29f + 7.7f) - 0.5f) * 1.3f;
                float edge = Mathf.Max(1.7f, rimBand[i] + j1);
                if (t0 <= edge) return tiers;
                if (falls.Any(f => HorizontalDistance(f.sheer, x, z) < 7f)) return 0;     // achter een waterval: één rechte wand
                float t = t0 - edge;
                float j2 = (Mathf.PerlinNoise(x * 0.37f + 3.9f, z * 0.37f + 21.4f + seed) - 0.5f) * 1.2f;
                if (w1[i] > 0f && t <= w1[i] + j2) return l1[i];
                if (w2[i] > 0f && t <= w1[i] + w2[i] + j2 * 0.8f) return l2[i];
                if (w3[i] > 0f && t <= w1[i] + w2[i] + w3[i] + j2 * 0.6f) return talus[i];
                return 0;
            }
        }

        // Houdt bij waar al iets staat (minimale onderlinge afstand).
        sealed class SampleFreeGrid
        {
            readonly float cell;
            readonly Dictionary<long, List<Vector3>> cells = new Dictionary<long, List<Vector3>>();
            public SampleFreeGrid(float cellSize) => cell = cellSize;

            public bool TryTake(Vector3 p, float spacing)
            {
                int cx = Mathf.FloorToInt(p.x / cell), cz = Mathf.FloorToInt(p.z / cell);
                int r = Mathf.CeilToInt(spacing / cell);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                        if (cells.TryGetValue(((long)(cx + dx) << 32) ^ (uint)(cz + dz), out var list))
                            foreach (var q in list)
                                if ((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < spacing * spacing) return false;
                long k = ((long)cx << 32) ^ (uint)cz;
                if (!cells.TryGetValue(k, out var own)) cells[k] = own = new List<Vector3>();
                own.Add(p);
                return true;
            }
        }
    }
}
