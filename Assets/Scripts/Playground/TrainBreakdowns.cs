// Conductor Chaos — slijtage aan de trein: af en toe gaat er een onderhoudspunt kapot (dakventilator, stroomafnemer,
// luik, aspot, remslang) en moet iemand het met de moersleutel repareren (RepairPoint, issue #215).
// Zet dit op een voertuig van de nieuwe treinsets (Tools > Conductor Chaos > Train Sets doet dat). Alleen de server kiest een
// storing. RepairPoint synchroniseert de staat.
// Nog geen gevolgen voor het rijden (snelheid, licht): dat is een aparte stap.
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent, RequireComponent(typeof(NetworkObject))]
    public sealed class TrainBreakdowns : NetworkBehaviour
    {
        [Tooltip("Eerste storing op zijn vroegst na zoveel seconden.")]
        [SerializeField, Min(0f)] float firstAfter = 60f;
        [Tooltip("Tijd tussen twee storingen (s), willekeurig tussen deze twee waarden.")]
        [SerializeField] Vector2 interval = new Vector2(45f, 110f);
        [Tooltip("Zoveel onderhoudspunten mogen er op dit voertuig tegelijk kapot zijn.")]
        [SerializeField, Range(1, 6)] int maxBroken = 1;

        private NetworkList<float> openings;
        private TrainVehicleDoors doors;

        private void Awake() => openings = new NetworkList<float>();

        RepairPoint[] points;
        float next;

        void Start()
        {
            EnsureMeshColliders(gameObject);
            points = GetComponentsInChildren<RepairPoint>(true);
            next = Time.time + firstAfter + Random.Range(0f, Mathf.Max(0f, interval.x));
        }

        public static bool HasMovingDoors(GameObject vehicle) =>
            System.Array.Exists(vehicle.GetComponentsInChildren<MeshFilter>(true), filter =>
                filter.name.StartsWith("SideDoorLeaf", System.StringComparison.Ordinal) || IsEndDoorLeaf(filter.name) ||
                filter.name.StartsWith("RoofHatch_", System.StringComparison.Ordinal) ||
                filter.name.StartsWith("SlideDoor_", System.StringComparison.Ordinal));

        internal static void PrepareDoors(GameObject vehicle)
        {
            if (vehicle.GetComponent<NetworkObject>() && !vehicle.GetComponent<TrainBreakdowns>() && HasMovingDoors(vehicle))
                vehicle.AddComponent<TrainBreakdowns>();
        }

        public static bool IsEndDoorLeaf(string name) => name.StartsWith("EndDoorLeaf_", System.StringComparison.Ordinal);

        public static bool NeedsMeshCollider(MeshFilter filter) =>
            filter.sharedMesh && !filter.GetComponent<Collider>() &&
            !filter.name.StartsWith("Repair_Bolt", System.StringComparison.Ordinal) &&
            !filter.name.StartsWith("Broken_Bits", System.StringComparison.Ordinal) &&
            filter.name != "TankLiquid";

        internal static void EnsureMeshColliders(GameObject vehicle)
        {
            foreach (var filter in vehicle.GetComponentsInChildren<MeshFilter>(true))
            {
                if (IsEndDoorLeaf(filter.name))
                {
                    EnsureEndDoorCollider(filter);
                    continue;
                }
                if (!NeedsMeshCollider(filter)) continue;
                var owner = filter.GetComponentInParent<Rigidbody>();
                if (owner && !owner.isKinematic) continue;
                Mesh mesh = filter.sharedMesh;
                // Imported FBX meshes can be non-readable in Windows builds. Unity tries
                // to cook the mesh on AddComponent, so check BEFORE adding a MeshCollider.
                if (!mesh.isReadable)
                {
                    // Keep door and control collisions available without requiring
                    // Read/Write on every visual train mesh.
                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
                    continue;
                }

                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
        }

        internal static void EnsureEndDoorCollider(MeshFilter filter)
        {
            if (!filter.sharedMesh || TrainVehicleDoors.IsDoor(filter.GetComponent<Rigidbody>())) return;
            // A sliding leaf needs a solid shape even where the visual mesh cannot be cooked.
            foreach (var mesh in filter.GetComponents<MeshCollider>())
                if (!mesh.isTrigger) mesh.enabled = false;
            var box = System.Array.Find(filter.GetComponents<BoxCollider>(), shape => !shape.isTrigger);
            if (!box) box = filter.gameObject.AddComponent<BoxCollider>();
            Bounds bounds = filter.sharedMesh.bounds;
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * .02f);
            box.enabled = true;
        }

        public override void OnNetworkSpawn()
        {
            doors = new TrainVehicleDoors(gameObject);
            if (IsServer)
            {
                openings.Clear();
                for (int i = 0; i < doors.Count; i++) openings.Add(doors.Initial(i));
            }
            PresentDoors();
        }

        public override void OnNetworkDespawn()
        {
            doors?.Dispose();
            doors = null;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || doors == null) return;
            var drive = TrainDrive.FindControlOwner(GetComponent<Rigidbody>());
            bool allowAutomatic = !drive || !drive.ServiceDerailed;
            for (int i = 0; i < doors.Count && i < openings.Count; i++)
            {
                float opening = doors.ReadInput(i, openings[i], allowAutomatic);
                if (Mathf.Abs(openings[i] - opening) > .0001f) openings[i] = opening;
                doors.Present(i, openings[i]);
            }
        }

        private void PresentDoors()
        {
            if (doors == null) return;
            for (int i = 0; i < doors.Count && i < openings.Count; i++) doors.Present(i, openings[i]);
        }

        void Update()
        {
            if (IsSpawned && !IsServer) PresentDoors();
            var network = NetworkManager.Singleton;
            if (!network || !network.IsServer || points == null || points.Length == 0 || Time.time < next) return;
            next = Time.time + Random.Range(Mathf.Min(interval.x, interval.y), Mathf.Max(interval.x, interval.y));
            int broken = 0, intact = 0;
            foreach (var point in points)
            {
                if (!point || !point.IsSpawned) continue;
                if (point.IsBroken) broken++;
                else intact++;
            }
            if (broken >= maxBroken || intact == 0) return;
            int pick = Random.Range(0, intact);
            foreach (var point in points)
            {
                if (!point || !point.IsSpawned || point.IsBroken) continue;
                if (pick-- == 0) { point.ServerBreak(); return; }
            }
        }
    }
}
