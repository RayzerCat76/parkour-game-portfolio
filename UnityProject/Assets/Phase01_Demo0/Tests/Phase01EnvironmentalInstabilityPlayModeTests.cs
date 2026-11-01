using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    public sealed class Phase01EnvironmentalInstabilityPlayModeTests
    {
        [UnityTest]
        public IEnumerator Branch3_ExactlyThreeSupportsUseFixedReusableTimingAndSafeReset()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            SetAutoProperty(controller, "ActiveBranchIndex", 2);

            Phase01FadingSupport[] supports = controller.Branch3FadingSupports;
            Assert.That(supports, Has.Length.EqualTo(3));
            Assert.That(supports.Select(support => support.SupportOrder), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(supports.All(support => support.IsSolid && !support.IsCycleActive), Is.True);

            SetAutoProperty(controller, "ReceiverCharge", 1);
            Phase01Particle particle = controller.Particles[2];
            float pickupScalar = Vector3.Dot(
                particle.transform.position - controller.RadioPosition,
                controller.ActiveDirection);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(pickupScalar, 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            if (controller.CarriedParticle == null)
                Assert.That(controller.TryPickup(particle), Is.True);
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle));

            // Derive a safe approach scalar on the landing before the first support: the authored
            // profile moves when the branch is retuned, so hardcoded scalars rot.
            player.SetPositionDuringTransition(controller.PointOnActiveLane(
                SafeApproachScalarBefore(controller, 2, supports[0].InnerScalar), 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.CaptureCurrentRouteCheckpoint(), Is.True);
            float latestSafeScalar = player.PhysicalScalar;

            Phase01FadingSupport support = supports[0];
            Assert.That(support.BeginCycle(), Is.True);
            Assert.That(support.BeginCycle(), Is.False);
            Assert.That(support.ActivationCount, Is.EqualTo(1));

            yield return new WaitForSeconds(0.74f);
            Assert.That(support.IsWarning, Is.False);
            Assert.That(support.IsSolid, Is.True);
            yield return WaitUntil(() => support.IsWarning, 0.25f);
            Assert.That(support.LastWarningElapsed, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(support.IsSolid, Is.True);
            Assert.That(support.BeginCycle(), Is.False);
            Assert.That(support.ActivationCount, Is.EqualTo(1));

            yield return WaitUntil(() => !support.IsSolid, 0.4f);
            Assert.That(support.LastCollisionDisabledElapsed, Is.GreaterThanOrEqualTo(1.1f));
            player.SetPositionDuringTransition(
                controller.PointOnActiveLane((support.InnerScalar + support.OuterScalar) * 0.5f, -5f));
            Physics.SyncTransforms();
            player.ResetToCheckpoint();
            Physics.SyncTransforms();
            Assert.That(player.PhysicalScalar, Is.EqualTo(latestSafeScalar).Within(0.05f));
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle));
            Assert.That(controller.ReceiverCharge, Is.EqualTo(1));

            yield return WaitUntil(() => !support.IsCycleActive, 0.6f);
            Assert.That(support.IsSolid, Is.True);
            Assert.That(support.LastRestoreElapsed, Is.GreaterThanOrEqualTo(1.5f));
            Assert.That(support.LastRestoreElapsed, Is.LessThan(1.65f));
            Assert.That(support.BeginCycle(), Is.True);
            Assert.That(support.ActivationCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Branch3_LeftToRightCrossing_ActivatesEachSupportExactlyOnce()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch3(controller, player, 79f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            yield return TraverseBranch3(
                player,
                1f,
                94f,
                new[] { 79.3f, 83.1f, 86.9f, 90.7f });

            Assert.That(
                controller.Branch3FadingSupports.Select(support => support.ActivationCount),
                Is.EqualTo(new[] { 1, 1, 1 }));
        }

        [UnityTest]
        public IEnumerator Branch3_RightToLeftCrossing_ActivatesEachSupportExactlyOnce()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch3(controller, player, 94f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            yield return TraverseBranch3(
                player,
                -1f,
                79.2f,
                new[] { 93.9f, 90.1f, 86.3f, 82.5f });

            Assert.That(
                controller.Branch3FadingSupports.Select(support => support.ActivationCount),
                Is.EqualTo(new[] { 1, 1, 1 }));
        }

        [UnityTest]
        public IEnumerator Branch3_RestoredSupportRearmsOnlyAfterPlayerExit()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch3(controller, player, 79f);
            yield return new WaitForFixedUpdate();

            Phase01FadingSupport support = controller.Branch3FadingSupports[0];
            Vector3 supportTop = support.transform.position
                + support.transform.up * (support.transform.lossyScale.y * 0.5f + 0.03f);
            player.SetPositionDuringTransition(supportTop);
            Physics.SyncTransforms();
            yield return WaitUntil(() => support.ActivationCount == 1, 1f);

            Behaviour action = player.GetComponent("Phase01ParkourMotor") as Behaviour;
            if (action != null)
                action.enabled = false;
            player.enabled = false;
            Rigidbody body = player.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.position = supportTop;
            Physics.SyncTransforms();

            yield return WaitUntil(() => !support.IsCycleActive, 1.7f);
            yield return new WaitForFixedUpdate();
            Assert.That(support.ActivationCount, Is.EqualTo(1));
            Assert.That(support.IsArmed, Is.False,
                "Restoration rearmed while the player still occupied the activation area.");

            body.position = controller.PointOnActiveLane(79f, 0f);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(support.IsArmed, Is.True);
            Assert.That(support.BeginCycle(), Is.True);
            Assert.That(support.ActivationCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Branch3_FourClearGapsHaveNoBridgeAndGroundRunFalls()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch3(controller, player, 79f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Phase01FadingSupport[] supports = controller.Branch3FadingSupports;
            Vector2[] gaps =
            {
                new Vector2(80f, supports[0].InnerScalar),
                new Vector2(supports[0].OuterScalar, supports[1].InnerScalar),
                new Vector2(supports[1].OuterScalar, supports[2].InnerScalar),
                new Vector2(supports[2].OuterScalar, 93.2f)
            };
            foreach (Vector2 gap in gaps)
            {
                Assert.That(gap.y - gap.x, Is.EqualTo(1.8f).Within(0.01f));
                for (float scalar = gap.x + 0.15f; scalar < gap.y - 0.14f; scalar += 0.3f)
                {
                    Vector3 origin = controller.RadioPosition
                        + controller.ActiveDirection * scalar
                        + Vector3.up * 3f;
                    Assert.That(
                        Physics.Raycast(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore),
                        Is.False,
                        $"A continuous collider bridges Branch 3 at scalar {scalar:F2}.");
                }
            }

            int resetsBefore = player.SoftResetCount;
            int acceptedJumpsBefore = player.AcceptedJumpCount;
            float maximumScalar = player.PhysicalScalar;
            player.SetSimulatedAxis(player.InputForScalarDirection(1f));
            float deadline = Time.realtimeSinceStartup + 4f;
            while (player.SoftResetCount == resetsBefore && Time.realtimeSinceStartup < deadline)
            {
                maximumScalar = Mathf.Max(maximumScalar, player.PhysicalScalar);
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore + 1));
            Assert.That(player.AcceptedJumpCount, Is.EqualTo(acceptedJumpsBefore));
            Assert.That(maximumScalar, Is.LessThan(supports[0].OuterScalar));
            Assert.That(supports.All(support => support.ActivationCount == 0), Is.True);
        }

        [UnityTest]
        public IEnumerator Branch4_PrePickupRouteIsContinuousAndPickupDisablesGapFillersOnce()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch4(controller, player, 87f);
            yield return new WaitForFixedUpdate();

            Phase01Branch4ControlledCollapse route = controller.Branch4Collapse;
            Assert.That(route.SegmentCount, Is.EqualTo(4));
            Assert.That(route.OutboundGapFillerCount, Is.EqualTo(5));
            Assert.That(route.AreOutboundGapFillersEnabled, Is.True);
            Assert.That(route.Segments.Select(segment => segment.CollapseOrder), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(route.Segments.Select(segment => segment.InnerScalar), Is.EqualTo(new[] { 101.2f, 97.4f, 93.6f, 89.8f }));
            for (float scalar = 88.1f; scalar < 104.9f; scalar += 0.25f)
            {
                Vector3 ray = controller.RadioPosition
                    + controller.ActiveDirection * scalar
                    + Vector3.up * 3f;
                Assert.That(Physics.Raycast(ray, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore), Is.True,
                    $"The pre-pickup Branch 4 route lacks continuous support at {scalar:F2}.");
            }

            yield return ActivateBranch4Pickup(controller, player);
            Assert.That(route.IsActivated, Is.True);
            Assert.That(route.ActivationCount, Is.EqualTo(1));
            Assert.That(route.AreOutboundGapFillersEnabled, Is.False);
            Assert.That(route.OutboundGapFillerDisableCount, Is.EqualTo(1));
            Assert.That(route.Activate(), Is.False);
            Assert.That(route.ActivationCount, Is.EqualTo(1));
            Assert.That(route.OutboundGapFillerDisableCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Branch4_ActivatedRouteHasFiveClearJumpGapsAndGroundRunFalls()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch4(controller, player, 174f);
            yield return ActivateBranch4Pickup(controller, player);

            Phase01Branch4ControlledCollapse route = controller.Branch4Collapse;
            foreach (Vector2 gap in Branch4ReturnGaps(route))
            {
                Assert.That(gap.y - gap.x, Is.EqualTo(1.8f).Within(0.01f));
                for (float scalar = gap.x + 0.15f; scalar < gap.y - 0.14f; scalar += 0.3f)
                {
                    Vector3 origin = controller.RadioPosition
                        + controller.ActiveDirection * scalar
                        + Vector3.up * 3f;
                    Assert.That(
                        Physics.Raycast(origin, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore),
                        Is.False,
                        $"A collider bridges the activated Branch 4 gap at {scalar:F2}.");
                }
            }

            player.SetPositionDuringTransition(controller.PointOnActiveLane(106f, 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.CaptureCurrentRouteCheckpoint(), Is.True);
            int resetsBefore = player.SoftResetCount;
            int jumpsBefore = player.AcceptedJumpCount;
            player.SetSimulatedAxis(player.InputForScalarDirection(-1f));
            float deadline = Time.realtimeSinceStartup + 4f;
            while (player.SoftResetCount == resetsBefore && Time.realtimeSinceStartup < deadline)
                yield return null;
            player.SetSimulatedAxis(0f);
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore + 1));
            Assert.That(player.AcceptedJumpCount, Is.EqualTo(jumpsBefore));
            Assert.That(route.AreOutboundGapFillersEnabled, Is.False);
            Assert.That(player.PhysicalScalar, Is.GreaterThan(105f));
        }

        [UnityTest]
        public IEnumerator Branch4_ApprovedMotorCrossesWithFiveJumpsAndSegmentsWarnInOrder()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch4(controller, player, 174f);
            yield return ActivateBranch4Pickup(controller, player);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(106f, 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            yield return TraverseBranch4(player, 87f, new[] { 105.7f, 101.9f, 98.1f, 94.3f, 90.5f });
            Phase01Branch4ControlledCollapse route = controller.Branch4Collapse;
            Assert.That(route.NextCollapseOrder, Is.EqualTo(4));
            Assert.That(route.Segments.Select(segment => segment.ContactActivationCount), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            yield return WaitUntil(() => route.Segments.All(segment => segment.IsCollapsed), 1.2f);
            Assert.That(route.Segments.All(segment => segment.LastWarningDuration >= 0.9f), Is.True);
            Assert.That(route.Segments.Select(segment => segment.CollapseOrder), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            // Branch 4's approach is elevation-led since TASK 006: assert the landing matches its
            // authored profile height instead of assuming a flat zero-height route.
            foreach (float scalar in Phase01Branch1Envelope.AuthoredStandingSamples(3).Take(6))
            {
                Vector3 ray = controller.PointOnActiveLane(scalar, 3f);
                Assert.That(Physics.Raycast(ray, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(
                    controller.ActiveSurfaceHeight(scalar),
                    Is.EqualTo(controller.BranchTerrains[3].SampleHeight(scalar)).Within(0.001f),
                    $"Branch 4 approach surface at {scalar:F0} must match its authored profile.");
            }
        }

        [UnityTest]
        public IEnumerator Branch4_SoftResetPreservesStateAndRestoresArmedReturnSequence()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            PrepareBranch4(controller, player, 174f);
            yield return ActivateBranch4Pickup(controller, player);

            Phase01Branch4ControlledCollapse route = controller.Branch4Collapse;
            Phase01Particle particle = controller.Particles[3];
            SetAutoProperty(controller, "ReceiverCharge", 2);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(106f, 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.CaptureCurrentRouteCheckpoint(), Is.True);
            float latestSafeScalar = player.PhysicalScalar;
            Phase01CollapsibleSegment[] originalSequence = route.Segments.ToArray();
            Phase01CollapsibleSegment first = route.Segments[0];
            Assert.That(route.Segments[2].BeginCollapse(), Is.False);
            Assert.That(first.BeginCollapse(), Is.True);
            Assert.That(first.BeginCollapse(), Is.False);
            yield return new WaitForSeconds(0.84f);
            Assert.That(first.IsSolid, Is.True);
            Assert.That(first.IsWarning, Is.True);
            yield return WaitUntil(() => first.IsCollapsed, 0.3f);
            Assert.That(first.LastWarningDuration, Is.GreaterThanOrEqualTo(0.9f));

            player.SetPositionDuringTransition(controller.PointOnActiveLane(102.7f, -5f));
            player.ResetToCheckpoint();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Assert.That(player.PhysicalScalar, Is.EqualTo(latestSafeScalar).Within(0.05f));
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle));
            Assert.That(controller.ReceiverCharge, Is.EqualTo(2));
            Assert.That(controller.ActiveBranchIndex, Is.EqualTo(3));
            Assert.That(route.IsActivated, Is.True);
            Assert.That(route.ActivationCount, Is.EqualTo(1));
            Assert.That(route.AreOutboundGapFillersEnabled, Is.False);
            Assert.That(route.OutboundGapFillerDisableCount, Is.EqualTo(1));
            Assert.That(route.Segments, Is.EqualTo(originalSequence));
            Assert.That(route.NextCollapseOrder, Is.EqualTo(0));
            Assert.That(route.Segments.All(segment => segment.IsSolid && !segment.IsWarning && !segment.IsCollapsed), Is.True);
            Assert.That(route.Segments[2].BeginCollapse(), Is.False);
            Assert.That(first.BeginCollapse(), Is.True);
        }

        private static void PrepareBranch4(
            Phase01DemoController controller,
            Phase01PlayerMotor player,
            float scalar)
        {
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            SetAutoProperty(controller, "ActiveBranchIndex", 3);
            controller.CameraRig.SetYaw(-270f);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(scalar, 0f));
            Physics.SyncTransforms();
        }

        private static IEnumerator ActivateBranch4Pickup(
            Phase01DemoController controller,
            Phase01PlayerMotor player)
        {
            Phase01Particle particle = controller.Particles[3];
            player.SetPositionDuringTransition(controller.PointOnActiveLane(174f, 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            if (controller.CarriedParticle == null)
                Assert.That(controller.TryPickup(particle), Is.True);
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle));
            Assert.That(controller.Branch4Collapse.IsActivated, Is.True);
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

        private static IEnumerator TraverseBranch4(
            Phase01PlayerMotor player,
            float targetScalar,
            float[] takeoffScalars)
        {
            int resetsBefore = player.SoftResetCount;
            int jumpsBefore = player.AcceptedJumpCount;
            int nextJump = 0;
            bool holdingJump = false;
            float releaseAt = 0f;
            float axis = player.InputForScalarDirection(-1f);
            player.SetSimulatedAxis(axis);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (player.PhysicalScalar > targetScalar && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(axis);
                if (holdingJump && Time.realtimeSinceStartup >= releaseAt)
                {
                    player.SimulateJumpRelease();
                    holdingJump = false;
                }
                if (!holdingJump
                    && nextJump < takeoffScalars.Length
                    && player.IsGrounded
                    && player.PhysicalScalar <= takeoffScalars[nextJump])
                {
                    player.SimulateJumpPress(true);
                    holdingJump = true;
                    releaseAt = Time.realtimeSinceStartup + 0.5f;
                    nextJump++;
                }
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Assert.That(nextJump, Is.EqualTo(takeoffScalars.Length));
            Assert.That(player.AcceptedJumpCount, Is.EqualTo(jumpsBefore + takeoffScalars.Length));
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore));
            Assert.That(player.PhysicalScalar, Is.LessThanOrEqualTo(targetScalar));
        }

        /// <summary>
        /// Solid scalar on the landing just before <paramref name="before"/> with half a metre of solid
        /// ground either side, so a route checkpoint can be captured there.
        /// </summary>
        private static float SafeApproachScalarBefore(
            Phase01DemoController controller,
            int branch,
            float before)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
            for (float scalar = before - 0.1f; scalar > 2f; scalar -= 0.1f)
            {
                if (terrain.IsGapAt(scalar) || terrain.SampleHeight(scalar) < -1f)
                    continue;
                if (!terrain.IsGapAt(scalar - 0.5f) && !terrain.IsGapAt(scalar + 0.5f))
                    return scalar;
            }
            Assert.Fail($"No safe Branch {branch + 1} approach scalar was found before {before:F1}.");
            return before;
        }

        private static IEnumerator WaitUntil(System.Func<bool> predicate, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(predicate(), Is.True, "Timed out waiting for the deterministic hazard state.");
        }

        private static void PrepareBranch3(
            Phase01DemoController controller,
            Phase01PlayerMotor player,
            float scalar)
        {
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            SetAutoProperty(controller, "ActiveBranchIndex", 2);
            controller.CameraRig.SetYaw(-180f);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(scalar, 0f));
            Physics.SyncTransforms();
        }

        private static IEnumerator TraverseBranch3(
            Phase01PlayerMotor player,
            float scalarDirection,
            float targetScalar,
            float[] takeoffScalars)
        {
            int resetsBefore = player.SoftResetCount;
            int jumpsBefore = player.AcceptedJumpCount;
            int nextJump = 0;
            bool holdingJump = false;
            float releaseAt = 0f;
            float axis = player.InputForScalarDirection(scalarDirection);
            player.SetSimulatedAxis(axis);
            float deadline = Time.realtimeSinceStartup + 15f;
            while ((scalarDirection > 0f
                    ? player.PhysicalScalar < targetScalar
                    : player.PhysicalScalar > targetScalar)
                && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(axis);
                if (holdingJump && Time.realtimeSinceStartup >= releaseAt)
                {
                    player.SimulateJumpRelease();
                    holdingJump = false;
                }
                if (!holdingJump && nextJump < takeoffScalars.Length && player.IsGrounded)
                {
                    bool reachedTakeoff = scalarDirection > 0f
                        ? player.PhysicalScalar >= takeoffScalars[nextJump]
                        : player.PhysicalScalar <= takeoffScalars[nextJump];
                    if (reachedTakeoff)
                    {
                        player.SimulateJumpPress(true);
                        holdingJump = true;
                        releaseAt = Time.realtimeSinceStartup + 0.5f;
                        nextJump++;
                    }
                }
                yield return null;
            }

            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Assert.That(nextJump, Is.EqualTo(takeoffScalars.Length));
            Assert.That(player.AcceptedJumpCount, Is.EqualTo(jumpsBefore + takeoffScalars.Length));
            Assert.That(player.SoftResetCount, Is.EqualTo(resetsBefore));
            Assert.That(
                scalarDirection > 0f ? player.PhysicalScalar : -player.PhysicalScalar,
                Is.GreaterThanOrEqualTo(scalarDirection > 0f ? targetScalar : -targetScalar));
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Backing field for {propertyName} was not found.");
            field.SetValue(target, value);
        }
    }
}
