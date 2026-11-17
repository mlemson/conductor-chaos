// Conductor Chaos — gedeelde winterassets voor runtime-code (Resources/WinterAssets.asset).
using UnityEngine;

namespace ConductorChaos.Nature
{
    public sealed class WinterAssets : ScriptableObject
    {
        [Tooltip("Doorzichtig ijs om een bevroren reiziger.")]
        public Material iceShell;

        static WinterAssets instance;
        public static WinterAssets Instance => instance ? instance : instance = Resources.Load<WinterAssets>("WinterAssets");
    }
}
