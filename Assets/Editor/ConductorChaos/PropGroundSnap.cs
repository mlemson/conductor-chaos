// Conductor Chaos — geschilderde props op de grond houden (issue #35).
// Na het ophogen/uitdiepen van het terrein (Cliff Painter of Unity's penselen) worden alle objecten onder 'PaintedProps'
// boven dat terrein weer op de ondergrond gezet, met dezelfde inzakking als bij het schilderen. Ze landen op het terrein
// of op een klif (Cliffs) eronder. Wolken (Sky) en objecten met PaintedProp.followGround = uit blijven staan.
// Handmatig: Conductor Chaos > Art > Snap Painted Props To Ground.
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class PropGroundSnap
    {
        const string RootName = "PaintedProps";

        [MenuItem("Conductor Chaos/Art/Snap Painted Props To Ground")]
        static void SnapAllMenu()
        {
            int n = 0;
            foreach (var t in Terrain.activeTerrains) n += SnapOver(t);
            Debug.Log($"[Props] {n} objecten op de grond gezet.");
        }

        /// <summary>Zet geschilderde props boven dit terrein terug op de grond. Geeft het aantal verplaatste objecten.</summary>
        public static int SnapOver(Terrain terrain)
        {
            var root = GameObject.Find(RootName);
            if (root == null || terrain == null) return 0;
            terrain.ApplyDelayedHeightmapModification();             // TerrainCollider bijwerken voor de stralen
            var td = terrain.terrainData;
            var min = terrain.transform.position;
            var max = min + td.size;
            int moved = 0;
            foreach (Transform cat in root.transform)
            {
                if (cat.name == "Sky") continue;                       // wolken zweven
                foreach (Transform prop in cat)
                {
                    var p = prop.position;
                    if (p.x < min.x || p.z < min.z || p.x > max.x || p.z > max.z) continue;
                    var marker = prop.GetComponent<PaintedProp>();
                    if (marker != null && !marker.followGround) continue;
                    if (!Ground(prop, terrain, out float ground)) continue;
                    if (marker == null)                                 // van vóór deze versie: inzakking per soort schatten
                    {
                        marker = Undo.AddComponent<PaintedProp>(prop.gameObject);
                        marker.groundOffset = DefaultOffset(cat.name, prop);
                    }
                    float y = ground + marker.groundOffset;
                    if (Mathf.Abs(y - p.y) < 0.01f) continue;
                    Undo.RecordObject(prop, "Props op de grond");
                    prop.position = new Vector3(p.x, y, p.z);
                    moved++;
                }
            }
            return moved;
        }

        // grondhoogte onder het object: terrein, of een geschilderde klif eronder (niet het object zelf, geen bomen e.d.)
        static bool Ground(Transform prop, Terrain terrain, out float y)
        {
            y = 0f;
            var p = prop.position;
            float top = Mathf.Max(p.y + 6f, terrain.transform.position.y + terrain.terrainData.size.y + 1f);
            var hits = Physics.RaycastAll(new Vector3(p.x, top, p.z), Vector3.down, top - terrain.transform.position.y + 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(prop)) continue;
                bool isTerrain = h.collider is TerrainCollider;
                bool isCliff = h.transform.parent != null && FindCategory(h.transform) == "Cliffs";
                if (!isTerrain && !isCliff) continue;
                if (isCliff && h.point.y > p.y + 3f) continue;           // niet omhoog springen naar een hoge klif ernaast
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            if (!found)
            {
                best = terrain.SampleHeight(p) + terrain.transform.position.y;
                found = true;
            }
            y = best;
            return found;
        }

        static string FindCategory(Transform t)
        {
            while (t.parent != null)
            {
                if (t.parent.name == RootName) return t.name;
                t = t.parent;
            }
            return null;
        }

        static float DefaultOffset(string category, Transform prop)
        {
            float s = prop.lossyScale.x;
            switch (category)
            {
                case "Rocks":
                {
                    float h = 0f;
                    var rs = prop.GetComponentsInChildren<Renderer>();
                    if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); h = b.size.y; }
                    return -(0.05f * s + 0.22f * h);
                }
                case "Paths": return -0.03f * s;
                case "Plants": return -0.02f * s;
                case "Trees": return -0.08f * s;
                case "Cliffs": case "Water": return -0.2f * s;
                case "Mountains": return -1f * s;
                default: return -0.05f * s;
            }
        }
    }
}
