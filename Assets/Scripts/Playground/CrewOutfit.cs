// Conductor Chaos — wat een speler draagt: crew-skin (conducteur, monteur, stoker) en extraatjes (hoed, bril, ...).
// Netwerk: de server schrijft één gepakte waarde (skin + per slot een accessoire); grijpen aan het kledingrek of
// een extraatje laat de server die waarde zetten. De eigenaar onthoudt hem lokaal (PlayerPrefs) en vraagt hem bij
// elke spawn opnieuw aan, zodat het tenue in elk level terugkomt. Afzetten kan niet: een nieuw extraatje in
// hetzelfde slot vervangt het oude.
using System.Collections.Generic;
using ConductorChaos.Characters;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(PlaygroundPlayer))]
    public sealed class CrewOutfit : NetworkBehaviour
    {
        private const string SavedLook = "ConductorChaos.CrewLook";
        private const int SlotCount = 3;

        private readonly NetworkVariable<int> look = new(0);
        private readonly List<GameObject> clothing = new();
        private readonly List<GameObject> extras = new();
        private PlaygroundPlayer player;
        private bool dirty, dressed;

        public int Skin => look.Value & 0xF;
        public int Worn(CrewWardrobeCatalog.Slot slot) => (look.Value >> (4 + 4 * (int)slot) & 0xF) - 1;

        private void Awake() => player = GetComponent<PlaygroundPlayer>();

        public override void OnNetworkSpawn()
        {
            look.OnValueChanged += OnLookChanged;
            dirty = true;
            if (IsOwner) RequestLookServerRpc(PlayerPrefs.GetInt(SavedLook, 0));
        }

        public override void OnNetworkDespawn() => look.OnValueChanged -= OnLookChanged;

        private void OnLookChanged(int previous, int current)
        {
            dirty = true;
            if (!IsOwner) return;
            PlayerPrefs.SetInt(SavedLook, current);
            PlayerPrefs.Save();
        }

        [ServerRpc]
        private void RequestLookServerRpc(int saved) => look.Value = Sanitize(saved);

        /// <summary>Server: tenue van het kledingrek.</summary>
        internal void ServerWearSkin(int skin)
        {
            var catalog = CrewWardrobeCatalog.Load();
            if (!IsServer || !catalog || skin < 0 || skin >= catalog.SkinCount) return;
            int next = (look.Value & ~0xF) | skin;
            if (next != look.Value) look.Value = next;
        }

        /// <summary>Server: extraatje gepakt, gaat meteen op (vervangt wat in hetzelfde slot zat).</summary>
        internal void ServerWearAccessory(int index)
        {
            var catalog = CrewWardrobeCatalog.Load();
            if (!IsServer || !catalog || index < 0 || index >= catalog.AccessoryCount) return;
            int shift = 4 + 4 * (int)catalog.AccessoryAt(index).slot;
            int next = (look.Value & ~(0xF << shift)) | ((index + 1) << shift);
            if (next != look.Value) look.Value = next;
        }

        private static int Sanitize(int value)
        {
            var catalog = CrewWardrobeCatalog.Load();
            if (!catalog) return 0;
            int skin = value & 0xF;
            int result = skin < catalog.SkinCount ? skin : 0;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int shift = 4 + 4 * slot;
                int index = (value >> shift & 0xF) - 1;
                if (index >= 0 && index < catalog.AccessoryCount && (int)catalog.AccessoryAt(index).slot == slot)
                    result |= (index + 1) << shift;
            }
            return result;
        }

        private void LateUpdate()
        {
            if (!dirty || !player || !player.VisualReady) return;
            dirty = false;
            // Het model in de prefab is al de conducteur zonder extraatjes: niets te doen tot er iets verandert.
            if (look.Value == 0 && !dressed) return;
            Dress(look.Value);
        }

        private void Dress(int value)
        {
            var catalog = CrewWardrobeCatalog.Load();
            Transform visual = player.Visual;
            Transform body = visual ? Find(visual, "Body") : null;
            GameObject skin = catalog ? catalog.SkinModel(value & 0xF) : null;
            Transform source = skin ? Find(skin.transform, "Body") : null;
            if (!body || !source)
            {
                Debug.LogWarning("CrewOutfit: geen garderobe (Resources/CrewWardrobe) of lijf gevonden.");
                return;
            }
            dressed = true;
            CopyLook(source, body);
            CopyLook(source.Find("Arm_L"), player.LeftArm);
            CopyLook(source.Find("Arm_R"), player.RightArm);

            foreach (Transform child in body)
                if (IsClothing(child.name) && !clothing.Contains(child.gameObject) && !extras.Contains(child.gameObject))
                    child.gameObject.SetActive(false);                // kleding van het prefabmodel
            foreach (var item in clothing) if (item) Destroy(item);
            foreach (var item in extras) if (item) Destroy(item);
            clothing.Clear();
            extras.Clear();
            foreach (Transform child in source)
                if (IsClothing(child.name)) clothing.Add(Spawn(child, body));

            bool hat = false;
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int index = (value >> (4 + 4 * slot) & 0xF) - 1;
                Transform node = index >= 0 ? catalog.AccessoryNode(index) : null;
                if (!node) continue;
                extras.Add(Spawn(node, body));
                hat |= slot == (int)CrewWardrobeCatalog.Slot.Head;
            }
            foreach (var item in clothing)
                if (catalog.IsHeadwear(item.name)) item.SetActive(!hat);
            player.RefreshOutfitVisual();
        }

        private static GameObject Spawn(Transform template, Transform parent)
        {
            GameObject copy = Instantiate(template.gameObject, parent, false);
            copy.name = template.name;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            return copy;
        }

        private static void CopyLook(Transform from, Transform to)
        {
            if (!from || !to || !from.TryGetComponent<MeshFilter>(out var mesh) || !to.TryGetComponent<MeshFilter>(out var target)) return;
            target.sharedMesh = mesh.sharedMesh;
            if (from.TryGetComponent<MeshRenderer>(out var look) && to.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.sharedMaterials = look.sharedMaterials;
        }

        private static bool IsClothing(string node) =>
            !(node.StartsWith("Eye_") || node.StartsWith("EyeClosed_") || node.StartsWith("Lid_") || node.StartsWith("Blink_") ||
              node.StartsWith("Pupil_") || node.StartsWith("Mouth_") || node.StartsWith("Arm_") || node.StartsWith("Hand_") ||
              node.StartsWith("Leg_") || node.StartsWith("Acc_") || node.StartsWith("Playground"));

        private static Transform Find(Transform root, string node)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == node) return item;
            return null;
        }
    }
}
