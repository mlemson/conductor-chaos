// Conductor Chaos — bouwt de netwerkprefabs voor de spullen op het vertrekperron (ServiceGear): jerrycan, pompslang met
// spuitmond en sneeuwschuiver. Ze komen in Resources/, zodat de server ze bij elke dienst kan neerzetten en PlaygroundHost
// ze registreert. Ontbreekt er een, dan bouwt de editor ze vanzelf na het compileren; opnieuw bouwen kan via het menu.
using ConductorChaos.Stations;
using ConductorChaos.Yard;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    internal static class ServiceGearPrefabs
    {
        const string Folder = "Assets/Resources";
        const string MaterialDir = "Assets/Art/Materials/Environment/Station";
        const string NozzleModel = StationKitPrefabs.ModelDir + "/Props/Prop_HoseNozzle.fbx";
        const string PumpModel = StationKitPrefabs.ModelDir + "/Yard/Yard_OilPump.fbx";

        [InitializeOnLoadMethod]
        static void BuildWhenMissing() => EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string name in ServiceGear.ResourceNames)
                if (!AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{name}.prefab")) { Build(); return; }
        };

        [MenuItem("Tools/Conductor Chaos/Service Gear (jerrycan, pompslang, sneeuwschuiver) bouwen")]
        static void Build()
        {
            Save(Jerrycan(), ServiceGear.JerrycanResource);
            Save(Nozzle(), ServiceGear.NozzleResource);
            Save(Pusher(), ServiceGear.PusherResource);
            Debug.Log("[ServiceGear] Prefabs gebouwd: " + string.Join(", ", ServiceGear.ResourceNames));
        }

        static GameObject Jerrycan()
        {
            var root = Root(ServiceGear.JerrycanResource, 4f);
            Part(root, PrimitiveType.Cube, "Romp", new Vector3(0f, .21f, 0f), Quaternion.identity, new Vector3(.17f, .42f, .34f), "M_Stn_Red", true);
            Part(root, PrimitiveType.Cube, "Handvat", new Vector3(0f, .45f, -.05f), Quaternion.identity, new Vector3(.05f, .05f, .2f), "M_Stn_Dark", true);
            Part(root, PrimitiveType.Cylinder, "Tuit", new Vector3(0f, .46f, .15f), Quaternion.Euler(50f, 0f, 0f), new Vector3(.04f, .06f, .04f), "M_Stn_Dark", false);
            var outlet = Point(root, "Point_WaterOut", new Vector3(0f, .5f, .2f));
            var spout = root.AddComponent<LiquidSource>();
            spout.Setup(LiquidKind.Diesel, outlet, Vector3.forward, null, null, 5f, .9f, .025f);
            root.GetComponent<ServiceGear>().Configure(ServiceGear.GearKind.Jerrycan, spout, null, null, null);
            return root;
        }

        static GameObject Nozzle()
        {
            var root = Root(ServiceGear.NozzleResource, 1.2f);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NozzleModel);
            Transform outlet, hoseIn;
            if (model)
            {
                var nozzle = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(nozzle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                nozzle.name = "Spuitmond";
                // BoxCollider zonder maat neemt zelf de grenzen van de mesh op hetzelfde object over; anders een kleine doos.
                var mesh = nozzle.GetComponentInChildren<MeshFilter>();
                var box = (mesh ? mesh.gameObject : nozzle).AddComponent<BoxCollider>();
                if (!mesh) box.size = new Vector3(.3f, .08f, .06f);
                outlet = Find(root.transform, "Point_WaterOut");
                hoseIn = Find(root.transform, "Point_HoseIn");
            }
            else
            {
                Debug.LogWarning("[ServiceGear] Prop_HoseNozzle ontbreekt; eenvoudige spuitmond.");
                Part(root, PrimitiveType.Cube, "Spuitmond", Vector3.zero, Quaternion.identity, new Vector3(.3f, .08f, .06f), "M_Stn_Dark", true);
                outlet = hoseIn = null;
            }
            if (!outlet) outlet = Point(root, "Point_WaterOut", new Vector3(.16f, 0f, 0f));
            if (!hoseIn) hoseIn = Point(root, "Point_HoseIn", new Vector3(-.16f, 0f, 0f));
            var spout = root.AddComponent<LiquidSource>();
            spout.Setup(LiquidKind.Diesel, outlet, Vector3.right, null, null, 6f, 1.2f, .03f);
            var pump = AssetDatabase.LoadAssetAtPath<GameObject>(PumpModel);
            if (!pump) Debug.LogWarning("[ServiceGear] Yard_OilPump ontbreekt; de slang hangt dan aan de spuitmond zelf.");
            root.GetComponent<ServiceGear>().Configure(ServiceGear.GearKind.FuelNozzle, spout, hoseIn, pump, Material("M_Stn_Dark"));
            return root;
        }

        static GameObject Pusher()
        {
            var root = Root(ServiceGear.PusherResource, 3f);
            Part(root, PrimitiveType.Cube, "Blad", new Vector3(0f, .16f, .25f), Quaternion.identity, new Vector3(.8f, .32f, .03f), "M_Stn_Metal", true);
            Vector3 from = new(0f, .2f, .23f), to = new(0f, 1f, -.6f);
            Part(root, PrimitiveType.Cylinder, "Steel", (from + to) * .5f, Quaternion.FromToRotation(Vector3.up, to - from),
                new Vector3(.035f, Vector3.Distance(from, to) * .5f, .035f), "M_Stn_WoodLight", true);
            Part(root, PrimitiveType.Cube, "Greep", to, Quaternion.identity, new Vector3(.35f, .04f, .04f), "M_Stn_Dark", true);
            root.GetComponent<ServiceGear>().Configure(ServiceGear.GearKind.SnowPusher, null, null, null, null);
            return root;
        }

        static GameObject Root(string name, float mass)
        {
            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody>();
            root.AddComponent<ServiceGear>();
            return root;
        }

        static void Part(GameObject root, PrimitiveType shape, string name, Vector3 position, Quaternion rotation, Vector3 scale, string material, bool collide)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.SetLocalPositionAndRotation(position, rotation);
            part.transform.localScale = scale;
            if (!collide) Object.DestroyImmediate(part.GetComponent<Collider>());
            var look = Material(material);
            if (look) part.GetComponent<Renderer>().sharedMaterial = look;
        }

        static Transform Point(GameObject root, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(root.transform, false);
            point.localPosition = position;
            return point;
        }

        static Material Material(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{name}.mat");
            if (!material) Debug.LogWarning($"[ServiceGear] Materiaal {name} ontbreekt.");
            return material;
        }

        static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = Find(child, name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
