using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class PlaygroundPlayer : NetworkBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float reach = 1.8f;
        [SerializeField] private float jumpSpeed = 5f;

        private readonly NetworkVariable<Quaternion> leftPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> leftLegPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightLegPose = new(Quaternion.identity);
        private Rigidbody body;
        private RigidbodyConstraints originalConstraints;
        private Vector3 originalCenterOfMass;
        private ArmPhysics left;
        private ArmPhysics right;
        private PlaygroundLegRig legs;
        private Vector2 requestedMove;
        private float requestedYaw;
        private float requestedPitch;
        private bool leftHeld, rightHeld, jumpQueued;
        private bool lastLeft, lastRight;
        private float nextInput, nextPose, lastJump;
        private float lastServerInput;
        private float yaw, pitch = 18f;
        private Camera playerCamera;
        private readonly RaycastHit[] hits = new RaycastHit[32];
#if DEVELOPMENT_BUILD
        private bool automatedInput;
#endif

        public void Configure(Transform model, Transform armL, Transform armR)
        {
            visual = model;
            leftArm = armL;
            rightArm = armR;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            originalConstraints = body.constraints;
            originalCenterOfMass = body.centerOfMass;
        }

        public override void OnNetworkSpawn()
        {
            body.isKinematic = !IsServer;
            if (IsServer)
            {
                body.constraints = RigidbodyConstraints.None;
                body.centerOfMass = new Vector3(0, 0.55f, 0);
                body.maxAngularVelocity = 12f;
            }
            left = new ArmPhysics(leftArm, body, IsServer);
            right = new ArmPhysics(rightArm, body, IsServer);
            legs = PlaygroundLegRig.Create(visual, body, IsServer);
            lastServerInput = Time.unscaledTime;
            if (left.Collider && right.Collider) Physics.IgnoreCollision(left.Collider, right.Collider);
            if (IsServer)
            {
                leftPose.Value = left.LocalRotation;
                rightPose.Value = right.LocalRotation;
            }
            if (IsOwner)
            {
                playerCamera = Camera.main;
                yaw = transform.eulerAngles.y;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
#if DEVELOPMENT_BUILD
                if (!IsServer && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-playground-autotest") >= 0)
                {
                    automatedInput = true;
                    StartCoroutine(RemoteSmokeTest());
                }
#endif
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
#if DEVELOPMENT_BUILD
            if (automatedInput) return;
#endif
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                if (lastLeft || lastRight) SendInputServerRpc(Vector2.zero, yaw, pitch, false, false);
                lastLeft = lastRight = false;
                return;
            }
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            bool active = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            if (active)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -60f, 65f);
            }
            Vector2 move = active ? new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                               (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector2.zero;
            bool l = active && mouse.leftButton.isPressed;
            bool r = active && mouse.rightButton.isPressed;
            if (active && keyboard.spaceKey.wasPressedThisFrame) JumpServerRpc();
            if (Time.unscaledTime >= nextInput || l != lastLeft || r != lastRight)
            {
                SendInputServerRpc(Vector2.ClampMagnitude(move, 1f), yaw, pitch, l, r);
                nextInput = Time.unscaledTime + 0.05f;
                lastLeft = l;
                lastRight = r;
            }
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 move, float direction, float lookPitch, bool l, bool r)
        {
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) || !float.IsFinite(direction) || !float.IsFinite(lookPitch)) return;
            requestedMove = Vector2.ClampMagnitude(move, 1f);
            requestedYaw = Mathf.Repeat(direction, 360f);
            requestedPitch = Mathf.Clamp(lookPitch, -60f, 65f);
            lastServerInput = Time.unscaledTime;
            leftHeld = l;
            rightHeld = r;
            if (!l) left?.Release();
            if (!r) right?.Release();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || !IsSpawned || !IsOwner) return;
            lastLeft = lastRight = false;
            SendInputServerRpc(Vector2.zero, yaw, pitch, false, false);
        }

        [ServerRpc]
        private void JumpServerRpc() => jumpQueued = true;

#if DEVELOPMENT_BUILD
        private System.Collections.IEnumerator RemoteSmokeTest()
        {
            yield return new WaitForSeconds(1.5f);
            Vector3 start = transform.position;
            Transform testLeg = null;
            foreach (Transform candidate in visual.GetComponentsInChildren<Transform>())
                if (candidate.name == "Leg_L") { testLeg = candidate; break; }
            Quaternion initialLeg = testLeg ? testLeg.localRotation : Quaternion.identity;
            float legAngle = 0f;
            for (int i = 0; i < 25; i++)
            {
                SendInputServerRpc(Vector2.up, 0f, 0f, false, false);
                yield return new WaitForSeconds(0.05f);
                if (testLeg) legAngle = Mathf.Max(legAngle, Quaternion.Angle(initialLeg, testLeg.localRotation));
            }
            SendInputServerRpc(Vector2.zero, 0f, 0f, false, false);
            yield return new WaitForSeconds(0.3f);
            float moved = Vector3.Distance(start, transform.position);
            float baseY = transform.position.y;
            float peakY = baseY;
            JumpServerRpc();
            for (int i = 0; i < 30; i++)
            {
                yield return new WaitForSeconds(0.03f);
                peakY = Mathf.Max(peakY, transform.position.y);
            }
            Debug.Log($"PLAYGROUND_REMOTE_SMOKE moved={moved:F2} jump={peakY - baseY:F2} legAngle={legAngle:F1}");
        }
#endif

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer) return;
            if (Time.unscaledTime - lastServerInput > 0.35f)
            {
                requestedMove = Vector2.zero;
                leftHeld = rightHeld = false;
                left.Release();
                right.Release();
            }
            Quaternion facing = Quaternion.Euler(0f, requestedYaw, 0f);
            Vector3 target = facing * new Vector3(requestedMove.x, 0f, requestedMove.y) * speed;
            Vector3 current = body.linearVelocity;
            Vector3 change = Vector3.ClampMagnitude(new Vector3(target.x - current.x, 0f, target.z - current.z), acceleration * Time.fixedDeltaTime);
            body.AddForce(change, ForceMode.VelocityChange);

            // A damped upright motor yields to impacts instead of freezing body rotation.
            Quaternion error = facing * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                body.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * 35f) - body.angularVelocity * 9f, 65f), ForceMode.Acceleration);

            if (jumpQueued)
            {
                jumpQueued = false;
                if (Time.time - lastJump > 0.25f && IsGrounded())
                {
                    Vector3 velocity = body.linearVelocity;
                    velocity.y = jumpSpeed;
                    body.linearVelocity = velocity;
                    lastJump = Time.time;
                }
            }
            Quaternion aim = Quaternion.Euler(requestedPitch, requestedYaw, 0f);
            left.Step(leftHeld, requestedPitch, aim, hits, transform, reach);
            right.Step(rightHeld, requestedPitch, aim, hits, transform, reach);
            legs?.Step(body.linearVelocity, IsGrounded());
            if (Time.time >= nextPose)
            {
                leftPose.Value = left.LocalRotation;
                rightPose.Value = right.LocalRotation;
                if (legs != null)
                {
                    leftLegPose.Value = legs.LeftRotation;
                    rightLegPose.Value = legs.RightRotation;
                }
                nextPose = Time.time + 0.05f;
            }
        }

        private bool IsGrounded()
        {
            Vector3 origin = body.position + body.rotation * Vector3.up * 0.28f;
            int count = Physics.SphereCastNonAlloc(origin, 0.22f, Vector3.down, hits, 0.22f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform) && hits[i].normal.y > 0.55f) return true;
            return false;
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!IsServer)
            {
                left.ShowRemote(leftPose.Value);
                right.ShowRemote(rightPose.Value);
                legs?.ShowRemote(leftLegPose.Value, rightLegPose.Value);
            }
            if (!IsOwner || !playerCamera) return;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 focus = transform.position + Vector3.up * 0.9f;
            Vector3 direction = -(orbit * Vector3.forward);
            float distance = 4f;
            int count = Physics.SphereCastNonAlloc(focus, 0.16f, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0.3f, hits[i].distance - 0.15f));
            playerCamera.transform.SetPositionAndRotation(focus + direction * distance, orbit);
        }

        public override void OnNetworkDespawn()
        {
            left?.Dispose();
            right?.Dispose();
            legs?.Dispose();
            if (body && IsServer)
            {
                body.constraints = originalConstraints;
                body.centerOfMass = originalCenterOfMass;
            }
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner || ConductorChaos.UI.ChaosTrialHud.IsVisible) return;
            // Read-only fallback for scenes without the current UI canvas.
            GUI.Label(new Rect(16, 16, 1000, 24), "WASD lopen | spatie springen | muis kijken | linker/rechter muisknop: hand grijpen | Esc cursor");
        }

        // Arm physics exists only on the server; observers receive the shoulder pose.
        private sealed class ArmPhysics
        {
            private readonly Transform arm;
            private readonly Rigidbody parent;
            private readonly Rigidbody rigid;
            private readonly Quaternion rest;
            private readonly CapsuleCollider collider;
            private readonly ConfigurableJoint shoulder;
            private SpringJoint grip;
            private float nextAttempt;

            public Quaternion LocalRotation => arm ? arm.localRotation : Quaternion.identity;
            public Collider Collider => collider;

            public ArmPhysics(Transform transform, Rigidbody body, bool server)
            {
                arm = transform;
                parent = body;
                if (!arm) { Debug.LogError("Playground: missing conductor arm."); return; }
                rest = arm.localRotation;
                rigid = arm.gameObject.AddComponent<Rigidbody>();
                rigid.mass = 0.35f;
                rigid.isKinematic = !server;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                collider = arm.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                // Keep the shoulder clear of the torso; the forearm/hand remains
                // solid so it cannot sweep through the body during a collision.
                collider.center = new Vector3(0, -0.19f, 0);
                collider.height = 0.16f;
                collider.radius = 0.055f;
                if (!server) return;
                shoulder = arm.gameObject.AddComponent<ConfigurableJoint>();
                shoulder.connectedBody = body;
                shoulder.autoConfigureConnectedAnchor = false;
                shoulder.anchor = Vector3.zero;
                shoulder.connectedAnchor = body.transform.InverseTransformPoint(arm.position);
                shoulder.xMotion = shoulder.yMotion = shoulder.zMotion = ConfigurableJointMotion.Locked;
                shoulder.angularXMotion = shoulder.angularYMotion = shoulder.angularZMotion = ConfigurableJointMotion.Limited;
                shoulder.lowAngularXLimit = new SoftJointLimit { limit = -110f };
                shoulder.highAngularXLimit = new SoftJointLimit { limit = 75f };
                shoulder.angularYLimit = new SoftJointLimit { limit = 45f };
                shoulder.angularZLimit = new SoftJointLimit { limit = 45f };
                shoulder.enableCollision = true;
            }

            public void Step(bool held, float pitch, Quaternion aim, RaycastHit[] buffer, Transform player, float reach)
            {
                if (!rigid) return;
                Quaternion localTarget = rest * Quaternion.Euler(held ? -65f + pitch * 0.65f : 0f, 0, 0);
                Quaternion desired = arm.parent.rotation * localTarget;
                Quaternion error = desired * Quaternion.Inverse(rigid.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                    rigid.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * 32f) - rigid.angularVelocity * 5f, 55f), ForceMode.Acceleration);

                if (!held) Release();
                if (!held || grip || Time.time < nextAttempt) return;
                nextAttempt = Time.time + 0.1f;
                Vector3 hand = arm.TransformPoint(new Vector3(0, -0.205f, 0));
                int count = Physics.SphereCastNonAlloc(hand, 0.13f, aim * Vector3.forward, buffer, 0.38f, ~0, QueryTriggerInteraction.Ignore);
                float closest = float.PositiveInfinity;
                RaycastHit chosen = default;
                bool found = false;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = buffer[i];
                    if (hit.collider.transform.IsChildOf(player) || hit.distance >= closest ||
                        Vector3.Distance(parent.position, hit.point) > reach) continue;
                    if (hit.rigidbody)
                    {
                        NetworkObject target = hit.rigidbody.GetComponentInParent<NetworkObject>();
                        if (!target || !target.IsSpawned || !target.IsOwnedByServer) continue;
                    }
                    closest = hit.distance;
                    chosen = hit;
                    found = true;
                }
                if (!found) return;
                grip = arm.gameObject.AddComponent<SpringJoint>();
                grip.autoConfigureConnectedAnchor = false;
                grip.connectedBody = chosen.rigidbody;
                grip.connectedAnchor = chosen.rigidbody ? chosen.rigidbody.transform.InverseTransformPoint(chosen.point) : chosen.point;
                grip.anchor = new Vector3(0, -0.205f, 0);
                grip.spring = 180f;
                grip.damper = 18f;
                grip.minDistance = 0f;
                grip.maxDistance = 0.03f;
                grip.breakForce = 300f;
            }

            public void ShowRemote(Quaternion rotation)
            {
                if (arm) arm.localRotation = Quaternion.Slerp(arm.localRotation, rotation, 15f * Time.deltaTime);
            }

            public void Release()
            {
                if (!grip) return;
                Object.Destroy(grip);
                grip = null;
            }

            public void Dispose()
            {
                Release();
                if (shoulder) Object.Destroy(shoulder);
                if (collider) Object.Destroy(collider);
                if (rigid) Object.Destroy(rigid);
                if (arm) arm.localRotation = rest;
            }
        }
    }
}

