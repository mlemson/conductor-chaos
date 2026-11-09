using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    [EditorTool("Magnetisch perron plaatsen")]
    public sealed class PlatformSnapTool : EditorTool
    {
        const string MenuPath = "Conductor Chaos/Art/Station/Magnetisch perron plaatsen";
        const float SnapDistance = 0.85f;
        const float PlatformTop = 1.05f;
        const float TrackEdge = 1.65f;
        const float PlatformDepth = 3.5f;
        const float SeamTolerance = 0.02f;

        static readonly GUIContent Icon = new GUIContent("Perron", "Klik op een vrije aansluiting of sleep het stuk ernaartoe");
        static readonly Quaternion Opposite = Quaternion.Euler(0f, 180f, 0f);
        readonly List<Edge> neighbours = new List<Edge>();
        Transform moving;
        Edge[] movingEdges;
        bool dragging;
        bool refreshNeighbours;

        public override GUIContent toolbarIcon => Icon;

        [MenuItem(MenuPath)]
        static void Activate() => ToolManager.SetActiveTool<PlatformSnapTool>();

        [MenuItem(MenuPath, true)]
        static bool CanActivate() => SelectedPlatform() != null;

        public override bool IsAvailable() => CanActivate();

        static Transform SelectedPlatform()
        {
            var selected = Selection.activeGameObject;
            if (Selection.gameObjects.Length != 1 || selected == null || EditorUtility.IsPersistent(selected)
                || EditorApplication.isPlayingOrWillChangePlaymode) return null;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(selected);
            return root != null && PlatformName(root.transform) != null ? root.transform : null;
        }

        public override void OnActivated() => EditorApplication.hierarchyChanged += InvalidateNeighbours;

        public override void OnWillBeDeactivated()
        {
            EditorApplication.hierarchyChanged -= InvalidateNeighbours;
            moving = null;
            dragging = false;
        }

        void InvalidateNeighbours() => refreshNeighbours = true;

        public override void OnToolGUI(EditorWindow window)
        {
            if (!(window is SceneView)) return;
            var platform = SelectedPlatform();
            if (platform == null) return;
            if (moving != platform)
            {
                moving = platform;
                dragging = false;
                refreshNeighbours = true;
            }
            if (refreshNeighbours) CollectEdges(platform);

            var input = Event.current.rawType;
            var position = Handles.PositionHandle(platform.position, Tools.pivotRotation == PivotRotation.Local
                ? platform.rotation : Quaternion.identity);
            if (position != platform.position)
            {
                if (!dragging)
                {
                    Undo.RegisterCompleteObjectUndo(platform, "Perron plaatsen");
                    dragging = true;
                }
                platform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
            }

            if (!dragging)
            {
                DrawFreeConnections();
                return;
            }
            if (input == EventType.MouseUp)
            {
                if (TrySnap(out var point, out var rotation, out _))
                {
                    platform.SetPositionAndRotation(point, rotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
                }
                dragging = false;
                SceneView.RepaintAll();
            }
            else if (input == EventType.Repaint && TrySnap(out _, out _, out var seam))
            {
                using (new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.65f)))
                {
                    var across = seam.Rotation * Vector3.right * (PlatformDepth * seam.Scale * 0.5f);
                    Handles.DrawAAPolyLine(4f, seam.Position - across, seam.Position + across);
                    Handles.Label(seam.Position, "Perron klikt vast");
                }
            }
        }

        void CollectEdges(Transform platform)
        {
            refreshNeighbours = false;
            movingEdges = Edges(platform);
            neighbours.Clear();
            foreach (var candidate in Object.FindObjectsByType<Transform>())
            {
                if (candidate == platform || candidate.gameObject.scene != platform.gameObject.scene
                    || candidate.IsChildOf(platform) || platform.IsChildOf(candidate)) continue;
                neighbours.AddRange(Edges(candidate));
            }
        }

        void DrawFreeConnections()
        {
            using (new Handles.DrawingScope(new Color(0.25f, 0.9f, 0.65f)))
            {
                foreach (var target in neighbours)
                {
                    foreach (var source in movingEdges)
                    {
                        if (!source.Matches(target)) continue;
                        if (IsOccupied(target)) break;

                        float size = HandleUtility.GetHandleSize(target.Position) * 0.075f;
                        if (Handles.Button(target.Position, target.Rotation, size, size * 1.5f, Handles.DotHandleCap))
                        {
                            Undo.RegisterCompleteObjectUndo(moving, "Perron vastklikken");
                            var pose = source.PoseAt(target);
                            moving.SetPositionAndRotation(pose.position, pose.rotation);
                            PrefabUtility.RecordPrefabInstancePropertyModifications(moving);
                            SceneView.RepaintAll();
                            return;
                        }
                        Handles.Label(target.Position + Vector3.up * size * 1.5f, "Klik om aan te sluiten");
                        break;
                    }
                }
            }
        }

        bool TrySnap(out Vector3 position, out Quaternion rotation, out Edge seam)
        {
            position = default;
            rotation = default;
            seam = default;
            var best = SnapDistance * SnapDistance;
            foreach (var source in movingEdges)
            {
                var sourcePosition = source.Position;
                foreach (var target in neighbours)
                {
                    if (!source.Matches(target)) continue;
                    var pose = source.PoseAt(target);
                    var distance = Mathf.Min((target.Position - sourcePosition).sqrMagnitude,
                        (pose.position - moving.position).sqrMagnitude);
                    if (distance >= best) continue;
                    if (Quaternion.Angle(moving.rotation, pose.rotation) > 100f || IsOccupied(target)) continue;
                    best = distance;
                    seam = target;
                    position = pose.position;
                    rotation = pose.rotation;
                }
            }
            return best < SnapDistance * SnapDistance;
        }

        bool IsOccupied(Edge target)
        {
            foreach (var other in neighbours)
            {
                if (target.Root == other.Root || !target.Matches(other)) continue;
                if ((target.Position - other.Position).sqrMagnitude < SeamTolerance * SeamTolerance
                    && Quaternion.Angle(target.Rotation * Opposite, other.Rotation) < 2f) return true;
            }
            return false;
        }

        static string PlatformName(Transform root)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(root.gameObject)) return null;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root.gameObject);
            return path != null && path.StartsWith(StationKitPrefabs.PrefabDir + "/Platform/", System.StringComparison.Ordinal)
                ? Path.GetFileNameWithoutExtension(path) : null;
        }

        static Edge[] Edges(Transform root)
        {
            var name = PlatformName(root);
            if (name == null) return System.Array.Empty<Edge>();
            var scale = root.lossyScale;
            if (scale.x <= 0f || !Mathf.Approximately(scale.x, scale.y) || !Mathf.Approximately(scale.x, scale.z))
                return System.Array.Empty<Edge>();

            var collider = root.Find("Col_Main");
            if (collider == null || !collider.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
                return System.Array.Empty<Edge>();

            // Read the imported axis signs and actual length from the collision mesh. Its outline has no bevel.
            var meshToRoot = root.worldToLocalMatrix * collider.localToWorldMatrix;
            var bounds = filter.sharedMesh.bounds;
            var center = meshToRoot.MultiplyPoint3x4(bounds.center);
            float xSign = Mathf.Sign(center.x), zSign = Mathf.Sign(center.z);
            float length = Mathf.Abs(center.x) + Mathf.Abs(meshToRoot.m00) * bounds.extents.x
                + Mathf.Abs(meshToRoot.m01) * bounds.extents.y + Mathf.Abs(meshToRoot.m02) * bounds.extents.z;

            Edge At(float x, float z, Vector3 outward, bool start) => new Edge(root,
                new Vector3(x * xSign, PlatformTop, z * zSign),
                Quaternion.LookRotation(new Vector3(outward.x * xSign, 0f, outward.z * zSign), Vector3.up),
                scale.x, start);

            float middle = TrackEdge + PlatformDepth * 0.5f;
            var left = At(0f, middle, Vector3.left, true);
            var right = At(length, middle, Vector3.right, false);
            switch (name)
            {
                case "Platform_Straight_3m":
                case "Platform_Straight_6m":
                    return new[] { left, right };
                case "Platform_Corner_End":
                    return new[] { left, At(length * 0.5f, TrackEdge + PlatformDepth, Vector3.forward, false) };
                case "Platform_Corner_Start":
                    return new[] { right, At(length * 0.5f, TrackEdge + PlatformDepth, Vector3.forward, true) };
                case "Platform_EndCap_End":
                case "Platform_Ramp_End":
                case "Platform_Stairs_End":
                    return new[] { left };
                case "Platform_EndCap_Start":
                case "Platform_Ramp_Start":
                case "Platform_Stairs_Start":
                    return new[] { right };
                default:
                    return System.Array.Empty<Edge>();
            }
        }

        readonly struct Edge
        {
            public readonly Transform Root;
            readonly Vector3 localPoint;
            readonly Quaternion localRotation;
            public readonly float Scale;
            readonly bool start;

            public Edge(Transform root, Vector3 point, Quaternion rotation, float scale, bool start)
            {
                Root = root;
                localPoint = point;
                localRotation = rotation;
                Scale = scale;
                this.start = start;
            }

            public Vector3 Position => Root.TransformPoint(localPoint);
            public Quaternion Rotation => Root.rotation * localRotation;

            public Pose PoseAt(Edge target)
            {
                var turn = target.Rotation * Opposite * Quaternion.Inverse(Rotation);
                return new Pose(target.Position - turn * (Position - Root.position), turn * Root.rotation);
            }

            // Pair opposite seam types to keep the tactile strips on the same side.
            public bool Matches(Edge other) => start != other.start && Mathf.Approximately(Scale, other.Scale);
        }
    }
}
