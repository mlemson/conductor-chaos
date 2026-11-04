using Unity.Netcode;
using Unity.Netcode.Components;
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
        private readonly NetworkVariable<Quaternion> leftLegPose = new(Quaternion.identity);
        private readonly NetworkVariable<Quaternion> rightLegPose = new(Quaternion.identity);
        private Rigidbody body;
        private RigidbodyConstraints originalConstraints;
        private Vector3 originalCenterOfMass;
        private CapsuleCollider bodyCapsule;
        private float originalBodyRadius;
        private ArmPhysics left;
        private ArmPhysics right;
        private PlaygroundLegRig legs;
        private Vector2 requestedMove;
        private float requestedYaw;
        private float requestedPitch;
        private bool leftHeld, rightHeld, jumpQueued, sprinting;
        private bool lastLeft, lastRight;
        private float nextInput, nextPose, lastJump;
        private float nextSprintHop;
        private float lastServerInput;
        private Vector3 smoothedLean;
        private float yaw, pitch = 18f;
        private Camera playerCamera;
        private float originalCameraNearClip;
        private Renderer[] cameraBodyRenderers;
        private bool[] cameraBodyRenderingOff;
        private bool cameraHidesBody;
        private static readonly Vector2[] AlternateCameraAngles =
        {
            new(0f, 25f), new(-35f, 25f), new(35f, 25f), new(0f, 60f), new(-70f, 45f), new(70f, 45f)
        };
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] ledgeObstacles = new Collider[16];
        private readonly Vector3[] nearbyWallNormals = new Vector3[16];
        private Vector3 ledgeLip, ledgeInward;
        private float nextLedgeProbe;
        private bool hasLedge, mantling;
        private Vector3 clearGroundPosition;
        private Vector3 blockedRecoveryDirection;
        private Quaternion clearGroundRotation;
        private bool hasClearGround, clearingLimbs;
        private float stalledTime, nextClearanceProbe, nextRecoveryProbe, restoreLimbsAt;
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
            hasClearGround = clearingLimbs = false;
            blockedRecoveryDirection = Vector3.zero;
            stalledTime = nextClearanceProbe = nextRecoveryProbe = 0f;
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
                    originalCameraNearClip = playerCamera.nearClipPlane;
                    playerCamera.nearClipPlane = Mathf.Min(originalCameraNearClip, 0.08f);
                    cameraBodyRenderers = visual ? visual.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
                    cameraBodyRenderingOff = new bool[cameraBodyRenderers.Length];
                }
                yaw = transform.eulerAngles.y;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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
            if (keyboard == null || mouse == null)
            {
                if (lastLeft || lastRight) SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false);
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
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -80f, 65f);
            }
            Vector2 move = active ? new Vector2((keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                               (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector2.zero;
            bool l = active && mouse.leftButton.isPressed;
            bool r = active && mouse.rightButton.isPressed;
            if (active && keyboard.spaceKey.wasPressedThisFrame) JumpServerRpc();
            if (Time.unscaledTime >= nextInput || l != lastLeft || r != lastRight)
            {
                SendInputServerRpc(Vector2.ClampMagnitude(move, 1f), yaw, pitch, l, r, active && keyboard.leftShiftKey.isPressed);
                nextInput = Time.unscaledTime + 0.05f;
                lastLeft = l;
                lastRight = r;
            }
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 move, float direction, float lookPitch, bool l, bool r, bool sprint)
        {
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) || !float.IsFinite(direction) || !float.IsFinite(lookPitch)) return;
            requestedMove = Vector2.ClampMagnitude(move, 1f);
            requestedYaw = Mathf.Repeat(direction, 360f);
            requestedPitch = Mathf.Clamp(lookPitch, -80f, 65f);
            lastServerInput = Time.unscaledTime;
            leftHeld = l;
            rightHeld = r;
            sprinting = sprint;
            if (!l) left?.Release();
            if (!r) right?.Release();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused || !IsSpawned || !IsOwner) return;
            lastLeft = lastRight = false;
            SendInputServerRpc(Vector2.zero, yaw, pitch, false, false, false);
        }

        [ServerRpc]
        private void JumpServerRpc() => jumpQueued = true;

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
                SendInputServerRpc(Vector2.up, 0f, 0f, false, false, false);
                yield return new WaitForSeconds(0.05f);
                if (testLeg) legAngle = Mathf.Max(legAngle, Quaternion.Angle(initialLeg, testLeg.localRotation));
            }
            SendInputServerRpc(Vector2.zero, 0f, 0f, false, false, false);
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
                sprinting = false;
                left.Release();
                right.Release();
            }
            Quaternion facing = Quaternion.Euler(0f, requestedYaw, 0f);
            float uprightControl = Mathf.InverseLerp(0.15f, 0.75f, Vector3.Dot(body.rotation * Vector3.up, Vector3.up));
            Vector3 current = body.linearVelocity;
            bool grounded = IsGrounded();
            Quaternion aim = Quaternion.Euler(requestedPitch, requestedYaw, 0f);
            bool pulling = requestedMove.y > 0.1f;
            bool firmLedgeGrip = hasLedge && pulling;
            left.Step(leftHeld, aim, hits, transform, reach, pulling, firmLedgeGrip);
            right.Step(rightHeld, aim, hits, transform, reach, pulling, firmLedgeGrip);
            bool climbing = ApplyClimbAssist(pulling && !jumpQueued && (leftHeld || rightHeld));
            int hangingHands = StaticGripCenter(out Vector3 gripCenter);
            bool hanging = !climbing && hangingHands > 0;
            Vector3 moveDirection = facing * new Vector3(requestedMove.x, 0f, requestedMove.y);
            if (Vector3.Dot(moveDirection.normalized, blockedRecoveryDirection) < 0.80f)
                blockedRecoveryDirection = Vector3.zero;
            if (blockedRecoveryDirection != Vector3.zero) moveDirection = Vector3.zero;
            if (RecoverFromPinch(moveDirection, grounded)) return;
            float walkSpeed = Mathf.Min(sprinting ? sprintSpeed : speed, sprinting ? 3.8f : 1.5f);
            bool stepped = false;
            if (climbing) legs?.BraceForClimb(mantling);
            bool supported = !climbing && legs != null && legs.Step(current, requestedMove, grounded && current.y - legs.SupportVelocity.y < 0.5f, walkSpeed, sprinting && !hanging, out stepped);
            Vector3 groundVelocity = supported ? legs.SupportVelocity : Vector3.zero;
            Vector3 target = groundVelocity + moveDirection * walkSpeed * uprightControl;
            // Keep some steering when the torso touches uneven ground but neither foot probe finds support.
            float movementAcceleration = grounded ? acceleration * (supported ? (sprinting ? 1.35f : 1f) : 0.45f)
                : acceleration * (sprinting ? 0.45f : 0.2f);
            Vector3 change = Vector3.ClampMagnitude(new Vector3(target.x - current.x, 0f, target.z - current.z), movementAcceleration * Time.fixedDeltaTime);
            if (climbing || hanging) change = Vector3.zero;
            else body.AddForce(change, ForceMode.VelocityChange);
            if (hanging) MoveWhileGripping(gripCenter, moveDirection, hangingHands, pulling && !jumpQueued);

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
                : facing * Quaternion.Euler(smoothedLean.x, 0f, smoothedLean.z);
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

        private bool RecoverFromPinch(Vector3 move, bool grounded)
        {
            if (!bodyCapsule || left.HasGrip || right.HasGrip)
            {
                stalledTime = 0f;
                SetLimbClearance(false);
                return false;
            }

            if (clearingLimbs && Time.time >= restoreLimbsAt) SetLimbClearance(false);
            if (!clearingLimbs && stalledTime < 0.10f && grounded && Time.time >= nextClearanceProbe &&
                Vector3.Dot(body.rotation * Vector3.up, Vector3.up) > 0.94f && Mathf.Abs(body.linearVelocity.y) < 0.6f)
            {
                nextClearanceProbe = Time.time + 0.12f;
                Quaternion upright = Quaternion.Euler(0f, body.rotation.eulerAngles.y, 0f);
                if (IsClearGround(body.position, upright))
                {
                    clearGroundPosition = body.position;
                    clearGroundRotation = upright;
                    hasClearGround = true;
                }
            }

            Vector3 direction = move.normalized;
            if (move.sqrMagnitude < 0.04f || jumpQueued || Mathf.Abs(body.linearVelocity.y) > 1f ||
                Vector3.Dot(body.linearVelocity, direction) >= 0.18f)
            {
                stalledTime = 0f;
                return false;
            }
            stalledTime += Time.fixedDeltaTime;
            if (stalledTime < 0.25f || Time.time < nextRecoveryProbe) return false;
            nextRecoveryProbe = Time.time + 0.12f;

            Vector3 origin = body.worldCenterOfMass;
            bool canBackOut = false, pinched = false;
            int wallCount = 0;
            for (int i = 0; i < nearbyWallNormals.Length; i++)
            {
                Vector3 ray = Quaternion.AngleAxis((i % 8) * 45f, Vector3.up) * direction;
                Vector3 sample = i < 8 ? origin : origin - Vector3.up * 0.20f;
                if (!LedgeRay(sample, ray, bodyCapsule.radius + 0.21f, out RaycastHit wall) ||
                    wall.rigidbody || Mathf.Abs(wall.normal.y) > 0.60f) continue;
                Vector3 normal = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
                for (int j = 0; j < wallCount; j++)
                    if (Vector3.Dot(nearbyWallNormals[j], normal) < -0.40f) pinched = true;
                nearbyWallNormals[wallCount++] = normal;
                canBackOut |= Vector3.Dot(direction, normal) >= -0.10f;
            }
            if (!pinched && stalledTime >= 0.85f) pinched = TorsoInsideStaticWall();
            if (!pinched && !canBackOut) return false;

            // Give loose limbs time to slip free while the torso continues to collide normally.
            SetLimbClearance(true);
            restoreLimbsAt = Time.time + 0.28f;
            Vector3 fromClearGround = body.position - clearGroundPosition;
            if (!pinched || stalledTime < 0.85f || !hasClearGround || fromClearGround.sqrMagnitude < 0.20f * 0.20f ||
                fromClearGround.sqrMagnitude > 1.5f * 1.5f ||
                Mathf.Abs(fromClearGround.y) > 0.55f || !IsClearGround(clearGroundPosition, clearGroundRotation)) return false;

            ReturnToClearGround();
            blockedRecoveryDirection = direction;
            stalledTime = 0f;
            return true;
        }

        private bool IsClearGround(Vector3 position, Quaternion rotation)
        {
            if (!LedgeRay(position + Vector3.up * 0.24f, Vector3.down, 0.41f, out RaycastHit floor) ||
                floor.rigidbody || floor.normal.y < 0.72f) return false;
            float radius = bodyCapsule.radius + 0.20f;
            float halfSegment = Mathf.Max(0f, bodyCapsule.height * 0.5f - radius);
            Vector3 center = position + rotation * bodyCapsule.center + Vector3.up * 0.045f;
            int count = Physics.OverlapCapsuleNonAlloc(center - Vector3.up * halfSegment,
                center + Vector3.up * halfSegment, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return false;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        private bool TorsoInsideStaticWall()
        {
            float radius = bodyCapsule.radius;
            Vector3 center = body.position + body.rotation * bodyCapsule.center;
            Vector3 axis = body.rotation * Vector3.up * Mathf.Max(0f, bodyCapsule.height * 0.5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = ledgeObstacles[i];
                if (obstacle.attachedRigidbody || obstacle.transform.IsChildOf(transform)) continue;
                if (Physics.ComputePenetration(bodyCapsule, body.position, body.rotation, obstacle,
                        obstacle.transform.position, obstacle.transform.rotation, out Vector3 normal, out float depth) &&
                    depth > 0.08f && Mathf.Abs(normal.y) < 0.60f) return true;
            }
            return false;
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

        private void ReturnToClearGround()
        {
            Rigidbody[] parts = GetComponentsInChildren<Rigidbody>();
            Vector3[] positions = new Vector3[parts.Length];
            Quaternion[] rotations = new Quaternion[parts.Length];
            Quaternion turn = clearGroundRotation * Quaternion.Inverse(body.rotation);
            for (int i = 0; i < parts.Length; i++)
            {
                positions[i] = clearGroundPosition + turn * (parts[i].position - body.position);
                rotations[i] = turn * parts[i].rotation;
            }

            if (TryGetComponent(out NetworkTransform networkTransform))
                networkTransform.Teleport(clearGroundPosition, clearGroundRotation, transform.localScale);
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].position = positions[i];
                parts[i].rotation = rotations[i];
                parts[i].linearVelocity = Vector3.zero;
                parts[i].angularVelocity = Vector3.zero;
            }
            legs?.ReleaseSupport();
            smoothedLean = Vector3.zero;
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
            Vector3 focus = transform.position + Vector3.up * 0.9f;
            Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
            float nearHeight = Mathf.Tan(playerCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * playerCamera.nearClipPlane;
            float radius = Mathf.Max(0.16f, new Vector3(nearHeight * playerCamera.aspect, nearHeight, playerCamera.nearClipPlane).magnitude);
            Bounds modelBounds = new(focus, Vector3.one * 0.45f);
            foreach (Renderer renderer in cameraBodyRenderers)
                if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy) modelBounds.Encapsulate(renderer.bounds);

            Vector3 fallbackPosition = focus;
            Quaternion fallbackLook = look;
            float fallbackDistance = -1f;
            if (TryCameraOrbit(focus, look, radius, modelBounds, ref fallbackPosition, ref fallbackLook, ref fallbackDistance)) return;
            foreach (Vector2 alternate in AlternateCameraAngles)
            {
                Quaternion rotated = Quaternion.Euler(Mathf.Max(pitch, alternate.y), yaw + alternate.x, 0f);
                if (TryCameraOrbit(focus, rotated, radius, modelBounds, ref fallbackPosition, ref fallbackLook, ref fallbackDistance)) return;
            }

            // A gap can be narrower than the camera and the model together.
            SetCameraBodyHidden(true);
            playerCamera.transform.SetPositionAndRotation(fallbackPosition, fallbackLook);
        }

        private bool TryCameraOrbit(Vector3 focus, Quaternion look, float radius, Bounds modelBounds,
            ref Vector3 fallbackPosition, ref Quaternion fallbackLook, ref float fallbackDistance)
        {
            Vector3 direction = -(look * Vector3.forward);
            float distance = 4f;
            int count = Physics.SphereCastNonAlloc(focus, radius, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(transform)) distance = Mathf.Min(distance, Mathf.Max(0f, hits[i].distance - 0.025f));

            Vector3 position = focus + direction * distance;
            count = Physics.OverlapSphereNonAlloc(position, radius, ledgeObstacles, ~0, QueryTriggerInteraction.Ignore);
            if (count == ledgeObstacles.Length) return false;
            for (int i = 0; i < count; i++)
                if (!ledgeObstacles[i].transform.IsChildOf(transform)) return false;

            if (distance > fallbackDistance)
            {
                fallbackPosition = position;
                fallbackLook = look;
                fallbackDistance = distance;
            }
            Vector3 axis = new(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float modelEdge = Vector3.Dot(modelBounds.center - focus, direction) + Vector3.Dot(modelBounds.extents, axis);
            if (distance < modelEdge + radius + 0.03f) return false;

            SetCameraBodyHidden(false);
            playerCamera.transform.SetPositionAndRotation(position, look);
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
                renderer.forceRenderingOff = hidden || cameraBodyRenderingOff[i];
            }
        }

        public override void OnNetworkDespawn()
        {
            SetCameraBodyHidden(false);
            if (playerCamera) playerCamera.nearClipPlane = originalCameraNearClip;
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

        private void OnGUI()
        {
            if (!IsSpawned || !IsOwner) return;
            GUI.Label(new Rect(16, 16, 1500, 24), "WASD lopen | Shift rennen | spatie springen | muisknop vasthouden: grijpen | W bij een rand: klimmen | Esc: cursor");
        }

        // Arm physics exists only on the server; observers receive the shoulder pose.
        private sealed class ArmPhysics
        {
            private static readonly Vector3 HandOffset = new(0f, -0.205f, 0f);
            private readonly Transform arm;
            private readonly Rigidbody parent;
            private readonly Rigidbody rigid;
            private readonly Quaternion rest;
            private readonly CapsuleCollider collider;
            private readonly ConfigurableJoint shoulder;
            private SpringJoint grip;
            private Collider gripSurface;
            private float nextAttempt;
            private bool releasedAtCrest;

            public Quaternion LocalRotation => arm ? arm.localRotation : Quaternion.identity;
            public Collider Collider => collider;
            public bool HasGrip => grip;
            public bool TryStaticAnchor(out Vector3 anchor)
            {
                anchor = default;
                if (!grip || !gripSurface || grip.connectedBody) return false;
                anchor = grip.connectedAnchor;
                return (anchor - arm.TransformPoint(HandOffset)).sqrMagnitude <= 0.30f * 0.30f;
            }

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

            public void Step(bool held, Quaternion aim, RaycastHit[] buffer, Transform player, float reach, bool pulling, bool firmLedgeGrip)
            {
                if (!rigid) return;
                if (!held) releasedAtCrest = false;
                if (!held || (grip && (!gripSurface || !gripSurface.enabled || !gripSurface.gameObject.activeInHierarchy))) Release();
                // The model's arms are angled outward at rest. Rotate the actual
                // shoulder-to-hand direction toward the camera aim, rather than
                // rotating around the arm's already-tilted local X axis.
                Vector3 aimInParent = arm.parent.InverseTransformDirection(aim * Vector3.forward);
                Vector3 restDirection = rest * Vector3.down;
                aimInParent = (aimInParent + Vector3.right * Mathf.Sign(restDirection.x) * 0.55f).normalized;
                if (grip && !grip.connectedBody)
                {
                    Vector3 towardAnchor = arm.parent.InverseTransformDirection(grip.connectedAnchor - rigid.position);
                    if (towardAnchor.sqrMagnitude > 0.001f) aimInParent = towardAnchor.normalized;
                }
                Quaternion aiming = Quaternion.FromToRotation(restDirection, aimInParent) * rest;
                // Follow the look direction even when the hand is not gripping.
                Quaternion localTarget = Quaternion.Slerp(rest, aiming, held ? 1f : 0.75f);
                Quaternion desired = arm.parent.rotation * localTarget;
                Quaternion error = desired * Quaternion.Inverse(rigid.rotation);
                error.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                bool looseGrip = grip && !grip.connectedBody && !firmLedgeGrip;
                if (float.IsFinite(axis.x) && axis.sqrMagnitude > 0.01f)
                    rigid.AddTorque(Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad * (looseGrip ? 85f : held ? 180f : 32f))
                        - rigid.angularVelocity * (looseGrip ? 7f : held ? 14f : 5f), looseGrip ? 150f : held ? 420f : 55f), ForceMode.Acceleration);

                if (!held || grip || releasedAtCrest || Time.time < nextAttempt) return;
                nextAttempt = Time.time + 0.1f;
                Vector3 hand = arm.TransformPoint(HandOffset);
                Vector3 direction = aim * Vector3.forward;
                if (!TryGripCast(hand, direction, 0.13f, 0.38f, buffer, player, reach, false, out RaycastHit chosen) &&
                    (!pulling || !TryGripCast(hand, Vector3.ProjectOnPlane(direction, Vector3.up).normalized,
                        0.09f, 0.18f, buffer, player, reach, true, out chosen))) return;

                gripSurface = chosen.collider;
                grip = arm.gameObject.AddComponent<SpringJoint>();
                grip.autoConfigureConnectedAnchor = false;
                grip.connectedBody = chosen.rigidbody;
                grip.connectedAnchor = chosen.rigidbody ? chosen.rigidbody.transform.InverseTransformPoint(chosen.point) : chosen.point;
                grip.anchor = HandOffset;
                grip.spring = 320f;
                grip.damper = 32f;
                grip.minDistance = 0f;
                grip.maxDistance = 0.03f;
                grip.breakForce = 550f;
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
                    if (hit.collider.transform.IsChildOf(player) || hit.distance >= closest ||
                        (wallOnly && (hit.rigidbody || Mathf.Abs(hit.normal.y) > 0.55f)) ||
                        Vector3.Distance(parent.position, hit.point) > reach) continue;
                    if (hit.rigidbody)
                    {
                        NetworkObject target = hit.rigidbody.GetComponentInParent<NetworkObject>();
                        if (!target || !target.IsSpawned || !target.IsOwnedByServer) continue;
                    }
                    closest = hit.distance;
                    chosen = hit;
                }
                return chosen.collider != null;
            }

            public void FinishClimb()
            {
                Release();
                releasedAtCrest = true;
            }

            public void ShowRemote(Quaternion rotation)
            {
                if (arm) arm.localRotation = Quaternion.Slerp(arm.localRotation, rotation, 15f * Time.deltaTime);
            }

            public void Release()
            {
                if (grip) Object.Destroy(grip);
                grip = null;
                gripSurface = null;
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
