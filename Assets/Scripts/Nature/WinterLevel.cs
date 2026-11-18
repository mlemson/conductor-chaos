// Conductor Chaos — winterlevel (issues #242, #243). Staat op één object in een winterscène (de levelbouwer zet het
// neer) en zet de kou aan: reizigers buiten de warmte krijgen het koud en kunnen bevriezen. De server rekent met deze
// instellingen (PlaygroundPassenger); clients lezen alleen wat de server doorgeeft.
using UnityEngine;

namespace ConductorChaos.Nature
{
    [DisallowMultipleComponent]
    public sealed class WinterLevel : MonoBehaviour
    {
        [Tooltip("Zoveel seconden buiten in de kou en een reiziger is bevroren.")]
        [SerializeField, Min(10f)] float freezeSeconds = 150f;
        [Tooltip("Zoveel seconden in de warmte en een bevroren reiziger is helemaal ontdooid.")]
        [SerializeField, Min(3f)] float thawSeconds = 25f;
        [Tooltip("In een wagon met een open deur koelt het zo veel langzamer af als buiten (0..1).")]
        [SerializeField, Range(0f, 1f)] float openDoorFactor = 1f;

        static WinterLevel active;

        public static WinterLevel Active => active && active.isActiveAndEnabled ? active : null;
        public float FreezeSeconds => freezeSeconds;
        public float ThawSeconds => thawSeconds;
        public float OpenDoorFactor => openDoorFactor;

        void OnEnable() => active = this;

        void OnDisable()
        {
            if (active == this) active = null;
        }
    }
}
