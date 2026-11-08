using System.Collections.Generic;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Rigidbody))]
    public sealed class TrainDrive : NetworkBehaviour
    {
        [SerializeField] private TrackPiece firstTrack;
        [SerializeField] private float startDistance;
        [SerializeField] private float maximumSpeed = 6f;
        [SerializeField] private float acceleration = 2f;
        [SerializeField] private Rigidbody[] availableWagons = System.Array.Empty<Rigidbody>();
        private readonly NetworkVariable<bool> powered = new(false);
        private readonly NetworkVariable<bool> buttonDown = new(false);
        private readonly NetworkVariable<float> throttle = new(0f);
        private readonly NetworkVariable<float> displayedSpeed = new(0f);
        private readonly NetworkVariable<float> doorAngle = new(0f);
        private readonly NetworkVariable<Vector2> wagonDoorOffsets = new(Vector2.zero);
        private const float FuelCapacity = 3f;
        private const float StartingFuel = FuelCapacity * 0.5f;
        private readonly NetworkVariable<float> fuel = new(StartingFuel);
        private readonly NetworkVariable<Vector3> shovelPosition = new();
        private readonly NetworkVariable<Quaternion> shovelRotation = new(Quaternion.identity);
        private Rigidbody body;
        private TrackPiece track;
        private float distance, speed;
        private Transform lever;
        private Transform leverParent;
        private Transform leverPivot;
        private Rigidbody leverBody;
        private PlaygroundPlayer[] players = System.Array.Empty<PlaygroundPlayer>();
        private float nextPlayerScan;
        private Transform leverHand;
        // Trekhendel (cabine v3): schuift langs de verticale as van de loc door de sleuf in de lessenaar.
        // Boven = vooruit, midden = neutraal, onder = achteruit. Moet gelijk zijn aan LEVER_TRAVEL in build_locomotive.py.
        private const float LeverTravel = 0.19f;
        private float leverOffset, previousHandHeight;
        private Vector3 leverRestPosition, leverSlideAxis;
        private Vector3 leverPosition, leverScale;
        private Collider[] leverColliders;
        private bool[] leverColliderEnabled;
        private readonly Collider[] buttonContacts = new Collider[16];
        private float nextButtonPress;
        private Transform startButton;
        private Renderer startButtonRenderer;
        private MaterialPropertyBlock buttonOriginalProperties, buttonProperties;
        private Light startButtonLight;
        private bool? buttonLit;
        private Transform speedNeedle, heatNeedle, cabLamp;
        private float displayedHeat;
        private Light cabLampLight;
        private Transform fireboxDoor, fireboxLatch, doorHand;
        private Rigidbody doorBody;
        private SphereCollider latchCollider;
        private Quaternion doorRest;
        private Vector3 previousDoorHand;
        private float doorOpeningSign;
        private Transform shovel, shovelLoad, coalScoopPoint, fireboxFeedPoint, fireboxFlame;
        private Transform shovelParent;
        private Rigidbody shovelBody;
        private SphereCollider shovelGrip;
        private BoxCollider shovelBlade, addedCoalBinCollider;
        private readonly BoxCollider[] shovelRims = new BoxCollider[3];
        private Collider[] originalShovelColliders;
        private bool[] originalShovelColliderStates;
        private Transform coalBin, smokeOrigin;
        // Waar de schepkolen liggen: sinds cabine v3 de 'CoalStock' op de kolenwagen (oudere modellen: 'CoalBin' in de loc).
        // De thuisposities van de brokken staan in de ruimte van dat voertuig.
        private Transform coalSource;
        private Rigidbody coalSourceBody;
        private MeshFilter cabin;
        private TrainSteamEffects steamEffects;
        private readonly List<Rigidbody> coalBodies = new();
        private readonly List<Vector3> coalHomes = new();
        private readonly List<Quaternion> coalHomeRotations = new();
        private readonly List<float> coalRespawnTimes = new();
        // Losse kolen in de tenderbak: brokken die stil liggen 'slapen' (kinematisch, als kind van de wagen), zodat ze
        // vast meerijden, niet trillen en geen netwerkverkeer kosten. Een schep, hand of speler in de buurt
        // maakt ze wakker (gewone physics); tot rust gekomen in de bak gaan ze weer slapen.
        private readonly List<bool> coalAsleep = new();
        private readonly List<float> coalCalmTime = new();
        private readonly List<float> coalRadius = new();
        private readonly Dictionary<Rigidbody, int> coalIndex = new();
        private readonly List<Bounds> coalDisturbers = new();
        private readonly Collider[] binContacts = new Collider[384];
        private Bounds coalBinLocal;
        // Kolenposes zijn relatief aan het voertuig met de kolen (coalSource): stil liggende kolen veranderen dan niet.
        private NetworkList<CoalPose> coalPoses;
        private static readonly HashSet<Rigidbody> GrippableCoal = new();
        private static readonly Dictionary<Rigidbody, TrainDrive> ControlOwners = new();
        private readonly WagonDoorPanel[] wagonDoors = new WagonDoorPanel[2];
        private const float WagonDoorTravel = 0.56f;
        private const float WagonDoorLeadIn = 0.08f;
        private const float WagonDoorInset = 0.05f;

        private sealed class WagonDoorPanel
        {
            public Transform Leaf, Hand;
            public Rigidbody Body;
            public BoxCollider Panel;
            public SphereCollider Handle;
            public Collider[] OriginalColliders;
            public bool[] OriginalEnabled;
            public Vector3 RestPosition;
            public float PreviousHandX;
            public bool OpensNegative;
        }

        // Positie/rotatie relatief aan het voertuig waar de kolen bij horen (coalSource), niet in wereldruimte.
        private struct CoalPose : INetworkSerializable, System.IEquatable<CoalPose>
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public bool Visible;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Position);
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref Visible);
            }
            public bool Equals(CoalPose other) => Visible == other.Visible && Position == other.Position && Rotation == other.Rotation;
        }

        private void Awake()
        {
            coalPoses = new NetworkList<CoalPose>();
        }
        internal static bool IsCoalBody(Rigidbody target) => target && GrippableCoal.Contains(target);
        internal static TrainDrive FindControlOwner(Rigidbody target) =>
            target && ControlOwners.TryGetValue(target, out var owner) ? owner : target ? target.GetComponentInParent<TrainDrive>() : null;

        internal bool TryGetCabin(Vector3 playerPosition, float margin, out Transform frame)
        {
            frame = null;
            if (!IsSpawned || !cabin || !cabin.sharedMesh) return false;
            Bounds bounds = cabin.sharedMesh.bounds;
            bounds.Expand(margin * 2f);
            if (!bounds.Contains(cabin.transform.InverseTransformPoint(playerPosition + transform.up * 0.15f))) return false;
            frame = cabin.transform;
            return true;
        }

        internal Vector3 ClampCabinEye(Vector3 eye)
        {
            Bounds bounds = cabin.sharedMesh.bounds;
            Vector3 margin = new Vector3(0.24f, 0.12f, 0.24f);
            Vector3 min = bounds.min + margin;
            Vector3 max = bounds.max - margin;
            return new Vector3(Mathf.Clamp(eye.x, min.x, max.x), Mathf.Clamp(eye.y, min.y, max.y),
                Mathf.Clamp(eye.z, min.z, max.z));
        }
        private Transform shovelHand;
        private bool shovelHeld;
        private Vector3 previousShovelHand, shovelReleaseVelocity;
        private Collider[] shovelHolderColliders;
        private Vector3 shovelRestPosition;
        private Quaternion shovelRestRotation, shovelBladeRotation;
        private Vector3 shovelStartPosition;
        private Quaternion shovelStartRotation;
        private Light firelight;
        private bool canStoke;
        internal bool HasCoalFurnace => IsSpawned && canStoke;
        internal float FuelFraction => fuel.Value / FuelCapacity;
        private Quaternion leverRest;
        private float railCenterOffset, rearwardRailSign;
        private const float CouplingRange = 1.05f;
        private readonly Dictionary<TrackPiece, TrackPiece> previous = new();
        private readonly Dictionary<TrackPiece, TrackPiece> following = new();
        private TrackPiece[] railPieces = System.Array.Empty<TrackPiece>();
        private readonly RaycastHit[] railObstacleHits = new RaycastHit[32];
        private readonly Dictionary<Rigidbody, LooseWagon> looseWagons = new();
        private readonly List<Rigidbody> coupledWagons = new();
        private readonly Dictionary<Rigidbody, float> couplingGaps = new();
        private readonly Dictionary<Rigidbody, CouplerLayout> couplers = new();
        private readonly HashSet<Rigidbody> reversedWagons = new();
        private readonly List<MeshCollider> derailColliders = new();
        private readonly List<BoxCollider> derailBoxes = new();
        private readonly List<Joint> derailJoints = new();
        private PhysicsMaterial derailMaterial;
        private float originalTrainMass;
        private bool derailed;

        private sealed class LooseWagon
        {
            public TrackPiece Piece;
            public float Along, Speed;
            public bool Reversed;
            public Vector3 Offset;
            public readonly Dictionary<PlaygroundPlayer, Vector3> Pushes = new();
        }

        private readonly struct CouplerLayout
        {
            public readonly Vector3 Positive, Negative;
            public readonly float FrontSign;
            public CouplerLayout(Vector3 front, Vector3 rear)
            {
                bool frontIsPositive = front.x > rear.x;
                Positive = frontIsPositive ? front : rear;
                Negative = frontIsPositive ? rear : front;
                FrontSign = frontIsPositive ? 1f : -1f;
            }
            public float Center => (Positive.z + Negative.z) * 0.5f;
        }

        public void Configure(TrackPiece first, float start) { firstTrack = first; startDistance = start; }
        public void ConfigureWagons(Rigidbody[] wagons) => availableWagons = wagons;
        internal bool IsCabControlCollider(Collider candidate) =>
            candidate && (candidate == latchCollider || IsShovelGrip(candidate) || IsLeverHandle(candidate) ||
                (wagonDoors[0] != null && candidate == wagonDoors[0].Handle) ||
                (wagonDoors[1] != null && candidate == wagonDoors[1].Handle));

        internal bool IsLeverHandle(Collider candidate) => leverPivot && candidate && candidate.transform == leverPivot;
        internal bool IsShovelGrip(Collider candidate) => candidate && (candidate == shovelGrip || candidate.transform == shovelLoad);

        internal bool TryClaimCabControl(Collider candidate, Transform hand)
        {
            if (!IsShovelGrip(candidate)) return true;
            if (shovelHand && shovelHand != hand) return false;
            shovelHand = hand;
            return true;
        }

        internal void ReleaseCabControl(Collider candidate, Transform hand)
        {
            if (IsShovelGrip(candidate) && shovelHand == hand) shovelHand = null;
        }

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            originalTrainMass = body.mass;
            derailed = false;
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (IsServer) { powered.Value = false; throttle.Value = 0f; displayedSpeed.Value = 0f; doorAngle.Value = 0f; wagonDoorOffsets.Value = Vector2.zero; speed = 0f; buttonDown.Value = false; nextButtonPress = 0f; }
            coalBin = coalScoopPoint = coalSource = null;
            coalSourceBody = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Regulator") { lever = t; leverRest = t.localRotation; }
                if (t.name == "HeatNeedle") heatNeedle = t;
                if (t.name == "CabLamp") cabLamp = t;
                if (t.name == "StartButton") startButton = t;
                if (t.name == "SpeedNeedle") speedNeedle = t;
                if (t.name == "FireboxDoor") fireboxDoor = t;
                if (t.name == "FireboxLatch") fireboxLatch = t;
                if (t.name == "CoalShovel") shovel = t;
                if (t.name == "CoalBin" || t.name == "CoalStock") coalBin = t;
                if (t.name == "Cab") cabin = t.GetComponent<MeshFilter>();
                if (t.name == "ChimneySmoke") smokeOrigin = t;
                if (t.name == "ShovelLoad") shovelLoad = t;
                if (t.name == "CoalScoopPoint") coalScoopPoint = t;
                if (t.name == "FireboxFeedPoint") fireboxFeedPoint = t;
                if (t.name == "FireboxFlame") fireboxFlame = t;
            }
            CreateCabControls();
            if (cabLamp)
            {
                var glow = new GameObject("Cabinelamp licht");
                glow.transform.SetParent(cabLamp, false);
                glow.transform.localPosition = Vector3.down * 0.11f;
                cabLampLight = glow.AddComponent<Light>();
                cabLampLight.type = LightType.Point;
                cabLampLight.color = new Color(1f, 0.925f, 0.63f);
                cabLampLight.range = 2.4f;
                cabLampLight.intensity = 0.6f;
                cabLampLight.shadows = LightShadows.None;
            }
            if (startButton && startButton.TryGetComponent<Renderer>(out startButtonRenderer))
            {
                buttonOriginalProperties = new MaterialPropertyBlock();
                startButtonRenderer.GetPropertyBlock(buttonOriginalProperties);
                buttonProperties = new MaterialPropertyBlock();
                startButtonRenderer.GetPropertyBlock(buttonProperties);
                var lamp = new GameObject("Startknop licht");
                lamp.transform.SetParent(startButton, false);
                lamp.transform.localPosition = Vector3.left * 0.08f;
                startButtonLight = lamp.AddComponent<Light>();
                startButtonLight.type = LightType.Point;
                startButtonLight.color = new Color(0.988f, 0.486f, 0.412f);
                startButtonLight.range = 0.55f;
                startButtonLight.intensity = 0.8f;
                buttonLit = null;
            }
            if (fireboxDoor && fireboxLatch)
            {
                doorRest = fireboxDoor.localRotation;
                var latchOffset = fireboxDoor.InverseTransformPoint(fireboxLatch.position);
                doorOpeningSign = Mathf.Sign(latchOffset.x * latchOffset.z);
                doorBody = fireboxDoor.gameObject.AddComponent<Rigidbody>();
                doorBody.isKinematic = true;
                doorBody.useGravity = false;
                latchCollider = fireboxLatch.gameObject.AddComponent<SphereCollider>();
                latchCollider.radius = 0.085f;
            }
            PrepareStoking();
            if (smokeOrigin && coalBodies.Count > 0)
            {
                var steam = new GameObject("Stoom en motorgeluid");
                steam.transform.SetParent(smokeOrigin, false);
                steamEffects = steam.AddComponent<TrainSteamEffects>();
                var puff = coalBodies[0].GetComponent<MeshFilter>().sharedMesh;
                Material puffMaterial = coalBodies[0].GetComponent<Renderer>().sharedMaterial;
                foreach (var part in GetComponentsInChildren<Renderer>(true))
                    if (part.name == "FireboxCoals") { puffMaterial = part.sharedMaterial; break; }
                Vector3 puffSize = puff ? puff.bounds.size : Vector3.one * 0.137f;
                steamEffects.Configure(puff, puffMaterial, 0.137f / Mathf.Max(0.01f, (puffSize.x + puffSize.y + puffSize.z) / 3f));
            }
            couplers.Clear();
            couplers.Add(body, GetCouplers(transform));
            railCenterOffset = couplers[body].Center;
            rearwardRailSign = couplers[body].FrontSign;
            foreach (var wagon in availableWagons)
                if (wagon && wagon != body) couplers[wagon] = GetCouplers(wagon.transform);
            PrepareWagonDoors();
            if (!IsServer) return;
            BuildRailLinks();
            foreach (var wagon in availableWagons)
                if (wagon && wagon != body) looseWagons.TryAdd(wagon, new LooseWagon());
            track = firstTrack;
            if (track) distance = Mathf.Clamp(startDistance, 0f, track.Length);
            if (!RailUsable(track))
                Debug.LogError("TrainDrive: configure a connected starting track.", this);
            else
            {
                if (TryRailPose(track, distance, railCenterOffset, false, out var position, out var rotation))
                {
                    body.position = position;
                    body.rotation = rotation;
                    if (canStoke)
                    {
                        shovelBody.position = body.position + body.rotation * shovelStartPosition;
                        shovelBody.rotation = body.rotation * shovelStartRotation;
                    }
                }
            }
        }

        private void Update()
        {
            if (!IsServer && leverPivot) leverPivot.localPosition = leverRestPosition + leverSlideAxis * (throttle.Value * LeverTravel);
            if (startButton) startButton.localScale = new Vector3(buttonDown.Value ? 0.6f : 1f, 1f, 1f);
            if (startButtonRenderer && buttonLit != powered.Value)
            {
                buttonProperties.SetFloat("_Emissive", powered.Value ? 1f : 0f);
                startButtonRenderer.SetPropertyBlock(buttonProperties);
                if (startButtonLight) startButtonLight.enabled = powered.Value;
                buttonLit = powered.Value;
            }
            if (speedNeedle)
            {
                float sweep = 240f * Mathf.Clamp01(displayedSpeed.Value / Mathf.Max(maximumSpeed, 0.01f));
                speedNeedle.localRotation = Quaternion.Euler(sweep - (fireboxLatch ? 0f : 120f), 0f, 0f);
            }
            if (heatNeedle)
            {
                // Hittemeter: de brandstof in de oven (0 = koud links, vol = rode zone rechts), zelfde zwaai als SpeedNeedle.
                float heat = IsSpawned && canStoke ? Mathf.Clamp01(fuel.Value / FuelCapacity) : 0f;
                displayedHeat = Mathf.MoveTowards(displayedHeat, heat, 0.6f * Time.deltaTime);
                heatNeedle.localRotation = Quaternion.Euler(240f * displayedHeat, 0f, 0f);
            }
            if (!IsServer && fireboxDoor && fireboxLatch) ApplyDoorAngle();
            if (IsSpawned && !IsServer) ApplyWagonDoors();
            if (IsSpawned && canStoke) PresentStoking();
            if (steamEffects) steamEffects.Present(powered.Value, displayedSpeed.Value / Mathf.Max(maximumSpeed, 0.01f));
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            UpdateCabControls();
            if (derailed)
            {
                displayedSpeed.Value = body.linearVelocity.magnitude;
                return;
            }
            TryCoupleWagon();
            foreach (var wagon in coupledWagons)
                if (wagon) couplingGaps[wagon] = Mathf.MoveTowards(couplingGaps[wagon], 0f, 0.4f * Time.fixedDeltaTime);
            MoveLooseWagons();
            if (!RailUsable(track))
            {
                displayedSpeed.Value = 0f;
                return;
            }
            bool canDrive = powered.Value && (!canStoke || fuel.Value > 0f);
            if (canDrive && canStoke)
                fuel.Value = Mathf.Max(0f, fuel.Value - Mathf.Abs(throttle.Value) * Time.fixedDeltaTime / 35f);
            speed = Mathf.MoveTowards(speed, canDrive ? throttle.Value * maximumSpeed : 0f, acceleration * Time.fixedDeltaTime);
            var oldTrack = track;
            float oldDistance = distance;
            float remaining = speed * Time.fixedDeltaTime;
            bool reachedEnd = !AdvanceOnRail(ref track, ref distance, remaining);
            bool towardTail = remaining * rearwardRailSign > 0f;
            float leadingEnd = Mathf.Sign(remaining) * (towardTail ? ConsistRearExtent() : FrontExtent(body));
            bool wagonsFit = coupledWagons.Count == 0 || CanPlaceWagons(track, distance);
            bool blocked = Mathf.Abs(remaining) > 0.0001f && RailMovementBlocked(body, oldTrack, oldDistance, track, distance, leadingEnd);
            if ((reachedEnd || (towardTail && !wagonsFit)) && !blocked && Mathf.Abs(speed) > 0.01f)
            {
                var endTrack = track;
                float endDistance = distance;
                if (towardTail && !wagonsFit)
                    AdvanceOnRail(ref endTrack, ref endDistance, ConsistRearExtent() * rearwardRailSign);
                if (endTrack && !endTrack.name.Contains("BufferStop"))
                {
                    SampleRail(oldTrack, oldDistance, out _, out var direction);
                    Derail(direction * speed);
                    return;
                }
            }
            if (reachedEnd) speed = 0f;
            if (!TryRailPose(track, distance, railCenterOffset, false, out var target, out var rotation) ||
                blocked || !wagonsFit)
            {
                track = oldTrack;
                distance = oldDistance;
                speed = 0f;
                displayedSpeed.Value = 0f;
                PlaceWagons();
                return;
            }
            SampleRail(oldTrack, oldDistance, out var oldRailPosition, out _);
            SampleRail(track, distance, out var railPosition, out _);
            displayedSpeed.Value = Vector3.Distance(oldRailPosition, railPosition) / Time.fixedDeltaTime;
            body.MovePosition(target);
            body.MoveRotation(rotation);
            PlaceWagons();
        }

        private void Derail(Vector3 velocity)
        {
            derailed = true;
            powered.Value = false;
            throttle.Value = 0f;
            leverOffset = 0f;
            speed = 0f;
            if (canStoke)
                for (int i = 0; i < coalBodies.Count; i++)
                    if (coalBodies[i].gameObject.activeSelf) WakeCoal(i);
            body.mass = Mathf.Max(originalTrainMass, 8f);
            derailMaterial = new PhysicsMaterial("Derailment friction") { dynamicFriction = 0.02f, staticFriction = 0.02f, frictionCombine = PhysicsMaterialCombine.Minimum };
            Rigidbody tail = body;
            MakeDynamic(body, velocity);
            foreach (var wagon in coupledWagons)
            {
                if (!wagon) continue;
                MakeDynamic(wagon, velocity);
                var joint = wagon.gameObject.AddComponent<ConfigurableJoint>();
                joint.connectedBody = tail;
                joint.autoConfigureConnectedAnchor = false;
                var layout = couplers[wagon];
                joint.anchor = PositiveTowardHead(wagon) ? layout.Positive : layout.Negative;
                joint.connectedAnchor = tail.transform.InverseTransformPoint(wagon.transform.TransformPoint(joint.anchor));
                joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
                joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
                joint.breakForce = body.mass * Physics.gravity.magnitude * 0.75f;
                derailJoints.Add(joint);
                tail = wagon;
            }
        }

        private void MakeDynamic(Rigidbody vehicle, Vector3 velocity)
        {
            foreach (var shape in vehicle.GetComponentsInChildren<MeshCollider>())
            {
                if (shape.attachedRigidbody != vehicle || shape.convex || !shape.enabled || !shape.sharedMesh) continue;
                Bounds bounds = shape.sharedMesh.bounds;
                if (shape.transform == cabin?.transform)
                {
                    float floor = bounds.size.y * 0.2f;
                    float wall = Mathf.Min(bounds.size.z * 0.1f, 0.2f);
                    AddDerailBox(shape, new Vector3(bounds.center.x, bounds.min.y + floor * 0.5f, bounds.center.z),
                        new Vector3(bounds.size.x, floor, bounds.size.z));
                    for (int side = -1; side <= 1; side += 2)
                        AddDerailBox(shape, new Vector3(bounds.center.x, bounds.center.y, bounds.center.z + side * (bounds.extents.z - wall * 0.5f)),
                            new Vector3(bounds.size.x, bounds.size.y, wall));
                }
                else AddDerailBox(shape, bounds.center, bounds.size);
                shape.enabled = false;
                derailColliders.Add(shape);
            }
            vehicle.isKinematic = false;
            vehicle.useGravity = true;
            vehicle.linearVelocity = velocity;
        }

        private void AddDerailBox(MeshCollider shape, Vector3 center, Vector3 size)
        {
            var box = shape.gameObject.AddComponent<BoxCollider>();
            box.center = center;
            box.size = Vector3.Max(size, Vector3.one * 0.01f);
            box.sharedMaterial = derailMaterial;
            box.isTrigger = shape.isTrigger;
            derailBoxes.Add(box);
        }

        private static bool RailUsable(TrackPiece piece) =>
            piece && piece.isActiveAndEnabled && piece.HasSockets && piece.Length >= 0.01f && !piece.name.Contains("Broken");

        private void BuildRailLinks()
        {
            railPieces = FindObjectsByType<TrackPiece>();
            foreach (var piece in railPieces)
            {
                if (!piece.HasSockets || !piece.next) continue;
                following[piece] = piece.next;
                previous.TryAdd(piece.next, piece);
            }
            foreach (var piece in railPieces)
            {
                if (!RailUsable(piece) || following.ContainsKey(piece) || piece.name.Contains("BufferStop")) continue;
                TrackPiece nearest = null;
                float best = 0.25f * 0.25f;
                foreach (var candidate in railPieces)
                {
                    if (candidate == piece || previous.ContainsKey(candidate) || !RailUsable(candidate)) continue;
                    float gap = (piece.EndPosition - candidate.StartPosition).sqrMagnitude;
                    if (gap >= best || Vector3.Dot(piece.EndDirection, candidate.StartDirection) < 0.9f) continue;
                    nearest = candidate;
                    best = gap;
                }
                if (!nearest) continue;
                following.Add(piece, nearest);
                previous.Add(nearest, piece);
            }
        }

        private bool AdvanceOnRail(ref TrackPiece piece, ref float along, float delta)
        {
            for (int i = 0; i < 64 && RailUsable(piece); i++)
            {
                float length = piece.Length;
                float target = along + delta;
                if (target >= 0f && target <= length)
                {
                    along = target;
                    return true;
                }
                bool forward = target > length;
                var links = forward ? following : previous;
                if (!links.TryGetValue(piece, out var next) || !RailUsable(next))
                {
                    along = forward ? length : 0f;
                    return false;
                }
                delta = forward ? target - length : target;
                piece = next;
                along = forward ? 0f : next.Length;
            }
            return false;
        }

        private void SampleRail(TrackPiece piece, float along, out Vector3 position, out Vector3 direction)
        {
            float length = piece.Length;
            piece.Evaluate(along / length, out position, out direction);
            const float seam = 0.5f;
            // Meet halfway across a recovered socket gap so slow wagons never jump it in one tick.
            if (along < seam && previous.TryGetValue(piece, out var before) && before && before.next != piece)
            {
                float blend = Mathf.SmoothStep(0.5f, 0f, along / seam);
                position += (before.EndPosition - piece.StartPosition) * blend;
                direction = Vector3.Slerp(direction, before.EndDirection, blend);
            }
            else if (length - along < seam && following.TryGetValue(piece, out var after) && after && piece.next != after)
            {
                float blend = Mathf.SmoothStep(0.5f, 0f, (length - along) / seam);
                position += (after.StartPosition - piece.EndPosition) * blend;
                direction = Vector3.Slerp(direction, after.StartDirection, blend);
            }
        }

        private bool TryRailPose(TrackPiece piece, float along, float center, bool reversed,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            if (!RailUsable(piece)) return false;
            SampleRail(piece, along, out var rail, out var direction);
            if (direction.sqrMagnitude < 0.01f) return false;
            rotation = Quaternion.LookRotation(direction, piece.transform.up) *
                       Quaternion.Euler(0f, reversed ? -90f : 90f, 0f);
            position = rail + rotation * new Vector3(0f, TrackPiece.RailToTrainPivot, -center);
            return true;
        }

        private bool RailMovementBlocked(Rigidbody vehicle, TrackPiece from, float before, TrackPiece to, float after, float lead)
        {
            Vector3 start = RailProbePoint(from, before, lead, out var facing);
            Vector3 end = RailProbePoint(to, after, lead, out _);
            Vector3 move = end - start;
            float length = move.magnitude;
            if (length <= 0.0001f) return false;
            int count = Physics.BoxCastNonAlloc(start, new Vector3(1.05f, 0.55f, 0.06f), move / length,
                railObstacleHits, facing, length + 0.05f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var obstacle = railObstacleHits[i].collider;
                if (obstacle.transform.IsChildOf(vehicle.transform) || (vehicle == body && IsCoupledWagon(obstacle)) ||
                    obstacle is TerrainCollider || obstacle.GetComponentInParent<TrackPiece>() ||
                    obstacle.GetComponentInParent<PlaygroundPlayer>()) continue;
                return true;
            }
            return false;
        }

        private Vector3 RailProbePoint(TrackPiece piece, float along, float lead, out Quaternion facing)
        {
            var nosePiece = piece;
            float noseDistance = along;
            bool onTrack = AdvanceOnRail(ref nosePiece, ref noseDistance, lead);
            if (onTrack) { piece = nosePiece; along = noseDistance; }
            SampleRail(piece, along, out var rail, out var direction);
            if (direction.sqrMagnitude < 0.01f) direction = piece.StartDirection;
            facing = Quaternion.LookRotation(direction * Mathf.Sign(lead), piece.transform.up);
            return rail + facing * Vector3.up * 1.35f + (onTrack ? Vector3.zero : direction * lead);
        }

        private static Bounds ModelBounds(Transform root)
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
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

        private static CouplerLayout GetCouplers(Transform root)
        {
            Transform front = null, rear = null, chimney = null;
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                if (node.name == "Coupler_Front") front = node;
                if (node.name == "Coupler_Rear") rear = node;
                if (node.name == "ChimneySmoke") chimney = node;
            }
            if (front && rear)
                return new CouplerLayout(root.InverseTransformPoint(front.position), root.InverseTransformPoint(rear.position));

            // Legacy scene models still use their previous bounds-based spacing.
            Bounds bounds = ModelBounds(root);
            float center = bounds.center.z;
            foreach (var node in root.GetComponentsInChildren<Transform>())
                if (node.name == "Wheels" && node.TryGetComponent<Renderer>(out var wheels))
                {
                    center = root.InverseTransformPoint(wheels.bounds.center).z;
                    break;
                }
            var positive = new Vector3(bounds.max.x, 0.17f, center);
            var negative = new Vector3(bounds.min.x, 0.17f, center);
            return chimney && root.InverseTransformPoint(chimney.position).x < bounds.center.x
                ? new CouplerLayout(negative, positive) : new CouplerLayout(positive, negative);
        }

        private void TryCoupleWagon()
        {
            if (!RailUsable(track) || Mathf.Abs(speed) > 2.2f) return;
            var tail = coupledWagons.Count == 0 ? body : coupledWagons[coupledWagons.Count - 1];
            if (!tail || !couplers.TryGetValue(tail, out var tailLayout)) return;
            bool tailPositiveTowardHead = PositiveTowardHead(tail);
            Vector3 rear = tail.position + tail.rotation * (tailPositiveTowardHead ? tailLayout.Negative : tailLayout.Positive);
            Vector3 outward = tail.rotation * (tailPositiveTowardHead ? Vector3.left : Vector3.right);
            float tailExtent = ConsistRearExtent();
            Rigidbody nearest = null;
            bool nearestReversed = false;
            float nearestGap = 0f;
            float nearestDistance = CouplingRange * CouplingRange;
            foreach (var wagon in availableWagons)
            {
                if (!wagon || wagon == body || coupledWagons.Contains(wagon) || !couplers.TryGetValue(wagon, out var layout) ||
                    !wagon.TryGetComponent<NetworkObject>(out var networkWagon) || !networkWagon.IsSpawned) continue;
                float alignment = Vector3.Dot(outward, wagon.rotation * Vector3.right);
                if (Mathf.Abs(alignment) < 0.86f) continue;
                bool positiveTowardHead = alignment < 0f;
                bool reversed = positiveTowardHead != (rearwardRailSign > 0f);
                Vector3 socket = positiveTowardHead ? layout.Positive : layout.Negative;
                Vector3 front = wagon.position + wagon.rotation * socket;
                Vector3 gap = front - rear;
                float forwardGap = Vector3.Dot(gap, outward);
                if (gap.sqrMagnitude >= nearestDistance || forwardGap < -0.09f ||
                    (gap - forwardGap * outward).sqrMagnitude > 0.24f * 0.24f) continue;
                float spacing = Mathf.Max(0f, forwardGap);
                if (!WagonPose(track, distance, tailExtent + spacing + Mathf.Abs(socket.x), wagon, reversed, out var expected, out _) ||
                    (expected - wagon.position).sqrMagnitude > 0.50f * 0.50f) continue;
                nearest = wagon;
                nearestReversed = reversed;
                nearestGap = spacing;
                nearestDistance = gap.sqrMagnitude;
            }
            if (!nearest) return;
            coupledWagons.Add(nearest);
            couplingGaps.Add(nearest, nearestGap);
            if (nearestReversed) reversedWagons.Add(nearest);
            looseWagons.Remove(nearest);
        }

        internal void QueueWagonPush(Rigidbody wagon, PlaygroundPlayer player, Vector3 direction)
        {
            if (IsSpawned && IsServer && looseWagons.TryGetValue(wagon, out var motion))
                motion.Pushes.TryAdd(player, direction);
        }

        private void MoveLooseWagons()
        {
            foreach (var entry in looseWagons)
            {
                var wagon = entry.Key;
                var motion = entry.Value;
                if (!wagon || !wagon.TryGetComponent<NetworkObject>(out var net) || !net.IsSpawned ||
                    (motion.Pushes.Count == 0 && Mathf.Abs(motion.Speed) < 0.001f))
                {
                    motion.Pushes.Clear();
                    motion.Speed = 0f;
                    continue;
                }
                if (!RailUsable(motion.Piece) && !FindWagonRail(wagon, motion))
                {
                    motion.Pushes.Clear();
                    motion.Speed = 0f;
                    continue;
                }

                SampleRail(motion.Piece, motion.Along, out _, out var direction);
                float effort = 0f;
                var tangent = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                foreach (var push in motion.Pushes)
                    if (push.Key && push.Key.IsSpawned) effort += Vector3.Dot(push.Value, tangent);
                motion.Pushes.Clear();
                float requestedSpeed = Mathf.Clamp(effort * 0.20f, -0.65f, 0.65f);
                float acceleration = Mathf.Abs(requestedSpeed) > 0.001f ? 0.36f : 1.2f;
                motion.Speed = Mathf.MoveTowards(motion.Speed, requestedSpeed, acceleration * Time.fixedDeltaTime);
                float step = motion.Speed * Time.fixedDeltaTime;
                if (Mathf.Abs(step) < 0.0001f) continue;

                var piece = motion.Piece;
                float along = motion.Along;
                var layout = couplers[wagon];
                bool towardRear = (step > 0f) != motion.Reversed;
                float lead = Mathf.Sign(step) * (towardRear ? -layout.Negative.x : layout.Positive.x);
                if (!AdvanceOnRail(ref piece, ref along, step) ||
                    !TryRailPose(piece, along, layout.Center, motion.Reversed, out var position, out var rotation))
                {
                    motion.Speed = 0f;
                    continue;
                }
                var nose = piece;
                float noseDistance = along;
                if (!AdvanceOnRail(ref nose, ref noseDistance, lead) ||
                    RailMovementBlocked(wagon, motion.Piece, motion.Along, piece, along, lead))
                {
                    motion.Speed = 0f;
                    continue;
                }

                motion.Piece = piece;
                motion.Along = along;
                motion.Offset = Vector3.MoveTowards(motion.Offset, Vector3.zero, Mathf.Abs(step) * 0.5f);
                wagon.MovePosition(position + rotation * motion.Offset);
                wagon.MoveRotation(Quaternion.RotateTowards(wagon.rotation, rotation, 60f * Time.fixedDeltaTime));
            }
        }

        private bool FindWagonRail(Rigidbody wagon, LooseWagon motion)
        {
            motion.Piece = null;
            float center = couplers[wagon].Center;
            Vector3 railOrigin = wagon.position + wagon.rotation * new Vector3(0f, -TrackPiece.RailToTrainPivot, center);
            float nearest = 0.65f * 0.65f;
            foreach (var piece in railPieces)
            {
                if (!RailUsable(piece)) continue;
                float length = piece.Length;
                if ((railOrigin - piece.StartPosition).sqrMagnitude > (length + 0.65f) * (length + 0.65f)) continue;
                int segments = Mathf.CeilToInt(length * 2f);
                Vector3 start = piece.StartPosition;
                for (int i = 1; i <= segments; i++)
                {
                    piece.Evaluate(i / (float)segments, out var end, out _);
                    Vector3 chord = end - start;
                    float fraction = Mathf.Clamp01(Vector3.Dot(railOrigin - start, chord) / Mathf.Max(0.0001f, chord.sqrMagnitude));
                    float gap = (railOrigin - Vector3.Lerp(start, end, fraction)).sqrMagnitude;
                    start = end;
                    if (gap >= nearest) continue;
                    float progress = (i - 1 + fraction) / segments;
                    piece.Evaluate(progress, out _, out var direction);
                    float facing = Vector3.Dot(wagon.rotation * Vector3.right, direction);
                    if (Mathf.Abs(facing) < 0.85f) continue;
                    nearest = gap;
                    motion.Piece = piece;
                    motion.Along = progress * length;
                    motion.Reversed = facing > 0f;
                }
            }
            if (!TryRailPose(motion.Piece, motion.Along, center, motion.Reversed, out var position, out var rotation)) return false;
            motion.Offset = Quaternion.Inverse(rotation) * (wagon.position - position);
            return true;
        }

        private bool IsCoupledWagon(Collider collider)
        {
            foreach (var wagon in coupledWagons)
                if (wagon && collider.transform.IsChildOf(wagon.transform)) return true;
            return false;
        }

        private bool WagonPose(TrackPiece piece, float along, float offset, Rigidbody wagon, bool reversed,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = default;
            return AdvanceOnRail(ref piece, ref along, offset * rearwardRailSign) &&
                   TryRailPose(piece, along, couplers[wagon].Center, reversed, out position, out rotation);
        }

        // The FBX importer can mirror the model's X axis. The named locomotive mounts define which way is forward.
        private bool PositiveTowardHead(Rigidbody wagon) => (rearwardRailSign > 0f) != reversedWagons.Contains(wagon);

        private float FrontExtent(Rigidbody wagon) =>
            PositiveTowardHead(wagon) ? couplers[wagon].Positive.x : -couplers[wagon].Negative.x;

        private float RearExtent(Rigidbody wagon) =>
            PositiveTowardHead(wagon) ? -couplers[wagon].Negative.x : couplers[wagon].Positive.x;

        private float ConsistRearExtent()
        {
            float extent = RearExtent(body);
            foreach (var wagon in coupledWagons)
                if (wagon) extent += couplingGaps[wagon] + FrontExtent(wagon) + RearExtent(wagon);
            return extent;
        }

        private bool CanPlaceWagons(TrackPiece piece, float along)
        {
            float offset = RearExtent(body);
            foreach (var wagon in coupledWagons)
            {
                if (!wagon || !couplers.ContainsKey(wagon)) return false;
                offset += couplingGaps[wagon] + FrontExtent(wagon);
                if (!WagonPose(piece, along, offset, wagon, reversedWagons.Contains(wagon), out _, out _)) return false;
                offset += RearExtent(wagon);
            }
            return true;
        }

        private void PlaceWagons()
        {
            float offset = RearExtent(body);
            foreach (var wagon in coupledWagons)
            {
                if (!wagon) return;
                offset += couplingGaps[wagon] + FrontExtent(wagon);
                if (WagonPose(track, distance, offset, wagon, reversedWagons.Contains(wagon), out var position, out var rotation))
                {
                    wagon.MovePosition(position);
                    wagon.MoveRotation(rotation);
                }
                offset += RearExtent(wagon);
            }
        }

        private void UpdateCabControls()
        {
            if (Time.time >= nextPlayerScan)
            {
                players = FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None);
                nextPlayerScan = Time.time + 0.25f;
            }
            UpdateFireboxDoor();
            UpdateWagonDoors();
            UpdateShovel();
            UpdateCoal();
            if (!leverPivot || !startButton) return;
            PlaygroundPlayer operatorPlayer = null;
            Transform hand = null;
            Vector3 handPosition = Vector3.zero;
            foreach (var player in players)
                if (player && player.IsSpawned && player.TryGetControlHand(leverBody, out hand, out handPosition, useAim: true))
                { operatorPlayer = player; break; }
            if (hand)
            {
                // Omhoog/omlaag richten met de hand aan de greep schuift de hendel mee door de sleuf.
                float handHeight = Vector3.Dot(leverParent.InverseTransformPoint(handPosition), leverSlideAxis);
                if (leverHand == hand && operatorPlayer.HasCabControlInput)
                    leverOffset = Mathf.Clamp(leverOffset + Mathf.Clamp((handHeight - previousHandHeight) * 1.2f, -0.04f, 0.04f),
                        -LeverTravel, LeverTravel);
                previousHandHeight = handHeight;
            }
            leverHand = hand;
            leverPivot.localPosition = leverRestPosition + leverSlideAxis * leverOffset;
            float input = derailed ? 0f : leverOffset / LeverTravel;
            if (Mathf.Abs(input) < 0.06f) input = 0f;
            if (Mathf.Abs(input - throttle.Value) > 0.01f || (input == 0f && throttle.Value != 0f)) throttle.Value = input;

            bool touching = false, pressing = false;
            int count = Physics.OverlapSphereNonAlloc(startButton.position, 0.12f, buttonContacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider contact = buttonContacts[i];
                var player = contact.GetComponentInParent<PlaygroundPlayer>();
                if (!player || !player.IsSpawned || !player.IsHandCollider(contact)) continue;
                touching = true;
                Vector3 point = contact.bounds.center;
                Vector3 towardButton = startButton.position - point;
                var handBody = contact.attachedRigidbody;
                if (handBody && towardButton.sqrMagnitude > 0.0001f &&
                    Vector3.Dot(handBody.GetPointVelocity(point) - body.GetPointVelocity(point), towardButton.normalized) > 0.2f)
                    pressing = true;
            }
            bool pressed = touching && (buttonDown.Value || pressing);
            if (!derailed && pressed && !buttonDown.Value && Time.time >= nextButtonPress)
            {
                powered.Value = !powered.Value;
                nextButtonPress = Time.time + 0.35f;
            }
            if (buttonDown.Value != pressed) buttonDown.Value = pressed;
        }

        private void PrepareWagonDoors()
        {
            Debug.Assert(Mathf.Approximately(MoveWagonDoor(-WagonDoorTravel, -1f, true), -WagonDoorTravel));
            Debug.Assert(Mathf.Approximately(MoveWagonDoor(WagonDoorTravel, 1f, false), WagonDoorTravel));
            Debug.Assert(Mathf.Approximately(WagonDoorDisplacement(WagonDoorLeadIn * 0.5f).x, 0f));
            Debug.Assert(Mathf.Approximately(WagonDoorDisplacement(WagonDoorTravel).z, -WagonDoorInset));
            foreach (var wagon in availableWagons)
            {
                if (!wagon) continue;
                Transform left = null, right = null;
                foreach (var part in wagon.GetComponentsInChildren<Transform>(true))
                {
                    if (part.name == "DoorLeaf_L") left = part;
                    else if (part.name == "DoorLeaf_R") right = part;
                }
                if (!left || !right) continue;
                var leftMesh = left.GetComponent<MeshFilter>();
                var rightMesh = right.GetComponent<MeshFilter>();
                if (!leftMesh || !leftMesh.sharedMesh || !rightMesh || !rightMesh.sharedMesh ||
                    left.GetComponent<Rigidbody>() || right.GetComponent<Rigidbody>())
                {
                    Debug.LogWarning("TrainDrive: wagon door panels are missing meshes or already have rigidbodies.", wagon);
                    return;
                }
                float leftSide = left.localPosition.x - left.parent.InverseTransformPoint(right.position).x;
                float rightSide = right.localPosition.x - right.parent.InverseTransformPoint(left.position).x;
                if (Mathf.Abs(leftSide) < 0.01f || Mathf.Abs(rightSide) < 0.01f || Mathf.Sign(leftSide) == Mathf.Sign(rightSide))
                {
                    Debug.LogWarning("TrainDrive: wagon door panels do not have distinct sides.", wagon);
                    return;
                }
                wagonDoors[0] = CreateWagonDoor(left, leftMesh.sharedMesh.bounds, leftSide < 0f);
                wagonDoors[1] = CreateWagonDoor(right, rightMesh.sharedMesh.bounds, rightSide < 0f);
                ApplyWagonDoors();
                return;
            }
        }

        private WagonDoorPanel CreateWagonDoor(Transform leaf, Bounds bounds, bool opensNegative)
        {
            var panel = new WagonDoorPanel { Leaf = leaf, RestPosition = leaf.localPosition, OpensNegative = opensNegative };
            panel.OriginalColliders = leaf.GetComponents<Collider>();
            panel.OriginalEnabled = new bool[panel.OriginalColliders.Length];
            for (int i = 0; i < panel.OriginalColliders.Length; i++)
            {
                panel.OriginalEnabled[i] = panel.OriginalColliders[i].enabled;
                panel.OriginalColliders[i].enabled = false;
            }
            panel.Body = leaf.gameObject.AddComponent<Rigidbody>();
            panel.Body.isKinematic = true;
            panel.Body.useGravity = false;
            panel.Panel = leaf.gameObject.AddComponent<BoxCollider>();
            panel.Panel.center = bounds.center;
            panel.Panel.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.09f, bounds.size.z));
            panel.Handle = leaf.gameObject.AddComponent<SphereCollider>();
            panel.Handle.center = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, opensNegative ? 0.82f : 0.18f),
                Mathf.Lerp(bounds.min.y, bounds.max.y, 0.45f), bounds.max.z + 0.03f);
            panel.Handle.radius = 0.09f;
            ControlOwners[panel.Body] = this;
            return panel;
        }

        private void UpdateWagonDoors()
        {
            Vector2 offsets = wagonDoorOffsets.Value;
            for (int i = 0; i < wagonDoors.Length; i++)
            {
                var panel = wagonDoors[i];
                if (panel == null || !panel.Body) continue;
                Transform holdingHand = null;
                foreach (var player in players)
                {
                    if (!player || !player.IsSpawned || !player.TryGetControlHand(panel.Body, out var hand, out var position, useAim: true)) continue;
                    float handX = panel.Leaf.parent.InverseTransformPoint(position).x;
                    if (panel.Hand == hand)
                    {
                        float delta = handX - panel.PreviousHandX;
                        if (i == 0) offsets.x = MoveWagonDoor(offsets.x, delta, panel.OpensNegative);
                        else offsets.y = MoveWagonDoor(offsets.y, delta, panel.OpensNegative);
                    }
                    panel.PreviousHandX = handX;
                    holdingHand = hand;
                    break;
                }
                panel.Hand = holdingHand;
            }
            if (offsets != wagonDoorOffsets.Value) wagonDoorOffsets.Value = offsets;
            ApplyWagonDoors();
        }

        private static float MoveWagonDoor(float offset, float delta, bool opensNegative) =>
            Mathf.Clamp(offset + Mathf.Clamp(delta, -0.06f, 0.06f), opensNegative ? -WagonDoorTravel : 0f, opensNegative ? 0f : WagonDoorTravel);

        private static Vector3 WagonDoorDisplacement(float offset)
        {
            float distance = Mathf.Abs(offset);
            float slide = Mathf.Max(0f, distance - WagonDoorLeadIn) * WagonDoorTravel / (WagonDoorTravel - WagonDoorLeadIn);
            return new Vector3(Mathf.Sign(offset) * slide, 0f, -WagonDoorInset * Mathf.Clamp01(distance / WagonDoorLeadIn));
        }

        private void ApplyWagonDoors()
        {
            Vector2 offsets = wagonDoorOffsets.Value;
            for (int i = 0; i < wagonDoors.Length; i++)
                if (wagonDoors[i] != null && wagonDoors[i].Leaf)
                    wagonDoors[i].Leaf.localPosition = wagonDoors[i].RestPosition + WagonDoorDisplacement(i == 0 ? offsets.x : offsets.y);
        }

        private void UpdateFireboxDoor()
        {
            if (!doorBody) return;
            foreach (var player in players)
            {
                if (!player || !player.IsSpawned || !player.TryGetControlHand(doorBody, out var hand, out var position)) continue;
                Vector3 handDirection = fireboxDoor.parent.InverseTransformPoint(position) - fireboxDoor.localPosition;
                handDirection = Vector3.ProjectOnPlane(Quaternion.Inverse(doorRest) * handDirection, Vector3.up);
                if (handDirection.sqrMagnitude < 0.04f) break;
                if (doorHand == hand && player.HasCabControlInput)
                {
                    float delta = Vector3.SignedAngle(previousDoorHand, handDirection, Vector3.up) * doorOpeningSign;
                    doorAngle.Value = Mathf.Clamp(doorAngle.Value + Mathf.Clamp(delta, -6f, 6f), 0f, 100f);
                    ApplyDoorAngle();
                }
                doorHand = hand;
                previousDoorHand = handDirection;
                return;
            }
            doorHand = null;
        }

        private void ApplyDoorAngle() =>
            fireboxDoor.localRotation = doorRest * Quaternion.AngleAxis(doorAngle.Value * doorOpeningSign, Vector3.up);

        private void PrepareStoking()
        {
            canStoke = shovel && shovelLoad && fireboxFeedPoint && fireboxFlame && doorBody;
            if (!canStoke) return;
            if (!coalBin) FindCoalStockOnWagons();
            canStoke = coalBin && coalScoopPoint;
            if (!canStoke)
            {
                Debug.LogWarning("TrainDrive: geen schepkolen gevonden. Koppel de kolenwagen (Tender_Blockout met 'CoalStock') " +
                                 "via Tools > Conductor Chaos > Configure Train On Selected Track; stoken staat uit.", this);
                return;
            }
            coalSource = coalBin.IsChildOf(transform) ? transform : coalSourceBody ? coalSourceBody.transform : coalBin.root;
            if (coalSource == transform) coalSourceBody = body;
            var chunks = System.Array.FindAll(coalBin.GetComponentsInChildren<MeshFilter>(),
                mesh => mesh.name.StartsWith("CoalChunk", System.StringComparison.Ordinal));
            if (chunks.Length == 0) { canStoke = false; return; }
            System.Array.Sort(chunks, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (!coalBin.GetComponent<Collider>() && coalBin.TryGetComponent<MeshFilter>(out var binMesh))
            {
                addedCoalBinCollider = coalBin.gameObject.AddComponent<BoxCollider>();
                addedCoalBinCollider.center = binMesh.sharedMesh.bounds.center;
                addedCoalBinCollider.size = binMesh.sharedMesh.bounds.size;
            }

            if (IsServer) coalPoses.Clear();
            foreach (var chunk in chunks)
            {
                var piece = Instantiate(chunk.gameObject, chunk.transform.position, chunk.transform.rotation);
                piece.name = chunk.name + " (fysiek)";
                chunk.gameObject.SetActive(false);
                foreach (var existing in piece.GetComponents<Collider>()) existing.enabled = false;
                // Botsvorm = de convexe vorm van het brok (zoals bij het vullen van de bak in settle_coal.py), zodat de
                // brokken blijven liggen zoals ze liggen; zonder leesbare mesh een iets kleinere box.
                var mesh = chunk.sharedMesh;
                if (mesh && mesh.isReadable)
                {
                    var hull = piece.AddComponent<MeshCollider>();
                    hull.sharedMesh = mesh;
                    hull.convex = true;
                }
                else
                {
                    var box = piece.AddComponent<BoxCollider>();
                    box.center = mesh ? mesh.bounds.center : Vector3.zero;
                    box.size = mesh ? mesh.bounds.size * 0.85f : Vector3.one * 0.13f;
                }
                var coal = piece.AddComponent<Rigidbody>();
                coal.mass = 0.25f;
                coal.maxDepenetrationVelocity = 0.5f;
                coal.isKinematic = true;
                coal.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                // slapend in de bak: hangt aan de wagen en rijdt exact mee (ook op clients, die de pose lokaal volgen)
                piece.transform.SetParent(coalSource, true);
                int index = coalBodies.Count;
                if (IsServer)
                {
                    GrippableCoal.Add(coal);
                    coalPoses.Add(new CoalPose { Position = piece.transform.localPosition, Rotation = piece.transform.localRotation, Visible = true });
                }
                coalIndex[coal] = index;
                coalBodies.Add(coal);
                coalAsleep.Add(true);
                coalCalmTime.Add(0f);
                Vector3 extents = mesh ? Vector3.Scale(mesh.bounds.extents, piece.transform.lossyScale) : Vector3.one * 0.065f;
                coalRadius.Add(Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
                coalHomes.Add(piece.transform.localPosition);
                coalHomeRotations.Add(piece.transform.localRotation);
                coalRespawnTimes.Add(0f);
            }
            // gebied waarin kolen mogen slapen en waarin schep/handen/spelers ze wekken: de vulling + marge
            coalBinLocal = new Bounds(coalHomes[0], Vector3.zero);
            foreach (var home in coalHomes) coalBinLocal.Encapsulate(home);
            coalBinLocal.Expand(new Vector3(0.5f, 0.8f, 0.5f));

            shovelRestPosition = shovel.localPosition;
            shovelRestRotation = shovel.localRotation;
            shovelParent = shovel.parent;
            shovelStartPosition = transform.InverseTransformPoint(shovel.position);
            shovelStartRotation = Quaternion.Inverse(transform.rotation) * shovel.rotation;
            Vector3 bladeDirection = Vector3.ProjectOnPlane(shovelLoad.localPosition, Vector3.up);
            shovelBladeRotation = Quaternion.LookRotation(bladeDirection, Vector3.up);
            originalShovelColliders = shovel.GetComponentsInChildren<Collider>(true);
            originalShovelColliderStates = new bool[originalShovelColliders.Length];
            for (int i = 0; i < originalShovelColliders.Length; i++)
            {
                originalShovelColliderStates[i] = originalShovelColliders[i].enabled;
                originalShovelColliders[i].enabled = false;
            }
            shovel.SetParent(null, true);
            shovelBody = shovel.gameObject.AddComponent<Rigidbody>();
            shovelBody.mass = 0.4f;
            shovelBody.isKinematic = !IsServer;
            shovelBody.useGravity = IsServer;
            shovelBody.interpolation = RigidbodyInterpolation.Interpolate;
            ControlOwners[shovelBody] = this;
            shovelGrip = shovel.gameObject.AddComponent<SphereCollider>();
            shovelGrip.radius = 0.066f;
            shovelBlade = shovelLoad.gameObject.AddComponent<BoxCollider>();
            shovelBlade.center = new Vector3(0f, -0.03f, 0f);
            shovelBlade.size = new Vector3(0.38f, 0.032f, 0.38f);
            for (int i = 0; i < shovelRims.Length; i++)
            {
                var rim = shovelLoad.gameObject.AddComponent<BoxCollider>();
                rim.center = i == 2 ? new Vector3(0f, -0.0055f, 0.179f) : new Vector3(i == 0 ? -0.1775f : 0.1775f, 0.0095f, -0.0075f);
                rim.size = i == 2 ? new Vector3(0.38f, 0.049f, 0.022f) : new Vector3(0.025f, 0.079f, 0.355f);
                shovelRims[i] = rim;
            }
            var firelightObject = new GameObject("Vuurlicht");
            firelightObject.transform.SetParent(fireboxFlame, false);
            firelight = firelightObject.AddComponent<Light>();
            firelight.type = LightType.Point;
            firelight.color = new Color(1f, 0.49f, 0.18f);
            firelight.range = 0.9f;
            if (!IsServer) return;
            fuel.Value = StartingFuel;
            shovelPosition.Value = shovelBody.position;
            shovelRotation.Value = shovelBody.rotation;
        }

        private void FindCoalStockOnWagons()
        {
            foreach (var wagon in availableWagons)
            {
                if (!wagon || wagon == body) continue;
                Transform stock = null, scoop = null;
                foreach (var t in wagon.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "CoalStock" || t.name == "CoalBin") stock = t;
                    if (t.name == "CoalScoopPoint") scoop = t;
                }
                if (!stock || !scoop) continue;
                coalBin = stock;
                coalScoopPoint = scoop;
                coalSourceBody = wagon;
                return;
            }
        }

        private void CoalHome(int index, out Vector3 position, out Quaternion rotation)
        {
            // Rigidbody-pose als die er is: die is al bijgewerkt voordat de transform wordt gesynchroniseerd.
            if (coalSourceBody)
            {
                position = coalSourceBody.position + coalSourceBody.rotation * coalHomes[index];
                rotation = coalSourceBody.rotation * coalHomeRotations[index];
                return;
            }
            position = coalSource.TransformPoint(coalHomes[index]);
            rotation = coalSource.rotation * coalHomeRotations[index];
        }

        private void UpdateShovel()
        {
            if (!canStoke) return;
            foreach (var player in players)
            {
                if (!player || !player.IsSpawned || !player.TryGetControlHand(shovelBody, out var hand, out var handPosition) ||
                    hand != shovelHand) continue;
                if (!shovelHeld)
                {
                    shovelHeld = true;
                    shovelBody.isKinematic = true;
                    previousShovelHand = handPosition;
                    shovelReleaseVelocity = Vector3.zero;
                    shovelHolderColliders = player.GetComponentsInChildren<Collider>();
                    SetShovelHolderCollisions(true);
                }
                shovelReleaseVelocity = Vector3.ClampMagnitude((handPosition - previousShovelHand) / Time.fixedDeltaTime, 10f);
                previousShovelHand = handPosition;
                Quaternion rotation = Quaternion.LookRotation(player.CabAimForward, transform.up) * Quaternion.Inverse(shovelBladeRotation);
                shovelBody.MovePosition(handPosition);
                rotation = Quaternion.RotateTowards(shovelBody.rotation, rotation, 360f * Time.fixedDeltaTime);
                shovelBody.MoveRotation(rotation);
                shovelPosition.Value = handPosition;
                shovelRotation.Value = rotation;
                return;
            }
            if (shovelHeld)
            {
                SetShovelHolderCollisions(false);
                shovelHolderColliders = null;
                shovelHeld = false;
                shovelBody.isKinematic = false;
                shovelBody.linearVelocity = shovelReleaseVelocity;
            }
            shovelPosition.Value = shovelBody.position;
            shovelRotation.Value = shovelBody.rotation;
        }

        private void SetShovelHolderCollisions(bool ignore)
        {
            if (shovelHolderColliders == null) return;
            foreach (var part in shovelHolderColliders)
            {
                if (!part || !part.enabled) continue;
                if (shovelGrip) Physics.IgnoreCollision(part, shovelGrip, ignore);
                if (shovelBlade) Physics.IgnoreCollision(part, shovelBlade, ignore);
                foreach (var rim in shovelRims)
                    if (rim) Physics.IgnoreCollision(part, rim, ignore);
            }
        }

        private void UpdateCoal()
        {
            if (!canStoke) return;
            Vector3 mouth = Vector3.Lerp(fireboxFeedPoint.position, fireboxFlame.position, 0.42f);
            CollectCoalDisturbers();
            for (int i = 0; i < coalBodies.Count; i++)
            {
                var coal = coalBodies[i];
                if (!coal.gameObject.activeSelf)
                {
                    if (Time.time < coalRespawnTimes[i]) continue;
                    CoalHome(i, out var home, out _);
                    int overlaps = Physics.OverlapSphereNonAlloc(home, 0.05f, buttonContacts, ~0, QueryTriggerInteraction.Ignore);
                    bool occupied = false;
                    for (int j = 0; j < overlaps && !occupied; j++)
                        occupied = IsCoalBody(buttonContacts[j].attachedRigidbody);
                    if (occupied) continue;
                    coal.isKinematic = true;
                    coal.transform.SetParent(coalSource, false);
                    coal.transform.localPosition = coalHomes[i];
                    coal.transform.localRotation = coalHomeRotations[i];
                    coalAsleep[i] = true;
                    coalCalmTime[i] = 0f;
                    coal.gameObject.SetActive(true);
                }
                Vector3 position = coal.transform.position;
                bool nearHelper = false;
                if (coalAsleep[i])
                {
                    if (NearCoalDisturber(position, coalRadius[i] + 0.06f)) WakeCoal(i);
                }
                else
                {
                    nearHelper = NearCoalDisturber(position, coalRadius[i] + 0.12f);
                    Vector3 local = Quaternion.Inverse(SourceRotation()) * (coal.position - SourcePosition());
                    Vector3 relative = coal.linearVelocity - SourceVelocity(coal.position);
                    Vector3 spin = coal.angularVelocity - (coalSourceBody ? coalSourceBody.angularVelocity : Vector3.zero);
                    bool calm = !nearHelper && coalBinLocal.Contains(local) &&
                                relative.sqrMagnitude < 0.05f * 0.05f && spin.sqrMagnitude < 0.5f * 0.5f;
                    coalCalmTime[i] = calm ? coalCalmTime[i] + Time.fixedDeltaTime : 0f;
                    if (coalCalmTime[i] > 0.75f) SleepCoal(i);
                }
                bool inFire = doorAngle.Value >= 50f && fuel.Value < FuelCapacity - 0.05f &&
                              (coal.position - mouth).sqrMagnitude < 0.23f * 0.23f;
                Vector3 stock = coalSource.position;
                if (inFire || coal.position.y < stock.y - 20f || (coal.position - stock).sqrMagnitude > 10000f)
                {
                    if (inFire) fuel.Value = Mathf.Min(FuelCapacity, fuel.Value + 0.5f);
                    WakeCoal(i);
                    coal.gameObject.SetActive(false);
                    coalRespawnTimes[i] = Time.time + 2.5f;
                }
                var previousPose = coalPoses[i];
                var pose = new CoalPose { Visible = coal.gameObject.activeSelf };
                if (coalAsleep[i])
                {
                    pose.Position = coal.transform.localPosition;
                    pose.Rotation = coal.transform.localRotation;
                }
                else
                {
                    pose.Position = Quaternion.Inverse(SourceRotation()) * (coal.position - SourcePosition());
                    pose.Rotation = Quaternion.Inverse(SourceRotation()) * coal.rotation;
                }
                if (pose.Visible != previousPose.Visible || (pose.Position - previousPose.Position).sqrMagnitude > 0.0001f ||
                    Quaternion.Angle(pose.Rotation, previousPose.Rotation) > 2f)
                    coalPoses[i] = pose;
            }
        }

        private Vector3 SourcePosition() => coalSourceBody ? coalSourceBody.position : coalSource.position;
        private Quaternion SourceRotation() => coalSourceBody ? coalSourceBody.rotation : coalSource.rotation;
        private Vector3 SourceVelocity(Vector3 point) => coalSourceBody ? coalSourceBody.GetPointVelocity(point) : Vector3.zero;

        // Kolen wekken elkaar niet: anders verspreidt een kleine botsing zich door de hele stapel.
        private void CollectCoalDisturbers()
        {
            coalDisturbers.Clear();
            Vector3 center = coalSource.TransformPoint(coalBinLocal.center);
            Vector3 half = Vector3.Scale(coalBinLocal.extents, coalSource.lossyScale);
            int count = Physics.OverlapBoxNonAlloc(center, half, binContacts, coalSource.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
            {
                var shape = binContacts[k];
                var owner = shape.attachedRigidbody;
                if (!owner || owner == coalSourceBody || shape.transform.IsChildOf(coalSource) || coalIndex.ContainsKey(owner)) continue;
                coalDisturbers.Add(shape.bounds);
            }
        }

        private bool NearCoalDisturber(Vector3 point, float reach)
        {
            for (int k = 0; k < coalDisturbers.Count; k++)
            {
                if ((coalDisturbers[k].ClosestPoint(point) - point).sqrMagnitude >= reach * reach) continue;
                return true;
            }
            return false;
        }

        private void WakeCoal(int index)
        {
            if (!coalAsleep[index]) return;
            var coal = coalBodies[index];
            Vector3 position = coal.transform.position;
            coal.transform.SetParent(null, true);
            coal.isKinematic = false;
            coal.linearVelocity = SourceVelocity(position);
            coal.angularVelocity = Vector3.zero;
            coal.WakeUp();
            coalAsleep[index] = false;
            coalCalmTime[index] = 0f;
        }

        private void SleepCoal(int index)
        {
            var coal = coalBodies[index];
            coal.isKinematic = true;
            coal.transform.SetParent(coalSource, true);
            coalAsleep[index] = true;
            coalCalmTime[index] = 0f;
        }

        private void PresentStoking()
        {
            if (!IsServer)
            {
                float follow = 1f - Mathf.Exp(-24f * Time.deltaTime);
                shovel.SetPositionAndRotation(Vector3.Lerp(shovel.position, shovelPosition.Value, follow),
                    Quaternion.Slerp(shovel.rotation, shovelRotation.Value, follow));
                // kolen hangen op clients altijd aan hun wagen; de pose is relatief, dus rijden ze zonder naijlen mee
                for (int i = 0; i < coalBodies.Count && i < coalPoses.Count; i++)
                {
                    var coal = coalBodies[i].transform;
                    var pose = coalPoses[i];
                    if (!coal.gameObject.activeSelf && pose.Visible)
                    {
                        coal.localPosition = pose.Position;
                        coal.localRotation = pose.Rotation;
                    }
                    coal.gameObject.SetActive(pose.Visible);
                    if (!pose.Visible) continue;
                    coal.localPosition = Vector3.Lerp(coal.localPosition, pose.Position, follow);
                    coal.localRotation = Quaternion.Slerp(coal.localRotation, pose.Rotation, follow);
                }
            }
            float flameStrength = fuel.Value > 0f ? Mathf.Lerp(0.38f, 1f, Mathf.Clamp01(fuel.Value)) : 0f;
            fireboxFlame.gameObject.SetActive(flameStrength > 0f);
            float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 9f) + 0.07f * Mathf.Sin(Time.time * 17f);
            fireboxFlame.localScale = new Vector3(1f, flicker, 1f) * flameStrength;
            firelight.intensity = 1.1f * flameStrength * flicker;
        }

        private void CreateCabControls()
        {
            if (!lever || !startButton) { Debug.LogError("TrainDrive: de cabinehendel of startknop ontbreekt in het locomotiefmodel.", this); return; }
            leverParent = lever.parent;
            leverPosition = lever.localPosition;
            leverScale = lever.localScale;
            Vector3 pivotPosition = lever.position;
            leverColliders = lever.GetComponentsInChildren<Collider>(true);
            leverColliderEnabled = new bool[leverColliders.Length];
            for (int i = 0; i < leverColliders.Length; i++)
            {
                leverColliderEnabled[i] = leverColliders[i].enabled;
                leverColliders[i].enabled = false;
            }

            leverPivot = new GameObject("Cabinehendel fysica").transform;
            leverPivot.SetParent(leverParent, false);
            leverPivot.position = pivotPosition;
            leverPivot.localRotation = leverRest;
            leverRestPosition = leverPivot.localPosition;
            leverSlideAxis = leverParent.InverseTransformDirection(transform.up).normalized;
            leverOffset = previousHandHeight = 0f;
            lever.SetParent(leverPivot, false);
            lever.localPosition = Vector3.zero;
            lever.localRotation = Quaternion.identity;
            lever.localScale = leverScale;
            // Greep = de brede gele trekstang (pivot = hart van de greep); de capsule volgt de lengte van de stang.
            var handle = leverPivot.gameObject.AddComponent<CapsuleCollider>();
            handle.direction = 2;
            handle.height = 0.30f;
            if (lever.TryGetComponent<MeshFilter>(out var leverMesh) && leverMesh.sharedMesh)
            {
                Vector3 size = Vector3.Scale(leverMesh.sharedMesh.bounds.size, leverScale);
                handle.direction = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
                handle.height = size[handle.direction] + 0.04f;
            }
            handle.center = Vector3.zero;
            handle.radius = 0.06f;
            leverBody = leverPivot.gameObject.AddComponent<Rigidbody>();
            leverBody.mass = 0.15f;
            leverBody.useGravity = false;
            leverBody.isKinematic = true;
            leverBody.angularDamping = 0.75f;
            leverBody.interpolation = RigidbodyInterpolation.None;

        }

        public override void OnNetworkDespawn()
        {
            if (derailed)
            {
                if (body) { body.isKinematic = true; body.useGravity = false; }
                foreach (var wagon in coupledWagons)
                    if (wagon) { wagon.isKinematic = true; wagon.useGravity = false; }
                foreach (var joint in derailJoints)
                    if (joint) Destroy(joint);
                foreach (var box in derailBoxes)
                    if (box) Destroy(box);
                foreach (var shape in derailColliders)
                    if (shape) shape.enabled = true;
                if (body) body.mass = originalTrainMass;
                if (derailMaterial) Destroy(derailMaterial);
                derailMaterial = null;
                derailJoints.Clear();
                derailBoxes.Clear();
                derailColliders.Clear();
                derailed = false;
            }
            if (startButton) startButton.localScale = Vector3.one;
            if (startButtonRenderer && buttonOriginalProperties != null) startButtonRenderer.SetPropertyBlock(buttonOriginalProperties);
            if (startButtonLight) Destroy(startButtonLight.gameObject);
            startButtonRenderer = null;
            startButtonLight = null;
            if (speedNeedle) speedNeedle.localRotation = Quaternion.identity;
            if (heatNeedle) heatNeedle.localRotation = Quaternion.identity;
            displayedHeat = 0f;
            if (cabLampLight) Destroy(cabLampLight.gameObject);
            cabLampLight = null;
            if (fireboxDoor && fireboxLatch) fireboxDoor.localRotation = doorRest;
            if (latchCollider) Destroy(latchCollider);
            if (doorBody) Destroy(doorBody);
            for (int i = 0; i < wagonDoors.Length; i++)
            {
                var panel = wagonDoors[i];
                if (panel == null) continue;
                if (panel.Leaf) panel.Leaf.localPosition = panel.RestPosition;
                if (panel.Body) { ControlOwners.Remove(panel.Body); Destroy(panel.Body); }
                if (panel.Handle) Destroy(panel.Handle);
                if (panel.Panel) Destroy(panel.Panel);
                for (int j = 0; j < panel.OriginalColliders.Length; j++)
                    if (panel.OriginalColliders[j]) panel.OriginalColliders[j].enabled = panel.OriginalEnabled[j];
                wagonDoors[i] = null;
            }
            if (canStoke)
            {
                SetShovelHolderCollisions(false);
                shovelHolderColliders = null;
                shovelHeld = false;
                ControlOwners.Remove(shovelBody);
                if (shovelBody) shovelBody.isKinematic = true;
                shovel.SetParent(shovelParent, false);
                shovel.localPosition = shovelRestPosition;
                shovel.localRotation = shovelRestRotation;
                for (int i = 0; i < originalShovelColliders.Length; i++)
                    if (originalShovelColliders[i]) originalShovelColliders[i].enabled = originalShovelColliderStates[i];
                fireboxFlame.gameObject.SetActive(true);
                fireboxFlame.localScale = Vector3.one;
                Destroy(shovelGrip);
                Destroy(shovelBody);
                Destroy(firelight.gameObject);
                Destroy(shovelBlade);
                foreach (var rim in shovelRims)
                    if (rim) Destroy(rim);
                if (addedCoalBinCollider) Destroy(addedCoalBinCollider);
                foreach (var coal in coalBodies)
                {
                    GrippableCoal.Remove(coal);
                    if (coal) Destroy(coal.gameObject);
                }
                coalBodies.Clear();
                coalAsleep.Clear();
                coalCalmTime.Clear();
                coalRadius.Clear();
                coalIndex.Clear();
                coalDisturbers.Clear();
                coalHomes.Clear();
                coalHomeRotations.Clear();
                coalRespawnTimes.Clear();
                foreach (var chunk in coalBin.GetComponentsInChildren<MeshFilter>(true))
                    if (chunk.name.StartsWith("CoalChunk", System.StringComparison.Ordinal)) chunk.gameObject.SetActive(true);
            }
            if (steamEffects) steamEffects.Teardown();
            shovelHand = null;
            canStoke = false;
            coalSource = null;
            coalSourceBody = null;
            doorHand = null;
            if (lever && leverParent)
            {
                lever.SetParent(leverParent, false);
                lever.localPosition = leverPosition;
                lever.localRotation = leverRest;
                lever.localScale = leverScale;
            }
            if (leverColliders != null)
                for (int i = 0; i < leverColliders.Length; i++)
                    if (leverColliders[i]) leverColliders[i].enabled = leverColliderEnabled[i];
            if (leverPivot) Destroy(leverPivot.gameObject);
            players = System.Array.Empty<PlaygroundPlayer>();
            leverHand = null;
            leverOffset = previousHandHeight = 0f;
            nextPlayerScan = 0f;
            previous.Clear();
            following.Clear();
            railPieces = System.Array.Empty<TrackPiece>();
            looseWagons.Clear();
            coupledWagons.Clear();
            couplingGaps.Clear();
            couplers.Clear();
            reversedWagons.Clear();
        }
    }
}
