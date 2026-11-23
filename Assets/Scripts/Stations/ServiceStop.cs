using ConductorChaos.Playground;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Stations
{
    public sealed class ServiceStop : MonoBehaviour
    {
        [SerializeField] private BoxCollider platform;
        [SerializeField] private Transform waiting;
        [SerializeField] private GameObject[] freightPrefabs;
        private ServiceCargo post, required, forbidden;
        private bool opened;

        internal bool DoorAtPlatform(Transform outside) => ServiceStation.Contains(platform, outside.position);

        public void Configure(BoxCollider zone, Transform waitingArea, GameObject[] freight)
        {
            platform = zone;
            waiting = waitingArea;
            freightPrefabs = freight;
        }

        internal bool UpdateOnServer(TrainDrive train, ServiceTickets tickets, BoxCollider destination,
            ServiceManifest manifest, bool pickup)
        {
            float distance = (train.ServiceRailPosition - transform.position).sqrMagnitude;
            if (!opened && distance < 65f * 65f)
            {
                opened = true;
                if (train.ServiceCoachVisible && tickets)
                    tickets.SpawnAtStop(this, waiting, manifest);

                if (pickup)
                {
                    required = SpawnFreight(manifest.RequiredFreight, 2.7f, destination, train);
                    forbidden = SpawnFreight(manifest.ExcludedFreight, 4.1f, destination, train);
                    post = SpawnFreight(manifest.BonusFreight, 5.5f, destination, train);
                }
                else post = SpawnFreight(0, 3.5f, destination, train);
            }

            bool travelling = train.ServiceSpeed >= .22f;
            Observe(post, travelling);
            Observe(required, travelling);
            Observe(forbidden, travelling);
            // Shunting alongside the platform gives the crew a chance to correct a loading mistake.
            return forbidden && forbidden.AboardCount > 0 && travelling && distance > 23f * 23f;
        }

        private static void Observe(ServiceCargo freight, bool travelling)
        {
            if (!freight) return;
            freight.ObserveLoadingOnServer();
            if (travelling) freight.ObserveTravelOnServer();
        }

        private ServiceCargo SpawnFreight(int type, float offset, BoxCollider destination, TrainDrive train)
        {
            var prefab = freightPrefabs[type].GetComponent<NetworkObject>();
            var item = Spawn(prefab, waiting.position + waiting.right * .75f + waiting.forward * offset, waiting.rotation);
            var body = item.GetComponent<Rigidbody>();
            body.mass = Mathf.Min(body.mass, 6f);
            var cargo = gameObject.AddComponent<ServiceCargo>();
            cargo.SetBonusCargo(body, destination, train);
            return cargo;
        }

        internal int DeliveredPost => post ? post.CountBonusDelivery() : 0;
        internal bool DeliveredRequired => required && required.CountBonusDelivery() > 0;

        internal NetworkObject Spawn(NetworkObject prefab, Vector3 position, Quaternion rotation, byte appearance = 0)
        {
            var item = Instantiate(prefab, position, rotation);
            SceneManager.MoveGameObjectToScene(item.gameObject, gameObject.scene);
            if (appearance != 0) item.GetComponent<PlaygroundPassenger>().SelectStopAppearance(appearance);
            item.Spawn(destroyWithScene: true);
            return item;
        }
    }
}
