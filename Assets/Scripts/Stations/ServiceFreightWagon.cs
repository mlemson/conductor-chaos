using ConductorChaos.Playground;
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    public sealed class ServiceFreightWagon : NetworkBehaviour
    {
        private TrainDrive train;
        internal Rigidbody Body { get; private set; }
        internal BoxCollider LoadArea { get; private set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            var container = RepairPoint.FindDeep(transform, "Container");
            if (container) container.gameObject.SetActive(false);
            var deck = RepairPoint.FindDeep(transform, "Deck");
            var renderer = deck ? deck.GetComponent<MeshRenderer>() : null;
            var materials = renderer ? renderer.sharedMaterials : null;
            Material rail = materials != null && materials.Length > 0 ? materials[0] : null;
            float center = -1.30f;
            AddBox("Laadvloer", new Vector3(0f, .055f, center), new Vector3(7.1f, .13f, 2.50f));
            AddRail("Rand perronzijde", new Vector3(0f, .20f, center + 1.22f), new Vector3(7.1f, .16f, .12f), rail);
            AddRail("Rand spoorzijde", new Vector3(0f, .26f, center - 1.22f), new Vector3(7.1f, .28f, .12f), rail);
            AddRail("Kopschot voor", new Vector3(3.49f, .31f, center), new Vector3(.14f, .38f, 2.50f), rail);
            AddRail("Kopschot achter", new Vector3(-3.49f, .31f, center), new Vector3(.14f, .38f, 2.50f), rail);
            LoadArea = AddBox("Vrachtvak", new Vector3(0f, .69f, center), new Vector3(6.82f, 1.14f, 2.30f));
            LoadArea.isTrigger = true;
        }

        private BoxCollider AddBox(string label, Vector3 center, Vector3 size)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = center;
            var box = part.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        private void AddRail(string label, Vector3 center, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = center;
            part.transform.localScale = size;
            if (material) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        public override void OnNetworkSpawn()
        {
            train = ServiceRun.Active ? ServiceRun.Active.Train : null;
            if (train) train.RegisterFreightWagon(Body);
        }

        public override void OnNetworkDespawn()
        {
            if (train) train.UnregisterFreightWagon(Body);
        }
    }
}
