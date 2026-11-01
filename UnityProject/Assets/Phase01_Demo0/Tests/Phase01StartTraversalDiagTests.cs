using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Regression guard for a reported bug: at the start of Phase 01 the camera stayed parked at the
    /// radio while the player walked (and fell) outward, because the radio lock reached 14 m - further
    /// than the first gap. The lock is now only wide enough to frame a deposit, so the camera must
    /// follow as soon as the player commits outward, and outward travel must leave the lock.
    /// </summary>
    public sealed class Phase01StartTraversalPlayModeTests
    {
        [UnityTest]
        [Timeout(60000)]
        public IEnumerator StartOfPhase01_HoldingOutwardMovesTheCamera()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01CameraRig rig = Object.FindAnyObjectByType<Phase01CameraRig>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);

            // The lock may only cover the approach to the receiver, never the first gap of the branch.
            Assert.That(
                rig.RadioLockExitDistance,
                Is.LessThan(controller.BranchTerrains[0].IsGapAt(2.6f) ? 2.5f : 4.9f),
                "The radio lock must release before Branch 1's first gap.");
            Assert.That(rig.RadioLockExitDistance, Is.LessThan(4.9f));

            float axis = player.InputForScalarDirection(1f);
            Vector3 startCamera = rig.transform.position;
            float cameraTravel = 0f;
            float maxScalar = 0f;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(axis);
                cameraTravel = Mathf.Max(cameraTravel, Vector3.Distance(rig.transform.position, startCamera));
                maxScalar = Mathf.Max(maxScalar, player.CurrentScalar);
                yield return null;
            }

            player.SetSimulatedAxis(0f);
            Assert.That(cameraTravel, Is.GreaterThan(3f),
                $"The camera must follow the player outward (travelled {cameraTravel:F2} m).");
            Assert.That(rig.State, Is.Not.EqualTo(Phase01CameraRig.CameraState.RadioLocked),
                "The camera must not stay parked on the radio while the player is outward.");
            Assert.That(maxScalar, Is.GreaterThan(rig.RadioLockExitDistance),
                $"The player must be able to reach past the lock window (reached {maxScalar:F2} m).");
        }
    }
}
