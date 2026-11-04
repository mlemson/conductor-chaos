using System;
using ConductorChaos.Track;
using UnityEditor;
using UnityEngine;

namespace ConductorChaos.EditorTools
{
    public static class TrackTerrainCheck
    {
        [MenuItem("Conductor Chaos/Art/Track/Check Terrain Fitting")]
        public static void Run()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(20, 10, 20) };
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++)
                for (int x = 0; x < 33; x++) heights[z, x] = x / 160f;
            data.SetHeights(0, 0, heights);
            var terrain = Terrain.CreateTerrainGameObject(data);
            var root = new GameObject("Track fitting test");
            try
            {
                var piece = root.AddComponent<TrackPiece>();
                piece.start = Socket(root, "start", new Vector3(2, 1.43f, 5));
                piece.startForward = Socket(root, "startForward", new Vector3(3, 1.43f, 5));
                piece.end = Socket(root, "end", new Vector3(8, 1.43f, 5));
                piece.endForward = Socket(root, "endForward", new Vector3(9, 1.43f, 5));
                var anchor = piece.StartPosition;
                TrackBuilderWindow.FitToTerrain(piece, false);
                float expected = data.GetInterpolatedHeight(piece.EndPosition.x / 20f, piece.EndPosition.z / 20f) + 0.43f;
                if (Vector3.Distance(anchor, piece.StartPosition) > 0.001f || Mathf.Abs(piece.EndPosition.y - expected) > 0.02f)
                    throw new Exception("Track terrain fitting failed to preserve the connection or match the slope.");
                Debug.Log("Track terrain fitting check passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(terrain);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        static Transform Socket(GameObject root, string name, Vector3 position)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(root.transform, false);
            socket.position = position;
            return socket;
        }
    }
}

