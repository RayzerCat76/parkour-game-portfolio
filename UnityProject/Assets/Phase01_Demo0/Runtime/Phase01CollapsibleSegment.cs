using System.Collections;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01CollapsibleSegment : MonoBehaviour
    {
        private static readonly Collider[] OccupancyHits = new Collider[8];

        public const float WarningDelaySeconds = 0.9f;

        [SerializeField] private int collapseOrder;
        [SerializeField] private float innerScalar;
        [SerializeField] private float outerScalar;
        [SerializeField] private Renderer segmentRenderer;
        [SerializeField] private Collider segmentCollider;

        private Phase01Branch4ControlledCollapse route;
        private bool presentationVisible = true;
        private MaterialPropertyBlock propertyBlock;
        private Bounds activationBounds;

        public int CollapseOrder => collapseOrder;
        public float InnerScalar => innerScalar;
        public float OuterScalar => outerScalar;
        public int ContactActivationCount { get; private set; }
        public bool IsWarning { get; private set; }
        public bool IsCollapsed { get; private set; }
        public bool IsSolid => segmentCollider != null && segmentCollider.enabled;
        public float LastWarningDuration { get; private set; }

        public void Configure(
            int order,
            float inner,
            float outer,
            Renderer renderer,
            Collider collider)
        {
            collapseOrder = order;
            innerScalar = inner;
            outerScalar = outer;
            segmentRenderer = renderer;
            segmentCollider = collider;
            propertyBlock ??= new MaterialPropertyBlock();
            CacheActivationBounds();
        }

        private void Awake()
        {
            segmentRenderer ??= GetComponent<Renderer>();
            segmentCollider ??= GetComponent<Collider>();
            propertyBlock ??= new MaterialPropertyBlock();
            CacheActivationBounds();
        }

        public void Arm(Phase01Branch4ControlledCollapse owner) => route = owner;

        private void CacheActivationBounds()
        {
            if (segmentCollider != null)
                activationBounds = segmentCollider.bounds;
        }

        private void FixedUpdate()
        {
            if (route == null || !route.IsActivated || IsWarning || IsCollapsed)
                return;
            if (TryFindOccupyingPlayer())
                BeginCollapse();
        }

        private bool TryFindOccupyingPlayer()
        {
            if (segmentCollider == null || !segmentCollider.enabled)
                return false;
            Vector3 center = activationBounds.center
                + transform.up * (activationBounds.extents.y + 0.125f);
            Vector3 halfExtents = new Vector3(
                activationBounds.extents.x + 0.12f,
                0.325f,
                activationBounds.extents.z + 0.12f);
            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                OccupancyHits,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Phase01PlayerMotor player = ResolvePlayer(OccupancyHits[i]);
                if (player != null && IsInsideActivationArea(player))
                    return true;
            }
            return false;
        }

        private bool IsInsideActivationArea(Phase01PlayerMotor player)
        {
            Vector3 foot = player.transform.position;
            const float horizontalMargin = 0.12f;
            return foot.x >= activationBounds.min.x - horizontalMargin
                && foot.x <= activationBounds.max.x + horizontalMargin
                && foot.z >= activationBounds.min.z - horizontalMargin
                && foot.z <= activationBounds.max.z + horizontalMargin
                && foot.y >= activationBounds.max.y - 0.2f
                && foot.y <= activationBounds.max.y + 0.45f;
        }

        private void OnCollisionEnter(Collision collision) => TryBeginFrom(collision);
        private void OnCollisionStay(Collision collision) => TryBeginFrom(collision);

        private void TryBeginFrom(Collision collision)
        {
            Phase01PlayerMotor player = ResolvePlayer(collision.collider);
            if (player != null && HasValidTopContact(collision, player))
                BeginCollapse();
        }

        private Phase01PlayerMotor ResolvePlayer(Collider other)
        {
            if (other == null)
                return null;
            Phase01PlayerMotor player = other.GetComponentInParent<Phase01PlayerMotor>();
            return player != null
                ? player
                : other.transform.root.GetComponentInChildren<Phase01PlayerMotor>();
        }

        private bool HasValidTopContact(Collision collision, Phase01PlayerMotor player)
        {
            Vector3 up = transform.up;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float upAlignment = Mathf.Abs(Vector3.Dot(contact.normal, up));
                float playerAboveContact = Vector3.Dot(player.transform.position - contact.point, up);
                if (upAlignment >= 0.6f && playerAboveContact >= -0.12f)
                    return true;
            }
            return false;
        }

        public bool BeginCollapse()
        {
            if (IsWarning
                || IsCollapsed
                || segmentCollider == null
                || !segmentCollider.enabled
                || route == null
                || !route.TryBeginSegment(collapseOrder))
                return false;

            ContactActivationCount++;
            IsWarning = true;
            ApplyWarningColor();
            StartCoroutine(CollapseAfterWarning());
            return true;
        }

        private IEnumerator CollapseAfterWarning()
        {
            float startedAt = Time.time;
            while (Time.time - startedAt < WarningDelaySeconds)
                yield return null;

            LastWarningDuration = Time.time - startedAt;
            segmentCollider.enabled = false;
            IsWarning = false;
            IsCollapsed = true;
            if (segmentRenderer != null)
                segmentRenderer.enabled = false;
        }

        private void ApplyWarningColor()
        {
            if (segmentRenderer == null)
                return;
            segmentRenderer.GetPropertyBlock(propertyBlock);
            Color warning = new Color(1f, 0.32f, 0.08f, 1f);
            propertyBlock.SetColor("_BaseColor", warning);
            propertyBlock.SetColor("_Color", warning);
            segmentRenderer.SetPropertyBlock(propertyBlock);
        }

        public void RestoreArmed()
        {
            StopAllCoroutines();
            IsWarning = false;
            IsCollapsed = false;
            LastWarningDuration = 0f;
            if (segmentCollider != null)
                segmentCollider.enabled = true;
            propertyBlock.Clear();
            if (segmentRenderer != null)
            {
                segmentRenderer.SetPropertyBlock(propertyBlock);
                segmentRenderer.enabled = presentationVisible;
            }
        }

        public void SetPresentationVisible(bool visible)
        {
            presentationVisible = visible;
            if (segmentRenderer != null)
                segmentRenderer.enabled = presentationVisible && !IsCollapsed;
        }
    }
}
