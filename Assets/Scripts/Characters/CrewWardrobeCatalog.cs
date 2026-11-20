// Conductor Chaos — crew-garderobe: welke skins en extraatjes een speler kan dragen (briefingruimte, Centraal).
// Skins delen het conducteur-lijf (Body, Arm_L/R, Leg_L/R), dus CrewOutfit wisselt alleen meshes en kleding.
// Accessoires staan in CrewAccessories.fbx onder Body/Acc_* op hun plek op het lijf.
// Het asset staat in Resources/CrewWardrobe en wordt gevuld door Conductor Chaos > Briefing > Build Grand Station Intro.
using UnityEngine;

namespace ConductorChaos.Characters
{
    [CreateAssetMenu(menuName = "Conductor Chaos/Crew Wardrobe", fileName = "CrewWardrobe")]
    public sealed class CrewWardrobeCatalog : ScriptableObject
    {
        public enum Slot { Head, Face, Neck }

        [System.Serializable]
        public struct Skin
        {
            public string label;
            public GameObject model;
        }

        [System.Serializable]
        public struct Accessory
        {
            public string label;
            public string node;
            public Slot slot;
        }

        [SerializeField] private Skin[] skins = System.Array.Empty<Skin>();
        [SerializeField] private GameObject accessoryModel;
        [SerializeField] private Accessory[] accessories = System.Array.Empty<Accessory>();
        [Tooltip("Hoofddeksels van de skins; verborgen zodra er een hoed op gaat.")]
        [SerializeField] private string[] headwear = { "Cap", "Helmet", "Beanie" };

        private static CrewWardrobeCatalog cached;
        public static CrewWardrobeCatalog Load() => cached ? cached : cached = Resources.Load<CrewWardrobeCatalog>("CrewWardrobe");

        public int SkinCount => skins.Length;
        public int AccessoryCount => accessories.Length;
        public GameObject SkinModel(int index) => index >= 0 && index < skins.Length ? skins[index].model : null;
        public Accessory AccessoryAt(int index) => accessories[index];
        public int IndexOf(string node) => System.Array.FindIndex(accessories, a => a.node == node);

        public bool IsHeadwear(string node) => System.Array.IndexOf(headwear, node) >= 0;

        /// <summary>De Acc_*-knoop in CrewAccessories (kind van Body, lokale stand = plek op het lijf).</summary>
        public Transform AccessoryNode(int index)
        {
            if (!accessoryModel || index < 0 || index >= accessories.Length) return null;
            Transform body = accessoryModel.transform.Find("Body");
            return body ? body.Find(accessories[index].node) : null;
        }

        public void Configure(Skin[] skinList, GameObject accessoryFbx, Accessory[] accessoryList)
        {
            skins = skinList;
            accessoryModel = accessoryFbx;
            accessories = accessoryList;
        }
    }
}
