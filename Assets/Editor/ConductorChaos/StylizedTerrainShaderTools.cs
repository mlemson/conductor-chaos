// Conductor Chaos — terrein met de gestileerde shader tekenen (issue #118).
// ConductorChaos/StylizedTerrain tekent gras exact zoals het gras op de klifmodellen (zelfde ramp en berekening);
// overige lagen houden hun textuur. Menu:
//   Conductor Chaos > Art > Terrain > Use Stylized Terrain Shader   (alle terreinen in de open scènes)
//   Conductor Chaos > Art > Terrain > Use URP Terrain Lit (back)     (terug naar het standaardmateriaal)
// Eenmalig automatisch toegepast op de open scènes na import (vlag in EditorPrefs); daarna alleen via het menu.
// Zet Draw Instanced uit (de shader ondersteunt geen terrain-instancing) en houdt de basemap uit beeld.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class StylizedTerrainShaderTools
    {
        const string MaterialPath = "Assets/Art/Terrain/M_StylizedTerrain.mat";
        const string ShaderName = "ConductorChaos/StylizedTerrain";
        const string AutoKey = "ConductorChaos.StylizedTerrain.AutoApplied.v1";
        const string UrpTerrainLit = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat";
        static readonly HashSet<Terrain> KnownTerrains = new HashSet<Terrain>();
        static bool terrainTrackingReady;

        static StylizedTerrainShaderTools()
        {
            EditorApplication.delayCall += RememberTerrains;
            EditorSceneManager.sceneOpened += (_, _) => RememberTerrains();
            EditorApplication.hierarchyChanged += ApplyToNewNeighbours;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetBool(AutoKey, false)) return;
                if (Shader.Find(ShaderName) == null) return;          // nog niet geïmporteerd: volgende keer
                int n = Apply(true);
                EditorPrefs.SetBool(AutoKey, true);
                if (n > 0)
                    Debug.Log($"[Terrain] {n} terrein(en) tekenen nu gras zoals de kliffen (ConductorChaos/StylizedTerrain). " +
                              "Sla de scène op om dit te bewaren. Terug: Conductor Chaos > Art > Terrain > Use URP Terrain Lit (back).");
            };
        }

        static void RememberTerrains()
        {
            foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
                KnownTerrains.Add(terrain);
            terrainTrackingReady = true;
        }

        static void ApplyToNewNeighbours()
        {
            if (!terrainTrackingReady) return;
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
            foreach (var terrain in terrains)
            {
                if (!KnownTerrains.Add(terrain) || EditorApplication.isPlayingOrWillChangePlaymode ||
                    !terrain.gameObject.scene.IsValid() || !terrain.gameObject.scene.isLoaded) continue;
                var current = terrain.materialTemplate;
                if (current && current.shader && current.shader.name != "Universal Render Pipeline/Terrain/Lit") continue;
                foreach (var neighbour in terrains)
                {
                    if (neighbour == terrain || neighbour.gameObject.scene != terrain.gameObject.scene ||
                        !neighbour.materialTemplate || !neighbour.materialTemplate.shader || neighbour.materialTemplate.shader.name != ShaderName) continue;
                    Undo.RecordObject(terrain, "Match neighbouring terrain");
                    terrain.materialTemplate = neighbour.materialTemplate;
                    terrain.drawInstanced = false;
                    terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 5000f);
                    EditorUtility.SetDirty(terrain);
                    EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                    break;
                }
            }
        }

        [MenuItem("Conductor Chaos/Art/Terrain/Use Stylized Terrain Shader")]
        static void ApplyMenu() => Apply(true);

        [MenuItem("Conductor Chaos/Art/Terrain/Use URP Terrain Lit (back)")]
        static void RevertMenu() => Apply(false);

        public static Material GetOrCreateMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat != null) return mat;
            var shader = Shader.Find(ShaderName);
            if (shader == null) return null;
            mat = new Material(shader) { name = "M_StylizedTerrain" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        static int Apply(bool stylized)
        {
            Material mat = stylized ? GetOrCreateMaterial() : AssetDatabase.LoadAssetAtPath<Material>(UrpTerrainLit);
            if (mat == null)
            {
                Debug.LogWarning(stylized ? "[Terrain] Shader ConductorChaos/StylizedTerrain niet gevonden."
                                          : "[Terrain] URP TerrainLit-materiaal niet gevonden: " + UrpTerrainLit);
                return 0;
            }
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
            {
                Undo.RecordObject(t, stylized ? "Use Stylized Terrain" : "Use URP Terrain Lit");
                t.materialTemplate = mat;
                t.drawInstanced = !stylized;
                if (stylized) t.basemapDistance = Mathf.Max(t.basemapDistance, 5000f);
                EditorUtility.SetDirty(t);
                EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                n++;
            }
            return n;
        }
    }
}
