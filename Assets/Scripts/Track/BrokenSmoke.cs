// Conductor Chaos — bewegende rook en stoom bij kapotte onderdelen (issue #239). De modellen hebben in Broken_Bits
// een paar vaste wolkjes (materiaal *_Smoke of *_Steam). Die halen we uit het model en vervangen we door een
// deeltjesbron op dezelfde plek: wolkjes in dezelfde vorm en kleur die opstijgen, aanzwellen en weer verdwijnen.
// Zit onder Broken_Bits, dus het stopt vanzelf zodra het onderdeel gerepareerd is. Puur cosmetisch en lokaal.
using System.Collections.Generic;
using UnityEngine;

namespace ConductorChaos.Track
{
    [DisallowMultipleComponent]
    public sealed class BrokenSmoke : MonoBehaviour
    {
        private readonly List<ParticleSystem> emitters = new();
        private readonly List<Object> owned = new();

        /// <summary>Vervangt de vaste wolkjes onder <paramref name="bits"/> door bewegende rook.</summary>
        internal static void Attach(Transform bits)
        {
            if (!bits) return;
            foreach (var renderer in bits.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable || renderer.GetComponent<BrokenSmoke>()) continue;
                var materials = renderer.sharedMaterials;
                Mesh mesh = filter.sharedMesh;
                int subMeshes = Mathf.Min(mesh.subMeshCount, materials.Length);
                var puffSubs = new List<int>();
                for (int i = 0; i < subMeshes; i++)
                    if (IsPuffMaterial(materials[i])) puffSubs.Add(i);
                if (puffSubs.Count == 0) continue;

                var smoke = renderer.gameObject.AddComponent<BrokenSmoke>();
                smoke.Build(renderer, filter, mesh, materials, subMeshes, puffSubs);
            }
        }

        private static bool IsPuffMaterial(Material material)
        {
            if (!material) return false;
            string name = material.name.Replace(" (Instance)", "");
            return name.EndsWith("_Smoke", System.StringComparison.Ordinal) || name.EndsWith("_Steam", System.StringComparison.Ordinal);
        }

        private void Build(MeshRenderer renderer, MeshFilter filter, Mesh mesh, Material[] materials, int subMeshes, List<int> puffSubs)
        {
            // 1. Het model zonder de vaste wolkjes.
            var keptMaterials = new List<Material>();
            var keptSubs = new List<int>();
            for (int i = 0; i < subMeshes; i++)
                if (!puffSubs.Contains(i)) { keptSubs.Add(i); keptMaterials.Add(materials[i]); }
            var vertices = mesh.vertices;
            var stripped = new Mesh { name = mesh.name + " zonder rook", indexFormat = mesh.indexFormat };
            stripped.vertices = vertices;
            stripped.normals = mesh.normals;
            if (mesh.tangents.Length == vertices.Length) stripped.tangents = mesh.tangents;
            if (mesh.uv.Length == vertices.Length) stripped.uv = mesh.uv;
            if (mesh.colors.Length == vertices.Length) stripped.colors = mesh.colors;
            stripped.subMeshCount = keptSubs.Count;
            for (int k = 0; k < keptSubs.Count; k++) stripped.SetTriangles(mesh.GetTriangles(keptSubs[k]), k);
            stripped.RecalculateBounds();
            owned.Add(stripped);
            filter.sharedMesh = stripped;
            renderer.sharedMaterials = keptMaterials.ToArray();

            // 2. Per materiaal één rookbron, met de wolkjes van het model als vorm, plek en grootte.
            foreach (int sub in puffSubs)
            {
                var puffs = SplitPuffs(vertices, mesh.GetTriangles(sub));
                if (puffs.Count == 0) continue;
                bool steam = materials[sub].name.Contains("_Steam");
                CreateEmitter(renderer.transform, puffs, materials[sub], steam);
            }
        }

        private struct Puff
        {
            public Vector3 Center;   // lokaal t.o.v. de renderer
            public float Radius;
            public Mesh Shape;       // eenheidsgrootte, rond de oorsprong
        }

        // De wolkjes zijn losse bollen in één submesh: groepeer driehoeken die posities delen.
        private List<Puff> SplitPuffs(Vector3[] vertices, int[] triangles)
        {
            var key = new Dictionary<Vector3Int, int>();
            var parent = new List<int>();
            int Weld(Vector3 p)
            {
                var q = Vector3Int.RoundToInt(p * 1000f);
                if (!key.TryGetValue(q, out int id)) { id = parent.Count; key[q] = id; parent.Add(id); }
                return id;
            }
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            var welded = new int[triangles.Length];
            for (int t = 0; t < triangles.Length; t++) welded[t] = Weld(vertices[triangles[t]]);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int a = Find(welded[t]), b = Find(welded[t + 1]), c = Find(welded[t + 2]);
                parent[b] = a;
                parent[Find(c)] = Find(a);
            }
            var groups = new Dictionary<int, List<int>>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                int root = Find(welded[t]);
                if (!groups.TryGetValue(root, out var list)) groups[root] = list = new List<int>();
                list.Add(triangles[t]); list.Add(triangles[t + 1]); list.Add(triangles[t + 2]);
            }
            var result = new List<Puff>();
            foreach (var tris in groups.Values)
            {
                var bounds = new Bounds(vertices[tris[0]], Vector3.zero);
                foreach (int index in tris) bounds.Encapsulate(vertices[index]);
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
                if (radius < .01f) continue;
                var remap = new Dictionary<int, int>();
                var shapeVertices = new List<Vector3>();
                var shapeTriangles = new int[tris.Count];
                for (int i = 0; i < tris.Count; i++)
                {
                    if (!remap.TryGetValue(tris[i], out int local))
                    {
                        local = shapeVertices.Count;
                        remap[tris[i]] = local;
                        shapeVertices.Add((vertices[tris[i]] - bounds.center) / (radius * 2f));
                    }
                    shapeTriangles[i] = local;
                }
                var shape = new Mesh { name = "Rookwolkje" };
                shape.SetVertices(shapeVertices);
                shape.SetTriangles(shapeTriangles, 0);
                shape.RecalculateNormals();
                shape.RecalculateBounds();
                owned.Add(shape);
                result.Add(new Puff { Center = bounds.center, Radius = radius, Shape = shape });
            }
            result.Sort((a, b) => a.Center.y.CompareTo(b.Center.y));
            return result;
        }

        private void CreateEmitter(Transform model, List<Puff> puffs, Material material, bool steam)
        {
            Vector3 scale = model.lossyScale;
            float worldScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
            Puff first = puffs[0], last = puffs[puffs.Count - 1];
            Vector3 from = model.TransformPoint(first.Center);
            Vector3 to = puffs.Count > 1 ? model.TransformPoint(last.Center) : from + model.up * first.Radius * worldScale * 4f;
            Vector3 path = to - from;
            float distance = Mathf.Max(.15f, path.magnitude * 1.35f);
            float startSize = first.Radius * 2f * worldScale;
            float endSize = last.Radius * 2f * worldScale;

            var go = new GameObject(steam ? "Stoom" : "Rook");
            go.transform.SetParent(transform, false);
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(path.sqrMagnitude > 1e-6f ? path.normalized : Vector3.up,
                Mathf.Abs(Vector3.Dot(path.normalized, Vector3.up)) > .95f ? Vector3.forward : Vector3.up);

            var particles = go.AddComponent<ParticleSystem>();
            emitters.Add(particles);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float lifetime = steam ? 1.0f : 1.7f;
            var main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .85f, lifetime * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(distance / lifetime * .8f, distance / lifetime * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(startSize * .8f, startSize * 1.05f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = steam ? 0f : -.04f;
            main.maxParticles = 24;
            var emission = particles.emission;
            emission.rateOverTime = steam ? 7f : 3.2f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = steam ? 10f : 16f;
            shape.radius = startSize * .25f;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            float grow = Mathf.Clamp(endSize / Mathf.Max(.01f, startSize), 1.1f, 2.4f);
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, .35f), new Keyframe(.18f, 1f), new Keyframe(.7f, grow), new Keyframe(1f, 0f)));
            var spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-.8f, .8f);
            var inherit = particles.inheritVelocity;
            inherit.enabled = true;
            inherit.mode = ParticleSystemInheritVelocityMode.Initial;
            inherit.curve = new ParticleSystem.MinMaxCurve(.75f);
            var noise = particles.noise;
            noise.enabled = !steam;
            noise.strength = .25f;
            noise.frequency = .6f;

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = first.Shape;
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.alignment = ParticleSystemRenderSpace.World;
            particles.Play();
        }

        // Bij het (opnieuw) kapotgaan meteen een paar wolkjes, zodat je het ziet gebeuren.
        private void OnEnable()
        {
            foreach (var particles in emitters)
            {
                if (!particles) continue;
                particles.Play();
                particles.Emit(2);
            }
        }

        private void OnDestroy()
        {
            foreach (var item in owned)
                if (item) Destroy(item);
        }
    }
}
