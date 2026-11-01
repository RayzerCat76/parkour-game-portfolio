using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase02.VolcanoEscape.Tests
{
    public sealed class Phase02RespawnAndShakePlayModeTests
    {
        [UnityTest]
        public IEnumerator VolcanoEscape_RespawnUsesLatestSafePoseWithoutFallbackFlash()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller =
                Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Phase02CameraRig rig = Object.FindAnyObjectByType<Phase02CameraRig>();
            Phase02RollingRespawnPass rollingRespawn =
                Object.FindAnyObjectByType<Phase02RollingRespawnPass>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(rollingRespawn, Is.Not.Null);

            Rigidbody playerBody = controller.Motor.GetComponent<Rigidbody>();
            Assert.That(playerBody, Is.Not.Null);

            const float safeProgressX = 120f;
            controller.Motor.TeleportAndReset(new Vector3(
                safeProgressX,
                controller.SurfaceHeightAt(safeProgressX),
                0f));
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.That(controller.CurrentCheckpointIndex, Is.EqualTo(-1),
                "The visible section checkpoint counter must remain untouched in Section 1.");
            Assert.That(rollingRespawn.HasSafePose, Is.True);
            Assert.That(rollingRespawn.LatestSafePosition.x,
                Is.EqualTo(safeProgressX).Within(0.35f));

            controller.Fail(0, "RespawnFramingTest");
            float minimumRenderedPlayerX = playerBody.position.x;
            float minimumRenderedCameraX = rig.SceneCamera.transform.position.x;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                minimumRenderedPlayerX = Mathf.Min(minimumRenderedPlayerX, playerBody.position.x);
                minimumRenderedCameraX = Mathf.Min(
                    minimumRenderedCameraX,
                    rig.SceneCamera.transform.position.x);

                if (!controller.IsResetting && rollingRespawn.RespawnOverrideCount >= 1)
                    break;
            }
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(controller.IsResetting, Is.False);
            Assert.That(controller.IsPlayable, Is.True);
            Assert.That(minimumRenderedPlayerX,
                Is.GreaterThan(safeProgressX - 0.5f),
                "A section-start fallback player frame was visibly exposed during respawn.");
            Assert.That(minimumRenderedCameraX,
                Is.GreaterThan(safeProgressX + rig.FollowOffsetX - 0.5f),
                "A section-start fallback camera frame was visibly exposed during respawn.");
            Assert.That(controller.PlayerX,
                Is.EqualTo(rollingRespawn.LatestSafePosition.x).Within(0.25f),
                "Respawn incorrectly returned to the start of Phase 2.");
            Assert.That(rig.SceneCamera.transform.position.x,
                Is.EqualTo(controller.PlayerX + rig.FollowOffsetX).Within(0.12f));
            Assert.That(rig.SceneCamera.transform.position.y,
                Is.EqualTo(rig.BaseHeight + controller.PlayerY * 0.18f).Within(0.12f));
            Assert.That(rig.IsShaking, Is.False);
        }

        [UnityTest]
        public IEnumerator VolcanoEscape_ShakeMovesCompleteWorldAndEndsAfterPulse()
        {
            SceneManager.LoadScene("Phase02", LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            Phase02VolcanoEscapeController controller =
                Object.FindAnyObjectByType<Phase02VolcanoEscapeController>();
            Phase02CameraRig rig = Object.FindAnyObjectByType<Phase02CameraRig>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(rig, Is.Not.Null);
            Assert.That(controller.VolcanoBackground, Is.Not.Null);
            Assert.That(controller.VolcanoBackground.transform.parent,
                Is.Not.EqualTo(rig.SceneCamera.transform),
                "The volcano must not remain parented to the shaken camera.");
            Assert.That(rig.IsShaking, Is.False,
                "Phase 02 must not continuously shake between finite tremor pulses.");

            rig.PulseShake(0.22f, 0.28f);
            yield return null;
            Assert.That(rig.IsShaking, Is.True);
            Assert.That(Vector3.Distance(rig.SceneCamera.transform.position, rig.BaseCameraPosition),
                Is.GreaterThan(0.001f));

            yield return new WaitForSecondsRealtime(0.45f);
            Assert.That(rig.IsShaking, Is.False);
            Assert.That(Vector3.Distance(rig.SceneCamera.transform.position, rig.BaseCameraPosition),
                Is.LessThan(0.015f));
        }
    }
}
