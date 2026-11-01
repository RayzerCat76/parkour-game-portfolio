#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests.ReviewCapture
{
    public sealed class Phase01ReviewCaptureTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const int FrameRate = 30;
        // Encode at game-time cadence so slow Editor updates do not create slow-motion proof footage.

        private Phase01DemoController controller;
        private Phase01PlayerMotor player;
        private Phase01CameraRig cameraRig;
        private Phase01CentralSupport support;
        private Phase01PerformanceMonitor performanceMonitor;
        private Camera captureCamera;
        private Renderer playerRenderer;
        private RenderTexture renderTexture;
        private Texture2D readback;
        private MediaEncoder encoder;
        private GameObject overlayRoot;
        private GameObject slateBackdrop;
        private TextMesh slateText;
        private TextMesh shotText;
        private TextMesh statusText;
        private TextMesh footerText;
        private Material backdropMaterial;
        private string outputPath;
        private string inspectionFolder;
        private int frameCount;
        private float nextCaptureGameTime;
        private float maximumVisualFootError;
        private float maximumProfileError;
        private float maximumVerticalStep;
        private float maximumStableLockDrift;
        private float maximumReleaseStep;
        private float previousPlayerY;
        private bool hasPreviousPlayerY;

        [UnityTest]
        [Category("ReviewCapture")]
        public IEnumerator CaptureSupportedRadioAndStrictEdgeLockPass()
        {
            string captureRequest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewCaptures", "TASK_P3_CAPTURE_REQUEST"));
            bool requested = Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-phase01Fix5ReviewCapture")
                || File.Exists(captureRequest);
            if (!requested)
                Assert.Ignore("Explicit capture flag required; review capture automation is disabled by default.");
            if (File.Exists(captureRequest))
                File.Delete(captureRequest);

            EditorSceneManager.OpenScene("Assets/Scenes/Phase01_Demo0.unity", OpenSceneMode.Single);
            yield return new EnterPlayMode();
            yield return null;

            controller = UnityEngine.Object.FindAnyObjectByType<Phase01DemoController>();
            player = UnityEngine.Object.FindAnyObjectByType<Phase01PlayerMotor>();
            cameraRig = UnityEngine.Object.FindAnyObjectByType<Phase01CameraRig>();
            captureCamera = Camera.main;
            playerRenderer = player != null ? player.GetComponentInChildren<Renderer>() : null;
            support = controller != null ? controller.CentralSupport : null;
            performanceMonitor = UnityEngine.Object.FindAnyObjectByType<Phase01PerformanceMonitor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(captureCamera, Is.Not.Null);
            Assert.That(playerRenderer, Is.Not.Null);
            Assert.That(support, Is.Not.Null);
            Assert.That(controller.HasValidReferences, Is.True);

            Phase01AcceptanceDriver driver = UnityEngine.Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            Assert.That(driver, Is.Not.Null);
            Coroutine prelude = driver.StartCoroutine(driver.Run());
            float preludeDeadline = Time.realtimeSinceStartup + 12f;
            while ((controller.ActiveBranchIndex != 1 ||
                    !cameraRig.IsStableRadioLock ||
                    controller.IsInputLocked ||
                    controller.State != Phase01DemoController.DemoState.Idle) &&
                   Time.realtimeSinceStartup < preludeDeadline)
                yield return null;
            driver.StopCoroutine(prelude);
            player.ClearSimulatedInput();
            Assert.That(controller.ActiveBranchIndex, Is.EqualTo(1), "Branch 1 gameplay prelude did not reach Branch 2.");
            Assert.That(controller.ReceiverCharge, Is.EqualTo(1));
            Assert.That(cameraRig.IsStableRadioLock, Is.True);
            Assert.That(controller.IsInputLocked, Is.False);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string captureRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewCaptures"));
            Directory.CreateDirectory(captureRoot);
            outputPath = Path.Combine(captureRoot, $"TASK_P3_GAP_RADIO_FIX_{stamp}.mp4");
            inspectionFolder = Path.Combine(captureRoot, $"TASK_P3_GAP_RADIO_FIX_{stamp}_inspection_frames");
            Directory.CreateDirectory(inspectionFolder);

            SetupCapture();
            SetupOverlay();

            SetSlateMode(true);
            slateText.text = $"TASK_P3_GAP_RADIO_FIX — SHORT PROOF\n\nUnity {Application.unityVersion}\nREAL-TIME Unity Media capture (normal Time.deltaTime)\nNo Time.captureFramerate\nBranch 1 completed through normal gameplay before recording\nCapture: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}";
            yield return Hold(5f, "Branch 2 collapse + camera proof", "00_title");

            SetSlateMode(false);
            yield return MoveInsideStableLock(
                cameraRig.RadioLockExitDistance - 0.3f,
                "01_branch2_fixed_lock",
                "Branch 2 — leave the fixed radio frame");
            yield return CrossLockExit("02_branch2_follow", "Branch 2 — smooth release to BranchFollow");
            yield return CaptureMoveWithRealJumps(
                () => controller.CarriedParticle != null,
                1f,
                new[] { 8.9f, 17.7f },
                10f,
                "Branch 2 OUTWARD — two real gaps and raised shelf",
                false);

            Phase01Branch2RouteTransformation route = controller.BranchTerrains[1]
                .GetComponent<Phase01Branch2RouteTransformation>();
            Assert.That(route, Is.Not.Null);
            bool savedCollapseMid = false;
            bool savedDebrisFall = false;
            while (!route.IsTransformed)
            {
                shotText.text = $"PICKUP COLLAPSE — locked / {route.AnimationProgress:P0}";
                Assert.That(controller.IsInputLocked, Is.True);
                CaptureFrame();
                if (!savedCollapseMid && route.AnimationProgress >= 0.35f)
                {
                    SaveInspectionFrame("03_return_pieces_settling");
                    savedCollapseMid = true;
                }
                if (!savedDebrisFall && route.AnimationProgress >= 0.62f)
                {
                    SaveInspectionFrame("04_gap_debris_falling");
                    savedDebrisFall = true;
                }
                yield return null;
            }
            Assert.That(savedCollapseMid && savedDebrisFall, Is.True);
            Assert.That(route.UpperRouteVisible, Is.False);
            Assert.That(route.FallingPiecesVisible, Is.False);
            Assert.That(route.LowerCollisionReady, Is.True);
            Assert.That(route.FallingPieceCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(route.LastCollapseDuration, Is.InRange(1f, 1.6f));
            yield return Hold(6f, "FINAL LOWER ROUTE ONLY — upper route and debris removed", "05_final_lower_route_only");

            int resetsBefore = player.SoftResetCount;
            player.SetSimulatedAxis(AxisForScalar(-1f));
            float fallDeadline = Time.time + 7f;
            while (player.SoftResetCount == resetsBefore && Time.time < fallDeadline)
            {
                shotText.text = "INTENTIONAL FALL — no jump into the return gap";
                CaptureFrame();
                if (player.transform.position.y < -1.5f)
                    SaveInspectionFrame("06_intentional_fall");
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore + 1));
            Assert.That(controller.CarriedParticle, Is.Not.Null);
            Assert.That(route.IsTransformed, Is.True);
            Assert.That(route.ActivationCount, Is.EqualTo(1));
            yield return Hold(5f, "RESET PRESERVES — carried particle + completed collapse", "07_reset_preserved");

            yield return CaptureMoveWithRealJumps(
                () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                -1f,
                new[] { 16.8f },
                10f,
                "LOWER RETURN — real gap jump / void-safe BranchFollow",
                true);
            yield return Hold(5f, "STABLE RADIO LOCK — return complete", "09_radio_lock");
            yield return CaptureDepositTransition(2, "10_branch2_deposit", null);
            Assert.That(controller.ReceiverCharge, Is.EqualTo(2));

            SetSlateMode(true);
            slateText.text = $"TASK_P3_GAP_RADIO_FIX PROOF COMPLETE\n\nVisible return gap: real fall\nParticle + transformed route reset: PASS\nReturn jump clears the hole: PASS\nRadio hard stop + deposit: {controller.ReceiverCharge} / 4\n\nPlaceholder visuals and silence";
            yield return Hold(5f, "End slate", "99_end_slate");

            player.ClearSimulatedInput();
            FinishCapture();

            float duration = frameCount / (float)FrameRate;
            Assert.That(duration, Is.InRange(30f, 60f), $"Capture duration was {duration:F2}s.");
            Assert.That(maximumVerticalStep, Is.LessThan(0.4f));
            Assert.That(maximumStableLockDrift, Is.LessThanOrEqualTo(cameraRig.StableLockPositionTolerance + 0.001f));
            Assert.That(maximumReleaseStep, Is.LessThan(0.4f));
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(1024L));
            Assert.That(Directory.GetFiles(inspectionFolder, "*.png").Length, Is.GreaterThanOrEqualTo(9));
            Debug.Log(
                $"[Phase01Branch2CollapseCameraCapture] PASS path={outputPath} frames={frameCount} fps={FrameRate} "
                + $"duration={duration:F2}s stableDriftMax={maximumStableLockDrift:F5} "
                + $"releaseStepMax={maximumReleaseStep:F4} inspection={inspectionFolder}");
            yield return new ExitPlayMode();
        }

        private IEnumerator CaptureAutomatedAcceptanceLoop()
        {
            Phase01AcceptanceDriver driver = UnityEngine.Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            Assert.That(driver, Is.Not.Null);
            int resetStart = player.SoftResetCount;
            bool[] saved = new bool[8];
            driver.StartCoroutine(driver.Run());
            while (!driver.Finished)
            {
                Phase01Branch2RouteTransformation route = controller.BranchTerrains != null
                    && controller.BranchTerrains.Length > 1
                    ? controller.BranchTerrains[1].GetComponent<Phase01Branch2RouteTransformation>()
                    : null;
                shotText.text = controller.ActiveBranchIndex == 1
                    ? controller.CarriedParticle == null
                        ? "Branch 2 OUTWARD — real Gap A, raised centre shelf, real Gap B"
                        : player.SoftResetCount == resetStart
                            ? "Branch 2 TRANSFORMED — intentional fall and particle-preserving reset"
                            : "Branch 2 LOWER RETURN — step-down, real gap, ramp, flat radio approach"
                    : $"Branch {controller.ActiveBranchNumber} regression — charge {controller.ReceiverCharge} / 4";

                CaptureFrame();
                if (!saved[0]) { SaveInspectionFrame("02_branch1_live"); saved[0] = true; }
                if (!saved[1] && controller.ActiveBranchIndex == 1 && controller.CarriedParticle == null)
                { SaveInspectionFrame("03_branch2_outward"); saved[1] = true; }
                if (!saved[2] && controller.ActiveBranchIndex == 1 && player.CurrentScalar >= 14f && controller.CarriedParticle == null)
                { SaveInspectionFrame("04_branch2_raised_shelf"); saved[2] = true; }
                if (!saved[3] && route != null && route.IsTransformed)
                { SaveInspectionFrame("05_branch2_transformed"); saved[3] = true; }
                if (!saved[4] && controller.ActiveBranchIndex == 1 && player.SoftResetCount > resetStart)
                { SaveInspectionFrame("06_branch2_reset_preserved"); saved[4] = true; }
                if (!saved[5] && controller.ActiveBranchIndex == 1 && controller.CarriedParticle != null && player.CurrentScalar < 20f)
                { SaveInspectionFrame("07_branch2_lower_return"); saved[5] = true; }
                if (!saved[6] && controller.ActiveBranchIndex == 2)
                { SaveInspectionFrame("08_branch3_regression"); saved[6] = true; }
                if (!saved[7] && controller.ActiveBranchIndex == 3)
                { SaveInspectionFrame("09_branch4_regression"); saved[7] = true; }
                yield return null;
            }
            Assert.That(driver.Passed, Is.True, driver.Report);
        }

        private IEnumerator CaptureStandardBranch(int branchNumber)
        {
            Assert.That(cameraRig.IsStableRadioLock, Is.True);
            yield return MoveInsideStableLock(
                cameraRig.RadioLockExitDistance - 0.3f,
                null,
                $"Branch {branchNumber} — player travels inside fixed edge lock");
            yield return CrossLockExit(null, $"Branch {branchNumber} — smooth release to follow");
            yield return MoveUntil(
                () => controller.CarriedParticle != null,
                AxisForScalar(1f),
                4f,
                $"Branch {branchNumber} — long follow reaches particle");
            yield return MoveUntil(
                () => player.CurrentScalar >= controller.OuterDistance - 0.03f,
                AxisForScalar(1f),
                4f,
                $"Branch {branchNumber} — long endpoint");
            yield return Hold(1f, $"Branch {branchNumber} — endpoint retained");
            yield return MoveToRadio(branchNumber);
            yield return Hold(3f, $"Branch {branchNumber} — supported radio in stable mirrored lock");
            yield return CaptureDepositTransition(branchNumber, null, null);
        }

        private IEnumerator CaptureMoveWithRealJumps(
            Func<bool> predicate,
            float scalarDirection,
            float[] takeoffScalars,
            float timeoutSeconds,
            string shot,
            bool proveVoidSafeCamera)
        {
            float axis = AxisForScalar(scalarDirection);
            int nextJump = 0;
            bool holdingJump = false;
            float releaseAt = -1f;
            float deadline = Time.time + timeoutSeconds;
            int rejectedBefore = cameraRig.RejectedVoidHeightCount;
            float takeoffCameraY = 0f;
            float minimumAirborneCameraY = float.MaxValue;
            float minimumViewportY = float.MaxValue;
            float maximumViewportY = float.MinValue;
            bool sawAirborne = false;
            bool savedMidJump = false;
            bool capturedAirborneFloor = false;
            int airborneFrameCount = 0;
            bool completedTrackedJump = false;

            shotText.text = shot;
            player.SetSimulatedAxis(axis);
            while (!predicate() && Time.time < deadline)
            {
                player.SetSimulatedAxis(axis);
                if (holdingJump && Time.time >= releaseAt)
                {
                    player.SimulateJumpRelease();
                    holdingJump = false;
                }
                if (!holdingJump && nextJump < takeoffScalars.Length && player.IsGrounded)
                {
                    bool reachedTakeoff = scalarDirection > 0f
                        ? player.CurrentScalar >= takeoffScalars[nextJump]
                        : player.CurrentScalar <= takeoffScalars[nextJump];
                    if (reachedTakeoff)
                    {
                        player.SimulateJumpPress(true);
                        holdingJump = true;
                        releaseAt = Time.time + 0.5f;
                        nextJump++;
                    }
                }

                CaptureFrame();
                yield return null;
                if (proveVoidSafeCamera && nextJump > 0 && sawAirborne && player.IsGrounded)
                    completedTrackedJump = true;
                if (proveVoidSafeCamera && nextJump > 0 && !completedTrackedJump && !player.IsGrounded)
                {
                    sawAirborne = true;
                    airborneFrameCount++;
                    if (!capturedAirborneFloor && airborneFrameCount >= 2)
                    {
                        takeoffCameraY = cameraRig.AirborneCameraFloorY;
                        capturedAirborneFloor = true;
                    }
                    if (capturedAirborneFloor)
                        minimumAirborneCameraY = Mathf.Min(minimumAirborneCameraY, cameraRig.transform.position.y);
                    float viewportY = captureCamera.WorldToViewportPoint(
                        player.transform.position + Vector3.up * player.GroundOffset).y;
                    minimumViewportY = Mathf.Min(minimumViewportY, viewportY);
                    maximumViewportY = Mathf.Max(maximumViewportY, viewportY);
                    if (!savedMidJump && player.VerticalVelocity <= 0f)
                    {
                        SaveInspectionFrame("08_return_gap_camera_arc");
                        savedMidJump = true;
                    }
                }
            }

            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Assert.That(predicate(), Is.True, $"Timed out: {shot}");
            Assert.That(nextJump, Is.EqualTo(takeoffScalars.Length), "Every required jump must use real input.");
            if (proveVoidSafeCamera)
            {
                Assert.That(sawAirborne && savedMidJump, Is.True);
                Assert.That(cameraRig.RejectedVoidHeightCount, Is.GreaterThan(rejectedBefore));
                Assert.That(capturedAirborneFloor, Is.True);
                Assert.That(minimumAirborneCameraY, Is.GreaterThanOrEqualTo(takeoffCameraY - 0.02f));
                Assert.That(minimumViewportY, Is.GreaterThanOrEqualTo(0.04f));
                Assert.That(maximumViewportY, Is.LessThanOrEqualTo(0.98f));
            }
        }

        private float AxisForScalar(float scalarDirection)
        {
            return player.InputForScalarDirection(scalarDirection);
        }

        private IEnumerator CaptureScreenInput(int branchNumber, float screenAxis, string shot)
        {
            Vector3 beforeWorld = player.transform.position;
            float beforeViewportX = captureCamera.WorldToViewportPoint(beforeWorld).x;
            shotText.text = $"Branch {branchNumber} — {shot}";
            player.SetSimulatedAxis(screenAxis);
            int frames = Mathf.CeilToInt(0.2f * FrameRate);
            for (int i = 0; i < frames; i++)
            {
                CaptureFrame();
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            float projection = Vector3.Dot(player.transform.position - beforeWorld, captureCamera.transform.right);
            float afterViewportX = captureCamera.WorldToViewportPoint(player.transform.position).x;
            Assert.That(Mathf.Sign(projection), Is.EqualTo(Mathf.Sign(screenAxis)));
            Assert.That(Mathf.Sign(afterViewportX - beforeViewportX), Is.EqualTo(Mathf.Sign(screenAxis)));
            yield return Hold(2f, $"Branch {branchNumber} — {shot} confirmed");
        }

        private IEnumerator MoveToRadio(int branchNumber)
        {
            shotText.text = $"Branch {branchNumber} — follow, edge-lock entry, supported radio";
            player.SetSimulatedAxis(AxisForScalar(-1f));
            int limit = Mathf.CeilToInt((branchNumber == 1 ? 20f : 8f) * FrameRate);
            int frames = 0;
            bool gapJumpStarted = false;
            int gapJumpReleaseFrame = -1;
            bool savedLockStart = false;
            bool savedLockMid = false;
            bool savedStableEarly = false;
            while (player.CurrentScalar > controller.RadioStopDistance + 0.02f && frames++ < limit)
            {
                player.SetSimulatedAxis(AxisForScalar(-1f));
                if (branchNumber == 1 && !gapJumpStarted && player.CurrentScalar < 22f && player.CurrentScalar > 16f)
                {
                    player.SimulateJumpPress(true);
                    gapJumpStarted = true;
                    gapJumpReleaseFrame = frames + Mathf.CeilToInt(0.62f * FrameRate);
                    SaveInspectionFrame("05_gap_jump_midpoint");
                }
                if (gapJumpStarted && frames >= gapJumpReleaseFrame)
                    player.SimulateJumpRelease();
                CaptureFrame();
                if (branchNumber == 1
                    && cameraRig.State == Phase01CameraRig.CameraState.RadioLockBlend
                    && !savedLockStart)
                {
                    SaveInspectionFrame("01_first_lock_blend");
                    savedLockStart = true;
                }
                if (branchNumber == 1
                    && cameraRig.State == Phase01CameraRig.CameraState.RadioLockBlend
                    && cameraRig.TransitionProgress >= 0.45f
                    && !savedLockMid)
                {
                    SaveInspectionFrame("02_lock_blend_midpoint");
                    savedLockMid = true;
                }
                if (branchNumber == 1
                    && cameraRig.IsStableRadioLock
                    && player.CurrentScalar > controller.RadioStopDistance + 4f
                    && !savedStableEarly)
                {
                    SaveInspectionFrame("03_stable_lock_early");
                    savedStableEarly = true;
                }
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Assert.That(player.CurrentScalar, Is.LessThanOrEqualTo(controller.RadioStopDistance + 0.02f));
            yield return Hold(1.2f, $"Branch {branchNumber} — strict radio lock stabilizes");
            Assert.That(cameraRig.IsStableRadioLock, Is.True);
            if (branchNumber == 1)
            {
                SaveInspectionFrame("03_stable_lock_early");
            }
            Assert.That(support.RadioContactError, Is.LessThan(0.002f));
            float radioX = captureCamera.WorldToViewportPoint(controller.RadioPosition + Vector3.up).x;
            Assert.That(radioX > 0.5f ? radioX : 1f - radioX, Is.InRange(0.88f, 0.94f));
        }

        private IEnumerator MoveInsideStableLock(float targetScalar, string inspectionName, string shot)
        {
            Assert.That(cameraRig.IsStableRadioLock, Is.True);
            Vector3 stableCamera = cameraRig.transform.position;
            float stableSupportX = captureCamera.WorldToViewportPoint(support.transform.position).x;
            float direction = Mathf.Sign(targetScalar - player.CurrentScalar);
            shotText.text = shot;
            player.SetSimulatedAxis(AxisForScalar(direction));
            float deadline = Time.time + 20f;
            while ((direction > 0f
                    ? player.CurrentScalar < targetScalar - 0.02f
                    : player.CurrentScalar > targetScalar + 0.02f)
                && Time.time < deadline)
            {
                CaptureFrame();
                yield return null;
                Assert.That(cameraRig.IsStableRadioLock, Is.True);
                float drift = Vector3.Distance(cameraRig.transform.position, stableCamera);
                maximumStableLockDrift = Mathf.Max(maximumStableLockDrift, drift);
                Assert.That(drift, Is.LessThanOrEqualTo(cameraRig.StableLockPositionTolerance + 0.001f));
                Assert.That(captureCamera.WorldToViewportPoint(support.transform.position).x,
                    Is.EqualTo(stableSupportX).Within(0.001f));
            }
            player.SetSimulatedAxis(0f);
            if (direction > 0f)
                Assert.That(player.CurrentScalar, Is.GreaterThanOrEqualTo(targetScalar - 0.02f));
            else
                Assert.That(player.CurrentScalar, Is.LessThanOrEqualTo(targetScalar + 0.02f));
            Assert.That(player.CurrentScalar, Is.LessThanOrEqualTo(cameraRig.RadioLockExitDistance + 0.2f));
            yield return Hold(2f, shot, inspectionName);
        }

        private IEnumerator CrossLockExit(string inspectionName, string shot)
        {
            Assert.That(player.CurrentScalar, Is.LessThanOrEqualTo(cameraRig.RadioLockExitDistance + 0.2f));
            Vector3 beforeRelease = cameraRig.transform.position;
            shotText.text = shot;
            player.SetSimulatedAxis(AxisForScalar(1f));
            float deadline = Time.time + 1f;
            while (cameraRig.IsRadioLocked && Time.time < deadline)
            {
                CaptureFrame();
                yield return null;
            }
            CaptureFrame();
            if (inspectionName != null)
                SaveInspectionFrame(inspectionName);
            maximumReleaseStep = Mathf.Max(maximumReleaseStep, Vector3.Distance(beforeRelease, cameraRig.transform.position));
            Assert.That(cameraRig.IsRadioLocked, Is.False);
            Assert.That(player.CurrentScalar, Is.GreaterThan(cameraRig.RadioLockExitDistance));
            Assert.That(maximumReleaseStep, Is.LessThan(0.4f));

            deadline = Time.time + 1.5f;
            while (cameraRig.State == Phase01CameraRig.CameraState.RadioUnlockBlend && Time.time < deadline)
            {
                CaptureFrame();
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Assert.That(cameraRig.State, Is.Not.EqualTo(Phase01CameraRig.CameraState.RadioUnlockBlend));
        }

        private IEnumerator CaptureDepositTransition(
            int branchNumber,
            string depositInspectionName,
            string turnInspectionName)
        {
            Assert.That(cameraRig.IsStableRadioLock, Is.True);
            Vector3 stableCamera = cameraRig.transform.position;
            float yawStart = controller.CameraYaw;
            player.SimulateInteract();
            CaptureFrame();
            yield return null;

            float deadline = Time.time + 4f;
            bool savedDeposit = false;
            bool savedTurn = false;
            int maximumVisible = 0;
            while (controller.IsInputLocked && Time.time < deadline)
            {
                shotText.text = $"Deposit {branchNumber} — {cameraRig.State} / input locked";
                maximumVisible = Mathf.Max(maximumVisible, controller.VisibleBranchPresentationCount);
                CaptureFrame();

                if (cameraRig.State == Phase01CameraRig.CameraState.DepositLocked
                    && controller.State == Phase01DemoController.DemoState.Depositing)
                {
                    if (!savedDeposit && depositInspectionName != null)
                    {
                        SaveInspectionFrame(depositInspectionName);
                        savedDeposit = true;
                    }
                }
                float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yawStart, controller.CameraYaw));
                if (!savedTurn
                    && turnInspectionName != null
                    && cameraRig.State == Phase01CameraRig.CameraState.TurnOfVision
                    && yawDelta > 25f
                    && yawDelta < 65f)
                {
                    SaveInspectionFrame(turnInspectionName);
                    savedTurn = true;
                }
                yield return null;
            }

            Assert.That(controller.ReceiverCharge, Is.EqualTo(branchNumber));
            Assert.That(controller.CameraQuarterTurns, Is.EqualTo(branchNumber));
            Assert.That(maximumVisible, Is.EqualTo(2));
            if (depositInspectionName != null)
                Assert.That(savedDeposit, Is.True);
            if (turnInspectionName != null)
                Assert.That(savedTurn, Is.True);
        }

        private void SetupCapture()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
            renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Phase01 FIX5 Real-Time Review Capture RT"
            };
            renderTexture.Create();
            readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            captureCamera.targetTexture = renderTexture;
            encoder = new MediaEncoder(outputPath, new VideoTrackAttributes
            {
                frameRate = new MediaRational(FrameRate),
                width = (uint)Width,
                height = (uint)Height,
                includeAlpha = false
            });
            nextCaptureGameTime = Time.time;
        }

        private void SetupOverlay()
        {
            overlayRoot = new GameObject("TEMP Phase01 FIX5 Review Capture Overlay");
            overlayRoot.transform.SetParent(captureCamera.transform, false);
            backdropMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            backdropMaterial.color = new Color(0.012f, 0.018f, 0.035f, 1f);
            slateBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(slateBackdrop.GetComponent<Collider>());
            slateBackdrop.transform.SetParent(overlayRoot.transform, false);
            slateBackdrop.transform.localPosition = new Vector3(0f, 0f, 3f);
            slateBackdrop.transform.localScale = new Vector3(16f, 9.2f, 1f);
            slateBackdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
            slateText = CreateText("Slate", new Vector3(0f, 0f, 2.5f), 0.065f, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.2f));
            shotText = CreateText("Shot Label", new Vector3(0f, 3.02f, 2.4f), 0.032f, TextAnchor.UpperCenter, Color.white);
            statusText = CreateText("Debug Status", new Vector3(-5.9f, 2.96f, 2.4f), 0.020f, TextAnchor.UpperLeft, new Color(0.55f, 0.92f, 1f));
            footerText = CreateText("Footer", new Vector3(-5.9f, -3.05f, 2.4f), 0.021f, TextAnchor.LowerLeft, new Color(0.8f, 0.84f, 0.9f));
            footerText.text = "AUTOMATED REAL-TIME REVIEW — normal Time.deltaTime / no deterministic stepping";
        }

        private void SetSlateMode(bool active)
        {
            slateBackdrop.SetActive(active);
            slateText.gameObject.SetActive(active);
            shotText.gameObject.SetActive(!active);
            statusText.gameObject.SetActive(!active);
            footerText.gameObject.SetActive(!active);
        }

        private TextMesh CreateText(string name, Vector3 localPosition, float characterSize, TextAnchor anchor, Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(overlayRoot.transform, false);
            textObject.transform.localPosition = localPosition;
            TextMesh textMesh = textObject.AddComponent<TextMesh>();
            textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textMesh.GetComponent<MeshRenderer>().sharedMaterial = textMesh.font.material;
            textMesh.fontSize = 64;
            textMesh.characterSize = characterSize;
            textMesh.anchor = anchor;
            textMesh.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left
                : TextAlignment.Center;
            textMesh.color = color;
            return textMesh;
        }

        private IEnumerator Hold(float seconds, string shot, string inspectionName = null)
        {
            player.SetSimulatedAxis(0f);
            shotText.text = shot;
            float startedAt = Time.time;
            float deadline = startedAt + seconds;
            bool savedInspection = false;
            while (Time.time < deadline)
            {
                CaptureFrame();
                if (!savedInspection && inspectionName != null && Time.time >= startedAt + seconds * 0.5f)
                {
                    SaveInspectionFrame(inspectionName);
                    savedInspection = true;
                }
                yield return null;
            }
        }

        private IEnumerator MoveUntil(Func<bool> predicate, float axis, float timeoutSeconds, string shot)
        {
            shotText.text = shot;
            player.SetSimulatedAxis(axis);
            int limit = Mathf.CeilToInt(timeoutSeconds * FrameRate);
            int frames = 0;
            while (!predicate() && frames++ < limit)
            {
                CaptureFrame();
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Assert.That(predicate(), Is.True, $"Timed out: {shot}");
        }

        private IEnumerator CaptureIntentionalFallAndReset()
        {
            shotText.text = "Branch 1 — intentional no-jump fall after pickup";
            player.SetSimulatedAxis(AxisForScalar(-1f));
            int frames = 0;
            while (player.transform.position.y > -2.5f && frames++ < Mathf.CeilToInt(3f * FrameRate))
            {
                CaptureFrame();
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            SaveInspectionFrame("06_intentional_fall");
            while (player.CurrentScalar < controller.OuterDistance - 0.2f && frames++ < Mathf.CeilToInt(5f * FrameRate))
            {
                CaptureFrame();
                yield return null;
            }
            Assert.That(player.CheckpointIndex, Is.EqualTo(1));
            Assert.That(controller.CarriedParticle, Is.Not.Null);
            SaveInspectionFrame("07_post_reset_particle_carried");
        }

        private void CaptureFrame()
        {
            if (Time.time + 0.0001f < nextCaptureGameTime)
                return;
            do
                nextCaptureGameTime += 1f / FrameRate;
            while (nextCaptureGameTime <= Time.time);

            float surfaceHeight = controller.ActiveSurfaceHeight(player.CurrentScalar);
            float visualFootError = Mathf.Abs(playerRenderer.bounds.min.y - surfaceHeight);
            bool activeGap = controller.ActiveSurfaceHeight(player.CurrentScalar) < -3f;
            if (controller.State == Phase01DemoController.DemoState.Idle && !activeGap && player.IsGrounded)
            {
                maximumProfileError = Mathf.Max(maximumProfileError, player.FootSurfaceError);
                maximumVisualFootError = Mathf.Max(maximumVisualFootError, visualFootError);
                if (hasPreviousPlayerY)
                    maximumVerticalStep = Mathf.Max(maximumVerticalStep, Mathf.Abs(player.transform.position.y - previousPlayerY));
                Assert.That(controller.VisibleBranchPresentationCount, Is.EqualTo(1));
            }
            previousPlayerY = player.transform.position.y;
            hasPreviousPlayerY = controller.State == Phase01DemoController.DemoState.Idle && !activeGap && player.IsGrounded;

            float radioX = captureCamera.WorldToViewportPoint(controller.RadioPosition + Vector3.up).x;
            statusText.text = $"Branch: {controller.ActiveBranchNumber} / 4 | visible: {controller.VisibleBranchPresentationCount}\n"
                + $"Charge: {controller.ReceiverCharge} / 4 | carried: {(controller.CarriedParticle == null ? "None" : $"B{controller.CarriedParticle.BranchIndex + 1}")}\n"
                + $"Camera: {cameraRig.State} | stable lock: {cameraRig.IsStableRadioLock}\n"
                + $"Support: {cameraRig.CurrentSupportHeight:F2} | airborne lift: {cameraRig.CurrentAirborneLift:F2} | rejected void: {cameraRig.RejectedVoidHeightCount}\n"
                + $"Radio viewport X: {radioX:F3} | target: {cameraRig.RadioViewportXTarget:F2}\n"
                + $"Enter / exit: {cameraRig.RadioLockEnterDistance:F1} / {cameraRig.RadioLockExitDistance:F1}\n"
                + $"Scalar: {player.CurrentScalar:F2} / {controller.OuterDistance:F1}\n"
                + $"Stable drift max: {maximumStableLockDrift:F4} / tolerance {cameraRig.StableLockPositionTolerance:F3}\n"
                + $"Support contact error: {support.RadioContactError:F4}\n"
                + $"Input locked: {controller.IsInputLocked} | complete: {controller.DemoComplete}";
            if (performanceMonitor != null)
                statusText.text += $"\nFrame: {performanceMonitor.CurrentFrameMs:F2} ms / {performanceMonitor.CurrentFps:F1} FPS | GC: {performanceMonitor.CurrentGcBytes} B";

            captureCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = previous;
            Assert.That(encoder.AddFrame(readback), Is.True);
            frameCount++;
        }

        private void SaveInspectionFrame(string name)
        {
            File.WriteAllBytes(Path.Combine(inspectionFolder, $"{name}.png"), readback.EncodeToPNG());
        }

        private void FinishCapture()
        {
            encoder.Dispose();
            encoder = null;
            captureCamera.targetTexture = null;
            renderTexture.Release();
            UnityEngine.Object.Destroy(readback);
            UnityEngine.Object.Destroy(renderTexture);
            UnityEngine.Object.Destroy(backdropMaterial);
            UnityEngine.Object.Destroy(overlayRoot);
        }
    }
}
#endif
