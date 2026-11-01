using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    public sealed class Phase01CameraPlayModeTests
    {
        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Fix5_EarlyLatchedCameraAndRuntimeMeshLifecycle_PassFullLoop()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01CameraRig rig = Object.FindAnyObjectByType<Phase01CameraRig>();
            Phase01AcceptanceDriver driver = Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);
            Assert.That(controller.HasValidReferences, Is.True);
            Assert.That(player.IsUsingSimulatedInput, Is.False, "Ordinary Play Mode auto-started automation.");
            // The lock only needs to cover the approach to the radio: the branches are parkour from the
            // first metre, so a window that reaches 11-14 m parks the camera while the player is already
            // jumping (see Phase01StartTraversalDiagTests).
            Assert.That(rig.RadioLockEnterDistance, Is.InRange(2.5f, 4f));
            Assert.That(rig.RadioLockExitDistance, Is.InRange(4f, 5.5f));
            Assert.That(rig.RadioLockExitDistance, Is.GreaterThan(rig.RadioLockEnterDistance));

            Mesh[] topMeshes = new Mesh[4];
            Mesh[] cliffMeshes = new Mesh[4];
            for (int i = 0; i < 4; i++)
            {
                Phase01BranchTerrain terrain = controller.BranchTerrains[i];
                Assert.That(terrain, Is.Not.Null);
                Assert.That(terrain.TopMesh, Is.Not.Null);
                Assert.That(terrain.CliffMesh, Is.Not.Null);
                topMeshes[i] = terrain.TopMesh;
                cliffMeshes[i] = terrain.CliffMesh;
            }

            int latchBefore = rig.LockSideLatchCount;
            int targetBefore = rig.LockTargetCalculationCount;
            // Branch 1 is elevation-led since TASK 005, so resolve a genuinely solid authored scalar
            // just outside the radio-lock enter distance instead of assuming one.
            player.SetPositionDuringTransition(
                controller.PointOnActiveLane(FirstSolidBranch1Scalar(controller, rig.RadioLockEnterDistance + 1f), 0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return ApproachBranch1RadioLock(player, rig, 6f);
            Assert.That(rig.State, Is.EqualTo(Phase01CameraRig.CameraState.RadioLockBlend));
            Assert.That(player.CurrentScalar, Is.GreaterThan(controller.RadioStopDistance + 1.5f));
            Assert.That(rig.LockSideLatchCount, Is.EqualTo(latchBefore + 1));
            Assert.That(rig.LockTargetCalculationCount, Is.EqualTo(targetBefore + 1));

            Vector3 start = rig.TransitionStartPosition;
            Vector3 target = rig.TransitionTargetPosition;
            Vector3 overall = target - start;
            Vector3 previous = rig.transform.position;
            float previousDistance = Vector3.Distance(previous, target);
            float previousRadioError = Mathf.Abs(RadioX(rig, controller) - rig.RadioViewportXTarget);
            while (rig.State == Phase01CameraRig.CameraState.RadioLockBlend)
            {
                yield return null;
                Vector3 current = rig.transform.position;
                Assert.That(Vector3.Distance(current, target), Is.LessThanOrEqualTo(previousDistance + 0.0002f));
                Assert.That(Vector3.Dot(current - previous, overall), Is.GreaterThanOrEqualTo(-0.00002f));
                float radioError = Mathf.Abs(RadioX(rig, controller) - rig.RadioViewportXTarget);
                Assert.That(radioError, Is.LessThanOrEqualTo(previousRadioError + 0.0002f));
                Assert.That(rig.CurrentLookAhead, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(rig.LockSideLatchCount, Is.EqualTo(latchBefore + 1));
                Assert.That(rig.LockTargetCalculationCount, Is.EqualTo(targetBefore + 1));
                previous = current;
                previousDistance = Vector3.Distance(current, target);
                previousRadioError = radioError;
            }

            Assert.That(rig.State, Is.EqualTo(Phase01CameraRig.CameraState.RadioLocked));
            Assert.That(rig.IsStableRadioLock, Is.True);
            float stableAt = Time.realtimeSinceStartup;
            Vector3 stableCamera = rig.transform.position;
            yield return MoveUntil(player, () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                player.InputForScalarDirection(-1f), 4f);
            float stableLead = Time.realtimeSinceStartup - stableAt;
            float maximumStableLead = (rig.RadioLockEnterDistance - controller.RadioStopDistance)
                / player.MoveSpeed + 0.25f;
            Assert.That(stableLead, Is.InRange(0f, maximumStableLead));
            Assert.That(Vector3.Distance(rig.transform.position, stableCamera), Is.LessThanOrEqualTo(rig.StableLockPositionTolerance));
            Assert.That(RadioX(rig, controller), Is.EqualTo(rig.RadioViewportXTarget).Within(0.002f));

            player.SetSimulatedAxis(0f);
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            controller = Object.FindAnyObjectByType<Phase01DemoController>();
            player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            rig = Object.FindAnyObjectByType<Phase01CameraRig>();
            driver = Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            for (int i = 0; i < 4; i++)
            {
                topMeshes[i] = controller.BranchTerrains[i].TopMesh;
                cliffMeshes[i] = controller.BranchTerrains[i].CliffMesh;
            }
            yield return driver.Run();
            Assert.That(driver.Passed, Is.True, driver.Report);
            Assert.That(controller.DemoComplete, Is.True);
            Assert.That(controller.ReceiverCharge, Is.EqualTo(4));
            Assert.That(controller.CameraQuarterTurns, Is.EqualTo(4));
            Phase01Branch2RouteTransformation branch2Transformation = controller.BranchTerrains[1]
                .GetComponent<Phase01Branch2RouteTransformation>();
            Assert.That(branch2Transformation, Is.Not.Null);
            Assert.That(branch2Transformation.IsTransformed, Is.True);
            Assert.That(branch2Transformation.UpperRouteVisible, Is.False);
            Assert.That(branch2Transformation.FallingPiecesVisible, Is.False);
            Assert.That(branch2Transformation.LowerCollisionReady, Is.True);
            Assert.That(rig.RejectedVoidHeightCount, Is.GreaterThan(0));
            // Branches build their mesh from the authored profile if the serialized mesh is absent, so the
            // invariant that matters is that the meshes are stable and never rebuilt per frame.
            Assert.That(Phase01BranchTerrain.RuntimeMeshDestructionCount, Is.Zero);
            for (int i = 0; i < 4; i++)
            {
                Assert.That(controller.BranchTerrains[i].TopMesh, Is.SameAs(topMeshes[i]));
                Assert.That(controller.BranchTerrains[i].CliffMesh, Is.SameAs(cliffMeshes[i]));
            }
            LogAssert.NoUnexpectedReceived();
            Debug.Log($"[Phase01Fix5Camera] PASS stableLead={stableLead:F3}s enter={rig.RadioLockEnterDistance:F1} exit={rig.RadioLockExitDistance:F1}");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator Fix5_UnlockIsSingleAndMonotonic()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01CameraRig rig = Object.FindAnyObjectByType<Phase01CameraRig>();
            Phase01AcceptanceDriver driver = Object.FindAnyObjectByType<Phase01AcceptanceDriver>();

            yield return driver.Run();
            Assert.That(driver.Passed, Is.True, driver.Report);
            Assert.That(rig.UnlockBlendStartCount, Is.EqualTo(4), "All four branches must unlock once on outward travel.");
            Assert.That(controller.VisibleBranchPresentationCount, Is.EqualTo(1));
            Assert.That(controller.CentralSupport.RadioContactError, Is.LessThan(0.002f));
            Assert.That(controller.OuterDistance, Is.EqualTo(176f).Within(0.001f));
            Assert.That(player.IsUsingSimulatedInput, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        private static float RadioX(Phase01CameraRig rig, Phase01DemoController controller)
        {
            return rig.ViewCamera.WorldToViewportPoint(controller.RadioPosition + Vector3.up).x;
        }

        private static IEnumerator ApproachBranch1RadioLock(
            Phase01PlayerMotor player,
            Phase01CameraRig rig,
            float timeout)
        {
            float axis = player.InputForScalarDirection(-1f);
            float deadline = Time.realtimeSinceStartup + timeout;
            player.SetSimulatedAxis(axis);
            while (rig.State != Phase01CameraRig.CameraState.RadioLockBlend
                && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(axis);
                yield return null;
            }
            player.SimulateJumpRelease();
        }

        /// <summary>
        /// First solid authored scalar on Branch 1 at or beyond <paramref name="minimum"/>. Branch 1 is
        /// an elevation-led route, so camera tests must sample real landing geometry rather than assume a
        /// flat profile.
        /// </summary>
        private static float FirstSolidBranch1Scalar(Phase01DemoController controller, float minimum)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[0];
            for (float scalar = minimum; scalar <= terrain.OuterScalar; scalar += 0.1f)
            {
                if (!terrain.IsGapAt(scalar) && terrain.SampleHeight(scalar) > -1f)
                    return scalar;
            }
            Assert.Fail($"No solid Branch 1 scalar was found at or beyond {minimum:F1}.");
            return minimum;
        }

        private static IEnumerator WaitForState(Phase01CameraRig rig, Phase01CameraRig.CameraState state, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (rig.State != state && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(rig.State, Is.EqualTo(state));
        }

        private static IEnumerator MoveUntil(Phase01PlayerMotor player, System.Func<bool> predicate, float axis, float timeout)
        {
            player.SetSimulatedAxis(axis);
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            player.SetSimulatedAxis(0f);
            Assert.That(predicate(), Is.True);
        }
    }
}
