// Conductor Chaos — extraatje in de briefingruimte (hoed, bril, snor, vlinderdas). Pak het met je hand vast en het
// gaat meteen op: de server ziet de greep en zet het op de CrewOutfit van die speler. Het ding zelf blijft liggen,
// zodat iedereen er een kan pakken. Vast (kinematisch) lijf, geen eigen netwerkstate.
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CrewAccessoryPickup : MonoBehaviour
    {
        [SerializeField] private int accessory;
        private Rigidbody body;

        public void Configure(int index) => accessory = index;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        private void FixedUpdate()
        {
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer) return;
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.IsSpawned && player.TryGetControlHand(body, out _, out _, out _) &&
                    player.TryGetComponent<CrewOutfit>(out var outfit))
                    outfit.ServerWearAccessory(accessory);
        }
    }
}
