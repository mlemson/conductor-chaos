// Conductor Chaos — vult Resources/PassengerWardrobe.asset uit de look-specs die
// ArtSource/Characters/PassengerLooks/build_passenger_looks.py naar ArtSource/Exports/*Looks.looks.json schrijft,
// en zet de muzikant (5e reizigerstype) als stopmodel op Resources/ServiceBonusPassenger.prefab.
using System.Collections.Generic;
using System.IO;
using ConductorChaos.Characters;
using ConductorChaos.Playground;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class PassengerWardrobeSetup
    {
        const string CatalogPath = "Assets/Resources/PassengerWardrobe.asset";
        const string BonusPrefab = "Assets/Resources/ServiceBonusPassenger.prefab";
        const string Models = "Assets/Art/Models/Characters";
        const string Materials = "Assets/Art/Materials/Characters";
        // Volgorde = appearance-index - 1 in PlaygroundPassenger (0 = zakenreiziger in de prefab zelf).
        static readonly string[] StopModels = { "Tourist", "ElderlyPassenger", "ChildPassenger", "Musician" };

        [System.Serializable] class Pair { public string from, to; }
        [System.Serializable] class LookSpec { public string name; public string[] hide, add; public Pair[] materials; }
        [System.Serializable] class Spec { public string type; public LookSpec[] looks; }

        [MenuItem("Conductor Chaos/Passengers/Build Passenger Wardrobe")]
        public static void Build()
        {
            string exports = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArtSource", "Exports"));
            var kinds = new List<PassengerWardrobe.Kind>();
            foreach (string file in Directory.GetFiles(exports, "*Looks.looks.json"))
            {
                var spec = JsonUtility.FromJson<Spec>(File.ReadAllText(file));
                string asset = spec.type + "Looks";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{asset}/{asset}.fbx");
                if (!model) { Debug.LogError("Looks-model ontbreekt: " + asset); return; }
                var looks = new List<PassengerWardrobe.Look>();
                foreach (var l in spec.looks)
                {
                    var from = new Material[l.materials.Length];
                    var to = new Material[l.materials.Length];
                    for (int i = 0; i < l.materials.Length; i++)
                    {
                        from[i] = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{spec.type}/{l.materials[i].from}.mat");
                        to[i] = AssetDatabase.LoadAssetAtPath<Material>($"{Materials}/{asset}/{l.materials[i].to}.mat");
                        if (!from[i] || !to[i]) { Debug.LogError($"Materiaal ontbreekt voor {spec.type}/{l.name}: {l.materials[i].from}"); return; }
                    }
                    looks.Add(new PassengerWardrobe.Look { label = l.name, hide = l.hide, add = l.add, from = from, to = to });
                }
                kinds.Add(new PassengerWardrobe.Kind { materialPrefix = $"M_{spec.type}_", looksModel = model, looks = looks.ToArray() });
            }
            var catalog = AssetDatabase.LoadAssetAtPath<PassengerWardrobe>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<PassengerWardrobe>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(kinds.ToArray());
            EditorUtility.SetDirty(catalog);

            var prefab = PrefabUtility.LoadPrefabContents(BonusPrefab);
            try
            {
                var passenger = prefab.GetComponent<PlaygroundPassenger>();
                var so = new SerializedObject(passenger);
                var list = so.FindProperty("stopModels");
                list.arraySize = StopModels.Length;
                for (int i = 0; i < StopModels.Length; i++)
                {
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/{StopModels[i]}/{StopModels[i]}.fbx");
                    if (!fbx) { Debug.LogError("Reizigersmodel ontbreekt: " + StopModels[i]); return; }
                    list.GetArrayElementAtIndex(i).objectReferenceValue = fbx;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, BonusPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();
            Debug.Log($"PassengerWardrobe: {kinds.Count} reizigerstypes met looks; {StopModels.Length} stopmodellen op de bonusreiziger.");
        }
    }
}
