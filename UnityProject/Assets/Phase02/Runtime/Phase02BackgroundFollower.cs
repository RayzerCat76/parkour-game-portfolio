using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    /// <summary>
    /// Keeps the static volcano centered on the unshaken camera target while
    /// remaining outside the camera hierarchy. Camera tremor therefore moves
    /// the complete rendered world instead of making only the platforms appear
    /// to wobble beneath a fixed background.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class Phase02BackgroundFollower : MonoBehaviour
    {
        private Phase02VolcanoEscapeController controller;
        private Transform backgroundRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Phase02")
                return;

            if (FindAnyObjectByType<Phase02BackgroundFollower>() != null)
                return;

            new GameObject("Phase02 Background Follower")
                .AddComponent<Phase02BackgroundFollower>();
        }

        private IEnumerator Start()
        {
            yield return null;
            controller = FindAnyObjectByType<Phase02VolcanoEscapeController>();
            backgroundRoot = GameObject.Find("Static Volcano Background")?.transform;
            if (controller == null || backgroundRoot == null)
            {
                yield return null;
                controller = FindAnyObjectByType<Phase02VolcanoEscapeController>();
                backgroundRoot = GameObject.Find("Static Volcano Background")?.transform;
            }

            if (controller == null || backgroundRoot == null)
            {
                Debug.LogError("[Phase02] Background follower could not resolve the controller or volcano root.");
                enabled = false;
                yield break;
            }

            backgroundRoot.SetParent(null, true);
            FollowUnshakenTarget();
        }

        private void LateUpdate()
        {
            FollowUnshakenTarget();
        }

        private void FollowUnshakenTarget()
        {
            if (controller == null || backgroundRoot == null || controller.PlayerTransform == null)
                return;

            Transform player = controller.PlayerTransform;
            backgroundRoot.position = new Vector3(
                player.position.x + 5.5f,
                4.25f + player.position.y * 0.18f,
                -18f);
            backgroundRoot.rotation = Quaternion.identity;
        }
    }
}
