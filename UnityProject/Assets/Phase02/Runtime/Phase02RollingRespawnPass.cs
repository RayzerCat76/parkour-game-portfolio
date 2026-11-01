using System.Collections;
using System.Reflection;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    /// <summary>
    /// Preserves the project's established respawn rule: the latest valid safe
    /// grounded world pose is used first, while the authored section checkpoint
    /// remains only a fallback. During a reset, the controller's selected
    /// checkpoint is temporarily redirected to the rolling safe pose so there is
    /// exactly one teleport and no visible section-start frame.
    /// </summary>
    [DefaultExecutionOrder(2500)]
    public sealed class Phase02RollingRespawnPass : MonoBehaviour
    {
        private const float GroundedHoldDuration = 0.18f;
        private const float MinimumCaptureAdvance = 3f;
        private const float GapSafetyMargin = 2.25f;

        private static readonly FieldInfo CheckpointPositionsField =
            typeof(Phase02VolcanoEscapeController).GetField(
                "checkpointPositions",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo CurrentCheckpointIndexField =
            typeof(Phase02VolcanoEscapeController).GetField(
                "currentCheckpointIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private Phase02VolcanoEscapeController controller;
        private Phase02CameraRig cameraRig;
        private Phase01ParkourMotor motor;
        private Rigidbody playerBody;
        private Collider playerCollider;
        private Vector3[] checkpointPositions;
        private float groundedHold;
        private bool wasResetting;
        private bool overrideActive;
        private int originalCheckpointIndex;
        private int injectedCheckpointSlot = -1;
        private Vector3 originalCheckpointPosition;

        public bool HasSafePose { get; private set; }
        public Vector3 LatestSafePosition { get; private set; }
        public int RespawnOverrideCount { get; private set; }
        public bool IsOwningResetPose => overrideActive;

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

            if (FindAnyObjectByType<Phase02RollingRespawnPass>() != null)
                return;

            new GameObject("Phase02 Rolling Safe Respawn")
                .AddComponent<Phase02RollingRespawnPass>();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;

            controller = FindAnyObjectByType<Phase02VolcanoEscapeController>();
            cameraRig = FindAnyObjectByType<Phase02CameraRig>();
            if (controller == null || cameraRig == null || controller.Motor == null)
            {
                Debug.LogError("[Phase02] Rolling respawn could not resolve controller, camera, or motor.");
                enabled = false;
                yield break;
            }

            motor = controller.Motor;
            playerBody = motor.GetComponent<Rigidbody>();
            playerCollider = motor.GetComponent<Collider>();
            checkpointPositions = CheckpointPositionsField?.GetValue(controller) as Vector3[];
            if (playerBody == null
                || playerCollider == null
                || checkpointPositions == null
                || checkpointPositions.Length == 0
                || CurrentCheckpointIndexField == null)
            {
                Debug.LogError("[Phase02] Rolling respawn could not access the required reset state.");
                enabled = false;
                yield break;
            }

            LatestSafePosition = new Vector3(4f, controller.SurfaceHeightAt(4f), 0f);
            HasSafePose = true;
            wasResetting = controller.IsResetting;
        }

        private void Update()
        {
            if (controller == null || motor == null || playerBody == null)
                return;

            bool resetting = controller.IsResetting;
            if (!wasResetting && resetting)
                BeginResetRedirect();

            if (wasResetting && !resetting)
                EndResetRedirect();

            wasResetting = resetting;
            if (!resetting && !overrideActive && controller.IsPlayable)
                TrackLatestSafeGroundedPose();
            else
                groundedHold = 0f;
        }

        private void BeginResetRedirect()
        {
            groundedHold = 0f;
            if (!HasSafePose || checkpointPositions == null || checkpointPositions.Length == 0)
                return;

            originalCheckpointIndex = controller.CurrentCheckpointIndex;
            injectedCheckpointSlot = originalCheckpointIndex >= 0
                ? Mathf.Clamp(originalCheckpointIndex, 0, checkpointPositions.Length - 1)
                : 0;
            originalCheckpointPosition = checkpointPositions[injectedCheckpointSlot];

            checkpointPositions[injectedCheckpointSlot] = LatestSafePosition;
            if (originalCheckpointIndex < 0)
                CurrentCheckpointIndexField.SetValue(controller, injectedCheckpointSlot);

            overrideActive = true;
            RespawnOverrideCount++;
        }

        private void EndResetRedirect()
        {
            if (!overrideActive)
                return;

            cameraRig.SnapToTarget();

            if (checkpointPositions != null
                && injectedCheckpointSlot >= 0
                && injectedCheckpointSlot < checkpointPositions.Length)
            {
                checkpointPositions[injectedCheckpointSlot] = originalCheckpointPosition;
            }

            CurrentCheckpointIndexField.SetValue(controller, originalCheckpointIndex);
            injectedCheckpointSlot = -1;
            overrideActive = false;
        }

        private void OnDisable()
        {
            if (!overrideActive || controller == null)
                return;

            if (checkpointPositions != null
                && injectedCheckpointSlot >= 0
                && injectedCheckpointSlot < checkpointPositions.Length)
            {
                checkpointPositions[injectedCheckpointSlot] = originalCheckpointPosition;
            }

            CurrentCheckpointIndexField?.SetValue(controller, originalCheckpointIndex);
            injectedCheckpointSlot = -1;
            overrideActive = false;
        }

        private void TrackLatestSafeGroundedPose()
        {
            if (!motor.IsGrounded)
            {
                groundedHold = 0f;
                return;
            }

            Vector3 bodyPosition = playerBody.position;
            if (!IsSafeCaptureX(bodyPosition.x)
                || !TryFindStableSupport(bodyPosition, out RaycastHit support))
            {
                groundedHold = 0f;
                return;
            }

            groundedHold += Time.unscaledDeltaTime;
            if (groundedHold < GroundedHoldDuration)
                return;

            if (HasSafePose && bodyPosition.x < LatestSafePosition.x + MinimumCaptureAdvance)
                return;

            LatestSafePosition = new Vector3(bodyPosition.x, support.point.y, 0f);
            HasSafePose = true;
            groundedHold = 0f;
        }

        private bool IsSafeCaptureX(float x)
        {
            if (x < 4f || x > Phase02VolcanoEscapeController.AuthoredRouteLength - 5f)
                return false;

            for (int i = 0; i < controller.GapIntervals.Count; i++)
            {
                Vector2 gap = controller.GapIntervals[i];
                if (x >= gap.x - GapSafetyMargin && x <= gap.y + GapSafetyMargin)
                    return false;
            }

            return true;
        }

        private bool TryFindStableSupport(Vector3 position, out RaycastHit stableHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                position + Vector3.up * 1.4f,
                Vector3.down,
                3f,
                ~0,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider candidate = hits[i].collider;
                if (candidate == null || candidate == playerCollider)
                    continue;

                string objectName = candidate.gameObject.name;
                if (!objectName.Contains("Stable Platform")
                    || objectName.Contains("Collapsing Ground"))
                    continue;

                stableHit = hits[i];
                return true;
            }

            stableHit = default;
            return false;
        }
    }
}
