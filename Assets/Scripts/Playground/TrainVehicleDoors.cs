using System;
using System.Collections.Generic;
using ConductorChaos.Track;
using UnityEngine;

namespace ConductorChaos.Playground
{
    // Shared door poses live in TrainBreakdowns. Local rigidbodies expose hand grips on each peer.
    internal sealed class TrainVehicleDoors : IDisposable
    {
        private static readonly HashSet<Rigidbody> Bodies = new();
        private readonly List<Door> doors = new();
        internal int Count => doors.Count;
        internal float Initial(int i) => doors[i].Hinged ? 1f : 0f;
        internal static bool IsDoor(Rigidbody body) => body && Bodies.Contains(body);

        internal TrainVehicleDoors(GameObject root)
        {
            bool automatic = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material && (material.name.StartsWith("M_Modern_", StringComparison.Ordinal) ||
                        material.name.StartsWith("M_Future_", StringComparison.Ordinal))) automatic = true;

            foreach (var pair in TrainDrive.FindEndDoors(root))
            {
                Vector3 center = (pair.LeftRest + pair.RightRest) * .5f;
                Add(pair.Left, pair.Slide, center, pair.Automatic || automatic);
                Add(pair.Right, -pair.Slide, center, pair.Automatic || automatic);
            }
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                string name = filter.name;
                if (!filter.sharedMesh) continue;
                bool side = name == "SideDoorLeaf" || name == "SideDoorLeaf_Far" ||
                    name is "SideDoorLeaf_L" or "SideDoorLeaf_R" or "SideDoorLeaf_L_Far" or "SideDoorLeaf_R_Far";
                bool cattle = name is "SlideDoor_Near" or "SlideDoor_Far";
                bool hatch = name.StartsWith("RoofHatch_", StringComparison.Ordinal);
                if (!side && !cattle && !hatch) continue;
                Vector3 slide = Vector3.zero, center = filter.transform.localPosition;
                if (!hatch)
                {
                    bool paired = name.Contains("_L", StringComparison.Ordinal) || name.Contains("_R", StringComparison.Ordinal);
                    if (paired)
                    {
                        var other = RepairPoint.FindDeep(filter.transform.parent,
                            name.Contains("_L", StringComparison.Ordinal) ? name.Replace("_L", "_R") : name.Replace("_R", "_L"));
                        if (other) center = (center + other.localPosition) * .5f;
                    }
                    bool positive = name.StartsWith("SideDoorLeaf_R", StringComparison.Ordinal);
                    float distance = cattle ? 1.55f : filter.sharedMesh.bounds.size.x + .04f;
                    slide = filter.transform.localRotation * Vector3.right * (positive ? distance : -distance);
                }
                Add(filter.transform, slide, center, automatic && !hatch && !cattle, hatch);
            }
        }

        private void Add(Transform leaf, Vector3 slide, Vector3 center, bool automatic, bool hinged = false)
        {
            if (!leaf || leaf.GetComponent<Rigidbody>() || !leaf.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh) return;
            doors.Add(new Door(leaf, slide, center, automatic, hinged, filter.sharedMesh.bounds));
        }

        internal float ReadInput(int i, float opening, bool allowAutomatic) => doors[i].ReadInput(opening, allowAutomatic);
        internal void Present(int i, float opening) => doors[i].Present(opening);

        public void Dispose()
        {
            foreach (var door in doors) door.Dispose();
            doors.Clear();
        }

        private sealed class Door : IDisposable
        {
            private readonly Transform leaf;
            private readonly Vector3 rest, slide, center;
            private readonly Quaternion rotation;
            private readonly Rigidbody body;
            private readonly BoxCollider collider;
            private readonly Collider[] original;
            private readonly bool[] enabled;
            private readonly bool automatic;
            private Transform hand;
            private Vector3 handAtGrab;
            private float atGrab, holdUntil, pitchAtGrab;
            internal bool Hinged { get; }

            internal Door(Transform leaf, Vector3 slide, Vector3 center, bool automatic, bool hinged, Bounds bounds)
            {
                this.leaf = leaf;
                this.slide = slide;
                this.center = center;
                this.automatic = automatic;
                Hinged = hinged;
                rest = leaf.localPosition;
                rotation = leaf.localRotation;
                original = leaf.GetComponentsInChildren<Collider>(true);
                enabled = new bool[original.Length];
                for (int i = 0; i < original.Length; i++)
                {
                    enabled[i] = original[i].enabled;
                    original[i].enabled = false;
                }
                collider = leaf.gameObject.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = Vector3.Max(bounds.size, Vector3.one * .07f);
                body = leaf.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.None;
                Bodies.Add(body);
                if (!hinged)
                {
                    Debug.Assert(Mathf.Approximately(SlideTarget(0f, slide, slide), 1f));
                    Debug.Assert(Mathf.Approximately(SlideTarget(1f, -slide, slide), 0f));
                    Debug.Assert(Mathf.Approximately(SlideTarget(.5f, Vector3.zero, slide), .5f));
                }
            }

            internal float ReadInput(float opening, bool allowAutomatic)
            {
                if (!leaf || !leaf.gameObject.activeInHierarchy) return opening;
                if (automatic)
                {
                    bool near = false;
                    Vector3 doorCenter = leaf.parent.TransformPoint(center + Vector3.up * .8f);
                    foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                        if (player && player.IsSpawned && player.gameObject.scene == leaf.gameObject.scene)
                        {
                            Vector3 offset = player.transform.position + Vector3.up * .6f - doorCenter;
                            if (Mathf.Abs(offset.y) < 1.3f && Vector3.ProjectOnPlane(offset, leaf.parent.up).sqrMagnitude < 1.8f * 1.8f)
                            { near = true; break; }
                        }
                    if (near && allowAutomatic) holdUntil = Time.time + .7f;
                    return Mathf.MoveTowards(opening, allowAutomatic && (near || Time.time < holdUntil) ? 1f : 0f,
                        Time.fixedDeltaTime * 2.4f);
                }

                foreach (var player in PlaygroundPlayer.SpawnedPlayers)
                {
                    if (!player || !player.IsSpawned ||
                        !player.TryGetControlHand(body, out var grip, out var point, out _, useAim: true)) continue;
                    Vector3 local = leaf.parent.InverseTransformPoint(point);
                    if (hand != grip)
                    {
                        hand = grip;
                        handAtGrab = local;
                        atGrab = opening;
                        pitchAtGrab = player.CabAimPitch;
                        return opening;
                    }
                    float target = Hinged ? atGrab + (pitchAtGrab - player.CabAimPitch) / 65f :
                        SlideTarget(atGrab, local - handAtGrab, slide);
                    return Mathf.MoveTowards(opening, Mathf.Clamp01(target), Time.fixedDeltaTime * 2.4f);
                }
                hand = null;
                return opening;
            }

            private static float SlideTarget(float opening, Vector3 delta, Vector3 direction) =>
                Mathf.Clamp01(opening + Vector3.Dot(delta, direction) / Mathf.Max(.01f, direction.sqrMagnitude));

            internal void Present(float opening)
            {
                if (!leaf) return;
                if (Hinged) leaf.localRotation = Quaternion.Slerp(Quaternion.identity, rotation, opening);
                else leaf.localPosition = rest + slide * opening;
            }

            public void Dispose()
            {
                Bodies.Remove(body);
                if (leaf) leaf.SetLocalPositionAndRotation(rest, rotation);
                if (collider) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
                if (body) UnityEngine.Object.Destroy(body);
                for (int i = 0; i < original.Length; i++)
                    if (original[i]) original[i].enabled = enabled[i];
            }
        }
    }
}
