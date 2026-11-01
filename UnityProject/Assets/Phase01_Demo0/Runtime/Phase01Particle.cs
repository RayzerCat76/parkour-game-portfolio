using System.Collections;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01Particle : MonoBehaviour
    {
        public enum ParticleState
        {
            Available,
            Carried,
            Transferring,
            Deposited
        }

        [SerializeField] private int branchIndex;
        [SerializeField] private Transform viewCamera;
        [SerializeField] private Renderer particleRenderer;

        private Transform carryTarget;
        private Vector3 availablePosition;
        private Vector3 originalScale;

        public int BranchIndex => branchIndex;
        public ParticleState State { get; private set; }
        public bool IsPresentationVisible => particleRenderer != null && particleRenderer.enabled;

        public void Configure(int branch, Transform cameraTransform, Renderer renderer)
        {
            branchIndex = branch;
            viewCamera = cameraTransform;
            particleRenderer = renderer;
        }

        private void Awake()
        {
            availablePosition = transform.position;
            originalScale = transform.localScale;
            State = ParticleState.Available;
        }

        private void Update()
        {
            float phase = Time.time * 3f + branchIndex;
            if (State == ParticleState.Available)
            {
                transform.position = availablePosition + Vector3.up * (Mathf.Sin(phase) * 0.12f);
            }
            else if (State == ParticleState.Carried && carryTarget != null)
            {
                Vector3 screenRight = viewCamera != null ? viewCamera.right : Vector3.right;
                transform.position = carryTarget.position
                    + Vector3.up * (1.25f + Mathf.Sin(phase * 1.4f) * 0.18f)
                    + screenRight * (Mathf.Cos(phase) * 0.35f);
            }
        }

        public bool BeginCarry(Transform target)
        {
            if (State != ParticleState.Available || target == null)
                return false;

            carryTarget = target;
            State = ParticleState.Carried;
            return true;
        }

        public void SetPresentationVisible(bool visible)
        {
            if (particleRenderer != null)
                particleRenderer.enabled = visible && State != ParticleState.Deposited;
        }

        public IEnumerator TransferTo(Transform target, float duration)
        {
            if (State != ParticleState.Carried || target == null)
                yield break;

            State = ParticleState.Transferring;
            carryTarget = null;
            Vector3 start = transform.position;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                transform.position = Vector3.Lerp(start, target.position, eased);
                transform.localScale = Vector3.Lerp(originalScale, originalScale * 0.2f, eased);
                yield return null;
            }

            transform.position = target.position;
            transform.localScale = originalScale;
            State = ParticleState.Deposited;
            if (particleRenderer != null)
                particleRenderer.enabled = false;
        }
    }
}
