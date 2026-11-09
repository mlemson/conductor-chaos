// Conductor Chaos — zet CharacterFace (oogstanden, monden, knipperen) op personages in de open scène (issue #205).
// Menu: Tools > Conductor Chaos > Characters > Add Face (Blinking) To Scene Characters.
// Cosmetisch en lokaal: geen netwerkstatus.
using ConductorChaos.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.EditorTools
{
    public static class CharacterFaceTools
    {
        /// <summary>Het modelobject van een personage: de ouder van "Body" (die heeft Eye_L/R als kinderen).</summary>
        public static Transform ModelRoot(Transform eye)
        {
            for (var t = eye; t != null; t = t.parent)
                if (t.name == "Body") return t.parent ? t.parent : t;
            return null;
        }

        /// <summary>Voegt CharacterFace toe (als die er nog niet is) en neemt de standaardexpressie van het model over.</summary>
        public static CharacterFace Ensure(GameObject model, bool undo)
        {
            var face = model.GetComponent<CharacterFace>();
            if (face) return face;
            face = undo ? Undo.AddComponent<CharacterFace>(model) : model.AddComponent<CharacterFace>();
            face.CaptureFromModel();
            return face;
        }

        [MenuItem("Tools/Conductor Chaos/Characters/Add Face (Blinking) To Scene Characters")]
        public static void AddToSceneCharacters()
        {
            int added = 0, existing = 0;
            var eyes = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                    foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                        if (tr.name == "Eye_L") eyes.Add(tr);
            }
            foreach (var t in eyes)
            {
                var root = ModelRoot(t);
                if (!root) continue;
                if (root.GetComponent<CharacterFace>()) { existing++; continue; }
                Ensure(root.gameObject, undo: true);
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                added++;
            }
            Debug.Log($"[CharacterFace] {added} personage(s) kregen een gezicht met knipperen, {existing} hadden er al één.");
        }
    }
}
