// Conductor Chaos — de slang tussen het tappunt en de spuitmond (issue #237). Getekend als een doorhangend buisje
// (iedereen tekent hem zelf uit de gesynchroniseerde stand van de spuitmond); op de server houdt een gewricht de
// spuitmond binnen de slanglengte, zodat je hem niet verder meeneemt dan de slang lang is.
using UnityEngine;

namespace ConductorChaos.Yard
{
    [DisallowMultipleComponent]
    public sealed class HoseLine : MonoBehaviour
    {
        [SerializeField] Transform start;
        [SerializeField] Transform end;
        [SerializeField] Rigidbody nozzle;
        [SerializeField] float length = 12f;
        [SerializeField] float radius = 0.035f;
        [SerializeField] Material material;

        const int Segments = 24, Sides = 6;
        Mesh mesh;
        GameObject line;
        ConfigurableJoint leash;
        readonly Vector3[] points = new Vector3[Segments + 1];

        public void Setup(Transform from, Transform to, Rigidbody body, float hoseLength, Material look)
        {
            start = from;
            end = to;
            nozzle = body;
            length = hoseLength;
            material = look;
        }

        void OnDestroy()
        {
            if (line) Destroy(line);
            if (mesh) Destroy(mesh);
        }

        void FixedUpdate()
        {
            if (!nozzle || nozzle.isKinematic || leash || !start || !end) return;
            leash = nozzle.gameObject.AddComponent<ConfigurableJoint>();
            leash.autoConfigureConnectedAnchor = false;
            leash.connectedBody = null;
            leash.anchor = nozzle.transform.InverseTransformPoint(end.position);
            leash.connectedAnchor = start.position;
            leash.xMotion = leash.yMotion = leash.zMotion = ConfigurableJointMotion.Limited;
            leash.angularXMotion = leash.angularYMotion = leash.angularZMotion = ConfigurableJointMotion.Free;
            leash.linearLimit = new SoftJointLimit { limit = length };
            leash.linearLimitSpring = new SoftJointLimitSpring { spring = 400f, damper = 40f };
        }

        void LateUpdate()
        {
            if (!start || !end) return;
            if (!line)
            {
                mesh = new Mesh { name = "Hose" };
                mesh.MarkDynamic();
                line = new GameObject($"Hose ({name})") { hideFlags = HideFlags.DontSave };
                line.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = line.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
            }
            Vector3 a = start.position, b = end.position;
            float d = Vector3.Distance(a, b);
            float sag = Mathf.Sqrt(Mathf.Max(0f, length * length - d * d)) * 0.5f;
            // de slang ligt op de grond als hij ruim is: niet dieper dan de grond onder het midden
            Vector3 mid = (a + b) * 0.5f;
            float floor = Physics.Raycast(mid + Vector3.up * 2f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)
                ? hit.point.y + radius : mid.y - sag;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                p.y = Mathf.Max(p.y, Mathf.Min(floor, Mathf.Lerp(a.y, b.y, t)));
                points[i] = p;
            }
            var verts = new Vector3[(Segments + 1) * Sides];
            var normals = new Vector3[verts.Length];
            var tris = new int[Segments * Sides * 6];
            for (int i = 0; i <= Segments; i++)
            {
                Vector3 dir = (i < Segments ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
                Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 other = Vector3.Cross(dir, side);
                for (int s = 0; s < Sides; s++)
                {
                    float ang = s * Mathf.PI * 2f / Sides;
                    Vector3 n = side * Mathf.Cos(ang) + other * Mathf.Sin(ang);
                    verts[i * Sides + s] = points[i] + n * radius;
                    normals[i * Sides + s] = n;
                }
            }
            int k = 0;
            for (int i = 0; i < Segments; i++)
                for (int s = 0; s < Sides; s++)
                {
                    int p0 = i * Sides + s, p1 = i * Sides + (s + 1) % Sides, p2 = p0 + Sides, p3 = p1 + Sides;
                    tris[k++] = p0; tris[k++] = p1; tris[k++] = p2;
                    tris[k++] = p1; tris[k++] = p3; tris[k++] = p2;
                }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}
