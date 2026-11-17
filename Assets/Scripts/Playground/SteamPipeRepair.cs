using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Playground
{
    internal sealed class SteamPipeRepair : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private Transform bolt;
        private Renderer boltRenderer;
        private MaterialPropertyBlock paint;
        private Mesh boltMesh;
        private float tightened;

        public SphereCollider BoltCollider { get; private set; }

        public static SteamPipeRepair Install(Transform gauge, Transform regulator)
        {
            Transform firebox = gauge.parent;
            Vector3 controls = firebox.InverseTransformPoint(regulator.position) - gauge.localPosition;
            float towardCrew = Mathf.Sign(controls.x);
            float towardFirebox = -Mathf.Sign(controls.z);
            var pipe = new GameObject("Koperen stoomleiding bij de vuurhaard");
            pipe.transform.SetParent(firebox, false);
            pipe.transform.localPosition = gauge.localPosition + new Vector3(.025f * towardCrew, .24f, .37f * towardFirebox);
            pipe.transform.localRotation = Quaternion.LookRotation(Vector3.right * towardCrew, Vector3.up);
            var fitting = pipe.AddComponent<SteamPipeRepair>();
            var gaugeRenderer = gauge.GetComponent<Renderer>();
            fitting.Build(gaugeRenderer ? gaugeRenderer.sharedMaterial : null);
            return fitting;
        }

        private void Build(Material material)
        {
            var copper = new Color(.67f, .31f, .12f);
            var brass = new Color(.75f, .51f, .22f);
            Tube("Stijgpijp", new Vector3(-.195f, .21f, .035f), new Vector3(-.195f, 0f, .035f), .038f, material, copper);
            Tube("Bocht naar koppeling", new Vector3(-.195f, 0f, .035f), new Vector3(0f, 0f, .035f), .038f, material, copper);
            Tube("Aansluiting op ketel", new Vector3(-.195f, .21f, -.035f), new Vector3(-.195f, .21f, .055f), .060f, material, brass);
            Tube("Kraag van koppeling", new Vector3(0f, 0f, .015f), new Vector3(0f, 0f, .13f), .115f, material, brass);

            var head = new GameObject("Zeskantbout van de stoomleiding");
            bolt = head.transform;
            bolt.SetParent(transform, false);
            boltMesh = MakeBoltHead();
            head.AddComponent<MeshFilter>().sharedMesh = boltMesh;
            boltRenderer = head.AddComponent<MeshRenderer>();
            if (material) boltRenderer.sharedMaterial = material;
            paint = new MaterialPropertyBlock();
            BoltCollider = head.AddComponent<SphereCollider>();
            BoltCollider.radius = .10f;

            Primitive(PrimitiveType.Cube, "Merkstreep op de boutkop", bolt,
                new Vector3(0f, .043f, .033f), new Vector3(.020f, .064f, .009f), material, new Color(.97f, .87f, .65f));
            Present(false, 1f, true);
        }

        public void Present(bool broken, float progress, bool snap = false)
        {
            if (BoltCollider.enabled != broken) BoltCollider.enabled = broken;
            tightened = snap ? progress : Mathf.MoveTowards(tightened, progress, 3.8f * Time.deltaTime);
            bolt.localPosition = Vector3.forward * (.146f + .058f * (1f - tightened));
            bolt.localRotation = Quaternion.AngleAxis(-120f * tightened, Vector3.forward);
            Color color = broken
                ? Color.Lerp(new Color(.84f, .17f, .09f), new Color(1f, .44f, .18f), .5f + .5f * Mathf.Sin(Time.time * 5f))
                : new Color(.80f, .57f, .28f);
            paint.SetColor(BaseColor, color);
            boltRenderer.SetPropertyBlock(paint);
        }

        private void Tube(string label, Vector3 from, Vector3 to, float radius, Material material, Color color)
        {
            Transform part = Primitive(PrimitiveType.Cylinder, label, transform, (from + to) * .5f,
                new Vector3(2f * radius, (to - from).magnitude * .5f, 2f * radius), material, color);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        }

        private static Transform Primitive(PrimitiveType type, string label, Transform parent, Vector3 center,
            Vector3 scale, Material material, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = center;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            if (material) renderer.sharedMaterial = material;
            var tint = new MaterialPropertyBlock();
            tint.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(tint);
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return part.transform;
        }

        private static Mesh MakeBoltHead()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f, next = (i + 1) * Mathf.PI / 3f;
                Vector3 a = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * .092f;
                Vector3 b = new Vector3(Mathf.Cos(next), Mathf.Sin(next), 0f) * .092f;
                Vector3 z = Vector3.forward * .030f;
                int j = vertices.Count;
                vertices.AddRange(new[] { z, a + z, b + z, -z, b - z, a - z, a - z, b - z, b + z, a + z });
                triangles.AddRange(new[] { j, j + 1, j + 2, j + 3, j + 4, j + 5,
                    j + 6, j + 7, j + 8, j + 6, j + 8, j + 9 });
            }
            var mesh = new Mesh { name = "Zeskantkop stoomleiding" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private void OnDestroy()
        {
            if (boltMesh) Destroy(boltMesh);
        }
    }
}
