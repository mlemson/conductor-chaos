// Conductor Chaos — gladde grond (issue #243): op perrons en paden met ijs glijden spelers een beetje door. De speler
// past alleen zijn eigen versnelling aan (minder grip), dus de besturing blijft direct maar remt en draait trager.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class IceSurface : MonoBehaviour
    {
        [Tooltip("Hoeveel grip er overblijft (1 = gewoon, 0,35 = flink glad).")]
        [SerializeField, Range(.15f, 1f)] float grip = .4f;

        static readonly Dictionary<Collider, IceSurface> cache = new();

        /// <summary>Grip op deze ondergrond: 1 als het geen ijs is.</summary>
        public static float GripOn(Collider ground)
        {
            if (!ground) return 1f;
            if (!cache.TryGetValue(ground, out var ice))
            {
                ice = ground.GetComponentInParent<IceSurface>();
                if (cache.Count > 4096) cache.Clear();
                cache[ground] = ice;
            }
            return ice && ice.isActiveAndEnabled ? ice.grip : 1f;
        }

        void OnDestroy() => cache.Clear();
    }
}
