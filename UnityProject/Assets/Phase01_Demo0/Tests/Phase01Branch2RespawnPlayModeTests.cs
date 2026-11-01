using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    public sealed class Phase01Branch2RespawnPlayModeTests
    {
        [UnityTest]
        public IEnumerator TransformedReturn_FallRespawnsAtLatestSafeApproach()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            SetAutoProperty(controller, "ActiveBranchIndex", 1);
            controller.CameraRig.SetYaw(controller.CameraRig.TurnAngle);

            Phase01BranchTerrain terrain = controller.BranchTerrains[1];
            Phase01Branch2RouteTransformation route = terrain
                .GetComponent<Phase01Branch2RouteTransformation>();
            Assert.That(route, Is.Not.Null);

            // Use the production pickup path so input locking, route activation, and the
            // outer checkpoint are established exactly as they are during real play.
            Phase01Particle particle = controller.Particles[1];
            // Derive the pickup scalar from the authored particle so this test cannot rot when the
            // branch profile is retuned (TASK 006 moved it from 145 to 131).
            float pickupScalar = Vector3.Dot(
                particle.transform.position - controller.RadioPosition,
                controller.ActiveDirection);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(pickupScalar, 0f));
            Physics.SyncTransforms();
            yield return WaitUntilGrounded(player, 1.5f);
            yield return null;

            Vector3 pickupSeparation = particle.transform.position - player.transform.position;
            pickupSeparation.y = 0f;
            Assert.That(
                pickupSeparation.magnitude,
                Is.LessThanOrEqualTo(0.9f),
                $"The player did not settle within pickup range. " +
                $"distance={pickupSeparation.magnitude:F2}, player={player.transform.position}, " +
                $"particle={particle.transform.position}.");

            // The production Update loop may already have called CheckForPickup while the
            // test waited for grounding. Only call TryPickup when it has not happened yet.
            if (controller.CarriedParticle == null)
            {
                Assert.That(controller.TryPickup(particle), Is.True,
                    "The Branch 2 particle could not be picked up through the production flow.");
            }
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle),
                "The production pickup flow did not carry the Branch 2 particle.");

            yield return WaitForTransformedReturn(controller, route, 3f);

            Assert.That(route.IsTransformed, Is.True);
            Assert.That(controller.CanAcceptInput, Is.True,
                "Input did not unlock after the Branch 2 return route settled.");
            Assert.That(controller.CarriedParticle, Is.EqualTo(particle));

            int outerCheckpointIndex = player.CheckpointIndex;
            Assert.That(outerCheckpointIndex, Is.GreaterThan(0),
                "The production pickup flow did not establish the outer return checkpoint.");

            // Walk inward under normal motor control and stop with a full safety margin
            // outside the outermost intentional 120.0-121.8 gap.
            const float targetApproachScalar = 123f;
            float inwardAxis = player.InputForScalarDirection(-1f);
            Assert.That(Mathf.Abs(inwardAxis), Is.GreaterThan(0.5f));
            player.SetSimulatedAxis(inwardAxis);

            float walkDeadline = Time.realtimeSinceStartup + 7f;
            while (player.PhysicalScalar > targetApproachScalar
                && Time.realtimeSinceStartup < walkDeadline)
            {
                player.SetSimulatedAxis(inwardAxis);
                yield return new WaitForFixedUpdate();
            }

            player.SetSimulatedAxis(0f);
            Assert.That(
                player.PhysicalScalar,
                Is.LessThanOrEqualTo(targetApproachScalar + 0.35f),
                "The test did not reach the safe transformed-route approach before the gap.");
            Assert.That(player.PhysicalScalar, Is.GreaterThan(122.25f),
                "The test moved too close to the gap for a full checkpoint safety window.");
            Assert.That(player.IsGrounded, Is.True,
                "The player was not grounded at the safe approach before the fall.");

            float checkpointDeadline = Time.realtimeSinceStartup + 1.5f;
            while (player.CheckpointIndex <= outerCheckpointIndex
                && Time.realtimeSinceStartup < checkpointDeadline)
                yield return null;

            Assert.That(
                player.CheckpointIndex,
                Is.GreaterThan(outerCheckpointIndex),
                $"The transformed return did not record a newer safe checkpoint. " +
                $"scalar={player.PhysicalScalar:F2}, current={player.CurrentScalar:F2}, " +
                $"grounded={player.IsGrounded}, input={controller.CanAcceptInput}, " +
                $"state={controller.State}, checkpoint={player.CheckpointIndex}, outer={outerCheckpointIndex}.");

            float latestSafeApproach = player.PhysicalScalar;
            player.SetPositionDuringTransition(controller.PointOnActiveLane(120.9f, -5f));
            Physics.SyncTransforms();
            player.ResetToCheckpoint();
            Physics.SyncTransforms();

            Assert.That(player.SoftResetCount, Is.EqualTo(1));
            Assert.That(
                player.PhysicalScalar,
                Is.EqualTo(latestSafeApproach).Within(0.9f),
                "The fall restored the stale outer pickup checkpoint instead of the latest safe approach.");
            Assert.That(player.PhysicalScalar, Is.GreaterThan(121.8f));
            Assert.That(player.PhysicalScalar, Is.LessThan(126f));
        }

        private static IEnumerator WaitUntilGrounded(Phase01PlayerMotor player, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!player.IsGrounded && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();

            Assert.That(player.IsGrounded, Is.True, "The player did not become grounded in time.");
        }

        private static IEnumerator WaitForTransformedReturn(
            Phase01DemoController controller,
            Phase01Branch2RouteTransformation route,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while ((!route.IsTransformed || !controller.CanAcceptInput)
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(route.IsTransformed, Is.True,
                "The Branch 2 return route did not finish transforming in time.");
            Assert.That(controller.CanAcceptInput, Is.True,
                "Branch 2 input did not unlock after transformation.");
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
