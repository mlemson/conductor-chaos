using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using ConductorChaos.Track;

namespace ConductorChaos.Playground
{
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public sealed class PlaygroundPlayer : NetworkBehaviour
    {
        [SerializeField] private Transform visual;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float sprintSpeed = 4.7f;
        [SerializeField] private float acceleration = 18f;
        [SerializeField] private float reach = 1.8f;
        [SerializeField] private float jumpSpeed = 5f;
        [SerializeField] private float balanceLean = 7f;
        [Header("Climb Assist")]
        [SerializeField, Min(0f)] private float climbAcceleration = 28f;
        [SerializeField, Min(0f)] private float climbUpSpeed = 2f;
        [SerializeField, Range(1, 2)] private int climbHandsRequired = 1;

        private readonly NetworkVariable<Quaternion> leftPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightPose = new(Quaternion.identity);
        private readonly NetworkVariable<Vector2> armStretch = new(Vector2.one);
        private readonly NetworkVariable<Quaternion> leftLegPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightLegPose = new(Quaternion.identity);
        private Rigidbody body;
        private PlaygroundHost sessionHost;
        private RigidbodyConstraints originalConstraints;
        private Vector3 originalCenterOfMass;
        private CapsuleCollider bodyCapsule;
        private float originalBodyRadius;
        private ArmPhysics left;
        private ArmPhysics right;
        internal bool IsHandCollider(Collider candidate) =>
            (left != null && left.Collider == candidate) || (right != null && right.Collider == candidate);
        internal bool TryGetControlHand(Rigidbody candidate, out Transform hand, out Vector3 position, bool useAim = false)
        {
            var holding = leftHeld && left != null && left.IsHoldingBody(candidate) ? left
                : rightHeld && right != null && right.IsHoldingBody(candidate) ? right : null;
            hand = holding != null ? holding.Transform : null;
            position = holding != null ? (useAim ? holding.AimedHandPosition : holding.HandPosition) : Vector3.zero;
            return holding != null;
        }
        internal bool HasCabControlInput => Time.unscaledTime <= lastLeverInputAt;
        internal Vector3 CabAimForward => Quaternion.Euler(requestedPitch, requestedYaw, 0f) * Vector3.forward;
        private PlaygroundLegRig legs;
        private Vector2 requestedMove;
        private float requestedYaw;
        private float requestedPitch;
        private bool leftHeld, rightHeld, jumpQueued, sprinting, crouching;
        private bool lastLeft, lastRight;
        private float nextInput, nextPose, lastJump;
        private float nextSprintHop;
        private float lastServerInput;
        private float lastLeverInputAt;
        private Vector3 smoothedLean;
        private float yaw, pitch = 18f;
        private Camera playerCamera;
        private float originalCameraFieldOfView;
        private float originalCameraNearClip;
        private Renderer[] cameraBodyRenderers;
        private bool[] cameraBodyRenderingOff;
        private bool cameraHidesBody;
        private float cameraDistance = 4f;
        private float cameraZoomVelocity;
        private float desiredCameraDistance = PlaygroundControls.DefaultCameraDistance;
        private float smoothFocusY, smoothFocusVelocity;
        private bool hasSmoothFocus;
        private Vector3 lastClearCameraPosition;
        private bool hasClearCameraPosition;
        private TrainDrive trainDrive;
        private bool usingCabinCamera;
        private bool usingLookDownCamera;
        private Vector3 wagonPushDirection;
        private Vector3 airborneSupportVelocity;
        private Vector3 cameraEyeOffset;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] ledgeObstacles = new Collider[64];
        private readonly Collider[] cameraFocusObstacles = new Collider[16];
        private Vector3 ledgeLip, ledgeInward;
        private float nextLedgeProbe;
        private bool hasLedge, mantling;
        private bool clearingLimbs;
        private float stalledTime, nextRecoveryProbe, nextTrackStep, restoreLimbsAt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            bodyCapsule = GetComponent<CapsuleCollider>();
            if (bodyCapsule) originalBodyRadius = bodyCapsule.radius;
        }

        public override void OnNetworkSpawn()
        {
            sessionHost = NetworkManager.GetComponent<PlaygroundHost>();
            cameraDistance = desiredCameraDistance = PlaygroundControls.DefaultCameraDistance;
            cameraZoomVelocity = 0f;
            cameraEyeOffset = Vector3.zero;
            hasClearCameraPosition = false;
            usingCabinCamera = false;
            usingLookDownCamera = false;
            wagonPushDirection = Vector3.zero;
            clearingLimbs = false;
            stalledTime = nextRecoveryProbe = nextTrackStep = 0f;
            body.isKinematic = !IsServer;
            // Leave clearance for the short arms at the shoulders while keeping
            // a solid torso core for collisions and hand interaction.
            if (bodyCapsule) bodyCapsule.radius = Mathf.Min(originalBodyRadius, 0.20f);
            if (IsServer)
            {
                body.constraints = RigidbodyConstraints.None;
                body.centerOfMass = new Vector3(0, 0.35f, 0);
                body.maxAngularVelocity = 12f;
            }
            left = new ArmPhysics(leftArm, body, IsServer);
            right = new ArmPhysics(rightArm, body, IsServer);
            legs = PlaygroundLegRig.Create(visual, body, IsServer);
            lastServerInput = Time.unscaledTime;
            lastLeverInputAt = -1f;
            if (left.Collider && right.Collider) Physics.IgnoreCollision(left.Collider, right.Collider);
            if (IsServer)
            {
                leftPose.Value = left.LocalRotation;
                rightPose.Value = right.LocalRotation;
                armStretch.Value = Vector2.one;
            }
            if (IsOwner)
            {
                playerCamera = Camera.main;
                if (playerCamera)
                {
                    originalCameraNearClip = playerCamera.nearClipPlane;
                    originalCameraFieldOfView = playerCamera.fieldOfView;
                    playerCamera.nearClipPlane = Mathf.Min(originalCameraNearClip, 0.08f);
                    cameraBodyRenderers = visual ? visual.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
                    cameraBodyRenderingOff = new bool[cameraBodyRenderers.Length];
                }
                yaw = transform.eulerAngles.y;
                if (!PlaygroundMenu.BlocksInput)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (automatedInput) return;
#endif
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (PlaygroundMenu.BlocksInput)
            {
                if (lastLeft || lastRight || Time.unscaledTime >= nextInput)
                {
                    SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false, false);
                    nextInput = Time.unscaledTime + 0.05f;
                    lastLeft = lastRight = false;
                }
                return;
            }
            if (keyboard == null || mouse == null)
            {
                if (lastLeft || lastRight) SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false, false);
                lastLeft = lastRight = false;
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                if (lastLeft || lastRight) SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false, false);
                lastLeft = lastRight = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            bool active = Cursor.lockState == CursorLockMode.Locked && Application.isFocused;
            if (active)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -80f, 65f);
                desiredCameraDistance = PlaygroundControls.ZoomDistance(desiredCameraDistance, mouse.scroll.ReadValue().y);
            }
            Vector2 move = active ? new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                               (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector2.zero;
            bool l = active && mouse.leftButton.isPressed;
            bool r = active && mouse.rightButton.isPressed;
            if (active && keyboard.spaceKey.wasPressedThisFrame) JumpServerRpc();
            if (Time.unscaledTime >= nextInput || l != lastLeft || r != lastRight)
            {
                SendInputServerRpc(Vector2.ClampMagnitude(move, 1f), yaw, pitch, l, r, active && keyboard.leftShiftKey.isPressed,
                    active && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed));
                nextInput = Time.unscaledTime + 0.05f;
                lastLeft = l;
                lastRight = r;
            }
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 move, float direction, float lookPitch, bool l, bool r, bool sprint, bool crouch)
        {
            if (!l) { leftHeld = false; left?.Release(); }
            if (!r) { rightHeld = false; right?.Release(); }
            if (sessionHost && sessionHost.Phase != PlaygroundHost.SessionPhase.Playing) return;
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) || !float.IsFinite(direction) || !float.IsFinite(lookPitch)) return;
            if ((l || r) && (move.sqrMagnitude > 0.01f || Mathf.Abs(Mathf.DeltaAngle(requestedYaw, direction)) > 0.02f || Mathf.Abs(requestedPitch - lookPitch) > 0.02f))
                lastLeverInputAt = Time.unscaledTime + 0.12f;
            requestedMove = Vector2.ClampMagnitude(move, 1f);
            requestedYaw = Mathf.Repeat(direction, 360f);
            requestedPitch = Mathf.Clamp(lookPitch, -80f, 65f);
            lastServerInput = Time.unscaledTime;
            leftHeld = l;
            rightHeld = r;
            crouching = crouch;
            sprinting = sprint && !crouch;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || !IsSpawned || !IsOwner) return;
            lastLeft = lastRight = false;
            SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false, false);
        }

        [ServerRpc]
        private void JumpServerRpc()
        {
            if (!sessionHost || sessionHost.Phase == PlaygroundHost.SessionPhase.Playing) jumpQueued = true;
        }

        internal void RequestUnstuck()
        {
            if (IsSpawned && IsOwner) UnstuckServerRpc();
        }

        [ServerRpc]
        private void UnstuckServerRpc()
        {
            if (sessionHost && sessionHost.Phase != PlaygroundHost.SessionPhase.Playing) return;
            if (!trainDrive) trainDrive = FindAnyObjectByType<TrainDrive>();
            if (!trainDrive || !bodyCapsule || !trainDrive.TryFindRecoveryPoint(out Vector3 floor, out Vector3 velocity)) return;
            float radius = bodyCapsule.radius;
            Vector3 lower = floor + Vector3.up * (radius + 0.08f);
            Vector3 upper = lower + Vector3.up * Mathf.Max(0f, bodyCapsule.height - 2f * radius);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return;
            left.Release(); right.Release(); legs?.ReleaseSupport();
            Quaternion rotation = Quaternion.Euler(0f, requestedYaw, 0f);
            Vector3 destination = lower - rotation * (bodyCapsule.center - Vector3.up * (bodyCapsule.height * 0.5f - radius));
            Quaternion turn = rotation * Quaternion.Inverse(body.rotation);
            Rigidbody[] parts = GetComponentsInChildren<Rigidbody>();
            Vector3[] positions = new Vector3[parts.Length];
            Quaternion[] rotations = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++) { positions[i] = destination + turn * (parts[i].position - body.position); rotations[i] = turn * parts[i].rotation; }
            GetComponent<NetworkTransform>().Teleport(destination, rotation, transform.localScale);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].position = positions[i];
                parts[i].rotation = rotations[i];
                if (!parts[i].isKinematic) { parts[i].linearVelocity = velocity; parts[i].angularVelocity = Vector3.zero; }
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
                SendInputServerRpc(Vector2.up, 0f, 0f, false, false, false, false);
                yield return new WaitForSeconds(0.05f);
                if (testLeg) legAngle = Mathf.Max(legAngle, Quaternion.Angle(initialLeg, testLeg.localRotation));
            }
            SendInputServerRpc(Vector2.zero, 0f, 0f, false, false, false, false);
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
                sprinting = crouching = false;
                left.Release();
                right.Release();
            }
            Quaternion facing = Quaternion.Euler(0f, requestedYaw, 0f);
            float uprightControl = Mathf.InverseLerp(0.15f, 0.75f, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
            Vector3 current = body.linearVelocity;
            bool grounded = IsGrounded(out RaycastHit groundHit);
            Quaternion aim = Quaternion.Euler(requestedPitch, requestedYaw, 0f);
            bool pulling = requestedMove.y > 0.1f;
            bool firmLedgeGrip = hasLedge && pulling;
            Vector3 moveDirection = facing * new Vector3(requestedMove.x, 0f, requestedMove.y);
            bool stretchArms = (leftHeld || rightHeld) && !left.HoldingShovel && !right.HoldingShovel;
            if (!trainDrive) trainDrive = FindAnyObjectByType<TrainDrive>();
            bool cabinView = trainDrive && trainDrive.TryGetCabin(transform.position, -0.06f, out _);
            left.Step(leftHeld, stretchArms, aim, hits, transform, reach, pulling, firmLedgeGrip, moveDirection, cabinView);
            right.Step(rightHeld, stretchArms, aim, hits, transform, reach, pulling, firmLedgeGrip, moveDirection, cabinView);
            bool climbing = ApplyClimbAssist(pulling && !jumpQueued && (leftHeld || rightHeld));
            float handPush = 0f;
            if (!climbing && !jumpQueued && Time.time - lastJump > 0.18f)
            {
                float press = Mathf.InverseLerp(28f, 60f, requestedPitch);
                handPush = Mathf.Max(left.PushUp(press, current.y), right.PushUp(press, current.y));
            }
            bool bracing = handPush > 0f;
            if (!grounded && !climbing && !bracing && !pulling && !jumpQueued && left.HasGrip != right.HasGrip)
                (left.HasGrip ? left : right).SlideDown();
            int hangingHands = StaticGripCenter(out Vector3 gripCenter);
            bool hanging = !climbing && hangingHands > 0;
            wagonPushDirection = grounded && !hanging && !climbing && !jumpQueued ? moveDirection * uprightControl : Vector3.zero;
            Vector3 recoveryGroundVelocity = grounded && groundHit.rigidbody && groundHit.rigidbody.isKinematic
                ? groundHit.rigidbody.GetPointVelocity(groundHit.point) : Vector3.zero;
            RecoverFromPinch(moveDirection, recoveryGroundVelocity);
            if ((grounded || stalledTime >= 0.25f) && !jumpQueued && !left.HasGrip && !right.HasGrip)
                StepOverTrack(moveDirection);
            float walkSpeed = Mathf.Min(sprinting ? sprintSpeed : speed, sprinting ? 3.8f : 1.5f);
            bool stepped = false;
            if (climbing) legs?.BraceForClimb(mantling);
            if (bracing) legs?.ReleaseSupport();
            bool supported = !climbing && !bracing && legs != null && legs.Step(current, requestedMove, grounded && current.y - legs.SupportVelocity.y < 0.5f, walkSpeed, sprinting && !hanging, out stepped);
            Vector3 groundVelocity = supported ? legs.SupportVelocity : grounded && groundHit.rigidbody && groundHit.rigidbody.isKinematic
                ? groundHit.rigidbody.GetPointVelocity(groundHit.point) : Vector3.zero;
            if (grounded || supported) airborneSupportVelocity = groundVelocity;
            else groundVelocity = airborneSupportVelocity;
            Vector3 target = groundVelocity + moveDirection * walkSpeed * uprightControl;
            // Keep some steering when the torso touches uneven ground but neither foot probe finds support.
            float movementAcceleration = grounded ? acceleration * (supported ? (sprinting ? 1.35f : 1f) : 0.45f)
                : acceleration * (sprinting ? 0.45f : 0.2f);
            Vector3 change = Vector3.ClampMagnitude(new Vector3(target.x - current.x, 0f, target.z - current.z), movementAcceleration * Time.fixedDeltaTime);
            if (climbing || hanging) change = Vector3.zero;
            else body.AddForce(change, ForceMode.VelocityChange);
            if (hanging) MoveWhileGripping(gripCenter, moveDirection, hangingHands, pulling && !jumpQueued && !bracing);
            if (bracing) body.AddForce(Vector3.up * handPush, ForceMode.Acceleration);

            // A small push on alternating planted steps makes running a hop, not a repeated full jump.
            Vector3 relativeVelocity = current - groundVelocity;
            if (sprinting && supported && grounded && !jumpQueued && !climbing && !hanging && uprightControl > 0.8f &&
                requestedMove.sqrMagnitude > 0.01f && new Vector2(relativeVelocity.x, relativeVelocity.z).sqrMagnitude > 0.0225f &&
                (stepped || Time.time >= nextSprintHop + 0.12f) &&
                Time.time >= nextSprintHop && Time.time - lastJump > 0.3f)
            {
                legs.BeginSprintHop();
                body.AddForce(Vector3.up * Mathf.Clamp(2.05f - relativeVelocity.y, 0f, 2.45f), ForceMode.VelocityChange);
                nextSprintHop = Time.time + 0.32f;
            }

            // A damped upright motor yields to impacts instead of freezing body rotation.
            Vector3 localChange = Quaternion.Inverse(facing) * (change / Mathf.Max(Time.fixedDeltaTime, 0.001f));
            Vector3 desiredLean = new Vector3(-localChange.z, 0f, localChange.x);
            desiredLean = Vector3.ClampMagnitude(desiredLean, acceleration) / acceleration * balanceLean;
            smoothedLean = Vector3.Lerp(smoothedLean, desiredLean, 5f * Time.fixedDeltaTime);
            Quaternion targetBalance = climbing
                ? Quaternion.LookRotation(ledgeInward) * Quaternion.Euler(mantling ? 78f : 12f, 0f, 0f)
                : facing * Quaternion.Euler(smoothedLean.x + (crouching ? 50f : 0f), 0f, smoothedLean.z);
            if (hanging)
            {
                float lean = hangingHands == 1 ? 30f : 16f;
                targetBalance = facing * Quaternion.Euler(requestedMove.y * lean, 0f, -requestedMove.x * lean * 0.6f);
            }
            Quaternion error = targetBalance * Quaternion.Inverse(body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                body.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * (hanging ? 38f : 100f))
                    - body.angularVelocity * (hanging ? 7f : 18f), hanging ? 65f : 140f), ForceMode.Acceleration);

            if (jumpQueued)
            {
                jumpQueued = false;
                bool hasGrip = left.HasGrip || right.HasGrip;
                if (Time.time - lastJump > 0.25f && (grounded || (hasGrip && legs != null && legs.HasFootContact())))
                {
                    legs?.ReleaseSupport();
                    Vector3 velocity = body.linearVelocity;
                    velocity.y = jumpSpeed * (hanging ? 0.8f : 1f);
                    body.linearVelocity = velocity;
                    lastJump = Time.time;
                }
            }
            if (Time.time >= nextPose)
            {
                leftPose.Value = left.LocalRotation;
                rightPose.Value = right.LocalRotation;
                armStretch.Value = new Vector2(left.Stretch, right.Stretch);
                if (legs != null)
                {
                    leftLegPose.Value = legs.LeftRotation;
                    rightLegPose.Value = legs.RightRotation;
                }
                nextPose = Time.time + 0.05f;
            }
        }

        private int StaticGripCenter(out Vector3 center)
        {
            bool leftGrip = left.TryStaticAnchor(out Vector3 leftAnchor);
            bool rightGrip = right.TryStaticAnchor(out Vector3 rightAnchor);
            center = leftGrip && rightGrip ? (leftAnchor + rightAnchor) * 0.5f : leftGrip ? leftAnchor : rightAnchor;
            return (leftGrip ? 1 : 0) + (rightGrip ? 1 : 0);
        }

        private void MoveWhileGripping(Vector3 anchor, Vector3 move, int hands, bool pulling)
        {
            if (move.sqrMagnitude < 0.01f) return;
            Vector3 toGrip = anchor - body.worldCenterOfMass;
            Vector3 tether = toGrip.normalized;
            Vector3 velocity = body.linearVelocity;
            // Steer along the swing, leaving a little give for leaning away from the wall.
            Vector3 tangent = Vector3.ProjectOnPlane(move, tether);
            Vector3 push = tangent * (hands == 1 ? 8f : 5f) + move * 2f;
            if (tangent.sqrMagnitude > 0.01f)
                push -= Vector3.Project(velocity, tangent) * 3f;

            // A plain grip can pull the torso to hand height; crossing a lip still needs a validated ledge.
            float rise = toGrip.y + 0.10f;
            if (pulling && rise > 0.02f && velocity.y < climbUpSpeed && Time.time - lastJump > 0.18f)
            {
                float targetUp = Mathf.Min(rise * 5f, climbUpSpeed * 0.75f);
                float lift = Mathf.Max(0f, (targetUp - velocity.y) * 9f - Physics.gravity.y * 0.8f);
                push.y = Mathf.Max(0f, push.y) + Mathf.Min(lift, climbAcceleration * (hands == 1 ? 0.64f : 0.82f));
            }

            body.AddForce(Vector3.ClampMagnitude(push, climbAcceleration), ForceMode.Acceleration);
        }

        private void RecoverFromPinch(Vector3 move, Vector3 groundVelocity)
        {
            if (clearingLimbs && Time.time >= restoreLimbsAt) SetLimbClearance(false);
            Vector3 velocity = body.linearVelocity - groundVelocity;
            if (!bodyCapsule || left.HasGrip || right.HasGrip || move.sqrMagnitude < 0.04f ||
                jumpQueued || Mathf.Abs(velocity.y) > 1f || Vector3.Dot(velocity, move.normalized) >= 0.18f)
            {
                stalledTime = 0f;
                if (left.HasGrip || right.HasGrip) SetLimbClearance(false);
                return;
            }

            stalledTime += Time.fixedDeltaTime;
            if (stalledTime < 0.25f || Time.time < nextRecoveryProbe || clearingLimbs) return;
            nextRecoveryProbe = Time.time + 0.12f;

            Vector3 direction = move.normalized;
            int count = Physics.OverlapSphereNonAlloc(body.position, bodyCapsule.radius + 0.30f,
                ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            bool nearTrack = false;
            for (int i = 0; i < count && !nearTrack; i++)
                nearTrack = ledgeObstacles[i].GetComponentInParent<TrackPiece>();

            // Limb contact can yield when backing away from a wall or straddling the track.
            bool backingAway = LedgeRay(body.worldCenterOfMass, -direction, bodyCapsule.radius + 0.18f, out var wall) &&
                Mathf.Abs(wall.normal.y) < 0.60f && Vector3.Dot(direction, wall.normal) > 0.3f;
            if (!nearTrack && !backingAway) return;
            SetLimbClearance(true);
            restoreLimbsAt = Time.time + 0.22f;
            nextRecoveryProbe = Time.time + 0.42f;
        }

        private void StepOverTrack(Vector3 move)
        {
            if (!bodyCapsule || move.sqrMagnitude < 0.04f || Mathf.Abs(body.linearVelocity.y) > 0.35f || Time.time < nextTrackStep) return;
            Vector3 forward = move.normalized;
            Vector3 foot = body.position;
            float reach = bodyCapsule.radius + 0.22f;
            Vector3 stepProbe = foot;
            if (LedgeRay(foot + Vector3.up * 0.08f, forward, reach, out var edge) &&
                edge.collider.GetComponentInParent<TrackPiece>() && edge.normal.y <= 0.55f)
                stepProbe = new Vector3(edge.point.x, foot.y, edge.point.z) + forward * 0.055f;
            else if (stalledTime < 0.25f) return;

            Vector3 aboveEdge = stepProbe + Vector3.up * 0.27f;
            if (!LedgeRay(aboveEdge, Vector3.down, 0.30f, out var top) ||
                !top.collider.GetComponentInParent<TrackPiece>() || top.normal.y < 0.65f) return;
            float rise = top.point.y - foot.y;
            if (rise < 0.045f || rise > 0.24f) return;

            float radius = bodyCapsule.radius - 0.015f;
            Vector3 axis = Vector3.up * Mathf.Max(0f, bodyCapsule.height * 0.5f - bodyCapsule.radius);
            Vector3 center = foot + body.rotation * bodyCapsule.center;
            float lift = rise + 0.045f;
            int count = Physics.CapsuleCastNonAlloc(center - axis, center + axis, radius,
                Vector3.up, hits, lift, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return;
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform)) return;

            center += Vector3.up * lift + forward * 0.14f;
            count = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, radius,
                ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return;

            SetLimbClearance(true);
            restoreLimbsAt = Time.time + 0.20f;
            nextTrackStep = Time.time + 0.24f;
            float liftSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * (lift + 0.025f));
            body.AddForce(Vector3.up * Mathf.Max(0f, liftSpeed - body.linearVelocity.y), ForceMode.VelocityChange);
        }

        private void SetLimbClearance(bool clearing)
        {
            if (clearing == clearingLimbs) return;
            clearingLimbs = clearing;
            if (left.Collider) left.Collider.enabled = !clearing;
            if (right.Collider) right.Collider.enabled = !clearing;
            legs?.SetContactEnabled(!clearing);
            if (clearing) legs?.ReleaseSupport();
        }

        private bool ApplyClimbAssist(bool pulling)
        {
            bool leftGrip = left.TryStaticAnchor(out Vector3 leftAnchor);
            bool rightGrip = right.TryStaticAnchor(out Vector3 rightAnchor);
            if (!pulling || !bodyCapsule || (!leftGrip && !rightGrip))
            {
                hasLedge = mantling = false;
                nextLedgeProbe = 0f;
                return false;
            }

            bool leftOnLedge = hasLedge && leftGrip && NearLedge(leftAnchor);
            bool rightOnLedge = hasLedge && rightGrip && NearLedge(rightAnchor);
            if (!leftOnLedge && !rightOnLedge) mantling = false;
            if (Time.time >= nextLedgeProbe)
            {
                nextLedgeProbe = Time.time + 0.10f;
                hasLedge = (leftGrip && TryFindLedge(leftAnchor)) || (rightGrip && TryFindLedge(rightAnchor));
                leftOnLedge = hasLedge && leftGrip && NearLedge(leftAnchor);
                rightOnLedge = hasLedge && rightGrip && NearLedge(rightAnchor);
            }

            int hands = (leftOnLedge ? 1 : 0) + (rightOnLedge ? 1 : 0);
            if (hands < climbHandsRequired)
            {
                mantling = false;
                return false;
            }

            Bounds torso = bodyCapsule.bounds;
            float inwardDistance = Vector3.Dot(body.worldCenterOfMass - ledgeLip, ledgeInward);
            mantling |= torso.center.y >= ledgeLip.y - 0.14f;
            float clearance = ledgeLip.y + 0.04f - torso.min.y;
            if (mantling && inwardDistance >= 0.12f && clearance <= 0.10f)
            {
                if (leftOnLedge) left.FinishClimb();
                if (rightOnLedge) right.FinishClimb();
                hasLedge = mantling = false;
                return false;
            }

            float gripStrength = hands == 2 ? 1f : 0.72f;
            float upSpeed = mantling ? Mathf.Clamp(clearance * 7f, 0f, climbUpSpeed) : climbUpSpeed;
            // Lean onto the lip first; only push strongly inward when the torso can clear it.
            float inwardSpeed = mantling ? Mathf.Lerp(0.20f, 1.05f, Mathf.InverseLerp(0.22f, 0.04f, clearance)) : 0.12f;
            Vector3 velocity = body.linearVelocity;
            float lift = velocity.y < climbUpSpeed ? Mathf.Max(0f, (upSpeed * gripStrength - velocity.y) * 9f - Physics.gravity.y) : 0f;
            Vector3 assist = Vector3.up * lift
                + ledgeInward * ((inwardSpeed - Vector3.Dot(velocity, ledgeInward)) * 9f);
            body.AddForce(Vector3.ClampMagnitude(assist, climbAcceleration * gripStrength), ForceMode.Acceleration);
            return true;
        }

        private bool NearLedge(Vector3 anchor) => (anchor - ledgeLip).sqrMagnitude <= 0.48f * 0.48f
            && Mathf.Abs(Vector3.Dot(anchor - ledgeLip, ledgeInward)) <= 0.26f;

        private bool TryFindLedge(Vector3 anchor)
        {
            Vector3 approach = hasLedge && NearLedge(anchor) ? ledgeInward
                : Vector3.ProjectOnPlane(anchor - body.worldCenterOfMass, Vector3.up).normalized;
            if (approach == Vector3.zero ||
                !LedgeRay(anchor - approach * 0.24f - Vector3.up * 0.10f, approach, 0.45f, out RaycastHit wall) ||
                Mathf.Abs(wall.normal.y) > 0.55f || wall.rigidbody)
                return false;

            Vector3 inward = Vector3.ProjectOnPlane(-wall.normal, Vector3.up).normalized;
            float radius = bodyCapsule.radius;
            Vector3 lip = new Vector3(wall.point.x, anchor.y, wall.point.z);
            Vector3 aboveLip = lip + Vector3.up * 0.30f;
            if (!LedgeRay(aboveLip + inward * 0.08f, Vector3.down, 0.48f, out RaycastHit top) ||
                top.normal.y < 0.70f || top.rigidbody ||
                !LedgeRay(aboveLip + inward * (radius * 0.85f + 0.03f), Vector3.down, 0.48f, out RaycastHit landing) ||
                landing.normal.y < 0.70f || landing.rigidbody || Mathf.Abs(landing.point.y - top.point.y) > 0.12f)
                return false;

            Vector3 lower = landing.point + Vector3.up * (radius / landing.normal.y + 0.04f);
            Vector3 upper = lower + Vector3.up * Mathf.Max(0f, bodyCapsule.height - 2f * radius);
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return false;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return false;

            ledgeLip = new Vector3(lip.x, Mathf.Max(top.point.y, landing.point.y), lip.z);
            ledgeInward = inward;
            return true;
        }

        private bool LedgeRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance >= closest || hit.collider.transform.IsChildOf(transform)) continue;
                closest = hit.distance;
                nearest = hit;
            }
            return nearest.collider != null;
        }

        private bool IsGrounded(out RaycastHit groundHit)
        {
            groundHit = default;
            Vector3 origin = body.position + body.rotation * Vector3.up * 0.28f;
            int count = Physics.SphereCastNonAlloc(origin, 0.22f, Vector3.down, hits, 0.22f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform) && hits[i].normal.y > 0.55f &&
                    (!groundHit.collider || hits[i].distance < groundHit.distance)) groundHit = hits[i];
            return groundHit.collider;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!IsSpawned || !IsServer || wagonPushDirection.sqrMagnitude < 0.01f ||
                !collision.rigidbody || !collision.rigidbody.isKinematic) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (Vector3.Dot(wagonPushDirection, -collision.GetContact(i).normal) < 0.2f) continue;
                if (!trainDrive) trainDrive = FindAnyObjectByType<TrainDrive>();
                if (trainDrive) trainDrive.QueueWagonPush(collision.rigidbody, this, wagonPushDirection);
                break;
            }
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!IsServer)
            {
                left.ShowRemote(leftPose.Value, armStretch.Value.x);
                right.ShowRemote(rightPose.Value, armStretch.Value.y);
                legs?.ShowRemote(leftLegPose.Value, rightLegPose.Value);
            }
            if (!IsOwner || !playerCamera) return;
            Vector3 focus = transform.position + Vector3.up * 0.9f;
            if (PlayerPrefs.GetInt("ChaosUI.CameraBob", 1) == 0)
            {
                if (!hasSmoothFocus || Mathf.Abs(focus.y - smoothFocusY) > 2f) { smoothFocusY = focus.y; smoothFocusVelocity = 0f; }
                smoothFocusY = Mathf.SmoothDamp(smoothFocusY, focus.y, ref smoothFocusVelocity, 0.2f, Mathf.Infinity, Time.unscaledDeltaTime);
                focus.y = smoothFocusY;
                hasSmoothFocus = true;
            }
            else hasSmoothFocus = false;
            Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
            usingLookDownCamera = pitch >= (usingLookDownCamera ? 50f : 58f);
            if (!trainDrive) trainDrive = FindAnyObjectByType<TrainDrive>();
            Transform cabinFrame = null;
            usingCabinCamera = trainDrive && trainDrive.TryGetCabin(transform.position, usingCabinCamera ? 0.04f : -0.06f, out cabinFrame);
            float zoomTarget = usingCabinCamera ? 0f : Mathf.SmoothStep(desiredCameraDistance, 0f, Mathf.InverseLerp(44f, 58f, pitch));
            cameraDistance = PlaygroundControls.EaseCameraDistance(cameraDistance, zoomTarget, ref cameraZoomVelocity);
            float eyeBlend = 1f - Mathf.SmoothStep(0f, 1f, cameraDistance / PlaygroundControls.FirstPersonDistance);
            Vector3 eyeOffset = PlaygroundControls.FirstPersonEyeOffset(yaw) * eyeBlend;
            if (usingCabinCamera)
            {
                Vector3 eye = trainDrive.ClampCabinEye(cabinFrame.InverseTransformPoint(focus + eyeOffset));
                eyeOffset = cabinFrame.TransformPoint(eye) - focus;
            }
            cameraEyeOffset = Vector3.Lerp(cameraEyeOffset, eyeOffset, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            focus += cameraEyeOffset;
            float nearHeight = Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * playerCamera.nearClipPlane;
            float radius = Mathf.Max(0.16f, new Vector3(nearHeight * playerCamera.aspect, nearHeight, playerCamera.nearClipPlane).magnitude);
            Bounds modelBounds = new(focus, Vector3.one * 0.45f);
            foreach (Renderer renderer in cameraBodyRenderers)
                if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) modelBounds.Encapsulate(renderer.bounds);

            Vector3 direction = -(look * Vector3.forward);
            float available = cameraDistance;
            int focusCount = Physics.OverlapSphereNonAlloc(focus, radius, cameraFocusObstacles, ~0, QueryTriggerInteraction.Ignore);
            int count = Physics.SphereCastNonAlloc(focus, radius, direction, hits, available, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var obstacle = hits[i].collider;
                if (obstacle.transform.IsChildOf(transform)) continue;
                bool containsFocus = false;
                for (int j = 0; j < focusCount; j++)
                    if (cameraFocusObstacles[j] == obstacle && (obstacle.ClosestPoint(focus) - focus).sqrMagnitude < 0.000001f)
                    { containsFocus = true; break; }
                if (!containsFocus) available = Mathf.Min(available, Mathf.Max(0f, hits[i].distance - 0.025f));
            }
            if (available < cameraDistance)
            {
                cameraDistance = available;
                cameraZoomVelocity = 0f;
            }
            Vector3 cameraPosition = focus + direction * cameraDistance;
            bool clear = CameraPositionClear(cameraPosition, radius);
            if (!clear)
            {
                for (float distance = available; distance >= 0f; distance -= 0.1f)
                {
                    Vector3 candidate = focus + direction * distance;
                    if (!CameraPositionClear(candidate, radius)) continue;
                    cameraPosition = candidate;
                    cameraDistance = distance;
                    clear = true;
                    break;
                }
            }
            if (!clear && hasClearCameraPosition && (lastClearCameraPosition - focus).sqrMagnitude <= 16f && CameraPositionClear(lastClearCameraPosition, radius) && CameraPathClear(focus, lastClearCameraPosition, radius, focusCount))
            {
                cameraPosition = lastClearCameraPosition;
                clear = true;
            }
            if (!clear)
            {
                for (int i = 0; i < 8 && !clear; i++)
                    for (float distance = 0.5f; distance <= 4f; distance += 0.5f)
                    {
                        Vector3 alternate = Quaternion.AngleAxis((i / 2 + 1) * (i % 2 == 0 ? 45f : -45f), Vector3.up) * direction;
                        Vector3 candidate = focus + alternate * distance;
                        if (!CameraPositionClear(candidate, radius) || !CameraPathClear(focus, candidate, radius, focusCount)) continue;
                        cameraPosition = candidate;
                        clear = true;
                        break;
                    }
            }
            if (clear) { lastClearCameraPosition = cameraPosition; hasClearCameraPosition = true; }
            Vector3 offset = cameraPosition - focus;
            Vector3 viewDirection = offset.sqrMagnitude > 0.0001f ? offset.normalized : direction;
            Vector3 axis = new(Mathf.Abs(viewDirection.x), Mathf.Abs(viewDirection.y), Mathf.Abs(viewDirection.z));
            float modelEdge = Vector3.Dot(modelBounds.center - focus, viewDirection) + Vector3.Dot(modelBounds.extents, axis);
            SetCameraBodyHidden(offset.magnitude < Mathf.Max(PlaygroundControls.FirstPersonDistance,
                modelEdge + radius + (cameraHidesBody ? 0.15f : 0.03f)));
            UpdateCameraFieldOfView(usingCabinCamera || usingLookDownCamera ||
                (desiredCameraDistance >= 1.5f && offset.sqrMagnitude < 1.5f * 1.5f));
            playerCamera.transform.SetPositionAndRotation(cameraPosition, look);
        }

        private void UpdateCameraFieldOfView(bool fixedPosition)
        {
            float target = fixedPosition
                ? PlaygroundControls.ZoomFieldOfView(originalCameraFieldOfView, desiredCameraDistance)
                : originalCameraFieldOfView;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, target, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
        }

        private bool CameraPositionClear(Vector3 position, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return false;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private bool CameraPathClear(Vector3 focus, Vector3 position, float radius, int focusCount)
        {
            Vector3 offset = position - focus;
            if (offset.sqrMagnitude < 0.0001f) return true;
            int count = Physics.SphereCastNonAlloc(focus, radius, offset.normalized, hits, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var obstacle = hits[i].collider;
                if (obstacle.transform.IsChildOf(transform)) continue;
                bool containsFocus = false;
                for (int j = 0; j < focusCount; j++)
                    if (cameraFocusObstacles[j] == obstacle && (obstacle.ClosestPoint(focus) - focus).sqrMagnitude < 0.000001f)
                    { containsFocus = true; break; }
                if (!containsFocus) return false;
            }
            return true;
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
            SetCameraBodyHidden(false);
            if (playerCamera)
            {
                playerCamera.nearClipPlane = originalCameraNearClip;
                playerCamera.fieldOfView = originalCameraFieldOfView;
            }
            left?.Dispose();
            right?.Dispose();
            legs?.Dispose();
            if (body && IsServer)
            {
                body.constraints = originalConstraints;
                body.centerOfMass = originalCenterOfMass;
            }
            if (bodyCapsule) bodyCapsule.radius = originalBodyRadius;
            if (IsOwner) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

// Arm physics exists only on the server; observers receive the shoulder pose.
        private sealed class ArmPhysics
        {
            private static readonly Vector3 HandOffset = new(0f, -0.205f, 0f);
            private const float GripStretch = 1.55f;
            private const float StretchSpeed = 3f;
            private const float GripAssistRadius = 0.28f;
            private static readonly Vector3[] GripAssistRays =
            {
                Vector3.forward, new(-0.62f, 0f, 1f), new(0.62f, 0f, 1f), new(0f, -0.62f, 1f), new(0f, 0.62f, 1f)
            };
            private readonly Transform arm;
            private readonly Rigidbody parent;
            private readonly Rigidbody rigid;
            private readonly Quaternion rest;
            private readonly Vector3 restScale;
            private readonly CapsuleCollider collider;
            private readonly ConfigurableJoint shoulder;
            private Joint grip;
            private Collider gripSurface;
            private Vector3 gripNormal;
            private TrainDrive cabGripTrain;
            private readonly Collider[] leverOverlaps = new Collider[32];
            private float nextAttempt;
            private float handYield;
            private bool releasedAtCrest;

            public Quaternion LocalRotation => arm ? arm.localRotation : Quaternion.identity;
            public Collider Collider => collider;
            public Transform Transform => arm;
            public Vector3 HandPosition => arm ? arm.TransformPoint(HandOffset) : Vector3.zero;
            public Vector3 AimedHandPosition { get; private set; }
            public float Stretch { get; private set; } = 1f;
            public bool IsHoldingBody(Rigidbody candidate) => grip && grip.connectedBody == candidate;
            public bool HoldingShovel => cabGripTrain && cabGripTrain.IsShovelGrip(gripSurface);
            public bool HasGrip => grip;
            public float PushUp(float press, float upSpeed) => cabGripTrain ? 0f
                : PlaygroundControls.HandPushAcceleration(grip, gripSurface, press, upSpeed);
            public void SlideDown() => PlaygroundControls.SlideWallGrip(grip, gripSurface, ref gripNormal);

            public bool TryStaticAnchor(out Vector3 anchor)
            {
                anchor = default;
                if (!grip || !gripSurface || grip.connectedBody) return false;
                if ((grip.connectedAnchor - HandPosition).sqrMagnitude > 0.30f * 0.30f) return false;
                anchor = grip.connectedAnchor;
                if (grip is ConfigurableJoint)
                    anchor -= gripSurface.transform.TransformDirection(gripNormal) * PlaygroundControls.GripPalmOffset;
                return true;
            }

            public ArmPhysics(Transform transform, Rigidbody body, bool server)
            {
                arm = transform;
                parent = body;
                if (!arm) { Debug.LogError("Playground: missing conductor arm."); return; }
                rest = arm.localRotation;
                restScale = arm.localScale;
                rigid = arm.gameObject.AddComponent<Rigidbody>();
                rigid.mass = 0.35f;
                rigid.isKinematic = !server;
                rigid.interpolation = RigidbodyInterpolation.Interpolate;
                collider = arm.gameObject.AddComponent<CapsuleCollider>();
                collider.direction = 1;
                // Keep the shoulder clear of the torso; the forearm/hand remains
                // solid so it cannot sweep through the body during a collision.
                collider.center = HandOffset;
                collider.height = 0.12f;
                collider.radius = 0.045f;
                rigid.centerOfMass = Vector3.zero;
                rigid.inertiaTensor = Vector3.one * 0.01f;
                rigid.inertiaTensorRotation = Quaternion.identity;
                if (!server) return;
                shoulder = arm.gameObject.AddComponent<ConfigurableJoint>();
                shoulder.connectedBody = body;
                shoulder.autoConfigureConnectedAnchor = false;
                shoulder.anchor = Vector3.zero;
                shoulder.connectedAnchor = body.transform.InverseTransformPoint(arm.position);
                shoulder.xMotion = shoulder.yMotion = shoulder.zMotion = ConfigurableJointMotion.Locked;
                shoulder.angularXMotion = shoulder.angularYMotion = shoulder.angularZMotion = ConfigurableJointMotion.Limited;
                shoulder.lowAngularXLimit = new SoftJointLimit { limit = -170f };
                shoulder.highAngularXLimit = new SoftJointLimit { limit = 170f };
                shoulder.angularYLimit = new SoftJointLimit { limit = 85f };
                shoulder.angularZLimit = new SoftJointLimit { limit = 85f };
                shoulder.enableCollision = true;
            }

            public void Step(bool held, bool stretch, Quaternion aim, RaycastHit[] buffer, Transform player, float reach, bool pulling, bool firmLedgeGrip, Vector3 move, bool cabinView)
            {
                if (!rigid) return;
                if (!held) releasedAtCrest = false;
                if (!held || (grip && (!gripSurface || !gripSurface.enabled || !gripSurface.gameObject.activeInHierarchy)) ||
                    (!grip && gripSurface)) Release();
                bool holdingShovel = HoldingShovel;
                float upwardReach = Mathf.InverseLerp(cabinView ? -0.18f : 0.25f, cabinView ? 0.65f : 0.9f, (aim * Vector3.forward).y);
                float cabinReach = cabinView ? Mathf.Sqrt(upwardReach) : upwardReach;
                float targetStretch = holdingShovel ? 0.75f : stretch ? GripStretch : 1f;
                if (!grip) targetStretch = Mathf.Max(targetStretch, Mathf.Lerp(1f, cabinView ? 2.6f : 1.9f, cabinReach));
                if (!(grip is ConfigurableJoint)) StretchToward(targetStretch, Time.fixedDeltaTime);
                // The model's arms are angled outward at rest. Rotate the actual
                // shoulder-to-hand direction toward the camera aim, rather than
                // rotating around the arm's already-tilted local X axis.
                Vector3 lookDirection = aim * Vector3.forward;
                float upwardAim = Mathf.Clamp01(lookDirection.y);
                Vector3 raisedAim = Vector3.Slerp(lookDirection, Vector3.up, 0.35f * upwardAim);
                if (!held) raisedAim = (raisedAim + Vector3.up * 0.20f).normalized;
                Vector3 aimInParent = arm.parent.InverseTransformDirection(raisedAim);
                Vector3 restDirection = rest * Vector3.down;
                float outwardAim = Mathf.Lerp(0.55f, cabinView ? -0.25f : 0.30f, cabinView ? cabinReach : upwardAim);
                aimInParent = (aimInParent + Vector3.right * Mathf.Sign(restDirection.x) * outwardAim).normalized;
                if (grip && gripSurface && grip is ConfigurableJoint)
                {
                    var support = grip.connectedBody;
                    Vector3 anchor = support ? support.transform.TransformPoint(grip.connectedAnchor) : grip.connectedAnchor;
                    Vector3 towardAnchor = arm.parent.InverseTransformDirection(anchor - rigid.position);
                    if (towardAnchor.sqrMagnitude > 0.001f) aimInParent = towardAnchor.normalized;
                }
                Quaternion aiming = Quaternion.FromToRotation(restDirection, aimInParent) * rest;
                AimedHandPosition = arm.position + arm.parent.TransformVector(aiming * Vector3.Scale(arm.localScale, HandOffset));
                // Keep both arms aimed even when their grip buttons are released.
                Quaternion localTarget = holdingShovel ? Quaternion.FromToRotation(restDirection, Vector3.Slerp(Vector3.down, aimInParent, 0.35f)) * rest
                    : Quaternion.Slerp(rest, aiming, held ? 1f : Mathf.Lerp(0.75f, 0.95f, upwardAim));
                bool blocked = false;
                if (!held && collider && collider.enabled && move.sqrMagnitude > 0.01f)
                {
                    Vector3 handPoint = arm.TransformPoint(HandOffset);
                    int count = Physics.OverlapSphereNonAlloc(handPoint + move.normalized * 0.04f, 0.075f,
                        leverOverlaps, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        Collider obstacle = leverOverlaps[i];
                        if (obstacle.transform.IsChildOf(player)) continue;
                        Vector3 nearest = obstacle is MeshCollider mesh && !mesh.convex
                            ? obstacle.bounds.ClosestPoint(handPoint) : obstacle.ClosestPoint(handPoint);
                        if (Mathf.Abs(nearest.y - handPoint.y) > 0.075f) continue;
                        blocked = true;
                        break;
                    }
                }
                handYield = Mathf.MoveTowards(handYield, blocked ? 1f : 0f, Time.fixedDeltaTime * (blocked ? 9f : 4f));
                if (!held) localTarget = Quaternion.Slerp(localTarget, rest, handYield);
                Quaternion desired = arm.parent.rotation * localTarget;
                Quaternion error = desired * Quaternion.Inverse(rigid.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                bool looseGrip = grip && !grip.connectedBody && !firmLedgeGrip;
                if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                    rigid.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * (looseGrip ? 85f : held ? 180f : Mathf.Lerp(32f, 8f, handYield)))
                        - rigid.angularVelocity * (looseGrip ? 7f : held ? 14f : 5f), looseGrip ? 150f : held ? 420f : Mathf.Lerp(55f, 20f, handYield)), ForceMode.Acceleration);

                if (!held || grip || releasedAtCrest || Time.time < nextAttempt) return;
                nextAttempt = Time.time + 0.04f;
                Vector3 hand = arm.TransformPoint(HandOffset);
                Vector3 direction = aim * Vector3.forward;
                if (TryGripNearbyCabControl(hand, direction, buffer, player, reach)) return;
                if (!TryGripCast(hand, direction, 0.13f, 0.38f, buffer, player, reach, false, out RaycastHit chosen) &&
                    !TryGripAssist(hand, aim, buffer, player, reach, out chosen) &&
                    (!pulling || !TryGripCast(hand, Vector3.ProjectOnPlane(direction, Vector3.up).normalized,
                        0.09f, 0.18f, buffer, player, reach, true, out chosen))) return;

                var train = TrainDrive.FindControlOwner(chosen.rigidbody);
                bool cabGrip = train && train.IsSpawned && train.IsOwnedByServer && train.IsCabControlCollider(chosen.collider);
                AttachGrip(chosen.collider, chosen.rigidbody, chosen.point, IsClimbSurface(chosen), cabGrip, chosen.normal);
            }

            private bool AttachGrip(Collider surface, Rigidbody target, Vector3 point, bool climbingGrip, bool cabGrip, Vector3 normal = default)
            {
                if (!cabGrip && (!target || target.isKinematic && !TrainDrive.IsCoalBody(target)) && normal.sqrMagnitude > 0.5f)
                {
                    if (!PlaygroundControls.CanPlantHand(HandPosition, point, normal)) return false;
                    gripSurface = surface;
                    gripNormal = surface.transform.InverseTransformDirection(normal);
                    grip = PlaygroundControls.PlantHand(arm.gameObject, target, HandOffset, point, normal, 1100f);
                    return true;
                }

                if (cabGrip)
                {
                    var train = TrainDrive.FindControlOwner(target);
                    if (!train.TryClaimCabControl(surface, arm)) return false;
                    cabGripTrain = train;
                    if (train.IsShovelGrip(surface)) point = target.position;
                    else if (surface is SphereCollider handle) point = handle.transform.TransformPoint(handle.center);
                    if (collider) Physics.IgnoreCollision(collider, surface, true);
                }
                gripSurface = surface;
                gripNormal = cabGrip ? Vector3.zero : surface.transform.InverseTransformDirection(normal);
                var spring = arm.gameObject.AddComponent<SpringJoint>();
                spring.autoConfigureConnectedAnchor = false;
                spring.connectedBody = target;
                spring.connectedAnchor = target ? target.transform.InverseTransformPoint(point) : point;
                spring.anchor = HandOffset;
                spring.spring = cabGrip ? 450f : climbingGrip ? 380f : 320f;
                spring.damper = cabGrip ? 40f : climbingGrip ? 36f : 32f;
                spring.minDistance = 0f;
                spring.maxDistance = 0.03f;
                spring.breakForce = cabGrip ? 1000f : climbingGrip ? 725f : 550f;
                grip = spring;
                return true;
            }

            private bool TryGripNearbyCabControl(Vector3 hand, Vector3 aim, RaycastHit[] buffer, Transform player, float reach)
            {
                int count = Physics.OverlapSphereNonAlloc(hand, 0.35f, leverOverlaps, ~0, QueryTriggerInteraction.Ignore);
                Collider closest = null;
                Vector3 closestPoint = default;
                float closestDistance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    Collider surface = leverOverlaps[i];
                    Rigidbody target = surface.attachedRigidbody;
                    var train = TrainDrive.FindControlOwner(target);
                    bool cabControl = train && train.IsSpawned && train.IsOwnedByServer && train.IsCabControlCollider(surface);
                    if (!cabControl && !TrainDrive.IsCoalBody(target)) continue;
                    Vector3 point = surface is SphereCollider handle ? handle.transform.TransformPoint(handle.center) : surface.ClosestPoint(hand);
                    if ((point - parent.position).sqrMagnitude > reach * reach) continue;
                    Vector3 offset = point - hand;
                    if ((!train || !train.IsShovelGrip(surface)) && offset.sqrMagnitude > 0.20f * 0.20f) continue;
                    if (Vector3.Dot(offset, aim) < -0.08f || offset.sqrMagnitude >= closestDistance) continue;
                    bool blocked = false;
                    int obstacles = Physics.RaycastNonAlloc(hand, offset.normalized, buffer, Mathf.Max(0f, offset.magnitude - 0.025f),
                        ~0, QueryTriggerInteraction.Ignore);
                    for (int j = 0; j < obstacles && !blocked; j++)
                        blocked = buffer[j].rigidbody != target && !buffer[j].collider.transform.IsChildOf(player);
                    if (blocked) continue;
                    closest = surface;
                    closestPoint = point;
                    closestDistance = offset.sqrMagnitude;
                }
                return closest && AttachGrip(closest, closest.attachedRigidbody, closestPoint, false,
                    !TrainDrive.IsCoalBody(closest.attachedRigidbody));
            }

            private bool TryGripCast(Vector3 hand, Vector3 direction, float radius, float distance,
                RaycastHit[] buffer, Transform player, float reach, bool wallOnly, out RaycastHit chosen)
            {
                int count = Physics.SphereCastNonAlloc(hand, radius, direction, buffer, distance, ~0, QueryTriggerInteraction.Ignore);
                float closest = float.PositiveInfinity;
                chosen = default;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = buffer[i];
                    if (hit.collider.transform.IsChildOf(player) || hit.distance <= 0f || hit.distance >= closest ||
                        (wallOnly && (hit.rigidbody || Mathf.Abs(hit.normal.y) > 0.55f)) ||
                        Vector3.Distance(parent.position, hit.point) > reach) continue;
                    if (hit.rigidbody)
                    {
                        NetworkObject target = hit.rigidbody.GetComponentInParent<NetworkObject>();
                        if ((!target || !target.IsSpawned || !target.IsOwnedByServer) && !TrainDrive.IsCoalBody(hit.rigidbody)) continue;
                    }
                    if (!hit.rigidbody || hit.rigidbody.isKinematic && !TrainDrive.IsCoalBody(hit.rigidbody))
                    {
                        var train = TrainDrive.FindControlOwner(hit.rigidbody);
                        bool control = train && train.IsCabControlCollider(hit.collider);
                        if (!control && !PlaygroundControls.CanPlantHand(hand, hit.point, hit.normal)) continue;
                    }
                    closest = hit.distance;
                    chosen = hit;
                }
                return chosen.collider != null;
            }

            private bool TryGripAssist(Vector3 hand, Quaternion aim, RaycastHit[] buffer, Transform player, float reach, out RaycastHit chosen)
            {
                chosen = default;
                float closest = GripAssistRadius * GripAssistRadius;
                Vector3 forward = aim * Vector3.forward;
                foreach (Vector3 ray in GripAssistRays)
                {
                    Vector3 direction = aim * ray.normalized;
                    // Start just behind the hand so a palm already touching a mesh can still find its surface.
                    const float backtrack = 0.12f;
                    int count = Physics.RaycastNonAlloc(hand - direction * backtrack, direction, buffer,
                        backtrack + GripAssistRadius, ~0, QueryTriggerInteraction.Ignore);
                    if (count == buffer.Length) continue;
                    RaycastHit surface = default;
                    float firstDistance = float.PositiveInfinity;
                    for (int i = 0; i < count; i++)
                    {
                        RaycastHit hit = buffer[i];
                        if (hit.collider.transform.IsChildOf(player) || hit.distance >= firstDistance) continue;
                        firstDistance = hit.distance;
                        surface = hit;
                    }

                    if (!surface.collider) continue;
                    var train = TrainDrive.FindControlOwner(surface.rigidbody);
                    bool cabControl = train && train.IsSpawned && train.IsOwnedByServer && train.IsCabControlCollider(surface.collider);
                    if (!IsClimbSurface(surface) && !cabControl) continue;
                    if (!cabControl && !PlaygroundControls.CanPlantHand(hand, surface.point, surface.normal)) continue;
                    Vector3 offset = surface.point - hand;
                    float distance = offset.sqrMagnitude;
                    if (distance >= closest || Vector3.Dot(offset, forward) < -0.06f ||
                        (surface.point - parent.position).sqrMagnitude > reach * reach) continue;
                    closest = distance;
                    chosen = surface;
                }
                return chosen.collider != null;
            }

            private bool IsClimbSurface(RaycastHit hit) => !hit.rigidbody &&
                (hit.normal.y <= 0.65f || hit.point.y >= parent.worldCenterOfMass.y + 0.06f);

            public void FinishClimb()
            {
                Release();
                releasedAtCrest = true;
            }

            public void ShowRemote(Quaternion rotation, float stretch)
            {
                if (!arm) return;
                arm.localRotation = Quaternion.Slerp(arm.localRotation, rotation, 15f * Time.deltaTime);
                StretchToward(stretch, Time.deltaTime);
            }

            private void StretchToward(float target, float deltaTime)
            {
                if (Stretch == target) return;
                Stretch = Mathf.MoveTowards(Stretch, target, StretchSpeed * deltaTime);
                arm.localScale = new Vector3(restScale.x, restScale.y * Stretch, restScale.z);
            }

            public void Release()
            {
                if (cabGripTrain)
                {
                    cabGripTrain.ReleaseCabControl(gripSurface, arm);
                    if (collider && gripSurface) Physics.IgnoreCollision(collider, gripSurface, false);
                    cabGripTrain = null;
                }
                PlaygroundControls.ReleaseGrip(grip);
                grip = null;
                gripSurface = null;
                nextAttempt = 0f;
            }

            public void Dispose()
            {
                Release();
                if (shoulder) Object.Destroy(shoulder);
                if (collider) Object.Destroy(collider);
                if (rigid) Object.Destroy(rigid);
                if (arm)
                {
                    arm.localRotation = rest;
                    arm.localScale = restScale;
                }
            }
        }
    }
}
