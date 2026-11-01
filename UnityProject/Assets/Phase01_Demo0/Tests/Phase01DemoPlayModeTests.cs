using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Phase01.MovementSandbox;
using ProjectReturn.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    public sealed class Phase01DemoPlayModeTests
    {
        [UnityTest]
        // The full four-branch loop is ~280 s of game time; wall-clock timeouts must tolerate a loaded
        // development machine (video calls, browsers) rather than failing the route spuriously.
        [Timeout(1500000)]
        public IEnumerator CompleteCrossLoop_PassesAllAcceptanceChecks()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01AcceptanceDriver driver = Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            Assert.That(driver, Is.Not.Null, "Acceptance driver is missing from Phase01_Demo0.");
            MusicDirector music = ProjectServicesRoot.EnsureExists().Music;
            music.ResetRecordedEventsForTests();

            yield return driver.Run();
            yield return null;
            Assert.That(driver.Passed, Is.True, driver.Report);
            Phase01PacingMonitor pacing = Object.FindAnyObjectByType<Phase01PacingMonitor>();
            Assert.That(pacing, Is.Not.Null);
            Assert.That(pacing.PhaseEntryCount, Is.EqualTo(1));
            Assert.That(pacing.PickupRecordCount, Is.EqualTo(4));
            Assert.That(pacing.DepositRecordCount, Is.EqualTo(4));
            Assert.That(pacing.CompletedSplitCount, Is.EqualTo(4));
            Assert.That(pacing.FinalCompletionCount, Is.EqualTo(1));
            Debug.Log($"[Phase01Pacing] total={pacing.FinalTotalSeconds:F1}s splits={pacing.SplitSummary}");
            Debug.Log($"[DiagCleanRun] branches={pacing.CleanRunSummary} cleanBranches={pacing.CleanBranchCount} "
                + $"falls={pacing.TotalFalls} cleanRun={pacing.CleanRunCompleted}");
            Assert.That(music.GetEventCount("ParticlePickedUp"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ParticleDepositStarted"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ParticleDeposited"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ViewRotationStarted"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ViewRotationCompleted"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ActiveBranchChanged"), Is.EqualTo(4));
            Assert.That(music.GetEventCount("ReceiverCompleted"), Is.EqualTo(1));
            Assert.That(music.GetEventCount("Branch2RouteTransformationStarted"), Is.EqualTo(1));
            Assert.That(music.GetEventCount("Branch4ControlledCollapseActivated"), Is.EqualTo(1));
            int supportActivations = Object.FindAnyObjectByType<Phase01DemoController>()
                .Branch3FadingSupports.Sum(support => support.ActivationCount);
            Assert.That(music.GetEventCount("Branch3FadingSupportActivated"), Is.EqualTo(supportActivations));

            Phase01FlowBridge bridge = Object.FindAnyObjectByType<Phase01FlowBridge>();
            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge.Continue(), Is.True);
            Assert.That(music.GetEventCount("Phase01Completed"), Is.EqualTo(1));
            Assert.That(bridge.Continue(), Is.False);
            Assert.That(music.GetEventCount("Phase01Completed"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PlayerHud_RepresentsRequiredStatesAndControls()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01DebugOverlay hud = Object.FindAnyObjectByType<Phase01DebugOverlay>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.ProductionHudAvailable, Is.True);

            hud.RefreshHudState();
            Assert.That(hud.ObjectiveText, Is.EqualTo("Reach the yellow particle"));
            Assert.That(hud.BranchProgressText, Is.EqualTo("Branch 1 / 4"));
            Assert.That(hud.ReceiverProgressText, Is.EqualTo("Receiver charge: 0 / 4"));
            Assert.That(hud.CarryStatusText, Is.EqualTo("Particle: not carried"));
            Assert.That(hud.DepositPromptVisible, Is.False);
            Assert.That(hud.ControlsVisible, Is.True);
            Assert.That(hud.ControlsText, Does.Contain("Move"));
            Assert.That(hud.ControlsText, Does.Contain("Space"));
            Assert.That(hud.ControlsText, Does.Contain("E"));

            SetAutoProperty(controller, "CarriedParticle", controller.Particles[0]);
            hud.RefreshHudState();
            Assert.That(hud.ObjectiveText, Is.EqualTo("Return the particle to the radio"));
            Assert.That(hud.CarryStatusText, Does.Contain("carrying Branch 1"));
            Assert.That(hud.DepositPromptVisible, Is.False);

            SetAutoProperty(player, "CurrentScalar", controller.RadioStopDistance);
            SetPrivateField(controller.CameraRig, "radioLocked", true);
            SetPrivateField(controller.CameraRig, "transitionManaged", false);
            SetAutoProperty(controller.CameraRig, "State", Phase01CameraRig.CameraState.RadioLocked);
            SetAutoProperty(controller.CameraRig, "IsFramingStable", true);
            hud.RefreshHudState();
            Assert.That(hud.DepositPromptVisible, Is.True);
            Assert.That(hud.ContextPromptText, Is.EqualTo("Press E to deposit"));

            SetPrivateField(controller, "inputLocked", true);
            hud.RefreshHudState();
            Assert.That(hud.DepositPromptVisible, Is.False);
            Assert.That(hud.TransitionMessageVisible, Is.True);
            Assert.That(hud.ControlsVisible, Is.False);
            Assert.That(hud.ContextPromptText, Does.Contain("Transition"));

            SetPrivateField(controller, "inputLocked", false);
            SetAutoProperty(controller, "ReceiverCharge", 4);
            SetAutoProperty(controller, "DemoComplete", true);
            hud.RefreshHudState();
            Assert.That(hud.CompletionBannerVisible, Is.True);
            Assert.That(hud.ObjectiveText, Is.EqualTo("Phase 01 complete"));
            Assert.That(hud.ReceiverProgressText, Is.EqualTo("Receiver charge: 4 / 4"));
            Assert.That(hud.DepositPromptVisible, Is.False);
            Assert.That(hud.TransitionMessageVisible, Is.False);
            Assert.That(hud.ControlsVisible, Is.False);
            Assert.That(hud.ObjectiveText, Does.Not.Contain("FPS"));
            Assert.That(hud.ObjectiveText, Does.Not.Contain("GC"));
        }

        [UnityTest]
        public IEnumerator Branch2Rise_HeldOutwardInputStaysGroundedAndJumpClearsGapA()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            SetActiveBranchForTest(controller, 1);
            controller.CameraRig.SetYaw(controller.CameraRig.TurnAngle > 0f ? 90f : -90f);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(2.6f, 0f));
            player.SnapToActiveLane();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(player.IsGrounded, Is.True);

            float axis = player.InputForScalarDirection(1f);
            Assert.That(Mathf.Abs(axis), Is.GreaterThan(0.5f));
            player.SetSimulatedAxis(axis);

            bool lostGroundOnRise = false;
            float approachDeadline = Time.realtimeSinceStartup + 4f;
            while (player.PhysicalScalar < 4.1f && Time.realtimeSinceStartup < approachDeadline)
            {
                player.SetSimulatedAxis(axis);
                yield return null;
                if (player.PhysicalScalar >= 2.65f
                    && player.PhysicalScalar <= 4.1f
                    && !player.IsGrounded)
                    lostGroundOnRise = true;
            }

            Assert.That(player.PhysicalScalar, Is.GreaterThanOrEqualTo(4f),
                "The test did not reach the Branch 2 first-gap takeoff area.");
            Assert.That(lostGroundOnRise, Is.False,
                "The motor lost ground contact while running along the Branch 2 approach terrace.");
            Assert.That(player.IsGrounded, Is.True,
                "The player was airborne before the Gap A jump was pressed.");

            int acceptedBefore = player.AcceptedJumpCount;
            int resetsBefore = player.SoftResetCount;
            player.SetSimulatedAxis(axis);
            player.SimulateJumpPress(true);

            bool sawAirborne = false;
            bool clearedGap = false;
            float jumpStartedAt = Time.realtimeSinceStartup;
            float jumpDeadline = jumpStartedAt + 2.5f;
            while (!clearedGap && Time.realtimeSinceStartup < jumpDeadline)
            {
                player.SetSimulatedAxis(axis);
                if (Time.realtimeSinceStartup - jumpStartedAt >= 0.5f)
                    player.SimulateJumpRelease();
                sawAirborne |= !player.IsGrounded;
                clearedGap |= player.PhysicalScalar >= 7.3f;
                yield return null;
            }

            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Assert.That(player.AcceptedJumpCount, Is.EqualTo(acceptedBefore + 1),
                "The grounded held-movement jump was not accepted.");
            Assert.That(sawAirborne, Is.True);
            Assert.That(clearedGap, Is.True,
                "The held-movement jump did not clear Branch 2 Gap A.");
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore));
        }

        [UnityTest]
        public IEnumerator Branch2TransformedRoute_HasSixVariedPhysicalGaps()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Assert.That(controller, Is.Not.Null);
            Phase01BranchTerrain terrain = controller.BranchTerrains[1];
            Phase01Branch2RouteTransformation route = terrain.GetComponent<Phase01Branch2RouteTransformation>();
            Assert.That(route, Is.Not.Null);

            yield return route.Activate();

            Assert.That(route.ReturnGapCount, Is.EqualTo(6));
            Assert.That(route.GetReturnGap(0), Is.EqualTo(new Vector2(120f, 121.8f)));
            Assert.That(route.GetReturnGap(1), Is.EqualTo(new Vector2(100f, 102f)));
            Assert.That(route.GetReturnGap(2), Is.EqualTo(new Vector2(78f, 79.6f)));
            Assert.That(route.GetReturnGap(3), Is.EqualTo(new Vector2(56f, 57.8f)));
            Assert.That(route.GetReturnGap(4), Is.EqualTo(new Vector2(34f, 36f)));
            Assert.That(route.GetReturnGap(5), Is.EqualTo(new Vector2(14f, 15.6f)));

            for (float scalar = 1.4f; scalar <= 176f; scalar += 0.2f)
            {
                Vector2[] gaps = Enumerable.Range(0, route.ReturnGapCount)
                    .Select(route.GetReturnGap)
                    .ToArray();
                bool nearGapEdge = gaps.Any(gap => Mathf.Abs(scalar - gap.x) <= 0.12f
                    || Mathf.Abs(scalar - gap.y) <= 0.12f);
                if (nearGapEdge)
                    continue;
                Vector3 origin = controller.RadioPosition + terrain.OutwardDirection * scalar + Vector3.up * 3f;
                bool hasSupport = Physics.Raycast(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore);
                bool inGap = gaps.Any(gap => scalar > gap.x && scalar < gap.y);
                Assert.That(hasSupport, Is.EqualTo(!inGap), $"Unexpected transformed support state at scalar {scalar:F1}.");
            }
        }

        [UnityTest]
        public IEnumerator ExtendedPacingLayout_UsesApprovedScaleJumpCountsAndRecoverySections()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller.OuterDistance, Is.EqualTo(176f).Within(0.01f));
            Assert.That(controller.BranchTerrains.All(terrain => terrain.OuterScalar == 178f), Is.True);
            Assert.That(player.PhysicalScalar, Is.InRange(1.4f, 2.5f));
            float[] expectedParticleScalars = { 109.1f, 131f, 165f, 174f };
            foreach (Phase01Particle particle in controller.Particles)
            {
                float scalar = Vector3.Dot(
                    particle.transform.position - controller.RadioPosition,
                    controller.BranchTerrains[particle.BranchIndex].OutwardDirection);
                Assert.That(scalar, Is.EqualTo(expectedParticleScalars[particle.BranchIndex]).Within(0.01f));
            }

            AssertTerrainGapsDerived(controller, 0);
            AssertTerrainGapsDerived(controller, 1);
            AssertTerrainGapsDerived(controller, 2);
            AssertTerrainGapsDerived(controller, 3);

            Assert.That(controller.BranchTerrains[0].GetComponentsInChildren<Phase01FadingSupport>(true), Is.Empty);
            Assert.That(controller.BranchTerrains[0].GetComponentsInChildren<Phase01CollapsibleSegment>(true), Is.Empty);
            Assert.That(
                controller.BranchTerrains[0].GetComponentsInChildren<Phase01MovingPlatform>(true),
                Has.Length.EqualTo(2),
                "Branch 1 authors exactly one riser lift and one ferry.");
            Assert.That(
                Object.FindObjectsByType<Phase01SweeperGate>(FindObjectsSortMode.None),
                Has.Length.EqualTo(2),
                "Branch 1 authors one patrol door plus one sweeping bar.");
            Assert.That(controller.BranchTerrains[2].GetComponentsInChildren<Phase01FadingSupport>(true), Has.Length.EqualTo(3));
            Assert.That(controller.Branch4Collapse.SegmentCount, Is.EqualTo(4));

            Vector2[] branch3HazardGaps =
            {
                new Vector2(80f, 81.8f), new Vector2(83.8f, 85.6f),
                new Vector2(87.6f, 89.4f), new Vector2(91.4f, 93.2f)
            };
            foreach (Vector2 gap in branch3HazardGaps)
                AssertClearJumpGap(controller, 2, gap);

            Assert.That(controller.Branch4Collapse.Activate(), Is.True);
            foreach (Vector2 gap in Branch4ReturnGaps(controller.Branch4Collapse))
                AssertClearJumpGap(controller, 3, gap);

            for (int branch = 0; branch <= 3; branch++)
            {
                foreach (float scalar in Phase01Branch1Envelope.AuthoredStandingSamples(branch))
                    AssertSupport(controller, branch, scalar);
            }


            Assert.That(
                Phase01Branch1Envelope.AuthoredJumpGapCount(controller),
                Is.EqualTo(11),
                "Branch 1 authors seventeen deliberate jump gaps plus two platform-gated voids.");
            Assert.That(
                new[]
                {
                    Phase01Branch1Envelope.AuthoredJumpGapCount(0),
                    Phase01Branch1Envelope.AuthoredJumpGapCount(1),
                    Phase01Branch1Envelope.AuthoredJumpGapCount(2),
                    Phase01Branch1Envelope.AuthoredJumpGapCount(3)
                },
                Is.EqualTo(new[] { 11, 21, 28, 27 }),
                "The difficulty ladder should author 11 / 21 / 28 / 27 jump gaps on branches 1-4: branch 1 "
                + "spends its length on mechanics (a riser, a ferry and two air currents) rather than on "
                + "jump distance, and the later branches carry more jumps as well as their own mechanics.");
        }

        /// <summary>
        /// Asserts every authored gap on a branch exists as a real void with no hidden bridge, derived
        /// from the runtime profile so the check cannot rot when the route is retuned.
        /// </summary>
        private static void AssertTerrainGapsDerived(Phase01DemoController controller, int branch)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
            foreach (Vector2 gap in Phase01Branch1Envelope.AuthoredGapSpans(branch))
            {
                if (Phase01Branch1Envelope.IsPlatformVoid(branch, gap))
                    continue; // crossed by an authored platform or support, not by a jump
                float center = (gap.x + gap.y) * 0.5f;
                Assert.That(terrain.IsGapAt(center), Is.True,
                    $"Branch {branch + 1} is missing its authored gap at {center:F2}.");
                float probeHeight = Mathf.Max(
                    terrain.SampleHeight(gap.x - 0.05f),
                    terrain.SampleHeight(gap.y + 0.05f));
                Vector3 origin = controller.RadioPosition
                    + terrain.OutwardDirection * center
                    + Vector3.up * (probeHeight + 2.5f);
                Assert.That(
                    Physics.Raycast(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore),
                    Is.False,
                    $"Branch {branch + 1} has a hidden bridge at gap {gap}.");
            }
        }

        private static void AssertTerrainGaps(Phase01DemoController controller, int branch, float[] centers)
        {
            foreach (float center in centers)
            {
                Assert.That(controller.BranchTerrains[branch].IsGapAt(center), Is.True,
                    $"Branch {branch + 1} is missing its pacing gap at {center:F2}.");
                Vector2 gap = FindGap(controller.BranchTerrains[branch], center);
                if (branch <= 2 && Phase01Branch1Envelope.IsPlatformVoid(branch, gap))
                    continue; // held open on purpose: crossed by the authored riser or ferry, not a jump
                Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
                float probeHeight = Mathf.Max(
                    terrain.SampleHeight(gap.x - 0.05f),
                    terrain.SampleHeight(gap.y + 0.05f));
                Vector3 origin = controller.RadioPosition
                    + terrain.OutwardDirection * ((gap.x + gap.y) * 0.5f)
                    + Vector3.up * (probeHeight + 2.5f);
                Assert.That(
                    Physics.Raycast(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore),
                    Is.False,
                    $"Branch {branch + 1} has a hidden bridge at gap {gap}.");
            }
        }

        private static Vector2 FindGap(Phase01BranchTerrain terrain, float center)
        {
            float inner = center;
            float outer = center;
            while (inner > 0f && terrain.IsGapAt(inner - 0.025f))
                inner -= 0.025f;
            while (outer < terrain.OuterScalar && terrain.IsGapAt(outer + 0.025f))
                outer += 0.025f;
            return new Vector2(inner, outer);
        }

        private static void AssertClearJumpGap(Phase01DemoController controller, int branch, Vector2 gap)
        {
            float width = gap.y - gap.x;
            Assert.That(width, Is.InRange(1.33f, 2.07f));
            Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
            float probeHeight = Mathf.Max(
                terrain.SampleHeight(gap.x - 0.05f),
                terrain.SampleHeight(gap.y + 0.05f));
            Vector3 origin = controller.RadioPosition
                + terrain.OutwardDirection * ((gap.x + gap.y) * 0.5f)
                + Vector3.up * (probeHeight + 2.5f);
            Assert.That(Physics.Raycast(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore), Is.False,
                $"Branch {branch + 1} has a hidden bridge at gap {gap}.");
        }

        private static void AssertSupport(Phase01DemoController controller, int branch, float scalar)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
            float probeHeight = Mathf.Max(terrain.SampleHeight(scalar), -0.5f);
            Vector3 origin = controller.RadioPosition
                + terrain.OutwardDirection * scalar
                + Vector3.up * (probeHeight + 2.5f);
            Assert.That(Physics.Raycast(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore), Is.True,
                $"Branch {branch + 1} lacks stable recovery support at {scalar:F1}.");
        }

        private static Vector2[] Branch4ReturnGaps(Phase01Branch4ControlledCollapse route)
        {
            Phase01CollapsibleSegment[] segments = route.Segments;
            return new[]
            {
                new Vector2(segments[0].OuterScalar, route.OuterApproachInnerScalar),
                new Vector2(segments[1].OuterScalar, segments[0].InnerScalar),
                new Vector2(segments[2].OuterScalar, segments[1].InnerScalar),
                new Vector2(segments[3].OuterScalar, segments[2].InnerScalar),
                new Vector2(route.FinalApproachOuterScalar, segments[3].InnerScalar)
            };
        }

        private static void SetActiveBranchForTest(Phase01DemoController controller, int branchIndex)
        {
            SetAutoProperty(controller, "ActiveBranchIndex", branchIndex);
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Backing field for {propertyName} was not found.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Private field {fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
