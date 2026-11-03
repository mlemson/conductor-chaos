// Conductor Chaos — gestileerd terrein: TerrainLayers uit de grondtextures en een startterrein (issue #38).
// Menu: Conductor Chaos > Art > Terrain > Create Stylized Terrain / Add Layers To Selected Terrain / Create Terrain Layers.
// Daarna schilder je met Unity's eigen Terrain-tools (Paint Texture: gras, bloemetjes, grond, stenen pad, rots;
// Raise/Lower en Smooth voor heuvels). Bomen, rotsen en kliffen erop zet je met de Prop Painter.
// Statisch decor; de TerrainCollider wordt later de ondergrond voor beweging (host-autoritatief, zie ARCHITECTURE).
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class StylizedTerrainTools
    {
        const string TexDir = "Assets/Art/Terrain/Textures";
        const string LayerDir = "Assets/Art/Terrain/Layers";
        const string DataDir = "Assets/Art/Terrain";

        // textuur, laagnaam, tegelgrootte (m)
        static readonly (string tex, string layer, float tile)[] Defs =
        {
            ("T_Ground_Grass", "TL_Grass", 8f),
            ("T_Ground_GrassFlowers", "TL_GrassFlowers", 8f),
            ("T_Ground_Dirt", "TL_Dirt", 6f),
            ("T_Ground_PathStones", "TL_PathStones", 3f),
            ("T_Ground_Rock", "TL_Rock", 6f),
        };

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Create Terrain Layers")]
        public static void CreateLayersMenu() => EnsureLayers();

        public static TerrainLayer[] EnsureLayers()
        {
            EnsureFolder(LayerDir);
            var list = new List<TerrainLayer>();
            foreach (var d in Defs)
            {
                var path = $"{LayerDir}/{d.layer}.terrainlayer";
                var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (tl == null)
                {
                    tl = new TerrainLayer();
                    AssetDatabase.CreateAsset(tl, path);
                }
                tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{d.tex}.png");
                if (tl.diffuseTexture == null) Debug.LogWarning($"[Terrain] Textuur ontbreekt: {TexDir}/{d.tex}.png");
                tl.tileSize = new Vector2(d.tile, d.tile);
                tl.metallic = 0f;
                tl.smoothness = 0f;                                   // mat, zoals de rest van de stijl
                tl.specular = Color.black;
                EditorUtility.SetDirty(tl);
                list.Add(tl);
            }
            AssetDatabase.SaveAssets();
            return list.ToArray();
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Create Stylized Terrain")]
        public static void CreateTerrain()
        {
            var layers = EnsureLayers();
            var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 512 };
            data.size = new Vector3(120f, 40f, 120f);
            data.terrainLayers = layers;

            // zachte golving als start (basis 1,6 m boven de onderkant, zodat je ook kunt verlagen)
            int res = data.heightmapResolution;
            var h = new float[res, res];
            float ox = Random.Range(0f, 100f), oz = Random.Range(0f, 100f);
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)res, nz = z / (float)res;
                    h[z, x] = 0.04f + 0.025f * Mathf.PerlinNoise(ox + nx * 3f, oz + nz * 3f) + 0.01f * Mathf.PerlinNoise(ox + nx * 9f, oz + nz * 9f);
                }
            data.SetHeights(0, 0, h);

            int ar = data.alphamapResolution;
            var a = new float[ar, ar, layers.Length];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                    a[z, x, 0] = 1f;                                  // alles begint als gras
            data.SetAlphamaps(0, 0, a);

            EnsureFolder(DataDir);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{DataDir}/StylizedTerrain.asset");
            AssetDatabase.CreateAsset(data, path);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "StylizedTerrain";
            float baseY = 0.04f * data.size.y + 0.0125f * data.size.y;
            go.transform.position = new Vector3(-data.size.x / 2f, -baseY, -data.size.z / 2f);
            var t = go.GetComponent<Terrain>();
            t.basemapDistance = 1000f;                               // textures scherp houden op afstand
            t.drawInstanced = true;
            Undo.RegisterCreatedObjectUndo(go, "Create Stylized Terrain");
            Selection.activeGameObject = go;
            Debug.Log($"[Terrain] Terrein gemaakt ({path}). Schilder lagen via Terrain > Paint Texture; " +
                      "zet bomen/rotsen/kliffen met Conductor Chaos > Art > Prop Painter.");
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Add Layers To Selected Terrain")]
        public static void AddToSelected()
        {
            var t = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (t == null) { Debug.LogWarning("[Terrain] Selecteer eerst een Terrain."); return; }
            var layers = EnsureLayers();
            var list = (t.terrainData.terrainLayers ?? new TerrainLayer[0]).ToList();
            foreach (var l in layers) if (!list.Contains(l)) list.Add(l);
            Undo.RecordObject(t.terrainData, "Add terrain layers");
            t.terrainData.terrainLayers = list.ToArray();
            Debug.Log($"[Terrain] {list.Count} lagen op {t.name}.");
        }
    }
}
