// Conductor Chaos — gereedschap in de TrainPlayground zetten (issues #215, #216).
// Tools > Conductor Chaos > Place Wrench, Ticket Punch & Tickets:
//   * legt een moersleutel en een kaartjestang naast de spawn van de speler (netwerkprops uit de stationskit);
//   * geeft elke fysieke reiziger (PlaygroundPassenger) een kaartje om op te houden (PassengerTicket).
// De wissel zet je zelf in je spoor met de Track Builder (Track_Switch_20m_Left/Right); het kapotte stuk
// (Track_Broken_6m) is te repareren met de moersleutel.
using System.Linq;
using ConductorChaos.EditorTools;
using ConductorChaos.Playground;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConductorChaos.Editor
{
    public static class ConductorToolsSetup
    {
        const string PropDir = StationKitPrefabs.PrefabDir + "/Props";

        [MenuItem("Tools/Conductor Chaos/Place Wrench, Ticket Punch & Tickets")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/TrainPlayground.unity" || scene.isDirty)
            {
                Debug.LogError("Open en bewaar TrainPlayground eerst; er is niets veranderd.");
                return;
            }
            var wrench = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Wrench.prefab");
            var punch = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_TicketPunch.prefab");
            var ticket = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Ticket.prefab");
            if (!wrench || !punch || !ticket || !wrench.GetComponent<RepairWrench>() || !punch.GetComponent<TicketPunch>())
            {
                StationKitPrefabs.Build();
                wrench = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Wrench.prefab");
                punch = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_TicketPunch.prefab");
                ticket = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropDir}/Prop_Ticket.prefab");
            }
            if (!wrench || !punch || !ticket)
            {
                Debug.LogError("Prop_Wrench / Prop_TicketPunch / Prop_Ticket ontbreken in " + PropDir + "; er is niets veranderd.");
                return;
            }
            var player = GameObject.Find("Playground Player");
            Transform anchor = player ? player.transform : null;
            int placed = 0;
            placed += PlaceNear(wrench, anchor, new Vector3(1.2f, 0f, 2.2f), scene) ? 1 : 0;
            placed += PlaceNear(punch, anchor, new Vector3(-1.2f, 0f, 2.2f), scene) ? 1 : 0;
            int tickets = 0;
            foreach (var passenger in Object.FindObjectsByType<PlaygroundPassenger>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (passenger.gameObject.scene != scene) continue;
                var holder = passenger.GetComponent<PassengerTicket>();
                if (!holder) holder = Undo.AddComponent<PassengerTicket>(passenger.gameObject);
                Undo.RecordObject(holder, "Ticket");
                holder.Configure(ticket);
                EditorUtility.SetDirty(holder);
                tickets++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Tools] {placed} gereedschap geplaatst, {tickets} reizigers met een kaartje. " +
                      "Wissel: Conductor Chaos > Art > Track > Track Builder (Switch 20m Left/Right).");
        }

        static bool PlaceNear(GameObject prefab, Transform anchor, Vector3 offset, Scene scene)
        {
            if (scene.GetRootGameObjects().Any(root => root.name == prefab.name)) return false;   // ligt er al
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(go, "Place " + prefab.name);
            Vector3 place = anchor ? anchor.position + anchor.rotation * offset : offset;
            foreach (var hit in Physics.RaycastAll(place + Vector3.up * 10f, Vector3.down, 25f, ~0, QueryTriggerInteraction.Ignore)
                         .OrderBy(h => h.distance))
                if (!hit.rigidbody && hit.normal.y > 0.55f) { place.y = hit.point.y + 0.25f; break; }
            // plat neerleggen (de modellen liggen in hun xy-vlak)
            go.transform.SetPositionAndRotation(place, (anchor ? Quaternion.Euler(0f, anchor.eulerAngles.y, 0f) : Quaternion.identity) *
                                                       Quaternion.Euler(90f, 0f, 0f));
            return true;
        }
    }
}
