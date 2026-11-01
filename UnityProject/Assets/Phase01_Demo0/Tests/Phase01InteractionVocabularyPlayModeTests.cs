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
    /// Guards the interaction vocabulary Phase 01 is allowed to build difficulty from, as distinct from the
    /// gaps themselves. Two capabilities matter most, because most of the interesting beats in games of this
    /// kind are made of them: the world launching the player, and hazards that ride the level's own moving
    /// geometry. Both existed as intent and neither worked.
    /// </summary>
    public sealed class Phase01InteractionVocabularyPlayModeTests
    {
        /// <summary>
        /// A launcher has to reach higher than a jump, otherwise it adds nothing a jump could not do.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LauncherFiresAndReachesAboveTheJumpApex()
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

            // Flat ledge on Branch 1, clear of the authored bar and its lane block.
            const float padScalar = 20f;
            GameObject padObject = new GameObject("Test Launcher");
            Phase01BouncePad pad = padObject.AddComponent<Phase01BouncePad>();
            pad.transform.position = controller.PointOnActiveLane(padScalar, 0f);
            pad.Configure(0, 12f, 1.2f, controller, player);

            Assert.That(pad.LaunchSpeed, Is.GreaterThan(
                    Mathf.Sqrt(2f * 22.222f * motor.JumpHeight)),
                "The authored launch speed must exceed the launch speed a jump apex implies.");

            player.SetPositionDuringTransition(controller.PointOnActiveLane(padScalar, 0f));
            for (int i = 0; i < 40 && !motor.IsGrounded; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(motor.IsGrounded, Is.True, "The launcher probe must start grounded on the ledge.");

            float surface = controller.ActiveSurfaceHeight(padScalar);
            float apex = float.MinValue;
            float deadline = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                apex = Mathf.Max(apex, player.transform.position.y - surface);
                yield return new WaitForFixedUpdate();
            }

            Debug.Log($"[DiagLauncher] launches={pad.LaunchCount} apex={apex:F2} jumpApex={motor.JumpHeight:F2}");
            Assert.That(pad.LaunchCount, Is.GreaterThanOrEqualTo(1),
                "Landing on the launcher must launch the player; it did not fire at all.");
            Assert.That(apex, Is.GreaterThan(motor.JumpHeight + 0.25f),
                $"The launch reached {apex:F2} m above the ledge against a {motor.JumpHeight:F2} m jump apex, "
                + "so it would open nothing a jump could not already reach.");
        }

        /// <summary>
        /// A gate parented to moving geometry has to ride it, which is what makes composed beats possible.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GateRidesTheMovingGeometryItIsParentedTo()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            Phase01MovingPlatform ferry = controller.BranchTerrains[0]
                .GetComponentsInChildren<Phase01MovingPlatform>(true)
                .First(platform => platform.name.Contains("Ferry"));
            Assert.That(ferry, Is.Not.Null, "Branch 1 must author the ferry this capability is proven on.");

            GameObject gateObject = new GameObject("Test Riding Gate");
            gateObject.transform.SetParent(ferry.transform, false);
            gateObject.transform.localPosition = new Vector3(0.4f, 0.9f, 0f);
            Phase01SweeperGate gate = gateObject.AddComponent<Phase01SweeperGate>();
            gate.ConfigureHorizontalSweep(0, 0.4f, 1.4f, 0.9f, 1f, -1f, 3f, controller, player);

            yield return new WaitForFixedUpdate();
            Vector3 ferryStart = ferry.transform.position;
            float minLocalX = float.MaxValue;
            float maxLocalX = float.MinValue;

            for (int i = 0; i < 60; i++)
            {
                // A riding bar stays inside its parent's frame: its local position only ever describes the
                // authored sweep. A bar anchored in world space drifts out of that frame as the platform
                // moves away from where the bar was resolved, which is what this range catches.
                Vector3 local = gate.transform.localPosition;
                minLocalX = Mathf.Min(minLocalX, local.x);
                maxLocalX = Mathf.Max(maxLocalX, local.x);
                yield return new WaitForFixedUpdate();
            }

            float ferryTravel = Vector3.Distance(ferry.transform.position, ferryStart);
            Debug.Log($"[DiagRiding] ferryTravel={ferryTravel:F2} localX=[{minLocalX:F2},{maxLocalX:F2}] "
                + $"localY={gate.transform.localPosition.y:F2}");

            Assert.That(ferryTravel, Is.GreaterThan(0.2f),
                "The ferry must actually move, otherwise this proves nothing.");
            Assert.That(gate.transform.localPosition.y, Is.EqualTo(0.9f).Within(0.05f),
                "The bar must keep the height it was authored at inside the platform's frame.");
            Assert.That(minLocalX, Is.GreaterThan(0.1f),
                $"A riding bar left its parent's frame (local x fell to {minLocalX:F2}); it would sweep "
                + "through world space instead of riding the platform.");
            Assert.That(maxLocalX, Is.LessThan(1.7f),
                $"A riding bar left its parent's frame (local x reached {maxLocalX:F2}); it would sweep "
                + "through world space instead of riding the platform.");
            Assert.That(maxLocalX - minLocalX, Is.GreaterThan(0.4f),
                "The bar must still perform its authored sweep while riding.");
        }
    }
}
