// Conductor Chaos — inspector voor CliffMass (issue #223): instellingen wijzigen bouwt de klif direct opnieuw.
using ConductorChaos.Art;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [CustomEditor(typeof(CliffMass))]
    [CanEditMultipleObjects]
    public class CliffMassEditor : UnityEditor.Editor   // volledig: het project heeft ook een namespace ConductorChaos.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            if (targets.Length == 1)
            {
                var m = (CliffMass)target;
                int max = 0;
                foreach (var kv in m.Cells) max = Mathf.Max(max, kv.Value);
                EditorGUILayout.HelpBox($"{m.CellCount} cellen · hoogste trede {max} ({max * m.stepHeight:0.#} m)", MessageType.None);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Opnieuw opbouwen")) changed = true;
                if (GUILayout.Button("Klif Massa Painter")) CliffMassPainterWindow.Open();
            }
            if (!changed) return;
            foreach (var t in targets)
            {
                var m = (CliffMass)t;
                var mf = m.GetComponent<MeshFilter>();
                var mc = m.GetComponent<MeshCollider>();
                var mr = m.GetComponent<MeshRenderer>();
                if (mr) Undo.RecordObject(mr, "Klif opnieuw opbouwen");
                if (mf) Undo.RecordObject(mf, "Klif opnieuw opbouwen");
                if (mc) Undo.RecordObject(mc, "Klif opnieuw opbouwen");
                CliffMassBuilder.Rebuild(m);
            }
        }
    }
}
