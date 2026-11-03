// Conductor Chaos — gestileerd stilstaand water (issue #42).
// Een plat wateroppervlak met grote, net iets schuine facetten (beweegt niet), een trigger-volume eronder,
// en twee effecten:
//   - Zinken: Rigidbodies in het water krijgen een opwaartse kracht kleiner dan de zwaartekracht plus veel weerstand,
//     dus wie erin valt zakt langzaam naar de bodem. Alleen voor niet-kinematische bodies, dus alleen waar de physics
//     autoritatief draait (host/server; op clients zijn de netwerk-bodies kinematisch en volgen ze de host).
//   - Onder water (lokaal, per camera): dichte donkerblauwe mist en een blauwe waas over het hele beeld.
// Het oppervlak wordt bij laden uit de instellingen opgebouwd en niet opgeslagen. Maak water via
// Conductor Chaos > Art > Water > Create Water (laag 'Ignore Raycast', zodat grond- en grijpstralen er niet op stuiten).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConductorChaos.Art
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public class StylizedWater : MonoBehaviour
    {
        [Header("Vorm")]
        [Tooltip("Breedte (x) en lengte (z) van het water in meter. Het oppervlak ligt op de positie van dit object.")]
        public Vector2 size = new Vector2(12f, 12f);
        [Tooltip("Diepte van het watervolume onder het oppervlak (m). Graaf het terrein minstens zo diep uit.")]
        [Min(0.2f)] public float depth = 3f;
        [Tooltip("Grootte van de facetten op het oppervlak (m).")]
        [Range(0.5f, 4f)] public float facetSize = 1.4f;
        public Material surfaceMaterial;

        [Header("Zinken")]
        [Tooltip("Opwaartse kracht als deel van de zwaartekracht. Onder 1 zak je langzaam weg.")]
        [Range(0f, 1.5f)] public float buoyancy = 0.7f;
        [Tooltip("Weerstand in het water: hoger = trager bewegen en zinken.")]
        [Range(0f, 10f)] public float waterDrag = 3f;

        [Header("Onder water")]
        public Shader tintShader;
        public Color underwaterTint = new Color(0.12f, 0.20f, 0.45f, 0.55f);
        public Color underwaterFog = new Color(0.10f, 0.17f, 0.36f, 1f);
        [Range(0.01f, 0.6f)] public float underwaterFogDensity = 0.18f;

        internal static readonly List<StylizedWater> Active = new List<StylizedWater>();

        Mesh mesh;
        readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();

        public float SurfaceY => transform.position.y;

        /// <summary>Ligt dit wereldpunt in het watervolume (onder het oppervlak)?</summary>
        public bool Contains(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) <= size.x * 0.5f && Mathf.Abs(l.z) <= size.y * 0.5f && l.y <= 0f && l.y >= -depth;
        }

        void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            UnderwaterView.Hook();
            Rebuild();
        }

        void OnDisable()
        {
            Active.Remove(this);
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
            var box = GetComponent<BoxCollider>();
            var center = new Vector3(0f, -depth * 0.5f, 0f);
            var extent = new Vector3(size.x, depth, size.y);
            if (!box.isTrigger) box.isTrigger = true;
            if (box.center != center) box.center = center;
            if (box.size != extent) box.size = extent;

            var mr = GetComponent<MeshRenderer>();
            if (surfaceMaterial != null && mr.sharedMaterial != surfaceMaterial) mr.sharedMaterial = surfaceMaterial;
            if (mr.shadowCastingMode != ShadowCastingMode.Off) mr.shadowCastingMode = ShadowCastingMode.Off;

            if (mesh == null)
                mesh = new Mesh { name = "StylizedWaterSurface", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh != mesh) mf.sharedMesh = mesh;
            BuildSurface();
        }

        void BuildSurface()
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(size.x / facetSize));
            int nz = Mathf.Max(1, Mathf.CeilToInt(size.y / facetSize));
            float cx = size.x / nx, cz = size.y / nz;
            var grid = new Vector3[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = -size.x * 0.5f + i * cx, z = -size.y * 0.5f + j * cz;
                    bool edge = i == 0 || j == 0 || i == nx || j == nz;
                    if (!edge)                                          // binnenpunten iets verschuiven: onregelmatige facetten
                    {
                        x += (Mathf.PerlinNoise(i * 0.71f + 3.3f, j * 0.53f) - 0.5f) * cx * 0.5f;
                        z += (Mathf.PerlinNoise(i * 0.47f, j * 0.69f + 8.1f) - 0.5f) * cz * 0.5f;
                    }
                    float y = edge ? 0f : (Mathf.PerlinNoise(x * 0.6f + 11f, z * 0.6f + 5f) - 0.5f) * 0.06f;
                    grid[j * (nx + 1) + i] = new Vector3(x, y, z);
                }

            var verts = new List<Vector3>(nx * nz * 6);
            var cols = new List<Color>(nx * nz * 6);
            var tris = new List<int>(nx * nz * 6);
            var shade = new Color(1f, 0f, 0.5f, 1f);
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                cols.Add(shade); cols.Add(shade); cols.Add(shade);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    var p00 = grid[j * (nx + 1) + i];
                    var p10 = grid[j * (nx + 1) + i + 1];
                    var p01 = grid[(j + 1) * (nx + 1) + i];
                    var p11 = grid[(j + 1) * (nx + 1) + i + 1];
                    if (((i + j) & 1) == 0) { Tri(p00, p01, p11); Tri(p00, p11, p10); }   // diagonaal afwisselen
                    else { Tri(p00, p01, p10); Tri(p10, p01, p11); }
                }
            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // --------------------------------------------------------------- zinken
        void FixedUpdate() => pushed.Clear();

        void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            // kinematische bodies (clients, bewegende trein) niet: die volgen de autoritatieve simulatie
            if (rb == null || rb.isKinematic || !pushed.Add(rb)) return;
            float under = SurfaceY - rb.worldCenterOfMass.y;
            float k = Mathf.Clamp01((under + 0.3f) / 0.6f);           // zacht van 'net aan het oppervlak' naar 'onder'
            if (k <= 0f) return;
            rb.AddForce(-Physics.gravity * (buoyancy * k), ForceMode.Acceleration);
            rb.AddForce(-rb.linearVelocity * (waterDrag * k), ForceMode.Acceleration);
            rb.AddTorque(-rb.angularVelocity * (waterDrag * 0.5f * k), ForceMode.Acceleration);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, -depth * 0.5f, 0f), new Vector3(size.x, depth, size.y));
        }
    }

    /// <summary>Onderwaterbeeld per camera: mist en een blauwe waas, alleen tijdens het renderen van die camera.</summary>
    static class UnderwaterView
    {
        static bool hooked;
        static GameObject tint;
        static Material tintMaterial;
        static bool restoreFog;
        static bool savedFog;
        static Color savedFogColor;
        static FogMode savedFogMode;
        static float savedFogDensity;

        public static void Hook()
        {
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.beginCameraRendering += Begin;
            RenderPipelineManager.endCameraRendering += End;
        }

        static void Begin(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || cam.cameraType == CameraType.Reflection) return;
            StylizedWater water = null;
            var p = cam.transform.position;
            foreach (var w in StylizedWater.Active)
                if (w != null && w.isActiveAndEnabled && w.Contains(p)) { water = w; break; }
            if (water == null)
            {
                if (tint != null && tint.activeSelf) tint.SetActive(false);
                return;
            }

            if (Application.isPlaying)                                   // in de editor de scène-instellingen niet aanraken
            {
                restoreFog = true;
                savedFog = RenderSettings.fog;
                savedFogColor = RenderSettings.fogColor;
                savedFogMode = RenderSettings.fogMode;
                savedFogDensity = RenderSettings.fogDensity;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogColor = water.underwaterFog;
                RenderSettings.fogDensity = water.underwaterFogDensity;
            }

            if (EnsureTint(water.tintShader))
            {
                tintMaterial.color = water.underwaterTint;
                tint.transform.position = p;
                if (!tint.activeSelf) tint.SetActive(true);
            }
        }

        static void End(ScriptableRenderContext context, Camera cam)
        {
            if (restoreFog)
            {
                RenderSettings.fog = savedFog;
                RenderSettings.fogColor = savedFogColor;
                RenderSettings.fogMode = savedFogMode;
                RenderSettings.fogDensity = savedFogDensity;
                restoreFog = false;
            }
            if (tint != null && tint.activeSelf) tint.SetActive(false);
        }

        static bool EnsureTint(Shader shader)
        {
            if (tint != null && tintMaterial != null) return true;
            if (shader == null) return false;
            tintMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var quad = new Mesh { name = "UnderwaterTintQuad", hideFlags = HideFlags.HideAndDontSave };
            quad.SetVertices(new List<Vector3> { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) });
            quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            quad.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);   // nooit weggeculld; de shader tekent schermvullend
            tint = new GameObject("UnderwaterTint") { hideFlags = HideFlags.HideAndDontSave };
            tint.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = tint.AddComponent<MeshRenderer>();
            mr.sharedMaterial = tintMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            tint.SetActive(false);
            return true;
        }
    }
}
