using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ConductorChaos.EditorTools
{
    internal static class WinterStationRoofs
    {
        const string Prefabs = "Assets/Art/Prefabs/Station";
        const string Snow = "Assets/Art/Materials/Environment/M_Env_SnowSoft.mat";
        internal const string CapName = "Sneeuw op het dak";
        const float CellSize = .16f;

        [MenuItem("Conductor Chaos/Art/Station/Build Snow Roof Variants")]
        static void BuildVariants()
        {
            foreach (string suffix in new[] { "", "_Small", "_Large", "_Green" })
                GetVariant("Station_Building" + suffix, rebuild: true);
        }

        internal static GameObject GetVariant(string name, bool rebuild = false)
        {
            string path = $"{Prefabs}/{name}_Snow.prefab";
            var variant = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (variant && !rebuild) return variant;
            var regular = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{name}.prefab");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Snow);
            if (!regular || !material) throw new InvalidOperationException($"Bronnen voor het sneeuwdak van {name} ontbreken.");

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(regular, scene);
                var cap = CreateCap(instance, name);
                var geometry = cap.GetComponent<MeshFilter>().sharedMesh;
                string meshPath = $"{Prefabs}/{name}_SnowRoof.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(geometry, existing);
                    Object.DestroyImmediate(geometry);
                    cap.GetComponent<MeshFilter>().sharedMesh = existing;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                else AssetDatabase.CreateAsset(geometry, meshPath);
                cap.GetComponent<MeshRenderer>().sharedMaterial = material;
                return PrefabUtility.SaveAsPrefabAsset(instance, path);
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }

        internal static void AddTo(GameObject building, string baseName)
        {
            if (building.transform.Find(CapName)) return;
            var source = GetVariant(baseName).transform.Find(CapName);
            var cap = new GameObject(CapName);
            cap.transform.SetParent(building.transform, false);
            cap.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            cap.AddComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
        }

        static GameObject CreateCap(GameObject building, string name)
        {
            var vertices = new List<Vector3>();
            var faces = new List<int>();
            foreach (var roof in building.GetComponentsInChildren<MeshRenderer>())
            {
                if (!roof.sharedMaterials.Any(IsTile)) continue;
                var mesh = roof.GetComponent<MeshFilter>().sharedMesh;
                var toBuilding = building.transform.worldToLocalMatrix * roof.transform.localToWorldMatrix;
                SampleRoof(mesh, toBuilding, roof.sharedMaterials, vertices, faces);
            }
            if (faces.Count == 0) throw new InvalidOperationException($"Geen dakpannen gevonden op {name}.");
            AddSnowEdges(vertices, faces);
            var cap = new GameObject(CapName);
            cap.transform.SetParent(building.transform, false);
            var geometry = new Mesh { name = name + " · sneeuwdak", indexFormat = IndexFormat.UInt32 };
            geometry.SetVertices(vertices);
            geometry.SetTriangles(faces, 0);
            geometry.RecalculateNormals();
            geometry.RecalculateBounds();
            cap.AddComponent<MeshFilter>().sharedMesh = geometry;
            cap.AddComponent<MeshRenderer>();
            return cap;
        }

        static void AddSnowEdges(List<Vector3> vertices, List<int> faces)
        {
            var borders = new HashSet<(int from, int to)>();
            void Edge(int a, int b)
            {
                if (!borders.Remove((b, a))) borders.Add((a, b));
            }
            for (int i = 0; i < faces.Count; i += 3)
            {
                Edge(faces[i], faces[i + 1]);
                Edge(faces[i + 1], faces[i + 2]);
                Edge(faces[i + 2], faces[i]);
            }
            foreach (var (a, b) in borders)
            {
                int bottom = vertices.Count;
                vertices.Add(vertices[a] - Vector3.up * .19f);
                vertices.Add(vertices[b] - Vector3.up * .19f);
                faces.Add(b); faces.Add(a); faces.Add(bottom);
                faces.Add(b); faces.Add(bottom); faces.Add(bottom + 1);
            }
        }

        static bool IsTile(Material material) => material && material.name.StartsWith("M_Stn_Roof", StringComparison.Ordinal);

        static void SampleRoof(Mesh mesh, Matrix4x4 local, Material[] materials, List<Vector3> vertices, List<int> faces)
        {
            using var data = MeshUtility.AcquireReadOnlyMeshData(mesh);
            using var points = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp);
            data[0].GetVertices(points);
            var tiles = new List<Vector3>();
            for (int m = 0; m < Mathf.Min(materials.Length, mesh.subMeshCount); m++)
            {
                if (!IsTile(materials[m])) continue;
                using var indices = new NativeArray<int>(mesh.GetSubMesh(m).indexCount, Allocator.Temp);
                data[0].GetIndices(indices, m);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = local.MultiplyPoint3x4(points[indices[i]]);
                    Vector3 b = local.MultiplyPoint3x4(points[indices[i + 1]]);
                    Vector3 c = local.MultiplyPoint3x4(points[indices[i + 2]]);
                    if (Vector3.Cross(b - a, c - a).normalized.y < .4f) continue;
                    tiles.Add(a); tiles.Add(b); tiles.Add(c);
                }
            }
            if (tiles.Count == 0) return;

            float x0 = tiles.Min(v => v.x), z0 = tiles.Min(v => v.z);
            int nx = Mathf.CeilToInt((tiles.Max(v => v.x) - x0) / CellSize) + 1;
            int nz = Mathf.CeilToInt((tiles.Max(v => v.z) - z0) / CellSize) + 1;
            var height = new float[nz, nx];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++) height[z, x] = float.NegativeInfinity;

            for (int i = 0; i < tiles.Count; i += 3)
            {
                Vector3 a = tiles[i], u = tiles[i + 1] - a, v = tiles[i + 2] - a;
                float area = u.x * v.z - u.z * v.x;
                if (Mathf.Abs(area) < 1e-6f) continue;
                int minX = Mathf.CeilToInt((a.x + Mathf.Min(0f, u.x, v.x) - x0) / CellSize);
                int maxX = Mathf.FloorToInt((a.x + Mathf.Max(0f, u.x, v.x) - x0) / CellSize);
                int minZ = Mathf.CeilToInt((a.z + Mathf.Min(0f, u.z, v.z) - z0) / CellSize);
                int maxZ = Mathf.FloorToInt((a.z + Mathf.Max(0f, u.z, v.z) - z0) / CellSize);
                for (int z = minZ; z <= maxZ; z++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        float dx = x0 + x * CellSize - a.x, dz = z0 + z * CellSize - a.z;
                        float b = (dx * v.z - dz * v.x) / area, c = (u.x * dz - u.z * dx) / area;
                        if (b >= -.001f && c >= -.001f && b + c <= 1.001f)
                            height[z, x] = Mathf.Max(height[z, x], a.y + b * u.y + c * v.y);
                    }
            }

            // A few tiles stay visible at the eaves and the thickness varies along the snow edge.
            var snow = new int[nz, nx];
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    snow[z, x] = -1;
                    float wx = x0 + x * CellSize, wz = z0 + z * CellSize;
                    float noise = Mathf.PerlinNoise(wx * 1.14f + 9f, wz * .87f + 16f);
                    float edge = Mathf.Min(x, nx - x - 1, z, nz - z - 1) * CellSize;
                    if (edge < .12f + .38f * noise) continue;
                    float surface = height[z, x];
                    if (float.IsNegativeInfinity(surface))
                        surface = Mathf.Max((height[z, x - 1] + height[z, x + 1]) * .5f,
                            (height[z - 1, x] + height[z + 1, x]) * .5f);
                    if (float.IsNegativeInfinity(surface)) continue;
                    snow[z, x] = vertices.Count;
                    vertices.Add(new Vector3(wx, surface + .12f + .13f * noise, wz));
                }
            for (int z = 0; z < nz - 1; z++)
                for (int x = 0; x < nx - 1; x++)
                {
                    int a = snow[z, x], b = snow[z, x + 1], c = snow[z + 1, x], d = snow[z + 1, x + 1];
                    if (a >= 0 && b >= 0 && c >= 0) { faces.Add(a); faces.Add(c); faces.Add(b); }
                    if (b >= 0 && c >= 0 && d >= 0) { faces.Add(b); faces.Add(c); faces.Add(d); }
                }
        }
    }
}
