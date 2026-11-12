// Conductor Chaos — de treinsets uit ArtSource/Train/train_kit.py (Diesel jaren '60, Modern Express, Futuristic Express).
// Menu: Tools > Conductor Chaos > Train Sets
//   - Place ... Set: zet de voertuigen van een set achter elkaar in de open scène (koppelingen tegen elkaar) en
//     maakt ze speelklaar (zie Prepare).
//   - Prepare Selected Vehicles: botsvorm (MeshCollider) op elk zichtbaar onderdeel, zodat je erin kunt lopen, de
//     ladders naar het dakluik en het dak kunt beklimmen en over het dak kunt lopen; een RepairPoint (heel bij de start) op elke
//     knoop RepairPoint_<Naam>; en TrainBreakdowns, zodat er tijdens het spelen af en toe iets kapot gaat.
// Daarna: Tools > Conductor Chaos > Configure Train On Selected Track koppelt ze als wagons achter de locomotief.
// De locs/stuurstandrijtuigen van de nieuwe sets rijden nog niet zelf (TrainDrive is gebouwd rond de stoomloc).
using System.Collections.Generic;
using System.Linq;
using ConductorChaos.Playground;
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class TrainSetTools
    {
        const string ModelDir = "Assets/Art/Models/Train";
        const float CouplerReach = 0.48f;      // koppeling steekt zoveel buiten de kopwand (0,12 + 0,36)

        // voertuig -> (map, halve baklengte voor, halve baklengte achter) in modelruimte (+X = voorkant)
        static readonly (string name, string folder, float front, float rear)[] Diesel =
        {
            ("DieselLocomotive_Blockout", "DieselLocomotive", 3.88f, 3.2f), ("DieselCoach_Blockout", "DieselCoach", 3.6f, 3.6f),
        };
        static readonly (string name, string folder, float front, float rear)[] Modern =
        {
            ("ModernCabCar_Blockout", "ModernCabCar", 5.10f, 3.2f), ("ModernCoach_Blockout", "ModernCoach", 3.6f, 3.6f),
        };
        static readonly (string name, string folder, float front, float rear)[] Futuristic =
        {
            ("FuturisticCabCar_Blockout", "FuturisticCabCar", 5.62f, 3.2f), ("FuturisticCoach_Blockout", "FuturisticCoach", 3.6f, 3.6f),
        };

        static readonly (string name, string folder, float front, float rear)[] Freight =
        {
            ("TankWagon_Blockout", "TankWagon", 3.6f, 3.6f), ("CargoWagon_Blockout", "CargoWagon", 3.6f, 3.6f),
        };

        /// <summary>Alle voertuigen van de nieuwe sets (voor Configure Train: koppelbaar als wagon).</summary>
        public static IEnumerable<string> Vehicles => Diesel.Concat(Modern).Concat(Futuristic).Concat(Freight).Select(v => v.name);

        [MenuItem("Tools/Conductor Chaos/Train Sets/Place Diesel '60 Set")]
        static void PlaceDiesel() => Place("Diesel '60", Diesel);

        [MenuItem("Tools/Conductor Chaos/Train Sets/Place Modern Express Set")]
        static void PlaceModern() => Place("Modern Express", Modern);

        [MenuItem("Tools/Conductor Chaos/Train Sets/Place Futuristic Express Set")]
        static void PlaceFuturistic() => Place("Futuristic Express", Futuristic);

        [MenuItem("Tools/Conductor Chaos/Train Sets/Place Freight Wagons (tank + cargo)")]
        static void PlaceFreight() => Place("Freight wagons", Freight);

        [MenuItem("Tools/Conductor Chaos/Train Sets/Prepare Selected Vehicles")]
        static void PrepareSelected()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
                if (!root) root = go;
                Prepare(root);
                n++;
            }
            Debug.Log($"[Train Sets] {n} voertuig(en) speelklaar gemaakt.");
        }

        [MenuItem("Tools/Conductor Chaos/Train Sets/Prepare Selected Vehicles", true)]
        static bool PrepareSelectedValidate() => Selection.gameObjects.Length > 0;

        static void Place(string label, (string name, string folder, float front, float rear)[] set)
        {
            Undo.SetCurrentGroupName("Place " + label);
            var view = SceneView.lastActiveSceneView;
            Vector3 origin = view ? view.pivot : Vector3.zero;
            origin.y = Mathf.Max(origin.y, 0f) + 0.62f;        // spoorstaaf ligt 0,62 m onder de pivot van het model
            float x = 0f;
            var made = new List<GameObject>();
            for (int i = 0; i < set.Length; i++)
            {
                var (name, folder, front, rear) = set[i];
                string path = $"{ModelDir}/{folder}/{name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model)
                {
                    EditorUtility.DisplayDialog("Train Sets", $"Model niet gevonden: {path}\nLaat Unity eerst de nieuwe modellen importeren.", "OK");
                    return;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                Undo.RegisterCreatedObjectUndo(go, "Place " + name);
                go.name = name;
                if (i > 0) x -= set[i - 1].rear + CouplerReach + CouplerReach + front;     // koppeling tegen koppeling
                go.transform.position = origin + new Vector3(x, 0f, 0f);
                Prepare(go);
                made.Add(go);
            }
            Selection.objects = made.ToArray();
            EditorSceneManager.MarkSceneDirty(made[0].scene);
            Debug.Log($"[Train Sets] {label}: {made.Count} voertuigen geplaatst. Zet ze op het spoor en kies " +
                      "Tools > Conductor Chaos > Configure Train On Selected Track om ze te koppelen.");
        }

        /// <summary>Botsvormen, onderhoudspunten en slijtage op één voertuig. Mag vaker: slaat over wat er al zit.</summary>
        public static void Prepare(GameObject vehicle)
        {
            int colliders = 0, repairs = 0;
            foreach (var filter in vehicle.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = filter.name;
                // de bout krijgt van RepairPoint zijn eigen bolvormige botsvorm; rook en losse brokstukken botsen niet
                if (!filter.sharedMesh || filter.GetComponent<Collider>() || n.StartsWith("Repair_Bolt") || n.StartsWith("Broken_Bits")) continue;
                var collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                collider.sharedMesh = filter.sharedMesh;
                collider.convex = false;
                colliders++;
            }
            foreach (var node in vehicle.GetComponentsInChildren<Transform>(true))
            {
                if (!node.name.StartsWith("RepairPoint_") || node.GetComponent<RepairPoint>()) continue;
                Undo.AddComponent<RepairPoint>(node.gameObject).Configure(false);     // heel bij de start
                repairs++;
            }
            if (vehicle.GetComponentsInChildren<RepairPoint>(true).Length > 0)
            {
                // RepairPoint is netwerkstate: het voertuig heeft een NetworkObject nodig (Configure Train voegt de rest toe)
                if (!vehicle.GetComponent<NetworkObject>()) Undo.AddComponent<NetworkObject>(vehicle);
                if (!vehicle.GetComponent<TrainBreakdowns>()) Undo.AddComponent<TrainBreakdowns>(vehicle);
            }
            EditorUtility.SetDirty(vehicle);
            Debug.Log($"[Train Sets] {vehicle.name}: {colliders} botsvormen, {repairs} onderhoudspunten toegevoegd.", vehicle);
        }
    }
}
