using System.Collections.Generic;
using UnityEngine;
using ConductorChaos.Playground;

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

        private readonly Dictionary<Rigidbody, Vector3> lastBayVelocity = new();
        private readonly RaycastHit[] supportHits = new RaycastHit[16];

        public override int Target => pieces?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && destination && cargoBay &&
            System.Array.TrueForAll(pieces, piece => piece);
        public int AboardCount => aboard.Count;
        public int TravelledCount => travelled.Count;
        public override bool ReadyToDepart => IsConfigured && aboard.Count == Target;

        public override void BeginOnServer()
        {
            aboard.Clear();
            travelled.Clear();
            pieceColliders.Clear();
            lastBayVelocity.Clear();
            foreach (var piece in pieces)
                if (piece) pieceColliders[piece] = piece.GetComponentsInChildren<Collider>();
        }

        // Called while the crew loads the train, including before it begins moving.
        public void ObserveLoadingOnServer()
        {
            aboard.Clear();
            var wagon = cargoBay ? TrainDrive.FindGuidedSupport(cargoBay.attachedRigidbody) : null;
            foreach (var piece in pieces)
            {
                if (!piece || !Overlaps(cargoBay, piece))
                {
                    if (piece) lastBayVelocity.Remove(piece);
                    continue;
                }
                aboard.Add(piece);
                CarryRestingCargo(piece, wagon);
            }
        }

        private void CarryRestingCargo(Rigidbody piece, Rigidbody wagon)
        {
            if (!wagon || piece.isKinematic || !HasWagonFloor(piece, wagon))
            {
                lastBayVelocity.Remove(piece);
                return;
            }
            foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                if (player.TryGetControlHand(piece, out _, out _, out _))
                {
                    lastBayVelocity.Remove(piece);
                    return;
                }

            Vector3 velocity = TrainDrive.GuidedBodyVelocity(wagon, piece.worldCenterOfMass);
            if (lastBayVelocity.TryGetValue(piece, out var previous) &&
                (piece.linearVelocity - previous).sqrMagnitude < 0.6f * 0.6f)
                piece.AddForce(Vector3.ClampMagnitude(velocity - previous, 12f * Time.fixedDeltaTime),
                    ForceMode.VelocityChange);
            lastBayVelocity[piece] = velocity;
        }

        private bool HasWagonFloor(Rigidbody piece, Rigidbody wagon)
        {
            if (!pieceColliders.TryGetValue(piece, out var colliders)) return false;
            Vector3 center = piece.worldCenterOfMass;
            float bottom = center.y;
            foreach (var collider in colliders)
                if (collider && collider.enabled && !collider.isTrigger)
                    bottom = Mathf.Min(bottom, collider.bounds.min.y);
            int count = Physics.RaycastNonAlloc(center, Vector3.down, supportHits, center.y - bottom + 0.10f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (supportHits[i].rigidbody == wagon && supportHits[i].normal.y > 0.55f) return true;
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
