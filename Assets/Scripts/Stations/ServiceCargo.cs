using System.Collections.Generic;
using UnityEngine;
using ConductorChaos.Playground;
using Unity.Netcode.Components;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Counts pieces currently aboard and tracks which ones have ridden on the train.
    /// </summary>
    public sealed class ServiceCargo : ServiceObjective
    {
        [SerializeField] private Rigidbody[] pieces;
        [SerializeField] private BoxCollider destination;
        [SerializeField] private BoxCollider cargoBay;
        private readonly HashSet<Rigidbody> aboard = new();
        private readonly HashSet<Rigidbody> travelled = new();
        private readonly Dictionary<Rigidbody, Collider[]> pieceColliders = new();
        // Depot (#239): elke wagon (of de loc) telt; wat fysiek in of op de trein zit, reist mee.
        private TrainDrive train;
        private readonly Dictionary<Rigidbody, Bounds> trainVolumes = new();
        private readonly List<Rigidbody> trainBodies = new();

        private readonly RaycastHit[] supportHits = new RaycastHit[16];
        private Terrain[] routeTerrains;
        private float recoveredAt = float.NegativeInfinity;
        private bool IsLevelOneSuitcase => Target == 1 && gameObject.scene.name == ServiceRun.FirstLevelScene;

        public override int Target => pieces?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && destination && cargoBay &&
            System.Array.TrueForAll(pieces, piece => piece);
        internal bool UsesFreightWagon => Target > 1;
        internal void SetFreightBay(BoxCollider zone) => cargoBay = zone;
        internal void SetTrain(TrainDrive value)
        {
            train = value;
            trainVolumes.Clear();
        }
        public int AboardCount => aboard.Count;
        public int TravelledCount => travelled.Count;
        public override bool ReadyToDepart => IsConfigured && aboard.Count == Target;

        public override void BeginOnServer()
        {
            aboard.Clear();
            travelled.Clear();
            pieceColliders.Clear();
            recoveredAt = float.NegativeInfinity;
            routeTerrains = IsLevelOneSuitcase ? Terrain.activeTerrains : null;
            foreach (var piece in pieces)
            {
                if (!piece) continue;
                pieceColliders[piece] = piece.GetComponentsInChildren<Collider>();
                if (!piece.isKinematic && (IsLevelOneSuitcase || UsesFreightWagon))
                    piece.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        // Called while the crew loads the train, including before it begins moving.
        public void ObserveLoadingOnServer()
        {
            if (train)
            {
                ObserveTrainLoadingOnServer();
                return;
            }
            aboard.Clear();
            var wagon = cargoBay ? TrainDrive.FindGuidedSupport(cargoBay.attachedRigidbody) : null;
            foreach (var piece in pieces)
            {
                if (!piece) continue;
                if (wagon && IsLevelOneSuitcase && travelled.Contains(piece)) RecoverBelowTerrain(piece, wagon);
                bool onFloor = wagon && HasWagonFloor(piece, wagon);
                bool inBay = Overlaps(cargoBay, piece);
                if (inBay) aboard.Add(piece);
                if (onFloor && (inBay || IsLevelOneSuitcase)) CarryRestingCargo(piece, wagon);
            }
        }

        private void ObserveTrainLoadingOnServer()
        {
            aboard.Clear();
            CollectTrainBodies();
            Rigidbody fallback = RecoveryWagon();
            foreach (var piece in pieces)
            {
                if (!piece) continue;
                if (fallback && IsLevelOneSuitcase && travelled.Contains(piece)) RecoverBelowTerrain(piece, fallback);
                Rigidbody support = TrainFloorUnder(piece);
                if (support || InsideTrain(piece)) aboard.Add(piece);
                if (support) CarryRestingCargo(piece, support);
            }
        }

        private void CollectTrainBodies()
        {
            trainBodies.Clear();
            if (!train) return;
            if (train.LocomotiveBody) trainBodies.Add(train.LocomotiveBody);
            foreach (var wagon in train.CoupledWagons)
                if (wagon && !trainBodies.Contains(wagon)) trainBodies.Add(wagon);
        }

        // De vloer (of het dak) van een treindeel waar het stuk op rust.
        private Rigidbody TrainFloorUnder(Rigidbody piece)
        {
            if (!pieceColliders.TryGetValue(piece, out var colliders)) return null;
            Vector3 center = piece.worldCenterOfMass;
            float bottom = center.y;
            foreach (var collider in colliders)
                if (collider && collider.enabled && !collider.isTrigger)
                    bottom = Mathf.Min(bottom, collider.bounds.min.y);
            int count = Physics.SphereCastNonAlloc(center, .09f, Vector3.down, supportHits, center.y - bottom + .08f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody hit = supportHits[i].rigidbody;
                if (hit && trainBodies.Contains(hit) && Vector3.Dot(supportHits[i].normal, hit.rotation * Vector3.up) > .65f)
                    return hit;
            }
            return null;
        }

        // Binnen in een wagon (of erboven gedragen door iemand die op de wagon staat).
        private bool InsideTrain(Rigidbody piece)
        {
            Vector3 point = piece.worldCenterOfMass;
            foreach (var body in trainBodies)
            {
                Bounds volume = TrainVolume(body);
                if (volume.size.sqrMagnitude < .01f) continue;
                Vector3 local = body.transform.InverseTransformPoint(point);
                Vector3 min = volume.min, max = volume.max + Vector3.up * 1.4f;
                if (local.x >= min.x && local.x <= max.x && local.y >= min.y && local.y <= max.y &&
                    local.z >= min.z && local.z <= max.z) return true;
            }
            return false;
        }

        private Bounds TrainVolume(Rigidbody body)
        {
            if (trainVolumes.TryGetValue(body, out var cached)) return cached;
            var volume = new Bounds();
            bool any = false;
            foreach (var collider in body.GetComponentsInChildren<Collider>())
            {
                if (!collider || !collider.enabled || collider.isTrigger || collider.attachedRigidbody != body) continue;
                Bounds world = collider.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 p = new(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
                    Vector3 local = body.transform.InverseTransformPoint(world.min + Vector3.Scale(world.size, p));
                    if (!any) { volume = new Bounds(local, Vector3.zero); any = true; }
                    else volume.Encapsulate(local);
                }
            }
            trainVolumes[body] = volume;
            return volume;
        }

        private Rigidbody RecoveryWagon()
        {
            Rigidbody bay = cargoBay ? cargoBay.attachedRigidbody : null;
            if (bay && trainBodies.Contains(bay) && cargoBay.enabled) return bay;
            for (int i = trainBodies.Count - 1; i >= 0; i--)
                if (TrainVolume(trainBodies[i]).size.sqrMagnitude > .01f) return trainBodies[i];
            return null;
        }

        private void CarryRestingCargo(Rigidbody piece, Rigidbody wagon)
        {
            if (piece.isKinematic || piece.angularVelocity.sqrMagnitude > 16f || Held(piece)) return;

            Vector3 floorVelocity = TrainDrive.GuidedBodyVelocity(wagon, piece.worldCenterOfMass);
            Vector3 slip = floorVelocity - piece.linearVelocity;
            Vector3 up = wagon.rotation * Vector3.up;
            Vector3 horizontalSlip = Vector3.ProjectOnPlane(slip, up);
            // A shove, a hard curve or loss of floor contact can still send the luggage sliding.
            float grip = UsesFreightWagon ? 1.35f : 1f;
            if (horizontalSlip.sqrMagnitude > grip * grip || Mathf.Abs(Vector3.Dot(slip, up)) > .8f) return;
            // Give a supported bag the floor velocity, including on rising track, with bounded acceleration.
            float friction = UsesFreightWagon ? 9f : 7f;
            piece.AddForce(Vector3.ClampMagnitude(slip, friction * Time.fixedDeltaTime), ForceMode.VelocityChange);
        }

        private static bool Held(Rigidbody piece)
        {
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player && player.TryGetControlHand(piece, out _, out _, out _)) return true;
            return false;
        }

        private void RecoverBelowTerrain(Rigidbody piece, Rigidbody wagon)
        {
            Vector3 location = piece.position;
            if (routeTerrains == null || piece.isKinematic || location.y > wagon.position.y - 2.5f ||
                !piece.TryGetComponent<NetworkTransform>(out var network) || !network.IsSpawned || !network.CanCommitToTransform || Held(piece)) return;
            float terrainBase = float.PositiveInfinity;
            bool belowTerrain = false;
            foreach (var terrain in routeTerrains)
            {
                if (!terrain || !terrain.terrainData || terrain.gameObject.scene != gameObject.scene) continue;
                terrainBase = Mathf.Min(terrainBase, terrain.transform.position.y);
                Vector3 local = location - terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (local.x < 0f || local.x > size.x || local.z < 0f || local.z > size.z) continue;
                if (location.y > terrain.transform.position.y + terrain.SampleHeight(location) - 2.5f) return;
                belowTerrain = true;
            }
            if (!belowTerrain && (!float.IsFinite(terrainBase) || location.y >= terrainBase - 4f)) return;

            Vector3 safe;
            if (cargoBay && cargoBay.attachedRigidbody == wagon)
                safe = cargoBay.transform.TransformPoint(cargoBay.center + Vector3.down * .38f);
            else
            {
                Bounds volume = TrainVolume(wagon);
                safe = wagon.transform.TransformPoint(new Vector3(volume.center.x, volume.min.y + 1.6f, volume.center.z));
            }
            network.Teleport(safe, wagon.rotation, piece.transform.localScale);
            piece.position = safe;
            piece.rotation = wagon.rotation;
            piece.linearVelocity = TrainDrive.GuidedBodyVelocity(wagon, safe);
            piece.angularVelocity = Vector3.zero;
            recoveredAt = Time.time;
            Debug.Log("De koffer is teruggezet in de wagon na een val onder het speelveld.", piece);
        }

        internal byte SuitcaseStatus(bool travelling, out Vector3 position)
        {
            position = Vector3.zero;
            if (!IsLevelOneSuitcase || !pieces[0]) return 0;
            if (Time.time - recoveredAt < 5f) return 2;
            Rigidbody suitcase = pieces[0];
            if (!travelling || !travelled.Contains(suitcase) || aboard.Contains(suitcase)) return 0;
            position = suitcase.position;
            return 1;
        }

        private bool HasWagonFloor(Rigidbody piece, Rigidbody wagon)
        {
            if (!pieceColliders.TryGetValue(piece, out var colliders)) return false;
            Vector3 center = piece.worldCenterOfMass;
            float bottom = center.y;
            foreach (var collider in colliders)
                if (collider && collider.enabled && !collider.isTrigger)
                    bottom = Mathf.Min(bottom, collider.bounds.min.y);
            int count = Physics.SphereCastNonAlloc(center, .09f, Vector3.down, supportHits, center.y - bottom + .08f,
                ~0, QueryTriggerInteraction.Ignore);
            Vector3 up = wagon.rotation * Vector3.up;
            for (int i = 0; i < count; i++)
                if (supportHits[i].rigidbody == wagon && Vector3.Dot(supportHits[i].normal, up) > .65f) return true;
            return false;
        }

        // Only the server observes transported cargo, and only while the train is moving.
        public override void ObserveTravelOnServer()
        {
            travelled.UnionWith(aboard);
        }

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            // Report deliveries as they happen. The train's exact stopping location
            // determines when the level is WON, not whether a delivery is displayed.
            int count = 0;
            foreach (var piece in pieces)
                if (piece && travelled.Contains(piece) && Overlaps(destination, piece) &&
                    piece.linearVelocity.sqrMagnitude < 1f)
                    count++;
            return count;
        }

        private bool Overlaps(BoxCollider zone, Rigidbody piece)
        {
            if (!zone || !zone.enabled || !zone.gameObject.activeInHierarchy || !piece) return false;
            if (ServiceStation.Contains(zone, piece.worldCenterOfMass)) return true;

            // A crate can be visibly inside a marked area while its centre of mass is
            // just outside the invisible trigger (e.g. when resting on the platform).
            // Test its physical shape instead; this also works for crates already
            // resting inside the area when the level/observer starts.
            if (!pieceColliders.TryGetValue(piece, out var colliders))
            {
                colliders = piece.GetComponentsInChildren<Collider>();
                pieceColliders[piece] = colliders;
            }
            Bounds zoneBounds = zone.bounds;
            foreach (var collider in colliders)
                if (collider && collider.enabled && !collider.isTrigger &&
                    collider.gameObject.activeInHierarchy && zoneBounds.Intersects(collider.bounds) &&
                    Physics.ComputePenetration(zone, zone.transform.position, zone.transform.rotation,
                        collider, collider.transform.position, collider.transform.rotation, out _, out _))
                    return true;
            return false;
        }
    }
}
