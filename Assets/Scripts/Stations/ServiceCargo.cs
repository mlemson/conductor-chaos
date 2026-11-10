using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Stations
{
    /// <summary>
    /// Counts real pieces of cargo, not trigger-enter events. The server remembers which
    /// crates have been in the wagon and which were seen aboard during the journey.
    /// </summary>
    public sealed class ServiceCargo : ServiceObjective
    {
        [SerializeField] private Rigidbody[] pieces;
        [SerializeField] private BoxCollider destination;
        [SerializeField] private BoxCollider cargoBay;
        private readonly HashSet<Rigidbody> loaded = new();
        private readonly HashSet<Rigidbody> travelled = new();
        private readonly Dictionary<Rigidbody, Collider[]> pieceColliders = new();

        public override int Target => pieces?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && destination && cargoBay &&
            System.Array.TrueForAll(pieces, piece => piece);
        public int LoadedCount => loaded.Count;
        public int TravelledCount => travelled.Count;

        public override void BeginOnServer()
        {
            loaded.Clear();
            travelled.Clear();
            pieceColliders.Clear();
            foreach (var piece in pieces)
                if (piece) pieceColliders[piece] = piece.GetComponentsInChildren<Collider>();
        }

        // Called while the crew loads the train, including before it begins moving.
        public void ObserveLoadingOnServer()
        {
            foreach (var piece in pieces)
                if (piece && Overlaps(cargoBay, piece)) loaded.Add(piece);
        }

        // Only the server observes transported cargo, and only while the train is moving.
        public override void ObserveTravelOnServer()
        {
            foreach (var piece in pieces)
            {
                if (!piece || !Overlaps(cargoBay, piece)) continue;
                loaded.Add(piece);
                travelled.Add(piece);
            }
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
                    collider.gameObject.activeInHierarchy && zoneBounds.Intersects(collider.bounds))
                    return true;
            return false;
        }
    }
}
