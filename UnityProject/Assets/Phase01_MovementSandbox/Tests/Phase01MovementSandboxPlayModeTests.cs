using System.Collections;
using NUnit.Framework;
using Phase01.Demo0;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.MovementSandbox.Tests
{
    public sealed class Phase01MovementSandboxPlayModeTests
    {
        [UnityTest]
        public IEnumerator SandboxTour_PassesMovementAndReliabilityRequirements()
        {
            SceneManager.LoadScene("Phase01_MovementSandbox", LoadSceneMode.Single);
            yield return null;

            Phase01ParkourMotor motor = Object.FindAnyObjectByType<Phase01ParkourMotor>();
            Phase01SandboxAcceptanceDriver driver = Object.FindAnyObjectByType<Phase01SandboxAcceptanceDriver>();
            Phase01SandboxReset reset = Object.FindAnyObjectByType<Phase01SandboxReset>();
            Phase01SandboxCamera cameraRig = Object.FindAnyObjectByType<Phase01SandboxCamera>();
            Phase01MovingPlatform platform = Object.FindAnyObjectByType<Phase01MovingPlatform>();
            Assert.That(motor, Is.Not.Null);
            Assert.That(driver, Is.Not.Null);
            Assert.That(reset, Is.Not.Null);
            Assert.That(cameraRig, Is.Not.Null);
            Assert.That(platform, Is.Not.Null);
            Assert.That(motor.IsUsingSimulatedInput, Is.False);
            Assert.That(motor.MoveSpeed, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(motor.AirControlMultiplier, Is.LessThan(1f));
            Assert.That(motor.CoyoteTime, Is.InRange(0.10f, 0.16f));
            Assert.That(motor.JumpBufferTime, Is.InRange(0.10f, 0.16f));

            yield return driver.RunAcceptance();
            Assert.That(driver.Passed, Is.True, driver.Report);
            Assert.That(driver.MeasuredHeldJumpHeight, Is.InRange(1.8f, 2.6f));
            Assert.That(driver.MeasuredTapJumpHeight, Is.LessThan(driver.MeasuredHeldJumpHeight * 0.78f));
            Assert.That(driver.MeasuredApexTime, Is.InRange(0.35f, 0.58f));
            Assert.That(driver.MeasuredCoyoteDelay, Is.InRange(0.03f, 0.10f));
            Assert.That(reset.LastResetDuration, Is.LessThan(1f));
            Assert.That(reset.ActivatedCheckpointCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(Mathf.Abs(motor.transform.position.z), Is.LessThan(0.001f));
            Assert.That(platform.ReversalCount, Is.GreaterThan(0));
            Assert.That(cameraRig.transform.position.z, Is.EqualTo(-14f).Within(0.001f));
            Assert.That(motor.IsUsingSimulatedInput, Is.False);
            LogAssert.NoUnexpectedReceived();
            Debug.Log($"[Phase01MovementSandboxTests] PASS held={driver.MeasuredHeldJumpHeight:F3}m "
                + $"tap={driver.MeasuredTapJumpHeight:F3}m apex={driver.MeasuredApexTime:F3}s "
                + $"coyote={driver.MeasuredCoyoteDelay:F3}s reset={reset.LastResetDuration:F3}s");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator ProductionDemo0_RemainsAcceptedAndUsesApprovedParkourMotor()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            Phase01AcceptanceDriver productionDriver = Object.FindAnyObjectByType<Phase01AcceptanceDriver>();
            Phase01PlayerMotor productionPlayer = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Phase01ParkourMotor[] productionMotors = Object.FindObjectsByType<Phase01ParkourMotor>(FindObjectsSortMode.None);
            Assert.That(productionDriver, Is.Not.Null);
            Assert.That(productionPlayer, Is.Not.Null);
            Assert.That(productionMotors, Has.Length.EqualTo(1));
            Assert.That(productionMotors[0].gameObject, Is.SameAs(productionPlayer.gameObject));
            Assert.That(productionMotors[0].MoveSpeed, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(productionPlayer.IsUsingSimulatedInput, Is.False);
            yield return productionDriver.Run();
            Assert.That(productionDriver.Passed, Is.True, productionDriver.Report);
            Assert.That(Object.FindObjectsByType<Phase01ParkourMotor>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
