using System.Collections.Generic;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

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
        private readonly NetworkVariable<bool> buttonDown = new(false);
        private readonly NetworkVariable<float> throttle = new(0f);
        private Rigidbody body;
        private TrackPiece track;
        private float distance, speed;
        private Transform lever;
        private Transform leverParent;
        private Transform leverPivot;
        private Rigidbody leverBody;
        private HingeJoint leverHinge;
        private PlaygroundPlayer[] players = System.Array.Empty<PlaygroundPlayer>();
        private float nextPlayerScan;
        private bool leverHeld;
        private Vector3 leverPosition, leverScale;
        private Collider[] leverColliders;
        private bool[] leverColliderEnabled;
        private readonly Collider[] buttonContacts = new Collider[16];
        private float nextButtonPress;
        private Transform startButton;
        private Quaternion leverRest;
        private float railCenterOffset;
        private readonly Dictionary<TrackPiece, TrackPiece> previous = new();

        public void Configure(TrackPiece first, float start) { firstTrack = first; startDistance = start; }
        internal bool IsLeverCollider(Collider candidate) => leverPivot && candidate && candidate.transform == leverPivot;

        public override void OnNetworkSpawn()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = IsServer ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            if (IsServer) { powered.Value = false; throttle.Value = 0f; buttonDown.Value = false; nextButtonPress = 0f; }
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
            else
            {
                track.Evaluate(distance / track.Length, out var position, out var direction);
                if (direction.sqrMagnitude > 0.01f)
                {
                    var rotation = Quaternion.LookRotation(direction, track.transform.up) * Quaternion.Euler(0f, 90f, 0f);
                    body.position = position + track.transform.up * TrackPiece.RailToTrainPivot - rotation * new Vector3(0f, 0f, railCenterOffset);
                    body.rotation = rotation;
                }
            }
        }

        private void Update()
        {
            if (!IsServer && leverPivot) leverPivot.localRotation = leverRest * Quaternion.Euler(0, 0, -throttle.Value * 35f);
            if (startButton) startButton.localScale = new Vector3(0.18f, buttonDown.Value ? 0.025f : 0.04f, 0.18f);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !track || !track.HasSockets) return;
            UpdateCabControls();
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

        private void UpdateCabControls()
        {
            if (!leverPivot || !startButton) return;
            if (Time.time >= nextPlayerScan)
            {
                players = FindObjectsByType<PlaygroundPlayer>(FindObjectsSortMode.None);
                nextPlayerScan = Time.time + 0.25f;
            }
            bool held = false;
            foreach (var player in players)
                if (player && player.IsSpawned && player.IsHoldingBody(leverBody)) { held = true; break; }
            if (leverHinge && held != leverHeld)
            {
                if (!held)
                {
                    var spring = leverHinge.spring;
                    spring.targetPosition = leverHinge.angle;
                    leverHinge.spring = spring;
                }
                leverHinge.useSpring = !held;
                leverHeld = held;
            }
            float angle = Mathf.DeltaAngle(0f, (Quaternion.Inverse(leverRest) * leverPivot.localRotation).eulerAngles.z);
            float input = Mathf.Clamp(-angle / 35f, -1f, 1f);
            if (Mathf.Abs(input) < 0.06f) input = 0f;
            if (Mathf.Abs(input - throttle.Value) > 0.01f || (input == 0f && throttle.Value != 0f)) throttle.Value = input;

            bool touching = false;
            int count = Physics.OverlapSphereNonAlloc(startButton.position, 0.12f, buttonContacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var player = buttonContacts[i].GetComponentInParent<PlaygroundPlayer>();
                if (player && player.IsSpawned && player.IsHandCollider(buttonContacts[i])) { touching = true; break; }
            }
            if (touching && !buttonDown.Value && Time.time >= nextButtonPress)
            {
                powered.Value = !powered.Value;
                nextButtonPress = Time.time + 0.35f;
            }
            if (buttonDown.Value != touching) buttonDown.Value = touching;
        }

        private void CreateCabControls()
        {
            if (!lever) { Debug.LogError("TrainDrive: de cabinehendel Regulator ontbreekt.", this); return; }
            leverParent = lever.parent;
            leverPosition = lever.localPosition;
            leverScale = lever.localScale;
            Vector3 pivotPosition = lever.position - transform.up * 0.1f;
            Vector3 buttonPosition = pivotPosition + transform.right * 0.2f + transform.up * 0.34f - transform.forward * 0.27f;
            Quaternion buttonRotation = transform.rotation;
            var door = leverParent.Find("FireboxDoor");
            var stove = leverParent.GetComponent<Renderer>();
            if (door && stove)
            {
                Vector3 axis = leverParent.right.normalized;
                Vector3 outward = Vector3.Dot(door.position - stove.bounds.center, axis) < 0f ? -axis : axis;
                Vector3 side = Vector3.ProjectOnPlane(lever.position - door.position, transform.up);
                side = Vector3.ProjectOnPlane(side, outward).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(transform.up, outward).normalized;
                pivotPosition = door.position + outward * 0.035f + transform.up * 0.1f + side * 0.48f;
                buttonPosition = door.position + outward * 0.04f + transform.up * 0.29f + side * 0.05f;
                buttonRotation = Quaternion.FromToRotation(Vector3.up, outward);
            }
            leverColliders = lever.GetComponentsInChildren<Collider>(true);
            leverColliderEnabled = new bool[leverColliders.Length];
            for (int i = 0; i < leverColliders.Length; i++)
            {
                leverColliderEnabled[i] = leverColliders[i].enabled;
                leverColliders[i].enabled = false;
            }

            leverPivot = new GameObject("Cabinehendel fysica").transform;
            leverPivot.SetParent(leverParent, false);
            leverPivot.position = pivotPosition;
            leverPivot.localRotation = leverRest;
            lever.SetParent(leverPivot, false);
            lever.localPosition = Vector3.zero;
            lever.localRotation = Quaternion.identity;
            lever.localScale = leverScale;
            var handle = leverPivot.gameObject.AddComponent<SphereCollider>();
            handle.center = new Vector3(-0.05f, 0.45f, 0f);
            handle.radius = 0.18f;
            leverBody = leverPivot.gameObject.AddComponent<Rigidbody>();
            leverBody.mass = 0.15f;
            leverBody.useGravity = false;
            leverBody.isKinematic = !IsServer;
            leverBody.angularDamping = 0.75f;
            leverBody.interpolation = RigidbodyInterpolation.Interpolate;
            if (IsServer)
            {
                leverHinge = leverPivot.gameObject.AddComponent<HingeJoint>();
                leverHinge.connectedBody = body;
                leverHinge.autoConfigureConnectedAnchor = false;
                leverHinge.connectedAnchor = body.transform.InverseTransformPoint(leverPivot.position);
                leverHinge.axis = Vector3.forward;
                leverHinge.useLimits = true;
                leverHinge.limits = new JointLimits { min = -35f, max = 35f };
                leverHinge.spring = new JointSpring { spring = 25f, damper = 5f, targetPosition = 0f };
                leverHinge.useSpring = true;
            }

            var button = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            button.name = "Startknop";
            startButton = button.transform;
            startButton.SetParent(leverParent, false);
            startButton.position = buttonPosition;
            startButton.rotation = buttonRotation;
            startButton.localScale = new Vector3(0.18f, 0.04f, 0.18f);
            if (lever.TryGetComponent<Renderer>(out var leverRenderer))
                button.GetComponent<Renderer>().sharedMaterial = leverRenderer.sharedMaterial;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                    if (material && material.name == "M_Loco_Red") { button.GetComponent<Renderer>().sharedMaterial = material; break; }
                if (button.GetComponent<Renderer>().sharedMaterial && button.GetComponent<Renderer>().sharedMaterial.name == "M_Loco_Red") break;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (startButton) Destroy(startButton.gameObject);
            if (lever && leverParent)
            {
                lever.SetParent(leverParent, false);
                lever.localPosition = leverPosition;
                lever.localRotation = leverRest;
                lever.localScale = leverScale;
            }
            if (leverColliders != null)
                for (int i = 0; i < leverColliders.Length; i++)
                    if (leverColliders[i]) leverColliders[i].enabled = leverColliderEnabled[i];
            if (leverPivot) Destroy(leverPivot.gameObject);
            players = System.Array.Empty<PlaygroundPlayer>();
            leverHeld = false;
            nextPlayerScan = 0f;
            previous.Clear();
        }
    }
}

