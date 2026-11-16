// Conductor Chaos — speelbaar maken van de emplacementmodellen (issues #232, #233, #237), aangeroepen door
// StationKitPrefabs bij het bouwen van de prefabs:
//   Yard_WaterTower  handwiel (Move_Valve) en hendel (Move_Lever_Spout) met de hand te draaien; de vulpijp zakt mee
//                    (PoseFollower) en er komt water uit zodra de pijp omlaag is en het wiel open (LiquidSource).
//   Yard_HoseTap     kraantje met de hand open (kwartslag); een losse spuitmond (Loose_HoseNozzle) hangt aan een
//   Yard_OilPump     slang van 12 m (HoseLine) en spuit water, of olie bij de oliepomp.
// De kraan (Yard_Crane) krijgt nog geen bediening.
using ConductorChaos.Yard;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class YardPrefabs
    {
        const string NozzleModel = StationKitPrefabs.ModelDir + "/Props/Prop_HoseNozzle.fbx";
        const string MaterialDir = "Assets/Art/Materials/Environment/Station";

        static bool IsTap(string name) => name == "Yard_HoseTap" || name == "Yard_OilPump";

        /// <summary>Vóór de botsvormen: extra modellen in de prefab zetten (de spuitmond aan de slang).</summary>
        public static void BeforeColliders(GameObject go, string name)
        {
            if (!IsTap(name) || go.transform.Find("Loose_HoseNozzle")) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(NozzleModel);
            if (!model) { Debug.LogWarning("[Yard] Prop_HoseNozzle ontbreekt; tappunt zonder spuitmond."); return; }
            var nozzle = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            PrefabUtility.UnpackPrefabInstance(nozzle, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            nozzle.name = "Loose_HoseNozzle";                    // StationKitPrefabs: losse prop Prop_HoseNozzle
            nozzle.transform.localPosition = new Vector3(0.55f, 0.0f, 0.62f);
            nozzle.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
        }

        /// <summary>Na de botsvormen en de losse props: hendels, vloeistof en slang.</summary>
        public static void AfterColliders(GameObject go, string name)
        {
            if (name == "Yard_WaterTower")
            {
                var valve = MakeLever(go, "Valve");
                var spoutLever = MakeLever(go, "Lever_Spout");
                var spout = Find(go.transform, "Move_Spout");
                var up = Find(go.transform, "Pose_Spout_Up");
                var down = Find(go.transform, "Pose_Spout_Down");
                if (spout && up && down && spoutLever)
                    spout.gameObject.AddComponent<PoseFollower>().Setup(spoutLever, up.localRotation, down.localRotation);
                var outlet = Find(go.transform, "Point_WaterOut");
                if (outlet)
                    go.AddComponent<LiquidSource>().Setup(LiquidKind.Water, outlet, Vector3.down, valve, spoutLever, 140f, 1.4f, 0.10f);
            }
            else if (IsTap(name))
            {
                bool oil = name == "Yard_OilPump";
                var valve = MakeLever(go, "Valve");
                var nozzle = go.transform.Find("Loose_HoseNozzle");
                if (!nozzle) return;
                var outlet = Find(nozzle, "Point_WaterOut");
                if (outlet)
                    nozzle.gameObject.AddComponent<LiquidSource>().Setup(oil ? LiquidKind.Oil : LiquidKind.Water, outlet,
                        Vector3.right, valve, null, 45f, 3.2f, 0.035f);
                var start = Find(go.transform, "Point_ReelHose") ?? Find(go.transform, "Point_HoseOut");
                var end = Find(nozzle, "Point_HoseIn");
                var look = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{(oil ? "M_Stn_Dark" : "M_Stn_Red")}.mat");
                go.AddComponent<HoseLine>().Setup(start, end, nozzle.GetComponent<Rigidbody>(), 12f, look);
            }
        }

        /// <summary>Move_&lt;naam&gt; wordt een hendel met de hand, bereik uit Pose_&lt;naam&gt;_* (eerste = 0, tweede = 1).</summary>
        static HandLever MakeLever(GameObject go, string part)
        {
            var node = Find(go.transform, "Move_" + part);
            if (!node) return null;
            Transform a = null, b = null, c = null;
            foreach (Transform t in node.parent)
            {
                if (!t.name.StartsWith($"Pose_{part}_")) continue;
                if (!a) a = t; else if (!b) b = t; else if (!c) c = t;
            }
            if (!a || !b) return null;
            foreach (var col in node.GetComponentsInChildren<MeshCollider>(true)) col.convex = true;
            var body = node.gameObject.AddComponent<Rigidbody>();
            body.mass = part == "Valve" ? 6f : 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            node.gameObject.AddComponent<NetworkObject>();
            node.gameObject.AddComponent<NetworkTransform>().InLocalSpace = true;
            node.gameObject.AddComponent<NetworkRigidbody>();
            var lever = node.gameObject.AddComponent<HandLever>();
            lever.Configure(a.localRotation, b.localRotation, a.name.EndsWith("_Neutral"), c ? c.localRotation : (Quaternion?)null);
            return lever;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }
}
