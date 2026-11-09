using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Stations
{
    public sealed class ServiceCargo : ServiceObjective
    {
        [SerializeField] private Rigidbody[] pieces;
        [SerializeField] private BoxCollider destination;
        [SerializeField] private BoxCollider cargoBay;
        private readonly HashSet<Rigidbody> travelled = new();

        public override int Target => pieces?.Length ?? 0;
        public override bool IsConfigured => Target > 0 && destination && cargoBay && System.Array.TrueForAll(pieces, piece => piece);

        public override void ObserveTravelOnServer()
        {
            foreach (var piece in pieces)
                if (piece && ServiceStation.Contains(cargoBay, piece.worldCenterOfMass)) travelled.Add(piece);
        }

        public override int ReadProgress(bool trainStoppedAtDestination)
        {
            if (!trainStoppedAtDestination) return 0;
            int count = 0;
            foreach (var piece in pieces)
                if (piece && travelled.Contains(piece) && ServiceStation.Contains(destination, piece.worldCenterOfMass) &&
                    piece.linearVelocity.sqrMagnitude < 1f)
                    count++;
            return count;
        }
    }
}
