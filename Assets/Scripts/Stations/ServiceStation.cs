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
        public string StationName => ServiceRun.PlannedStationName(gameObject.scene.name, role) ??
            (string.IsNullOrWhiteSpace(stationName) ? name : stationName);
        public Transform CrewSpawn => crewSpawn;
        public BoxCollider TrainStop => trainStop;
        public BoxCollider Platform => platform;
        public Transform DepartureBell => departureBell;

        internal static void ApplyRouteSigns(ServiceStation departure, ServiceStation arrival)
        {
            if (!departure) return;
            const float stationRadiusSquared = 55f * 55f;
            StationGlyphFont font = Resources.Load<StationGlyphFont>("StationGlyphFont");
            foreach (var root in departure.gameObject.scene.GetRootGameObjects())
            {
                if (Mathf.Min(Distance(departure, root.transform), Distance(arrival, root.transform)) > stationRadiusSquared) continue;
                foreach (Transform node in root.GetComponentsInChildren<Transform>())
                {
                    if (node.name != "Sign_Name") continue;
                    float toDeparture = Distance(departure, node);
                    float toArrival = Distance(arrival, node);
                    if (Mathf.Min(toDeparture, toArrival) > stationRadiusSquared) continue;
                    var station = toDeparture <= toArrival ? departure : arrival;
                    if (!node.TryGetComponent<StationSign>(out var sign))
                        sign = AddSign(node, font);
                    if (sign) sign.Text = station.StationName;
                }
            }
        }

        private static float Distance(ServiceStation station, Transform target) =>
            station ? (station.transform.position - target.position).sqrMagnitude : float.PositiveInfinity;

        private static StationSign AddSign(Transform node, StationGlyphFont font)
        {
            if (!font) return null;
            var label = node.Find("Sign_Name_Text");
            var icon = node.Find("Sign_Name_Icon");
            if (!label || !label.TryGetComponent<Renderer>(out var textRenderer)) return null;
            var iconRenderer = icon ? icon.GetComponent<Renderer>() : null;
            var sign = node.gameObject.AddComponent<StationSign>();
            // Raw imported boards use the same post, station house and hall proportions as the authored prefabs.
            bool post = node.parent && node.parent.name.Contains("StationSign");
            bool hall = node.parent && node.parent.name.Contains("Station_Main");
            sign.Configure(font, "", hall ? .368f : post ? .264f : .28f, hall ? 4.268f : post ? 1.724f : 2.5f,
                hall ? .4784f : post ? .3432f : .364f, hall ? .1656f : post ? .1188f : .126f,
                post ? .84919f : 1f, hall ? .076f : post ? .056f : .066f, .035f, textRenderer, iconRenderer);
            return sign;
        }

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
