using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01ParkourMotor : MonoBehaviour
    {
        [Header("Horizontal")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float groundAcceleration = 32f;
        [SerializeField] private float groundDeceleration = 40f;
        [SerializeField, Range(0.2f, 1f)] private float airControlMultiplier = 0.65f;

        [Header("Jump")]
        [SerializeField] private float jumpHeight = 2.25f;
        [SerializeField] private float timeToApex = 0.45f;
        [SerializeField] private float fallGravityMultiplier = 1.45f;
        [SerializeField] private float lowJumpGravityMultiplier = 2.2f;
        [SerializeField] private float maximumFallSpeed = 16f;
        [SerializeField] private float coyoteTime = 0.14f;
        [SerializeField] private float jumpBufferTime = 0.14f;

        [Header("Ground Probe")]
        [SerializeField] private float groundCheckRadius = 0.27f;
        [SerializeField] private float groundCheckOffset = 0.08f;
        [SerializeField] private float groundSnapDistance = 0.38f;
        [SerializeField] private float groundSnapProbeHeight = 0.45f;

        private readonly Collider[] groundHits = new Collider[8];
        private readonly RaycastHit[] groundSnapHits = new RaycastHit[8];
        private Rigidbody body;
        private Collider ownCollider;
        private float inputAxis;
        private bool jumpHeld;
        private bool useSimulatedInput;
        private bool simulatedJumpPressed;
        private bool inputLocked;
        private bool laneClampSuspended;
        /// <summary>
        /// Set while an authored launch is in the air. A launch is not a released jump, so the short-hop
        /// gravity must not apply to it: without this latch a 12 m/s launch only reached 1.35 m because the
        /// module treated it as a tapped jump, which made an authored launcher's apex unpredictable.
        /// </summary>
        private bool externalLaunchHeld;
        private bool laneBlockSet;
        private float laneBlockInner;
        private float laneBlockOuter;
        private float previousLaneScalar;
        private bool jumpConsumed;
        private bool leftGroundAfterJump;

        // Authored air currents ("draughts"). A draught is a ride, not a jump: it carries the player along
        // the lane at an authored speed while holding their feet on an authored ride line, and it leaves the
        // jump state alone so a crossing never counts as a jump and never eats the player's jump. The intent
        // is refreshed every physics step by whichever current contains the player and expires on its own, so
        // a component that stops calling in - the player leaving it, the current pulsing off, the object
        // being disabled - cannot leave the player flying.
        private const float DraughtStiffness = 4.5f;
        private const float DraughtMaximumRise = 3.4f;
        private const float DraughtMaximumSink = 1.6f;
        private const float DraughtAcceleration = 26f;
        private const float DraughtHoldSeconds = 0.15f;
        private float draughtAlongSpeed;
        private float draughtRideHeight;
        private float draughtUntil;
        private bool draughting;
        private float coyoteTimer;
        private float bufferTimer;
        private Phase01MovingPlatform groundPlatform;
        private Vector3 movementDirection = Vector3.right;
        private Vector3 movementPlaneOrigin;
        private float minimumScalar = float.NegativeInfinity;
        private float maximumScalar = float.PositiveInfinity;
        private bool useExternalInput;
        private bool externalJumpPressed;
        private Func<Vector3, bool> groundingValidityProbe;

        public bool IsGrounded { get; private set; }
        public float HorizontalVelocity => body != null ? Vector3.Dot(body.linearVelocity, movementDirection) : 0f;
        public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;
        public float CoyoteTimer => coyoteTimer;
        public float JumpBufferTimer => bufferTimer;
        public float MoveSpeed => moveSpeed;
        public float GroundAcceleration => groundAcceleration;
        public float GroundDeceleration => groundDeceleration;
        public float AirControlMultiplier => airControlMultiplier;
        public float JumpHeight => jumpHeight;
        public float TimeToApex => timeToApex;
        public float Gravity => -2f * jumpHeight / (timeToApex * timeToApex);
        public float JumpLaunchSpeed => 2f * jumpHeight / timeToApex;
        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public float MaximumFallSpeed => maximumFallSpeed;
        public float GroundSnapDistance => groundSnapDistance;
        public Phase01MovingPlatform GroundPlatform => groundPlatform;
        public bool IsUsingSimulatedInput => useSimulatedInput;
        /// <summary>
        /// True while a host choreography owns the player's position outright. The accepted movement
        /// values are unchanged by this flag: it only gates the per-step lane projection below.
        /// </summary>
        public bool LaneClampSuspended => laneClampSuspended;
        public int AcceptedJumpCount { get; private set; }

        /// <summary>Launches performed by authored launchers rather than by the player's jump.</summary>
        public int ExternalLaunchCount { get; private set; }
        public int LandingCount { get; private set; }
        public int GroundSnapCount { get; private set; }

        /// <summary>Physics steps the player has spent being carried by an authored air current.</summary>
        public int DraughtFrameCount { get; private set; }

        /// <summary>Seconds the player has spent being carried. Test evidence that a ride is a ride.</summary>
        public float DraughtSeconds { get; private set; }

        /// <summary>True while an authored current is carrying the player.</summary>
        public bool IsDraughting => draughting;

        /// <summary>True while the jump button is held. Authored currents gate on this.</summary>
        public bool IsJumpHeld => jumpHeld;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            ownCollider = GetComponent<Collider>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void Update()
        {
            if (inputLocked)
            {
                inputAxis = 0f;
                return;
            }

            if (useExternalInput)
            {
                if (externalJumpPressed)
                {
                    externalJumpPressed = false;
                    QueueJump();
                }
            }
            else if (!useSimulatedInput)
            {
                Keyboard keyboard = Keyboard.current;
                inputAxis = keyboard == null ? 0f
                    : (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                jumpHeld = keyboard != null && keyboard.spaceKey.isPressed;
                if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    QueueJump();
            }
            else if (simulatedJumpPressed)
            {
                simulatedJumpPressed = false;
                QueueJump();
            }
        }

        private void FixedUpdate()
        {
            // The Phase 01 branch rotation deliberately moves the player off the active lane so that the
            // world and camera turn while the player holds station on screen. Without this guard the lane
            // projection at the end of FixedUpdate would snap the player back onto the old lane every
            // physics step, which is what made the rotation read inconsistently.
            if (laneClampSuspended)
            {
                body.linearVelocity = Vector3.zero;
                return;
            }

            draughting = Time.time < draughtUntil;
            bool wasGrounded = IsGrounded;
            ProbeGround();
            if (IsGrounded)
            {
                coyoteTimer = coyoteTime;
                externalLaunchHeld = false;
                if (!wasGrounded)
                    LandingCount++;
                if (leftGroundAfterJump)
                {
                    jumpConsumed = false;
                    leftGroundAfterJump = false;
                }
                if (groundPlatform != null)
                    body.position += groundPlatform.FrameDelta;
            }
            else
            {
                coyoteTimer = Mathf.Max(0f, coyoteTimer - Time.fixedDeltaTime);
                if (jumpConsumed)
                    leftGroundAfterJump = true;
            }

            bufferTimer = Mathf.Max(0f, bufferTimer - Time.fixedDeltaTime);
            Vector3 velocity = body.linearVelocity;
            float targetSpeed = inputAxis * moveSpeed;
            float rate = Mathf.Abs(targetSpeed) > 0.01f ? groundAcceleration : groundDeceleration;
            if (!IsGrounded)
                rate *= airControlMultiplier;
            float currentSpeed = Vector3.Dot(velocity, movementDirection);
            float nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.fixedDeltaTime);
            float currentScalar = Vector3.Dot(body.position - movementPlaneOrigin, movementDirection);
            if ((currentScalar <= minimumScalar + 0.001f && nextSpeed < 0f)
                || (currentScalar >= maximumScalar - 0.001f && nextSpeed > 0f))
                nextSpeed = 0f;
            velocity += movementDirection * (nextSpeed - currentSpeed);
            Vector3 lateral = Vector3.Cross(Vector3.up, movementDirection);
            velocity -= lateral * Vector3.Dot(velocity, lateral);

            if (bufferTimer > 0f && coyoteTimer > 0f && !jumpConsumed)
            {
                velocity.y = JumpLaunchSpeed;
                bufferTimer = 0f;
                coyoteTimer = 0f;
                IsGrounded = false;
                groundPlatform = null;
                jumpConsumed = true;
                leftGroundAfterJump = false;
                AcceptedJumpCount++;
            }
            else if (!IsGrounded || velocity.y > 0.01f)
            {
                float multiplier = velocity.y < 0f
                    ? fallGravityMultiplier
                    : jumpHeld || externalLaunchHeld ? 1f : lowJumpGravityMultiplier;
                velocity.y = Mathf.Max(
                    velocity.y + Gravity * multiplier * Time.fixedDeltaTime,
                    -maximumFallSpeed);
            }
            else if (velocity.y < 0f)
            {
                velocity.y = -0.5f;
            }

            if (draughting)
            {
                // Hold the player's feet on the ride line and carry them along the lane. Both are velocity
                // targets rather than teleports, so a draught can never put the player inside geometry, and
                // the lane clamp below still owns the along-lane position.
                float rideError = draughtRideHeight - body.position.y;
                // Assigned, not accelerated towards: a draught is a levitation, so a player who enters it
                // falling fast - off the end of a jump, which is exactly how a current is entered - has to be
                // caught on their first step inside it. Accelerating towards the ride line let a fast entry
                // keep most of its fall speed and drop out of the bottom of the current instead.
                velocity.y = Mathf.Clamp(
                    rideError * DraughtStiffness,
                    -DraughtMaximumSink,
                    DraughtMaximumRise);

                float alongSpeed = Vector3.Dot(velocity, movementDirection);
                float carriedSpeed = Mathf.MoveTowards(
                    alongSpeed,
                    draughtAlongSpeed,
                    DraughtAcceleration * Time.fixedDeltaTime);
                velocity += movementDirection * (carriedSpeed - alongSpeed);

                DraughtFrameCount++;
                DraughtSeconds += Time.fixedDeltaTime;
            }

            body.linearVelocity = velocity;
            Vector3 constrained = body.position;
            float unclampedScalar = Vector3.Dot(constrained - movementPlaneOrigin, movementDirection);
            float scalar = Mathf.Clamp(
                unclampedScalar,
                minimumScalar,
                maximumScalar);
            // A lane hazard (a bar crossing the route) is a wall the player has to deal with, not something
            // that shoves them: without this the solver resolves a kinematic bar against a player whose
            // velocity and position are rewritten every step, and the result is a bar that carries the
            // player along - or squeezes them off a narrow ledge. Only entry is refused, so a player who is
            // already inside the band is never trapped and can always walk or jump clear.
            if (laneBlockSet && laneBlockOuter > laneBlockInner)
            {
                bool wasInside = previousLaneScalar > laneBlockInner && previousLaneScalar < laneBlockOuter;
                if (!wasInside)
                {
                    if (previousLaneScalar <= laneBlockInner)
                        scalar = Mathf.Min(scalar, laneBlockInner);
                    else
                        scalar = Mathf.Max(scalar, laneBlockOuter);
                }
            }
            previousLaneScalar = scalar;
            if (scalar != unclampedScalar)
            {
                float constrainedSpeed = Vector3.Dot(velocity, movementDirection);
                if ((unclampedScalar < minimumScalar && constrainedSpeed < 0f)
                    || (unclampedScalar > maximumScalar && constrainedSpeed > 0f))
                    velocity -= movementDirection * constrainedSpeed;
            }
            constrained = movementPlaneOrigin + movementDirection * scalar + Vector3.up * constrained.y;
            body.linearVelocity = velocity;
            body.position = constrained;
        }

        /// <summary>Uses this sandbox action module on a rotated 2.5D lane.</summary>
        public void SetMovementAxis(Vector3 direction, Vector3 planeOrigin, float minScalar, float maxScalar)
        {
            if (direction.sqrMagnitude > 0.0001f)
                movementDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            movementPlaneOrigin = planeOrigin;
            minimumScalar = minScalar;
            maximumScalar = maxScalar;
            if (body != null)
                previousLaneScalar = Vector3.Dot(body.position - movementPlaneOrigin, movementDirection);
        }

        /// <summary>
        /// Lets a host lane reject geometric side-contact as ground (for an intentional hole)
        /// without changing the sandbox's normal collider-driven movement.
        /// </summary>
        public void SetGroundingValidityProbe(Func<Vector3, bool> probe)
        {
            groundingValidityProbe = probe;
        }

        /// <summary>Feeds a host game's input without this component reading the keyboard.</summary>
        public void SetExternalInput(float horizontal, bool holdJump, bool jumpPressed)
        {
            useExternalInput = true;
            inputAxis = Mathf.Clamp(horizontal, -1f, 1f);
            jumpHeld = holdJump;
            externalJumpPressed |= jumpPressed;
        }

        public void SetSimulatedInput(float horizontal, bool holdJump)
        {
            useSimulatedInput = true;
            inputAxis = Mathf.Clamp(horizontal, -1f, 1f);
            jumpHeld = holdJump;
        }

        public void SimulateJumpPress(bool holdJump = true)
        {
            useSimulatedInput = true;
            jumpHeld = holdJump;
            simulatedJumpPressed = false;
            QueueJump();
        }

        public void SimulateJumpRelease()
        {
            useSimulatedInput = true;
            jumpHeld = false;
        }

        public void ClearSimulatedInput()
        {
            useSimulatedInput = false;
            inputAxis = 0f;
            jumpHeld = false;
            simulatedJumpPressed = false;
        }

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (locked)
            {
                inputAxis = 0f;
                jumpHeld = false;
                simulatedJumpPressed = false;
                externalJumpPressed = false;
            }
        }

        public void TeleportAndReset(Vector3 position)
        {
            // The player is placed, not moved, so re-derive which side of any lane hazard they are on.
            previousLaneScalar = Vector3.Dot(position - movementPlaneOrigin, movementDirection);
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            coyoteTimer = 0f;
            bufferTimer = 0f;
            jumpConsumed = false;
            leftGroundAfterJump = false;
            IsGrounded = false;
            groundPlatform = null;
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Launches the player upward from an authored launcher - a bounce pad or equivalent.
        ///
        /// This mirrors exactly what a jump does to the internal state: it consumes the jump, clears the
        /// coyote and buffer windows and leaves the ground. Without that, the ground snap (which is skipped
        /// only when a jump has been consumed) pulled the launch straight back onto the ledge every step, so
        /// a launcher could fire repeatedly and move the player nowhere.
        /// </summary>
        public void LaunchVertically(float speed)
        {
            Vector3 velocity = body.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, speed);
            body.linearVelocity = velocity;
            coyoteTimer = 0f;
            bufferTimer = 0f;
            jumpConsumed = true;
            leftGroundAfterJump = true;
            IsGrounded = false;
            groundPlatform = null;
            externalLaunchHeld = true;
            ExternalLaunchCount++;
        }

        /// <summary>
        /// Hands an authored air current to the movement module. The caller refreshes this every physics
        /// step while the player is inside the current; the intent lapses on its own if it stops arriving.
        /// </summary>
        /// <param name="alongSpeed">Signed speed along the lane, in metres per second.</param>
        /// <param name="rideHeight">World height the current holds the player's feet at.</param>
        public void ApplyDraught(float alongSpeed, float rideHeight)
        {
            draughtAlongSpeed = alongSpeed;
            draughtRideHeight = rideHeight;
            draughtUntil = Time.time + DraughtHoldSeconds;
        }

        public void SetVelocityForAutomation(Vector3 velocity)
        {
            body.linearVelocity = movementDirection * Vector3.Dot(velocity, movementDirection)
                + Vector3.up * velocity.y;
        }

        /// <summary>
        /// Marks a band of the current lane as blocked for this step, so a hazard can stand in the player's
        /// way instead of shoving them along it. Entry is refused from whichever side the player is on, and
        /// a player already inside the band is never trapped, so clearing it is always possible.
        /// </summary>
        public void SetLaneBlock(bool blocked, float innerScalar, float outerScalar)
        {
            laneBlockSet = blocked && outerScalar > innerScalar;
            laneBlockInner = innerScalar;
            laneBlockOuter = outerScalar;
        }

        /// <summary>
        /// Lets a host choreography own the player's position for a scripted beat (the Phase 01 branch
        /// rotation). While suspended the motor applies no lane projection, gravity or input, so the
        /// host can place the player anywhere; resuming restores the normal accepted behaviour.
        /// </summary>
        public void SetLaneClampSuspended(bool suspended)
        {
            laneClampSuspended = suspended;
            if (!suspended)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            coyoteTimer = 0f;
            bufferTimer = 0f;
            jumpConsumed = false;
            leftGroundAfterJump = false;
            IsGrounded = false;
            groundPlatform = null;
        }

        private void QueueJump() => bufferTimer = jumpBufferTime;

        private void ProbeGround()
        {
            if (jumpConsumed && body.linearVelocity.y > 0.2f)
            {
                IsGrounded = false;
                groundPlatform = null;
                return;
            }

            Vector3 center = body.position + Vector3.up * groundCheckOffset;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                groundCheckRadius,
                groundHits,
                ~0,
                QueryTriggerInteraction.Ignore);
            Collider found = null;
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i] != null && groundHits[i] != ownCollider)
                {
                    found = groundHits[i];
                    break;
                }
            }

            if (found != null && groundingValidityProbe != null && !groundingValidityProbe(body.position))
                found = null;

            // A draught carries the player clear of the surface on purpose, so the ground snap must not drag
            // them back onto the lip they just left.
            if (found == null && !jumpConsumed && !draughting)
                found = TrySnapToGround();

            IsGrounded = found != null;
            groundPlatform = found != null ? found.GetComponentInParent<Phase01MovingPlatform>() : null;
        }

        private Collider TrySnapToGround()
        {
            Vector3 origin = body.position + Vector3.up * groundSnapProbeHeight;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                groundSnapHits,
                groundSnapProbeHeight + groundSnapDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            Collider nearest = null;
            Vector3 nearestPoint = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = groundSnapHits[i];
                Collider candidate = hit.collider;
                if (candidate == null
                    || candidate == ownCollider
                    || hit.normal.y < 0.55f
                    || hit.distance >= nearestDistance)
                    continue;

                float verticalDelta = hit.point.y - body.position.y;
                if (verticalDelta < -groundSnapDistance || verticalDelta > groundSnapDistance)
                    continue;

                Vector3 candidatePosition = body.position;
                candidatePosition.y = hit.point.y;
                if (groundingValidityProbe != null && !groundingValidityProbe(candidatePosition))
                    continue;

                nearestDistance = hit.distance;
                nearest = candidate;
                nearestPoint = hit.point;
            }

            if (nearest == null)
                return null;

            Vector3 snapped = body.position;
            snapped.y = nearestPoint.y;
            body.position = snapped;
            Vector3 velocity = body.linearVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = -0.5f;
                body.linearVelocity = velocity;
            }
            GroundSnapCount++;
            return nearest;
        }
    }
}
