using UnityEngine;

namespace Phase02.VolcanoEscape
{
    public sealed class Phase02CameraRig : MonoBehaviour
    {
        private Camera sceneCamera;
        private Transform target;
        private Rigidbody targetBody;
        private Phase02VolcanoEscapeController controller;
        private Vector3 smoothVelocity;
        private Vector3 basePosition;
        private float shakeClock;
        private float shakeTimeRemaining;
        private float shakeDuration;
        private float shakeAmplitude;
        private float nextAmbientTremorAt;
        private int tremorSequence;

        public Camera SceneCamera => sceneCamera;
        public float FollowOffsetX { get; private set; } = 5.5f;
        public float BaseHeight { get; private set; } = 4.25f;
        public bool IsShaking => shakeTimeRemaining > 0f;
        public Vector3 BaseCameraPosition => basePosition;

        public void Configure(Camera camera, Transform followTarget, Phase02VolcanoEscapeController owner)
        {
            sceneCamera = camera;
            target = followTarget;
            targetBody = followTarget != null ? followTarget.GetComponent<Rigidbody>() : null;
            controller = owner;
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 7.25f;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color(0.055f, 0.012f, 0.018f);
            nextAmbientTremorAt = Time.unscaledTime + 6f;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (sceneCamera == null || target == null)
                return;

            basePosition = ResolveDesiredPosition();
            sceneCamera.transform.position = basePosition;
            smoothVelocity = Vector3.zero;
            shakeTimeRemaining = 0f;
            shakeDuration = 0f;
            shakeAmplitude = 0f;
            nextAmbientTremorAt = Time.unscaledTime + 2.5f;
        }

        public void PulseShake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f)
                return;

            shakeAmplitude = Mathf.Max(shakeAmplitude, amplitude);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, duration);
        }

        private Vector3 ResolveDesiredPosition()
        {
            // Rigidbody.position is authoritative immediately after a physics
            // teleport, while Transform.position can remain stale until the
            // next physics step. Reading the body prevents the camera from
            // snapping to the pre-death location during a local reset.
            Vector3 targetPosition = targetBody != null
                ? targetBody.position
                : target.position;
            return new Vector3(
                targetPosition.x + FollowOffsetX,
                BaseHeight + targetPosition.y * 0.18f,
                -18f);
        }

        private void LateUpdate()
        {
            if (sceneCamera == null || target == null || controller == null)
                return;

            Vector3 desired = ResolveDesiredPosition();
            basePosition = Vector3.SmoothDamp(
                basePosition,
                desired,
                ref smoothVelocity,
                0.16f,
                80f,
                Time.unscaledDeltaTime);

            ScheduleAmbientTremor();
            Vector3 shakeOffset = ResolveShakeOffset();
            sceneCamera.transform.position = basePosition + shakeOffset;
        }

        private void ScheduleAmbientTremor()
        {
            if (controller.IsResetting)
            {
                shakeTimeRemaining = 0f;
                nextAmbientTremorAt = Time.unscaledTime + 2.5f;
                return;
            }

            if (Time.unscaledTime < nextAmbientTremorAt)
                return;

            int section = controller.CurrentSectionIndex;
            if (section >= 4)
            {
                nextAmbientTremorAt = Time.unscaledTime + 30f;
                return;
            }

            float amplitude;
            float duration;
            float baseInterval;
            switch (section)
            {
                case 0:
                    amplitude = 0.09f;
                    duration = 0.32f;
                    baseInterval = 6.8f;
                    break;
                case 1:
                    amplitude = 0.12f;
                    duration = 0.36f;
                    baseInterval = 5.7f;
                    break;
                case 2:
                    amplitude = 0.16f;
                    duration = 0.42f;
                    baseInterval = 4.6f;
                    break;
                default:
                    amplitude = 0.21f;
                    duration = 0.48f;
                    baseInterval = 3.4f;
                    break;
            }

            PulseShake(amplitude, duration);
            float deterministicVariation = (tremorSequence % 3) * 0.65f;
            tremorSequence++;
            nextAmbientTremorAt = Time.unscaledTime + baseInterval + deterministicVariation;
        }

        private Vector3 ResolveShakeOffset()
        {
            if (shakeTimeRemaining <= 0f || controller.IsResetting)
                return Vector3.zero;

            shakeTimeRemaining = Mathf.Max(0f, shakeTimeRemaining - Time.unscaledDeltaTime);
            shakeClock += Time.unscaledDeltaTime * 25f;
            float normalized = shakeDuration <= 0f ? 0f : 1f - shakeTimeRemaining / shakeDuration;
            float envelope = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            float magnitude = shakeAmplitude * envelope;

            if (shakeTimeRemaining <= 0f)
            {
                shakeAmplitude = 0f;
                shakeDuration = 0f;
            }

            return new Vector3(
                Mathf.Sin(shakeClock * 1.17f) * magnitude,
                Mathf.Sin(shakeClock * 1.93f + 0.7f) * magnitude * 0.72f,
                0f);
        }
    }
}
