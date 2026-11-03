// Conductor Chaos — gestileerd terrein: TerrainLayers uit de grondtextures en een startterrein (issue #38).
// Menu: Conductor Chaos > Art > Terrain > Create Flat Terrain (sculpt + paint) / Create Stylized Terrain (gentle hills) /
// Flatten Selected Terrain / Add Layers To Selected Terrain / Add Flat Test Layers / Create Terrain Layers.
// Daarna vorm en schilder je met Unity's eigen Terrain-tools: Raise or Lower (omhoog/omlaag trekken), Smooth, Set Height,
// en Paint Texture (gras, bloemetjes, grond, stenen pad, rots). Vlakke grond ligt op y = 0 met 10 m ruimte omlaag.
// Kliffen krijgen automatisch een grondtextuur op steile vlakken van hetzelfde Terrain, zonder overlay of verhoogde rand.
// Props zoals stenen en bloemen plaats je apart met de Prop Painter.
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

        // effen set (test): effen kleur met grote, net iets andere vlakken, zoals het gras/de rots op de kliffen
        static readonly (string tex, string layer, float tile)[] FlatDefs =
        {
            ("T_Flat_Grass", "TL_Flat_Grass", 10f),
            ("T_Flat_GrassSmall", "TL_Flat_GrassSmall", 10f),
            ("T_Flat_GrassShade", "TL_Flat_GrassShade", 10f),
            ("T_Flat_GrassFlowers", "TL_Flat_GrassFlowers", 10f),
            ("T_Flat_Dirt", "TL_Flat_Dirt", 8f),
            ("T_Flat_Sand", "TL_Flat_Sand", 8f),
            ("T_Flat_Rock", "TL_Flat_Rock", 8f),
            ("T_Flat_Stone", "TL_Flat_Stone", 5f),
            ("T_Flat_Snow", "TL_Flat_Snow", 10f),
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

        public static TerrainLayer[] EnsureLayers() => EnsureLayers(Defs);

        public static TerrainLayer[] EnsureLayers((string tex, string layer, float tile)[] defs)
        {
            EnsureFolder(LayerDir);
            var list = new List<TerrainLayer>();
            foreach (var d in defs)
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

        // Hoogte-instellingen: het terrein is 40 m hoog; de vlakke grond ligt op 25 % (10 m), zodat je
        // tot 10 m kunt uitgraven (rivier, ravijn) en tot 30 m kunt ophogen (heuvels, bergflanken).
        // Het object staat zo dat de vlakke grond precies op wereldhoogte 0 ligt (hoogte van het spoor).
        const float TerrainHeight = 40f;
        const float BaseFraction = 0.25f;

        [MenuItem("Conductor Chaos/Art/Terrain/Create Flat Terrain (sculpt + paint)")]
        public static void CreateFlatTerrain() => CreateTerrain(false);

        [MenuItem("Conductor Chaos/Art/Terrain/Create Stylized Terrain (gentle hills)")]
        public static void CreateHillyTerrain() => CreateTerrain(true);

        static void CreateTerrain(bool hills)
        {
            var layers = EnsureLayers();
            var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 512 };
            data.size = new Vector3(120f, TerrainHeight, 120f);
            data.terrainLayers = layers;

            int res = data.heightmapResolution;
            var h = new float[res, res];
            float ox = Random.Range(0f, 100f), oz = Random.Range(0f, 100f);
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    float nx = x / (float)res, nz = z / (float)res;
                    h[z, x] = BaseFraction + (hills ? 0.025f * Mathf.PerlinNoise(ox + nx * 3f, oz + nz * 3f) +
                                                      0.01f * Mathf.PerlinNoise(ox + nx * 9f, oz + nz * 9f) - 0.0175f : 0f);
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
            go.transform.position = new Vector3(-data.size.x / 2f, -BaseFraction * TerrainHeight, -data.size.z / 2f);
            var t = go.GetComponent<Terrain>();
            t.basemapDistance = 1000f;                               // textures scherp houden op afstand
            t.drawInstanced = true;
            Undo.RegisterCreatedObjectUndo(go, "Create Stylized Terrain");
            TerrainCliffsEditor.AddTo(t);                             // steile hellingen krijgen grondtextuur; geen extra mesh
            Selection.activeGameObject = go;
            Debug.Log($"[Terrain] Terrein gemaakt ({path}). " + SculptHelp);
        }

        const string SculptHelp =
            "Vormen: Terrain-component > Paint Terrain > 'Raise or Lower Terrain' (klik = omhoog, Shift+klik = omlaag), " +
            "'Smooth Height' om af te ronden, 'Set Height' voor vlakke plateaus (bv. onder het spoor). " +
            "Kleuren: 'Paint Texture' en kies een laag. Brush Size/Opacity bepalen grootte en kracht. " +
            "Hellingtextuur: 'StylizedCliffs' > Start Slope Percent (standaard 30%) en Transition Slope Percent. " +
            "Geen extra bovenrand; Cliff Painter maakt treden, ook gewone Unity-brushes krijgen slope-textuur.";

        [MenuItem("Conductor Chaos/Art/Terrain/Flatten Selected Terrain")]
        public static void FlattenSelected()
        {
            var t = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (t == null) { Debug.LogWarning("[Terrain] Selecteer eerst een Terrain."); return; }
            if (!EditorUtility.DisplayDialog("Terrein vlak maken",
                    $"Alle hoogteverschillen van '{t.name}' worden weggehaald (de geschilderde lagen blijven). " +
                    "De grond komt op wereldhoogte 0 te liggen, met 10 m ruimte omlaag en 30 m omhoog.", "Vlak maken", "Annuleren"))
                return;
            var data = t.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Flatten terrain");
            Undo.RecordObject(t.transform, "Flatten terrain");
            var size = data.size;
            data.size = new Vector3(size.x, TerrainHeight, size.z);
            int res = data.heightmapResolution;
            var h = new float[res, res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                    h[z, x] = BaseFraction;
            data.SetHeights(0, 0, h);
            var p = t.transform.position;
            t.transform.position = new Vector3(p.x, -BaseFraction * TerrainHeight, p.z);
            EditorUtility.SetDirty(data);
            Debug.Log($"[Terrain] {t.name} is vlak. " + SculptHelp, t);
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Add Layers To Selected Terrain")]
        public static void AddToSelected() => AddToSelected(Defs);

        [MenuItem("Conductor Chaos/Art/Terrain/Add Flat Test Layers To Selected Terrain")]
        public static void AddFlatToSelected() => AddToSelected(FlatDefs);

        static void AddToSelected((string tex, string layer, float tile)[] defs)
        {
            var t = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (t == null) { Debug.LogWarning("[Terrain] Selecteer eerst een Terrain."); return; }
            var layers = EnsureLayers(defs);
            var list = (t.terrainData.terrainLayers ?? new TerrainLayer[0]).ToList();
            foreach (var l in layers) if (!list.Contains(l)) list.Add(l);
            Undo.RecordObject(t.terrainData, "Add terrain layers");
            t.terrainData.terrainLayers = list.ToArray();
            Debug.Log($"[Terrain] {list.Count} lagen op {t.name}.");
        }
    }
}
