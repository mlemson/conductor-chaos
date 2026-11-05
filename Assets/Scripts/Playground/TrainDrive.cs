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
        [SerializeField] private float startDistance;
        [SerializeField] private float maximumSpeed = 6f;
        [SerializeField] private float acceleration = 2f;
        private readonly NetworkVariable<bool> powered = new(false);
        private readonly NetworkVariable<float> throttle = new(0f);
        private Rigidbody body;
        private TrackPiece track;
        private float distance, speed;
        private Transform lever;
        private Transform startButton;
        private TextMesh buttonHint, leverHint;
        private PlaygroundPlayer localPlayer;
        private float nextPlayerLookup;
        private Quaternion leverRest;
        private float railCenterOffset;
        private readonly Dictionary<TrackPiece, TrackPiece> previous = new();

        public void Configure(TrackPiece first, float start) { firstTrack = first; startDistance = start; }

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
            CreateCabControls();
            if (!IsServer) return;
            track = firstTrack;
            if (track) distance = Mathf.Clamp(startDistance, 0f, track.Length);
            foreach (var piece in FindObjectsByType<TrackPiece>(FindObjectsSortMode.None))
                if (piece.next && !previous.ContainsKey(piece.next)) previous.Add(piece.next, piece);
            if (!track || !track.HasSockets || track.Length < 0.01f)
                Debug.LogError("TrainDrive: configure a connected starting track.", this);
        }

        private void Update()
        {
            if (lever) lever.localRotation = leverRest * Quaternion.Euler(0, 0, -throttle.Value * 35f);
            if (startButton) startButton.localScale = new Vector3(0.18f, powered.Value ? 0.11f : 0.18f, 0.18f);
            if (!IsSpawned || !lever || !startButton) return;
            if (localPlayer && (!localPlayer.IsSpawned || !localPlayer.IsOwner)) localPlayer = null;
            if (!localPlayer && Time.unscaledTime >= nextPlayerLookup)
            {
                nextPlayerLookup = Time.unscaledTime + 1f;
                foreach (var player in FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None))
                    if (player.IsSpawned && player.IsOwner) { localPlayer = player; break; }
            }
            if (!localPlayer) { buttonHint.gameObject.SetActive(false); leverHint.gameObject.SetActive(false); return; }
            Vector3 hand = localPlayer.transform.position + Vector3.up * 0.9f;
            float buttonDistance = Vector3.Distance(hand, startButton.position);
            float leverDistance = Vector3.Distance(hand, lever.position + transform.up * 0.35f);
            bool canUse = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            bool useButton = canUse && buttonDistance <= 1.5f && buttonDistance <= leverDistance;
            bool useLever = canUse && leverDistance <= 1.5f && leverDistance < buttonDistance;
            buttonHint.gameObject.SetActive(useButton);
            leverHint.gameObject.SetActive(useLever);
            if (useButton) buttonHint.text = powered.Value ? "E  Uitzetten" : "E  Starten";
            if (useLever) leverHint.text = throttle.Value > 0.5f ? "E  Hendel: vooruit" : throttle.Value < -0.5f ? "E  Hendel: achteruit" : "E  Hendel: stop";
            Camera camera = Camera.main;
            if (camera && useButton) buttonHint.transform.rotation = camera.transform.rotation;
            if (camera && useLever) leverHint.transform.rotation = camera.transform.rotation;
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && (useButton || useLever))
                UseCabControlServerRpc(useLever);
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
            var rotation = Quaternion.LookRotation(direction, up) * Quaternion.Euler(0f, 90f, 0f);
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
        private void UseCabControlServerRpc(bool useLever, ServerRpcParams rpc = default)
        {
            if (!lever || !startButton) return;
            Vector3 control = useLever ? lever.position + transform.up * 0.35f : startButton.position;
            foreach (var player in FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.OwnerClientId == rpc.Receive.SenderClientId &&
                    Vector3.Distance(player.transform.position + Vector3.up * 0.9f, control) <= 1.5f)
                {
                    if (useLever) throttle.Value = throttle.Value > 0.5f ? -1f : throttle.Value < -0.5f ? 0f : 1f;
                    else powered.Value = !powered.Value;
                    return;
                }
        }

        private void CreateCabControls()
        {
            if (!lever) { Debug.LogError("TrainDrive: de cabinehendel Regulator ontbreekt.", this); return; }
            var button = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            button.name = "Startknop";
            startButton = button.transform;
            startButton.SetParent(lever.parent, false);
            startButton.position = lever.position + transform.up * 0.2f + transform.forward * 0.45f;
            startButton.localScale = Vector3.one * 0.18f;
            button.GetComponent<Collider>().isTrigger = true;
            if (lever.TryGetComponent<Renderer>(out var leverRenderer))
                button.GetComponent<Renderer>().sharedMaterial = leverRenderer.sharedMaterial;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.name == "M_Loco_Red") { button.GetComponent<Renderer>().sharedMaterial = material; break; }
                if (button.GetComponent<Renderer>().sharedMaterial && button.GetComponent<Renderer>().sharedMaterial.name == "M_Loco_Red") break;
            }
            buttonHint = MakeHint(lever.parent, startButton.localPosition + Vector3.up * 0.35f);
            leverHint = MakeHint(lever.parent, lever.localPosition + Vector3.up * 0.65f);
        }

        private static TextMesh MakeHint(Transform parent, Vector3 offset)
        {
            var hint = new GameObject("Bedieningshint").AddComponent<TextMesh>();
            hint.transform.SetParent(parent, false);
            hint.transform.localPosition = offset;
            hint.anchor = TextAnchor.MiddleCenter;
            hint.alignment = TextAlignment.Center;
            hint.fontSize = 48;
            hint.characterSize = 0.012f;
            hint.color = new Color(0.96f, 0.86f, 0.75f);
            hint.gameObject.SetActive(false);
            return hint;
        }

        public override void OnNetworkDespawn()
        {
            if (startButton) Destroy(startButton.gameObject);
            if (buttonHint) Destroy(buttonHint.gameObject);
            if (leverHint) Destroy(leverHint.gameObject);
            if (lever) lever.localRotation = leverRest;
            localPlayer = null;
            previous.Clear();
        }
    }
}

