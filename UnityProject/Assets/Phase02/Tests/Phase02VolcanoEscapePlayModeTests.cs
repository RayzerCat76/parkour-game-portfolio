using System.Collections;
using NUnit.Framework;
using Phase01.MovementSandbox;
using ProjectReturn.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase02.VolcanoEscape.Tests
{
    public sealed class Phase02VolcanoEscapePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ProjectServicesRoot.EnsureExists().Music.ResetRecordedEventsForTests();
            yield return null;
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_BuildsAuthoredFiveSectionGreybox()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.IsBuilt, Is.True);
            Assert.That(controller.IsPlayable, Is.True);
            Assert.That(controller.RouteLength, Is.EqualTo(1250f).Within(0.01f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(0), Is.EqualTo(0f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(1), Is.EqualTo(230f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(2), Is.EqualTo(460f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(3), Is.EqualTo(710f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(4), Is.EqualTo(1050f));
            Assert.That(Phase02VolcanoEscapeController.GetSectionBoundary(5), Is.EqualTo(1250f));
            Assert.That(controller.TotalGapCount, Is.EqualTo(Phase02VolcanoEscapeController.AuthoredGapCount));
            Assert.That(controller.CheckpointCount, Is.EqualTo(4));
            Assert.That(controller.CollapsingPlatformCount, Is.EqualTo(10));
            Assert.That(controller.DebrisHazardCount, Is.EqualTo(11));
            Assert.That(controller.ShockwaveHazardCount, Is.EqualTo(4));
            Assert.That(controller.CollapseFrontCount, Is.EqualTo(1));
            Assert.That(controller.AcceptanceDriver, Is.Not.Null);
            Assert.That(controller.AcceptanceDriver.IsConfigured, Is.True);
            Assert.That(Phase02VolcanoEscapeController.AuthoredSectionJumpCounts,
                Is.EqualTo(new[] { 8, 10, 11, 14, 7 }));
            Assert.That(controller.VolcanoBackground, Is.Not.Null);
            Assert.That(controller.WhiteOpening, Is.Not.Null);
            Assert.That(controller.VolcanoBackground.GetComponentsInChildren<Collider>(true), Is.Empty,
                "The static volcano background must not participate in gameplay physics.");
            Assert.That(controller.VolcanoBackground.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(controller.VolcanoBackground.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(controller.WhiteOpening.GetComponentsInChildren<Collider>(true), Is.Empty);

            PlaceholderSceneController placeholder = Object.FindAnyObjectByType<PlaceholderSceneController>();
            Assert.That(placeholder == null || !placeholder.enabled, Is.True,
                "The Phase 02 placeholder must be disabled when the volcano escape is installed.");
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_ReusesAcceptedPhase01MotorValues()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Phase01ParkourMotor motor = controller != null ? controller.Motor : null;
            Assert.That(motor, Is.Not.Null);
            Assert.That(motor.MoveSpeed, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(motor.JumpHeight, Is.EqualTo(2.25f).Within(0.001f));
            Assert.That(motor.TimeToApex, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(motor.CoyoteTime, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(motor.JumpBufferTime, Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(motor.GroundSnapDistance, Is.EqualTo(0.38f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_RequiredGapsHaveNoHiddenSupport()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.GapIntervals.Count, Is.EqualTo(50));

            foreach (Vector2 gap in controller.GapIntervals)
            {
                Assert.That(gap.y - gap.x, Is.InRange(1.45f, 2.4f));
                float center = (gap.x + gap.y) * 0.5f;
                bool hit = Physics.Raycast(
                    new Vector3(center, 6f, 0f),
                    Vector3.down,
                    out RaycastHit support,
                    20f,
                    ~0,
                    QueryTriggerInteraction.Ignore);
                Assert.That(hit, Is.False,
                    $"Gap {gap.x:F2}-{gap.y:F2} is bridged by {support.collider?.name}.");
            }
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_HazardsUseDeterministicAuthoredTiming()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02CollapsingPlatform[] collapsing =
                Object.FindObjectsByType<Phase02CollapsingPlatform>(FindObjectsSortMode.None);
            Phase02DebrisHazard[] debris =
                Object.FindObjectsByType<Phase02DebrisHazard>(FindObjectsSortMode.None);
            Phase02ShockwaveHazard[] shockwaves =
                Object.FindObjectsByType<Phase02ShockwaveHazard>(FindObjectsSortMode.None);
            Phase02CollapseFront[] fronts =
                Object.FindObjectsByType<Phase02CollapseFront>(FindObjectsSortMode.None);

            Assert.That(collapsing, Has.Length.EqualTo(10));
            Assert.That(debris, Has.Length.EqualTo(11));
            Assert.That(shockwaves, Has.Length.EqualTo(4));
            Assert.That(fronts, Has.Length.EqualTo(1));
            foreach (Phase02CollapsingPlatform hazard in collapsing)
                Assert.That(hazard.WarningDuration, Is.EqualTo(1.75f).Within(0.001f));
            foreach (Phase02DebrisHazard hazard in debris)
                Assert.That(hazard.WarningDuration, Is.InRange(0.85f, 1.15f));
            foreach (Phase02ShockwaveHazard hazard in shockwaves)
            {
                Assert.That(hazard.WarningDuration, Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(hazard.TravelDuration, Is.EqualTo(2.4f).Within(0.001f));
            }
            Assert.That(fronts[0].IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_EmitsEntryEventsAndReadableParametersOnce()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            Assert.That(controller, Is.Not.Null);
            Assert.That(services.Music.GetEventCount("Phase2Entered"), Is.EqualTo(1));
            Assert.That(services.Music.GetEventCount("Phase2RuptureStarted"), Is.EqualTo(1));
            Assert.That(services.Music.GetEventCount("Phase2SectionChanged"), Is.EqualTo(1));
            Assert.That(services.Music.Phase2Progress, Is.InRange(0f, 1f));
            Assert.That(services.Music.Phase2DangerIntensity, Is.GreaterThan(0f));
            Assert.That(services.Music.Phase2HazeDensity, Is.GreaterThan(0f));
            Assert.That(services.Music.Phase2CollapseProximity, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_LocalFailureRestoresLatestSafeGroundedPose()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Phase02RollingRespawnPass rollingRespawn = Object.FindAnyObjectByType<Phase02RollingRespawnPass>();
            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            Assert.That(controller, Is.Not.Null);
            Assert.That(rollingRespawn, Is.Not.Null);

            float checkpointTriggerX = Phase02VolcanoEscapeController.GetSectionBoundary(1) + 2.2f;
            controller.Motor.TeleportAndReset(new Vector3(
                checkpointTriggerX,
                controller.SurfaceHeightAt(checkpointTriggerX),
                0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(controller.CurrentCheckpointIndex, Is.EqualTo(0));

            const float safeProgressX = 300f;
            controller.Motor.TeleportAndReset(new Vector3(
                safeProgressX,
                controller.SurfaceHeightAt(safeProgressX),
                0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(rollingRespawn.HasSafePose, Is.True);
            Assert.That(rollingRespawn.LatestSafePosition.x, Is.EqualTo(safeProgressX).Within(0.35f),
                "The rolling respawn point did not advance to the latest stable grounded platform.");

            controller.Fail(1, "TestFailure");
            float deadline = Time.realtimeSinceStartup + 2f;
            while ((controller.IsResetting || rollingRespawn.RespawnOverrideCount < 1)
                   && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(controller.IsPlayable, Is.True);
            Assert.That(controller.CurrentCheckpointIndex, Is.EqualTo(0));
            Assert.That(controller.PlayerX, Is.EqualTo(rollingRespawn.LatestSafePosition.x).Within(0.25f));
            Assert.That(controller.PlayerX,
                Is.GreaterThan(Phase02VolcanoEscapeController.GetSectionBoundary(1) + 40f),
                "Respawn incorrectly returned to the beginning of Section 2.");
            Assert.That(services.Music.GetEventCount("Phase2PlayerFailed"), Is.EqualTo(1));
            Assert.That(services.Music.GetEventCount("Phase2PlayerReset"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_FinalControlledJumpTransitionsToPhase03ExactlyOnce()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller = Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            Assert.That(controller, Is.Not.Null);

            float finalX = Phase02VolcanoEscapeController.AuthoredRouteLength - 2f;
            controller.Motor.TeleportAndReset(new Vector3(finalX, controller.SurfaceHeightAt(finalX), 0f));
            controller.Motor.SetVelocityForAutomation(Vector3.up * 5f);
            yield return new WaitForFixedUpdate();
            yield return null;

            float deadline = Time.realtimeSinceStartup + 3f;
            while (SceneManager.GetActiveScene().name == "Phase02" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Phase03"));
            Assert.That(services.Music.GetEventCount("Phase2FinalJumpStarted"), Is.EqualTo(1));
            Assert.That(services.Music.GetEventCount("Phase2Completed"), Is.EqualTo(1));
            yield return null;
            Assert.That(services.Music.GetEventCount("Phase2Completed"), Is.EqualTo(1));
        }
    }
}
