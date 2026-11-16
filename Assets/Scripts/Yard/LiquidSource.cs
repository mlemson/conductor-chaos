// Conductor Chaos — straal water of olie (issue #237): uit de vulpijp van de watertoren of uit de spuitmond aan de
// slang. Licht gehouden: een buisje dat elke frame langs een valboog wordt opgebouwd (StylizedRamp met stroomstrepen),
// spetters waar hij neerkomt en een plasje dat groeit. Komt de straal in de vulopening van een LiquidTank, dan vult
// de server die tank. Of er iets stroomt volgt uit de stand van de hendels (HandLever), die al gesynchroniseerd is:
// clients tekenen de straal dus zelf, zonder extra netwerkverkeer.
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ConductorChaos.Yard
{
    [DisallowMultipleComponent]
    public sealed class LiquidSource : MonoBehaviour
    {
        [SerializeField] LiquidKind kind = LiquidKind.Water;
        [Tooltip("Lege knoop waar de vloeistof uitkomt (Point_WaterOut).")]
        [SerializeField] Transform outlet;
        [Tooltip("Richting van de straal in de ruimte van de uitloop.")]
        [SerializeField] Vector3 localDirection = Vector3.down;
        [Tooltip("Kraan of handwiel: stroomt vanaf deze stand (Normalized).")]
        [SerializeField] HandLever valve;
        [SerializeField] float openAt = 0.3f;
        [Tooltip("Optioneel: moet ook in deze stand staan (bijv. vulpijp omlaag).")]
        [SerializeField] HandLever gate;
        [SerializeField] float gateAt = 0.7f;
        [SerializeField] float litersPerSecond = 90f;
        [SerializeField] float exitSpeed = 1.6f;
        [SerializeField] float radius = 0.06f;

        const int Sides = 7, MaxSteps = 70, Droplets = 12;
        const float Step = 0.025f;

        readonly List<Vector3> path = new();
        readonly RaycastHit[] hits = new RaycastHit[8];
        Mesh mesh, drop;
        GameObject stream, puddle;
        MeshRenderer streamRenderer, puddleRenderer;
        readonly Transform[] droplets = new Transform[Droplets];
        readonly Vector3[] dropVelocity = new Vector3[Droplets];
        readonly float[] dropLife = new float[Droplets];
        MaterialPropertyBlock block;
        float amount, puddleSize, spawnTimer;
        Vector3 impact, impactNormal = Vector3.up;
        bool hitGround;
        LiquidTank target;
        Collider[] own;

        public LiquidKind Kind => kind;
        public bool Flowing => outlet && (!valve || valve.Normalized >= openAt) && (!gate || gate.Normalized >= gateAt);

        public void Setup(LiquidKind liquid, Transform from, Vector3 direction, HandLever tap, HandLever requires, float rate, float speed, float width)
        {
            kind = liquid;
            outlet = from;
            localDirection = direction;
            valve = tap;
            gate = requires;
            litersPerSecond = rate;
            exitSpeed = speed;
            radius = width;
        }

        static bool Authority => !NetworkManager.Singleton || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Awake()
        {
            own = GetComponentsInChildren<Collider>(true);
            block = new MaterialPropertyBlock();
            LiquidLook.Apply(block, kind, 2.4f);
        }

        void OnDestroy()
        {
            if (stream) Destroy(stream);
            if (puddle) Destroy(puddle);
            if (mesh) Destroy(mesh);
            if (drop) Destroy(drop);
        }

        void FixedUpdate()
        {
            if (target && amount > 0.5f && Authority) target.Pour(litersPerSecond * amount * Time.fixedDeltaTime, kind);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            amount = Mathf.MoveTowards(amount, Flowing ? 1f : 0f, dt * 4f);
            if (amount <= 0f)
            {
                target = null;
                if (stream) stream.SetActive(false);
                puddleSize = Mathf.MoveTowards(puddleSize, 0f, dt * 0.15f);
                UpdateDropletsAndPuddle(dt, false);
                return;
            }
            Trace();
            EnsureObjects();
            stream.SetActive(true);
            BuildTube();
            UpdateDropletsAndPuddle(dt, hitGround && !target);
        }

        void Trace()
        {
            path.Clear();
            target = null;
            hitGround = false;
            Vector3 p = outlet.position;
            Vector3 v = outlet.TransformDirection(localDirection.normalized) * exitSpeed;
            path.Add(p);
            for (int i = 0; i < MaxSteps; i++)
            {
                Vector3 q = p + v * Step + 0.5f * Physics.gravity * Step * Step;
                v += Physics.gravity * Step;
                // in de vulopening van een tank?
                foreach (var tank in LiquidTank.All)
                {
                    if (!tank.HatchOpen) continue;
                    Vector3 inlet = tank.Inlet;
                    if (p.y < inlet.y || q.y >= inlet.y) continue;
                    float t = (p.y - inlet.y) / Mathf.Max(1e-5f, p.y - q.y);
                    Vector3 x = Vector3.Lerp(p, q, t);
                    if (new Vector2(x.x - inlet.x, x.z - inlet.z).sqrMagnitude > tank.InletRadius * tank.InletRadius) continue;
                    path.Add(x);
                    path.Add(x + Vector3.down * 0.5f);                 // de hals in
                    target = tank;
                    return;
                }
                Vector3 seg = q - p;
                int n = Physics.RaycastNonAlloc(p, seg.normalized, hits, seg.magnitude, ~0, QueryTriggerInteraction.Ignore);
                float best = float.PositiveInfinity;
                RaycastHit chosen = default;
                for (int k = 0; k < n; k++)
                {
                    if (IsOwn(hits[k].collider) || hits[k].distance >= best) continue;
                    best = hits[k].distance;
                    chosen = hits[k];
                }
                if (best < float.PositiveInfinity)
                {
                    path.Add(chosen.point);
                    impact = chosen.point;
                    impactNormal = chosen.normal;
                    hitGround = chosen.normal.y > 0.6f;
                    return;
                }
                path.Add(q);
                p = q;
            }
            impact = p;
        }

        bool IsOwn(Collider c)
        {
            foreach (var o in own) if (o == c) return true;
            return false;
        }

        void EnsureObjects()
        {
            if (stream) return;
            mesh = new Mesh { name = "Liquid stream" };
            mesh.MarkDynamic();
            stream = new GameObject($"LiquidStream ({name})") { hideFlags = HideFlags.DontSave };
            stream.AddComponent<MeshFilter>().sharedMesh = mesh;
            streamRenderer = stream.AddComponent<MeshRenderer>();
            streamRenderer.sharedMaterial = LiquidLook.Material;
            streamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            streamRenderer.SetPropertyBlock(block);
            drop = Blob();
            for (int i = 0; i < Droplets; i++)
            {
                var d = new GameObject("Drop") { hideFlags = HideFlags.DontSave };
                d.transform.SetParent(stream.transform, false);
                d.AddComponent<MeshFilter>().sharedMesh = drop;
                var r = d.AddComponent<MeshRenderer>();
                r.sharedMaterial = LiquidLook.Material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.SetPropertyBlock(block);
                d.SetActive(false);
                droplets[i] = d.transform;
            }
            puddle = new GameObject($"LiquidPuddle ({name})") { hideFlags = HideFlags.DontSave };
            puddle.AddComponent<MeshFilter>().sharedMesh = drop;
            puddleRenderer = puddle.AddComponent<MeshRenderer>();
            puddleRenderer.sharedMaterial = LiquidLook.Material;
            puddleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var still = new MaterialPropertyBlock();
            LiquidLook.Apply(still, kind, 0.3f);
            puddleRenderer.SetPropertyBlock(still);
            puddle.SetActive(false);
        }

        void BuildTube()
        {
            int rings = path.Count;
            var verts = new Vector3[rings * Sides];
            var normals = new Vector3[rings * Sides];
            var tris = new int[(rings - 1) * Sides * 6];
            float t = Time.time;
            for (int i = 0; i < rings; i++)
            {
                Vector3 dir = (i < rings - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
                Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 other = Vector3.Cross(dir, side);
                float f = i / (float)Mathf.Max(1, rings - 1);
                // dikker bij de uitloop, iets dunner en wiebelend in de val
                float r = radius * amount * Mathf.Lerp(1f, 0.7f, f) * (1f + 0.12f * Mathf.Sin(t * 23f + i * 1.7f));
                for (int s = 0; s < Sides; s++)
                {
                    float a = s * Mathf.PI * 2f / Sides;
                    Vector3 n = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    verts[i * Sides + s] = path[i] + n * r;
                    normals[i * Sides + s] = n;
                }
            }
            int k = 0;
            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = i * Sides + s, b = i * Sides + (s + 1) % Sides, c = a + Sides, d = b + Sides;
                    tris[k++] = a; tris[k++] = b; tris[k++] = c;           // Cross(b-a, c-a) wijst naar buiten
                    tris[k++] = b; tris[k++] = d; tris[k++] = c;
                }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }

        void UpdateDropletsAndPuddle(float dt, bool splashing)
        {
            if (!stream) return;
            spawnTimer += dt;
            for (int i = 0; i < Droplets; i++)
            {
                var d = droplets[i];
                if (!d) continue;
                if (dropLife[i] > 0f)
                {
                    dropLife[i] -= dt;
                    dropVelocity[i] += Physics.gravity * dt;
                    d.position += dropVelocity[i] * dt;
                    d.localScale = Vector3.one * (radius * 0.9f * Mathf.Clamp01(dropLife[i] / 0.25f));
                    if (dropLife[i] <= 0f) d.gameObject.SetActive(false);
                }
                else if (splashing && amount > 0.3f && spawnTimer > 0.03f)
                {
                    spawnTimer = 0f;
                    Vector2 c = Random.insideUnitCircle;
                    dropVelocity[i] = impactNormal * Random.Range(1.0f, 2.0f) + new Vector3(c.x, 0f, c.y) * 0.9f;
                    d.position = impact + impactNormal * 0.02f;
                    dropLife[i] = Random.Range(0.18f, 0.32f);
                    d.gameObject.SetActive(true);
                }
            }
            if (!puddle) return;
            if (splashing) puddleSize = Mathf.MoveTowards(puddleSize, 0.75f, dt * 0.12f);
            puddle.SetActive(puddleSize > 0.02f);
            if (puddleSize > 0.02f && splashing)
                puddle.transform.SetPositionAndRotation(impact + impactNormal * 0.01f, Quaternion.FromToRotation(Vector3.up, impactNormal));
            puddle.transform.localScale = new Vector3(puddleSize, 0.012f, puddleSize);
        }

        /// <summary>Laag-poly bolletje (achtvlak, één keer verfijnd) voor spetters en, platgedrukt, het plasje.</summary>
        static Mesh Blob()
        {
            var v = new List<Vector3> { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[] f = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var tris = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = v.Count; v.Add(((v[a] + v[b]) * 0.5f).normalized);
                int bc = v.Count; v.Add(((v[b] + v[c]) * 0.5f).normalized);
                int ca = v.Count; v.Add(((v[c] + v[a]) * 0.5f).normalized);
                tris.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
            }
            var m = new Mesh { name = "Liquid blob" };
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            return m;
        }
    }
}
