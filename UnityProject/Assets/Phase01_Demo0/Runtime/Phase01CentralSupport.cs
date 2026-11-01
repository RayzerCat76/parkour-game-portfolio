using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01CentralSupport : MonoBehaviour
    {
        [SerializeField] private MeshRenderer walkableTopRenderer;
        [SerializeField] private MeshRenderer lowerMassRenderer;
        [SerializeField] private Renderer radioBodyRenderer;
        [SerializeField] private float supportRadius = 1.78f;
        [SerializeField] private float supportTopHeight = 0.04f;
        [SerializeField] private float supportBottomHeight = -5.98f;

        public MeshRenderer WalkableTopRenderer => walkableTopRenderer;
        public MeshRenderer LowerMassRenderer => lowerMassRenderer;
        public Renderer RadioBodyRenderer => radioBodyRenderer;
        public float SupportRadius => supportRadius;
        public float SupportTopHeight => supportTopHeight;
        public float SupportBottomHeight => supportBottomHeight;
        public float RadioBaseHeight => radioBodyRenderer != null
            ? radioBodyRenderer.bounds.min.y
            : float.PositiveInfinity;
        public float RadioContactError => Mathf.Abs(RadioBaseHeight - supportTopHeight);
        public bool HasValidReferences => walkableTopRenderer != null
            && lowerMassRenderer != null
            && radioBodyRenderer != null
            && supportRadius > 0f
            && supportBottomHeight < supportTopHeight;

        public void Configure(
            MeshRenderer topRenderer,
            MeshRenderer massRenderer,
            Renderer supportedRadioBody,
            float radius,
            float topHeight,
            float bottomHeight)
        {
            walkableTopRenderer = topRenderer;
            lowerMassRenderer = massRenderer;
            radioBodyRenderer = supportedRadioBody;
            supportRadius = radius;
            supportTopHeight = topHeight;
            supportBottomHeight = bottomHeight;
        }

        public float RadioFootprintMargin(Vector3 horizontalDirection)
        {
            if (radioBodyRenderer == null)
                return float.NegativeInfinity;

            Vector3 direction = Vector3.ProjectOnPlane(horizontalDirection, Vector3.up).normalized;
            Bounds bounds = radioBodyRenderer.bounds;
            float projectedExtent = Mathf.Abs(direction.x) * bounds.extents.x
                + Mathf.Abs(direction.z) * bounds.extents.z;
            return supportRadius - projectedExtent;
        }
    }
}
