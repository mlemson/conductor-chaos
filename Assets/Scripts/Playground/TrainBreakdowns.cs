// Conductor Chaos — slijtage aan de trein: af en toe gaat er een onderhoudspunt kapot (dakventilator, stroomafnemer,
// luik, aspot, remslang) en moet iemand het met de moersleutel repareren (RepairPoint, issue #215).
// Zet dit op een voertuig van de nieuwe treinsets (Tools > Conductor Chaos > Train Sets doet dat). Zelf geen
// netwerkstate: alleen de server kiest en roept RepairPoint.ServerBreak aan; de RepairPoint synchroniseert de stand.
// Nog geen gevolgen voor het rijden (snelheid, licht): dat is een aparte stap.
using ConductorChaos.Track;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [DisallowMultipleComponent]
    public sealed class TrainBreakdowns : MonoBehaviour
    {
        [Tooltip("Eerste storing op zijn vroegst na zoveel seconden.")]
        [SerializeField, Min(0f)] float firstAfter = 60f;
        [Tooltip("Tijd tussen twee storingen (s), willekeurig tussen deze twee waarden.")]
        [SerializeField] Vector2 interval = new Vector2(45f, 110f);
        [Tooltip("Zoveel onderhoudspunten mogen er op dit voertuig tegelijk kapot zijn.")]
        [SerializeField, Range(1, 6)] int maxBroken = 1;

        RepairPoint[] points;
        float next;

        void Start()
        {
            EnsureMeshColliders(gameObject);
            points = GetComponentsInChildren<RepairPoint>(true);
            next = Time.time + firstAfter + Random.Range(0f, Mathf.Max(0f, interval.x));
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
            if (!filter.sharedMesh) return;
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

        void Update()
        {
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
