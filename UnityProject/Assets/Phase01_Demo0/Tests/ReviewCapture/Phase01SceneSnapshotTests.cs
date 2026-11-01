#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests.ReviewCapture
{
    /// <summary>
    /// Writes plain PNG stills of the Phase 01 scene so the look of the level can be reviewed without a
    /// human sitting in front of the player. Cameras are placed explicitly because edit mode has no game
    /// loop to drive the gameplay rig.
    /// </summary>
    public sealed class Phase01SceneSnapshotTests
    {
        private const int Width = 1600;
        private const int Height = 900;

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CaptureLookStills()
        {
            // A fixed path inside the project rather than the temp folder: Path.GetTempPath() resolves
            // differently depending on how the editor was launched (it has already scattered two players
            // with the same name), and review stills that are written somewhere you cannot find them are
            // how a stale frame ends up being approved as the current look.
            string folder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Builds", "ReviewStills"));
            Directory.CreateDirectory(folder);
            EditorSceneManager.OpenScene("Assets/Scenes/Phase01_Demo0.unity", OpenSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Assert.That(controller, Is.Not.Null, "The Phase 01 scene must be open.");

            // Shoot through the gameplay camera itself when there is one, so the stills carry the same
            // background, clear flags and post-processing the player actually sees.
            Phase01CameraRig gameplayRig = Object.FindAnyObjectByType<Phase01CameraRig>();
            Assert.That(gameplayRig, Is.Not.Null, "The scene must have a camera rig to frame against.");
            Camera gameplay = gameplayRig.ViewCamera;
            GameObject rig = null;
            Camera camera = gameplay;
            if (camera == null)
            {
                rig = new GameObject("Snapshot Camera");
                camera = rig.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
            }
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 600f;

            // The shots that decide whether the art works: framed exactly as the gameplay rig frames, since
            // the rig is orthographic at size 4.2 and the wide shots above are framed far wider. Approving art
            // from a 9 m view when the player sees an 8.4 m view is how a backdrop ends up reading as a wall.
            yield return ShootGameplay(folder, "g1_opening", camera, gameplayRig, controller, 6f);
            yield return ShootGameplay(folder, "g2_terrace", camera, gameplayRig, controller, 12f);
            yield return ShootGameplay(folder, "g3_swept_lip", camera, gameplayRig, controller, 18.5f);
            yield return ShootGameplay(folder, "g4_mid_climb", camera, gameplayRig, controller, 60f);
            yield return ShootGameplay(folder, "g5_summit", camera, gameplayRig, controller, 90f);
            yield return ShootGameplay(folder, "g6_high", camera, gameplayRig, controller, 108f);

            // A walk down Branch 1 at gameplay framing. The art has to hold for the whole branch rather
            // than at six chosen poses, and this is also the cheapest way to catch geometry that pokes into
            // the frame where it should not - a backdrop or a prop standing where the player can see it
            // pass through the level. Void scalars are skipped: there is no surface to frame against there.
            int tour = 0;
            for (float scalar = 4f; scalar <= 176f; scalar += 6f)
            {
                if (controller.ActiveSurfaceHeight(scalar) < -3f)
                    continue;
                yield return ShootGameplay(folder, $"t{tour:00}_{scalar:000}", camera, gameplayRig, controller, scalar);
                tour++;
            }

            // Branch 1 runs along -X, so the familiar side-on read looks down +Z.
            yield return Shoot(folder, "01_branch1_opening", camera,
                new Vector3(-16f, 3.6f, -19f), new Vector3(-16f, 1.8f, 0f), 9f, 6f);
            yield return Shoot(folder, "02_branch1_sweeping_bar", camera,
                new Vector3(-11f, 3.2f, -9f), new Vector3(-11f, 1.6f, 0f), 6f, 6f);
            yield return Shoot(folder, "03_branch1_summit", camera,
                new Vector3(-78f, 11f, -19f), new Vector3(-78f, 8.5f, 0f), 16f, 6f);
            yield return Shoot(folder, "04_hub_overview", camera,
                new Vector3(-58f, 62f, -96f), new Vector3(0f, 6f, 0f), 74f, 18f);
            // The collectibles are authored as small glowing yellow cubes, so they get their own look check.
            yield return Shoot(folder, "06_collectible_cube", camera,
                new Vector3(-109f, 8.4f, -9f), new Vector3(-109f, 7.6f, 0f), 5f, 5f);
            yield return Shoot(folder, "07_hub_radio", camera,
                new Vector3(-4.6f, 2.4f, -9f), new Vector3(-3.2f, 1.2f, 0f), 4.6f, 8f);

            // The design's completion beat: the radio bursts once the fourth block lands.
            Phase01RadioBurst burst = Object.FindAnyObjectByType<Phase01RadioBurst>();
            Assert.That(burst, Is.Not.Null, "The radio burst presentation must exist.");
            burst.ApplyProgress(0.45f);
            yield return Shoot(folder, "08_radio_burst", camera,
                new Vector3(-5.4f, 3.4f, -10f), new Vector3(-3.2f, 2f, 0f), 5.4f, 8f);
            burst.ApplyProgress(1f, false);

            if (rig != null)
                Object.DestroyImmediate(rig);
            Debug.Log($"[Snapshot] stills written to {folder}");
        }

        /// <summary>
        /// Frames a still exactly as the gameplay rig does: on the lane plane, orthographic size 4.2, pitched
        /// by the rig's side-view pitch, with the lane surface on the rig's feet viewport line.
        /// </summary>
        private static IEnumerator ShootGameplay(
            string folder,
            string name,
            Camera camera,
            Phase01CameraRig rig,
            Phase01DemoController controller,
            float scalar)
        {
            Vector3 lanePoint = controller.PointOnActiveLane(scalar, 0f);
            camera.orthographic = true;
            camera.orthographicSize = rig.CloseOrthographicSize;

            // Drive the RIG, not the camera: the camera is a child of the rig with a lateral offset
            // (0, 4.8, -14), and setting the camera's world position discards that offset - which framed the
            // level edge-on and is why earlier stills could not be trusted.
            Transform rigTransform = rig.transform;
            rigTransform.rotation = Quaternion.Euler(0f, rig.Yaw, 0f);
            rigTransform.position = new Vector3(lanePoint.x, 0f, lanePoint.z);

            // One exact step: an orthographic camera's viewport projection is linear in the rig's height, so
            // measuring at height zero yields the height that puts the lane surface on the feet viewport line.
            float viewportY = camera.WorldToViewportPoint(lanePoint).y;
            float verticalProjection = Mathf.Max(0.1f, Vector3.Dot(camera.transform.up, Vector3.up));
            float y = (viewportY - rig.PlayerFeetViewportY)
                * (2f * rig.CloseOrthographicSize) / verticalProjection;
            rigTransform.position = new Vector3(lanePoint.x, y, lanePoint.z);

            yield return ShootWith(folder, name, camera);
        }

        private static IEnumerator ShootWith(string folder, string name, Camera camera)
        {
            // The skyline rides the camera in LateUpdate, which edit mode never runs, so pose it by hand.
            Object.FindAnyObjectByType<Phase01SkylineBackdrop>()?.Apply();

            RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            yield return null;
            camera.Render();
            yield return null;

            RenderTexture.active = target;
            Texture2D readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            readback.Apply();
            // Write, then prove the write happened. A still that silently fails to land is worse than no
            // still at all: reviewing a stale frame as if it were the current one is exactly how an earlier
            // art pass was approved while the level still looked wrong.
            string shotPath = Path.Combine(folder, name + ".png");
            File.WriteAllBytes(shotPath, readback.EncodeToPNG());
            if (!File.Exists(shotPath))
                throw new IOException($"Snapshot {shotPath} was not written.");
            camera.targetTexture = previous;
            RenderTexture.active = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(readback);
        }

        private static IEnumerator Shoot(
            string folder,
            string name,
            Camera camera,
            Vector3 position,
            Vector3 lookAt,
            float orthographicSize,
            float pitchDegrees)
        {
            camera.orthographicSize = orthographicSize;
            camera.transform.position = position;
            Vector3 flat = lookAt - position;
            flat.y = 0f;
            camera.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up)
                * Quaternion.Euler(pitchDegrees, 0f, 0f);

            yield return ShootWith(folder, name, camera);
        }

    }
}
#endif
