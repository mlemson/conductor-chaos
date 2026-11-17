// Conductor Chaos — zet de depotkeuze om in echte voertuigen (issue #239, bouwt op #220).
// - Loc: de dienstscène bevat één geconfigureerde TrainDrive (de stoomloc). Is er een andere loc gekozen, dan wisselt
//   TrainDrive.Awake het zichtbare model om vóórdat NGO het object spawnt: op host én clients gelijk, omdat clients de
//   keuze via DepotSync vóór het laden van de scène ontvangen. TrainDrive vindt daarna zelf de cabinebediening (#220).
// - Wagons: extra wagons zijn instanties van de bestaande ServiceFreightWagon-prefab met een ander model. De soort reist
//   mee als instantiation data, zodat clients hetzelfde model en dezelfde onderhoudspunten (RepairPoint) opbouwen.
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Depot
{
    public struct DepotWagonData : INetworkSerializable
    {
        public byte Kind;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter => serializer.SerializeValue(ref Kind);
    }

    /// <summary>Bouwt op clients een gespawnde dienstwagon met het juiste model.</summary>
    public sealed class DepotWagonHandler : NetworkPrefabInstanceHandlerWithData<DepotWagonData>
    {
        private readonly GameObject prefab;
        public DepotWagonHandler(GameObject prefab) => this.prefab = prefab;

        public override NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation, DepotWagonData data)
        {
            var instance = Object.Instantiate(prefab, position, rotation);
            if (instance.TryGetComponent<ServiceFreightWagon>(out var wagon)) wagon.ApplyKind((WagonKind)data.Kind);
            return instance.GetComponent<NetworkObject>();
        }

        public override void Destroy(NetworkObject networkObject)
        {
            if (networkObject) Object.Destroy(networkObject.gameObject);
        }
    }

    public static class DepotVehicles
    {
        /// <summary>Alleen de levels gebruiken het depot; de introductie en de playground blijven de stoomtrein.</summary>
        public static bool AppliesToScene(string scene)
        {
            var level = DepotCatalog.LevelForScene(scene);
            return level != null && level.UsesDepot;
        }

        /// <summary>Vanuit TrainDrive.Awake op de dienst-trein: gekozen loc tonen, kolenwagen weglaten en snelheid instellen.</summary>
        internal static void ApplyLocomotive(TrainDrive drive, Rigidbody[] wagons)
        {
            if (!drive || wagons == null || wagons.Length == 0 || !AppliesToScene(drive.gameObject.scene.name)) return;
            var loadout = DepotLoadout.Active;
            if (loadout.Train == TrainKind.Steam) return;
            var info = DepotCatalog.Train(loadout.Train);
            var models = DepotModels.Instance;
            var model = models ? models.Locomotive(loadout.Train) : null;
            if (!model)
            {
                Debug.LogWarning($"Depot: model voor {info.Name} ontbreekt in Resources/DepotModels; de stoomloc rijdt.", drive);
                return;
            }

            // De kolenwagen hoort bij de stoomloc: weg uit de samenstelling en onzichtbaar op zijn plek laten staan.
            var kept = new System.Collections.Generic.List<Rigidbody>();
            foreach (var wagon in wagons)
            {
                if (wagon && RepairPoint.FindDeep(wagon.transform, "CoalStock")) Hide(wagon.gameObject);
                else kept.Add(wagon);
            }
            drive.ConfigureWagons(kept.ToArray());
            drive.SetDepotPerformance(info.MaximumSpeed, info.AccelerationRate);

            var root = drive.transform;
            if (root.TryGetComponent<Rigidbody>(out var body)) body.isKinematic = true;
            for (int i = root.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.GetChild(i).gameObject);
            var instance = Object.Instantiate(model, root, false);
            instance.name = model.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            PrepareVehicle(drive.gameObject, instance.transform);
        }

        /// <summary>Wisselt het model van een dienstwagon (ServiceFreightWagon) naar de gekozen soort.</summary>
        internal static Transform BuildWagon(GameObject root, WagonKind kind)
        {
            var models = DepotModels.Instance;
            var model = models ? models.Wagon(kind) : null;
            if (!model)
            {
                Debug.LogWarning($"Depot: model voor {DepotCatalog.Wagon(kind).Name} ontbreekt in Resources/DepotModels.", root);
                return null;
            }
            for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var instance = Object.Instantiate(model, root.transform, false);
            instance.name = model.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            PrepareVehicle(root, instance.transform);
            return instance.transform;
        }

        /// <summary>Botsvormen, onderhoudspunten en slijtage, net als Train Sets > Prepare Selected Vehicles in de editor.</summary>
        private static void PrepareVehicle(GameObject vehicle, Transform model)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (TrainBreakdowns.IsEndDoorLeaf(filter.name))
                {
                    TrainBreakdowns.EnsureEndDoorCollider(filter);
                    continue;
                }
                if (!TrainBreakdowns.NeedsMeshCollider(filter)) continue;
                var mesh = filter.sharedMesh;
                if (!mesh.isReadable)
                {
                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = Vector3.Max(mesh.bounds.size, Vector3.one * .02f);
                    continue;
                }
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = filter.name == "TankHatch";
            }
            bool hasRepairs = false;
            bool hasTank = false;
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
            {
                hasTank |= node.name == "TankLiquid";
                if (!node.name.StartsWith("RepairPoint_", System.StringComparison.Ordinal)) continue;
                if (!node.GetComponent<RepairPoint>()) node.gameObject.AddComponent<RepairPoint>().Configure(false);
                hasRepairs = true;
            }
            if ((hasRepairs || TrainBreakdowns.HasMovingDoors(vehicle)) && vehicle.GetComponent<NetworkObject>() && !vehicle.GetComponent<TrainBreakdowns>())
                vehicle.AddComponent<TrainBreakdowns>();
            if (hasTank && !vehicle.GetComponent<ConductorChaos.Yard.LiquidTank>())
                vehicle.AddComponent<ConductorChaos.Yard.LiquidTank>();
        }

        private static void Hide(GameObject vehicle)
        {
            if (vehicle.TryGetComponent<Rigidbody>(out var body)) { body.isKinematic = true; body.useGravity = false; }
            foreach (var renderer in vehicle.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (var collider in vehicle.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        /// <summary>Lokale bounds van alle zichtbare onderdelen, in de ruimte van root.</summary>
        internal static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = root.InverseTransformPoint(filter.transform.TransformPoint(p));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
}
