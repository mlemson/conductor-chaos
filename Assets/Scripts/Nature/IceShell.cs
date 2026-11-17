// Conductor Chaos — ijsblok om een bevroren reiziger (issue #243): een achtkantig, iets scheef gefacetteerd blok om het
// model, met doorzichtig ijsmateriaal. Alleen presentatie; aan/uit op iedere speler via de bevroren-staat van de server.
using UnityEngine;

namespace ConductorChaos.Nature
{
    public static class IceShell
    {
        /// <summary>Maak (of vind) het ijsblok onder <paramref name="root"/>; past om alle renderers eronder.</summary>
        public static GameObject For(Transform root)
        {
            var existing = root.Find("Ijsblok");
            if (existing) return existing.gameObject;
            var bounds = LocalBounds(root);
            var go = new GameObject("Ijsblok", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = BuildMesh(bounds, Mathf.RoundToInt(root.position.x * 97f + root.position.z * 31f));
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = WinterAssets.Instance ? WinterAssets.Instance.iceShell : null;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            return go;
        }

        static Bounds LocalBounds(Transform root)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.name == "Ijsblok") continue;
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = root.InverseTransformPoint(new Vector3(i % 2 == 0 ? wb.min.x : wb.max.x,
                        (i / 2) % 2 == 0 ? wb.min.y : wb.max.y, i / 4 == 0 ? wb.min.z : wb.max.z));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            if (!any) b = new Bounds(new Vector3(0f, .6f, 0f), new Vector3(.6f, 1.2f, .6f));
            return b;
        }

        static Mesh BuildMesh(Bounds b, int seed)
        {
            var random = new System.Random(seed);
            float r = Mathf.Max(b.extents.x, b.extents.z) + .10f;
            float y0 = b.min.y - .03f, y1 = b.max.y + .10f;
            const int sides = 8;
            var rings = new Vector3[3][];
            float[] heights = { y0, Mathf.Lerp(y0, y1, .55f), y1 };
            float[] scale = { 1f, 1.06f, .82f };
            for (int k = 0; k < 3; k++)
            {
                rings[k] = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = (i + .5f) * Mathf.PI * 2f / sides;
                    float jitter = 1f + ((float)random.NextDouble() - .5f) * .14f;
                    float h = heights[k] + (k == 2 ? ((float)random.NextDouble() - .5f) * .12f : 0f);
                    rings[k][i] = new Vector3(b.center.x + Mathf.Cos(a) * r * scale[k] * jitter, h, b.center.z + Mathf.Sin(a) * r * scale[k] * jitter);
                }
            }
            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            void Quad(Vector3 a, Vector3 bb, Vector3 c, Vector3 d)
            {
                int s = v.Count;
                v.Add(a); v.Add(bb); v.Add(c); v.Add(d);
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }
            for (int k = 0; k < 2; k++)
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Quad(rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]);
                }
            Vector3 top = new(b.center.x, y1 + .05f, b.center.z), bottom = new(b.center.x, y0, b.center.z);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides, s = v.Count;
                v.Add(rings[2][i]); v.Add(rings[2][j]); v.Add(top);
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                s = v.Count;
                v.Add(rings[0][i]); v.Add(rings[0][j]); v.Add(bottom);
                t.Add(s); t.Add(s + 1); t.Add(s + 2);
            }
            var mesh = new Mesh { name = "Ijsblok" };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
