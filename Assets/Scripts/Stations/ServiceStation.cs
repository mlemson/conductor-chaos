using UnityEngine;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Conductor Chaos/Station voor een dienst")]
    public sealed class ServiceStation : MonoBehaviour
    {
        public enum StationRole { Vertrek, Aankomst }

        [SerializeField] private StationRole role;
        [SerializeField] private string stationName;
        [SerializeField] private Transform crewSpawn;
        [SerializeField] private BoxCollider trainStop;
        [SerializeField] private BoxCollider platform;
        [SerializeField] private Transform departureBell;

        public StationRole Role => role;
        public string StationName => string.IsNullOrWhiteSpace(stationName) ? name : stationName;
        public Transform CrewSpawn => crewSpawn;
        public BoxCollider TrainStop => trainStop;
        public BoxCollider Platform => platform;
        public Transform DepartureBell => departureBell;

        public static bool Contains(BoxCollider zone, Vector3 point)
        {
            if (!zone || !zone.enabled || !zone.gameObject.activeInHierarchy) return false;
            Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
            Vector3 half = zone.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnDrawGizmosSelected()
        {
            DrawZone(platform, new Color(1f, .72f, .16f));
            DrawZone(trainStop, role == StationRole.Vertrek ? Color.cyan : Color.green);
            if (!crewSpawn) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(crewSpawn.position, .18f);
            Gizmos.DrawRay(crewSpawn.position, crewSpawn.forward);
        }

        private static void DrawZone(BoxCollider zone, Color color)
        {
            if (!zone) return;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = zone.transform.localToWorldMatrix;
            Gizmos.color = color;
            Gizmos.DrawWireCube(zone.center, zone.size);
            Gizmos.matrix = previous;
        }
    }
}
