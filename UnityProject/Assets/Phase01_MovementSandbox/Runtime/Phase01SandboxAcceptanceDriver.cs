using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxAcceptanceDriver : MonoBehaviour
    {
        [SerializeField] private Phase01ParkourMotor motor;
        [SerializeField] private Phase01SandboxReset reset;
        [SerializeField] private Phase01SandboxCamera cameraRig;
        [SerializeField] private Phase01MovingPlatform movingPlatform;
        [SerializeField] private Phase01SandboxCheckpoint[] checkpoints;
        [SerializeField] private Phase01SandboxPerformanceMonitor performance;

        private readonly List<string> failures = new List<string>();

        public bool Finished { get; private set; }
        public bool Passed => Finished && failures.Count == 0;
        public string Report => failures.Count == 0 ? "Sandbox automated tour passed." : string.Join(" | ", failures);
        public float MeasuredHeldJumpHeight { get; private set; }
        public float MeasuredTapJumpHeight { get; private set; }
        public float MeasuredApexTime { get; private set; }
        public float MeasuredCoyoteDelay { get; private set; }
        public float MeasuredResetDuration => reset.LastResetDuration;
        public string CurrentShot { get; private set; } = "Sandbox ready";
        public bool CapturePacing { get; set; }

        public void Configure(
            Phase01ParkourMotor targetMotor,
            Phase01SandboxReset resetController,
            Phase01SandboxCamera targetCamera,
            Phase01MovingPlatform platform,
            Phase01SandboxCheckpoint[] sceneCheckpoints,
            Phase01SandboxPerformanceMonitor monitor)
        {
            motor = targetMotor;
            reset = resetController;
            cameraRig = targetCamera;
            movingPlatform = platform;
            checkpoints = sceneCheckpoints;
            performance = monitor;
        }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, value => value == "-phase01SandboxPerformance"))
                StartCoroutine(RunPerformanceAndQuit());
            else if (Array.Exists(args, value => value == "-phase01SandboxSelfTest"))
                StartCoroutine(RunAndQuit());
        }

        public IEnumerator RunAcceptance()
        {
            Finished = false;
            failures.Clear();
            yield return null;
            Check(motor != null && reset != null && cameraRig != null && movingPlatform != null, "required references");
            Check(!motor.IsUsingSimulatedInput, "manual input default");
            Check(Mathf.Abs(motor.transform.position.z) < 0.001f, "movement plane");

            CurrentShot = "Horizontal acceleration, stop, and reversal";
            motor.TeleportAndReset(new Vector3(-4f, 0.05f, 0f));
            yield return WaitGrounded(1f);
            motor.SetSimulatedInput(1f, false);
            yield return new WaitForSeconds(0.8f);
            motor.SetSimulatedInput(0f, false);
            yield return new WaitForSeconds(0.5f);
            motor.SetSimulatedInput(-1f, false);
            yield return new WaitForSeconds(0.7f);
            motor.SetSimulatedInput(0f, false);
            yield return CapturePause();

            yield return MeasureJump(false);
            yield return MeasureJump(true);
            Check(MeasuredHeldJumpHeight >= 1.8f && MeasuredHeldJumpHeight <= 2.6f, "held jump height range");
            Check(MeasuredTapJumpHeight < MeasuredHeldJumpHeight * 0.78f, "tap jump lower than held");
            Check(MeasuredApexTime >= 0.35f && MeasuredApexTime <= 0.58f, "apex time range");

            yield return TestCoyote(true);
            yield return TestCoyote(false);
            yield return TestBuffer(true);
            yield return TestBuffer(false);
            yield return TraverseSandbox();

            motor.ClearSimulatedInput();
            CurrentShot = "Sandbox acceptance complete";
            yield return CapturePause();
            Check(reset.ActivatedCheckpointCount >= 3, "three checkpoints activated");
            Check(Mathf.Abs(motor.transform.position.z) < 0.001f, "plane preserved");
            Finished = true;
        }

        private IEnumerator MeasureJump(bool held)
        {
            CurrentShot = held ? "Held jump — full-height arc" : "Tapped jump — reduced-height arc";
            motor.TeleportAndReset(new Vector3(-4f, 0.05f, 0f));
            yield return WaitGrounded(1f);
            float startY = motor.transform.position.y;
            float started = Time.time;
            int jumpsBefore = motor.AcceptedJumpCount;
            motor.SetSimulatedInput(0f, held);
            motor.SimulateJumpPress(held);
            float peak = startY;
            bool released = held;
            while (Time.time - started < 2f)
            {
                if (!held && !released && Time.time - started > 0.08f)
                {
                    motor.SimulateJumpRelease();
                    released = true;
                }
                peak = Mathf.Max(peak, motor.transform.position.y);
                if (motor.AcceptedJumpCount > jumpsBefore && motor.VerticalVelocity <= 0f)
                    break;
                yield return null;
            }
            float height = peak - startY;
            if (held)
            {
                MeasuredHeldJumpHeight = height;
                MeasuredApexTime = Time.time - started;
            }
            else
            {
                MeasuredTapJumpHeight = height;
            }
            motor.SimulateJumpRelease();
            yield return WaitGrounded(2f);
            Check(motor.AcceptedJumpCount == jumpsBefore + 1, held ? "held jump once" : "tap jump once");
            yield return CapturePause();
        }

        private IEnumerator TestCoyote(bool insideWindow)
        {
            CurrentShot = insideWindow
                ? "Coyote time — inside-window edge jump succeeds"
                : "Coyote time — outside-window input expires";
            motor.TeleportAndReset(new Vector3(5.2f, 0.05f, 0f));
            yield return WaitGrounded(1f);
            motor.SetSimulatedInput(1f, false);
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (motor.IsGrounded && Time.realtimeSinceStartup < deadline)
                yield return null;
            float leftAt = Time.realtimeSinceStartup;
            float delay = insideWindow ? 0.04f : motor.CoyoteTime + 0.09f;
            yield return new WaitForSeconds(delay);
            int before = motor.AcceptedJumpCount;
            if (insideWindow)
                MeasuredCoyoteDelay = Time.realtimeSinceStartup - leftAt;
            motor.SimulateJumpPress(true);
            yield return new WaitForSeconds(0.12f);
            bool accepted = motor.AcceptedJumpCount == before + 1;
            Check(accepted == insideWindow, insideWindow ? "coyote inside accepted" : "coyote outside expired");
            motor.SimulateJumpRelease();
            motor.SetSimulatedInput(0f, false);
            yield return WaitForResetOrGround(2f);
            yield return CapturePause();
        }

        private IEnumerator TestBuffer(bool insideWindow)
        {
            CurrentShot = insideWindow
                ? "Jump buffer — pre-landing input launches on contact"
                : "Jump buffer — old input expires without repeat";
            motor.TeleportAndReset(new Vector3(-4f, insideWindow ? 0.38f : 3.2f, 0f));
            motor.SetSimulatedInput(0f, true);
            yield return null;
            if (insideWindow)
                motor.SetVelocityForAutomation(new Vector3(0f, -4f, 0f));
            int before = motor.AcceptedJumpCount;
            motor.SimulateJumpPress(true);
            yield return new WaitForSeconds(0.5f);
            bool accepted = motor.AcceptedJumpCount == before + 1;
            Check(accepted == insideWindow, insideWindow ? "buffer inside accepted" : "buffer outside expired");
            motor.SimulateJumpRelease();
            yield return WaitGrounded(2f);
            yield return CapturePause();
        }

        private IEnumerator TraverseSandbox()
        {
            CurrentShot = "Low step and short gap — clean traversal";
            reset.ActivateCheckpoint(checkpoints[0]);
            motor.TeleportAndReset(checkpoints[0].RespawnPosition);
            yield return WaitGrounded(1f);
            motor.SetSimulatedInput(1f, false);
            yield return MoveUntilX(4.8f, 3f, "low step traversal");
            motor.SimulateJumpPress(true);
            yield return MoveUntilX(10f, 3f, "short gap clear");
            motor.SimulateJumpRelease();
            yield return WaitGrounded(1.5f);
            motor.SetSimulatedInput(0f, false);
            yield return CapturePause();
            reset.ActivateCheckpoint(checkpoints[1]);

            CurrentShot = "Intentional gap fall — fast soft reset";
            motor.TeleportAndReset(new Vector3(5.4f, 0.05f, 0f));
            int resetsBefore = reset.ResetCount;
            motor.SetSimulatedInput(1f, false);
            yield return WaitUntil(() => reset.ResetCount > resetsBefore, 2f, "gap soft reset");
            Check(reset.LastResetDuration < 1f, "reset under one second");
            Check(Vector3.Distance(motor.transform.position, reset.CurrentRespawnPosition) < 0.8f, "latest checkpoint reset");
            yield return CapturePause();

            CurrentShot = "Raised ledge — held jump and stable landing";
            motor.TeleportAndReset(new Vector3(14.2f, 0.05f, 0f));
            yield return WaitGrounded(1f);
            motor.SetSimulatedInput(1f, true);
            motor.SimulateJumpPress(true);
            yield return MoveUntilX(18.2f, 2f, "raised ledge reached");
            motor.SimulateJumpRelease();
            yield return WaitGrounded(2f);
            motor.SetSimulatedInput(0f, false);
            Check(motor.transform.position.y > 1f, "raised ledge landing");
            yield return CapturePause();
            reset.ActivateCheckpoint(checkpoints[2]);

            CurrentShot = "Moving platform — idle ride and inherited displacement";
            Vector3 platformTop = movingPlatform.transform.position + Vector3.up * 0.4f;
            motor.TeleportAndReset(platformTop);
            yield return WaitGrounded(1f);
            float relativeStart = motor.transform.position.x - movingPlatform.transform.position.x;
            motor.SetSimulatedInput(0f, false);
            yield return new WaitForSeconds(1.2f);
            float relativeEnd = motor.transform.position.x - movingPlatform.transform.position.x;
            Check(Mathf.Abs(relativeEnd - relativeStart) < 0.2f, "moving platform idle inheritance");
            yield return CapturePause();
            CurrentShot = "Moving platform — jump and stable re-landing";
            int jumpBefore = motor.AcceptedJumpCount;
            motor.SimulateJumpPress(true);
            yield return new WaitForSeconds(0.16f);
            Check(motor.AcceptedJumpCount == jumpBefore + 1, "moving platform jump");
            motor.SimulateJumpRelease();
            yield return WaitGrounded(2f);
            motor.TeleportAndReset(movingPlatform.transform.position + Vector3.up * 2f);
            motor.SetVelocityForAutomation(new Vector3(0f, -2f, 0f));
            yield return WaitUntil(
                () => motor.IsGrounded && motor.GroundPlatform == movingPlatform,
                2f,
                "moving platform stable re-landing");
            yield return CapturePause();

            CurrentShot = "Latest checkpoint — fall and deterministic return";
            resetsBefore = reset.ResetCount;
            motor.TeleportAndReset(new Vector3(motor.transform.position.x, -4f, 0f));
            yield return WaitUntil(() => reset.ResetCount > resetsBefore, 2f, "latest checkpoint fall reset");
            yield return CapturePause();
        }

        private IEnumerator CapturePause()
        {
            if (CapturePacing)
                yield return new WaitForSecondsRealtime(5f);
        }

        private IEnumerator MoveUntilX(float targetX, float timeout, string failure)
        {
            yield return WaitUntil(() => motor.transform.position.x >= targetX, timeout, failure);
        }

        private IEnumerator WaitGrounded(float timeout)
        {
            yield return WaitUntil(() => motor.IsGrounded, timeout, "grounding timeout");
        }

        private IEnumerator WaitForResetOrGround(float timeout)
        {
            yield return WaitUntil(() => reset.IsResetting || motor.IsGrounded, timeout, "fall resolution timeout");
            while (reset.IsResetting)
                yield return null;
        }

        private IEnumerator WaitUntil(Func<bool> predicate, float timeout, string failure)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Check(predicate(), failure);
        }

        private void Check(bool condition, string failure)
        {
            if (!condition && !failures.Contains(failure))
                failures.Add(failure);
        }

        private IEnumerator RunAndQuit()
        {
            yield return RunAcceptance();
            if (Passed)
                Debug.Log($"[Phase01SandboxAcceptance] PASS {Report}");
            else
                Debug.LogError($"[Phase01SandboxAcceptance] FAIL {Report}");
            yield return null;
            Application.Quit(Passed ? 0 : 1);
        }

        private IEnumerator RunPerformanceAndQuit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1280, 720, false);
            yield return new WaitForSecondsRealtime(3f);
            performance.BeginMeasurement();
            yield return RunAcceptance();
            Phase01SandboxPerformanceMonitor.Result result = performance.EndMeasurement();
            string path = ArgumentValue("-phase01SandboxPerformanceOutput");
            if (string.IsNullOrEmpty(path))
                path = System.IO.Path.Combine(Application.persistentDataPath, "TASK_P1_MOVEMENT_SANDBOX_PerformanceMetrics.json");
            Phase01SandboxPerformanceMonitor.Write(path, result);
            bool passed = Passed && result.averageFrameMs <= 20f && result.errorCount == 0
                && result.exceptionCount == 0 && result.unsafeMeshErrorCount == 0;
            string metrics = $"avg={result.averageFrameMs:F3}ms p95={result.p95FrameMs:F3}ms p99={result.p99FrameMs:F3}ms "
                + $"worst={result.worstFrameMs:F3}ms fps={result.averageFps:F1} gc={result.averageGcBytesPerFrame}B/frame "
                + $"collections={result.gcCollectionCount} errors={result.errorCount} exceptions={result.exceptionCount}";
            if (passed)
                Debug.Log($"[Phase01SandboxPerformance] PASS {metrics} output={path}");
            else
                Debug.LogError($"[Phase01SandboxPerformance] FAIL {metrics} tour={Report} output={path}");
            yield return null;
            Application.Quit(passed ? 0 : 1);
        }

        private static string ArgumentValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key)
                    return args[i + 1];
            return null;
        }
    }
}
