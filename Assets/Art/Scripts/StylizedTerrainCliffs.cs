// Conductor Chaos — klif/grond-overgang via de bestaande Unity Terrain (issue #38).
// GEEN overlay-rotsmesh of opgeplakte graslip: de oorspronkelijke hoogtekaart en TerrainCollider
// blijven exact hetzelfde. Alleen een gereserveerde TerrainLayer wordt op steile hellingen gemengd.
// De andere, door de gebruiker geschilderde lagen behouden onderling hun verhouding,
// zodat het effect bij afvlakken weer omkeerbaar is. Geen netwerkstaat of colliderwijzigingen.
using System;
using UnityEngine;

namespace ConductorChaos.Art
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class StylizedTerrainCliffs : MonoBehaviour
    {
        [Header("Hellingtextuur")]
        [Tooltip("Alleen deze terreinlaag wordt automatisch geschilderd. Gebruik de aparte TL_CliffSlopeDirt, niet de handmatige TL_Dirt.")]
        public TerrainLayer slopeLayer;

        [Tooltip("Vanaf hoeveel procent helling grond zichtbaar wordt (30% = ongeveer 16,7 graden).")]
        [Range(0f, 400f)] public float startSlopePercent = 30f;

        [Tooltip("Hoeveel extra hellingsprocent nodig is voor vrijwel volledig grond. Bij start 30 en overgang 25 is dat ongeveer 55%.")]
        [Range(1f, 200f)] public float transitionSlopePercent = 25f;

        [Tooltip("Laat minimaal een beetje van de oorspronkelijke textuur over, zodat het omkeerbaar blijft.")]
        [Range(0.50f, 0.98f)] public float maxSlopeWeight = 0.98f;

        [Tooltip("Uit: alle steile hellingen. Aan: alleen binnen het geschilderde Cliff Painter-masker.")]
        public bool onlyPaintedCliffs = false;

        // Behoud de bestaande, geserialiseerde painter-data om huidige terreinen niet te breken.
        [SerializeField, HideInInspector] byte[] cliffMask;
        [SerializeField, HideInInspector] int cliffMaskRes;
        [SerializeField, HideInInspector] bool maskInitialised;

        public static bool IsBuilding { get; private set; }

        public void MarkCliff(int x, int z, int heightmapResolution)
        {
            EnsureMask(heightmapResolution);
            if (x < 0 || z < 0 || x >= cliffMaskRes || z >= cliffMaskRes) return;
            cliffMask[z * cliffMaskRes + x] = 1;
        }

        public void MarkRegion(RectInt region, int heightmapResolution)
        {
            EnsureMask(heightmapResolution);
            for (int z = Mathf.Max(0, region.yMin); z < Mathf.Min(cliffMaskRes, region.yMax); z++)
                for (int x = Mathf.Max(0, region.xMin); x < Mathf.Min(cliffMaskRes, region.xMax); x++)
                    cliffMask[z * cliffMaskRes + x] = 1;
        }

        public void EnsureMask(int heightmapResolution)
        {
            if (heightmapResolution <= 0) return;
            if (cliffMask == null || cliffMaskRes != heightmapResolution)
            {
                cliffMask = new byte[heightmapResolution * heightmapResolution];
                cliffMaskRes = heightmapResolution;
            }
            maskInitialised = true;
        }

        void OnEnable() => Rebuild();

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    Rebuild();
            };
        }
#endif

        [ContextMenu("Rebuild Slope Textures")]
        public void Rebuild() => Paint(null);

        // heightmapChanged geeft een rechthoek in hoogtekaartpixels: update alleen het
        // corresponderende alphamapgebied (plus marge) na een penseelstreek.
        public void Rebuild(RectInt changedHeightRegion) => Paint(changedHeightRegion);

        void Paint(RectInt? changedHeightRegion)
        {
            var terrain = GetComponentInParent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return;

            // Oude scènes kunnen nog het MeshFilter/MeshRenderer-paar van de klifoverlay hebben.
            // Schakel het expliciet uit, ook als de slope-layer op dit moment ontbreekt.
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.enabled) renderer.enabled = false;
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) filter.sharedMesh = null;

            var td = terrain.terrainData;
#if UNITY_EDITOR
            if (slopeLayer == null)
                slopeLayer = UnityEditor.AssetDatabase.LoadAssetAtPath<TerrainLayer>(
                    "Assets/Art/Terrain/Layers/TL_CliffSlopeDirt.terrainlayer");
#endif
            if (slopeLayer == null || td.alphamapResolution < 1) return;

            int slopeIndex = -1;
            var layers = td.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != slopeLayer && (layers[i] == null || layers[i].name != slopeLayer.name)) continue;
                slopeIndex = i;
                break;
            }

            if (slopeIndex < 0)
            {
                var newLayers = new TerrainLayer[layers.Length + 1];
                Array.Copy(layers, newLayers, layers.Length);
                slopeIndex = layers.Length;
                newLayers[slopeIndex] = slopeLayer;
                td.terrainLayers = newLayers;
                layers = td.terrainLayers;
            }
            if (layers.Length <= 1) return; // een bestaande ondergrond is vereist voor herstel

            int ar = td.alphamapResolution;
            var bounds = new RectInt(0, 0, ar, ar);
            if (changedHeightRegion.HasValue)
            {
                int hr = Mathf.Max(1, td.heightmapResolution - 1);
                var r = changedHeightRegion.Value;
                int x0 = Mathf.FloorToInt(((r.xMin - 2f) / hr) * ar) - 2;
                int z0 = Mathf.FloorToInt(((r.yMin - 2f) / hr) * ar) - 2;
                int x1 = Mathf.CeilToInt(((r.xMax + 2f) / hr) * ar) + 2;
                int z1 = Mathf.CeilToInt(((r.yMax + 2f) / hr) * ar) + 2;
                x0 = Mathf.Clamp(x0, 0, ar);
                z0 = Mathf.Clamp(z0, 0, ar);
                x1 = Mathf.Clamp(x1, 0, ar);
                z1 = Mathf.Clamp(z1, 0, ar);
                bounds = new RectInt(x0, z0, x1 - x0, z1 - z0);
            }
            if (bounds.width == 0 || bounds.height == 0) return;

            float beginAngle = Mathf.Atan(Mathf.Max(0f, startSlopePercent) * 0.01f) * Mathf.Rad2Deg;
            float endAngle = Mathf.Atan((Mathf.Max(0f, startSlopePercent) + Mathf.Max(1f, transitionSlopePercent)) * 0.01f) * Mathf.Rad2Deg;
            float maxWeight = Mathf.Clamp(maxSlopeWeight, 0.5f, 0.98f);
            float[,,] splat = td.GetAlphamaps(bounds.x, bounds.y, bounds.width, bounds.height);
            bool changed = false;

            for (int z = 0; z < bounds.height; z++)
            {
                float v = (bounds.y + z + 0.5f) / ar;
                for (int x = 0; x < bounds.width; x++)
                {
                    float u = (bounds.x + x + 0.5f) / ar;
                    float slope = td.GetSteepness(u, v);
                    float t = Mathf.InverseLerp(beginAngle, endAngle, slope);
                    float target = Mathf.SmoothStep(0f, maxWeight, t);

                    if (onlyPaintedCliffs && !IsPainted(u, v, td.heightmapResolution))
                        target = 0f;

                    float originalSum = 0f;
                    for (int layer = 0; layer < layers.Length; layer++)
                        if (layer != slopeIndex) originalSum += splat[z, x, layer];

                    // De speciale hellinglaag mag nooit 100% worden, anders verliezen
                    // we de informatie over de oorspronkelijke geschilderde lagen.
                    bool emptyBase = originalSum < 0.00001f;
                    float scale = (1f - target) / Mathf.Max(originalSum, 0.00001f);
                    float oldTarget = splat[z, x, slopeIndex];
                    if (Mathf.Abs(oldTarget - target) < 0.0005f && !emptyBase) continue;

                    for (int layer = 0; layer < layers.Length; layer++)
                    {
                        if (layer == slopeIndex) continue;
                        splat[z, x, layer] = emptyBase
                            ? (layer == 0 ? 1f - target : 0f)
                            : splat[z, x, layer] * scale;
                    }
                    splat[z, x, slopeIndex] = target;
                    changed = true;
                }
            }

            if (!changed) return;
            IsBuilding = true;
            try
            {
                td.SetAlphamaps(bounds.x, bounds.y, splat);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.EditorUtility.SetDirty(td);
#endif
            }
            finally { IsBuilding = false; }
        }

        bool IsPainted(float u, float v, int heightmapResolution)
        {
            if (!maskInitialised || cliffMask == null || cliffMaskRes != heightmapResolution)
                return false;

            int cx = Mathf.Clamp(Mathf.RoundToInt(u * (cliffMaskRes - 1)), 0, cliffMaskRes - 1);
            int cz = Mathf.Clamp(Mathf.RoundToInt(v * (cliffMaskRes - 1)), 0, cliffMaskRes - 1);
            for (int z = Mathf.Max(0, cz - 2); z <= Mathf.Min(cliffMaskRes - 1, cz + 2); z++)
                for (int x = Mathf.Max(0, cx - 2); x <= Mathf.Min(cliffMaskRes - 1, cx + 2); x++)
                    if (cliffMask[z * cliffMaskRes + x] != 0) return true;
            return false;
        }
    }
}
