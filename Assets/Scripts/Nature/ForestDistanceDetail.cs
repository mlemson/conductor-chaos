using System.Collections.Generic;
using ConductorChaos.Stations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Nature
{
    // Built once on each peer; Unity switches renderers per camera without touching the colliders.
    internal sealed class ForestDistanceDetail : MonoBehaviour
    {
        private const float CellSize = 32f;
        private const float DetailDistance = 100f;
        private const float TanHalfDefaultFov = .5773503f;
        private const int Sides = 6;
        private static readonly int MidRamp = Shader.PropertyToID("_Ramp2");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly List<Mesh> meshes = new();
        private Material material;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != ServiceRun.FifthLevelScene || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "PaintedProps") continue;
                var trees = root.transform.Find("Trees");
                if (trees && !trees.GetComponent<ForestDistanceDetail>())
                {
                    var shader = Resources.Load<Shader>("ForestDistance");
                    if (shader && shader.isSupported) trees.gameObject.AddComponent<ForestDistanceDetail>().Build(shader);
                }
                return;
            }
        }

        private void Build(Shader shader)
        {
            material = new Material(shader) { name = "Forest distance material" };
            var cells = new Dictionary<Vector2Int, List<Transform>>();
            foreach (Transform tree in transform)
            {
                Vector3 position = tree.localPosition;
                var key = new Vector2Int(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.z / CellSize));
                if (!cells.TryGetValue(key, out var trees)) cells.Add(key, trees = new List<Transform>());
                trees.Add(tree);
            }
            foreach (var cell in cells) BuildCell(cell.Key, cell.Value);
        }

        private void BuildCell(Vector2Int key, List<Transform> trees)
        {
            var cell = new GameObject($"Forest LOD {key.x}, {key.y}");
            cell.transform.SetParent(transform, false);
            cell.transform.localPosition = new Vector3((key.x + .5f) * CellSize, 0f, (key.y + .5f) * CellSize);
            var originals = new List<Renderer>();
            var vertices = new List<Vector3>();
            var colors = new List<Color32>();
            var triangles = new List<int>();
            foreach (var tree in trees)
            {
                // Future prefabs with their own LOD stay intact.
                if (tree.GetComponentInChildren<LODGroup>()) continue;
                var renderers = tree.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) continue;
                var bounds = new Bounds();
                bool found = false;
                Color32 crown = default, bark = default;
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled) continue;
                    Bounds local = renderer.localBounds;
                    if (renderer.transform != tree)
                    {
                        Matrix4x4 matrix = tree.worldToLocalMatrix * renderer.localToWorldMatrix;
                        local = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                        Bounds source = renderer.localBounds;
                        for (int corner = 0; corner < 8; corner++)
                            local.Encapsulate(matrix.MultiplyPoint3x4(source.min + Vector3.Scale(source.size,
                                new Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1))));
                    }
                    if (found) bounds.Encapsulate(local);
                    else { bounds = local; found = true; }

                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (!mat || !mat.HasProperty(MidRamp) && !mat.HasProperty(BaseColor)) continue;
                        Color color = mat.GetColor(mat.HasProperty(MidRamp) ? MidRamp : BaseColor);
                        if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
                        if (mat.name.IndexOf("Bark", System.StringComparison.OrdinalIgnoreCase) >= 0) bark = color;
                        else if (crown.a == 0) crown = color;
                    }
                }
                if (!found || bounds.size.y <= .01f || crown.a == 0 && bark.a == 0) continue;

                Matrix4x4 toCell = cell.transform.worldToLocalMatrix * tree.localToWorldMatrix;
                bool bush = tree.name.StartsWith("Bush_", System.StringComparison.Ordinal);
                bool dead = tree.name.StartsWith("Tree_Dead", System.StringComparison.Ordinal);
                bool pine = tree.name.StartsWith("Tree_Pine", System.StringComparison.Ordinal);
                var foot = new Vector3(0f, bounds.min.y, 0f);
                var baseOfCrown = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * (bush ? 0f : .29f), bounds.center.z);
                if (!bush)
                {
                    float trunkRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) * (dead ? .26f : .13f);
                    AddShape(foot, Vector3.one * trunkRadius, bounds.size.y * (dead ? 1f : .75f), .16f, .50f,
                        bark.a != 0 ? bark : crown, toCell, vertices, colors, triangles);
                }
                float crownHeight = bounds.max.y - baseOfCrown.y;
                AddShape(baseOfCrown, bounds.extents, crownHeight, pine ? .25f : .42f, pine ? .12f : .66f,
                    crown.a != 0 ? crown : bark, toCell, vertices, colors, triangles, dead ? .42f : 1f);
                tree.SetParent(cell.transform, true);
                originals.AddRange(renderers);
            }
            if (originals.Count == 0) { Destroy(cell); return; }

            var mesh = new Mesh { name = cell.name };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.UploadMeshData(true);
            meshes.Add(mesh);
            cell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var proxy = cell.AddComponent<MeshRenderer>();
            proxy.sharedMaterial = material;
            proxy.shadowCastingMode = ShadowCastingMode.Off;
            proxy.receiveShadows = false;

            var lod = cell.AddComponent<LODGroup>();
            lod.SetLODs(new[] { new LOD(.5f, originals.ToArray()), new LOD(0f, new Renderer[] { proxy }) });
            lod.RecalculateBounds();
            var levels = lod.GetLODs();
            levels[0].screenRelativeTransitionHeight = Mathf.Min(.95f, lod.size / (2f * TanHalfDefaultFov * DetailDistance));
            lod.SetLODs(levels);
        }

        private static void AddShape(Vector3 foot, Vector3 radii, float height, float widestAt, float topRadius,
            Color32 color, Matrix4x4 toCell, List<Vector3> vertices, List<Color32> colors, List<int> triangles,
            float width = 1f)
        {
            int start = vertices.Count;
            for (int ring = 0; ring < 3; ring++)
            {
                float y = height * (ring == 0 ? 0f : ring == 1 ? widestAt : 1f);
                float radius = width * (ring == 0 ? .55f : ring == 1 ? 1f : topRadius);
                for (int side = 0; side < Sides; side++)
                {
                    float angle = side * (2f * Mathf.PI / Sides);
                    vertices.Add(toCell.MultiplyPoint3x4(foot + new Vector3(Mathf.Cos(angle) * radii.x * radius,
                        y, Mathf.Sin(angle) * radii.z * radius)));
                    float shade = ring == 0 ? .72f : ring == 1 ? .90f : 1f;
                    colors.Add(new Color32((byte)(color.r * shade), (byte)(color.g * shade), (byte)(color.b * shade), 255));
                }
            }
            for (int side = 0; side < Sides; side++)
            {
                int next = (side + 1) % Sides;
                for (int ring = 0; ring < 2; ring++)
                {
                    int a = start + ring * Sides;
                    triangles.Add(a + side); triangles.Add(a + side + Sides); triangles.Add(a + next);
                    triangles.Add(a + next); triangles.Add(a + side + Sides); triangles.Add(a + next + Sides);
                }
            }
            for (int side = 1; side < Sides - 1; side++)
            {
                int top = start + 2 * Sides;
                triangles.Add(top); triangles.Add(top + side + 1); triangles.Add(top + side);
                triangles.Add(start); triangles.Add(start + side); triangles.Add(start + side + 1);
            }
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes) Destroy(mesh);
            Destroy(material);
        }
    }
}
