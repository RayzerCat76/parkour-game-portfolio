using System.Collections;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01CameraRig : MonoBehaviour
    {
        public enum CameraState
        {
            BranchFollow,
            RadioLockBlend,
            RadioLocked,
            DepositLocked,
            TurnOfVision,
            NextBranchRadioLocked,
            RadioUnlockBlend,
            DemoComplete
        }

        [Header("Explicit References")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Transform radio;
        [SerializeField] private Phase01DemoController controller;

        [Header("Grounded Branch Follow")]
        [SerializeField, Range(0f, 8f)] private float sideViewPitchDegrees = 5f;
        [SerializeField] private float groundedOrthographicSize = 4.2f;
        [SerializeField, Range(0.3f, 0.45f)] private float playerFeetViewportY = 0.38f;
        [SerializeField] private float followDamping = 0.18f;
        [SerializeField] private float lookAheadDistance = 2.2f;
        [SerializeField] private float lookAheadBlendDuration = 0.5f;
        [System.NonSerialized] private float supportHeightBlendDuration = 0.24f;
        [System.NonSerialized] private float airborneFollowFraction = 0.28f;
        [System.NonSerialized] private float maximumAirborneLift = 0.9f;

        [Header("Latched Radio Lock")]
        /// <summary>
        /// The radio lock exists to hold a stable framing around the receiver for a deposit, so it only
        /// needs to cover the approach to the radio. It used to reach 11 m / 14 m, which was invisible
        /// while every branch began with a long empty approach; once the branches became parkour from
        /// the first metre, the camera sat parked on the radio while the player fell and respawned
        /// inside that window ("the camera is stuck when I walk left"). 3 m / 4.5 m keeps the deposit
        /// framing and hands control back to branch follow before the first gap, so the opening reads
        /// normally: the player sees the gap, runs up, and jumps it with the camera already following.
        /// </summary>
        [SerializeField] private float radioLockEnterDistance = 3f;
        [SerializeField] private float radioLockExitDistance = 4.5f;
        [SerializeField, Range(0.06f, 0.12f)] private float radioLeftViewportX = 0.12f;
        [SerializeField, Range(0.88f, 0.94f)] private float radioRightViewportX = 0.88f;
        [SerializeField] private float radioLockBlendDuration = 0.75f;
        [SerializeField] private float radioUnlockBlendDuration = 0.65f;
        [SerializeField] private float stableLockPositionTolerance = 0.01f;

        [Header("Deposit And Turn Of Vision")]
        [SerializeField] private float depositOrthographicSize = 4.2f;
        [SerializeField] private float turnOrthographicSize = 4.2f;
        [SerializeField] private float completeOrthographicSize = 4.8f;
        [SerializeField] private float depositFocusDuration = 0.3f;
        [SerializeField] private float turnPreparationDuration = 0.25f;
        [SerializeField] private float turnOfVisionDuration = 0.9f;
        /// <summary>
        /// Yaw applied per deposit. It is +90 so the camera turns in the same rotational direction as
        /// the world/player orbit: the player then holds a fixed screen position while the cross turns
        /// around the radio, and outward travel reads in the same screen direction on every branch.
        /// The previous -90 turned the camera against the world, sweeping the player across the radio
        /// (screen X -1.0 -> 0.00 -> +1.0) and alternating the outward screen side branch by branch.
        /// </summary>
        [SerializeField] private float turnAngle = 90f;
        [SerializeField] private float nextBranchSettleDuration = 0.6f;

        private Vector3 followVelocity;
        private float sizeVelocity;
        private float lookAheadVelocity;
        private float currentLookAhead;
        private float lastTravelSign;
        private float estimatedTravelSpeed;
        private Vector3 previousPlayerPosition;
        private bool radioLocked;
        private bool transitionManaged;
        private bool lockSideLatched;
        private bool latchedRadioOnRight;
        private Vector3 blendStartPosition;
        private Vector3 blendTargetPosition;
        private float blendStartSize;
        private float blendTargetSize;
        private float blendElapsed;
        private float blendDuration;
        private Phase01PlayerMotor playerMotor;
        private float currentSupportHeight;
        private float supportHeightVelocity;
        private float airborneLift;
        private float airborneLiftVelocity;
        private bool hasValidSupportHeight;
        private bool wasPlayerSupported = true;
        private float airborneCameraFloorY;

        public float Yaw { get; private set; }
        public float RotationDuration => turnOfVisionDuration;
        public float TurnAngle => turnAngle;
        public float CurrentLookAhead => currentLookAhead;
        public float CloseOrthographicSize => groundedOrthographicSize;
        public float TurnOrthographicSize => turnOrthographicSize;
        public float RadioLockEnterDistance => radioLockEnterDistance;
        public float RadioLockExitDistance => radioLockExitDistance;
        public float RadioLockBlendDuration => radioLockBlendDuration;
        public float RadioUnlockBlendDuration => radioUnlockBlendDuration;
        public float PlayerFeetViewportY => playerFeetViewportY;
        public float SideViewPitchDegrees => sideViewPitchDegrees;
        public float StableLockPositionTolerance => stableLockPositionTolerance;
        public float RadioViewportXTarget { get; private set; }
        public CameraState State { get; private set; } = CameraState.BranchFollow;
        public bool IsRadioLocked => radioLocked;
        public bool IsStableRadioLock => radioLocked
            && !transitionManaged
            && (State == CameraState.RadioLocked || State == CameraState.NextBranchRadioLocked)
            && IsFramingStable;
        public bool IsFramingStable { get; private set; }
        public Vector3 StableLockCameraPosition { get; private set; }
        public Camera ViewCamera => viewCamera;
        public string LatchedLockSide => !lockSideLatched ? "None" : latchedRadioOnRight ? "Right" : "Left";
        public int LockSideLatchCount { get; private set; }
        public int LockTargetCalculationCount { get; private set; }
        public int UnlockBlendStartCount { get; private set; }
        public Vector3 TransitionStartPosition => blendStartPosition;
        public Vector3 TransitionTargetPosition => blendTargetPosition;
        public float TransitionProgress => blendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / blendDuration);
        public float LastLockCompletedPlayerScalar { get; private set; }
        public float LastLockStableRealtime { get; private set; }
        public float CurrentSupportHeight => currentSupportHeight;
        public float CurrentAirborneLift => airborneLift;
        public int RejectedVoidHeightCount { get; private set; }
        public float AirborneCameraFloorY => airborneCameraFloorY;

        public void Configure(
            Camera camera,
            Transform playerTarget,
            Transform radioTarget,
            Phase01DemoController demoController)
        {
            viewCamera = camera;
            player = playerTarget;
            radio = radioTarget;
            controller = demoController;
            playerMotor = playerTarget != null ? playerTarget.GetComponent<Phase01PlayerMotor>() : null;
        }

        private void Start()
        {
            ApplySideViewPitch();
            SnapToBranchFollow();
        }

        private void OnValidate()
        {
            radioLockExitDistance = Mathf.Max(radioLockExitDistance, radioLockEnterDistance + 0.1f);
            radioLockBlendDuration = Mathf.Max(0.05f, radioLockBlendDuration);
            radioUnlockBlendDuration = Mathf.Max(0.05f, radioUnlockBlendDuration);
            stableLockPositionTolerance = Mathf.Max(0.001f, stableLockPositionTolerance);
            supportHeightBlendDuration = Mathf.Max(0.05f, supportHeightBlendDuration);
            maximumAirborneLift = Mathf.Max(0f, maximumAirborneLift);
            ApplySideViewPitch();
        }

        private void ApplySideViewPitch()
        {
            if (viewCamera != null)
                viewCamera.transform.localRotation = Quaternion.Euler(sideViewPitchDegrees, 0f, 0f);
        }

        private void LateUpdate()
        {
            if (transitionManaged || !HasReferences())
                return;

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 activeDirection = controller.ActiveDirection;
            Vector3 frameDelta = player.position - previousPlayerPosition;
            float travel = Vector3.Dot(frameDelta, activeDirection);
            if (Mathf.Abs(travel) > 0.0001f)
            {
                lastTravelSign = Mathf.Sign(travel);
                estimatedTravelSpeed = Mathf.Abs(travel) / deltaTime;
            }

            float scalar = ActiveScalar(activeDirection);
            switch (State)
            {
                case CameraState.BranchFollow:
                    UpdateBranchFollow(activeDirection, scalar, deltaTime);
                    if (scalar <= radioLockEnterDistance)
                        BeginRadioLockBlend();
                    break;

                case CameraState.RadioLockBlend:
                    AdvanceFixedBlend(deltaTime, CameraState.RadioLocked);
                    break;

                case CameraState.RadioLocked:
                case CameraState.NextBranchRadioLocked:
                    HoldExactRadioLock();
                    if (scalar > radioLockExitDistance)
                        BeginRadioUnlockBlend(activeDirection, scalar);
                    break;

                case CameraState.RadioUnlockBlend:
                    AdvanceFixedBlend(deltaTime, CameraState.BranchFollow);
                    break;
            }

            previousPlayerPosition = player.position;
        }

        public void SetYaw(float yaw)
        {
            Yaw = yaw;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (transitionManaged && State == CameraState.TurnOfVision)
                transform.position = GroundedPoint(radio.position, viewCamera.orthographicSize, RadioGroundHeight());
        }

        public IEnumerator FocusDeposit()
        {
            transitionManaged = true;
            radioLocked = true;
            State = CameraState.DepositLocked;
            ResetFollowMotion();
            Vector3 target = CalculateLatchedRadioLockPoint(depositOrthographicSize, false);
            yield return TweenFixedFrame(target, depositOrthographicSize, depositFocusDuration);
        }

        public IEnumerator RevealCross()
        {
            State = CameraState.DepositLocked;
            yield return TweenFixedFrame(
                GroundedPoint(radio.position, turnOrthographicSize, RadioGroundHeight()),
                turnOrthographicSize,
                turnPreparationDuration);
        }

        public void BeginRotation()
        {
            State = CameraState.TurnOfVision;
        }

        public IEnumerator SettleToNextBranch(Vector3 outwardDirection)
        {
            State = CameraState.NextBranchRadioLocked;
            radioLocked = true;
            LatchLockSide();
            Vector3 target = CalculateLatchedRadioLockPoint(groundedOrthographicSize, true);
            yield return TweenFixedFrame(target, groundedOrthographicSize, nextBranchSettleDuration);

            ResetFollowMotion();
            previousPlayerPosition = player.position;
            transitionManaged = false;
            blendTargetPosition = target;
            blendTargetSize = groundedOrthographicSize;
            CompleteStableLock(CameraState.NextBranchRadioLocked);
        }

        public IEnumerator SettleDemoComplete()
        {
            State = CameraState.DemoComplete;
            radioLocked = true;
            yield return TweenFixedFrame(
                GroundedPoint(radio.position, completeOrthographicSize, RadioGroundHeight()),
                completeOrthographicSize,
                nextBranchSettleDuration);
            IsFramingStable = true;
        }

        private void SnapToBranchFollow()
        {
            if (!HasReferences())
                return;

            transitionManaged = false;
            radioLocked = false;
            lockSideLatched = false;
            ResetFollowMotion();
            previousPlayerPosition = player.position;
            float scalar = ActiveScalar(controller.ActiveDirection);
            SetImmediateSupportHeight(SafeInitialSurfaceHeight(scalar));
            transform.position = GroundedPoint(
                player.position,
                groundedOrthographicSize,
                currentSupportHeight);
            viewCamera.orthographicSize = groundedOrthographicSize;
            IsFramingStable = true;
            State = CameraState.BranchFollow;
        }

        private void UpdateBranchFollow(Vector3 activeDirection, float scalar, float deltaTime)
        {
            float lookAheadTarget = lastTravelSign * lookAheadDistance;
            currentLookAhead = Mathf.SmoothDamp(
                currentLookAhead,
                lookAheadTarget,
                ref lookAheadVelocity,
                lookAheadBlendDuration,
                Mathf.Infinity,
                deltaTime);

            float sampledScalar = Mathf.Clamp(
                scalar + currentLookAhead,
                controller.RadioStopDistance,
                controller.OuterDistance);
            Vector3 followPoint = radio.position + activeDirection * sampledScalar;
            float appliedSupportHeight = ResolveBranchFollowSupportHeight(scalar, deltaTime);
            bool playerSupported = IsPlayerSupported();
            if (wasPlayerSupported && !playerSupported)
            {
                airborneCameraFloorY = transform.position.y;
                if (followVelocity.y < 0f)
                    followVelocity.y = 0f;
            }
            Vector3 target = GroundedPoint(
                followPoint,
                groundedOrthographicSize,
                appliedSupportHeight);
            if (!playerSupported)
                target.y = Mathf.Max(target.y, airborneCameraFloorY);
            transform.position = Vector3.SmoothDamp(
                transform.position,
                target,
                ref followVelocity,
                followDamping,
                Mathf.Infinity,
                deltaTime);
            if (!playerSupported && transform.position.y < airborneCameraFloorY)
            {
                Vector3 clampedPosition = transform.position;
                clampedPosition.y = airborneCameraFloorY;
                transform.position = clampedPosition;
                if (followVelocity.y < 0f)
                    followVelocity.y = 0f;
            }
            wasPlayerSupported = playerSupported;
            viewCamera.orthographicSize = Mathf.SmoothDamp(
                viewCamera.orthographicSize,
                groundedOrthographicSize,
                ref sizeVelocity,
                followDamping,
                Mathf.Infinity,
                deltaTime);
            IsFramingStable = Vector3.Distance(transform.position, target) <= stableLockPositionTolerance
                && Mathf.Abs(viewCamera.orthographicSize - groundedOrthographicSize) < 0.02f;
        }

        private void BeginRadioLockBlend()
        {
            radioLocked = true;
            LatchLockSide();
            ResetFollowMotion();
            CaptureFixedBlend(
                CalculateLatchedRadioLockPoint(groundedOrthographicSize, true),
                groundedOrthographicSize,
                radioLockBlendDuration);
            State = CameraState.RadioLockBlend;
            IsFramingStable = false;
        }

        private void BeginRadioUnlockBlend(Vector3 activeDirection, float scalar)
        {
            radioLocked = false;
            ResetFollowMotion();
            currentLookAhead = lookAheadDistance;
            lastTravelSign = 1f;
            float predictedScalar = Mathf.Clamp(
                scalar + estimatedTravelSpeed * radioUnlockBlendDuration + lookAheadDistance,
                controller.RadioStopDistance,
                controller.OuterDistance);
            Vector3 followPoint = radio.position + activeDirection * predictedScalar;
            Vector3 target = GroundedPoint(
                followPoint,
                groundedOrthographicSize,
                SafeActiveSurfaceHeight(scalar));
            CaptureFixedBlend(target, groundedOrthographicSize, radioUnlockBlendDuration);
            UnlockBlendStartCount++;
            State = CameraState.RadioUnlockBlend;
            IsFramingStable = false;
        }

        private void CaptureFixedBlend(Vector3 targetPosition, float targetSize, float duration)
        {
            blendStartPosition = transform.position;
            blendTargetPosition = targetPosition;
            blendStartSize = viewCamera.orthographicSize;
            blendTargetSize = targetSize;
            blendElapsed = 0f;
            blendDuration = Mathf.Max(0.0001f, duration);
        }

        private void AdvanceFixedBlend(float deltaTime, CameraState completedState)
        {
            blendElapsed = Mathf.Min(blendElapsed + deltaTime, blendDuration);
            float t = Mathf.SmoothStep(0f, 1f, blendElapsed / blendDuration);
            transform.position = Vector3.LerpUnclamped(blendStartPosition, blendTargetPosition, t);
            viewCamera.orthographicSize = Mathf.LerpUnclamped(blendStartSize, blendTargetSize, t);
            IsFramingStable = false;

            if (blendElapsed < blendDuration)
                return;

            transform.position = blendTargetPosition;
            viewCamera.orthographicSize = blendTargetSize;
            if (completedState == CameraState.RadioLocked)
                CompleteStableLock(CameraState.RadioLocked);
            else
            {
                State = CameraState.BranchFollow;
                IsFramingStable = true;
                lockSideLatched = false;
                previousPlayerPosition = player.position;
                followVelocity = Vector3.zero;
                sizeVelocity = 0f;
                lookAheadVelocity = 0f;
            }
        }

        private void CompleteStableLock(CameraState lockedState)
        {
            State = lockedState;
            radioLocked = true;
            transform.position = blendTargetPosition;
            viewCamera.orthographicSize = groundedOrthographicSize;
            StableLockCameraPosition = transform.position;
            LastLockCompletedPlayerScalar = ActiveScalar(controller.ActiveDirection);
            LastLockStableRealtime = Time.realtimeSinceStartup;
            IsFramingStable = true;
            ResetFollowMotion();
        }

        private void HoldExactRadioLock()
        {
            transform.position = StableLockCameraPosition;
            viewCamera.orthographicSize = groundedOrthographicSize;
            IsFramingStable = true;
        }

        private void LatchLockSide()
        {
            float screenDirection = Mathf.Sign(Vector3.Dot(controller.ActiveDirection, viewCamera.transform.right));
            latchedRadioOnRight = screenDirection < 0f;
            lockSideLatched = true;
            RadioViewportXTarget = latchedRadioOnRight ? radioRightViewportX : radioLeftViewportX;
            LockSideLatchCount++;
        }

        private Vector3 CalculateLatchedRadioLockPoint(float orthographicSize, bool countCalculation)
        {
            if (!lockSideLatched)
                LatchLockSide();
            if (countCalculation)
                LockTargetCalculationCount++;

            float viewportWidth = 2f * orthographicSize * viewCamera.aspect;
            Vector3 point = radio.position
                - viewCamera.transform.right * ((RadioViewportXTarget - 0.5f) * viewportWidth);
            return GroundedPoint(point, orthographicSize, RadioGroundHeight());
        }

        private float ActiveScalar(Vector3 activeDirection)
        {
            return Mathf.Max(0f, Vector3.Dot(player.position - radio.position, activeDirection));
        }

        private float RadioGroundHeight()
        {
            return SafeActiveSurfaceHeight(controller.RadioStopDistance);
        }

        private float ResolveBranchFollowSupportHeight(float playerScalar, float deltaTime)
        {
            if (playerMotor == null && player != null)
                playerMotor = player.GetComponent<Phase01PlayerMotor>();

            float sampledHeight = controller.ActiveSurfaceHeight(playerScalar);
            bool validHeight = IsValidSurfaceHeight(sampledHeight);
            bool grounded = IsPlayerSupported();
            if (!validHeight)
                RejectedVoidHeightCount++;
            else if (grounded || !hasValidSupportHeight)
            {
                if (!hasValidSupportHeight)
                    SetImmediateSupportHeight(sampledHeight);
                else
                    currentSupportHeight = Mathf.SmoothDamp(
                        currentSupportHeight,
                        sampledHeight,
                        ref supportHeightVelocity,
                        supportHeightBlendDuration,
                        Mathf.Infinity,
                        deltaTime);
            }

            float liftTarget = 0f;
            if (!grounded && hasValidSupportHeight)
            {
                float playerRise = Mathf.Max(0f, player.position.y - currentSupportHeight);
                liftTarget = Mathf.Min(maximumAirborneLift, playerRise * airborneFollowFraction);
            }
            airborneLift = Mathf.SmoothDamp(
                airborneLift,
                liftTarget,
                ref airborneLiftVelocity,
                grounded ? supportHeightBlendDuration : 0.12f,
                Mathf.Infinity,
                deltaTime);
            return currentSupportHeight + airborneLift;
        }

        // Gap terrain deliberately samples below the reset volume; it is never camera ground.
        private float SafeActiveSurfaceHeight(float scalar)
        {
            float sampledHeight = controller.ActiveSurfaceHeight(scalar);
            if (IsValidSurfaceHeight(sampledHeight))
                return sampledHeight;
            return hasValidSupportHeight ? currentSupportHeight : 0f;
        }

        private float SafeInitialSurfaceHeight(float scalar)
        {
            float sampledHeight = controller.ActiveSurfaceHeight(scalar);
            return IsValidSurfaceHeight(sampledHeight) ? sampledHeight : 0f;
        }

        private void SetImmediateSupportHeight(float height)
        {
            currentSupportHeight = height;
            supportHeightVelocity = 0f;
            airborneLift = 0f;
            airborneLiftVelocity = 0f;
            hasValidSupportHeight = true;
        }

        private static bool IsValidSurfaceHeight(float height) => height > -3f;

        private bool IsPlayerSupported()
        {
            return playerMotor == null
                || (playerMotor.IsGrounded && playerMotor.VerticalVelocity <= 0.05f);
        }

        private Vector3 GroundedPoint(Vector3 horizontalPoint, float orthographicSize, float groundHeight)
        {
            Vector3 originalPosition = transform.position;
            float originalSize = viewCamera.orthographicSize;
            Vector3 samplePosition = horizontalPoint;
            samplePosition.y = 0f;
            transform.position = samplePosition;
            viewCamera.orthographicSize = orthographicSize;

            Vector3 groundPoint = radio.position;
            groundPoint.y = groundHeight;
            float viewportYAtZero = viewCamera.WorldToViewportPoint(groundPoint).y;
            float verticalProjection = Mathf.Max(0.1f, Vector3.Dot(viewCamera.transform.up, Vector3.up));
            samplePosition.y = (viewportYAtZero - playerFeetViewportY)
                * (2f * orthographicSize)
                / verticalProjection;

            transform.position = originalPosition;
            viewCamera.orthographicSize = originalSize;
            return samplePosition;
        }

        private IEnumerator TweenFixedFrame(Vector3 targetPosition, float targetSize, float duration)
        {
            Vector3 startPosition = transform.position;
            float startSize = viewCamera.orthographicSize;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                viewCamera.orthographicSize = Mathf.LerpUnclamped(startSize, targetSize, t);
                yield return null;
            }

            transform.position = targetPosition;
            viewCamera.orthographicSize = targetSize;
        }

        private void ResetFollowMotion()
        {
            followVelocity = Vector3.zero;
            sizeVelocity = 0f;
            lookAheadVelocity = 0f;
            currentLookAhead = 0f;
            airborneLift = 0f;
            airborneLiftVelocity = 0f;
            wasPlayerSupported = IsPlayerSupported();
            airborneCameraFloorY = transform.position.y;
        }

        private bool HasReferences()
        {
            return viewCamera != null && player != null && radio != null && controller != null;
        }
    }
}
