using System;
using System.Collections;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01FadingSupport : MonoBehaviour
    {
        private static readonly Collider[] OccupancyHits = new Collider[8];

        public const float WarningDelaySeconds = 0.8f;
        public const float CollisionDisableDelaySeconds = 1.1f;
        public const float RestoreDelaySeconds = 1.5f;

        [SerializeField] private int supportOrder;
        [SerializeField] private float innerScalar;
        [SerializeField] private float outerScalar;
        [SerializeField] private Renderer supportRenderer;
        [SerializeField] private Collider supportCollider;

        private bool presentationVisible = true;
        private bool activationArmed = true;
        private MaterialPropertyBlock propertyBlock;
        private Color normalColor = Color.white;
        private Bounds activationBounds;
        private Phase01PlayerMotor lastActivatingPlayer;

        public event Action<Phase01FadingSupport> Activated;

        public int SupportOrder => supportOrder;
        public float InnerScalar => innerScalar;
        public float OuterScalar => outerScalar;
        public int ActivationCount { get; private set; }
        public bool IsArmed => activationArmed;
        public bool IsCycleActive { get; private set; }
        public bool IsWarning { get; private set; }
        public bool IsSolid => supportCollider != null && supportCollider.enabled;
        public float LastWarningElapsed { get; private set; }
        public float LastCollisionDisabledElapsed { get; private set; }
        public float LastRestoreElapsed { get; private set; }

        public void Configure(
            int order,
            float inner,
            float outer,
            Renderer renderer,
            Collider collider)
        {
            supportOrder = order;
            innerScalar = inner;
            outerScalar = outer;
            supportRenderer = renderer;
            supportCollider = collider;
            ResolvePresentation();
            CacheActivationBounds();
        }

        private void Awake()
        {
            supportRenderer ??= GetComponent<Renderer>();
            supportCollider ??= GetComponent<Collider>();
            ResolvePresentation();
            CacheActivationBounds();
        }

        private void ResolvePresentation()
        {
            if (supportRenderer != null && supportRenderer.sharedMaterial != null)
                normalColor = supportRenderer.sharedMaterial.color;
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void CacheActivationBounds()
        {
            if (supportCollider != null)
                activationBounds = supportCollider.bounds;
        }

        private void OnCollisionEnter(Collision collision) => TryBeginFrom(collision);
        private void OnCollisionStay(Collision collision) => TryBeginFrom(collision);

        private void TryBeginFrom(Collision collision)
        {
            Phase01PlayerMotor player = ResolvePlayer(collision.collider);
            if (player == null || !HasValidTopContact(collision, player))
                return;
            BeginCycle(player);
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

        public bool BeginCycle() => BeginCycle(null);

        private bool BeginCycle(Phase01PlayerMotor player)
        {
            if (!activationArmed
                || IsCycleActive
                || supportCollider == null
                || !supportCollider.enabled)
                return false;

            activationArmed = false;
            lastActivatingPlayer = player;
            IsCycleActive = true;
            IsWarning = false;
            ActivationCount++;
            Activated?.Invoke(this);
            StartCoroutine(RunCycle());
            return true;
        }

        private IEnumerator RunCycle()
        {
            float startedAt = Time.time;
            yield return WaitUntilElapsed(startedAt, WarningDelaySeconds);
            LastWarningElapsed = Time.time - startedAt;
            IsWarning = true;
            // A white-hot flare rather than a hue. Phase 1 is the colourless phase - hue is what the later
            // phases regain - so the warning has to be carried by value, and a support that lights up past
            // white both reads instantly against the pale stone path and is the only moving thing in shot.
            ApplyColor(new Color(2.4f, 2.45f, 2.55f, 1f));

            yield return WaitUntilElapsed(startedAt, CollisionDisableDelaySeconds);
            LastCollisionDisabledElapsed = Time.time - startedAt;
            supportCollider.enabled = false;
            if (supportRenderer != null)
                supportRenderer.enabled = false;

            yield return WaitUntilElapsed(startedAt, RestoreDelaySeconds);
            LastRestoreElapsed = Time.time - startedAt;
            supportCollider.enabled = true;
            IsWarning = false;
            IsCycleActive = false;
            if (lastActivatingPlayer == null)
                activationArmed = true;
            ApplyColor(normalColor);
            if (supportRenderer != null)
                supportRenderer.enabled = presentationVisible;
        }

        private void FixedUpdate()
        {
            if (!IsCycleActive && activationArmed && TryFindOccupyingPlayer(out Phase01PlayerMotor player))
            {
                BeginCycle(player);
                return;
            }
            if (IsCycleActive || activationArmed || lastActivatingPlayer == null)
                return;
            if (IsInsideActivationArea(lastActivatingPlayer))
                return;
            activationArmed = true;
            lastActivatingPlayer = null;
        }

        private bool TryFindOccupyingPlayer(out Phase01PlayerMotor player)
        {
            player = null;
            if (supportCollider == null || !supportCollider.enabled)
                return false;

            Vector3 up = transform.up;
            Vector3 center = activationBounds.center
                + up * (activationBounds.extents.y + 0.125f);
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
                Phase01PlayerMotor candidate = ResolvePlayer(OccupancyHits[i]);
                if (candidate != null && IsInsideActivationArea(candidate))
                {
                    player = candidate;
                    return true;
                }
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

        private static IEnumerator WaitUntilElapsed(float startedAt, float delay)
        {
            while (Time.time - startedAt < delay)
                yield return null;
        }

        private void ApplyColor(Color color)
        {
            if (supportRenderer == null)
                return;
            supportRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            supportRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetPresentationVisible(bool visible)
        {
            presentationVisible = visible;
            if (supportRenderer != null)
                supportRenderer.enabled = visible && IsSolid;
        }
    }
}
