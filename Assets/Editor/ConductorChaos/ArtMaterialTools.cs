// Conductor Chaos — wisselen tussen de stylized ramp-shader en URP Lit voor alle art-materialen (issue #33).
// Gebruik "Use URP Lit (fallback)" als ConductorChaos/StylizedRamp niet compileert (roze materialen).
// De ramp-waarden blijven in de materialen bewaard, dus terugzetten kan altijd.
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class ArtMaterialTools
    {
        const string MaterialRoot = "Assets/Art/Materials";
        const string Stylized = "ConductorChaos/StylizedRamp";
        const string UrpLit = "Universal Render Pipeline/Lit";

        [MenuItem("Conductor Chaos/Art/Use URP Lit (fallback)")]
        public static void UseUrpLit() => Switch(UrpLit);

        [MenuItem("Conductor Chaos/Art/Use Stylized Ramp")]
        public static void UseStylized() => Switch(Stylized);

        static void Switch(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogError($"[ArtMaterials] Shader niet gevonden: {shaderName}"); return; }
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == shader) continue;
                var baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                int queue = m.renderQueue;
                m.shader = shader;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseColor);
                m.renderQueue = queue;
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtMaterials] {n} materialen omgezet naar {shaderName}");
        }
    }
}
