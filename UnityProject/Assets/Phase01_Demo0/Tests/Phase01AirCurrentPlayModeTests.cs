using System.Collections;
using System.Linq;
using NUnit.Framework;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Focused proof for the Phase 01 air currents: the mechanic that answers "the parkour is just jumps".
    ///
    /// These tests deliberately run against the authored currents in the level rather than a synthetic one,
    /// and they measure the ride in the movement module. A current is only worth having if it is a different
    /// verb from jumping, so the tests assert the things that make it different: a crossing needs a hold and
    /// no jump at all, letting go mid-air drops the player into the void, and the same current carries the
    /// player back the other way - which is what keeps a both-ways branch fair.
    /// </summary>
    public sealed class Phase01AirCurrentPlayModeTests
    {
        private static IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static Phase01Draught AuthoredCurrent(string nameFragment)
        {
            return Object.FindObjectsByType<Phase01Draught>(FindObjectsSortMode.None)
                .FirstOrDefault(current => current.name.Contains(nameFragment));
        }

        private static IEnumerator Settle(Phase01PlayerMotor player, Phase01ParkourMotor motor, float scalar)
        {
            player.SetPositionDuringTransition(
                Object.FindAnyObjectByType<Phase01DemoController>().PointOnActiveLane(scalar, 0f));
            for (int i = 0; i < 60 && !motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
        }


        /// <summary>
        /// The headline claim: a void wider than any jump is crossed by holding on, with no jump spent.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AuthoredCurrentCarriesAPlayerWhoHoldsOnAndSpendsNoJump()
        {
            yield return LoadLevel();
            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01ParkourMotor motor = player.GetComponent<Phase01ParkourMotor>();
            Phase01Draught current = AuthoredCurrent("Inner Chasm");
            Assert.That(current, Is.Not.Null, "Branch 1 must author an inner chasm air current.");
            Assert.That(current.IsBlowing, Is.True, "The baseline branch's currents are steady.");

            float flatReach = 2f * player.MoveSpeed * (player.JumpLaunchSpeed / -player.Gravity);
            Assert.That(current.Width, Is.GreaterThan(flatReach * 2f),
                $"The current must span a void no jump can cross (width {current.Width:F2} m against a "
                + $"{flatReach:F2} m flat reach).");

            yield return Settle(player, motor, current.InnerScalar - 1.0f);
            Assert.That(motor.IsGrounded, Is.True, "The probe must start on the lip the current is entered from.");
            int jumpsBefore = motor.AcceptedJumpCount;
            float rideBefore = player.DraughtSeconds;

            // Hold the button, walk forward, and let the current do the work. No jump is ever queued: a ride
            // that needed a jump would not be a different verb.
            player.SetSimulatedJumpHeld(true);
            float deadline = Time.realtimeSinceStartup + 12f;
            while (player.CurrentScalar < current.OuterScalar - 0.5f && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(1f));
                yield return new WaitForFixedUpdate();
            }
            player.SetSimulatedAxis(0f);
            player.SetSimulatedJumpHeld(false);

            float rideSeconds = player.DraughtSeconds - rideBefore;
            Debug.Log($"[DiagCurrent] ride={rideSeconds:F2}s scalar={player.CurrentScalar:F2} "
                + $"of {current.InnerScalar:F2}->{current.OuterScalar:F2} jumps={motor.AcceptedJumpCount - jumpsBefore}");

            Assert.That(player.CurrentScalar, Is.GreaterThan(current.OuterScalar - 0.6f),
                $"The current must carry the player to the far lip: reached {player.CurrentScalar:F2}.");
            Assert.That(rideSeconds, Is.GreaterThan(1.2f),
                $"The crossing must actually be a ride ({rideSeconds:F2}s of carried time).");
            Assert.That(current.CrossingCount, Is.GreaterThan(0), "The current must record the crossing.");
            Assert.That(motor.AcceptedJumpCount, Is.EqualTo(jumpsBefore),
                "A ride must not spend a jump: that is what makes it a different verb from jumping.");
        }

        /// <summary>
        /// The other half of the mechanic: it only carries a player who holds on. Letting go is a real
        /// decision with a real consequence, which is what turns a crossing into a beat.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AuthoredCurrentDropsAPlayerWhoLetsGoMidAir()
        {
            yield return LoadLevel();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01ParkourMotor motor = player.GetComponent<Phase01ParkourMotor>();
            Phase01Draught current = AuthoredCurrent("Inner Chasm");
            Assert.That(current, Is.Not.Null);

            yield return Settle(player, motor, current.InnerScalar - 1.0f);

            player.SetSimulatedJumpHeld(true);
            float holdDeadline = Time.realtimeSinceStartup + 12f;
            while (player.CurrentScalar < current.InnerScalar + 1.5f
                && Time.realtimeSinceStartup < holdDeadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(1f));
                yield return new WaitForFixedUpdate();
            }
            Assert.That(player.CurrentScalar, Is.GreaterThan(current.InnerScalar + 1.0f),
                "The probe must actually be inside the current before letting go.");

            player.SetSimulatedJumpHeld(false);
            float releasedAt = player.transform.position.y;
            float deepest = releasedAt;
            float dropDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < dropDeadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(1f));
                deepest = Mathf.Min(deepest, player.transform.position.y);
                yield return new WaitForFixedUpdate();
            }
            player.SetSimulatedAxis(0f);

            Debug.Log($"[DiagCurrent] releasedAt={releasedAt:F2} deepest={deepest:F2} "
                + $"scalar={player.CurrentScalar:F2} rideLine={current.RideHeight:F2}");
            Assert.That(releasedAt - deepest, Is.GreaterThan(1.0f),
                "Letting go mid-current must drop the player: the hold is the mechanic.");
            Assert.That(player.CurrentScalar, Is.LessThan(current.OuterScalar - 0.5f),
                "A released player must not arrive at the far lip.");
        }

        /// <summary>
        /// The current has to work inwards as well as outwards. Every branch lane is walked out and back, so
        /// a one-way mechanic would strand the return trip - this is the property that let a purely vertical
        /// lift be rejected in favour of an air current.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AuthoredCurrentCarriesThePlayerBackFromTheFarLip()
        {
            yield return LoadLevel();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01ParkourMotor motor = player.GetComponent<Phase01ParkourMotor>();
            Phase01Draught current = AuthoredCurrent("Inner Chasm");
            Assert.That(current, Is.Not.Null);

            yield return Settle(player, motor, current.OuterScalar + 1.0f);
            Assert.That(motor.IsGrounded, Is.True, "The probe must start on the far ledge.");
            float rideBefore = player.DraughtSeconds;

            player.SetSimulatedJumpHeld(true);
            float deadline = Time.realtimeSinceStartup + 14f;
            while (player.CurrentScalar > current.InnerScalar + 0.5f && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(-1f));
                yield return new WaitForFixedUpdate();
            }
            player.SetSimulatedAxis(0f);
            player.SetSimulatedJumpHeld(false);

            float rideSeconds = player.DraughtSeconds - rideBefore;
            Debug.Log($"[DiagCurrent] inwardRide={rideSeconds:F2}s scalar={player.CurrentScalar:F2}");
            Assert.That(player.CurrentScalar, Is.LessThan(current.InnerScalar + 0.6f),
                $"The current must carry the player back to the inner lip: reached {player.CurrentScalar:F2}.");
            Assert.That(rideSeconds, Is.GreaterThan(1.2f), "The return crossing must be a ride too.");
        }
    }
}
