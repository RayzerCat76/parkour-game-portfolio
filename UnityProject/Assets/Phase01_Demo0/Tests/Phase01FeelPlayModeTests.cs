using System.Collections;
using NUnit.Framework;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Guards the movement feel the demo is played with, as distinct from its route content. Coyote time is
    /// the one that matters most: a player who runs off a lip and presses jump a moment later expects the
    /// jump to still happen, and "it did not feel like there was any coyote time" is a real playability
    /// bug even though the timer exists.
    /// </summary>
    public sealed class Phase01FeelPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CoyoteTimeAcceptsAJumpJustAfterLeavingTheLedgeAndThenCloses()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01ParkourMotor motor = Object.FindAnyObjectByType<Phase01ParkourMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);

            float coyote = motor.CoyoteTime;
            Assert.That(coyote, Is.GreaterThan(0.05f), "The demo must author a usable coyote window.");

            yield return StandAndThenWalkOffTheLip(controller, player, motor);
            Assert.That(motor.IsGrounded, Is.False, "The player should be airborne after running off the lip.");

            // A jump pressed inside the window must still fire.
            int acceptedBefore = motor.AcceptedJumpCount;
            for (int i = 0; i < Mathf.Max(1, Mathf.RoundToInt(coyote * 0.4f / Time.fixedDeltaTime)); i++)
                yield return new WaitForFixedUpdate();
            player.SimulateJumpPress(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            player.SimulateJumpRelease();
            Assert.That(motor.AcceptedJumpCount, Is.EqualTo(acceptedBefore + 1),
                $"A jump pressed about {coyote * 0.4f:F3}s after leaving the ledge must be accepted inside the "
                + $"{coyote:F3}s coyote window.");
            Assert.That(motor.VerticalVelocity, Is.GreaterThan(1f),
                "A coyote jump must actually launch the player upward.");

            // A jump pressed well after the window must not.
            yield return StandAndThenWalkOffTheLip(controller, player, motor);
            int lateBefore = motor.AcceptedJumpCount;
            for (int i = 0; i < Mathf.CeilToInt(coyote * 2.5f / Time.fixedDeltaTime); i++)
                yield return new WaitForFixedUpdate();
            player.SimulateJumpPress(true);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            player.SimulateJumpRelease();
            Assert.That(motor.AcceptedJumpCount, Is.EqualTo(lateBefore),
                $"A jump pressed about {coyote * 2.5f:F3}s after leaving the ledge must be refused; the "
                + $"{coyote:F3}s coyote window should have closed.");
        }

        /// <summary>
        /// Puts the player just behind Branch 1's first lip, then walks them off it with no jump.
        /// </summary>
        private static IEnumerator StandAndThenWalkOffTheLip(
            Phase01DemoController controller,
            Phase01PlayerMotor player,
            Phase01ParkourMotor motor)
        {
            // Branch 1's first lip sits at scalar 5.6; stand a little short of it.
            player.SetPositionDuringTransition(controller.PointOnActiveLane(5f, 0f));
            for (int i = 0; i < 40 && !motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();
            yield return null;

            float deadline = Time.realtimeSinceStartup + 2f;
            while (motor.IsGrounded && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(1f));
                yield return null;
            }
            player.SetSimulatedAxis(0f);
        }
    }
}
