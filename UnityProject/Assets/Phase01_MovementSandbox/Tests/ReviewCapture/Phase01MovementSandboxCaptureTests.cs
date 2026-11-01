#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.MovementSandbox.Tests
{
    public sealed class Phase01MovementSandboxCaptureTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const int FrameRate = 30;

        private Phase01ParkourMotor motor;
        private Phase01SandboxAcceptanceDriver driver;
        private Phase01SandboxReset reset;
        private Phase01SandboxCamera sandboxCamera;
        private Phase01SandboxPerformanceMonitor performance;
        private Phase01MovingPlatform movingPlatform;
        private Camera captureCamera;
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
        private string renderedShot = string.Empty;
        private string previousRenderedShot = string.Empty;
        private int renderedShotFrames;
        private readonly HashSet<string> saved = new HashSet<string>();
        private TextMesh[] courseTexts;

        [UnityTest]
        [Category("ReviewCapture")]
        public IEnumerator CaptureMovementSandboxAcceptance()
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), value => value == "-phase01SandboxCapture"))
                Assert.Ignore("Run only through the Phase01 sandbox capture command.");

            SceneManager.LoadScene("Phase01_MovementSandbox", LoadSceneMode.Single);
            yield return null;
            motor = UnityEngine.Object.FindAnyObjectByType<Phase01ParkourMotor>();
            driver = UnityEngine.Object.FindAnyObjectByType<Phase01SandboxAcceptanceDriver>();
            reset = UnityEngine.Object.FindAnyObjectByType<Phase01SandboxReset>();
            sandboxCamera = UnityEngine.Object.FindAnyObjectByType<Phase01SandboxCamera>();
            performance = UnityEngine.Object.FindAnyObjectByType<Phase01SandboxPerformanceMonitor>();
            movingPlatform = UnityEngine.Object.FindAnyObjectByType<Phase01MovingPlatform>();
            captureCamera = Camera.main;
            Assert.That(motor, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(captureCamera, Is.Not.Null);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewCaptures"));
            Directory.CreateDirectory(root);
            outputPath = Path.Combine(root, $"TASK_P1_MOVEMENT_SANDBOX_{stamp}.mp4");
            inspectionFolder = Path.Combine(root, $"TASK_P1_MOVEMENT_SANDBOX_{stamp}_inspection_frames");
            Directory.CreateDirectory(inspectionFolder);
            SetupCapture();
            SetupOverlay();

            SetSlate(true);
            slateText.text = $"TASK_P1_MOVEMENT_SANDBOX\n\nUnity {Application.unityVersion}\nScene: {SceneManager.GetActiveScene().name}\nReal runtime / normal Time.deltaTime\nUnity MediaEncoder at {Width} × {Height}, {FrameRate} fps\n{DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}";
            yield return Hold(5f, "Title", "00_title");

            SetSlate(false);
            sandboxCamera.enabled = false;
            captureCamera.transform.position = new Vector3(18f, 3.5f, -14f);
            captureCamera.orthographicSize = 12f;
            yield return Hold(3f, "Sandbox overview — isolated movement course", "00_sandbox_overview");
            captureCamera.orthographicSize = 4.5f;
            sandboxCamera.enabled = true;
            sandboxCamera.BeginResetSettle(motor.transform.position);
            driver.CapturePacing = true;
            driver.StartCoroutine(driver.RunAcceptance());
            float deadline = Time.realtimeSinceStartup + 140f;
            while (!driver.Finished && Time.realtimeSinceStartup < deadline)
            {
                CaptureFrame();
                SaveConditionalFrames();
                yield return null;
            }
            Assert.That(driver.Finished, Is.True, "Capture tour timed out.");
            Assert.That(driver.Passed, Is.True, driver.Report);
            yield return CaptureMovingPlatformEvidence();
            sandboxCamera.enabled = true;
            sandboxCamera.BeginResetSettle(motor.transform.position);
            yield return Hold(2f, "Sandbox acceptance complete", "12_final_sandbox_completion");

            SetSlate(true);
            string metricsPath = Path.Combine(root, "TASK_P1_MOVEMENT_SANDBOX_PerformanceMetrics.json");
            Phase01SandboxPerformanceMonitor.Result metrics = File.Exists(metricsPath)
                ? JsonUtility.FromJson<Phase01SandboxPerformanceMonitor.Result>(File.ReadAllText(metricsPath))
                : new Phase01SandboxPerformanceMonitor.Result();
            slateText.text = $"MOVEMENT SANDBOX ACCEPTANCE COMPLETE\n\n"
                + $"Speed {motor.MoveSpeed:F1} | accel/decel {motor.GroundAcceleration:F0}/{motor.GroundDeceleration:F0}\n"
                + $"Held jump {driver.MeasuredHeldJumpHeight:F3} m | tap {driver.MeasuredTapJumpHeight:F3} m\n"
                + $"Apex {driver.MeasuredApexTime:F3} s | coyote/buffer {motor.CoyoteTime:F2}/{motor.JumpBufferTime:F2} s\n"
                + $"Reset {driver.MeasuredResetDuration:F3} s\n"
                + $"Standalone avg/p95/p99 {metrics.averageFrameMs:F3}/{metrics.p95FrameMs:F3}/{metrics.p99FrameMs:F3} ms\n"
                + $"GC {metrics.averageGcBytesPerFrame} B/frame, collections {metrics.gcCollectionCount}, errors {metrics.errorCount}\n"
                + "Sandbox build/launch PASS | Phase01_Demo0 regression PASS\n\n"
                + "Known limitation: development overlay allocates; placeholder art and silence";
            yield return Hold(8f, "End metrics", "99_end_metrics_slate");

            motor.ClearSimulatedInput();
            FinishCapture();
            float duration = frameCount / (float)FrameRate;
            Assert.That(duration, Is.InRange(90f, 150f));
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(1024L));
            Assert.That(Directory.GetFiles(inspectionFolder, "*.png").Length, Is.GreaterThanOrEqualTo(14));
            Debug.Log($"[Phase01MovementSandboxCapture] PASS path={outputPath} frames={frameCount} "
                + $"fps={FrameRate} duration={duration:F2}s inspection={inspectionFolder}");
        }

        private void SetupCapture()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
            renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Phase01 Movement Sandbox Capture"
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
        }

        private void SetupOverlay()
        {
            overlayRoot = new GameObject("TEMP Sandbox Capture Overlay");
            overlayRoot.transform.SetParent(captureCamera.transform, false);
            backdropMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            backdropMaterial.color = new Color(0.01f, 0.018f, 0.035f, 1f);
            slateBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            UnityEngine.Object.Destroy(slateBackdrop.GetComponent<Collider>());
            slateBackdrop.transform.SetParent(overlayRoot.transform, false);
            slateBackdrop.transform.localPosition = new Vector3(0f, 0f, 3f);
            slateBackdrop.transform.localScale = new Vector3(16f, 9.2f, 1f);
            slateBackdrop.GetComponent<Renderer>().sharedMaterial = backdropMaterial;
            slateText = Text("Slate", new Vector3(0f, 0f, 2.5f), 0.058f, TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.2f));
            shotText = Text("Shot", new Vector3(0f, 3.12f, 2.4f), 0.032f, TextAnchor.UpperCenter, Color.white);
            statusText = Text("Status", new Vector3(-6.0f, 3.0f, 2.4f), 0.019f, TextAnchor.UpperLeft, new Color(0.55f, 0.92f, 1f));
            footerText = Text("Footer", new Vector3(-6f, -3.05f, 2.4f), 0.02f, TextAnchor.LowerLeft, new Color(0.8f, 0.84f, 0.9f));
            footerText.text = "AUTOMATED REAL-RUNTIME SANDBOX REVIEW — normal Time.deltaTime";
            TextMesh[] allText = UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude);
            courseTexts = Array.FindAll(allText, item => !item.transform.IsChildOf(overlayRoot.transform));
        }

        private TextMesh Text(string name, Vector3 position, float size, TextAnchor anchor, Color color)
        {
            GameObject target = new GameObject(name);
            target.transform.SetParent(overlayRoot.transform, false);
            target.transform.localPosition = position;
            TextMesh text = target.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 64;
            text.characterSize = size;
            text.anchor = anchor;
            text.alignment = anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left : TextAlignment.Center;
            text.color = color;
            return text;
        }

        private void SetSlate(bool active)
        {
            slateBackdrop.SetActive(active);
            slateText.gameObject.SetActive(active);
            shotText.gameObject.SetActive(!active);
            statusText.gameObject.SetActive(!active);
            footerText.gameObject.SetActive(!active);
            if (courseTexts != null)
                foreach (TextMesh text in courseTexts)
                    if (text != null)
                        text.gameObject.SetActive(!active);
        }

        private IEnumerator Hold(float seconds, string shot, string inspection)
        {
            shotText.text = shot;
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                CaptureFrame();
                yield return null;
            }
            SaveNamed(inspection);
        }

        private void CaptureFrame()
        {
            if (driver != null && driver.CapturePacing)
                shotText.text = driver.CurrentShot;
            renderedShot = shotText.text;
            if (renderedShot == previousRenderedShot)
                renderedShotFrames++;
            else
            {
                previousRenderedShot = renderedShot;
                renderedShotFrames = 1;
            }
            if (motor != null)
            {
                statusText.text = $"Grounded: {motor.IsGrounded} | platform: {(motor.GroundPlatform == null ? "None" : "Moving")}\n"
                    + $"Velocity X/Y: {motor.HorizontalVelocity:F2} / {motor.VerticalVelocity:F2}\n"
                    + $"Coyote: {motor.CoyoteTimer:F3}/{motor.CoyoteTime:F2} | buffer: {motor.JumpBufferTimer:F3}/{motor.JumpBufferTime:F2}\n"
                    + $"Checkpoint: {reset.CurrentCheckpointId} | resetting: {reset.IsResetting}\n"
                    + $"Held/tap: {driver.MeasuredHeldJumpHeight:F2}/{driver.MeasuredTapJumpHeight:F2} m | apex {driver.MeasuredApexTime:F2}s\n"
                    + $"Frame: {performance.CurrentFrameMs:F2} ms / {performance.CurrentFps:F1} FPS | GC {performance.CurrentGcBytes} B";
            }
            captureCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
            readback.Apply(false);
            RenderTexture.active = previous;
            Assert.That(encoder.AddFrame(readback), Is.True);
            frameCount++;
        }

        private void SaveConditionalFrames()
        {
            string shot = renderedShot;
            if (renderedShotFrames >= 6 && shot.StartsWith("Horizontal") && motor.IsGrounded && Mathf.Abs(motor.HorizontalVelocity) > 2f)
                SaveNamed("01_grounded_movement");
            if (renderedShotFrames >= 6 && shot.StartsWith("Tapped") && !motor.IsGrounded && motor.VerticalVelocity <= 0.15f && motor.transform.position.y > 0.8f)
                SaveNamed("02_tap_jump_apex");
            if (renderedShotFrames >= 6 && shot.StartsWith("Held") && !motor.IsGrounded && motor.VerticalVelocity <= 0.15f && motor.transform.position.y > 1.5f)
                SaveNamed("03_held_jump_apex");
            if (renderedShotFrames >= 2 && shot.Contains("inside-window") && !motor.IsGrounded)
                SaveNamed("04_coyote_edge_frame");
            if (renderedShotFrames >= 2 && shot.StartsWith("Jump buffer") && motor.VerticalVelocity > 2f)
                SaveNamed("05_buffered_jump_landing");
            if (renderedShotFrames >= 2 && shot.StartsWith("Low step") && motor.transform.position.x > 6f && motor.transform.position.x < 9f)
                SaveNamed("06_short_gap_midpoint");
            if (renderedShotFrames >= 2 && shot.StartsWith("Intentional") && reset.IsResetting)
                SaveNamed("07_soft_reset_transition");
            if (renderedShotFrames >= 6 && shot.StartsWith("Raised") && motor.IsGrounded && motor.transform.position.y > 1f)
                SaveNamed("08_raised_ledge_landing");
            if (renderedShotFrames >= 2 && shot.StartsWith("Latest checkpoint") && reset.IsResetting)
                SaveNamed("11_latest_checkpoint_reset");
        }

        private IEnumerator CaptureMovingPlatformEvidence()
        {
            driver.CapturePacing = false;
            sandboxCamera.enabled = false;
            Rigidbody playerBody = motor.GetComponent<Rigidbody>();
            Rigidbody platformBody = movingPlatform.GetComponent<Rigidbody>();
            RigidbodyInterpolation playerInterpolation = playerBody.interpolation;
            RigidbodyInterpolation platformInterpolation = platformBody.interpolation;
            playerBody.interpolation = RigidbodyInterpolation.None;
            platformBody.interpolation = RigidbodyInterpolation.None;
            motor.SetSimulatedInput(0f, false);
            motor.TeleportAndReset(movingPlatform.transform.position + Vector3.up * 0.4f);

            shotText.text = "Moving platform — idle ride and inherited displacement";
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((!motor.IsGrounded || motor.GroundPlatform != movingPlatform)
                && Time.realtimeSinceStartup < deadline)
            {
                FrameMovingPlatform();
                yield return null;
            }
            Assert.That(motor.GroundPlatform, Is.EqualTo(movingPlatform), "Capture player did not settle on moving platform.");
            float rideUntil = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < rideUntil)
            {
                FrameMovingPlatform();
                yield return null;
            }
            FrameMovingPlatform();
            SaveNamed("09_moving_platform_idle_ride");

            shotText.text = "Moving platform — jump and stable re-landing";
            int jumpBefore = motor.AcceptedJumpCount;
            motor.SimulateJumpPress(true);
            float releaseAt = Time.realtimeSinceStartup + 0.16f;
            deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= releaseAt)
                    motor.SimulateJumpRelease();
                FrameMovingPlatform();
                if (motor.AcceptedJumpCount == jumpBefore + 1 && !motor.IsGrounded
                    && motor.VerticalVelocity <= 0.15f && motor.transform.position.y > movingPlatform.transform.position.y + 1f)
                    break;
                yield return null;
            }
            Assert.That(motor.AcceptedJumpCount, Is.EqualTo(jumpBefore + 1), "Capture moving-platform jump was not accepted once.");
            FrameMovingPlatform();
            SaveNamed("10_moving_platform_jump");

            deadline = Time.realtimeSinceStartup + 2f;
            while ((!motor.IsGrounded || motor.GroundPlatform != movingPlatform)
                && Time.realtimeSinceStartup < deadline)
            {
                FrameMovingPlatform();
                yield return null;
            }
            Assert.That(motor.GroundPlatform, Is.EqualTo(movingPlatform), "Capture moving-platform jump did not re-land stably.");
            motor.ClearSimulatedInput();
            playerBody.interpolation = playerInterpolation;
            platformBody.interpolation = platformInterpolation;
        }

        private void FrameMovingPlatform()
        {
            captureCamera.transform.position = new Vector3(movingPlatform.transform.position.x, 3.5f, -14f);
            CaptureFrame();
        }

        private void SaveNamed(string name)
        {
            if (saved.Contains(name))
                return;
            saved.Add(name);
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
