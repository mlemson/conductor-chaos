using System;
using System.Linq;
using ConductorChaos.Art;
using ConductorChaos.Track;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    /// <summary>Opens the channel around an existing river without replacing trains, tracks or scenery.</summary>
    internal static class CliffCanyonCarver
    {
        internal const float WaterToWallBase = .65f;
        internal const float WaterChannelMargin = 1.4f;
        private const float ChannelDepth = 1.2f;
        private const float TrackClearance = 3.2f;

        [MenuItem("Conductor Chaos/Art/Kloof: open kanaal rond geselecteerde wand")]
        private static void CarveSelected()
        {
            var mass = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<CliffMass>() : null;
            if (!mass || EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Kloof uitsnijden", "Selecteer de klifmassa in Edit Mode.", "OK");
                return;
            }

            try
            {
                var roots = mass.gameObject.scene.GetRootGameObjects();
                var terrain = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>()).Single();
                var river = roots.Single(r => r.name == "Rivier in de kloof").GetComponentsInChildren<StylizedWater>();
                Carve(mass, terrain, river);
            }
            catch (Exception error) { Debug.LogException(error); }
        }

        internal static int Carve(CliffMass mass, Terrain terrain, StylizedWater[] water)
        {
            if (!mass || mass.CellCount == 0 || !terrain || !terrain.terrainData || water == null ||
                water.Length < 2 || terrain.gameObject.scene != mass.gameObject.scene ||
                water.Any(w => !w || w.gameObject.scene != mass.gameObject.scene))
                throw new ArgumentException("De klif, het terrein en de rivierstukken moeten in dezelfde scène staan.");

            if (water.Max(w => w.SurfaceY) - water.Min(w => w.SurfaceY) > .1f)
                throw new ArgumentException("De rivierstukken moeten één doorlopend waterniveau vormen.");

            float surface = water.Min(w => w.SurfaceY);
            float newOrigin = surface - WaterToWallBase;
            float cellMargin = mass.cellSize * .7072f + .35f;
            var segments = water.Select(w => new ChannelSegment(w)).ToArray();
            var rail = mass.gameObject.scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<TrackPiece>())
                .Where(t => t.HasSockets && !t.name.Contains("Bridge"))
                .SelectMany(t => Enumerable.Range(0, Mathf.Max(2, Mathf.CeilToInt(t.Length) + 1)).Select(i =>
                {
                    t.Evaluate(Mathf.Min(1, i / Mathf.Max(1, t.Length)), out Vector3 position, out _);
                    return position;
                })).ToArray();

            var data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, resolution, resolution);
            Vector3 origin = terrain.transform.position;
            Vector3 mapSize = data.size;
            Vector3 lo = segments[0].Center, hi = lo;
            foreach (var s in segments)
            {
                var extent = Vector3.one * (s.HalfLength + s.HalfWidth + WaterChannelMargin + 5f);
                lo = Vector3.Min(lo, s.Center - extent);
                hi = Vector3.Max(hi, s.Center + extent);
            }

            var cleared = mass.Cells.Keys.Where(cell =>
            {
                var p = mass.transform.TransformPoint(new Vector3(cell.x * mass.cellSize, 0f, cell.y * mass.cellSize));
                return EdgeDistance(p, segments) <= WaterChannelMargin + cellMargin;
            }).ToArray();

            var originPosition = mass.transform.position;
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { mass, mass.transform, data }, "Kloof uitsnijden");
            mass.transform.position = new Vector3(originPosition.x, newOrigin, originPosition.z);
            foreach (var cell in cleared) mass.SetLevel(cell, 0);

            // Lower the actual terrain under the river and its banks. Preserve dry terrain and the railway shelf.
            int changed = 0;
            for (int z = 0; z < resolution; z++)
            {
                float wz = origin.z + z * mapSize.z / (resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float wx = origin.x + x * mapSize.x / (resolution - 1);
                    var point = new Vector3(wx, newOrigin, wz);
                    var local = mass.transform.InverseTransformPoint(point);
                    int tier = mass.GetLevel(mass.CellAt(local));
                    bool inBounds = wx >= lo.x && wx <= hi.x && wz >= lo.z && wz <= hi.z;
                    if (tier == 0 && !inBounds) continue;
                    float edge = inBounds ? EdgeDistance(point, segments) : float.PositiveInfinity;
                    if (tier == 0 && edge > WaterChannelMargin + 3f) continue;
                    if (rail.Any(r => (r.x - wx) * (r.x - wx) + (r.z - wz) * (r.z - wz) < TrackClearance * TrackClearance))
                        continue;

                    float before = origin.y + heights[z, x] * mapSize.y;
                    float target = before;
                    if (tier > 0)
                        target = Mathf.Min(target, mass.transform.TransformPoint(
                            new Vector3(local.x, tier * mass.stepHeight - CliffMassBuilder.TerrainCapClearance, local.z)).y);
                    if (edge <= WaterChannelMargin + 3f)
                    {
                        float bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, WaterChannelMargin + 3f, edge));
                        target = Mathf.Min(target, Mathf.Lerp(surface - ChannelDepth, before, bank));
                    }
                    if (before - target < .03f) continue;
                    heights[z, x] = Mathf.Clamp01((target - origin.y) / mapSize.y);
                    changed++;
                }
            }

            if (changed > 0) data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(mass);
            Physics.SyncTransforms();
            CliffMassBuilder.Rebuild(mass);
            EditorSceneManager.MarkSceneDirty(mass.gameObject.scene);
            Debug.Log($"KLOOF: oorsprong {originPosition.y:F2} → {newOrigin:F2} m, {cleared.Length} cellen uit de rivierdoorlaat, {changed} hoogtepunten verlaagd.", mass);
            return cleared.Length;
        }

        private struct ChannelSegment
        {
            public readonly Vector3 Center;
            public readonly Vector3 Forward;
            public readonly float HalfLength;
            public readonly float HalfWidth;

            public ChannelSegment(StylizedWater water)
            {
                Center = water.transform.position;
                Forward = water.transform.forward;
                Forward.y = 0;
                Forward.Normalize();
                HalfLength = water.size.y * Mathf.Abs(water.transform.lossyScale.z) * .5f;
                HalfWidth = water.size.x * Mathf.Abs(water.transform.lossyScale.x) * .5f;
            }
        }

        private static float EdgeDistance(Vector3 point, ChannelSegment[] segments)
        {
            float best = float.PositiveInfinity;
            foreach (var s in segments)
            {
                Vector3 offset = point - s.Center;
                offset.y = 0;
                float along = Mathf.Clamp(Vector3.Dot(offset, s.Forward), -s.HalfLength, s.HalfLength);
                best = Mathf.Min(best, (offset - s.Forward * along).magnitude - s.HalfWidth);
            }
            return best;
        }
    }
}