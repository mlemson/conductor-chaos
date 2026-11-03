// Conductor Chaos — water in de scène zetten (issue #42).
// Menu: Conductor Chaos > Art > Water > Create Water. Het water komt midden in de Scene view, op de grond eronder.
// Tip: graaf eerst met Raise or Lower (Shift) een kuil in het terrein en zet het water net onder de rand.
// Formaat, diepte, zinken en onderwaterkleur staan op het component StylizedWater.
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class WaterEditor
    {
        const string SurfaceMat = "Assets/Art/Materials/Environment/M_Env_WaterSurface.mat";
        const string TintShader = "Assets/Art/Shaders/UnderwaterTint.shader";

        [MenuItem("Conductor Chaos/Art/Water/Create Water")]
        static void CreateWater()
        {
            var pos = Vector3.zero;
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                pos = view.pivot;
                if (Physics.Raycast(pos + Vector3.up * 500f, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
                    pos = hit.point;
            }
            var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            go.layer = 2;                                            // Ignore Raycast: grond-/grijpstralen gaan erdoorheen
            go.transform.position = pos;
            var w = go.AddComponent<StylizedWater>();
            w.surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMat);
            w.tintShader = AssetDatabase.LoadAssetAtPath<Shader>(TintShader);
            if (w.surfaceMaterial == null || w.tintShader == null)
                Debug.LogWarning("[Water] M_Env_WaterSurface of UnderwaterTint ontbreekt in Assets/Art.");
            w.Rebuild();
            Undo.RegisterCreatedObjectUndo(go, "Create Water");
            Selection.activeGameObject = go;
            Debug.Log("[Water] Water gemaakt. Stel formaat en diepte in op StylizedWater; graaf het terrein eronder uit " +
                      "(Raise or Lower met Shift) zodat de bodem lager ligt dan het oppervlak.", go);
        }
    }
}
