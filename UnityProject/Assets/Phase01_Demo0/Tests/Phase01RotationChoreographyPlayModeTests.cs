using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Guards the branch-switch rotation choreography. The world and the camera must turn in the same
    /// rotational direction, which means the player holds a fixed screen position for the whole turn
    /// instead of sweeping across the radio, and outward travel reads in the same screen direction on
    /// every branch.
    /// </summary>
    public sealed class Phase01RotationChoreographyPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator RotationTurnsOneWayAndHoldsThePlayerStillOnScreen()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            controller = Object.FindAnyObjectByType<Phase01DemoController>();
            player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            rig = Object.FindAnyObjectByType<Phase01CameraRig>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);

            Camera viewCamera = rig.ViewCamera;
            Assert.That(viewCamera, Is.Not.Null);

            List<float> outwardScreenSigns = new List<float>();
            float previousYaw = rig.Yaw;

            for (int deposit = 0; deposit < 4; deposit++)
            {
                // Branch 2's route transformation and Branch 4's controlled collapse both lock input
                // while they settle; the next pickup needs the lane back under player control.
                yield return WaitUntil(
                    () => controller.CanAcceptInput,
                    8f,
                    "input unlocks before the next pickup");

                int branch = controller.ActiveBranchIndex;
                Phase01Particle particle = controller.Particles[branch];

                // Pick the active particle up by standing on it, then return beside the radio.
                float particleScalar = Vector3.Dot(
                    particle.transform.position - controller.RadioPosition,
                    controller.ActiveDirection);
                // Hold station on the particle until it is collected: the interpolated transform lags a
                // physics step behind a teleport, and batch mode renders faster than that step.
                float pickupDeadline = Time.realtimeSinceStartup + 2f;
                while (controller.CarriedParticle == null && Time.realtimeSinceStartup < pickupDeadline)
                {
                    player.SetPositionDuringTransition(controller.PointOnActiveLane(particleScalar, 0f));
                    yield return new WaitForFixedUpdate();
                    yield return null;
                }
                Assert.That(controller.CarriedParticle, Is.Not.Null,
                    $"Branch {branch + 1} particle was not collected for the rotation check.");

                player.SetPositionDuringTransition(
                    controller.PointOnActiveLane(controller.RadioStopDistance, 0f));
                yield return WaitUntil(() => rig.IsStableRadioLock, 4f, "radio lock settles before deposit");

                float startYaw = rig.Yaw;
                Assert.That(controller.TryDeposit(player.CurrentScalar), Is.True,
                    $"Branch {branch + 1} deposit was not accepted.");
                yield return WaitUntil(
                    () => controller.State == Phase01DemoController.DemoState.Rotating,
                    4f,
                    "rotation begins after the deposit");
                // Rotating also covers the reveal pan that re-centres the radio; the choreography claim
                // under test starts once the yaw itself begins to move.
                yield return WaitUntil(
                    () => Mathf.Abs(rig.Yaw - startYaw) > 0.5f,
                    4f,
                    "yaw turn starts");

                float minViewportX = float.MaxValue;
                float maxViewportX = float.MinValue;
                float minViewportYaw = 0f;
                float maxViewportYaw = 0f;
                float previousSampleYaw = startYaw;
                bool yawAdvancedMonotonically = true;
                int samples = 0;

                while (controller.State == Phase01DemoController.DemoState.Rotating
                    && Mathf.Abs(rig.Yaw - startYaw) < 89.9f)
                {
                    float viewportX = viewCamera.WorldToViewportPoint(
                        player.transform.position + Vector3.up * player.GroundOffset).x;
                    float yaw = rig.Yaw;
                    if (viewportX < minViewportX)
                    {
                        minViewportX = viewportX;
                        minViewportYaw = yaw;
                    }
                    if (viewportX > maxViewportX)
                    {
                        maxViewportX = viewportX;
                        maxViewportYaw = yaw;
                    }
                    if (yaw < previousSampleYaw - 0.0001f)
                        yawAdvancedMonotonically = false;
                    previousSampleYaw = yaw;
                    samples++;
                    yield return null;
                }

                yield return WaitUntil(
                    () => controller.State == Phase01DemoController.DemoState.Idle || controller.DemoComplete,
                    4f,
                    "branch settle completes");

                float delta = Mathf.DeltaAngle(startYaw, rig.Yaw);
                Assert.That(samples, Is.GreaterThan(1), "The rotation did not animate over multiple frames.");
                Assert.That(Mathf.Abs(delta), Is.EqualTo(90f).Within(0.01f),
                    $"Branch {branch + 1} rotation was {delta:F2} degrees, not one quarter turn.");
                Assert.That(yawAdvancedMonotonically, Is.True,
                    $"Branch {branch + 1} rotation reversed direction mid-turn.");
                Assert.That(maxViewportX - minViewportX, Is.LessThan(0.05f),
                    $"Branch {branch + 1} swept the player across the screen (viewport X span "
                    + $"{maxViewportX - minViewportX:F3}, min {minViewportX:F3} at yaw {minViewportYaw:F1}, "
                    + $"max {maxViewportX:F3} at yaw {maxViewportYaw:F1}).");

                outwardScreenSigns.Add(player.ActiveBranchScreenSign);
                float sign = Mathf.Sign(Mathf.DeltaAngle(previousYaw, rig.Yaw));
                Assert.That(sign, Is.EqualTo(Mathf.Sign(Mathf.DeltaAngle(previousYaw, startYaw + delta))),
                    "Quarter turns must accumulate in one consistent direction.");
                previousYaw = rig.Yaw;
            }

            Assert.That(controller.DemoComplete, Is.True);
            Assert.That(controller.CameraQuarterTurns, Is.EqualTo(4));
            Assert.That(outwardScreenSigns.Distinct().Count(), Is.EqualTo(1),
                $"Outward travel flipped screen sides between branches: {string.Join(", ", outwardScreenSigns)}.");
        }

        private static IEnumerator WaitUntil(
            System.Func<bool> predicate,
            float timeout,
            string failure)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(predicate(), Is.True, failure);
        }

        private Phase01DemoController controller;
        private Phase01PlayerMotor player;
        private Phase01CameraRig rig;
    }
}
