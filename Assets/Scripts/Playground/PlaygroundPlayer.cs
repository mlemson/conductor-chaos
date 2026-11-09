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
        internal bool IsHoldingBody(Rigidbody candidate) =>
            (left != null && left.IsHoldingBody(candidate)) || (right != null && right.IsHoldingBody(candidate));
        internal bool IsHandCollider(Collider candidate) =>
            (left != null && left.Collider == candidate) || (right != null && right.Collider == candidate);
        private PlaygroundLegRig legs;
        private Vector2 requestedMove;
        private float requestedYaw;
        private float requestedPitch;
        private float requestedHandFocus;
        private bool leftHeld, rightHeld, jumpQueued;
        private bool lastLeft, lastRight;
        private float nextInput, nextPose, lastJump;
        private float lastServerInput;
        private Vector3 trainVelocity;
        private TrainDrive airborneTrain;
        private float yaw, pitch = 18f;
        private Camera playerCamera;
        private float originalCameraFieldOfView;
        private float originalCameraNearClip;
        private Renderer[] cameraBodyRenderers;
        private bool[] cameraBodyRenderingOff;
        private bool cameraHidesBody;
        private float cameraDistance, cameraZoomVelocity;
        private float cameraHandFocus;
        private float desiredCameraDistance = PlaygroundControls.DefaultCameraDistance;
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
            cameraHandFocus = requestedHandFocus = 0f;
            trainVelocity = Vector3.zero;
            airborneTrain = null;
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
                if (playerCamera)
                {
                    originalCameraFieldOfView = playerCamera.fieldOfView;
                    originalCameraNearClip = playerCamera.nearClipPlane;
                    playerCamera.nearClipPlane = Mathf.Min(originalCameraNearClip, 0.08f);
                    cameraBodyRenderers = visual ? visual.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
                    cameraBodyRenderingOff = new bool[cameraBodyRenderers.Length];
                }
                cameraDistance = desiredCameraDistance = PlaygroundControls.DefaultCameraDistance;
                cameraZoomVelocity = 0f;
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
                desiredCameraDistance = PlaygroundControls.ZoomDistance(desiredCameraDistance, mouse.scroll.ReadValue().y);
            }
            Vector2 move = active ? new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                               (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector2.zero;
            bool l = active && mouse.leftButton.isPressed;
            bool r = active && mouse.rightButton.isPressed;
            if (active && keyboard.spaceKey.wasPressedThisFrame) JumpServerRpc();
            if (Time.unscaledTime >= nextInput || l != lastLeft || r != lastRight)
            {
                SendInputServerRpc(Vector2.ClampMagnitude(move, 1f), yaw, pitch, l, r, cameraHandFocus);
                nextInput = Time.unscaledTime + 0.05f;
                lastLeft = l;
                lastRight = r;
            }
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 move, float direction, float lookPitch, bool l, bool r, float handFocus = 0f)
        {
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) || !float.IsFinite(direction) || !float.IsFinite(lookPitch) ||
                !float.IsFinite(handFocus)) return;
            requestedMove = Vector2.ClampMagnitude(move, 1f);
            requestedYaw = Mathf.Repeat(direction, 360f);
            requestedPitch = Mathf.Clamp(lookPitch, -60f, 65f);
            requestedHandFocus = Mathf.Clamp01(handFocus);
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
                requestedHandFocus = 0f;
                leftHeld = rightHeld = false;
                left.Release();
                right.Release();
            }
            Quaternion facing = Quaternion.Euler(0f, requestedYaw, 0f);
            Vector3 current = body.linearVelocity;
            bool grounded = IsGrounded(out var ground);
            var support = grounded && ground.rigidbody && ground.rigidbody.isKinematic
                ? ground.rigidbody.GetComponent<TrainDrive>() : null;
            if (grounded)
            {
                airborneTrain = support;
                trainVelocity = support ? support.GuidedPointVelocity(body.position) : Vector3.zero;
            }
            else if (airborneTrain)
            {
                if (IsGrounded(out var below, 1.6f) && below.rigidbody &&
                    below.rigidbody.GetComponent<TrainDrive>() == airborneTrain)
                {
                    Vector3 correction = Vector3.ClampMagnitude(airborneTrain.GuidedPointVelocity(body.position) - trainVelocity,
                        20f * Time.fixedDeltaTime);
                    body.AddForce(correction, ForceMode.VelocityChange);
                    current += correction;
                    trainVelocity += correction;
                }
                else airborneTrain = null;
            }
            Vector3 target = trainVelocity + facing * new Vector3(requestedMove.x, 0f, requestedMove.y) * speed;
            bool jumping = jumpQueued && grounded && Time.time - lastJump > 0.25f;
            Vector3 change = Vector3.ClampMagnitude(new Vector3(target.x - current.x, 0f, target.z - current.z), acceleration * Time.fixedDeltaTime);
            if (!jumping || !support) body.AddForce(change, ForceMode.VelocityChange);

            // A damped upright motor yields to impacts instead of freezing body rotation.
            Quaternion error = facing * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                body.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * 35f) - body.angularVelocity * 9f, 65f), ForceMode.Acceleration);

            if (jumpQueued)
            {
                jumpQueued = false;
                if (jumping)
                {
                    Vector3 velocity = support ? target : body.linearVelocity;
                    velocity.y = jumpSpeed + (support ? trainVelocity.y : 0f);
                    body.linearVelocity = velocity;
                    lastJump = Time.time;
                }
            }
            Quaternion aim = Quaternion.Euler(requestedPitch, requestedYaw, 0f);
            left.Step(leftHeld, requestedPitch, aim, hits, transform, reach, requestedHandFocus);
            right.Step(rightHeld, requestedPitch, aim, hits, transform, reach, requestedHandFocus);
            float handPush = 0f;
            if (Time.time - lastJump > 0.18f)
            {
                float press = Mathf.InverseLerp(28f, 60f, requestedPitch);
                float upSpeed = body.linearVelocity.y;
                handPush = Mathf.Max(left.PushUp(press, upSpeed), right.PushUp(press, upSpeed));
            }
            if (handPush > 0f) body.AddForce(Vector3.up * handPush, ForceMode.Acceleration);
            if (!grounded && handPush <= 0f && requestedMove.y <= 0.1f && left.HasGrip != right.HasGrip)
                (left.HasGrip ? left : right).SlideDown();
            legs?.Step(body.linearVelocity, handPush <= 0f && grounded);
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

        private bool IsGrounded(out RaycastHit ground, float distance = 0.22f)
        {
            ground = default;
            Vector3 origin = body.position + body.rotation * Vector3.up * 0.28f;
            int count = Physics.SphereCastNonAlloc(origin, 0.22f, Vector3.down, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform) && hits[i].normal.y > 0.55f &&
                    (!ground.collider || hits[i].distance < ground.distance)) ground = hits[i];
            return ground.collider;
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
            cameraDistance = PlaygroundControls.EaseCameraDistance(cameraDistance, desiredCameraDistance, ref cameraZoomVelocity);
            float eyeBlend = PlaygroundControls.FirstPersonBlend(cameraDistance);
            focus += PlaygroundControls.FirstPersonEyeOffset(yaw) * eyeBlend;
            float distance = cameraDistance;
            int count = Physics.SphereCastNonAlloc(focus, 0.16f, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.15f));
            if (distance < cameraDistance)
            {
                cameraDistance = distance;
                cameraZoomVelocity = 0f;
            }
            cameraHandFocus = Mathf.MoveTowards(cameraHandFocus,
                PlaygroundControls.FirstPersonBlend(distance), 6f * Time.unscaledDeltaTime);
            SetCameraBodyHidden(distance < PlaygroundControls.FirstPersonDistance);
            float targetFov = desiredCameraDistance >= 1.5f && distance < 1.5f
                ? PlaygroundControls.ZoomFieldOfView(originalCameraFieldOfView, desiredCameraDistance)
                : originalCameraFieldOfView;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            playerCamera.transform.SetPositionAndRotation(focus + direction * distance, orbit);
        }

        private void SetCameraBodyHidden(bool hidden)
        {
            if (cameraBodyRenderers == null || cameraHidesBody == hidden) return;
            cameraHidesBody = hidden;
            for (int i = 0; i < cameraBodyRenderers.Length; i++)
            {
                Renderer renderer = cameraBodyRenderers[i];
                if (!renderer) continue;
                if (hidden) cameraBodyRenderingOff[i] = renderer.forceRenderingOff;
                bool isArm = (leftArm && renderer.transform.IsChildOf(leftArm)) || (rightArm && renderer.transform.IsChildOf(rightArm));
                renderer.forceRenderingOff = (hidden && !isArm) || cameraBodyRenderingOff[i];
            }
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
            if (IsOwner)
            {
                SetCameraBodyHidden(false);
                if (playerCamera)
                {
                    playerCamera.fieldOfView = originalCameraFieldOfView;
                    playerCamera.nearClipPlane = originalCameraNearClip;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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
            private Joint grip;
            private Collider gripSurface;
            private Vector3 gripNormal;
            private readonly Collider[] leverOverlaps = new Collider[12];
            private float nextAttempt;
            private float poseHandFocus;

            public Quaternion LocalRotation => arm ? arm.localRotation : Quaternion.identity;
            public Collider Collider => collider;
            public bool HasGrip => grip;
            public bool IsHoldingBody(Rigidbody candidate) => grip && grip.connectedBody == candidate;
            public float PushUp(float press, float upSpeed) =>
                PlaygroundControls.HandPushAcceleration(grip, gripSurface, press, upSpeed);
            public void SlideDown() => PlaygroundControls.SlideWallGrip(grip, gripSurface, ref gripNormal);

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

            public void Step(bool held, float pitch, Quaternion aim, RaycastHit[] buffer, Transform player, float reach, float handFocus)
            {
                if (!rigid) return;
                Quaternion localTarget = rest * Quaternion.Euler(held ? -65f + pitch * 0.65f : 0f, 0, 0);
                Vector3 restDirection = rest * Vector3.down;
                float handSide = Mathf.Sign(restDirection.x);
                if (!held || !grip) poseHandFocus = handFocus;
                if (poseHandFocus > 0f)
                {
                    Vector3 closeDirection = arm.parent.InverseTransformDirection(aim * Vector3.forward);
                    closeDirection -= Vector3.right * (handSide * 0.35f);
                    Quaternion closeTarget = Quaternion.FromToRotation(restDirection, closeDirection.normalized) * rest;
                    localTarget = Quaternion.Slerp(localTarget, closeTarget, poseHandFocus * (held ? 1f : 0.8f));
                }
                if (held && grip && gripSurface && grip is ConfigurableJoint)
                {
                    var support = grip.connectedBody;
                    Vector3 anchor = support ? support.transform.TransformPoint(grip.connectedAnchor) : grip.connectedAnchor;
                    Vector3 towardGrip = arm.parent.InverseTransformDirection(anchor - rigid.position);
                    if (towardGrip.sqrMagnitude > 0.001f)
                        localTarget = Quaternion.FromToRotation(restDirection, towardGrip.normalized) * rest;
                }
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
                if (TryGripNearbyLever(hand, reach)) return;
                Vector3 direction = PlaygroundControls.HandGripAim(aim, handSide, handFocus) * Vector3.forward;
                if (TryGripCast(hand, buffer, player, reach,
                    Physics.SphereCastNonAlloc(hand, 0.13f, direction, buffer, 0.38f, ~0, QueryTriggerInteraction.Ignore))) return;
                // A sphere cast can miss a wall the palm is already touching.
                TryGripCast(hand, buffer, player, reach,
                    Physics.RaycastNonAlloc(hand, direction, buffer, 0.17f, ~0, QueryTriggerInteraction.Ignore));
            }

            private bool TryGripCast(Vector3 hand, RaycastHit[] buffer, Transform player, float reach, int count)
            {
                float closest = float.PositiveInfinity;
                RaycastHit chosen = default;
                bool found = false;
                bool leverGrip = false;
                bool plantedGrip = false;
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
                    var train = hit.rigidbody ? hit.rigidbody.GetComponentInParent<TrainDrive>() : null;
                    bool lever = train && train.IsSpawned && train.IsOwnedByServer && train.IsLeverCollider(hit.collider);
                    bool planted = !lever && (!hit.rigidbody || hit.rigidbody.isKinematic);
                    if (planted && (hit.distance <= 0f || !PlaygroundControls.CanPlantHand(hand, hit.point, hit.normal))) continue;
                    closest = hit.distance;
                    chosen = hit;
                    found = true;
                    leverGrip = lever;
                    plantedGrip = planted;
                }
                if (!found) return false;
                gripSurface = leverGrip ? null : chosen.collider;
                gripNormal = leverGrip ? Vector3.zero : chosen.collider.transform.InverseTransformDirection(chosen.normal);
                Vector3 handAnchor = new Vector3(0, -0.205f, 0);
                if (plantedGrip)
                {
                    grip = PlaygroundControls.PlantHand(arm.gameObject, chosen.rigidbody, handAnchor, chosen.point, chosen.normal, 1100f);
                    return true;
                }

                var spring = arm.gameObject.AddComponent<SpringJoint>();
                spring.autoConfigureConnectedAnchor = false;
                spring.connectedBody = chosen.rigidbody;
                spring.connectedAnchor = chosen.rigidbody ? chosen.rigidbody.transform.InverseTransformPoint(chosen.point) : chosen.point;
                spring.anchor = handAnchor;
                spring.spring = leverGrip ? 450f : 180f;
                spring.damper = leverGrip ? 40f : 18f;
                spring.minDistance = 0f;
                spring.maxDistance = 0.03f;
                spring.breakForce = leverGrip ? 1000f : 300f;
                grip = spring;
                return true;
            }

            private bool TryGripNearbyLever(Vector3 hand, float reach)
            {
                int count = Physics.OverlapSphereNonAlloc(hand, 0.09f, leverOverlaps, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider surface = leverOverlaps[i];
                    Rigidbody target = surface.attachedRigidbody;
                    var train = target ? target.GetComponentInParent<TrainDrive>() : null;
                    if (!train || !train.IsSpawned || !train.IsOwnedByServer || !train.IsLeverCollider(surface)) continue;
                    Vector3 point = surface.ClosestPoint(hand);
                    if ((point - parent.position).sqrMagnitude > reach * reach) continue;
                    var spring = arm.gameObject.AddComponent<SpringJoint>();
                    spring.autoConfigureConnectedAnchor = false;
                    spring.connectedBody = target;
                    spring.connectedAnchor = target.transform.InverseTransformPoint(point);
                    spring.anchor = new Vector3(0, -0.205f, 0);
                    spring.spring = 450f;
                    spring.damper = 40f;
                    spring.minDistance = 0f;
                    spring.maxDistance = 0.03f;
                    spring.breakForce = 1000f;
                    grip = spring;
                    return true;
                }
                return false;
            }

            public void ShowRemote(Quaternion rotation)
            {
                if (arm) arm.localRotation = Quaternion.Slerp(arm.localRotation, rotation, 15f * Time.deltaTime);
            }

            public void Release()
            {
                gripSurface = null;
                PlaygroundControls.ReleaseGrip(grip);
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

