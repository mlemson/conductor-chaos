// Conductor Chaos — gedeelde bouwer voor de kloof-levels (issue #224 level 2, #228 level 3). Elk level is een LevelSpec
// (zie Level02Builder.cs en Level03Builder.cs); deze bouwer maakt er een speelbare scène van:
//   1. ServiceLevel01 wordt gekopieerd: trein, perrons, reizigers, vracht, oma, wissel, bel en gereedschap zijn daarmee al
//      goed ingesteld. Decor en de oude route ertussen gaan eruit; het eindstation schuift mee naar het eind van de route.
//   2. De route wordt stuk voor stuk aan de spoorkit gekoppeld (sockets). Stukken kunnen hellen (grade, zoals de
//      spoorbouwer ze op de grond laat volgen), wissels hebben een zijspoor met stootblok.
//   3. Kloven (één CliffMass per kloof, getrapte wanden, rivier, watervallen, rotspilaren) worden langs en onder het spoor
//      uitgezet; het terrein volgt het spoor (insnijdingen, taluds), ligt vlak op randhoogte langs de kloven en loopt
//      naar de rand op tot een ring van rotswanden met hoogvlaktes erachter. Losse rotsplateaus in de vallei.
//   4. Bos, rotsen, planten (ook op klifplateaus, richels en de kloofbodem), hekken langs de kloofranden, bergen en wolken.
//   5. Gameplay: vertrekopdrachten + brugrails + 'Zet de wissels goed', incidenten, tijdslimieten.
// Alles wat later in Unity wordt aangepast, blijft in de scène; opnieuw bouwen overschrijft het level.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ConductorChaos.Art;
using ConductorChaos.Playground;
using ConductorChaos.Nature;
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
    public static class CliffLevelBuilder
    {
        // ================================================================== spec
        public sealed class Step
        {
            public string prefab, tag;
            public float grade;                 // helling van het begin van het stuk in graden (+ = omhoog)
            public bool isSwitch, mainDiverges; // wissel: rijdt de hoofdroute over de afbuigende tak?
            public Step[] branch;               // de andere tak (zijspoor, eindigt met een stootblok)
        }

        public enum PartKind { Cross, Along }

        public sealed class CanyonPart
        {
            public PartKind kind;
            public string[] before, after, tags; // Cross: rand vóór de brug, brugtag (bridge), rand erna; Along: tags
            public string bridge;
            public int side = 1;                 // +1 = rechts van het spoor (Cross: vóór de brug), -1 = links
            public float offset = 24f;           // Along: afstand middenlijn-spoor
        }

        public sealed class CanyonSpec
        {
            public string name = "Kloof";
            public CanyonPart[] parts;
            public int tiers = 13;               // diepte in treden van 2,4 m
            public float riverHalf = 3.4f;
            public float minHalf = 9f, maxHalf = 27f;
            public Vector3 armStart = new Vector3(26f, 80f, 38f);   // straal, graden, rechtdoor
            public Vector3 armEnd = new Vector3(26f, 70f, 44f);
            public bool headFall = true;
            public int sideFalls = 2;
            public int pillars = 3;
            public float gap = 48f;              // minimale afstand tot eerdere kloven (middenlijn)
        }

        public sealed class CliffSpec
        {
            public string name;
            public string[] tags;
            public bool left;
            public float from = 5.6f, to = 13f, above = 4.6f;
            public bool stone;
            public string rockfall;              // naam van een steenlawine vanaf deze wand (of null)
        }

        public sealed class RailSpec { public string tag, title, instructions; }
        public sealed class DamageSpec { public string tag, name; public float chance = 0.6f; }

        public sealed class LevelSpec
        {
            public int number;
            public string scene, label, title, departureName, arrivalName;
            public int seed;
            public Step[] route;
            public CanyonSpec[] canyons;
            public CliffSpec[] cliffs;
            public RailSpec[] rails;
            public DamageSpec[] damages;
            public bool rotateDepartureObjectives;
            public bool switchFaults;            // wissels op de hoofdroute kunnen onderweg storen
            public int minimumIncidents = 1;
            public int[] timeLimits;
            public int extraWrenches = 1;
            public int mesas = 10;
            public bool cliffRing = true;
            public bool winter;                  // sneeuwlevel (#242): sneeuwgrond, besneeuwde bomen/kliffen, ijs, kou (#243)
            public bool forest;                  // boslevel (#246): dicht loofbos, bosgrond, varens, stammen die van de helling rollen
        }

        public static Step S(string prefab, string tag, float grade = 0f) => new Step { prefab = prefab, tag = tag, grade = grade };

        public static Step Switch(string prefab, string tag, bool mainDiverges, params Step[] branch) =>
            new Step { prefab = prefab, tag = tag, isSwitch = true, mainDiverges = mainDiverges, branch = branch };

        public static CanyonPart Cross(string[] before, string bridge, string[] after, int side) =>
            new CanyonPart { kind = PartKind.Cross, before = before, bridge = bridge, after = after, side = side };

        public static CanyonPart Along(string[] tags, int side, float offset = 24f) =>
            new CanyonPart { kind = PartKind.Along, tags = tags, side = side, offset = offset };

        public static Step[] Repeat(int count, string prefab, string tag, float grade = 0f) =>
            Enumerable.Range(0, count).Select(_ => S(prefab, tag, grade)).ToArray();

        // ================================================================== constanten
        const string SourceScene = "Assets/Scenes/" + ServiceRun.FirstLevelScene + ".unity";
        const string TrackDir = "Assets/Art/Prefabs/Track";
        const string EnvDir = "Assets/Art/Models/Environment";
        const string PropDir = "Assets/Art/Prefabs/Station/Props";
        const string WinterRoot = "Winterdecor";
        const float RailTop = 0.43f;            // sockets liggen op de bovenkant van de rails
        const float StepHeight = 2.4f;          // CliffMass-trede
        const float RimClear = 4.5f;            // de kloofrand blijft zo ver van het spoor
        const float CapClearance = 0.65f;       // terrein onder een klifcel ligt zo ver onder het grasdek (niets steekt erdoor)
        const float RiverMargin = 2.5f;         // rivierbedding + oever: hier staan nooit wanden

        static string TargetScene(LevelSpec spec) => $"Assets/Scenes/{spec.scene}.unity";
        static string DataFolder(LevelSpec spec) => $"Assets/Scenes/{spec.scene}_Data";

        public static bool Exists(LevelSpec spec) => AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene(spec));
        public static bool SourceExists => AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene);

        public static void BuildMenu(LevelSpec spec)
        {
            if (Exists(spec) &&
                !EditorUtility.DisplayDialog($"Level {spec.number} opnieuw bouwen",
                    $"{spec.scene} bestaat al. Opnieuw bouwen maakt een verse kopie van level 1 en overschrijft je " +
                    $"aanpassingen aan level {spec.number}. Doorgaan?", "Opnieuw bouwen", "Annuleren"))
                return;
            Build(spec);
        }

        // ================================================================== hoofdroutine
        public static void Build(LevelSpec spec)
        {
            string tag = $"Level {spec.number}";
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(tag, "Stop eerst de Play-modus.", "OK");
                return;
            }
            if (!SourceExists)
            {
                EditorUtility.DisplayDialog(tag, SourceScene + $" ontbreekt; level {spec.number} wordt daarvan afgeleid.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string target = TargetScene(spec), data = DataFolder(spec);
            var log = new StringBuilder();
            progressTitle = $"Level {spec.number} bouwen";
            try
            {
                Progress("Scène kopiëren", 0.02f);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(target)) AssetDatabase.DeleteAsset(target);
                if (AssetDatabase.IsValidFolder(data)) AssetDatabase.DeleteAsset(data);
                if (!AssetDatabase.CopyAsset(SourceScene, target)) throw new Exception("Kopiëren van level 1 mislukt.");
                AssetDatabase.CreateFolder("Assets/Scenes", spec.scene + "_Data");
                var scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Single);

                new LevelContext(spec, scene, log).Run();

                Progress("Opslaan", 0.96f);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                RefreshNetworkIds(scene, log, tag);
                EditorSceneManager.SaveScene(scene);
                AddToBuildSettings(target, log);
                AssetDatabase.SaveAssets();
                Debug.Log($"[{tag}] Gebouwd.\n" + log);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog($"{tag} gebouwd",
                    $"{spec.scene} staat klaar en is toegevoegd aan de buildlijst.\n\n" +
                    $"Speel het vorige level uit en kies 'Naar level {spec.number}', of open {spec.scene} en verfijn met de " +
                    "Prop Painter en de Klif Massa Painter. Details staan in de Console.", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(e);
                Debug.LogError($"[{tag}] Bouwen afgebroken.\n" + log);
                EditorUtility.DisplayDialog(tag, "Bouwen is mislukt: " + e.Message + "\n\nDetails staan in de Console.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static string progressTitle = "Level bouwen";
        static void Progress(string what, float t) => EditorUtility.DisplayProgressBar(progressTitle, what, t);

        // Levels in speelvolgorde in de buildlijst: direct na het vorige dienstlevel.
        static void AddToBuildSettings(string target, StringBuilder log)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == target)) return;
            int after = -1;
            for (int i = 0; i < scenes.Count; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
                if (ServiceRun.IsServiceScene(name) &&
                    Array.IndexOf(ServiceRun.ServiceScenes, name) < Array.IndexOf(ServiceRun.ServiceScenes, System.IO.Path.GetFileNameWithoutExtension(target)))
                    after = i;
            }
            scenes.Insert(after >= 0 ? after + 1 : scenes.Count, new EditorBuildSettingsScene(target, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            log.AppendLine("Toegevoegd aan de buildlijst.");
        }

        // In-scene NetworkObjects hebben een unieke GlobalObjectIdHash nodig. Nieuwe en gekopieerde objecten krijgen die
        // pas na een OnValidate in de opgeslagen scène; daarom hier expliciet, en daarna controleren op dubbelen.
        static void RefreshNetworkIds(Scene scene, StringBuilder log, string tag)
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
            if (duplicates > 0) Debug.LogWarning($"[{tag}] {duplicates} NetworkObject(s) zonder unieke GlobalObjectIdHash. Open de scène en sla hem nog één keer op.");
        }

        // ================================================================== één bouwbeurt
        sealed class Sample
        {
            public Vector3 rail;        // hartlijn op railhoogte
            public Vector3 dir;
            public float arc;           // afstand langs de hoofdroute (anders -1)
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

        sealed class Junction
        {
            public TrackSwitch sw;
            public bool mainDiverges;
            public string tag;
            public float arc;
        }

        sealed class StationArea
        {
            public Bounds bounds;
            public float y;
        }

        sealed class LevelContext
        {
            readonly LevelSpec spec;
            readonly Scene scene;
            readonly StringBuilder log;
            readonly Random rng;

            ServiceRun run;
            TrainDrive train;
            ServiceStation departure, arrival;
            readonly List<Placed> route = new List<Placed>();
            readonly List<Placed> branches = new List<Placed>();
            readonly List<Junction> junctions = new List<Junction>();
            readonly List<Sample> samples = new List<Sample>();
            SampleGrid grid;
            readonly List<StationArea> stationAreas = new List<StationArea>();
            Terrain terrain;
            TerrainData data;
            Transform propsRoot;
            float departureY;
            readonly List<Canyon> canyons = new List<Canyon>();
            readonly Dictionary<string, CliffMass> cliffsByName = new Dictionary<string, CliffMass>();
            Regional regional;
            Ring ring;

            public LevelContext(LevelSpec spec, Scene scene, StringBuilder log)
            {
                this.spec = spec;
                this.scene = scene;
                this.log = log;
                rng = new Random(spec.seed);
            }

            public void Run()
            {
                Progress("Level 1 inlezen", 0.05f);
                FindCore();
                Progress("Oud decor opruimen", 0.08f);
                ClearScenery();
                int marks = ServiceMarkerTools.ReplaceIn(scene, log);        // doelvakken en bakens als echte modellen (#229)
                if (marks > 0) log.AppendLine($"Doelvakken en bakens vernieuwd: {marks}.");
                Progress("Nieuwe route leggen", 0.12f);
                var arrivalGroup = SplitTrack(out var routeStart, out var arrivalFirst);
                departureY = routeStart.EndPosition.y - RailTop;
                LayRoute(routeStart);
                MoveArrival(arrivalGroup, arrivalFirst);
                Progress("Spoor bemonsteren", 0.18f);
                SampleTrack();
                MeasureStations(arrivalGroup);
                Progress("Kloven uitzetten", 0.2f);
                foreach (var c in spec.canyons ?? new CanyonSpec[0]) PlanCanyon(c);
                foreach (var c in canyons) CacheWallLevels(c);
                Progress("Terrein maken", 0.24f);
                BuildTerrain();
                propsRoot = Root("PaintedProps").transform;
                Progress("Kloofwanden", 0.55f);
                foreach (var c in canyons) BuildCanyonWalls(c);
                foreach (var c in canyons) BuildPillars(c);
                Progress("Rotswanden en plateaus", 0.6f);
                foreach (var c in spec.cliffs ?? new CliffSpec[0])
                    cliffsByName[c.name] = BuildCliff(c.name, c.tags, c.left, c.from, c.to, c.above, c.stone);
                if (ring != null) BuildRing();
                BuildMesas();
                Physics.SyncTransforms();
                foreach (var c in canyons) BuildWaterfalls(c);
                BuildFences();
                Progress("Gameplay", 0.66f);
                SetupGameplay();
                Progress("Bos en rotsen", 0.72f);
                PlantNature();
                if (spec.winter) DressWinter();
                if (spec.forest)
                {
                    Progress("Bosgrond en water", 0.86f);
                    Physics.SyncTransforms();
                    log.AppendLine("Bos: " + NatureDresser.DressForest(scene));
                }
                Progress("Water en achtergrond", 0.9f);
                foreach (var c in canyons) BuildRiver(c);
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
                    // decor van level 1 (ook een eigen landschap/terrein): het nieuwe level krijgt nieuw decor
                    bool decor = n.StartsWith("Tree_") || n.StartsWith("Hill_Grass") || n.StartsWith("Farm_") || n == "Grasland" ||
                                 n.StartsWith("Bush_") || n.StartsWith("Rock_") || n == "PaintedProps" || n.StartsWith("Landroute") ||
                                 n.StartsWith("Cloud_") || n.StartsWith("Mountain_") || n.StartsWith("Grass_") || n.StartsWith("Flower_") ||
                                 n == NatureDresser.RootName || n == WinterRoot ||
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
            Transform trackParent;
            float maxKink;

            GameObject LoadTrack(string prefabName)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackDir}/{prefabName}.prefab");
                if (!prefab && AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackKitPrefabs.ModelDir}/{prefabName}.fbx"))
                {
                    TrackKitPrefabs.Build();               // nieuw model (bv. de stenen brug) nog zonder prefab
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TrackDir}/{prefabName}.prefab");
                }
                if (!prefab) throw new Exception($"Spoorstuk {prefabName} ontbreekt in {TrackDir}.");
                return prefab;
            }

            // Eén stuk neerleggen met zijn begin op 'p' in richting 'd', hellend met 'grade' graden.
            TrackPiece Put(Step step, Vector3 p, Vector3 d)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(LoadTrack(step.prefab), scene);
                var piece = go.GetComponent<TrackPiece>();
                if (!piece) throw new Exception($"{step.prefab} heeft geen TrackPiece.");
                if (!piece.HasSockets) piece.FindSockets();
                // eerst om de verticale as (rechtop blijven), dan kantelen om de dwarsas door het begin (helling)
                float yaw = Vector3.SignedAngle(Flat(piece.StartDirection), Flat(d), Vector3.up);
                go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * go.transform.rotation;
                if (Mathf.Abs(step.grade) > 0.01f)
                {
                    var across = Vector3.Cross(Vector3.up, Flat(d));
                    go.transform.RotateAround(piece.StartPosition, across, -step.grade);
                }
                go.transform.position += p - piece.StartPosition;
                go.transform.SetParent(trackParent, true);
                go.name = $"{step.prefab} · {step.tag}";
                float kink = Vector3.Angle(piece.StartDirection, d);
                maxKink = Mathf.Max(maxKink, kink);
                return piece;
            }

            void LayRoute(TrackPiece start)
            {
                trackParent = Root("Spoor · route").transform;
                var end = LaySteps(spec.route, start, 0f, true, out float arc);
                log.AppendLine($"Nieuwe route: {route.Count} spoorstukken, {arc:0} m, {junctions.Count} wissels, {branches.Count} stukken zijspoor, " +
                               $"eindigt op hoogte {end.EndPosition.y - RailTop:0.0} m; grootste knik {maxKink:0.0}°.");
                if (maxKink > 3.5f) Debug.LogWarning($"[Level {spec.number}] Knik van {maxKink:0.0}° tussen twee spoorstukken.");
            }

            TrackPiece LaySteps(Step[] steps, TrackPiece previous, float arc, bool main, out float arcEnd)
            {
                Vector3 p = previous.EndPosition, d = previous.EndDirection;
                foreach (var step in steps)
                {
                    var piece = Put(step, p, d);
                    previous.next = piece;
                    Dirty(previous);
                    TrackPiece exit = piece;
                    if (step.isSwitch)
                    {
                        var sw = piece.GetComponent<TrackSwitch>();
                        if (!sw || !sw.Diverge) throw new Exception($"{step.prefab} is geen wissel met afbuigende route.");
                        if (!sw.Diverge.HasSockets) sw.Diverge.FindSockets();
                        var mainRoute = step.mainDiverges ? sw.Diverge : sw.Straight;
                        var other = step.mainDiverges ? sw.Straight : sw.Diverge;
                        // begint verkeerd: naar het zijspoor (de spelers moeten hem omzetten)
                        var so = new SerializedObject(sw);
                        so.FindProperty("startDiverging").boolValue = !step.mainDiverges;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        Dirty(sw);
                        if (main) junctions.Add(new Junction { sw = sw, mainDiverges = step.mainDiverges, tag = step.tag, arc = arc });
                        (main ? route : branches).Add(new Placed { piece = mainRoute, tag = step.tag, arcStart = main ? arc : -1f, arcEnd = main ? arc + mainRoute.Length : -1f });
                        branches.Add(new Placed { piece = other, tag = step.tag + "·zijspoor", arcStart = -1f, arcEnd = -1f });
                        if (step.branch != null && step.branch.Length > 0) LaySteps(step.branch, other, 0f, false, out _);
                        exit = mainRoute;
                        if (main) arc += mainRoute.Length;
                    }
                    else
                    {
                        float length = piece.Length;
                        (main ? route : branches).Add(new Placed { piece = piece, tag = step.tag, arcStart = main ? arc : -1f, arcEnd = main ? arc + length : -1f });
                        if (main) arc += length;
                    }
                    p = exit.EndPosition;
                    d = exit.EndDirection;
                    previous = exit;
                }
                arcEnd = arc;
                return previous;
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
                log.AppendLine($"Eindstation verplaatst naar ({to.x:0.0}, {to.y - RailTop:0.0}, {to.z:0.0}), {yaw:0.0}° gedraaid.");
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
                var tagOf = route.Concat(branches).GroupBy(r => r.piece).ToDictionary(g => g.Key, g => g.First());
                foreach (var piece in InScene<TrackPiece>())
                {
                    if (!piece.HasSockets || piece.Length < 0.01f) continue;
                    tagOf.TryGetValue(piece, out var placed);
                    bool bridge = piece.name.Contains("Bridge") || piece.transform.root.name.Contains("Bridge");
                    int n = Mathf.Max(2, Mathf.CeilToInt(piece.Length / 0.75f));
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n;
                        piece.Evaluate(t, out var pos, out var dir);
                        samples.Add(new Sample
                        {
                            rail = pos, dir = Flat(dir), bridge = bridge, tag = placed?.tag ?? "station",
                            arc = placed != null && placed.arcStart >= 0f ? Mathf.Lerp(placed.arcStart, placed.arcEnd, t) : -1f,
                        });
                    }
                }
                grid = new SampleGrid(samples, 8f);
                log.AppendLine($"Spoor: {samples.Count} meetpunten, hoogte {samples.Min(s => s.Pivot):0.0} tot {samples.Max(s => s.Pivot):0.0} m.");
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
                float arrivalY = route[route.Count - 1].piece.EndPosition.y - RailTop;
                var areas = new[] { (Area(departureRoots), departureY), (Area(arrivalGroup.Where(t => t && !t.GetComponentInChildren<TrackPiece>())), arrivalY) };
                foreach (var (area, y) in areas)
                    if (area != null)
                    {
                        var b = area.Value;
                        b.Expand(new Vector3(6f, 0f, 6f));
                        stationAreas.Add(new StationArea { bounds = b, y = y });
                    }
                log.AppendLine($"Stationsterreinen: {stationAreas.Count} (vertrek {departureY:0.0} m, aankomst {arrivalY:0.0} m).");
            }

            bool NearStation(Vector3 p, float margin) => stationAreas.Any(a => DistanceToBox(a.bounds, p.x, p.z) < margin);

            // -------------------------------------------------------------- kloven uitzetten
            // Een kloof is een middenlijn uit delen. Cross: een S onder een rechte brug door (kwartcirkels om begin en eind van
            // de brug met straal = halve brug, zodat hij het spoor precies haaks in het midden kruist en de rand overal even ver
            // van het spoor blijft). Along: evenwijdig aan het spoor. Aan beide uiteinden een uitloper die van het spoor wegdraait.
            void PlanCanyon(CanyonSpec cs)
            {
                var core = new List<Vector3>();
                Vector3 startHeading = Vector3.zero, startTurn = Vector3.zero, endHeading = Vector3.zero, endTurn = Vector3.zero;
                float c0 = 24f, rim = float.NaN;
                var rimSides = new List<(string[] tags, int side)>();
                for (int k = 0; k < cs.parts.Length; k++)
                {
                    var part = cs.parts[k];
                    Vector3 sh, st, eh, et;
                    float c;
                    if (part.kind == PartKind.Cross)
                    {
                        var before = Section(part.before);
                        var bridge = Section(new[] { part.bridge });
                        var after = Section(part.after);
                        if (before.Count == 0 || bridge.Count == 0 || after.Count == 0)
                            throw new Exception($"{cs.name}: route mist {string.Join("/", part.before)}, {part.bridge} of {string.Join("/", part.after)}.");
                        Vector3 a0 = Flat0(before[0].piece.StartPosition), b0 = Flat0(bridge[0].piece.StartPosition);
                        Vector3 b1 = Flat0(bridge[bridge.Count - 1].piece.EndPosition), e = Flat0(after[after.Count - 1].piece.EndPosition);
                        Vector3 dir = (b1 - b0).normalized, sv = Vector3.Cross(Vector3.up, dir) * part.side;
                        if (Vector3.Angle(dir, (b0 - a0).normalized) > 2f || Vector3.Angle(dir, (e - b1).normalized) > 2f)
                            Debug.LogWarning($"[Level {spec.number}] {cs.name}: rand en brug liggen niet in één lijn.");
                        c = Vector3.Distance(b0, b1) * 0.5f;
                        Segment(core, a0 + sv * c, b0 + sv * c);
                        Bend(core, b0, c, sv, dir);
                        Bend(core, b1, c, -dir, -sv);
                        Segment(core, b1 - sv * c, e - sv * c);
                        sh = -dir; st = sv; eh = dir; et = -sv;
                        rim = bridge[0].piece.StartPosition.y - RailTop - 0.15f;
                        rimSides.Add((part.before, part.side));
                        rimSides.Add((part.after, -part.side));
                    }
                    else
                    {
                        var pts = samples.Where(s => s.arc >= 0f && part.tags.Contains(s.tag)).OrderBy(s => s.arc).ToList();
                        if (pts.Count < 2) throw new Exception($"{cs.name}: route mist {string.Join("/", part.tags)}.");
                        c = part.offset;
                        foreach (var s in pts) core.Add(Flat0(s.rail) + Vector3.Cross(Vector3.up, s.dir) * part.side * c);
                        Vector3 t0 = pts[0].dir, t1 = pts[pts.Count - 1].dir;
                        sh = -t0; st = Vector3.Cross(Vector3.up, t0) * part.side;
                        eh = t1; et = Vector3.Cross(Vector3.up, t1) * part.side;
                        if (float.IsNaN(rim)) rim = pts.Average(s => s.Pivot) - 0.15f;
                        rimSides.Add((part.tags, part.side));
                    }
                    if (k == 0) { startHeading = sh; startTurn = st; c0 = c; }
                    endHeading = eh; endTurn = et;
                }

                var west = Arm(core[0], startHeading, startTurn, cs.armStart, c0, cs.gap);
                west.Reverse();
                var east = Arm(core[core.Count - 1], endHeading, endTurn, cs.armEnd, c0, cs.gap);
                var all = new List<Vector3>(west);
                all.AddRange(core);
                all.AddRange(east);

                var canyon = new Canyon(cs, Resample(all, 1f), (x, z) =>
                {
                    var s = grid.Nearest(x, z, 70f, q => !q.bridge);
                    return s != null ? HorizontalDistance(s.rail, x, z) : 70f;
                }, (float)rng.NextDouble() * 100f, rim, rimSides);

                // watervallen: de kopwand van de eerste uitloper (bron van de rivier) en zijwanden van de uitlopers
                var line = canyon.line;
                if (cs.headFall && west.Count > 12) canyon.AddFall(line[0], (line[0] - line[1]).normalized, cs.tiers > 8 ? 1.8f : 1.3f, true);
                var spots = new List<float>();
                if (west.Count > 30) spots.Add(west.Count * 0.5f);
                if (east.Count > 30) spots.Add(canyon.Length - east.Count * 0.5f);
                foreach (float at in spots.Take(cs.sideFalls))
                {
                    int i = canyon.IndexAt(at);
                    var n = canyon.Normal(i);
                    var track = grid.Nearest(line[i].x, line[i].z, 120f, q => true);
                    if (track != null && Vector3.Dot(n, Flat0(line[i] - track.rail)) < 0f) n = -n;   // de wand die je vanaf het spoor ziet
                    canyon.AddFall(line[i], n, cs.tiers > 8 ? 1.5f : 1.2f, false);
                }
                canyon.BuildFields();
                canyons.Add(canyon);
                log.AppendLine($"{cs.name}: {canyon.Length:0} m lang, rand op {canyon.rimY:0.0} m, bodem {canyon.floorY:0.0} m " +
                               $"({cs.tiers * StepHeight:0} m diep), uitlopers {west.Count}/{east.Count} m, {canyon.falls.Count} watervallen.");
            }

            List<Placed> Section(string[] tags) => route.Where(r => tags.Contains(r.tag)).ToList();

            // Uitloper: vanaf 'start' in richting 'heading', in een boog naar 'turn' toe, dan rechtdoor. Stopt waar hij
            // ander spoor of een eerdere kloof te dicht zou naderen.
            List<Vector3> Arm(Vector3 start, Vector3 heading, Vector3 turn, Vector3 shape, float c, float gap)
            {
                var pts = new List<Vector3>();
                float radius = shape.x, arc = radius * shape.y * Mathf.Deg2Rad, total = arc + shape.z;
                var p = start;
                for (float s = 1f; s <= total; s += 1f)
                {
                    float phi = Mathf.Min(s, arc) / radius;
                    p += heading * Mathf.Cos(phi) + turn * Mathf.Sin(phi);
                    var near = grid.Nearest(p.x, p.z, c - 1f, q => !q.bridge);
                    if (s > 8f && near != null) break;
                    if (canyons.Any(o => o.NearLine(p, gap))) break;
                    pts.Add(p);
                }
                return pts;
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

            bool InCanyon(Vector3 p, float extra) => canyons.Any(c => c.Dist(p.x, p.z) < c.Half(p.x, p.z) + extra);
            Canyon CanyonAt(Vector3 p, float extra) => canyons.FirstOrDefault(c => c.Dist(p.x, p.z) < c.Half(p.x, p.z) + extra);
            float RiverDist(Vector3 p) => canyons.Count == 0 ? 1e4f : canyons.Min(c => c.RiverDist(p.x, p.z) - c.riverHalf);

            // -------------------------------------------------------------- terrein
            Vector3 terrainOrigin, terrainSize;
            int res;
            float[,] heights;
            FarField far;

            void BuildTerrain()
            {
                var min = new Vector3(float.MaxValue, 0f, float.MaxValue);
                var max = new Vector3(float.MinValue, 0f, float.MinValue);
                foreach (var s in samples)
                {
                    min = Vector3.Min(min, s.rail);
                    max = Vector3.Max(max, s.rail);
                }
                foreach (var c in canyons)
                    foreach (var p in c.line)
                    {
                        min = Vector3.Min(min, p - new Vector3(c.half.Max() + 12f, 0f, c.half.Max() + 12f));
                        max = Vector3.Max(max, p + new Vector3(c.half.Max() + 12f, 0f, c.half.Max() + 12f));
                    }
                // rondom ruimte voor de vallei, de ring van rotswanden en de hoogvlakte erachter
                const float margin = 125f;
                float low = Mathf.Min(samples.Min(s => s.Pivot), canyons.Count > 0 ? canyons.Min(c => c.originY) : 0f) - 6f;
                float high = samples.Max(s => s.Pivot) + 60f;
                terrainOrigin = new Vector3(Mathf.Floor(min.x - margin), Mathf.Floor(low), Mathf.Floor(min.z - margin));
                terrainSize = new Vector3(Mathf.Ceil(max.x - min.x + 2 * margin), Mathf.Ceil(high - low), Mathf.Ceil(max.z - min.z + 2 * margin));
                float longest = Mathf.Max(terrainSize.x, terrainSize.z);
                res = longest > 820f ? 2049 : longest > 420f ? 1025 : 513;

                var layers = spec.winter ? StylizedTerrainTools.EnsureWinterLayers()
                           : spec.forest ? StylizedTerrainTools.EnsureForestLayers() : StylizedTerrainTools.EnsureLayers();
                data = new TerrainData { heightmapResolution = res, alphamapResolution = longest > 820f ? 2048 : 1024 };
                data.size = terrainSize;
                data.terrainLayers = layers;

                Progress("Terrein: afstandsvelden", 0.25f);
                far = new FarField(samples, terrainOrigin, terrainSize, 6f);
                regional = new Regional(samples, terrainOrigin, terrainSize, 8f, departureY);
                float noiseX = rng.Next(1000), noiseZ = rng.Next(1000);
                if (spec.cliffRing) ring = new Ring(terrainOrigin, terrainSize, rng, regional);

                heights = new float[res, res];
                for (int z = 0; z < res; z++)
                {
                    if ((z & 31) == 0) Progress("Terrein: hoogtes", 0.27f + 0.2f * z / res);
                    for (int x = 0; x < res; x++)
                    {
                        var w = World(x, z);
                        float y = Height(w.x, w.z, noiseX, noiseZ);
                        heights[z, x] = Mathf.Clamp01((y - terrainOrigin.y) / terrainSize.y);
                    }
                }
                data.SetHeights(0, 0, heights);
                PaintLayers(layers.Length, noiseX, noiseZ);

                AssetDatabase.CreateAsset(data, DataFolder(spec) + $"/Level{spec.number:00}_Terrain.asset");
                var go = Terrain.CreateTerrainGameObject(data);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.name = "Terrein · kliffenvallei";
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
                log.AppendLine($"Terrein {terrainSize.x:0} × {terrainSize.z:0} m (hoogte {terrainOrigin.y:0} tot {terrainOrigin.y + terrainSize.y:0} m), heightmap {res}.");
            }

            Vector3 World(int x, int z) => new Vector3(
                terrainOrigin.x + x / (float)(res - 1) * terrainSize.x, 0f,
                terrainOrigin.z + z / (float)(res - 1) * terrainSize.z);

            static float Noise(float x, float z, float scale, float ox, float oz) => Mathf.PerlinNoise(ox + x / scale, oz + z / scale);

            // Boslevel (#246): hoe dicht het bos hier is (0 = open plek, 1 = dicht bos). Vaste ruisverschuiving per level, zodat de
            // bosgrond (terreinkleur) en de bomen precies op elkaar vallen. Rond de stations open weide.
            float ForestAt(float x, float z)
            {
                float ox = spec.seed % 977, oz = spec.seed % 613;
                float n = Noise(x, z, 70f, ox, oz) * 0.75f + Noise(x, z, 24f, ox + 41f, oz + 17f) * 0.35f;
                float wood = Mathf.Clamp01((n - 0.30f) * 2.6f);
                foreach (var area in stationAreas)
                    wood *= Smooth(10f, 34f, DistanceToBox(area.bounds, x, z));
                return wood;
            }

            static float Fbm(float x, float z, float ox, float oz) =>
                Noise(x, z, 90f, ox, oz) * 0.55f + Noise(x, z, 38f, ox + 31f, oz + 7f) * 0.3f + Noise(x, z, 15f, ox + 77f, oz + 51f) * 0.15f;

            float Height(float x, float z, float ox, float oz)
            {
                float dAll = far.Distance(x, z);                                   // afstand tot het dichtstbijzijnde spoor
                float n = Fbm(x, z, ox, oz);
                float away = Smooth(18f, 90f, dAll);
                float baseY = regional.Height(x, z);                               // het land volgt het spoor in grote lijnen
                // glooiend land, hoger en woester naarmate je verder van het spoor komt
                float relief = (n - 0.45f) * (1.6f + 9f * away) + away * 9f * Mathf.Pow(Noise(x, z, 60f, ox + 300f, oz + 90f), 2f);
                if (ring != null) relief *= Mathf.Lerp(1f, 0.3f, Smooth(-80f, -25f, ring.Inside(x, z)));   // rustig dal voor de rotswand
                float h = baseY + relief;

                // het land rond een kloof ligt minstens op zijn randhoogte
                foreach (var c in canyons)
                {
                    float D = c.Dist(x, z);
                    if (D > 1e3f) continue;
                    float half = c.Half(x, z);
                    h = Mathf.Lerp(h, Mathf.Max(h, c.rimY + (n - 0.5f) * 2.4f), Smooth(half + 34f, half + 14f, D));
                }
                // de ring van rotswanden: daarbuiten een hoogvlakte op de top van de wand
                if (ring != null)
                {
                    float clear = RingClearance(x, z, dAll);
                    h = ring.Apply(x, z, h, baseY, clear, relief);
                }
                // vlak langs de kloofrand: de wanden (CliffMass) sluiten daar precies op aan
                bool inCanyon = false;
                foreach (var c in canyons)
                {
                    float D = c.Dist(x, z);
                    if (D > 1e3f) continue;
                    float half = c.Half(x, z);
                    h = Mathf.Lerp(h, c.rimY, Smooth(half + 16f, half + 8f, D));
                    if (D < half - 0.5f) inCanyon = true;
                }

                // onder het spoor vlak, met een talud of insnijding naar het land ernaast (niet onder de brug, niet in de kloof)
                var near = inCanyon ? null : grid.Nearest(x, z, 16f, s => !s.bridge);
                if (near != null)
                {
                    float d = HorizontalDistance(near.rail, x, z);
                    // voorbij het eind van een spoorstuk (brughoofd): korter talud
                    float lengthwise = Mathf.Abs((x - near.rail.x) * near.dir.x + (z - near.rail.z) * near.dir.z);
                    d += 1.5f * Mathf.Max(0f, lengthwise - 0.6f);
                    float bed = near.Pivot - 0.03f;
                    h = Mathf.Lerp(bed, h, Smooth(3.4f, 14f, d));
                }
                // onder het brugdek: grond altijd onder het dek (bij de brughoofden zakt het dek in de grond)
                var deck = grid.Nearest(x, z, 6f, s => s.bridge);
                if (deck != null)
                    h = Mathf.Lerp(Mathf.Min(h, deck.Pivot - 0.6f), h, Smooth(3.6f, 6f, HorizontalDistance(deck.rail, x, z)));

                // de kloof: op de bodem een rivierbedding; onder de wanden ligt het terrein net onder het laagste grasdek
                // van de cel en zijn buren (dan kan een schuine terreindriehoek nooit voor of door een wand steken)
                foreach (var c in canyons)
                {
                    float D = c.Dist(x, z);
                    if (D > 1e3f) continue;
                    float half = c.Half(x, z);
                    float r = c.RiverDist(x, z);
                    if (D >= half - 0.5f && r > c.riverHalf + RiverMargin) continue;
                    if (grid.Nearest(x, z, 3.2f, s => !s.bridge) != null) continue;          // spoorbed blijft
                    float bottom = c.floorY + 0.25f + Noise(x, z, 9f, ox + 70f, oz + 30f) * 0.5f;
                    bottom = Mathf.Lerp(c.waterY - Mathf.Min(1.3f, c.riverHalf * 0.4f + 0.3f), bottom, Smooth(c.riverHalf - 0.6f, c.riverHalf + 1.2f, r));
                    int cx = Mathf.RoundToInt(x), cz = Mathf.RoundToInt(z);
                    float support = float.MaxValue;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int level = c.WallLevel(cx + dx, cz + dz);
                            if (level > 0) support = Mathf.Min(support, c.originY + level * StepHeight - CapClearance);
                            else if (c.Dist(cx + dx, cz + dz) < c.Half(cx + dx, cz + dz)) support = Mathf.Min(support, bottom);
                        }
                    if (support < float.MaxValue) h = Mathf.Min(h, support);
                }

                // rond de stations vlak op perronhoogte
                foreach (var area in stationAreas)
                {
                    float d = DistanceToBox(area.bounds, x, z);
                    if (d < 14f) h = Mathf.Lerp(area.y - 0.03f, h, Smooth(0f, 14f, d));
                }
                return Mathf.Clamp(h, terrainOrigin.y + 0.2f, terrainOrigin.y + terrainSize.y - 0.2f);
            }

            // Hoe vrij de ring van rotswanden hier mag staan (1 = helemaal, 0 = niet): weg van spoor, kloven en stations.
            float RingClearance(float x, float z, float dAll)
            {
                float clear = Smooth(26f, 42f, dAll);
                foreach (var c in canyons)
                {
                    float D = c.Dist(x, z);
                    if (D > 1e3f) continue;
                    clear = Mathf.Min(clear, Smooth(c.Half(x, z) + 8f, c.Half(x, z) + 22f, D));
                }
                foreach (var area in stationAreas)
                    clear = Mathf.Min(clear, Smooth(20f, 36f, DistanceToBox(area.bounds, x, z)));
                return clear;
            }

            void PaintLayers(int layerCount, float ox, float oz)
            {
                // lagen (StylizedTerrainTools): 0 gras, 1 gras met bloemen, 2 grond, 3 stenen pad, 4 rots
                int ar = data.alphamapResolution;
                var a = new float[ar, ar, layerCount];
                for (int z = 0; z < ar; z++)
                {
                    if ((z & 63) == 0) Progress("Terrein: kleuren", 0.47f + 0.07f * z / ar);
                    for (int x = 0; x < ar; x++)
                    {
                        float wx = terrainOrigin.x + (x + 0.5f) / ar * terrainSize.x;
                        float wz = terrainOrigin.z + (z + 0.5f) / ar * terrainSize.z;
                        float grass = 1f, flowers = 0f, dirt = 0f, stones = 0f, rock = 0f;
                        float d = far.Distance(wx, wz);
                        float f = Noise(wx, wz, 22f, ox + 500f, oz + 500f);
                        if (spec.forest)
                        {
                            // bos: laag 1 = schaduwgras onder het bladerdak, laag 2 = bosgrond met blad (plekken in het dichte bos)
                            float wood = ForestAt(wx, wz) * Smooth(5f, 16f, d);
                            flowers = wood * 1.6f;
                            float litter = Noise(wx, wz, 9f, ox + 700f, oz + 300f);
                            if (wood > 0.45f && litter > 0.42f) dirt = Mathf.Clamp01((litter - 0.42f) * 4f) * wood * 1.4f;
                            grass = Mathf.Max(0.05f, 1f - wood);
                        }
                        else if (d > 7f && f > 0.6f) flowers = Mathf.Clamp01((f - 0.6f) * 6f);
                        if (d > 2.6f && d < 4.6f) dirt = 0.55f;                       // berm naast de bielzen
                        foreach (var c in canyons)
                        {
                            float D = c.Dist(wx, wz);
                            if (D > 1e3f) continue;
                            float half = c.Half(wx, wz);
                            if (D < half - 0.5f)
                            {
                                // kloofbodem: gras met kale plekken, grind en stenen langs de rivier
                                float r = c.RiverDist(wx, wz);
                                float g = Noise(wx, wz, 7f, ox + 90f, oz + 40f);
                                dirt = 0.25f + 0.5f * Mathf.Clamp01((0.55f - g) * 3f);
                                if (r < c.riverHalf + 1.6f) { stones = 0.8f; dirt = 0.4f; grass = 0.15f; flowers = 0f; }
                                else if (r < c.riverHalf + 3f) { stones = 0.3f; grass = 0.7f; }
                            }
                            else if (D < half + 2.5f) { dirt = Mathf.Max(dirt, 0.35f); flowers = 0f; }   // kale rand langs de kloof
                        }
                        foreach (var area in stationAreas)
                            if (DistanceToBox(area.bounds, wx, wz) < 3f) { stones = 0.5f; grass = 0.6f; }
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
                mass.snow = spec.winter;
                mass.tufts = true;
                mass.seed = seed;
                go.AddComponent<PaintedProp>().followGround = false;
                go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>();
                go.AddComponent<MeshCollider>();
                return mass;
            }

            void SetCell(CliffMass mass, float x, float z, int level)
            {
                var c = mass.CellAt(mass.transform.InverseTransformPoint(new Vector3(x, mass.transform.position.y, z)));
                if (level > mass.GetLevel(c)) mass.SetLevel(c, level);
            }

            /// Een klifwand naast het spoor: cellen van 'from' tot 'to' meter opzij, top minstens 'above' boven de rails.
            CliffMass BuildCliff(string name, string[] tags, bool left, float from, float to, float above, bool stone)
            {
                var section = samples.Where(s => s.arc >= 0f && tags.Contains(s.tag)).OrderBy(s => s.arc).ToList();
                if (section.Count == 0) return null;
                Vector3 Side(Sample s) => left ? Vector3.Cross(s.dir, Vector3.up) : Vector3.Cross(Vector3.up, s.dir);
                float originY = section.Min(s => GroundAt(s.rail + Side(s) * (from + 1f)));
                var mass = NewMass(name, section[0].rail + Side(section[0]) * (from + 2f), stone, rng.Next(1, 99999), originY - 0.4f);
                originY = mass.transform.position.y;
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
                        if (InCanyon(w, 3f)) continue;
                        float top = s.Pivot + above + (l > from + 4f ? StepHeight : 0f) + (l > from + 8f ? StepHeight : 0f);
                        int level = Mathf.Max(1, Mathf.CeilToInt((top - originY) / StepHeight));
                        var c = mass.CellAt(mass.transform.InverseTransformPoint(w));
                        if (level > mass.GetLevel(c)) { mass.SetLevel(c, level); cells++; }
                    }
                }
                CliffMassBuilder.Rebuild(mass);
                log.AppendLine($"{name}: {cells} cellen.");
                return mass;
            }

            // Eén CliffMass per kloof: getrapt profiel (rand, richels, puin) boven de bodem.
            void BuildCanyonWalls(Canyon canyon)
            {
                var mass = NewMass(canyon.spec.name + " · wanden", canyon.line[canyon.line.Length / 2], false, rng.Next(1, 99999), canyon.originY);
                int cells = 0;
                canyon.Bounds(out int x0, out int z0, out int x1, out int z1);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int level = canyon.WallLevel(x, z);
                        if (level <= 0) continue;
                        mass.SetLevel(mass.CellAt(mass.transform.InverseTransformPoint(new Vector3(x, canyon.originY, z))), level);
                        cells++;
                    }
                CliffMassBuilder.Rebuild(mass);
                canyon.walls = mass;
                log.AppendLine($"{canyon.spec.name}: {cells} wandcellen.");
            }

            // Wandhoogte per cel, één keer berekend: dezelfde getallen bouwen de wand (BuildCanyonWalls) en leggen het terrein
            // eronder (Height), zodat er nooit terrein door een grasdek steekt of een richel boven niets hangt.
            void CacheWallLevels(Canyon canyon)
            {
                canyon.CacheLevels((x, z) =>
                {
                    int level = canyon.Level(x, z);
                    if (level <= 0) return 0;
                    if (grid.Nearest(x, z, 3.2f, s => !s.bridge) != null) return 0;               // veiligheid: nooit op het spoor
                    if (canyons.Any(o => o != canyon && o.Dist(x, z) < o.Half(x, z) + 1f)) return 0;   // geen twee wanden door elkaar
                    return level;
                });
            }

            // Rotspilaren op de bodem van de wijde uitlopers, weg van het spoor, de rivier en de watervallen.
            void BuildPillars(Canyon canyon)
            {
                var placed = new List<Vector3>();
                for (int attempt = 0; attempt < 160 && placed.Count < canyon.spec.pillars; attempt++)
                {
                    int i = rng.Next(4, Mathf.Max(5, canyon.line.Length - 4));
                    if (canyon.nearTrack[i] < 38f) continue;
                    float radius = 2.6f + (float)rng.NextDouble() * 1.8f;
                    float offset = canyon.riverHalf + 2.2f + radius;
                    if (offset + radius > canyon.FloorHalf(i) - 0.8f) continue;
                    var center = canyon.line[i] + canyon.Normal(i) * (rng.NextDouble() < 0.5 ? -offset : offset);
                    if (placed.Any(q => Vector3.Distance(q, center) < 16f) || canyon.NearFall(center, radius + 6f)) continue;
                    var mass = NewMass("Rotspilaar in " + canyon.spec.name.ToLowerInvariant(), center, true, rng.Next(1, 99999), canyon.originY);
                    int top = Mathf.Max(3, canyon.tiers - 2 - rng.Next(6));
                    float seed = (float)rng.NextDouble() * 100f;
                    int r = Mathf.CeilToInt(radius + 2f);
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            var w = new Vector3(Mathf.Round(center.x) + dx, 0f, Mathf.Round(center.z) + dz);
                            if (canyon.WallLevel(w.x, w.z) > 0 || canyon.RiverDist(w.x, w.z) < canyon.riverHalf + 0.8f) continue;
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
                if (placed.Count > 0) log.AppendLine($"{canyon.spec.name}: {placed.Count} rotspilaren.");
            }

            // De ring van rotswanden rond de vallei: losse stukken met spleten ertussen, elk een eigen CliffMass.
            void BuildRing()
            {
                int built = 0, total = 0;
                foreach (var seg in ring.segments)
                {
                    CliffMass mass = null;
                    int cells = 0;
                    ring.SegmentBounds(seg, out int x0, out int z0, out int x1, out int z1);
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            if (!ring.InBand(seg, x, z)) continue;
                            if (RingClearance(x, z, far.Distance(x, z)) < 0.5f) continue;
                            if (!mass) mass = NewMass("Rotswand rond de vallei", new Vector3(x, 0f, z), seg.stone, rng.Next(1, 99999), seg.originY);
                            SetCell(mass, x, z, ring.LevelAt(seg, x, z));
                            cells++;
                        }
                    if (!mass) continue;
                    CliffMassBuilder.Rebuild(mass);
                    built++;
                    total += cells;
                }
                log.AppendLine($"Rotswanden rond de vallei: {built} stukken, {total} cellen.");
            }

            // Losse rotsplateaus (getrapt, plat bovenop) in de vallei: richtpunten en hoogteverschil in de verte.
            void BuildMesas()
            {
                int made = 0, big = 0;
                for (int attempt = 0; attempt < 400 && made < spec.mesas; attempt++)
                {
                    var p = new Vector3(terrainOrigin.x + 40f + (float)rng.NextDouble() * (terrainSize.x - 80f), 0f,
                                        terrainOrigin.z + 40f + (float)rng.NextDouble() * (terrainSize.z - 80f));
                    bool large = rng.NextDouble() < 0.45;
                    float radius = large ? 11f + (float)rng.NextDouble() * 7f : 4.5f + (float)rng.NextDouble() * 4f;
                    if (far.Distance(p.x, p.z) < radius + (large ? 30f : 18f) || InCanyon(p, radius + 12f) || NearStation(p, radius + 26f)) continue;
                    if (ring != null && ring.Inside(p.x, p.z) > -radius - 14f) continue;
                    var mass = NewMass(large ? "Rotsplateau" : "Klif in het heuvelland", p, rng.NextDouble() < 0.35, rng.Next(1, 99999), GroundAt(p) - 1.5f);
                    // de voet ligt overal onder de grond: zoek het laagste punt onder de cellen
                    float lowest = float.MaxValue;
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k / 16f * Mathf.PI * 2f;
                        lowest = Mathf.Min(lowest, GroundAt(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius));
                    }
                    var pos = mass.transform.position;
                    pos.y = Mathf.Min(pos.y, lowest - 0.6f);
                    mass.transform.position = pos;
                    if (large) Mesa(mass, p, radius, 3 + rng.Next(5), 22f);
                    else Blob(mass, p, radius, 2 + rng.Next(3), 14f);
                    CliffMassBuilder.Rebuild(mass);
                    made++;
                    if (large) big++;
                }
                log.AppendLine($"Rotsplateaus en losse kliffen in de vallei: {made} ({big} groot).");
            }

            // Plat getrapt plateau: brede top, één of twee lagere treden rondom, puin aan de voet.
            void Mesa(CliffMass mass, Vector3 center, float radius, int tiers, float trackClearance)
            {
                float seed = (float)rng.NextDouble() * 100f;
                float groundY = mass.transform.position.y;
                int extra = Mathf.CeilToInt((GroundAt(center) - groundY) / StepHeight);       // voet onder de grond
                int r = Mathf.CeilToInt(radius + 4f);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var w = new Vector3(Mathf.Round(center.x) + dx, 0f, Mathf.Round(center.z) + dz);
                        float angle = Mathf.Atan2(dz, dx);
                        float edge = radius * (0.7f + 0.6f * Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.1f, seed + Mathf.Sin(angle) * 1.1f));
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > edge + 2.2f) continue;
                        if (grid.Nearest(w.x, w.z, trackClearance, s => true) != null) continue;
                        float layer = Mathf.PerlinNoise(w.x * 0.21f + seed, w.z * 0.21f);
                        int level = dist < edge * 0.68f ? tiers : dist < edge * (0.84f + 0.1f * layer) ? tiers - 1 : dist < edge ? Mathf.Max(1, tiers - 2 - (layer > 0.5f ? 1 : 0)) : 1;
                        SetCell(mass, w.x, w.z, level + extra);
                    }
            }

            // Ronde, onregelmatige vlek cellen; het midden hoger (treden), richting rand lager.
            void Blob(CliffMass mass, Vector3 center, float radius, int tiers, float trackClearance)
            {
                float seed = (float)rng.NextDouble() * 100f;
                int extra = Mathf.CeilToInt((GroundAt(center) - mass.transform.position.y) / StepHeight);
                int r = Mathf.CeilToInt(radius + 3f);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var w = new Vector3(Mathf.Round(center.x) + dx, 0f, Mathf.Round(center.z) + dz);
                        float angle = Mathf.Atan2(dz, dx);
                        float edge = radius * (0.75f + 0.5f * Mathf.PerlinNoise(seed + Mathf.Cos(angle) * 1.3f, seed + Mathf.Sin(angle) * 1.3f));
                        float dist = Mathf.Sqrt(dx * dx + dz * dz);
                        if (dist > edge) continue;
                        if (grid.Nearest(w.x, w.z, trackClearance, s => true) != null) continue;
                        int level = Mathf.Clamp(1 + Mathf.FloorToInt((1f - dist / edge) * tiers * 1.2f), 1, tiers);
                        SetCell(mass, w.x, w.z, level + extra);
                    }
            }

            void BuildWaterfalls(Canyon canyon)
            {
                var model = Model("Water", "Waterfall_Large");
                var wall = canyon.walls ? canyon.walls.GetComponent<Collider>() : null;
                if (!model || !wall || canyon.falls.Count == 0) return;
                Physics.SyncTransforms();
                int made = 0;
                foreach (var fall in canyon.falls)
                {
                    // horizontaal vanuit de kloof naar de wand, een paar meter onder de rand
                    var from = new Vector3(fall.inside.x, canyon.RimTop - Mathf.Min(3.5f, canyon.tiers * StepHeight * 0.4f), fall.inside.z);
                    if (!wall.Raycast(new Ray(from, fall.dir), out var hit, 80f)) continue;
                    var foot = hit.point - fall.dir * 0.55f;
                    float ground = GroundAt(foot);
                    float height = canyon.RimTop + 0.06f - ground;
                    if (height < 2f) continue;
                    var go = Place(model, "Water", new Vector3(foot.x, ground, foot.z), Quaternion.LookRotation(-fall.dir).eulerAngles.y,
                        1f, 1f, 0f, false);
                    if (!go) continue;
                    // Waterfall_Large is 4,6 m hoog: alleen in de hoogte rekken, zodat de lip precies op de kloofrand ligt
                    go.transform.localScale = new Vector3(fall.width, height / 4.6f, fall.width * 0.8f);
                    go.GetComponent<PaintedProp>().followGround = false;
                    go.name = fall.head ? "Waterval · bron van de rivier" : "Waterval in de kloofwand";
                    if (spec.winter) Freeze(go);
                    made++;
                }
                log.AppendLine($"{canyon.spec.name}: {made} watervallen.");
            }

            // Houten hek langs de kloofkant van het spoor, bovenop de rotsrand.
            void BuildFences()
            {
                var fence = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Station/Farm/Farm_Fence_3m.fbx");
                if (!fence) { log.AppendLine("Hek overgeslagen: Farm_Fence_3m ontbreekt."); return; }
                Physics.SyncTransforms();
                int count = 0;
                foreach (var canyon in canyons)
                    foreach (var (tags, side) in canyon.rimSides)
                    {
                        var section = route.Where(r => tags.Contains(r.tag)).ToList();
                        if (section.Count == 0) continue;
                        float from = section[0].arcStart + 1.2f, to = section[section.Count - 1].arcEnd - 1.2f;
                        // 3,06 m per stuk, om de 3,15 m: palen raken elkaar niet (geen dubbele vlakken)
                        for (float a = from; a + 3.1f <= to; a += 3.15f)
                        {
                            var s = SampleAt(a);
                            var e = SampleAt(a + 3.06f);
                            if (s == null || e == null || s.bridge || e.bridge) continue;
                            var across = Vector3.Cross(Vector3.up, s.dir) * side;
                            var p = s.rail + across * (RimClear + 0.7f);
                            // alleen waar de rand echt naast het spoor ligt (niet in bochten waar de kloof wegdraait)
                            if (canyon.Dist(p.x, p.z) > canyon.Half(p.x, p.z) - 0.2f || canyon.Dist(p.x, p.z) < canyon.Half(p.x, p.z) - 1.6f) continue;
                            float y = CliffTop(p, out var top, out _) ? Mathf.Max(top, GroundAt(p)) : GroundAt(p);
                            float yaw = Quaternion.LookRotation(Vector3.Cross(Flat(e.rail - s.rail), Vector3.up)).eulerAngles.y;
                            var go = Place(fence, "Fences", new Vector3(p.x, y - 0.03f, p.z), yaw, 1f, 1f, -0.03f, true);
                            if (go) { go.GetComponent<PaintedProp>().followGround = false; count++; }
                        }
                    }
                log.AppendLine($"Hek langs de kloofranden: {count} stukken.");
            }

            // -------------------------------------------------------------- gameplay
            void SetupGameplay()
            {
                // stations
                Rename(departure, spec.departureName, spec.departureName + " · vertrek");
                Rename(arrival, spec.arrivalName, spec.arrivalName + " · aankomst");

                var runSo = new SerializedObject(run);
                var list = runSo.FindProperty("objectives");
                void AddObjective(ServiceObjective objective)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = objective;
                }
                void Describe(ServiceObjective objective, string title, string instructions, string icon)
                {
                    var objSo = new SerializedObject(objective);
                    objSo.FindProperty("title").stringValue = title;
                    objSo.FindProperty("instructions").stringValue = instructions;
                    objSo.FindProperty("icon").stringValue = icon;
                    objSo.ApplyModifiedPropertiesWithoutUndo();
                }

                // kapotte brugrails (opdracht per rail)
                foreach (var railSpec in spec.rails ?? new RailSpec[0])
                {
                    var placed = route.FirstOrDefault(r => r.tag == railSpec.tag);
                    var point = placed?.piece.GetComponent<RepairPoint>();
                    if (!point) throw new Exception($"Route mist een Track_Broken_6m met tag {railSpec.tag}.");
                    point.Configure(true);
                    Dirty(point);
                    var go = new GameObject(railSpec.title);
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var objective = go.AddComponent<ServiceTrackRepair>();
                    objective.Configure(point, true);
                    Describe(objective, railSpec.title, railSpec.instructions, "wrench");
                    AddObjective(objective);
                    PlaceSign(railSpec.tag, "Waarschuwing · " + railSpec.title.ToLowerInvariant());
                }

                // wissels: beginnen naar het zijspoor; zet ze om voordat de trein erover rijdt
                if (junctions.Count > 0)
                {
                    var go = new GameObject("Zet de wissels goed");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var objective = go.AddComponent<ServiceSwitchRoute>();
                    objective.Configure(junctions.Select(j => j.sw).ToArray(), junctions.Select(j => j.mainDiverges).ToArray());
                    Describe(objective, "Zet de wissels goed",
                        $"Er liggen {junctions.Count} wissels op de route en ze staan allemaal naar een zijspoor. Stop ervoor, " +
                        "loop naar de hendel en duw hem met je hand naar de andere kant. Rijd je toch het zijspoor op: " +
                        "achteruit terug tot vóór de wissel.", "route");
                    AddObjective(objective);
                    foreach (var j in junctions)
                    {
                        var repair = j.sw.GetComponent<RepairPoint>();
                        if (repair) { repair.Configure(false); Dirty(repair); }
                    }
                }

                runSo.FindProperty("combineObjectives").boolValue = true;
                runSo.FindProperty("rotateDepartureObjectives").boolValue = spec.rotateDepartureObjectives;
                runSo.FindProperty("levelLabel").stringValue = spec.label;
                var limits = runSo.FindProperty("timeLimits");
                limits.arraySize = spec.timeLimits.Length;
                for (int i = 0; i < spec.timeLimits.Length; i++) limits.GetArrayElementAtIndex(i).intValue = spec.timeLimits[i];
                runSo.ApplyModifiedPropertiesWithoutUndo();
                run.gameObject.name = $"Level {spec.number} · spelregels";
                var titles = new List<string>();
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue is ServiceObjective o) titles.Add(o.Title);
                log.AppendLine("Ingestelde opdrachten: " + string.Join(", ", titles) + ".");

                // extra moersleutels: er zijn meer plekken om te repareren
                var wrench = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Wrench.prefab");
                var firstWrench = scene.GetRootGameObjects().FirstOrDefault(r => r.name.StartsWith("Prop_Wrench"));
                if (wrench && firstWrench)
                    for (int k = 0; k < spec.extraWrenches; k++)
                    {
                        var extra = (GameObject)PrefabUtility.InstantiatePrefab(wrench, scene);
                        extra.transform.SetPositionAndRotation(firstWrench.transform.position + departure.transform.right * (1.3f * (k + 1)),
                            firstWrench.transform.rotation);
                        extra.name = $"Prop_Wrench (reserve {k + 1})";
                    }

                // incidenten
                var incidentsGo = new GameObject("Incidenten onderweg");
                SceneManager.MoveGameObjectToScene(incidentsGo, scene);
                incidentsGo.AddComponent<NetworkObject>();
                var incidents = incidentsGo.AddComponent<ServiceIncidents>();
                var rockfalls = new List<ServiceIncidents.Rockfall>();
                foreach (var c in spec.cliffs ?? new CliffSpec[0])
                {
                    if (string.IsNullOrEmpty(c.rockfall) || !cliffsByName.TryGetValue(c.name, out var mass) || !mass) continue;
                    var rf = MakeRockfall(incidentsGo.transform, c.rockfall, mass, c.tags, c.left, c.from);
                    if (rf != null) rockfalls.Add(rf);
                }
                var damages = new List<ServiceIncidents.TrackDamage>();
                foreach (var d in spec.damages ?? new DamageSpec[0])
                {
                    var placed = route.FirstOrDefault(r => r.tag == d.tag);
                    var point = placed?.piece.GetComponent<RepairPoint>();
                    if (!point) continue;
                    point.Configure(false);             // heel bij de start; een incident kan hem breken
                    Dirty(point);
                    damages.Add(new ServiceIncidents.TrackDamage
                    {
                        name = d.name, trigger = spec.rotateDepartureObjectives ? null : Trigger(incidentsGo.transform, "Trigger " + d.name.ToLowerInvariant(), placed.arcStart - 58f),
                        repair = point, chance = d.chance,
                    });
                }
                if (spec.switchFaults)
                    foreach (var j in junctions.Where(j => j.arc > 70f))      // niet vlak voor het vertrekperron
                    {
                        var repair = j.sw.GetComponent<RepairPoint>();
                        if (!repair) continue;
                        damages.Add(new ServiceIncidents.TrackDamage
                        {
                            name = "Wisselstoring " + j.tag, trigger = Trigger(incidentsGo.transform, "Trigger wisselstoring " + j.tag, j.arc - 62f),
                            repair = repair, chance = 0.3f,
                        });
                    }
                var warnings = junctions.Select(j => new ServiceIncidents.SwitchWarning
                {
                    approach = Trigger(incidentsGo.transform, "Nadering wissel " + j.tag, j.arc - 24f, 40f),
                    junction = j.sw, mainDiverges = j.mainDiverges,
                }).ToArray();
                incidents.Configure(rockfalls.ToArray(), damages.ToArray(), spec.minimumIncidents, warnings, spec.rotateDepartureObjectives);
                EditorUtility.SetDirty(incidents);
                log.AppendLine($"Incidenten: {rockfalls.Count} steenlawines, {damages.Count} spoorschade/wisselstoringen " +
                               $"(minstens {spec.minimumIncidents} per rit), {warnings.Length} wisselwaarschuwingen.");
            }

            void PlaceSign(string tag, string name)
            {
                var sign = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvDir}/Track/Prop_WarningSign.fbx");
                var placed = route.FirstOrDefault(r => r.tag == tag);
                if (!sign || placed == null) return;
                var s = SampleAt(placed.arcStart - 4f);
                if (s == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(sign, scene);
                go.transform.SetParent(Category("Props"), true);
                // aan de kant zonder kloof
                var left = Vector3.Cross(s.dir, Vector3.up);
                var side = InCanyon(s.rail + left * 6f, 0f) ? -left : left;
                var pos = s.rail + side * 3.4f;
                pos.y = GroundAt(pos);
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-s.dir));
                go.name = name;
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

            BoxCollider Trigger(Transform parent, string name, float arc, float length = 10f)
            {
                var s = SampleAt(Mathf.Max(0f, arc));
                var go = new GameObject(name) { layer = 2 };
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(s.rail + Vector3.up * 1f, Quaternion.LookRotation(s.dir));
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(9f, 7f, length);
                return box;
            }

            ServiceIncidents.Rockfall MakeRockfall(Transform parent, string name, CliffMass cliff, string[] tags, bool left, float from)
            {
                var section = route.Where(r => tags.Contains(r.tag)).ToList();
                if (section.Count == 0) return null;
                // midden van het laatste stuk van de wand: daar ligt de wand het dichtst langs het spoor
                var last = section[section.Count - 1];
                float mid = Mathf.Max(section[0].arcStart + 6f, last.arcEnd - Mathf.Min(14f, (last.arcEnd - section[0].arcStart) * 0.4f));
                var site = SampleAt(mid);
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                // rotsblokken zelf niet onder het NetworkObject van de incidenten (geen geneste NetworkObjects)
                var rockRoot = Root("Rotsblokken · steenlawines").transform;
                var siteMarker = new GameObject("Plek op het spoor").transform;
                siteMarker.SetParent(group, false);
                siteMarker.SetPositionAndRotation(site.rail, Quaternion.LookRotation(site.dir));

                string modelPath = spec.winter ? $"{EnvDir}/Winter/Snow_Pile_Track.fbx" : spec.forest ? $"{EnvDir}/Wood/Log_Fallen_02.fbx" : $"{EnvDir}/Rocks/Rock_Medium_01.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (!model) throw new Exception($"{modelPath} ontbreekt.");
                var rocks = new List<Rigidbody>();
                var landing = new List<Transform>();
                for (int k = 0; k < 3; k++)
                {
                    var s = SampleAt(mid + (k - 1) * 3.2f);
                    var side = left ? Vector3.Cross(s.dir, Vector3.up) : Vector3.Cross(Vector3.up, s.dir);
                    // klaar op de klifrand: recht boven de wand, iets naar binnen
                    var perch = s.rail + side * (from + 1.6f);
                    float top = TopOf(perch, cliff);
                    var rock = MakeRock(rockRoot, $"{name} · {(spec.winter ? "sneeuw" : spec.forest ? "stam" : "blok")} {k + 1}", model, new Vector3(perch.x, top, perch.z),
                        spec.winter ? 0.6f + 0.08f * k : spec.forest ? 0.78f + 0.06f * k : 0.85f + 0.12f * k);
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
                    chance = 0.7f,
                    warning = spec.winter ? "Sneeuw op het spoor" : spec.forest ? "Boomstammen op het spoor" : "Rotsen op het spoor",
                };
            }

            float TopOf(Vector3 at, CliffMass cliff)
            {
                Physics.SyncTransforms();
                var collider = cliff.GetComponent<Collider>();
                if (collider && collider.Raycast(new Ray(new Vector3(at.x, 400f, at.z), Vector3.down), out var hit, 800f))
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
                var trees = spec.forest ? ForestTrees() : spec.winter ? new (GameObject model, float weight, float clearance)[]
                {
                    (Model("Winter", "Tree_Pine_Snow_Medium"), 1.0f, 6.5f),
                    (Model("Winter", "Tree_Pine_Snow_Large"), 0.7f, 7.5f),
                    (Model("Winter", "Tree_Pine_Snow_Small"), 0.9f, 5.5f),
                } : new (GameObject model, float weight, float clearance)[]
                {
                    (Model("Trees", "Tree_Pine_Medium_01"), 1.0f, 6.5f),
                    (Model("Trees", "Tree_Pine_Large_01"), 0.65f, 7.5f),
                    (Model("Trees", "Tree_Pine_Small_01"), 0.8f, 5.5f),
                    (Model("Trees", "Tree_Pine_Variant_02"), 0.6f, 7f),
                    (Model("Trees", "Bush_01"), 0.7f, 4.2f),
                };
                trees = trees.Where(t => t.model).ToArray();
                float area = terrainSize.x * terrainSize.z;
                int maxTrees = spec.forest ? Mathf.RoundToInt(Mathf.Clamp(area / 170f, 1800f, 3400f))
                                           : Mathf.RoundToInt(Mathf.Clamp(area / 260f, 1400f, 3600f));
                int planted = 0, onCliffs = 0;
                float ox = rng.Next(1000), oz = rng.Next(1000);
                var taken = new SampleFreeGrid(4f);
                int candidates = Mathf.RoundToInt(area / 9f);
                for (int i = 0; i < candidates && trees.Length > 0 && planted < maxTrees; i++)
                {
                    if ((i & 1023) == 0) Progress("Bomen planten", 0.72f + 0.12f * i / candidates);
                    var p = new Vector3(terrainOrigin.x + 4f + (float)rng.NextDouble() * (terrainSize.x - 8f), 0f,
                                        terrainOrigin.z + 4f + (float)rng.NextDouble() * (terrainSize.z - 8f));
                    float d = far.Distance(p.x, p.z);
                    // bos in plukken: dicht waar de ruis hoog is, open weides rond de stations
                    float density;
                    if (spec.forest) density = ForestAt(p.x, p.z) * 1.6f;          // dicht loofbos tot vlak langs het spoor
                    else
                    {
                        float forest = Noise(p.x, p.z, 55f, ox, oz) * 0.8f + Noise(p.x, p.z, 18f, ox + 40f, oz + 9f) * 0.35f;
                        density = Mathf.Clamp01((forest - 0.48f) * 2.4f) * (0.35f + 0.65f * Smooth(6f, 30f, d));
                        if (NearStation(p, 14f)) density *= 0.15f;
                    }
                    if (rng.NextDouble() > density * 0.55f) continue;
                    var pick = Pick(trees, t => t.weight);
                    float treeScale = 0.85f + (float)rng.NextDouble() * 0.45f;
                    // bos: de kruin blijft naast het spoor (kruinstraal schaalt mee), zodat niets boven de trein hangt
                    if (d < pick.clearance * (spec.forest ? treeScale : 1f) || NearStation(p, 4f)) continue;
                    bool small = pick.model.name.StartsWith("Bush") || pick.model.name.Contains("Young") || pick.model.name.Contains("Dead");
                    if (!taken.TryTake(p, small ? 2f : spec.forest ? 4.2f : 3.1f)) continue;

                    // op de grond of bovenop een klif (alleen ver van het spoor)
                    bool onCliff = false;
                    float y;
                    if (CliffTop(p, out var top, out var normal))
                    {
                        if (d < 10f || normal.y < 0.9f || rng.NextDouble() < 0.3) continue;
                        y = top;
                        onCliff = true;
                    }
                    else
                    {
                        p.y = GroundAt(p);
                        if (SteepnessAt(p) > 27f) continue;
                        // op de kloofbodem wat minder bomen, nooit in de rivierbedding
                        if (InCanyon(p, 0f) && (RiverDist(p) < 1.6f || rng.NextDouble() < 0.4)) continue;
                        y = p.y;
                    }
                    float sink = 0.08f;
                    var go = Place(pick.model, "Trees", new Vector3(p.x, y - sink, p.z), (float)rng.NextDouble() * 360f,
                        treeScale, 0.9f + (float)rng.NextDouble() * 0.3f, -sink, d < 45f || NearStation(p, 40f));
                    if (go) { planted++; if (onCliff) onCliffs++; }
                }

                // rotsen bij klifvoeten en kloofranden
                var rockModels = (spec.winter ? new[] { Model("Rocks", "Rock_Small_01"), Model("Winter", "Rock_Snow_Medium"), Model("Winter", "Rock_Snow_Large") }
                    : new[] { Model("Rocks", "Rock_Small_01"), Model("Rocks", "Rock_Medium_01"), Model("Rocks", "Rock_Large_01") })
                    .Where(m => m).ToArray();
                int rocks = 0, maxRocks = Mathf.RoundToInt(Mathf.Clamp(area / 2500f, 140f, 360f));
                for (int i = 0; i < maxRocks * 20 && rocks < maxRocks && rockModels.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < 5.5f || NearStation(p, 6f)) continue;
                    p.y = GroundAt(p);
                    if (RiverDist(p) < 0.4f) continue;
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

                // gras en bloemen op vlakke stukken langs het spoor, rond de stations en op de kloofbodem (geen colliders)
                var plants = (spec.winter ? new[] { Model("Winter", "Snow_Drift_01"), Model("Winter", "Snow_Drift_01"), Model("Winter", "Snow_Drift_02"), Model("Winter", "Rock_Snow_Medium") }
                    : spec.forest ? new[] { Model("Plants", "Grass_Tuft_01"), Model("Plants", "Grass_Tuft_02"), Model("Plants", "Fern_01"), Model("Plants", "Flower_Wild_01") }
                    : new[] { Model("Plants", "Grass_Tuft_01"), Model("Plants", "Grass_Tuft_02"), Model("Plants", "Flower_Patch_01"), Model("Plants", "Flower_Daisy_01") })
                    .Where(m => m).ToArray();
                int plantCount = 0, maxPlants = Mathf.RoundToInt(Mathf.Clamp(area / 600f, 560f, 1200f));
                for (int i = 0; i < maxPlants * 25 && plantCount < maxPlants && plants.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    bool station = NearStation(p, 18f) && !NearStation(p, 1f);
                    bool canyonFloor = InCanyon(p, -2f) && RiverDist(p) > 0.8f;
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

            // Boslevel (#246): grote loofbomen voor een echt bos (boskit, nature_v4) met wat naaldbomen en struiken ertussen.
            // De vrije afstand tot het spoor is kruinstraal + ruim 2,5 m.
            (GameObject model, float weight, float clearance)[] ForestTrees() => new (GameObject model, float weight, float clearance)[]
            {
                (Model("Trees", "Tree_Beech_Tall_01"), 1.0f, 6.8f),
                (Model("Trees", "Tree_Beech_Tall_02"), 0.8f, 7.2f),
                (Model("Trees", "Tree_Oak_Giant_01"), 0.3f, 7.6f),
                (Model("Trees", "Tree_Linden_01"), 0.5f, 5.6f),
                (Model("Trees", "Tree_Maple_01"), 0.45f, 5.6f),
                (Model("Trees", "Tree_Maple_Autumn_01"), 0.18f, 5.6f),
                (Model("Trees", "Tree_Maple_Red_01"), 0.1f, 5.6f),
                (Model("Trees", "Tree_Chestnut_01"), 0.28f, 6.2f),
                (Model("Trees", "Tree_Poplar_01"), 0.2f, 4.4f),
                (Model("Trees", "Tree_Beech_Young_01"), 0.7f, 4.2f),
                (Model("Trees", "Tree_Dead_01"), 0.1f, 4.5f),
                (Model("Trees", "Tree_Oak_01"), 0.35f, 5.2f),
                (Model("Trees", "Tree_Birch_01"), 0.25f, 4.4f),
                (Model("Trees", "Tree_Pine_Large_01"), 0.2f, 7.5f),
                (Model("Trees", "Bush_Round_02"), 0.5f, 3.6f),
                (Model("Trees", "Bush_01"), 0.3f, 3.6f),
            };

            static bool CliffTop(Vector3 p, out float y, out Vector3 normal)
            {
                y = float.NegativeInfinity;
                normal = Vector3.up;
                foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 400f, p.z), Vector3.down, 800f, ~0, QueryTriggerInteraction.Ignore))
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
                marker.followGround = category != "Sky" && category != "Mountains";
                if (collider)
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                        if (mf.sharedMesh && !mf.GetComponent<Collider>())
                            mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                return go;
            }

            // Korte stukken delen hun oevers op de naden, ook als de rivier van richting verandert.
            void BuildRiver(Canyon canyon)
            {
                var surfaceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_Env_WaterSurface.mat");
                var tint = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Shaders/UnderwaterTint.shader");
                var parent = Root("Rivieren").transform;
                var group = new GameObject("Rivier in " + canyon.spec.name.ToLowerInvariant()).transform;
                group.SetParent(parent, false);
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
                    go.transform.SetParent(group, false);
                    var mid = (a + b) * 0.5f;
                    go.transform.SetPositionAndRotation(new Vector3(mid.x, canyon.waterY, mid.z),
                        Quaternion.LookRotation(Flat(b - a), Vector3.up));
                    var water = go.AddComponent<StylizedWater>();
                    water.size = new Vector2(2f * (canyon.riverHalf + 1.1f), length);
                    water.depth = Mathf.Min(1.6f, canyon.riverHalf * 0.5f + 0.4f);
                    water.surfaceMaterial = spec.winter ? IceMaterial() ?? surfaceMat : surfaceMat;
                    if (spec.winter) MakeIce(go, water.size, canyon.waterY);
                    water.tintShader = tint;
                    water.joinRiverSections = true;
                    count++;
                }
                foreach (Transform section in group) section.GetComponent<StylizedWater>().Rebuild();
                log.AppendLine($"{canyon.spec.name}: rivier van {count} stukken.");
            }

            // -------------------------------------------------------------- winter (#242, #243)
            Material IceMaterial() => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Environment/M_Env_Ice.mat");

            // Bevroren waterval: alles ijs.
            void Freeze(GameObject go)
            {
                var ice = IceMaterial();
                if (!ice) return;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(ice, r.sharedMaterials.Length).ToArray();
            }

            // Bevroren rivier: een dichte ijsvloer om op te lopen (glad), geen drijfvermogen.
            void MakeIce(GameObject section, Vector2 size, float y)
            {
                var water = section.GetComponent<StylizedWater>();
                if (water) water.buoyancy = 0f;
                var ice = new GameObject("IJsvloer");
                ice.layer = 0;
                ice.transform.SetParent(section.transform, false);
                var box = ice.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, -0.04f, 0f);
                box.size = new Vector3(size.x, 0.08f, size.y);
                ice.AddComponent<IceSurface>();
            }

            void DressWinter()
            {
                var winter = Root(WinterRoot).transform;
                var level = new GameObject("Kou en sneeuwval");
                level.transform.SetParent(winter, false);
                level.AddComponent<WinterLevel>();
                level.AddComponent<Snowfall>();
                // warme stationshal, gladde perrons
                int warm = 0, slippery = 0;
                foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray())
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                    if (go.name.StartsWith("Station_Building") || go.name.StartsWith("Station_Main"))
                    {
                        var rs = go.GetComponentsInChildren<Renderer>();
                        if (rs.Length == 0) continue;
                        var b = rs[0].bounds;
                        foreach (var r in rs) b.Encapsulate(r.bounds);
                        var zone = new GameObject($"Warm · {go.name}");
                        zone.transform.SetParent(winter, false);
                        zone.transform.position = b.center;
                        zone.AddComponent<BoxCollider>().size = b.size * 0.92f;
                        zone.AddComponent<WarmZone>();
                        warm++;
                    }
                    else if (go.name.StartsWith("Platform_") && !go.GetComponent<IceSurface>())
                    {
                        go.AddComponent<IceSurface>();
                        slippery++;
                    }
                }
                // decor: sneeuwpop bij elk station, houthakkershutten langs de route, een bevroren meertje
                var snowman = Model("Winter", "Snowman_01");
                var cabin = Model("Winter", "Cabin_01");
                var lake = Model("Winter", "Frozen_Lake_01");
                int men = 0, cabins = 0, lakes = 0;
                foreach (var area in stationAreas)
                    for (int k = 0; k < 40 && snowman; k++)
                    {
                        var c = area.bounds.center;
                        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = area.bounds.extents.magnitude * 0.6f + 6f + (float)rng.NextDouble() * 6f;
                        var p = new Vector3(c.x + Mathf.Cos(a) * r, 0f, c.z + Mathf.Sin(a) * r);
                        if (far.Distance(p.x, p.z) < 5f || NearStation(p, 3f)) continue;
                        p.y = GroundAt(p);
                        if (SteepnessAt(p) > 12f) continue;
                        var go = Place(snowman, "Winter", p + Vector3.down * 0.05f, (float)rng.NextDouble() * 360f, 1f, 1f, -0.05f, true);
                        if (go) { go.transform.SetParent(winter, true); men++; break; }
                    }
                for (int k = 0; k < 600 && cabins < 3 && cabin; k++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < 16f || d > 40f || NearStation(p, 20f) || InCanyon(p, 6f)) continue;
                    p.y = GroundAt(p);
                    if (SteepnessAt(p) > 8f || CliffTop(p, out _, out _)) continue;
                    var go = Place(cabin, "Winter", p + Vector3.down * 0.1f, (float)rng.NextDouble() * 360f, 1f, 1f, -0.1f, true);
                    if (!go) continue;
                    go.transform.SetParent(winter, true);
                    var smoke = new GameObject("Schoorsteenrook");
                    smoke.transform.SetParent(go.transform, false);
                    smoke.transform.localPosition = new Vector3(1.05f, 4.6f, -0.5f);
                    smoke.AddComponent<ChimneyPuffs>();
                    cabins++;
                }
                for (int k = 0; k < 800 && lakes < 1 && lake; k++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, 0f, terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    float d = far.Distance(p.x, p.z);
                    if (d < 22f || d > 70f || NearStation(p, 30f) || InCanyon(p, 10f)) continue;
                    p.y = GroundAt(p);
                    if (SteepnessAt(p) > 4f || CliffTop(p, out _, out _)) continue;
                    var go = Place(lake, "Winter", p + Vector3.up * 0.01f, (float)rng.NextDouble() * 360f, 1.2f, 1f, 0.01f, true);
                    if (!go) continue;
                    go.transform.SetParent(winter, true);
                    go.AddComponent<IceSurface>();
                    lakes++;
                }
                if (RenderSettings.fog) RenderSettings.fogColor = new Color(0.78f, 0.82f, 0.94f);
                log.AppendLine($"Winter: kou en sneeuwval aan, {warm} warme stationshallen, {slippery} gladde perronstukken, " +
                               $"{men} sneeuwpoppen, {cabins} hutten, {lakes} bevroren meer.");
            }

            void BuildBackdrop()
            {
                var mountains = new[] { Model("Mountains", "Mountain_Back_01"), Model("Mountains", "Mountain_Back_02"), Model("Mountains", "Mountain_Back_03") }
                    .Where(m => m).ToArray();
                var center = terrainOrigin + new Vector3(terrainSize.x * 0.5f, 0f, terrainSize.z * 0.5f);
                float rx = terrainSize.x * 0.5f + 80f, rz = terrainSize.z * 0.5f + 80f;
                float baseY = (ring != null ? ring.segments.Select(s => s.Top).DefaultIfEmpty(departureY).Average() : departureY) - 8f;
                int count = 0;
                int n = Mathf.Clamp(Mathf.RoundToInt((rx + rz) / 45f), 16, 30);
                for (int i = 0; i < n && mountains.Length > 0; i++)
                {
                    float angle = i / (float)n * Mathf.PI * 2f + (float)rng.NextDouble() * 0.15f;
                    var p = center + new Vector3(Mathf.Cos(angle) * rx, baseY, Mathf.Sin(angle) * rz);
                    if (Place(mountains[rng.Next(mountains.Length)], "Mountains", p, (float)rng.NextDouble() * 360f,
                            1.4f + (float)rng.NextDouble() * 1.0f, 0.9f + (float)rng.NextDouble() * 0.6f, 0f, false))
                        count++;
                }
                var clouds = new[] { Model("Sky", "Cloud_01"), Model("Sky", "Cloud_02"), Model("Sky", "Cloud_03") }.Where(m => m).ToArray();
                float skyY = samples.Max(s => s.Pivot) + 36f;
                int cloudCount = Mathf.Clamp(Mathf.RoundToInt(terrainSize.x * terrainSize.z / 30000f), 12, 30);
                for (int i = 0; i < cloudCount && clouds.Length > 0; i++)
                {
                    var p = new Vector3(terrainOrigin.x + (float)rng.NextDouble() * terrainSize.x, skyY + (float)rng.NextDouble() * 14f,
                                        terrainOrigin.z + (float)rng.NextDouble() * terrainSize.z);
                    Place(clouds[rng.Next(clouds.Length)], "Sky", p, (float)rng.NextDouble() * 360f, 1.5f + (float)rng.NextDouble(), 1f, 0f, false);
                }
                log.AppendLine($"Achtergrond: {count} bergen, {cloudCount} wolken.");
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
            readonly float[,] all;
            readonly Vector3 origin;
            readonly float step;
            readonly int nx, nz;

            public FarField(List<Sample> samples, Vector3 origin, Vector3 size, float step)
            {
                this.origin = origin;
                this.step = step;
                nx = Mathf.CeilToInt(size.x / step) + 2;
                nz = Mathf.CeilToInt(size.z / step) + 2;
                var chosen = samples.Where((s, i) => i % 2 == 0).ToArray();
                all = new float[nz, nx];
                for (int z = 0; z < nz; z++)
                    for (int x = 0; x < nx; x++)
                    {
                        float wx = origin.x + x * step, wz = origin.z + z * step, best = float.MaxValue;
                        foreach (var s in chosen)
                        {
                            float dx = s.rail.x - wx, dz = s.rail.z - wz, d = dx * dx + dz * dz;
                            if (d < best) best = d;
                        }
                        all[z, x] = chosen.Length > 0 ? Mathf.Sqrt(best) : 1e4f;
                    }
            }

            public float Distance(float x, float z)
            {
                float fx = Mathf.Clamp((x - origin.x) / step, 0f, nx - 1.001f), fz = Mathf.Clamp((z - origin.z) / step, 0f, nz - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(all[iz, ix], all[iz, ix + 1], tx), Mathf.Lerp(all[iz + 1, ix], all[iz + 1, ix + 1], tx), tz);
            }
        }

        // Hoogte van het land 'in grote lijnen': gewogen gemiddelde (Gauss, ~45 m) van de spoorhoogte, zodat het land
        // rond een hoog stuk route ook hoog ligt en het spoor niet overal op een dijk of in een sleuf loopt.
        sealed class Regional
        {
            readonly float[,] field;
            readonly Vector3 origin;
            readonly float step;
            readonly int nx, nz;

            public Regional(List<Sample> samples, Vector3 origin, Vector3 size, float step, float fallback)
            {
                this.origin = origin;
                this.step = step;
                nx = Mathf.CeilToInt(size.x / step) + 2;
                nz = Mathf.CeilToInt(size.z / step) + 2;
                var chosen = samples.Where((s, i) => i % 4 == 0).ToArray();
                const float sigma = 45f;
                field = new float[nz, nx];
                for (int z = 0; z < nz; z++)
                    for (int x = 0; x < nx; x++)
                    {
                        float wx = origin.x + x * step, wz = origin.z + z * step;
                        // geen afkapgrens: ver weg weegt het dichtstbijzijnde spoor nog steeds het zwaarst (geen treden in het land)
                        double sum = fallback * 1e-280, weight = 1e-280;
                        foreach (var s in chosen)
                        {
                            float dx = s.rail.x - wx, dz = s.rail.z - wz, d2 = dx * dx + dz * dz;
                            double w = Math.Exp(-d2 / (2.0 * sigma * sigma));
                            sum += w * (s.bridge ? s.Pivot - 1f : s.Pivot);
                            weight += w;
                        }
                        field[z, x] = (float)(sum / weight);
                    }
            }

            public float Height(float x, float z)
            {
                float fx = Mathf.Clamp((x - origin.x) / step, 0f, nx - 1.001f), fz = Mathf.Clamp((z - origin.z) / step, 0f, nz - 1.001f);
                int ix = (int)fx, iz = (int)fz;
                float tx = fx - ix, tz = fz - iz;
                return Mathf.Lerp(Mathf.Lerp(field[iz, ix], field[iz, ix + 1], tx), Mathf.Lerp(field[iz + 1, ix], field[iz + 1, ix + 1], tx), tz);
            }
        }

        // Ring van rotswanden langs de rand van het terrein (een afgeronde rechthoek). Langs de omtrek losse stukken met
        // spleten ertussen; elk stuk heeft een eigen hoogte. Buiten de ring ligt een hoogvlakte precies op de top van de wand.
        sealed class Ring
        {
            public sealed class Seg
            {
                public float u0, u1;             // stuk van de omtrek (0..1)
                public int tiers;
                public float originY;
                public bool stone;
                public float seed;
                public float Top => originY + tiers * StepHeight;
            }

            public readonly List<Seg> segments = new List<Seg>();
            readonly Vector2 center, halfSize;
            readonly float radius, perimeter;
            const float Inset = 36f;

            public Ring(Vector3 origin, Vector3 size, Random rng, Regional regional)
            {
                center = new Vector2(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);
                halfSize = new Vector2(size.x * 0.5f - Inset, size.z * 0.5f - Inset);
                radius = Mathf.Min(70f, Mathf.Min(halfSize.x, halfSize.y) * 0.5f);
                perimeter = 4f * (halfSize.x - radius) + 4f * (halfSize.y - radius) + 2f * Mathf.PI * radius;
                float u = (float)rng.NextDouble() * 0.02f;
                while (u < 1f)
                {
                    float length = (55f + (float)rng.NextDouble() * 85f) / perimeter;
                    float gap = (12f + (float)rng.NextDouble() * 20f) / perimeter;
                    var seg = new Seg { u0 = u, u1 = Mathf.Min(1f - 8f / perimeter, u + length), tiers = 4 + rng.Next(6),
                                        stone = rng.NextDouble() < 0.3, seed = (float)rng.NextDouble() * 100f };
                    if (seg.u1 - seg.u0 > 25f / perimeter)
                    {
                        // voet onder het laagste dal langs dit stuk
                        float lowest = float.MaxValue, highest = float.MinValue;
                        for (int k = 0; k <= 12; k++)
                        {
                            var p = PointAt(Mathf.Lerp(seg.u0, seg.u1, k / 12f), -30f);
                            float y = regional.Height(p.x, p.y);
                            lowest = Mathf.Min(lowest, y);
                            highest = Mathf.Max(highest, y);
                        }
                        seg.originY = lowest - 3f;
                        // de wand steekt overal minstens 8 m boven het dal ervoor uit
                        seg.tiers = Mathf.Clamp(Mathf.Max(seg.tiers, Mathf.CeilToInt((highest + 8f - seg.originY) / StepHeight)), 4, 15);
                        segments.Add(seg);
                    }
                    u += length + gap;
                }
            }

            // Getekende afstand tot de ring (negatief = in de vallei) en de plek langs de omtrek (0..1).
            public void Locate(float x, float z, out float sd, out float u)
            {
                float px = x - center.x, pz = z - center.y;
                float ax = halfSize.x - radius, az = halfSize.y - radius;
                float qx = Mathf.Abs(px) - ax, qz = Mathf.Abs(pz) - az;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qz, 0f)).magnitude;
                sd = outside + Mathf.Min(Mathf.Max(qx, qz), 0f) - radius;
                // omtrekpositie: hoek rond het midden, geschaald naar de rechthoek (monotoon, genoeg voor stukken en ruis)
                float angle = Mathf.Atan2(pz / halfSize.y, px / halfSize.x);
                u = (angle / (2f * Mathf.PI) + 1f) % 1f;
            }

            public float Inside(float x, float z)
            {
                Locate(x, z, out float sd, out _);
                return sd;
            }

            Vector2 PointAt(float u, float sd)
            {
                float angle = u * 2f * Mathf.PI;
                var dir = new Vector2(Mathf.Cos(angle) * halfSize.x, Mathf.Sin(angle) * halfSize.y);
                // zoek langs de straal de rand (sd = 0) en schuif dan sd op
                float lo = 0f, hi = 2f;
                for (int i = 0; i < 24; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    var q = center + dir * mid;
                    Locate(q.x, q.y, out float d, out _);
                    if (d < 0f) lo = mid; else hi = mid;
                }
                var edge = center + dir * lo;
                return edge + dir.normalized * sd;
            }

            float Width(Seg seg, float u) => 10f + 14f * Mathf.PerlinNoise(u * perimeter / 37f + seg.seed, 0.5f);

            Seg SegmentAt(float u)
            {
                for (int i = 0; i < segments.Count; i++)
                    if (u >= segments[i].u0 && u <= segments[i].u1) return segments[i];
                return null;
            }

            public bool InBand(Seg seg, float x, float z)
            {
                Locate(x, z, out float sd, out float u);
                return u >= seg.u0 && u <= seg.u1 && sd <= 0f && sd >= -Width(seg, u);
            }

            public int LevelAt(Seg seg, float x, float z)
            {
                Locate(x, z, out float sd, out float u);
                // binnenste meters een trede lager (richel), de rest op volle hoogte
                float ledge = 2f + 3f * Mathf.PerlinNoise(u * perimeter / 19f + seg.seed, 3.1f);
                return sd < -Width(seg, u) + ledge && seg.tiers > 4 ? seg.tiers - 1 - (Mathf.PerlinNoise(x * 0.2f, z * 0.2f) > 0.6f ? 1 : 0) : seg.tiers;
            }

            public void SegmentBounds(Seg seg, out int x0, out int z0, out int x1, out int z1)
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (int k = 0; k <= 40; k++)
                {
                    float u = Mathf.Lerp(seg.u0, seg.u1, k / 40f);
                    foreach (float sd in new[] { 1f, -26f })
                    {
                        var p = PointAt(u, sd);
                        minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                        minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
                    }
                }
                x0 = Mathf.FloorToInt(minX) - 2; z0 = Mathf.FloorToInt(minZ) - 2;
                x1 = Mathf.CeilToInt(maxX) + 2; z1 = Mathf.CeilToInt(maxZ) + 2;
            }

            /// Terreinhoogte met de ring erin verwerkt: in de vallei ongewijzigd, op en buiten de ring een hoogvlakte
            /// op de top van het stuk; in spleten (en waar de wand moet wijken) een helling.
            public float Apply(float x, float z, float h, float valley, float clear, float relief)
            {
                Locate(x, z, out float sd, out float u);
                if (sd < -40f) return h;
                // het stuk hier (of de stukken links en rechts van een spleet) bepaalt de hoogte van de hoogvlakte
                Seg own = SegmentAt(u);
                float top, wSeg;
                if (own != null)
                {
                    top = own.Top;
                    // vlak bij het eind van een stuk loopt de hoogvlakte al af naar de spleet
                    wSeg = Smooth(0f, 14f, Mathf.Min(u - own.u0, own.u1 - u) * perimeter);
                }
                else
                {
                    if (segments.Count == 0) return h;
                    Seg before = segments[segments.Count - 1], after = segments[0];
                    for (int i = 0; i < segments.Count; i++) if (segments[i].u1 < u) before = segments[i];
                    for (int i = segments.Count - 1; i >= 0; i--) if (segments[i].u0 > u) after = segments[i];
                    float a = ((u - before.u1 + 1f) % 1f) * perimeter, b = ((after.u0 - u + 1f) % 1f) * perimeter;
                    top = Mathf.Lerp(before.Top, after.Top, a / Mathf.Max(0.01f, a + b));
                    wSeg = 0f;
                }
                // hoogvlakte buiten de ring, met zachte heuvels die verder naar buiten oplopen
                float plateau = top - 0.12f + Mathf.Max(0f, sd - 4f) * 0.12f + Mathf.Max(0f, relief) * Smooth(6f, 40f, sd);
                // waar een wand staat begint de hoogvlakte precies aan zijn buitenkant (onder de wand blijft het dal);
                // in een spleet een helling van het dal naar de hoogvlakte
                float ramp = Mathf.Lerp(Smooth(-30f, 16f, sd), sd >= -0.5f ? 1f : 0f, wSeg);
                return Mathf.Lerp(h, Mathf.Max(h, plateau), ramp * clear);
            }
        }

        // De kloof: middenlijn (punten 1 m uit elkaar) met per punt de halve breedte tot de rand en het wandprofiel, plus
        // afstandsvelden (1 m raster) naar de middenlijn en naar de rivierlijn. Level() geeft per cel de wandhoogte in
        // treden; dezelfde Dist/Half bepalen het terrein, zodat wand en grond precies op elkaar aansluiten.
        sealed class Canyon
        {
            public readonly CanyonSpec spec;
            public readonly Vector3[] line;
            public readonly float[] arc, half, nearTrack, rimBand, w1, w2, w3;
            public readonly int[] l1, l2, talus;
            public readonly List<Vector3> river = new List<Vector3>();
            public readonly List<Fall> falls = new List<Fall>();
            public readonly List<(string[] tags, int side)> rimSides;
            public readonly int tiers;
            public readonly float riverHalf, rimY, originY, floorY, waterY;
            public CliffMass walls;
            public float Length => arc[arc.Length - 1];
            public float RimTop => originY + tiers * StepHeight;
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

            public Canyon(CanyonSpec spec, Vector3[] line, Func<float, float, float> trackDistance, float seed, float rimY,
                          List<(string[] tags, int side)> rimSides)
            {
                this.spec = spec;
                this.line = line;
                this.seed = seed;
                this.rimSides = rimSides;
                tiers = spec.tiers;
                riverHalf = spec.riverHalf;
                this.rimY = rimY;                                         // land langs de rand, net onder het spoorbed
                originY = rimY + 0.12f - tiers * StepHeight;              // wandtop steekt 12 cm boven het land
                floorY = originY + 0.6f;                                  // wandvoeten zakken in de bodem
                waterY = floorY + 0.05f;
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
                    float h = Mathf.Min(spec.maxHalf, 19.5f + wild * (4f + 6f * (Mathf.PerlinNoise(arc[i] / 23f + seed, 3.7f) - 0.5f)));
                    float end = Mathf.Min(arc[i], total - arc[i]);
                    h *= Mathf.Lerp(0.6f, 1f, Smooth(0f, 34f, end));
                    raw[i] = Mathf.Min(h, nearTrack[i] - RimClear);
                }
                for (int i = 0; i < n; i++)
                {
                    float sum = 0f; int count = 0;
                    for (int k = Mathf.Max(0, i - 6); k <= Mathf.Min(n - 1, i + 6); k++) { sum += raw[k]; count++; }
                    // de rand nooit dichter dan RimClear bij het spoor, ook niet als dat smaller is dan minHalf
                    half[i] = Mathf.Min(nearTrack[i] - RimClear, Mathf.Max(spec.minHalf, Mathf.Min(sum / count, spec.maxHalf)));
                    half[i] = Mathf.Max(2.5f, half[i]);
                }
                for (int i = 0; i < n; i++)
                {
                    float N(int k) => Mathf.PerlinNoise(arc[i] / 11f + k * 17.3f + seed, k * 5.1f + 0.5f);
                    rimBand[i] = 1.9f + 0.7f * N(1);
                    bool deep = tiers >= 7;
                    w1[i] = deep ? Mathf.Round(Mathf.Clamp(N(2) * 4.2f - 1f, 0f, 3f)) : 0f;
                    l1[i] = Mathf.Max(2, tiers - 3 - Mathf.FloorToInt(N(3) * 2.99f));
                    w2[i] = deep ? Mathf.Round(Mathf.Clamp(N(4) * 3.2f - 0.8f, 0f, 2f)) : 0f;
                    l2[i] = Mathf.Max(2, Mathf.Min(l1[i] - 2, l1[i] - 3 - Mathf.FloorToInt(N(5) * 2.99f)));
                    w3[i] = 1f + Mathf.Round(N(6) * 1.5f);
                    talus[i] = N(7) > 0.5f && tiers > 3 ? 2 : 1;
                    // de bodem houdt altijd ruimte voor de rivier met oevers
                    float budget = half[i] - (riverHalf + 2.5f) - (rimBand[i] + 0.6f);
                    w1[i] = Mathf.Min(w1[i], Mathf.Max(0f, budget)); budget -= w1[i];
                    w2[i] = Mathf.Min(w2[i], Mathf.Max(0f, budget)); budget -= w2[i];
                    w3[i] = Mathf.Min(w3[i], Mathf.Max(0f, budget));
                }
                // rivier: de middenlijn, aan het begin doorgetrokken tot vlak onder de kopwaterval
                Vector3 back = (line[0] - line[1]).normalized;
                // (de bedding met oever stopt een meter voor de kopwand, zodat hij de rand niet insnijdt)
                int extra = spec.headFall ? Mathf.Max(0, Mathf.FloorToInt(half[0] - rimBand[0] - riverHalf - RiverMargin - 1f)) : 0;
                for (int k = extra; k >= 1; k--) river.Add(line[0] + back * k);
                river.AddRange(line);
            }

            public bool NearLine(Vector3 p, float distance)
            {
                float d2 = distance * distance;
                foreach (var q in line)
                    if ((q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) < d2) return true;
                return false;
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
                int i = 0;
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
                var riverPts = river.ToArray();
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
                        for (int i = 0; i < riverPts.Length; i++)
                        {
                            float dx = riverPts[i].x - wx, dz = riverPts[i].z - wz, d = dx * dx + dz * dz;
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

            int[] levels;

            public void CacheLevels(Func<int, int, int> level)
            {
                levels = new int[nx * nz];
                for (int z = 0; z < nz; z++)
                    for (int x = 0; x < nx; x++)
                        levels[z * nx + x] = level(gx0 + x, gz0 + z);
            }

            /// Wandhoogte van de cel (x, z) zoals hij gebouwd wordt (0 = geen wand).
            public int WallLevel(int x, int z)
            {
                int ix = x - gx0, iz = z - gz0;
                if (levels == null || ix < 0 || iz < 0 || ix >= nx || iz >= nz) return 0;
                return levels[iz * nx + ix];
            }

            public int WallLevel(float x, float z) => WallLevel(Mathf.RoundToInt(x), Mathf.RoundToInt(z));

            /// Wandhoogte (in treden) van de cel op (x, z); 0 = geen wand (land buiten de kloof of de bodem).
            public int Level(float x, float z)
            {
                float D = Dist(x, z);
                if (D > 1e3f) return 0;
                int i = Nearest(x, z);
                float t0 = half[i] - D;                       // hoe ver binnen de rand
                if (t0 < 0f) return 0;
                if (RiverDist(x, z) <= riverHalf + RiverMargin) return 0;              // de rivier en zijn oevers blijven vrij
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
