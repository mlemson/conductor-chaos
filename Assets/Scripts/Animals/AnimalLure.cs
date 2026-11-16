// Conductor Chaos — lokmiddel voor dieren (issue #236): een koe of schaap binnen lureRadius loopt erheen en blijft
// daar grazen. Zo krijg je een koppige koe van het spoor af. Zet het op een losse prop (de hooibaal krijgt het van de
// prefabbouwer, Conductor Chaos > Art > Animals).
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Animals
{
    /// <summary>Iets waar dieren op afkomen (hooibaal). Zet het op een losse prop; de dieren zoeken het zelf.</summary>
    public sealed class AnimalLure : MonoBehaviour
    {
        static readonly List<AnimalLure> All = new();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static AnimalLure Nearest(Vector3 from, float radius)
        {
            AnimalLure best = null;
            float bestSq = radius * radius;
            foreach (var lure in All)
            {
                float sq = (lure.transform.position - from).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = lure; }
            }
            return best;
        }
    }
}
