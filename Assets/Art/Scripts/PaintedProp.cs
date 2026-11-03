// Conductor Chaos — markering voor objecten van de Prop Painter (issue #35).
// Onthoudt hoe ver het object in (of boven) de ondergrond staat, zodat het na het boetseren van het terrein weer
// netjes op de grond gezet kan worden (PropGroundSnap in de editor). Geen gedrag tijdens het spelen.
using UnityEngine;

namespace ConductorChaos.Art
{
    [DisallowMultipleComponent]
    public class PaintedProp : MonoBehaviour
    {
        [Tooltip("Uit = blijft op zijn plek als de grond eronder verandert (bv. wolken, iets dat zweeft).")]
        public bool followGround = true;
        [Tooltip("Hoogte t.o.v. de ondergrond in meter (negatief = in de grond).")]
        public float groundOffset;
    }
}
