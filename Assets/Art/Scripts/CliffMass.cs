// Conductor Chaos — één samengevoegde klif uit geschilderde cellen (issue #223).
// Gemaakt met Conductor Chaos > Art > Klif Massa Painter: elke cel op het raster heeft een hoogte in treden.
// De editor bouwt hier één mesh + collider van (pijlerstijl: facetkolommen, grasdek met overhang).
// Alleen data: tijdens het spelen gebeurt er niets (statisch decor, geen netwerkstatus).
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Art
{
    [DisallowMultipleComponent]
    public class CliffMass : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Tooltip("Rastermaat van één cel in meter. Na het eerste schilderen niet meer wijzigen.")]
        public float cellSize = 1f;
        [Tooltip("Hoogte van één trede in meter (2,4 = Cliff_Pillar_Small / Waterfall_Small).")]
        public float stepHeight = 2.4f;
        [Tooltip("Grasbolletjes op de bovenkant.")]
        public bool tufts = true;
        [Tooltip("Stenen klifwanden (M_Env_CliffStone) i.p.v. de rode rots.")]
        public bool stone;
        [Tooltip("Variatie in facetten en bolletjes.")]
        public int seed = 1;

        [HideInInspector] public float groundSignature;   // editor: herbouwen als de grond eronder verandert

        [SerializeField, HideInInspector] List<Vector2Int> cells = new List<Vector2Int>();
        [SerializeField, HideInInspector] List<int> levels = new List<int>();

        Dictionary<Vector2Int, int> map;

        public IReadOnlyDictionary<Vector2Int, int> Cells { get { EnsureMap(); return map; } }
        public int CellCount { get { EnsureMap(); return map.Count; } }

        public int GetLevel(Vector2Int c)
        {
            EnsureMap();
            return map.TryGetValue(c, out var l) ? l : 0;
        }

        /// <summary>Hoogte van een cel in treden; 0 = geen klif.</summary>
        public void SetLevel(Vector2Int c, int level)
        {
            EnsureMap();
            if (level <= 0) map.Remove(c);
            else map[c] = level;
        }

        public void Clear()
        {
            EnsureMap();
            map.Clear();
        }

        /// <summary>Cel (lokaal raster) onder een lokaal punt.</summary>
        public Vector2Int CellAt(Vector3 local) =>
            new Vector2Int(Mathf.RoundToInt(local.x / cellSize), Mathf.RoundToInt(local.z / cellSize));

        void EnsureMap()
        {
            if (map != null) return;
            map = new Dictionary<Vector2Int, int>(cells.Count);
            for (int i = 0; i < cells.Count && i < levels.Count; i++)
                if (levels[i] > 0) map[cells[i]] = levels[i];
        }

        // De dictionary is leidend zodra hij bestaat; bij (de)serialisatie (opslaan, Undo) gesorteerd terugschrijven,
        // zodat scene-diffs rustig blijven.
        public void OnBeforeSerialize()
        {
            if (map == null) return;
            var keys = new List<Vector2Int>(map.Keys);
            keys.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            cells.Clear();
            levels.Clear();
            foreach (var k in keys) { cells.Add(k); levels.Add(map[k]); }
        }

        public void OnAfterDeserialize() => map = null;
    }
}
