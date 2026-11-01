using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Phase01.Demo0
{
    /// <summary>Production branch adapter for the approved sandbox action module.</summary>
    [RequireComponent(typeof(Phase01ParkourMotor))]
    public sealed class Phase01PlayerMotor : MonoBehaviour
    {
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float groundOffset = 0.72f;

        private Phase01ParkourMotor action;
        private Phase01SweeperGate[] gates;
        private Phase01BranchTerrain gatesBranchTerrain;
        private int gatesBranchIndex = -1;
        /// <summary>
        /// Small gap left between the player and a blocking bar, so the player rests against the bar
        /// instead of being clamped into it.
        /// </summary>
        private const float LaneBlockStandoff = 0.25f;
        private bool useSimulatedInput;
        private float simulatedAxis;
        private bool simulatedInteract;
        private bool simulatedJumpPressed;
        private bool simulatedJumpHeld;
        private int checkpointIndex;
        private int branchEntryBranchIndex = -1;
        private int lastSafeBranchIndex = -1;
        private Vector3 branchEntryPosition;
        private Vector3 lastSafePosition;
        private float groundedDuration;
        private static readonly RaycastHit[] SupportHits = new RaycastHit[8];

        public float CurrentScalar { get; private set; }
        public float PhysicalScalar => controller == null
            ? 0f
            : Vector3.Dot(transform.position - controller.RadioPosition, controller.ActiveDirection);
        public bool IsUsingSimulatedInput => useSimulatedInput;
        public bool LaneClampSuspended => action != null && action.LaneClampSuspended;
        public float MoveSpeed => action != null ? action.MoveSpeed : 4.5f;
        public float GroundOffset => groundOffset;
        public bool IsGrounded => action != null && action.IsGrounded;
        public float VerticalVelocity => action != null ? action.VerticalVelocity : 0f;
        public float JumpHeight => action != null ? action.JumpHeight : 2.25f;
        public float TimeToApex => action != null ? action.TimeToApex : 0.45f;
        public float Gravity => action != null ? action.Gravity : -22.222f;
        public float JumpLaunchSpeed => action != null ? action.JumpLaunchSpeed : 10f;
        public float CoyoteTime => action != null ? action.CoyoteTime : 0.14f;
        public float JumpBufferTime => action != null ? action.JumpBufferTime : 0.14f;
        public float AirControlMultiplier => action != null ? action.AirControlMultiplier : 0.65f;
        public int AcceptedJumpCount => action != null ? action.AcceptedJumpCount : 0;
        public int ExternalLaunchCount => action != null ? action.ExternalLaunchCount : 0;

        /// <summary>Hands an authored vertical launch to the movement module. See LaunchVertically.</summary>
        public void LaunchVertically(float speed)
        {
            if (action != null)
                action.LaunchVertically(speed);
        }

        /// <summary>Hands an authored air current to the movement module. See Phase01Draught.</summary>
        public void ApplyDraught(float alongSpeed, float rideHeight)
        {
            if (action != null)
                action.ApplyDraught(alongSpeed, rideHeight);
        }

        /// <summary>Signed speed along the active lane. An air current reads this to know which way the player is travelling.</summary>
        public float AlongLaneVelocity => action != null ? action.HorizontalVelocity : 0f;

        /// <summary>Physics steps and seconds spent being carried by an authored air current.</summary>
        public int DraughtFrameCount => action != null ? action.DraughtFrameCount : 0;
        public float DraughtSeconds => action != null ? action.DraughtSeconds : 0f;
        public bool IsDraughting => action != null && action.IsDraughting;
        public bool IsJumpHeld => action != null && action.IsJumpHeld;
        public int CheckpointIndex => checkpointIndex;
        public int SoftResetCount { get; private set; }
        public float SurfaceHeight => controller != null ? controller.ActiveSurfaceHeight(CurrentScalar) : 0f;
        public float FootSurfaceError => Mathf.Abs(transform.position.y - SurfaceHeight);
        public float ActiveBranchScreenSign
        {
            get
            {
                if (controller == null || viewCamera == null)
                    return 0f;
                float alignment = Vector3.Dot(controller.ActiveDirection, viewCamera.transform.right);
                return Mathf.Abs(alignment) > 0.5f ? Mathf.Sign(alignment) : 0f;
            }
        }

        public void Configure(Phase01DemoController demoController, Camera camera)
        {
            controller = demoController;
            viewCamera = camera;
        }

        private void Awake() => action = GetComponent<Phase01ParkourMotor>();

        /// <summary>
        /// Publishes the active branch's bars to the movement module. The block is published from the
        /// ordinary physics step rather than with a script execution order, because re-ordering this
        /// component would also re-order its Update and LateUpdate against the camera rig, and the camera
        /// choreography depends on that ordering. A one-step lag is harmless: the block stays set until it
        /// is cleared.
        /// A bar is only a wall while it covers the player's body, so once the player's feet are above the
        /// bar a jump has cleared it and the lane is free again. Contact alone is not enough: the player's
        /// velocity and position are rewritten every step, so a kinematic bar resolves as a shove along the
        /// lane or a squeeze off a narrow ledge instead of as something the player has to deal with.
        /// </summary>
        private void FixedUpdate()
        {
            if (controller == null || action == null)
                return;

            Phase01SweeperGate[] gates = ActiveBranchGates();
            if (gates == null)
            {
                action.SetLaneBlock(false, 0f, 0f);
                return;
            }

            float feet = transform.position.y - groundOffset;
            float head = feet + groundOffset * 2f;
            foreach (Phase01SweeperGate gate in gates)
            {
                if (gate == null)
                    continue;

                gate.GetBarExtent(out float inner, out float outer, out float bottom, out float top);
                if (bottom >= head || top <= feet)
                    continue;

                action.SetLaneBlock(
                    true,
                    inner - LaneBlockStandoff,
                    outer + LaneBlockStandoff);
                return;
            }

            action.SetLaneBlock(false, 0f, 0f);
        }

        /// <summary>Cached per branch, because the movement step must not allocate.</summary>
        private Phase01SweeperGate[] ActiveBranchGates()
        {
            if (controller.BranchTerrains == null
                || controller.ActiveBranchIndex < 0
                || controller.ActiveBranchIndex >= controller.BranchTerrains.Length)
                return null;

            Phase01BranchTerrain terrain = controller.BranchTerrains[controller.ActiveBranchIndex];
            if (terrain == null)
                return null;

            if (gatesBranchIndex != controller.ActiveBranchIndex || gatesBranchTerrain != terrain)
            {
                gatesBranchIndex = controller.ActiveBranchIndex;
                gatesBranchTerrain = terrain;
                gates = terrain.GetComponentsInChildren<Phase01SweeperGate>(true);
            }
            return gates;
        }

        private void Start()
        {
            SnapToActiveLane();
        }

        private void Update()
        {
            if (controller == null || action == null)
                return;

            ConfigureActiveAxis();
            bool acceptsInput = controller.CanAcceptInput;
            action.SetInputLocked(!acceptsInput);
            if (!acceptsInput)
                return;

            float screenAxis = useSimulatedInput ? simulatedAxis : ReadMoveAxis();
            bool heldJump = useSimulatedInput ? simulatedJumpHeld : ReadJumpHeld();
            bool pressedJump = useSimulatedInput ? simulatedJumpPressed : ReadJumpPressed();
            simulatedJumpPressed = false;
            action.SetExternalInput(screenAxis * ActiveBranchScreenSign, heldJump, pressedJump);

            UpdateScalarAndCheckpoints();
            controller.CheckForPickup();
            bool interact = useSimulatedInput ? simulatedInteract : ReadInteract();
            simulatedInteract = false;
            if (interact)
                controller.TryDeposit(CurrentScalar);
        }

        private void LateUpdate()
        {
            if (controller == null || action == null)
                return;

            UpdateScalarAndCheckpoints();
            if (controller.CanAcceptInput && transform.position.y < -3.5f)
                ResetToCheckpoint();
        }

        private void ConfigureActiveAxis()
        {
            action.SetMovementAxis(
                controller.ActiveDirection,
                controller.RadioPosition,
                controller.RadioStopDistance,
                controller.OuterDistance);
            action.SetGroundingValidityProbe(IsValidGroundingPosition);
        }

        private bool IsValidGroundingPosition(Vector3 worldPosition)
        {
            if (controller == null || !TryGetDownwardSupport(worldPosition, out _, out _))
                return false;

            if (controller.ActiveBranchIndex != 1)
                return true;

            Phase01Branch2RouteTransformation transformation = controller.BranchTerrains[1]
                .GetComponent<Phase01Branch2RouteTransformation>();
            if (transformation == null || !transformation.IsTransformed)
                return true;

            float scalar = Vector3.Dot(
                worldPosition - controller.RadioPosition,
                controller.ActiveDirection);
            return !transformation.IsReturnGap(scalar);
        }

        private void UpdateScalarAndCheckpoints()
        {
            Vector3 offset = transform.position - controller.RadioPosition;
            CurrentScalar = Mathf.Clamp(
                Vector3.Dot(offset, controller.ActiveDirection),
                controller.RadioStopDistance,
                controller.OuterDistance);

            if (branchEntryBranchIndex != controller.ActiveBranchIndex)
                CaptureBranchEntry();

            if (ShouldPreserveTransformedBranch2OuterCheckpoint())
            {
                groundedDuration = 0f;
                return;
            }

            if (!action.IsGrounded)
            {
                groundedDuration = 0f;
                return;
            }

            groundedDuration += Time.deltaTime;
            if (groundedDuration < 0.10f
                || !controller.CanAcceptInput
                || controller.State == Phase01DemoController.DemoState.Depositing
                || controller.State == Phase01DemoController.DemoState.Rotating
                || !IsSafeScalar(CurrentScalar - 0.45f)
                || !IsSafeScalar(CurrentScalar)
                || !IsSafeScalar(CurrentScalar + 0.45f)
                || CurrentScalar < controller.RadioStopDistance
                || CurrentScalar > controller.OuterDistance)
                return;

            lastSafePosition = controller.PointOnActiveLane(CurrentScalar, 0f);
            lastSafeBranchIndex = controller.ActiveBranchIndex;
            checkpointIndex++;
        }

        private bool ShouldPreserveTransformedBranch2OuterCheckpoint()
        {
            if (controller == null
                || controller.ActiveBranchIndex != 1
                || controller.CarriedParticle == null
                || controller.CarriedParticle.BranchIndex != 1
                || lastSafeBranchIndex != 1)
                return false;

            Phase01Branch2RouteTransformation transformation = controller.BranchTerrains[1]
                .GetComponent<Phase01Branch2RouteTransformation>();
            return transformation != null
                && transformation.IsTransformed
                && !controller.CanAcceptInput;
        }

        public bool CaptureCurrentRouteCheckpoint()
        {
            if (controller == null)
                return false;

            float scalar = Mathf.Clamp(
                PhysicalScalar,
                controller.RadioStopDistance,
                controller.OuterDistance);
            float lowerSample = Mathf.Clamp(
                scalar - 0.45f,
                controller.RadioStopDistance,
                controller.OuterDistance);
            float upperSample = Mathf.Clamp(
                scalar + 0.45f,
                controller.RadioStopDistance,
                controller.OuterDistance);
            if (!IsSafeScalar(lowerSample)
                || !IsSafeScalar(scalar)
                || !IsSafeScalar(upperSample))
                return false;

            CurrentScalar = scalar;
            lastSafePosition = controller.PointOnActiveLane(scalar, 0f);
            lastSafeBranchIndex = controller.ActiveBranchIndex;
            groundedDuration = Mathf.Max(groundedDuration, 0.10f);
            checkpointIndex++;
            return true;
        }

        public void ResetToCheckpoint()
        {
            SoftResetCount++;
            Vector3 checkpointPosition = lastSafeBranchIndex == controller.ActiveBranchIndex
                ? lastSafePosition
                : branchEntryPosition;
            CurrentScalar = Mathf.Clamp(
                Vector3.Dot(checkpointPosition - controller.RadioPosition, controller.ActiveDirection),
                controller.RadioStopDistance,
                controller.OuterDistance);
            // Rigidbody interpolation can otherwise expose the pre-fall Transform for one more
            // frame, causing an immediate duplicate reset and stale checkpoint reporting.
            transform.position = checkpointPosition;
            action.TeleportAndReset(checkpointPosition);
        }

        private void CaptureBranchEntry()
        {
            branchEntryBranchIndex = controller.ActiveBranchIndex;
            branchEntryPosition = transform.position;
            lastSafeBranchIndex = -1;
            groundedDuration = 0f;
            checkpointIndex = 0;
        }

        private bool IsSafeScalar(float scalar)
        {
            if (scalar < controller.RadioStopDistance || scalar > controller.OuterDistance)
                return false;

            if (controller.ActiveBranchIndex == 1)
            {
                Phase01Branch2RouteTransformation transformation = controller.BranchTerrains[1]
                    .GetComponent<Phase01Branch2RouteTransformation>();
                if (transformation != null && transformation.IsTransformed)
                    return !transformation.IsReturnGap(scalar);
            }

            return !controller.BranchTerrains[controller.ActiveBranchIndex].IsGapAt(scalar);
        }

        private bool TryGetDownwardSupport(Vector3 worldPosition, out Collider support, out Vector3 supportPoint)
        {
            support = null;
            supportPoint = default;
            Vector3 origin = worldPosition + Vector3.up * 0.08f;
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                SupportHits,
                0.22f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = SupportHits[i];
                Collider candidate = hit.collider;
                if (candidate == null
                    || candidate.transform.IsChildOf(transform)
                    || hit.normal.y < 0.55f
                    || hit.distance >= nearestDistance)
                    continue;
                nearestDistance = hit.distance;
                support = candidate;
                supportPoint = hit.point;
            }
            return support != null;
        }

        private static float ReadMoveAxis()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return 0f;
            return (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
        }

        private static bool ReadInteract() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        private static bool ReadJumpPressed() => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        private static bool ReadJumpHeld() => Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

        public void SnapToActiveLane()
        {
            Vector3 offset = transform.position - controller.RadioPosition;
            CurrentScalar = Mathf.Clamp(Vector3.Dot(offset, controller.ActiveDirection), controller.RadioStopDistance, controller.OuterDistance);
            ConfigureActiveAxis();
            action.TeleportAndReset(controller.PointOnActiveLane(CurrentScalar, 0f));
            if (branchEntryBranchIndex != controller.ActiveBranchIndex)
                CaptureBranchEntry();
        }

        public void SetPositionDuringTransition(Vector3 position)
        {
            ConfigureActiveAxis();
            action.TeleportAndReset(position);
        }

        /// <summary>
        /// Hands the player's position to the branch-rotation choreography, or gives it back. See
        /// Phase01ParkourMotor.SetLaneClampSuspended.
        /// </summary>
        public void SetLaneClampSuspended(bool suspended)
        {
            if (action != null)
                action.SetLaneClampSuspended(suspended);
        }

        public void SetSimulatedAxis(float axis)
        {
            useSimulatedInput = true;
            simulatedAxis = Mathf.Clamp(axis, -1f, 1f);
        }

        public float InputForScalarDirection(float scalarDirection) => Mathf.Sign(scalarDirection) * ActiveBranchScreenSign;

        public void SimulateInteract()
        {
            useSimulatedInput = true;
            simulatedInteract = true;
        }

        public void SimulateJumpPress(bool holdJump = true)
        {
            useSimulatedInput = true;
            simulatedJumpHeld = holdJump;
            simulatedJumpPressed = true;
        }

        /// <summary>
        /// Holds or releases the simulated jump button without queueing a jump. An authored air current only
        /// needs the hold, so this is how a ride can be proven to need no jump at all: the focused tests hold
        /// the button, ride a current from lip to lip, and assert that no jump was ever accepted.
        /// </summary>
        public void SetSimulatedJumpHeld(bool held)
        {
            useSimulatedInput = true;
            simulatedJumpHeld = held;
        }

        public void SimulateJumpRelease()
        {
            useSimulatedInput = true;
            simulatedJumpHeld = false;
        }

        public void ClearSimulatedInput()
        {
            simulatedAxis = 0f;
            simulatedInteract = false;
            simulatedJumpPressed = false;
            simulatedJumpHeld = false;
            useSimulatedInput = false;
        }
    }
}
