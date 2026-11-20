// Conductor Chaos — kledingrek in de briefingruimte: pak een tenue (Outfit_Conductor/Engineer/Stoker) vast en je
// draagt die skin, in elk level (CrewOutfit onthoudt het). De volgorde van `outfits` = de skins in de garderobe.
// Server ziet de greep en zet de skin; de hangers zijn vaste (kinematische) lijven zonder eigen netwerkstate.
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    public sealed class CrewWardrobeRack : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] outfits = System.Array.Empty<Rigidbody>();

        public void Configure(Rigidbody[] hangers) => outfits = hangers;

        private void FixedUpdate()
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer) return;
            for (int skin = 0; skin < outfits.Length; skin++)
            {
                if (!outfits[skin]) continue;
                foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                    if (player && player.IsSpawned && player.TryGetControlHand(outfits[skin], out _, out _, out _) &&
                        player.TryGetComponent<CrewOutfit>(out var outfit))
                        outfit.ServerWearSkin(skin);
            }
        }
    }
}
