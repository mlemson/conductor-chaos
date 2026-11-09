using System.Linq;
using ConductorChaos.Playground;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Editor
{
    public static class PlaygroundSceneSetup
    {
        [MenuItem("Tools/Conductor Chaos/Configure Train On Selected Track")]
        public static void ConfigureTrain()
        {
            var locomotive = GameObject.Find("Locomotive_Blockout");
            if (!locomotive)
            {
                const string model = "Assets/Art/Models/Train/Locomotive/Locomotive_Blockout.fbx";
                var matches = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)) == model)
                    .Select(t => PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject))
                    .Where(root => root && root.scene.IsValid()).Distinct().ToArray();
                if (matches.Length == 1) locomotive = matches[0];
                else
                {
                    EditorUtility.DisplayDialog("Trein configureren", matches.Length == 0
                        ? "Geen actieve locomotief gevonden in de open scène. Plaats eerst het locomotiefmodel."
                        : "Er zijn meerdere locomotieven gevonden. Geef de gewenste locomotief de naam Locomotive_Blockout.", "OK");
                    return;
                }
            }
            var track = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<TrackPiece>() : null;
            if (track && (track.gameObject.scene != locomotive.scene || !track.HasSockets)) track = null;
            var selectedTrack = track;
            float closest = float.PositiveInfinity;
            float fraction = 0f;
            float selectedDistance = float.PositiveInfinity;
            float selectedFraction = 0f;
            track = null;
            foreach (var candidate in Object.FindObjectsByType<TrackPiece>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != locomotive.scene || !candidate.HasSockets || candidate.Length < 0.01f || candidate.name.Contains("Broken")) continue;
                for (int i = 0; i <= 32; i++)
                {
                    float t = i / 32f;
                    candidate.Evaluate(t, out var p, out _);
                    float d = (p - locomotive.transform.position).sqrMagnitude;
                    if (candidate == selectedTrack && d < selectedDistance) { selectedDistance = d; selectedFraction = t; }
                    if (d >= closest) continue;
                    closest = d;
                    fraction = t;
                    track = candidate;
                }
            }
            if (selectedTrack && selectedDistance <= 100f)
            {
                track = selectedTrack;
                fraction = selectedFraction;
                closest = selectedDistance;
            }
            if (!track || !float.IsFinite(closest))
            {
                EditorUtility.DisplayDialog("Trein configureren", "Geen bruikbaar spoorstuk gevonden. Plaats eerst een spoorstuk met aansluitpunten in de scène.", "OK");
                return;
            }
            if (closest > 100f)
            {
                EditorUtility.DisplayDialog("Trein configureren", "Er ligt geen spoor binnen 10 meter van de locomotief. Zet de locomotief dichter bij het spoor en probeer opnieuw.", "OK");
                return;
            }
            Undo.SetCurrentGroupName("Configure train");
            var rb = locomotive.GetComponent<Rigidbody>() ? locomotive.GetComponent<Rigidbody>() : Undo.AddComponent<Rigidbody>(locomotive);
            Undo.RecordObject(rb, "Configure train physics");
            rb.isKinematic = true;
            rb.useGravity = false;
            if (!locomotive.GetComponent<NetworkObject>()) Undo.AddComponent<NetworkObject>(locomotive);
            if (!locomotive.GetComponent<NetworkTransform>()) Undo.AddComponent<NetworkTransform>(locomotive);
            var drive = locomotive.GetComponent<TrainDrive>() ? locomotive.GetComponent<TrainDrive>() : Undo.AddComponent<TrainDrive>(locomotive);
            Undo.RecordObject(drive, "Set train track");
            drive.Configure(track, fraction * track.Length);
            var wagons = new System.Collections.Generic.List<Rigidbody>();
            foreach (var name in new[] { "Tender_Blockout", "RegionalWagon_Blockout", "CattleWagon_Blockout" })
            {
                foreach (var wagon in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => (t.name == name || t.name.StartsWith(name + " (")) && t.gameObject.scene == locomotive.scene))
                {
                    var wagonBody = wagon.GetComponent<Rigidbody>() ? wagon.GetComponent<Rigidbody>() : Undo.AddComponent<Rigidbody>(wagon.gameObject);
                    Undo.RecordObject(wagonBody, "Configure wagon physics");
                    wagonBody.isKinematic = true;
                    wagonBody.useGravity = false;
                    wagonBody.interpolation = RigidbodyInterpolation.Interpolate;
                    if (!wagon.GetComponent<NetworkObject>()) Undo.AddComponent<NetworkObject>(wagon.gameObject);
                    if (!wagon.GetComponent<NetworkTransform>()) Undo.AddComponent<NetworkTransform>(wagon.gameObject);
                    wagons.Add(wagonBody);
                }
            }
            drive.ConfigureWagons(wagons.ToArray());
            float railCenter = 0f;
            var wheels = locomotive.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Wheels");
            if (wheels && wheels.TryGetComponent<Renderer>(out var wheelRenderer))
                railCenter = locomotive.transform.InverseTransformPoint(wheelRenderer.bounds.center).z;
            track.Evaluate(fraction, out var position, out var direction);
            var rotation = Quaternion.LookRotation(direction, track.transform.up) * Quaternion.Euler(0, 90, 0);
            Undo.RecordObject(locomotive.transform, "Place locomotive on track");
            locomotive.transform.SetPositionAndRotation(position + track.transform.up * TrackPiece.RailToTrainPivot -
                                                        rotation * new Vector3(0, 0, railCenter), rotation);
            EditorUtility.SetDirty(drive);
            EditorSceneManager.MarkSceneDirty(locomotive.scene);
            Debug.Log("Train configured. Save the scene before playing.");
        }

        [MenuItem("Tools/Conductor Chaos/Enable Conductor Leg Rig")]
        public static void EnableConductorLegRig()
        {
            const string path = "Assets/Art/Models/Characters/Conductor/Conductor.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) { Debug.LogError("Conductor model importer was not found."); return; }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            Debug.Log("Conductor body mesh is readable for the continuous leg rig.");
        }

        [MenuItem("Tools/Conductor Chaos/Build Windows Client")]
        public static void BuildLocalTrialClient()
        {
            const string directory = "work/PlaygroundClient";
            System.IO.Directory.CreateDirectory(directory);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/TrainPlayground.unity" },
                locationPathName = directory + "/ConductorChaos.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Debug.Log("Playground client build: " + result.summary.result);
        }

        [MenuItem("Tools/Conductor Chaos/Configure Multiplayer Physics Trial")]
        public static void ConfigureMultiplayerTrial()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TrainPlayground.unity")
            {
                Debug.LogError("Open TrainPlayground before configuring its multiplayer trial.");
                return;
            }
            GameObject player = GameObject.Find("Playground Player");
            var host = Object.FindAnyObjectByType<PlaygroundHost>();
            if (!player || !host) { Debug.LogError("Playground player or host is missing."); return; }
            const string folder = "Assets/Playground";
            const string path = folder + "/RemotePlayer.prefab";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "Playground");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                prefab = PrefabUtility.SaveAsPrefabAsset(player, path);
                if (!prefab) { Debug.LogError("Could not create playground player prefab."); return; }
            }
            host.Configure(prefab);
            EditorUtility.SetDirty(host);

            if (!GameObject.Find("Playground Physics Crate"))
            {
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crate.name = "Playground Physics Crate";
                Undo.RegisterCreatedObjectUndo(crate, "Create physics test crate");
                crate.transform.position = player.transform.position + new Vector3(1.5f, 0.35f, 1.5f);
                crate.transform.localScale = Vector3.one * 0.45f;
                Rigidbody rigidbody = Undo.AddComponent<Rigidbody>(crate);
                rigidbody.mass = 1f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                Undo.AddComponent<NetworkObject>(crate);
                Undo.AddComponent<NetworkTransform>(crate);
                Undo.AddComponent<NetworkRigidbody>(crate);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Playground multiplayer trial configured.");
        }

        [MenuItem("Tools/Conductor Chaos/Prepare Physics Playground")]
        public static void Prepare()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TrainPlayground.unity")
            {
                Debug.LogError("Open Assets/Scenes/TrainPlayground.unity before preparing the playground.");
                return;
            }
            GameObject model = GameObject.Find("Conductor");
            if (!model || GameObject.Find("Playground Player"))
            {
                Debug.LogError("Expected an unconfigured Conductor and no existing Playground Player; nothing was changed.");
                return;
            }
            Transform left = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Arm_L");
            Transform right = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Arm_R");
            if (!left || !right) { Debug.LogError("Conductor arm anchors were not found; nothing was changed."); return; }

            Undo.SetCurrentGroupName("Prepare Physics Playground");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Playground Ground";
            Undo.RegisterCreatedObjectUndo(floor, "Create ground");
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(80f, 0.3f, 45f);

            int colliderCount = 0;
            foreach (string name in new[] { "Locomotive_Blockout", "RegionalWagon_Blockout", "Tender_Blockout", "CattleWagon_Blockout" })
            {
                GameObject trainPart = GameObject.Find(name);
                if (!trainPart) { Debug.LogWarning("Train model not found: " + name); continue; }
                foreach (MeshFilter filter in trainPart.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.sharedMesh || filter.GetComponent<Collider>()) continue;
                    MeshCollider collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false; // static preview; dynamic train physics needs separate collision shapes.
                    colliderCount++;
                }
            }

            var player = new GameObject("Playground Player");
            Undo.RegisterCreatedObjectUndo(player, "Create network player root");
            player.transform.SetPositionAndRotation(model.transform.position, model.transform.rotation);
            Undo.SetTransformParent(model.transform, player.transform, "Attach conductor model");
            var capsule = Undo.AddComponent<CapsuleCollider>(player);
            capsule.center = new Vector3(0f, 0.62f, 0f);
            capsule.height = 1.24f;
            capsule.radius = 0.27f;
            var body = Undo.AddComponent<Rigidbody>(player);
            body.mass = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            Undo.AddComponent<NetworkObject>(player);
            Undo.AddComponent<NetworkTransform>(player);
            var controller = Undo.AddComponent<PlaygroundPlayer>(player);
            controller.Configure(model.transform, left, right);
            EditorUtility.SetDirty(controller);

            if (!Object.FindAnyObjectByType<NetworkManager>())
            {
                var network = new GameObject("Playground Network Host");
                Undo.RegisterCreatedObjectUndo(network, "Create network host");
                var transport = Undo.AddComponent<UnityTransport>(network);
                var manager = Undo.AddComponent<NetworkManager>(network);
                manager.NetworkConfig.NetworkTransport = transport;
                manager.NetworkConfig.EnableSceneManagement = true;
                Undo.AddComponent<PlaygroundHost>(network);
                EditorUtility.SetDirty(manager);
            }
            if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scene.path, true) }).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Physics playground ready: {colliderCount} static train mesh colliders; scene saved.");
        }
    }
}

