// Conductor Chaos — crew-garderobe en briefing (Grand Whistleton).
// * Setup Crew Wardrobe: vult Resources/CrewWardrobe.asset (skins conducteur, monteur, stoker + vijf extraatjes uit
//   CrewAccessories.fbx) en zet CrewOutfit op de spelersprefab (Assets/Playground/RemotePlayer.prefab).
// * DressBriefing (vanuit GrandStationBuilder.BuildStation): maakt de tenues aan het kledingrek grijpbaar
//   (CrewWardrobeRack), legt extraatjes op de tafel en in twee kluisjes (CrewAccessoryPickup) en zet de stationschef
//   met tekstballonnen (BriefingSpeaker) op het podium.
using System.Linq;
using ConductorChaos.Characters;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class CrewWardrobeSetup
    {
        const string CatalogPath = "Assets/Resources/CrewWardrobe.asset";
        const string PlayerPrefab = "Assets/Playground/RemotePlayer.prefab";
        const string Characters = "Assets/Art/Models/Characters";

        [MenuItem("Conductor Chaos/Briefing/Setup Crew Wardrobe")]
        public static void Setup()
        {
            EnsureCatalog();
            EnsurePlayerPrefab();
        }

        public static CrewWardrobeCatalog EnsureCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CrewWardrobeCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<CrewWardrobeCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            CrewWardrobeCatalog.Skin Skin(string label, string model) => new()
                { label = label, model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/{model}/{model}.fbx") };
            CrewWardrobeCatalog.Accessory Acc(string label, string node, CrewWardrobeCatalog.Slot slot) => new()
                { label = label, node = node, slot = slot };
            catalog.Configure(
                new[] { Skin("Conducteur", "Conductor"), Skin("Monteur", "RailMechanic"), Skin("Stoker", "Stoker") },
                AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/CrewAccessories/CrewAccessories.fbx"),
                new[]
                {
                    Acc("Hoge hoed", "Acc_TopHat", CrewWardrobeCatalog.Slot.Head),
                    Acc("Strohoed", "Acc_StrawHat", CrewWardrobeCatalog.Slot.Head),
                    Acc("Zonnebril", "Acc_Sunglasses", CrewWardrobeCatalog.Slot.Face),
                    Acc("Snor", "Acc_Mustache", CrewWardrobeCatalog.Slot.Face),
                    Acc("Vlinderdas", "Acc_Bowtie", CrewWardrobeCatalog.Slot.Neck),
                });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static void EnsurePlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                if (!root.GetComponent<CrewOutfit>()) root.AddComponent<CrewOutfit>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>Kleedt de briefingruimte aan in een (nog niet opgeslagen) stationsopbouw.</summary>
        public static void DressBriefing(Transform station)
        {
            var catalog = EnsureCatalog();
            var props = station.Find("Props");
            var arch = station.Find("Architecture");
            if (!props || !arch) return;

            // kledingrek: tenues worden vaste lijven die je kunt vastpakken; volgorde = skins in de catalogus
            var rack = props.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Prop_CrewWardrobe"));
            if (rack)
            {
                var hangers = new[] { "Outfit_Conductor", "Outfit_Engineer", "Outfit_Stoker" }
                    .Select(n => rack.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n))
                    .Select(t => t ? Kinematic(t.gameObject) : null).ToArray();
                var component = rack.GetComponent<CrewWardrobeRack>();
                if (!component) component = rack.gameObject.AddComponent<CrewWardrobeRack>();
                component.Configure(hangers);
            }

            // tafel met extraatjes: hoge hoed en zonnebril (de hoeden zijn te breed voor meer op één tafel)
            var table = props.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Prop_AccessoryTable"));
            if (table)
            {
                string[] onTable = { "Acc_TopHat", "Acc_Sunglasses" };
                for (int k = 0; k < onTable.Length; k++)
                {
                    var spot = table.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"Spot_Item_{k + 1}");
                    if (spot) Pickup(catalog, catalog.IndexOf(onTable[k]), props, spot.position, table.rotation);
                }
            }

            // twee kluisjes in de briefing: vlinderdas en snor op de bodem achter deurtje 2
            // briefing ligt in de linkervleugel van de bron = Unity x > 12 (de import spiegelt x)
            var briefingLockers = props.Cast<Transform>()
                .Where(t => t.name.StartsWith("Prop_Lockers") && station.InverseTransformPoint(t.position).x > 12f)
                .OrderBy(t => t.position.x).ToArray();
            if (briefingLockers.Length >= 2)
            {
                // kast: 4 vakken van 0,46 m (bron-x -0,69, -0,23, ...; Unity spiegelt x), bodem 0,13, plank 1,18
                Pickup(catalog, catalog.IndexOf("Acc_Bowtie"), props, briefingLockers[0].TransformPoint(new Vector3(0.23f, 0.15f, -0.02f)),
                    briefingLockers[0].rotation);
                Pickup(catalog, catalog.IndexOf("Acc_Mustache"), props, briefingLockers[1].TransformPoint(new Vector3(0.23f, 0.15f, -0.02f)),
                    briefingLockers[1].rotation);
            }
            // strohoed op een hoek van de kaarttafel (missiekeuze)
            var map = arch.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("GS_MissionTable"));
            if (map)
            {
                Pickup(catalog, catalog.IndexOf("Acc_StrawHat"), props, map.TransformPoint(new Vector3(-0.70f, 1.97f, 0.30f)), map.rotation);
            }

            // stationschef op het podium, kijkt naar de banken
            var podium = arch.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("GS_Podium"));
            var speakerSpot = podium ? podium.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Spot_Speaker") : null;
            var master = AssetDatabase.LoadAssetAtPath<GameObject>($"{Characters}/StationMaster/StationMaster.fbx");
            if (speakerSpot && master)
            {
                var boss = (GameObject)PrefabUtility.InstantiatePrefab(master, props);
                boss.name = "Stationschef (briefing)";
                boss.transform.SetPositionAndRotation(speakerSpot.position, podium.rotation);
                var face = boss.GetComponent<CharacterFace>();
                if (!face) face = boss.AddComponent<CharacterFace>();
                face.CaptureFromModel();
                boss.AddComponent<BriefingSpeaker>();
                var capsule = boss.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.62f, 0f);
                capsule.height = 1.24f;
                capsule.radius = 0.3f;
            }
        }

        static Rigidbody Kinematic(GameObject go)
        {
            var body = go.GetComponent<Rigidbody>();
            if (!body) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return body;
        }

        static void Pickup(CrewWardrobeCatalog catalog, int index, Transform parent, Vector3 floor, Quaternion rotation)
        {
            var node = index >= 0 ? catalog.AccessoryNode(index) : null;
            if (!node) return;
            var root = new GameObject("Extra_" + catalog.AccessoryAt(index).label);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(floor, rotation);
            var visual = Object.Instantiate(node.gameObject, root.transform, false);
            visual.name = node.name;
            visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            // zet het ding met zijn onderkant op de plek (de knoop staat in lijfcoördinaten op hoofdhoogte)
            var rs = visual.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            visual.transform.position += floor - new Vector3(b.center.x, b.min.y, b.center.z) + Vector3.up * 0.005f;
            b.center += floor - new Vector3(b.center.x, b.min.y, b.center.z);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(b.center) + Vector3.up * 0.005f;
            box.size = Quaternion.Inverse(rotation) * b.size;
            box.size = new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z)) + Vector3.one * 0.04f;
            Kinematic(root);
            root.AddComponent<CrewAccessoryPickup>().Configure(index);
        }
    }
}
