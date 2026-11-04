using System.Collections.Generic;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Rigidbody))]
    public sealed class TrainDrive : NetworkBehaviour
    {
        [SerializeField] private TrackPiece firstTrack;
        [SerializeField] private float maximumSpeed = 6f;
        [SerializeField] private float acceleration = 2f;
        private readonly NetworkVariable<bool> powered = new(false);
        private readonly NetworkVariable<float> throttle = new(0f);
        private Rigidbody body;
        private TrackPiece track;
        private float distance, speed;
        private Transform lever;
        private Quaternion leverRest;
        private float railCenterOffset;
        private readonly Dictionary<TrackPiece, TrackPiece> previous = new();
        private bool panelVisible;

        public void Configure(TrackPiece first) => firstTrack = first;

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Regulator") { lever = t; leverRest = t.localRotation; }
                if (t.name == "Wheels" && t.TryGetComponent<Renderer>(out var wheels))
                    railCenterOffset = transform.InverseTransformPoint(wheels.bounds.center).z;
            }
            if (!IsServer) return;
            track = firstTrack;
            foreach (var piece in FindObjectsByType<TrackPiece>(FindObjectsSortMode.None))
                if (piece.next && !previous.ContainsKey(piece.next)) previous.Add(piece.next, piece);
            if (!track || !track.HasSockets || track.Length < 0.01f)
                Debug.LogError("TrainDrive: configure a connected starting track.", this);
        }

        private void Update()
        {
            if (lever) lever.localRotation = leverRest * Quaternion.Euler(0, 0, -throttle.Value * 35f);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !track || !track.HasSockets) return;
            speed = Mathf.MoveTowards(speed, powered.Value ? throttle.Value * maximumSpeed : 0f, acceleration * Time.fixedDeltaTime);
            var oldTrack = track;
            float oldDistance = distance;
            float remaining = speed * Time.fixedDeltaTime;
            for (int i = 0; i < 64 && Mathf.Abs(remaining) > 0.0001f; i++)
            {
                float length = track.Length;
                if (length < 0.01f) { speed = 0; break; }
                float nextDistance = distance + remaining;
                if (nextDistance >= 0f && nextDistance <= length) { distance = nextDistance; break; }
                if (nextDistance > length && track.next && track.next.HasSockets && !track.next.name.Contains("Broken"))
                {
                    remaining = nextDistance - length;
                    track = track.next;
                    distance = 0f;
                }
                else if (nextDistance < 0f && previous.TryGetValue(track, out var before) && before && before.HasSockets && !before.name.Contains("Broken"))
                {
                    remaining = nextDistance;
                    track = before;
                    distance = track.Length;
                }
                else { distance = Mathf.Clamp(nextDistance, 0f, length); speed = 0f; break; }
            }
            if (track.Length < 0.01f) return;
            track.Evaluate(distance / track.Length, out var railPosition, out var direction);
            if (direction.sqrMagnitude < 0.01f) return;
            var up = track.transform.up;
            var rotation = Quaternion.LookRotation(direction, up) * Quaternion.Euler(0f, -90f, 0f);
            Vector3 target = railPosition + up * TrackPiece.RailToTrainPivot - rotation * new Vector3(0f, 0f, railCenterOffset);
            Vector3 movement = target - body.position;
            if (movement.sqrMagnitude > 0.000001f)
            {
                Vector3 forward = movement.normalized;
                Vector3 center = body.position + body.rotation * new Vector3(0f, 0f, railCenterOffset);
                Vector3 front = center + forward * (speed >= 0 ? 3.4f : 2.6f) + up * 0.8f;
                foreach (var hit in Physics.BoxCastAll(front, new Vector3(1.1f, 0.6f, 0.1f), forward,
                             Quaternion.LookRotation(forward, up), movement.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<TrackPiece>() ||
                        hit.collider is TerrainCollider || hit.collider.GetComponentInParent<PlaygroundPlayer>()) continue;
                    track = oldTrack;
                    distance = oldDistance;
                    speed = 0f;
                    return;
                }
            }
            body.MovePosition(target);
            body.MoveRotation(rotation);
        }

        [ServerRpc(RequireOwnership = false)]
        private void DriveServerRpc(bool on, float input, ServerRpcParams rpc = default)
        {
            if (!float.IsFinite(input)) return;
            bool nearby = false;
            foreach (var player in FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.OwnerClientId == rpc.Receive.SenderClientId &&
                    Vector3.Distance(player.transform.position, transform.position) <= 5f) { nearby = true; break; }
            if (!nearby) return;
            powered.Value = on;
            throttle.Value = Mathf.Clamp(input, -1f, 1f);
        }

        private void OnGUI()
        {
            panelVisible = false;
            if (!IsSpawned || !NetworkManager) return;
            foreach (var player in FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.IsOwner && Vector3.Distance(player.transform.position, transform.position) <= 5f)
                    { panelVisible = true; break; }
            if (!panelVisible) return;
            const float width = 300f;
            GUILayout.BeginArea(new Rect(15, Screen.height - 125, width, 110), GUI.skin.box);
            GUILayout.Label("Treinbediening — Esc voor muis");
            if (GUILayout.Button(powered.Value ? "Trein uit" : "Trein starten")) DriveServerRpc(!powered.Value, throttle.Value);
            GUILayout.Label("Achteruit                 Stop                 Vooruit");
            float value = GUILayout.HorizontalSlider(throttle.Value, -1f, 1f);
            if (Mathf.Abs(value - throttle.Value) > 0.02f) DriveServerRpc(powered.Value, value);
            GUILayout.EndArea();
        }

        public static bool PointerOverPanel()
        {
            if (Mouse.current == null) return false;
            Vector2 p = Mouse.current.position.ReadValue();
            var rect = new Rect(15, Screen.height - 125, 300, 110);
            foreach (var train in FindObjectsByType<TrainDrive>(FindObjectsSortMode.None))
                if (train.panelVisible && rect.Contains(new Vector2(p.x, Screen.height - p.y))) return true;
            return false;
        }
    }
}

