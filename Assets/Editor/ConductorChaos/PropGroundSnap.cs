// Conductor Chaos — props volgen de echte Unity Terrain-hoogte (issues #35 en #38).
// Gras/bloemen op steile hellingen worden omkeerbaar verborgen (niet vernietigd),
// en keren terug als de plek weer vlak wordt. Andere props houden hun bestaande regels.
// Handmatig: Conductor Chaos > Art > Snap Painted Props To Ground.
using System;
using System.Collections.Generic;
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class PropGroundSnap
    {
        const string RootName = "PaintedProps";
        const float DefaultPlantSlopePercent = 40f;
        const float PlantFootprintRadius = 0.4f;

        [MenuItem("Conductor Chaos/Art/Snap Painted Props To Ground")]
        static void SnapAllMenu()
        {
            int affected = 0;
            foreach (var t in Terrain.activeTerrains)
                affected += SnapOver(t);
            Debug.Log($"[Props] {affected} geschilderde objecten gecorrigeerd of verborgen/hersteld. " +
                      "Bloemen en gras op steile hellingen blijven in de scène bewaard (Undo mogelijk).");
        }

        // Voor oudere losse planten zonder PaintedProp-component: selecteer de betreffende
        // objecten (of hun parent) en gebruik dit menu. Beperkt tot gras en bloemen;
        // geen globale verplaatsing van handmatig geplaatste scenery.
        [MenuItem("Conductor Chaos/Art/Snap Selected Flowers And Grass To Terrain")]
        static void SnapSelectedVegetationMenu()
        {
            int corrected = 0;
            var seen = new HashSet<Transform>();
            foreach (var root in Selection.transforms)
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!seen.Add(child) || !Plant(child, null)) continue;
                    var terrain = TerrainAt(child.position);
                    if (terrain == null) continue;
                    var marker = child.GetComponent<PaintedProp>();
                    if (marker == null)
                    {
                        marker = Undo.AddComponent<PaintedProp>(child.gameObject);
                        marker.groundOffset = DefaultOffset("Plants", child);
                    }
                    corrected += SnapOne(child, marker, "Plants", terrain);
                }
            Debug.Log($"[Props] {corrected} geselecteerde gras- of bloemobjecten verplaatst/verborgen. " +
                      "Handmatig uitgeschakelde objecten worden niet gewijzigd.");
        }

        /// <summary>
        /// Dezelfde plaatsingscontrole voor Prop Painter en herstel: ook de voetafdruk naast
        /// de plant moet vlak genoeg zijn (anders hangt de bloem aan de klifrand).
        /// </summary>
        public static bool SuitableForPlants(Terrain terrain, Vector3 world)
        {
            if (terrain == null || terrain.terrainData == null) return false;
            var td = terrain.terrainData;
            var origin = terrain.transform.position;
            var size = td.size;
            var cliff = terrain.GetComponentInChildren<StylizedTerrainCliffs>();
            float maxPercent = cliff != null
                ? Mathf.Clamp(cliff.startSlopePercent + 10f, 30f, 60f)
                : DefaultPlantSlopePercent;

            Vector2[] footprint =
            {
                Vector2.zero,
                new Vector2(PlantFootprintRadius, 0f),
                new Vector2(-PlantFootprintRadius, 0f),
                new Vector2(0f, PlantFootprintRadius),
                new Vector2(0f, -PlantFootprintRadius)
            };
            float baseHeight = terrain.SampleHeight(world) + origin.y;
            foreach (var offset in footprint)
            {
                float x = world.x + offset.x;
                float z = world.z + offset.y;
                if (x < origin.x || x > origin.x + size.x ||
                    z < origin.z || z > origin.z + size.z) return false;

                float u = (x - origin.x) / size.x;
                float v = (z - origin.z) / size.z;
                if (Mathf.Tan(td.GetSteepness(u, v) * Mathf.Deg2Rad) * 100f > maxPercent)
                    return false;
                if (Mathf.Abs(terrain.SampleHeight(new Vector3(x, baseHeight, z)) + origin.y - baseHeight) >
                    PlantFootprintRadius * maxPercent / 100f)
                    return false;
            }
            return true;
        }

        /// <summary>Vindt het corresponderende Unity Terrain onder een X/Z-punt, niet een collider van een geschilderde rots.</summary>
        public static Terrain TerrainAt(Vector3 world)
        {
            Terrain nearest = null;
            float error = float.MaxValue;
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var p = t.transform.position;
                var size = t.terrainData.size;
                if (world.x < p.x || world.z < p.z || world.x > p.x + size.x || world.z > p.z + size.z)
                    continue;

                float y = t.SampleHeight(world) + p.y;
                float candidate = Mathf.Abs(y - world.y);
                if (candidate < error)
                {
                    nearest = t;
                    error = candidate;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Snap bestaande PaintedProps en óók gemarkeerde props buiten de standaardroot.
        /// Inactieve, automatisch verborgen planten blijven bereikbaar en kunnen herstellen.
        /// </summary>
        public static int SnapOver(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) return 0;
            terrain.terrainData.SyncHeightmap();
            var min = terrain.transform.position;
            var max = min + terrain.terrainData.size;
            var seen = new HashSet<Transform>();
            int affected = 0;

            // Ondersteun props van oude scenes zonder PaintedProp-component.
            var root = GameObject.Find(RootName);
            if (root != null && root.scene == terrain.gameObject.scene)
                foreach (Transform cat in root.transform)
                {
                    if (cat.name == "Sky") continue;
                    foreach (Transform prop in cat)
                    {
                        if (!Within(prop.position, min, max)) continue;
                        var marker = prop.GetComponent<PaintedProp>();
                        if (marker == null)
                        {
                            marker = Undo.AddComponent<PaintedProp>(prop.gameObject);
                            marker.groundOffset = DefaultOffset(cat.name, prop);
                        }
                        seen.Add(prop);
                        affected += SnapOne(prop, marker, cat.name, terrain);
                    }
                }

            // Gebruik niet alleen GameObject.Find("PaintedProps"): gebruikers kunnen
            // een andere rootnaam kiezen of gemarkeerde prefabs verplaatsen in Hierarchy.
            foreach (var marker in UnityEngine.Object.FindObjectsByType<PaintedProp>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (marker == null || marker.gameObject.scene != terrain.gameObject.scene) continue;
                var prop = marker.transform;
                if (!seen.Add(prop) || !Within(prop.position, min, max)) continue;
                string category = FindCategory(prop);
                if (category == "Sky") continue;
                affected += SnapOne(prop, marker, category, terrain);
            }

            return affected;
        }

        static bool Within(Vector3 world, Vector3 min, Vector3 max) =>
            world.x >= min.x && world.z >= min.z && world.x <= max.x && world.z <= max.z;

        static bool Plant(Transform prop, string category)
        {
            if (category == "Plants") return true;
            var name = prop.name;
            return name.IndexOf("Grass_Tuft", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static int SnapOne(Transform prop, PaintedProp marker, string category, Terrain terrain)
        {
            if (marker == null || !marker.followGround) return 0;
            var go = prop.gameObject;
            bool plant = Plant(prop, category);
            // Laat objecten die de gebruiker zelf heeft uitgezet met rust.
            if (!go.activeSelf && !marker.HiddenBySteepTerrain) return 0;

            if (plant)
            {
                bool allowed = SuitableForPlants(terrain, prop.position);
                if (!allowed)
                {
                    if (marker.HiddenBySteepTerrain) return 0;
                    Undo.RecordObject(marker, "Hide plants on cliff");
                    Undo.RecordObject(go, "Hide plants on cliff");
                    marker.HiddenBySteepTerrain = true;
                    go.SetActive(false);
                    return 1;
                }
                if (marker.HiddenBySteepTerrain)
                {
                    Undo.RecordObject(marker, "Restore plants on flat ground");
                    Undo.RecordObject(go, "Restore plants on flat ground");
                    marker.HiddenBySteepTerrain = false;
                    go.SetActive(true);
                }
            }

            float y;
            if (plant)
            {
                // RaycastAll kan de bovenkant van een los cliff-prefab raken, zodat
                // bloemen blijven zweven. Planten horen uitsluitend op Unity Terrain.
                y = terrain.SampleHeight(prop.position) + terrain.transform.position.y;
            }
            else if (!Ground(prop, terrain, out y))
                return 0;

            float desired = y + marker.groundOffset;
            if (Mathf.Abs(desired - prop.position.y) < 0.01f)
                return 0;
            Undo.RecordObject(prop, "Snap painted prop to ground");
            var p = prop.position;
            prop.position = new Vector3(p.x, desired, p.z);
            return 1;
        }

        // Niet-plantprops kunnen bewust op geschilderde 3D-kliffen/rotsen staan:
        // behoud daarvoor bestaand collidergedrag, maar filter op het juiste terrein.
        static bool Ground(Transform prop, Terrain terrain, out float y)
        {
            var p = prop.position;
            float top = Mathf.Max(p.y + 6f, terrain.transform.position.y + terrain.terrainData.size.y + 1f);
            var hits = Physics.RaycastAll(new Vector3(p.x, top, p.z), Vector3.down,
                top - terrain.transform.position.y + 5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            bool found = false;
            var terrainCollider = terrain.GetComponent<TerrainCollider>();

            foreach (var h in hits)
            {
                if (h.transform.IsChildOf(prop)) continue;
                bool isTerrain = h.collider == terrainCollider;
                bool isCliff = h.transform.parent != null && FindCategory(h.transform) == "Cliffs";
                if (!isTerrain && !isCliff) continue;
                if (isCliff && h.point.y > p.y + 3f) continue;
                if (h.point.y > best) { best = h.point.y; found = true; }
            }
            y = found ? best : terrain.SampleHeight(p) + terrain.transform.position.y;
            return true;
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
                    if (rs.Length > 0)
                    {
                        var b = rs[0].bounds;
                        foreach (var r in rs) b.Encapsulate(r.bounds);
                        h = b.size.y;
                    }
                    return -(0.05f * s + 0.22f * h);
                }
                case "Paths": return -0.03f * s;
                case "Plants": return -0.02f * s;
                case "Trees": return -0.08f * s;
                case "Cliffs":
                case "Water": return -0.2f * s;
                case "Mountains": return -1f * s;
                default: return -0.05f * s;
            }
        }
    }
}
