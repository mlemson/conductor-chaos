// Conductor Chaos — geometrie voor CliffMass (issue #223). Puur rekenwerk, geen scene-toegang.
// Zelfde stijl als pillar()/cluster() in ArtSource/Environment/build_environment_kit.py:
//   - per trede de omtrek van alle cellen met hoogte >= trede (marching squares: hoeken worden afgeschuind),
//   - een wand met verticale facetkolommen (fold), voet die breder de grond in loopt,
//   - een grasdek met korte overhang en druppende rand,
//   - elke wand hoort bij de hoogste trede op die plek en loopt in één stuk naar de grond (of naar de lagere trede
//     ervoor). Alleen waar een lagere trede vóór de wand ligt komt een grasrichel; de hogere wand staat daar iets
//     naar binnen. Zo liggen er nooit twee wanden in hetzelfde vlak (geen z-fighting),
//   - een paar grasbolletjes (M_Env_Bush) bovenop.
// Alle variatie komt uit een hash van de positie: schilder je verder, dan blijft de rest van de klif gelijk.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class CliffMassGeometry
    {
        public const int SubCliff = 0, SubGrass = 1, SubBush = 2;
        const float MaxSegment = 1.0f;          // facetbreedte langs de wand
        const float UpperInset = 0.42f;         // hogere trede staat zo ver naar binnen (grasrichel)

        public class Output
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };
        }

        // Eén omtreklus van een trede. inside/outside = hoogte (treden) van de cel binnen/buiten de rand op dat punt.
        class Loop
        {
            public Vector2[] pts;
            public int[] inside, outside;
        }

        struct Ring
        {
            public Vector2[] pts;
            public float[] ys;
            public Color[] cols;
        }

        /// <param name="cells">cel (lokaal raster) -> hoogte in treden (>= 1)</param>
        /// <param name="ground">lokale x, z -> lokale hoogte van de grond (voor de voet van de onderste trede)</param>
        public static Output Build(IReadOnlyDictionary<Vector2Int, int> cells, float cellSize, float stepHeight, int seed,
                                   bool tufts, Func<float, float, float> ground)
        {
            var o = new Output();
            if (cells == null || cells.Count == 0) return o;
            cellSize = Mathf.Max(0.25f, cellSize);
            stepHeight = Mathf.Max(0.5f, stepHeight);
            int maxLevel = 0, minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
            foreach (var kv in cells)
            {
                maxLevel = Mathf.Max(maxLevel, kv.Value);
                minX = Mathf.Min(minX, kv.Key.x); maxX = Mathf.Max(maxX, kv.Key.x);
                minZ = Mathf.Min(minZ, kv.Key.y); maxZ = Mathf.Max(maxZ, kv.Key.y);
            }

            for (int tier = 1; tier <= maxLevel; tier++)
            {
                var loops = Contours(cells, tier, minX, minZ, maxX, maxZ, cellSize);
                float top = tier * stepHeight;
                for (int i = 0; i < loops.Count; i++)
                {
                    float width = 2f * Mathf.Abs(Area(loops[i].pts)) /
                                  Mathf.Max(0.01f, Perimeter(loops[i].pts));
                    float sc = Mathf.Clamp(width / 1.2f, 0.35f, 1f);
                    BuildLoop(o, loops[i], tier, top, stepHeight, sc, width, seed, ground);
                }

                // Do not triangulate a huge outer loop with a hole through it:
                // the old ear-clipping fallback filled the *canyon opening*
                // with a giant grass roof. Tile the exposed tier instead.
                // This also handles disconnected islands and diagonal saddles.
                CapCells(o, cells, tier, cellSize, top + 0.14f, seed);
                if (tufts) Tufts(o, cells, tier, top, cellSize, seed);
            }
            return o;
        }

        // ------------------------------------------------------------------ omtrek (marching squares)
        static List<Loop> Contours(IReadOnlyDictionary<Vector2Int, int> cells, int tier, int minX, int minZ, int maxX, int maxZ, float cs)
        {
            int Lv(int px, int pz) => cells.TryGetValue(new Vector2Int(px, pz), out var lv) ? lv : 0;
            var attr = new Dictionary<long, Vector2Int>();      // punt -> (binnen, buiten)
            // Segmenten met de klif aan de linkerkant; punten in verdubbelde rastercoördinaten (randmiddens).
            var next = new Dictionary<long, long>();
            var order = new List<long>();
            var corner = new bool[4];
            var lvl = new int[4];
            var cx = new int[4];
            var cz = new int[4];
            var crossKey = new long[4];
            var crossExit = new bool[4];
            for (int z = minZ - 1; z <= maxZ; z++)
                for (int x = minX - 1; x <= maxX; x++)
                {
                    cx[0] = x; cz[0] = z; cx[1] = x + 1; cz[1] = z; cx[2] = x + 1; cz[2] = z + 1; cx[3] = x; cz[3] = z + 1;
                    int count = 0, solid = 0;
                    for (int k = 0; k < 4; k++) { lvl[k] = Lv(cx[k], cz[k]); corner[k] = lvl[k] >= tier; if (corner[k]) solid++; }
                    if (solid == 0 || solid == 4) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        int k2 = (k + 1) & 3;
                        if (corner[k] == corner[k2]) continue;
                        crossKey[count] = Key(cx[k] + cx[k2], cz[k] + cz[k2]);
                        crossExit[count] = corner[k];
                        attr[crossKey[count]] = corner[k] ? new Vector2Int(lvl[k], lvl[k2]) : new Vector2Int(lvl[k2], lvl[k]);
                        count++;
                    }
                    // diagonaal verbonden (zadel): klif loopt door, het lege hoekje wordt afgesneden
                    for (int c = 0; c < count; c++)
                        if (crossExit[c])
                        {
                            long from = crossKey[c], to = crossKey[(c + 1) % count];
                            if (!next.ContainsKey(from)) { next[from] = to; order.Add(from); }
                        }
                }

            var loops = new List<Loop>();
            var used = new HashSet<long>();
            foreach (long start in order)
            {
                if (used.Contains(start)) continue;
                var pts = new List<Vector2>();
                var ins = new List<int>();
                var outs = new List<int>();
                long cur = start;
                int guard = next.Count + 2;
                while (!used.Contains(cur) && guard-- > 0)
                {
                    used.Add(cur);
                    pts.Add(Unkey(cur) * (cs * 0.5f));
                    var a = attr[cur];
                    ins.Add(a.x); outs.Add(a.y);
                    if (!next.TryGetValue(cur, out cur)) break;
                }
                if (pts.Count >= 3) loops.Add(Refine(pts, ins, outs));
            }
            return loops;
        }

        static long Key(int x2, int z2) => ((long)(x2 + 1000000) << 32) | (uint)(z2 + 1000000);
        static Vector2 Unkey(long k) => new Vector2((int)(k >> 32) - 1000000, (int)(k & 0xffffffff) - 1000000);

        // Rechte stukken met dezelfde buren samenvoegen tot facetten van hooguit ~1 m; te lange stukken verdelen.
        static Loop Refine(List<Vector2> pts, List<int> ins, List<int> outs)
        {
            int n = pts.Count;
            var keep = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int ip = (i + n - 1) % n, inx = (i + 1) % n;
                bool straight = Mathf.Abs(Cross(pts[i] - pts[ip], pts[inx] - pts[i])) < 1e-5f;
                bool same = ins[i] == ins[ip] && ins[i] == ins[inx] && outs[i] == outs[ip] && outs[i] == outs[inx];
                bool shortRun = keep.Count > 0 && Vector2.Distance(pts[keep[keep.Count - 1]], pts[inx]) <= MaxSegment * 1.05f;
                if (straight && same && shortRun) continue;      // dit punt valt weg: het facet wordt breder
                keep.Add(i);
            }
            if (keep.Count < 3) { keep.Clear(); for (int i = 0; i < n; i++) keep.Add(i); }
            var P = new List<Vector2>();
            var I = new List<int>();
            var O = new List<int>();
            for (int k = 0; k < keep.Count; k++)
            {
                int a = keep[k], b = keep[(k + 1) % keep.Count];
                int seg = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(pts[a], pts[b]) / MaxSegment));
                for (int s2 = 0; s2 < seg; s2++)
                {
                    bool first = s2 * 2 < seg;
                    P.Add(Vector2.Lerp(pts[a], pts[b], s2 / (float)seg));
                    I.Add(first ? ins[a] : ins[b]);
                    O.Add(first ? outs[a] : outs[b]);
                }
            }
            return new Loop { pts = P.ToArray(), inside = I.ToArray(), outside = O.ToArray() };
        }

        // ------------------------------------------------------------------ wand + grasrand per lus
        static Vector2[] BuildLoop(Output o, Loop loop, int tier, float top, float step, float sc, float width, int seed,
                                           Func<float, float, float> ground)
        {
            var P = loop.pts;
            int n = P.Length;
            var fold = new float[n];
            var drip = new float[n];
            var b = new float[n];          // onderkant van de wand: grond of de lagere trede ervoor
            var inset = new float[n];      // naar binnen waar een lagere trede vóór de wand ligt (grasrichel)
            var cover = new float[n];      // hier staat een hogere wand: deze rand verstoppen in die wand
            var covered = new bool[n];
            for (int k = 0; k < n; k++)
            {
                int qx = Mathf.RoundToInt(P[k].x * 100f), qz = Mathf.RoundToInt(P[k].y * 100f);
                fold[k] = (Hash(qx, qz, 1, seed) < 0.5f ? 0.13f : -0.04f) + Hash(qx, qz, 2, seed) * 0.06f - 0.02f;
                drip[k] = Hash(qx, qz, 4, seed) > 0.45f ? 0.04f + Hash(qx, qz, 5, seed) * 0.12f : 0f;
                int floor = Mathf.Clamp(loop.outside[k], 0, tier - 1);
                b[k] = floor == 0 ? (ground != null ? ground(P[k].x, P[k].y) : 0f) : floor * step;
                b[k] = Mathf.Min(b[k], top - 0.6f);
                inset[k] = floor == 0 ? 0f : UpperInset;
                covered[k] = loop.inside[k] > tier;
                cover[k] = covered[k] ? Mathf.Min(0.6f, width * 0.4f) / sc : 0f;   // ~0,6 m de hogere wand in (smalle stukken minder)
            }

            float[] D(Func<int, float> f) { var d = new float[n]; for (int k = 0; k < n; k++) d[k] = f(k) * sc; return d; }
            float[] Y(Func<int, float> f) { var y = new float[n]; for (int k = 0; k < n; k++) y[k] = f(k); return y; }

            // A wall can be 31 m tall in level 2. Four long facets make it look
            // like a single flat slab; split high walls into strata of ~2.4 m.
            // The first/last rings stay fixed, so the foot and grass rim still join.
            float lowestFoot = top;
            for (int k = 0; k < n; k++) lowestFoot = Mathf.Min(lowestFoot, b[k]);
            int segments = Mathf.Clamp(Mathf.CeilToInt((top - lowestFoot) / 2.4f), 4, 20);
            var body = new List<Ring>(segments + 1);
            for (int r = 0; r <= segments; r++)
            {
                float t = r / (float)segments;
                int ringIndex = r;
                body.Add(MakeRing(P, D(k =>
                {
                    float from = -fold[k] - 0.16f + inset[k];
                    float to = -fold[k] * 0.5f + 0.10f + inset[k];
                    float displacement = Mathf.Lerp(from, to, t);
                    // Interior strata have small deterministic folds; on short
                    // walls they fade away to avoid folded or inverted geometry.
                    if (ringIndex > 0 && ringIndex < segments)
                    {
                        int qx = Mathf.RoundToInt(P[k].x * 100f), qz = Mathf.RoundToInt(P[k].y * 100f);
                        float variation = (Hash(qx, qz, 31 + ringIndex * 17, seed) - 0.5f) * 0.28f;
                        displacement += variation * Mathf.Sin(t * Mathf.PI) *
                                        Mathf.Clamp01((top - b[k]) / 4f);
                    }
                    return displacement;
                }), Y(k => Mathf.Lerp(b[k] - 0.30f, top - 0.02f, t))));
            }
            for (int r = 0; r < body.Count; r++)
            {
                var ring = body[r];
                for (int k = 0; k < n; k++)
                {
                    float h = Mathf.Clamp01((ring.ys[k] - b[k]) / Mathf.Max(0.1f, top - b[k]));
                    float ao = 1f - 0.32f * Mathf.Pow(1f - Mathf.Clamp01(h / 0.7f), 1.5f);
                    if (r == 0) ao *= inset[k] > 0f ? 0.8f : 0.72f;     // voet in de grond / onder de richel
                    if (r == body.Count - 1) ao *= 0.86f;                 // onder de grasrand
                    ring.cols[k] = Shade(ao, 0f, P[k].x, ring.ys[k], P[k].y, seed);
                }
            }
            for (int r = 0; r + 1 < body.Count; r++) Strip(o, body[r], body[r + 1], SubCliff, covered);

            const float capIn = 0.38f;
            var grass = new List<Ring>
            {
                MakeRing(P, D(k => -fold[k] * 0.5f + 0.02f + inset[k] + cover[k]), Y(k => top - 0.07f - drip[k])),
                MakeRing(P, D(k => -fold[k] * 0.5f - 0.04f + inset[k] + cover[k]), Y(k => top + 0.04f)),
                MakeRing(P, D(k => -fold[k] * 0.5f + 0.12f + inset[k] + cover[k]), Y(k => top + 0.12f)),
                MakeRing(P, D(k => capIn + inset[k] + cover[k]), Y(k => top + 0.14f)),
            };
            float[] edge = { 0f, 0.8f, 0.6f, 0f };
            float[] aoG = { 0.78f, 1f, 1f, 1f };
            for (int r = 0; r < grass.Count; r++)
                for (int k = 0; k < n; k++)
                    grass[r].cols[k] = Shade(aoG[r], edge[r], P[k].x, grass[r].ys[k], P[k].y, seed);
            for (int r = 0; r + 1 < grass.Count; r++) Strip(o, grass[r], grass[r + 1], SubGrass, covered);
            return grass[grass.Count - 1].pts;
        }

        static Ring MakeRing(Vector2[] P, float[] d, float[] ys) =>
            new Ring { pts = Offset(P, d), ys = ys, cols = new Color[P.Length] };

        // d > 0 = de klif in (links van de looprichting), d < 0 = naar buiten
        static Vector2[] Offset(Vector2[] P, float[] d)
        {
            int n = P.Length;
            var outp = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = P[(i + n - 1) % n], b = P[i], c = P[(i + 1) % n];
                Vector2 e1 = (b - a).normalized, e2 = (c - b).normalized;
                Vector2 nrm = new Vector2(-e1.y, e1.x) + new Vector2(-e2.y, e2.x);
                if (nrm.sqrMagnitude < 1e-8f) nrm = new Vector2(-e2.y, e2.x);
                outp[i] = b + nrm.normalized * d[i];
            }
            return outp;
        }

        // Rand tussen twee ringen; de lus heeft de klif links, dus (a, c, b) en (a, d, c) wijzen naar buiten.
        // Stukken waar aan beide kanten een hogere wand staat, worden overgeslagen (die wand dekt ze).
        static void Strip(Output o, Ring lo, Ring hi, int sub, bool[] skip)
        {
            int n = lo.pts.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                if (skip[i] && skip[j]) continue;
                Vector3 a = V(lo.pts[i], lo.ys[i]), b = V(lo.pts[j], lo.ys[j]);
                Vector3 c = V(hi.pts[j], hi.ys[j]), d = V(hi.pts[i], hi.ys[i]);
                Tri(o, sub, a, c, b, lo.cols[i], hi.cols[j], lo.cols[j]);
                Tri(o, sub, a, d, c, lo.cols[i], hi.cols[i], hi.cols[j]);
            }
        }

        // ------------------------------------------------------------------ grass caps (marching squares)
        // A canyon is a long, often annular contour with a large hole down to
        // the river. Global ear-clipping was fragile: its fallback could draw
        // triangles across the hole, producing a flat-topped mesa above the river.
        //
        // Triangulate *only* exposed grid cells at this exact tier. Each square
        // is convex (or two separate triangles at a diagonal saddle), so no
        // triangle can cross an empty square in the canyon.
        static void CapCells(Output o, IReadOnlyDictionary<Vector2Int, int> cells,
                             int tier, float cs, float y, int seed)
        {
            var squares = new HashSet<Vector2Int>();
            foreach (var kv in cells)
            {
                if (kv.Value != tier) continue;
                for (int dz = 0; dz <= 1; dz++)
                    for (int dx = 0; dx <= 1; dx++)
                        squares.Add(new Vector2Int(kv.Key.x - dx, kv.Key.y - dz));
            }

            // Stable build order, independent of Dictionary iteration order.
            var ordered = new List<Vector2Int>(squares);
            ordered.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            foreach (var sq in ordered)
            {
                Vector2[] p =
                {
                    new Vector2(sq.x * cs, sq.y * cs),
                    new Vector2((sq.x + 1) * cs, sq.y * cs),
                    new Vector2((sq.x + 1) * cs, (sq.y + 1) * cs),
                    new Vector2(sq.x * cs, (sq.y + 1) * cs)
                };
                int mask = 0;
                for (int i = 0; i < 4; i++)
                {
                    var c = new Vector2Int(sq.x + (i == 1 || i == 2 ? 1 : 0),
                                           sq.y + (i >= 2 ? 1 : 0));
                    if (cells.TryGetValue(c, out int level) && level == tier)
                        mask |= 1 << i;
                }
                if (mask == 0) continue;

                // Diagonally opposed corners are *not* one polygon: joining
                // them would fill the empty space between separate shelves.
                if (mask == 5 || mask == 10)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if ((mask & (1 << i)) == 0) continue;
                        Vector2 prev = (p[i] + p[(i + 3) & 3]) * 0.5f;
                        Vector2 next = (p[i] + p[(i + 1) & 3]) * 0.5f;
                        CapTriangle(o, p[i], next, prev, y, seed);
                    }
                    continue;
                }

                // The remaining marching-squares cases have a convex contour.
                var poly = new List<Vector2>(6);
                for (int i = 0; i < 4; i++)
                {
                    bool inside = (mask & (1 << i)) != 0;
                    bool nextInside = (mask & (1 << ((i + 1) & 3))) != 0;
                    if (inside) poly.Add(p[i]);
                    if (inside != nextInside) poly.Add((p[i] + p[(i + 1) & 3]) * 0.5f);
                }
                for (int i = 1; i + 1 < poly.Count; i++)
                    CapTriangle(o, poly[0], poly[i], poly[i + 1], y, seed);
            }
        }

        static void CapTriangle(Output o, Vector2 a, Vector2 b, Vector2 c, float y, int seed)
        {
            // CCW in x/z points downward in Unity: reverse for upward normals.
            Tri(o, SubGrass, V(a, y), V(c, y), V(b, y),
                Shade(1f, 0f, a.x, y, a.y, seed),
                Shade(1f, 0f, c.x, y, c.y, seed),
                Shade(1f, 0f, b.x, y, b.y, seed));
        }

        // ------------------------------------------------------------------ grasbolletjes
        // Grasbolletjes alleen langs de randen en op kleine stukken: een grote vlakte blijft gewoon gras.
        // Kans per cel hangt af van de afstand (in cellen) tot de rand van deze trede.
        static readonly float[] TuftChanceByEdgeDistance = { 0f, 0f, 0.22f, 0.12f, 0.05f };

        static void Tufts(Output o, IReadOnlyDictionary<Vector2Int, int> cells, int tier, float top, float cs, int seed)
        {
            bool In(Vector2Int c) => cells.TryGetValue(c, out var l) && l >= tier;
            var dist = new Dictionary<Vector2Int, int>();
            var queue = new Queue<Vector2Int>();
            foreach (var kv in cells)
            {
                if (kv.Value < tier) continue;
                bool edge = false;
                for (int dz = -1; dz <= 1 && !edge; dz++)
                    for (int dx = -1; dx <= 1 && !edge; dx++)
                        if (!In(new Vector2Int(kv.Key.x + dx, kv.Key.y + dz))) edge = true;
                if (edge) { dist[kv.Key] = 1; queue.Enqueue(kv.Key); }
            }
            int maxD = TuftChanceByEdgeDistance.Length - 1;
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = dist[c];
                if (d >= maxD + 1) continue;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var nb = new Vector2Int(c.x + dx, c.y + dz);
                        if (!In(nb) || dist.ContainsKey(nb)) continue;
                        dist[nb] = d + 1;
                        queue.Enqueue(nb);
                    }
            }
            foreach (var kv in cells)
            {
                if (kv.Value != tier) continue;
                var c = kv.Key;
                if (!dist.TryGetValue(c, out int dc) || dc > maxD) continue;      // midden van een vlakte: geen bolletjes
                if (Hash(c.x, c.y, 11, seed) >= TuftChanceByEdgeDistance[dc]) continue;
                float s = 0.20f + Hash(c.x, c.y, 12, seed) * 0.14f;
                var center = new Vector3((c.x + (Hash(c.x, c.y, 13, seed) - 0.5f) * 0.5f) * cs, top + 0.12f + s * 0.45f,
                                         (c.y + (Hash(c.x, c.y, 14, seed) - 0.5f) * 0.5f) * cs);
                Blob(o, center, new Vector3(s * 1.1f, s * 0.95f, s), -s * 0.5f, c.x * 31 + c.y * 17, seed);
            }
        }

        static void Blob(Output o, Vector3 center, Vector3 r, float flatBottom, int salt, int seed)
        {
            const int seg = 7, rings = 4;
            var pts = new Vector3[rings + 1, seg];
            for (int i = 0; i <= rings; i++)
            {
                float phi = Mathf.PI * i / rings;
                for (int k = 0; k < seg; k++)
                {
                    float th = 2f * Mathf.PI * k / seg;
                    int key = i == 0 || i == rings ? 0 : k;
                    var j = new Vector3(Hash(salt, i * 8 + key, 21, seed) - 0.5f, Hash(salt, i * 8 + key, 22, seed) - 0.5f,
                                        Hash(salt, i * 8 + key, 23, seed) - 0.5f) * 0.2f;
                    var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)) + j;
                    p = Vector3.Scale(p, r);
                    p.y = Mathf.Max(p.y, flatBottom);
                    pts[i, k] = center + p;
                }
            }
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < seg; k++)
                {
                    int k2 = (k + 1) % seg;
                    Vector3 a = pts[i, k], b = pts[i, k2], c = pts[i + 1, k2], d = pts[i + 1, k];
                    BlobTri(o, center, a, b, c, seed);
                    BlobTri(o, center, a, c, d, seed);
                }
        }

        static void BlobTri(Output o, Vector3 center, Vector3 a, Vector3 b, Vector3 c, int seed)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) return;
            if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f) { var t = b; b = c; c = t; }
            Tri(o, SubBush, a, b, c, Shade(1f, 0.3f, a.x, a.y, a.z, seed), Shade(1f, 0.3f, b.x, b.y, b.z, seed),
                Shade(1f, 0.3f, c.x, c.y, c.z, seed));
        }

        // ------------------------------------------------------------------ hulpfuncties
        static Vector3 V(Vector2 p, float y) => new Vector3(p.x, y, p.y);

        // Platte facetten: elke driehoek eigen hoekpunten met de vlaknormaal.
        static void Tri(Output o, int sub, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            n.Normalize();
            int i = o.vertices.Count;
            o.vertices.Add(a); o.vertices.Add(b); o.vertices.Add(c);
            o.normals.Add(n); o.normals.Add(n); o.normals.Add(n);
            o.colors.Add(ca); o.colors.Add(cb); o.colors.Add(cc);
            o.triangles[sub].Add(i); o.triangles[sub].Add(i + 1); o.triangles[sub].Add(i + 2);
        }

        // Vertexkleur zoals bake_shading: R = occlusie, G = randlicht, B = verfvariatie (0,5 neutraal), A = 1.
        static Color Shade(float ao, float edge, float x, float y, float z, int seed)
        {
            float nz = ValueNoise(x * 0.8f, y * 0.8f, z * 0.8f, seed);
            return new Color(Mathf.Clamp01(ao), edge, Mathf.Clamp01((nz - 0.5f) * 1.8f + 0.5f), 1f);
        }

        static float ValueNoise(float x, float y, float z, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float fx = x - xi, fy = y - yi, fz = z - zi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); fz = fz * fz * (3f - 2f * fz);
            float L(int dx, int dy, int dz) => Hash(xi + dx, (yi + dy) * 7919 + zi + dz, 31, seed);
            float x00 = Mathf.Lerp(L(0, 0, 0), L(1, 0, 0), fx), x10 = Mathf.Lerp(L(0, 1, 0), L(1, 1, 0), fx);
            float x01 = Mathf.Lerp(L(0, 0, 1), L(1, 0, 1), fx), x11 = Mathf.Lerp(L(0, 1, 1), L(1, 1, 1), fx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, fy), Mathf.Lerp(x01, x11, fy), fz);
        }

        public static float Hash(int x, int z, int salt, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u; h = (h << 13) | (h >> 19);
                h ^= (uint)z * 0xC2B2AE3Du; h = (h << 17) | (h >> 15);
                h ^= (uint)salt * 0x27D4EB2Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static float Area(Vector2[] p)
        {
            float a = 0f;
            for (int i = 0; i < p.Length; i++) a += Cross(p[i], p[(i + 1) % p.Length]);
            return a * 0.5f;
        }

        static float Perimeter(Vector2[] p)
        {
            float l = 0f;
            for (int i = 0; i < p.Length; i++) l += Vector2.Distance(p[i], p[(i + 1) % p.Length]);
            return l;
        }


    }
}
