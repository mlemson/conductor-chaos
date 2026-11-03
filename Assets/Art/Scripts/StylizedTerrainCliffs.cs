// Conductor Chaos — klifranden op een Unity-terrein (issue #38).
// Alleen waar is geschilderd (Cliff Painter of Unity's terreinpenselen, het 'klifmasker') en het terrein steiler is dan 'steepAngle',
// komt er een gefacetteerde rotswand (M_Env_Cliff) over de helling,
// en langs de bovenrand een dikke grasrand (M_Env_Grass) die net over de rand hangt: hetzelfde ontwerp als de klifmodules.
// De mesh wordt uit de hoogtekaart opgebouwd (bij laden en, in de editor, live tijdens het boetseren) en niet opgeslagen,
// dus de scène en Git blijven klein. Puur decor: geen collider (de TerrainCollider blijft de ondergrond),
// geen gedeelde toestand; elke client bouwt hetzelfde resultaat uit dezelfde terreindata.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConductorChaos.Art
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class StylizedTerrainCliffs : MonoBehaviour
    {
        [Tooltip("Hellingen steiler dan deze hoek worden klif.")]
        [Range(20f, 75f)] public float steepAngle = 40f;
        [Tooltip("Grootte van de facetten, in stappen van de hoogtekaart (1 = ongeveer 0,5 m bij een terrein van 120 m). " +
                 "Groter = grovere facetten, maar het terrein kan dan door de wand prikken.")]
        [Range(1, 4)] public int facetStep = 1;
        [Tooltip("Hoe ver de rotswand golft (verticale plooien), in meter.")]
        [Range(0f, 0.6f)] public float folds = 0.25f;
        [Tooltip("Hoe ver het gras over de rand hangt, in meter.")]
        [Range(0.1f, 1f)] public float grassOverhang = 0.5f;
        [Tooltip("Dikte van de grasrand, in meter.")]
        [Range(0.05f, 0.6f)] public float grassThickness = 0.3f;
        public Material cliffMaterial;
        public Material grassMaterial;

        // klifmasker: 1 = hier is geschilderd (per punt van de hoogtekaart). Alleen daar komen kliffen,
        // zodat de rand met de kwast meegroeit en niet in één keer over het hele terrein verschijnt.
        [SerializeField, HideInInspector] byte[] cliffMask;
        [SerializeField, HideInInspector] int cliffMaskRes;
        [SerializeField, HideInInspector] bool maskInitialised;

        /// <summary>Markeer een punt van de hoogtekaart als klifgebied (aanroepen vanuit de Cliff Painter).</summary>
        public void MarkCliff(int x, int z, int heightmapResolution)
        {
            EnsureMask(heightmapResolution);
            if (x < 0 || z < 0 || x >= cliffMaskRes || z >= cliffMaskRes) return;
            cliffMask[z * cliffMaskRes + x] = 1;
        }

        /// <summary>Markeer een heel stuk van de hoogtekaart als klifgebied (aanroepen bij Unity's terreinpenselen).</summary>
        public void MarkRegion(RectInt region, int heightmapResolution)
        {
            EnsureMask(heightmapResolution);
            for (int z = Mathf.Max(region.yMin, 0); z < Mathf.Min(region.yMax, cliffMaskRes); z++)
                for (int x = Mathf.Max(region.xMin, 0); x < Mathf.Min(region.xMax, cliffMaskRes); x++)
                    cliffMask[z * cliffMaskRes + x] = 1;
        }

        /// <summary>Zorg voor een klifmasker; een nieuw masker is leeg, zodat kliffen alleen verschijnen waar je schildert.</summary>
        public void EnsureMask(int heightmapResolution)
        {
            if (cliffMask == null || cliffMaskRes != heightmapResolution)
            {
                cliffMask = new byte[heightmapResolution * heightmapResolution];
                cliffMaskRes = heightmapResolution;
            }
            maskInitialised = true;
        }

        /// <summary>True terwijl er gebouwd wordt (de editor negeert dan terreinmeldingen die het bouwen zelf veroorzaakt).</summary>
        public static bool IsBuilding { get; private set; }

        Mesh mesh;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> rockTris = new List<int>();
        readonly List<int> grassTris = new List<int>();

        void OnEnable() => Rebuild();

        void OnDisable()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Rebuild(); };
        }
#endif

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            var terrain = GetComponentInParent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return;

            if (transform.parent == terrain.transform)
            {
                // Unity tekent een terrein altijd op schaal 1: een schaal op het terreinobject hier opheffen, anders verschuift de rand
                var ps = terrain.transform.lossyScale;
                var unscaled = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);
                if (transform.localPosition != Vector3.zero || transform.localRotation != Quaternion.identity || transform.localScale != unscaled)
                {
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                    transform.localScale = unscaled;
                }
            }
            if (mesh == null)
                mesh = new Mesh { name = "StylizedCliffs", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;
            var mr = GetComponent<MeshRenderer>();
            if (cliffMaterial != null && grassMaterial != null)
            {
                var cur = mr.sharedMaterials;
                if (cur.Length != 2 || cur[0] != cliffMaterial || cur[1] != grassMaterial)
                    mr.sharedMaterials = new[] { cliffMaterial, grassMaterial };
            }

            IsBuilding = true;
            try { Build(terrain.terrainData); }
            finally { IsBuilding = false; }
        }

        void Build(TerrainData td)
        {
#if UNITY_EDITOR
            td.SyncHeightmap();                                    // penseelstreken staan eerst alleen op de GPU
#endif
            int res = td.heightmapResolution;
            float[,] H = td.GetHeights(0, 0, res, res);
            Vector3 size = td.size;
            int s = Mathf.Clamp(facetStep, 1, 8);
            int n = (res - 1) / s;                                 // aantal vakjes per zijde
            float cx = size.x / (res - 1) * s, cz = size.z / (res - 1) * s;
            float tan = Mathf.Tan(Mathf.Clamp(steepAngle, 10f, 85f) * Mathf.Deg2Rad);
            float tanHalf = Mathf.Tan(Mathf.Clamp(steepAngle, 10f, 85f) * 0.5f * Mathf.Deg2Rad);

            // terreinen van vóór het masker: alleen de harde treden (> 65°, zoals de Cliff Painter ze maakt) overnemen
            if (!maskInitialised)
            {
                maskInitialised = true;
                cliffMask = new byte[res * res];
                cliffMaskRes = res;
                float hard = Mathf.Tan(65f * Mathf.Deg2Rad) * (size.x / (res - 1));
                for (int z = 0; z < res - 1; z++)
                    for (int x = 0; x < res - 1; x++)
                        if (Mathf.Abs(H[z, x + 1] - H[z, x]) * size.y > hard || Mathf.Abs(H[z + 1, x] - H[z, x]) * size.y > hard)
                        { cliffMask[z * res + x] = 1; cliffMask[z * res + x + 1] = 1; cliffMask[(z + 1) * res + x] = 1; }
            }
            bool Allowed(int hx, int hz)
            {
                if (cliffMask == null || cliffMaskRes != res) return false;
                for (int z = Mathf.Max(hz - 2, 0); z <= Mathf.Min(hz + 2, res - 1); z++)
                    for (int x = Mathf.Max(hx - 2, 0); x <= Mathf.Min(hx + 2, res - 1); x++)
                        if (cliffMask[z * res + x] != 0) return true;
                return false;
            }

            float Hm(int i, int j) => H[Mathf.Clamp(j * s, 0, res - 1), Mathf.Clamp(i * s, 0, res - 1)] * size.y;
            Vector3 Base(int i, int j) => new Vector3(i * cx, Hm(i, j), j * cz);
            float HeightAt(Vector3 p) => td.GetInterpolatedHeight(Mathf.Clamp01(p.x / size.x), Mathf.Clamp01(p.z / size.z));

            // richting de helling af (horizontaal, over 2 vakjes gemeten zodat het rustig blijft)
            Vector3 Down(int i, int j)
            {
                int i0 = Mathf.Max(i - 2, 0), i1 = Mathf.Min(i + 2, n), j0 = Mathf.Max(j - 2, 0), j1 = Mathf.Min(j + 2, n);
                float gx = (Hm(i1, j) - Hm(i0, j)) / ((i1 - i0) * cx);
                float gz = (Hm(i, j1) - Hm(i, j0)) / ((j1 - j0) * cz);
                var d = new Vector3(-gx, 0f, -gz);
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.zero;
            }

            // alleen rond het klifmasker rekenen: zo is opbouwen snel genoeg om live mee te lopen tijdens een streek
            int iMin = n, iMax = -1, jMin = n, jMax = -1;
            if (cliffMask != null && cliffMaskRes == res)
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                        if (cliffMask[z * res + x] != 0)
                        {
                            iMin = Mathf.Min(iMin, x / s); iMax = Mathf.Max(iMax, x / s);
                            jMin = Mathf.Min(jMin, z / s); jMax = Mathf.Max(jMax, z / s);
                        }
            if (iMax < 0) { iMin = 0; jMin = 0; jMax = -1; }       // leeg masker: niets te doen
            else
            {
                int margin = 2 / s + 6;                            // bereik van Allowed plus het opvullen van de steile vakjes
                iMin = Mathf.Max(iMin - margin, 0); iMax = Mathf.Min(iMax + margin, n - 1);
                jMin = Mathf.Max(jMin - margin, 0); jMax = Mathf.Min(jMax + margin, n - 1);
            }

            // 1) welke vakjes zijn steil
            var steep = new bool[n, n];
            var slope2 = new float[n, n];
            var avg = new float[n, n];
            for (int j = jMin; j <= jMax; j++)
                for (int i = iMin; i <= iMax; i++)
                {
                    float h00 = Hm(i, j), h10 = Hm(i + 1, j), h01 = Hm(i, j + 1), h11 = Hm(i + 1, j + 1);
                    float gx = Mathf.Max(Mathf.Abs(h10 - h00), Mathf.Abs(h11 - h01)) / cx;
                    float gz = Mathf.Max(Mathf.Abs(h01 - h00), Mathf.Abs(h11 - h10)) / cz;
                    avg[i, j] = 0.25f * (h00 + h10 + h01 + h11);
                    slope2[i, j] = gx * gx + gz * gz;
                    steep[i, j] = slope2[i, j] > tan * tan;
                }
            int SteepAround(bool[,] m, int i, int j)
            {
                int c = 0;
                for (int b = Mathf.Max(j - 1, 0); b <= Mathf.Min(j + 1, n - 1); b++)
                    for (int a = Mathf.Max(i - 1, 0); a <= Mathf.Min(i + 1, n - 1); a++)
                        if ((a != i || b != j) && m[a, b]) c++;
                return c;
            }
            // gaatjes dicht en losse spikkels weg
            var st2 = (bool[,])steep.Clone();
            for (int j = jMin; j <= jMax; j++)
                for (int i = iMin; i <= iMax; i++)
                {
                    int c = SteepAround(steep, i, j);
                    if (!steep[i, j] && c >= 5) st2[i, j] = true;
                    if (steep[i, j] && c <= 1) st2[i, j] = false;
                }
            // de bolle overgang naast een klif (half zo steil) hoort er ook bij, anders prikt het terrein door de wand
            steep = (bool[,])st2.Clone();
            for (int j = jMin; j <= jMax; j++)
                for (int i = iMin; i <= iMax; i++)
                    if (!st2[i, j] && slope2[i, j] > tanHalf * tanHalf && SteepAround(st2, i, j) > 0) steep[i, j] = true;

            verts.Clear(); cols.Clear(); rockTris.Clear(); grassTris.Clear();

            // 2) bovenranden: randen tussen een steil vakje en een hoger, vlak buurvakje
            var lipEdges = new List<(int a, int b)>();
            int Key(int i, int j) => j * (n + 1) + i;
            void Edge(int qi, int qj, int ni, int nj, int ai, int aj, int bi, int bj)
            {
                if (ni < 0 || nj < 0 || ni >= n || nj >= n) return;
                if (steep[ni, nj] || avg[ni, nj] <= avg[qi, qj]) return;
                if (!Allowed(ai * s, aj * s) || !Allowed(bi * s, bj * s)) return;   // alleen waar met de Cliff Painter is geschilderd
                lipEdges.Add((Key(ai, aj), Key(bi, bj)));
            }
            for (int j = jMin; j <= jMax; j++)
                for (int i = iMin; i <= iMax; i++)
                {
                    if (!steep[i, j]) continue;
                    Edge(i, j, i, j + 1, i, j + 1, i + 1, j + 1);
                    Edge(i, j, i, j - 1, i, j, i + 1, j);
                    Edge(i, j, i + 1, j, i + 1, j, i + 1, j + 1);
                    Edge(i, j, i - 1, j, i, j, i, j + 1);
                }

            // 3) randlijnen: losse snippers (minder dan 4 randen) weg, dan gladstrijken (trapjes van het raster worden vloeiend)
            var nb = new Dictionary<int, List<int>>();
            void Link(int a, int b)
            {
                if (!nb.TryGetValue(a, out var l)) nb[a] = l = new List<int>(2);
                if (!l.Contains(b)) l.Add(b);
            }
            foreach (var e in lipEdges) { Link(e.a, e.b); Link(e.b, e.a); }
            var comp = new Dictionary<int, int>(nb.Count);
            var compEdges = new List<int>();
            foreach (var start in nb.Keys)
            {
                if (comp.ContainsKey(start)) continue;
                int id = compEdges.Count;
                compEdges.Add(0);
                var stack = new Stack<int>();
                stack.Push(start);
                comp[start] = id;
                while (stack.Count > 0)
                {
                    int q = stack.Pop();
                    foreach (int r in nb[q])
                        if (!comp.ContainsKey(r)) { comp[r] = id; stack.Push(r); }
                }
            }
            foreach (var e in lipEdges) compEdges[comp[e.a]]++;
            lipEdges.RemoveAll(e => compEdges[comp[e.a]] < 4);
            var pos = new Dictionary<int, Vector3>(nb.Count);
            var dir = new Dictionary<int, Vector3>(nb.Count);
            foreach (var k in nb.Keys) { int i = k % (n + 1), j = k / (n + 1); pos[k] = Base(i, j); dir[k] = Down(i, j); }
            for (int it = 0; it < 3; it++)
            {
                var p2 = new Dictionary<int, Vector3>(pos.Count);
                var d2 = new Dictionary<int, Vector3>(dir.Count);
                foreach (var kv in nb)
                {
                    int k = kv.Key;
                    if (kv.Value.Count == 2)
                    {
                        int q1 = kv.Value[0], q2 = kv.Value[1];
                        var p = pos[k] * 0.5f + (pos[q1] + pos[q2]) * 0.25f;
                        p.y = pos[k].y;
                        p2[k] = p;
                        var d = dir[k] * 0.5f + (dir[q1] + dir[q2]) * 0.25f;
                        d2[k] = d.sqrMagnitude > 1e-6f ? d.normalized : dir[k];
                    }
                    else { p2[k] = pos[k]; d2[k] = dir[k]; }
                }
                pos = p2; dir = d2;
            }

            // 4) voet van de wand per randpunt: langs de helling omlaag tot het minstens 0,75 m vlak blijft
            //    (een smal richeltje telt niet als voet), daarna de voeten langs de rand gladstrijken
            float flatDrop = 0.25f * tanHalf;
            Vector3 FootOf(int k)
            {
                var a = pos[k];
                var d = dir[k];
                float prev = a.y;
                int flat = 0;
                var first = a;
                for (int step = 1; step <= 80; step++)
                {
                    var q = a + d * (step * 0.25f);
                    float hq = HeightAt(q);
                    if (prev - hq < flatDrop)
                    {
                        if (flat == 0) { first = q; first.y = hq; }
                        flat++;
                        if (flat >= 3 && step > 3) return first;
                    }
                    else flat = 0;
                    prev = hq;
                }
                var end = a + d * 20f;
                end.y = HeightAt(end);
                return end;
            }
            var foot = new Dictionary<int, Vector3>(pos.Count);
            foreach (var k in pos.Keys) foot[k] = FootOf(k);
            for (int it = 0; it < 2; it++)
            {
                var f2 = new Dictionary<int, Vector3>(foot.Count);
                foreach (var kv in nb)
                {
                    int k = kv.Key;
                    if (kv.Value.Count == 2) f2[k] = foot[k] * 0.5f + (foot[kv.Value[0]] + foot[kv.Value[1]]) * 0.25f;
                    else f2[k] = foot[k];
                }
                foot = f2;
            }

            // 5) rotswand: kolom van de top naar de voet in een paar rijen, iets naar buiten met plooien,
            //    en altijd vóór het terrein (anders prikt het gras door de wand)
            const int rows = 3;
            var column = new Dictionary<int, Vector3[]>(pos.Count);
            var drop = new Dictionary<int, float>(pos.Count);
            foreach (var k in pos.Keys)
            {
                var a = pos[k];
                var d = dir[k];
                var F = foot[k];
                float h = a.y - F.y;
                drop[k] = h;
                var col = new Vector3[rows + 1];
                for (int r = 0; r <= rows; r++)
                {
                    float t = r / (float)rows;
                    var P = Vector3.Lerp(a, F, t);
                    P.y = a.y - 0.02f - (h + 0.5f) * t;
                    float f = Mathf.PerlinNoise(P.x * 0.45f + 17.3f, P.z * 0.45f + 3.1f);
                    P += d * ((0.12f + folds * f) * (r == 0 ? 0.5f : 1f));
                    if (r > 0 && r < rows) P.y += (Mathf.PerlinNoise(P.x * 0.9f + 50f, P.z * 0.9f) - 0.5f) * 0.25f;
                    for (int guard = 0; guard < 12 && r < rows && HeightAt(P) > P.y - 0.06f; guard++) P += d * 0.12f;
                    col[r] = P;
                }
                column[k] = col;
            }

            var grassTop = new Color(1f, 0f, 0.5f, 1f);
            var grassEdge = new Color(0.95f, 1f, 0.5f, 1f);
            var grassUnder = new Color(0.55f, 0f, 0.5f, 1f);
            var grassBack = new Color(0.4f, 0f, 0.5f, 1f);
            var lip = new Dictionary<int, (Vector3 inner, Vector3 outer, Vector3 under, Vector3 back)>(pos.Count);
            foreach (var k in pos.Keys)
            {
                var a = pos[k];
                var d = dir[k];
                float r = Mathf.PerlinNoise(a.x * 0.7f + 9.1f, a.z * 0.7f + 2.3f);
                var inner = a - d * 0.35f;
                inner.y = Mathf.Max(HeightAt(inner), a.y) + 0.05f;
                var outer = a + d * (grassOverhang * (0.8f + 0.4f * r));
                outer.y = a.y + 0.04f;
                var under = outer - Vector3.up * (grassThickness * (0.7f + 0.6f * r)) - d * 0.06f;
                var back = a - d * 0.15f - Vector3.up * grassThickness;
                lip[k] = (inner, outer, under, back);
            }

            foreach (var e in lipEdges)
            {
                if ((drop[e.a] + drop[e.b]) * 0.5f < 0.6f) continue;    // te laag voor een klif
                var outward = dir[e.a] + dir[e.b];
                var ca = column[e.a];
                var cb = column[e.b];
                for (int r = 0; r < rows; r++)
                {
                    float t0 = r / (float)rows, t1 = (r + 1) / (float)rows;
                    var c0 = new Color(0.65f + 0.35f * (1f - t0), 0f, 0.5f, 1f);
                    var c1 = new Color(0.65f + 0.35f * (1f - t1), 0f, 0.5f, 1f);
                    Quad(rockTris, ca[r], cb[r], cb[r + 1], ca[r + 1], c0, c0, c1, c1, outward);
                }
                var A = lip[e.a];
                var B = lip[e.b];
                Quad(grassTris, A.inner, B.inner, B.outer, A.outer, grassTop, grassTop, grassEdge, grassEdge, Vector3.up);
                Quad(grassTris, A.outer, B.outer, B.under, A.under, grassEdge, grassEdge, grassUnder, grassUnder, outward);
                Quad(grassTris, A.under, B.under, B.back, A.back, grassUnder, grassUnder, grassBack, grassBack, Vector3.down);
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(rockTris, 0, false);
            mesh.SetTriangles(grassTris, 1, false);
            mesh.RecalculateNormals();                             // losse hoekpunten per driehoek: harde facetten
            mesh.RecalculateBounds();
        }

        // vierhoek als twee driehoeken; met 'want' wordt de draairichting zo gekozen dat de voorkant die kant op wijst
        void Quad(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd, Vector3? want)
        {
            if (want.HasValue && Vector3.Dot(Vector3.Cross(b - a, c - a), want.Value) < 0f)
            {
                (b, d) = (d, b);
                (cb, cd) = (cd, cb);
            }
            Tri(tris, a, b, c, ca, cb, cc);
            Tri(tris, a, c, d, ca, cc, cd);
        }

        void Tri(List<int> tris, Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            cols.Add(ca); cols.Add(cb); cols.Add(cc);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }
    }
}
