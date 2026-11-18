// Conductor Chaos — losse spullen op het vertrekperron, gedeeld over het netwerk (server-fysica, NetworkTransform,
// net als de moersleutel; je pakt ze met je handen). De prefabs bouwt Tools > Conductor Chaos > Service Gear (ServiceGearPrefabs).
//   Jerrycan        20 liter diesel. Houd hem vast met de tuit boven de gele vuldop van de diesel: dan loopt hij leeg in de tank.
//   Pompslang       spuitmond aan een dieselpomp op het perron (slang van 10 m). Vasthouden boven de vuldop = tanken.
//   Sneeuwschuiver  breed blad: wat je ermee raakt terwijl je hem vasthoudt, schuift extra hard weg (sneeuw, stammen, rotsen).
using ConductorChaos.Playground;
using ConductorChaos.Yard;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Stations
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class ServiceGear : NetworkBehaviour
    {
        public enum GearKind : byte { Jerrycan, FuelNozzle, SnowPusher }

        public const string JerrycanResource = "ServiceGear_Jerrycan";
        public const string NozzleResource = "ServiceGear_FuelNozzle";
        public const string PusherResource = "ServiceGear_SnowPusher";
        public static readonly string[] ResourceNames = { JerrycanResource, NozzleResource, PusherResource };

        private const float JerrycanLiters = 20f, HoseLength = 10f, PushAcceleration = 4f, MaxPushedMass = 300f;

        [SerializeField] private GearKind kind;
        [Tooltip("Jerrycan en spuitmond: de straal diesel.")]
        [SerializeField] private LiquidSource spout;
        [Tooltip("Spuitmond: waar de slang vastzit.")]
        [SerializeField] private Transform hoseIn;
        [Tooltip("Spuitmond: model van de pomp die op het perron komt te staan.")]
        [SerializeField] private GameObject pumpModel;
        [SerializeField] private Material hoseMaterial;

        private readonly NetworkVariable<float> liters = new(JerrycanLiters);
        private readonly NetworkVariable<bool> pouring = new();
        private readonly NetworkVariable<Vector3> pumpPosition = new();
        private readonly NetworkVariable<Quaternion> pumpRotation = new(Quaternion.identity);

        private Rigidbody body;
        private GameObject pump;

        internal GearKind Kind => kind;
        internal float Liters => kind == GearKind.Jerrycan ? liters.Value : float.PositiveInfinity;

        /// <summary>Door de prefabbouwer.</summary>
        public void Configure(GearKind gear, LiquidSource source, Transform hose, GameObject pumpAsset, Material hoseLook)
        {
            kind = gear;
            spout = source;
            hoseIn = hose;
            pumpModel = pumpAsset;
            hoseMaterial = hoseLook;
        }

        private void Awake() => body = GetComponent<Rigidbody>();

        public override void OnNetworkSpawn()
        {
            if (kind == GearKind.FuelNozzle) BuildPump();
        }

        public override void OnNetworkDespawn()
        {
            if (pump) Destroy(pump);
        }

        /// <summary>Iedereen zet de (niet-netwerk) pomp zelf neer op de plek die de server meegaf.</summary>
        private void BuildPump()
        {
            if (!pumpModel || pump) return;
            pump = Instantiate(pumpModel, pumpPosition.Value, pumpRotation.Value);
            pump.name = "Dieselpomp";
            var bounds = Depot.DepotVehicles.LocalBounds(pump.transform);
            var box = pump.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            Transform start = Find(pump.transform, "Point_ReelHose");
            if (!start) start = Find(pump.transform, "Point_HoseOut");
            if (!start) start = pump.transform;
            pump.AddComponent<HoseLine>().Setup(start, hoseIn ? hoseIn : transform, body, HoseLength, hoseMaterial);
        }

        private void FixedUpdate()
        {
            if (!IsServer || !IsSpawned || !spout) return;
            bool open = (kind != GearKind.Jerrycan || liters.Value > 0f) && IsHeld() && AboveFiller();
            if (pouring.Value != open) pouring.Value = open;
            if (open && kind == GearKind.Jerrycan)
                liters.Value = Mathf.Max(0f, liters.Value - spout.LitersPerSecond * Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (spout) spout.Open = IsSpawned && pouring.Value;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (kind != GearKind.SnowPusher || !IsServer || !IsSpawned) return;
            var other = collision.rigidbody;
            if (!other || other.isKinematic || other.mass > MaxPushedMass || other.GetComponentInParent<PlaygroundPlayer>() || !IsHeld())
                return;
            Vector3 push = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            if (push.sqrMagnitude < .04f) push = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (push.sqrMagnitude < 1e-4f) return;
            other.AddForce(push.normalized * PushAcceleration, ForceMode.Acceleration);
        }

        private bool IsHeld()
        {
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.TryGetControlHand(body, out _, out _, out _)) return true;
            return false;
        }

        /// <summary>De tuit hangt boven een open vulopening (vuldop van de diesel of de ketelwagon).</summary>
        private bool AboveFiller()
        {
            Vector3 outlet = spout.OutletPosition;
            foreach (var inlet in LiquidInlets.All)
            {
                if (inlet == null || !inlet.HatchOpen) continue;
                Vector3 offset = outlet - inlet.Inlet;
                if (offset.y > -.05f && offset.y < 1.3f && new Vector2(offset.x, offset.z).sqrMagnitude < .45f * .45f) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ neerzetten (server)

        /// <summary>Server, bij het klaarzetten van een dienst: spullen op het vertrekperron naast de loc.</summary>
        internal static void SpawnAtDeparture(ServiceStation departure, Transform train, bool diesel, bool snow)
        {
            var platform = departure ? departure.Platform : null;
            if (!platform || !train || (!diesel && !snow)) return;
            Physics.SyncTransforms();
            Vector3 along = platform.size.x >= platform.size.z ? platform.transform.right : platform.transform.forward;
            along = Vector3.ProjectOnPlane(along, Vector3.up).normalized;
            Vector3 edge = platform.ClosestPoint(train.position);
            Vector3 inward = Vector3.ProjectOnPlane(platform.bounds.center - edge, Vector3.up);
            inward = (inward - Vector3.Project(inward, along)).normalized;
            if (inward.sqrMagnitude < .5f) inward = Vector3.Cross(Vector3.up, along);
            var facingTrack = Quaternion.LookRotation(-inward, Vector3.up);

            if (diesel)
            {
                if (TryFloor(platform, edge + inward * 1.1f, out var pumpFloor))
                    Spawn(NozzleResource, pumpFloor + along * .7f + Vector3.up * .25f, facingTrack, gear =>
                    {
                        gear.pumpPosition.Value = pumpFloor;
                        gear.pumpRotation.Value = facingTrack;
                    });
                foreach (float d in new[] { -1.4f, -2f })
                    if (TryFloor(platform, edge + inward * 1.4f + along * d, out var floor))
                        Spawn(JerrycanResource, floor + Vector3.up * .3f, facingTrack, null);
            }
            if (snow && TryFloor(platform, edge + inward * 1.6f + along * 2.6f, out var pusherFloor))
                Spawn(PusherResource, pusherFloor + Vector3.up * .15f, Quaternion.LookRotation(along, Vector3.up), null);
        }

        private static bool TryFloor(BoxCollider platform, Vector3 point, out Vector3 floor)
        {
            floor = default;
            if (!Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) ||
                hit.rigidbody || hit.normal.y < .8f || !ServiceStation.Contains(platform, hit.point + Vector3.up * .3f))
            {
                Debug.LogWarning($"ServiceGear: geen vrije perronvloer bij {point}; dit voorwerp blijft weg.", platform);
                return false;
            }
            floor = hit.point;
            return true;
        }

        private static void Spawn(string resource, Vector3 position, Quaternion rotation, System.Action<ServiceGear> before)
        {
            var asset = Resources.Load<GameObject>(resource);
            if (!asset || !asset.GetComponent<NetworkObject>())
            {
                Debug.LogWarning($"ServiceGear: Resources/{resource} ontbreekt. Bouw hem met Tools > Conductor Chaos > Service Gear.");
                return;
            }
            var instance = Instantiate(asset, position, rotation);
            if (instance.TryGetComponent<ServiceGear>(out var gear)) before?.Invoke(gear);
            instance.GetComponent<NetworkObject>().Spawn(true);
        }

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = Find(child, name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
