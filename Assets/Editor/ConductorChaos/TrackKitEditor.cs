// Conductor Chaos — spoorkit in Unity (issue #53).
// 1) Prefabs: van elk model in Assets/Art/Models/Environment/Track wordt een prefab (variant van het model) gemaakt
//    in Assets/Art/Prefabs/Track, met TrackPiece (sporen) en een MeshCollider op de verborgen botsvorm Col_Main.
//    Bochten krijgen Left/Right in de naam na meten van hun echte richting in Unity.
//    Gebeurt automatisch als de prefabs ontbreken; opnieuw via Conductor Chaos > Art > Track > Rebuild Track Prefabs.
// 2) Track Builder (Conductor Chaos > Art > Track > Track Builder): selecteer een spoorstuk en klik een stuk aan;
//    het nieuwe stuk wordt precies aan het eind vastgezet en geselecteerd; met Achteruit bouwen aan het begin.
//    Zonder selectie komt het eerste stuk midden in de Scene view op de grond.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConductorChaos.Track;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [InitializeOnLoad]
    public static class TrackKitPrefabs
    {
        public const string ModelDir = "Assets/Art/Models/Environment/Track";
        public const string PrefabDir = "Assets/Art/Prefabs/Track";

        static TrackKitPrefabs()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!AssetDatabase.IsValidFolder(ModelDir)) return;
                int models = AssetDatabase.FindAssets("t:Model", new[] { ModelDir }).Length;
                int prefabs = AssetDatabase.IsValidFolder(PrefabDir) ? AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }).Length : 0;
                if (models > 0 && prefabs < models) Build();
            };
        }

        internal static void BuildDeferred()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Build();
        }

        [MenuItem("Conductor Chaos/Art/Track/Rebuild Track Prefabs")]
        public static void Build()
        {
            EnsureFolder(PrefabDir);
            int made = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    bool isTrack = name.StartsWith("Track_");
                    if (isTrack)
                    {
                        var piece = go.AddComponent<TrackPiece>();
                        piece.FindSockets();
                        if (piece.HasSockets && (name.EndsWith("_A") || name.EndsWith("_B")))
                            name = name.Substring(0, name.Length - 2) + (piece.TurnDegrees > 0f ? "_Right" : "_Left");
                    }
                    foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    {
                        if (!t.name.StartsWith("Col_")) continue;
                        var mf = t.GetComponent<MeshFilter>();
                        var mr = t.GetComponent<MeshRenderer>();
                        if (mr != null) mr.enabled = false;
                        if (mf != null && mf.sharedMesh != null)
                        {
                            var col = t.gameObject.AddComponent<MeshCollider>();
                            col.sharedMesh = mf.sharedMesh;
                        }
                    }
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                               StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                    go.name = name;
                    PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                    made++;
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Track] {made} spoorprefabs gemaakt in {PrefabDir}. Bouwen: Conductor Chaos > Art > Track > Track Builder.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    /// <summary>Na het (her)importeren van spoormodellen de prefabs bijwerken.</summary>
    class TrackKitPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(p => p.StartsWith(TrackKitPrefabs.ModelDir) && p.EndsWith(".fbx"))) return;
            EditorApplication.delayCall -= TrackKitPrefabs.BuildDeferred;
            EditorApplication.delayCall += TrackKitPrefabs.BuildDeferred;
        }
    }

    public class TrackBuilderWindow : EditorWindow
    {
        List<GameObject> prefabs = new List<GameObject>();
        Vector2 scroll;
        [SerializeField] bool buildBackwards;

        [MenuItem("Conductor Chaos/Art/Track/Track Builder")]
        static void Open() => GetWindow<TrackBuilderWindow>("Track Builder");

        void OnEnable() => Reload();
        void OnFocus() => Reload();

        void Reload()
        {
            prefabs = AssetDatabase.IsValidFolder(TrackKitPrefabs.PrefabDir)
                ? AssetDatabase.FindAssets("t:Prefab", new[] { TrackKitPrefabs.PrefabDir })
                    .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(p => p != null).OrderBy(p => Order(p.name)).ThenBy(p => p.name).ToList()
                : new List<GameObject>();
        }

        static int Order(string n) => n.StartsWith("Track_Straight") ? 0 : n.StartsWith("Track_Curve") ? 1 : n.StartsWith("Track_") ? 2 : 3;

        void OnGUI()
        {
            var sel = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<TrackPiece>() : null;
            if (sel != null && !sel.gameObject.scene.IsValid()) sel = null;
            buildBackwards = EditorGUILayout.ToggleLeft(new GUIContent("Achteruit bouwen",
                "Plaats het volgende spoorstuk aan het begin van het geselecteerde stuk."), buildBackwards);
            EditorGUILayout.HelpBox(sel != null
                ? $"Nieuwe stukken komen aan het {(buildBackwards ? "begin" : "eind")} van '{sel.name}'."
                : "Geen spoorstuk geselecteerd: het eerste stuk komt midden in de Scene view op de grond.", MessageType.Info);
            if (prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox("Nog geen prefabs. Klik hieronder om ze te maken.", MessageType.Warning);
                if (GUILayout.Button("Rebuild Track Prefabs")) { TrackKitPrefabs.Build(); Reload(); }
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var p in prefabs)
            {
                if (GUILayout.Button(p.name.Replace("Track_", "").Replace("_", " "), GUILayout.Height(24)))
                    Place(p, sel);
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Prefabs opnieuw maken")) { TrackKitPrefabs.Build(); Reload(); }
        }

        void OnSelectionChange() => Repaint();

        void Place(GameObject prefab, TrackPiece selected)
        {
            if (buildBackwards && selected != null && selected.HasSockets &&
                prefab.TryGetComponent(out TrackPiece template) && template.HasSockets &&
                selected.gameObject.scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TrackPiece>(true))
                    .Any(track => track.next == selected))
            {
                ShowNotification(new GUIContent("Aan het begin zit al een spoorstuk vast."));
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Place Track Piece");
            var piece = go.GetComponent<TrackPiece>();
            if (selected != null && piece != null && piece.HasSockets && selected.HasSockets)
            {
                go.transform.SetParent(selected.transform.parent, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Vector3 targetDirection = buildBackwards ? selected.StartDirection : selected.EndDirection;
                Vector3 socketDirection = buildBackwards ? piece.EndDirection : piece.StartDirection;
                go.transform.rotation = Quaternion.LookRotation(targetDirection, selected.transform.up) *
                                        Quaternion.Inverse(Quaternion.LookRotation(socketDirection, Vector3.up));
                Vector3 target = buildBackwards ? selected.StartPosition : selected.EndPosition;
                go.transform.position += target - (buildBackwards ? piece.EndPosition : piece.StartPosition);

                FitToTerrain(piece, buildBackwards);

                TrackPiece previous = buildBackwards ? piece : selected;
                Undo.RecordObject(previous, "Link Track Piece");
                previous.next = buildBackwards ? selected : piece;
                PrefabUtility.RecordPrefabInstancePropertyModifications(previous);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            }
            else if (selected != null && selected.HasSockets)
            {
                // Prop (bord, slagboom): naast het uiteinde waarvandaan je bouwt.
                go.transform.SetParent(selected.transform.parent, false);
                Vector3 target = buildBackwards ? selected.StartPosition : selected.EndPosition;
                Vector3 direction = buildBackwards ? -selected.StartDirection : selected.EndDirection;
                Vector3 side = Vector3.Cross(selected.transform.up, direction).normalized;
                go.transform.SetPositionAndRotation(target + side * 2.6f - selected.transform.up * 0.43f,
                                                    Quaternion.LookRotation(direction, Vector3.up));
            }
            else
            {
                var pos = Vector3.zero;
                var view = SceneView.lastActiveSceneView;
                if (view != null)
                {
                    pos = view.pivot;
                    if (Physics.Raycast(pos + Vector3.up * 500f, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
                        pos = hit.point;
                }
                go.transform.position = pos;
                if (piece != null && piece.HasSockets && FollowsTerrain(piece))
                {
                    var terrain = PropGroundSnap.TerrainAt(piece.StartPosition);
                    if (terrain != null)
                    {
                        Vector3 start = piece.StartPosition;
                        float ground = terrain.SampleHeight(start) + terrain.transform.position.y;
                        go.transform.position += Vector3.up * (ground + 0.43f - start.y);
                        FitToTerrain(piece, false);
                    }
                }
            }
            Selection.activeGameObject = piece != null ? go : (selected != null ? selected.gameObject : go);
        }

        internal static void FitToTerrain(TrackPiece piece, bool backwards)
        {
            if (!FollowsTerrain(piece)) return;
            // shortcut: rigid pieces only fit their endpoints; use deformable track for uneven ground between them.
            for (int i = 0; i < 4; i++)
            {
                Vector3 anchor = backwards ? piece.EndPosition : piece.StartPosition;
                Vector3 far = backwards ? piece.StartPosition : piece.EndPosition;
                var terrain = PropGroundSnap.TerrainAt(far);
                if (terrain == null) return;
                float ground = terrain.SampleHeight(far) + terrain.transform.position.y;
                Vector3 chord = far - anchor;
                Vector3 horizontal = Vector3.ProjectOnPlane(chord, Vector3.up);
                if (horizontal.sqrMagnitude < 0.001f) return;
                float desiredHeight = ground + 0.43f - anchor.y;
                float currentAngle = Mathf.Atan2(chord.y, horizontal.magnitude);
                float targetAngle = Mathf.Asin(Mathf.Clamp(desiredHeight / chord.magnitude, -0.5f, 0.5f));
                Vector3 axis = Vector3.Cross(horizontal.normalized, Vector3.up);
                piece.transform.RotateAround(anchor, axis, (targetAngle - currentAngle) * Mathf.Rad2Deg);
            }
        }

        static bool FollowsTerrain(TrackPiece piece) =>
            !piece.name.Contains("Bridge") && !piece.name.Contains("Ramp") && !piece.name.Contains("Hill");
    }
}
