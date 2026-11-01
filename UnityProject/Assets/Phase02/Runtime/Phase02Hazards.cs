using System.Collections;
using Phase01.MovementSandbox;
using UnityEngine;

namespace Phase02.VolcanoEscape
{
    public abstract class Phase02ResettableHazard : MonoBehaviour
    {
        protected Phase02VolcanoEscapeController Controller { get; private set; }
        public int SectionIndex { get; private set; }
        public bool HasActivated { get; protected set; }

        protected void ConfigureBase(Phase02VolcanoEscapeController controller, int sectionIndex)
        {
            Controller = controller;
            SectionIndex = sectionIndex;
            controller.RegisterHazard(this);
        }

        public abstract void ResetHazard();
    }

    public sealed class Phase02CollapsingPlatform : Phase02ResettableHazard
    {
        private static readonly Color DefaultStableColor = new Color(0.5f, 0.17f, 0.11f);
        private static readonly Color WarningColor = new Color(0.95f, 0.24f, 0.08f);

        private Renderer platformRenderer;
        private Collider platformCollider;
        private Color stableColor;
        private Coroutine collapseRoutine;

        public float WarningDuration { get; private set; } = 1.75f;
        public bool CollisionEnabled => platformCollider != null && platformCollider.enabled;

        public void Configure(
            Phase02VolcanoEscapeController controller,
            int sectionIndex,
            Renderer renderer,
            Collider collider)
        {
            ConfigureBase(controller, sectionIndex);
            platformRenderer = renderer;
            platformCollider = collider;
            stableColor = ReadRendererColor(renderer, DefaultStableColor);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (HasActivated || Controller == null || Controller.IsResetting)
                return;
            if (collision.gameObject.GetComponentInParent<Phase01ParkourMotor>() == null)
                return;

            HasActivated = true;
            collapseRoutine = StartCoroutine(CollapseRoutine());
        }

        private IEnumerator CollapseRoutine()
        {
            Controller.NotifyHazardWarning("CollapsingGround");
            WriteRendererColor(platformRenderer, WarningColor);
            yield return new WaitForSeconds(WarningDuration);
            Controller.NotifyHazardTriggered("CollapsingGround");
            platformCollider.enabled = false;
            platformRenderer.enabled = false;
            collapseRoutine = null;
        }

        public override void ResetHazard()
        {
            if (collapseRoutine != null)
                StopCoroutine(collapseRoutine);
            collapseRoutine = null;
            HasActivated = false;
            if (platformCollider != null)
                platformCollider.enabled = true;
            if (platformRenderer != null)
            {
                platformRenderer.enabled = true;
                WriteRendererColor(platformRenderer, stableColor);
            }
        }

        private static Color ReadRendererColor(Renderer renderer, Color fallback)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return fallback;

            Material material = renderer.sharedMaterial;
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            return fallback;
        }

        private static void WriteRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            Material shared = renderer.sharedMaterial;
            bool hasBaseColor = shared.HasProperty("_BaseColor");
            bool hasColor = shared.HasProperty("_Color");
            if (!hasBaseColor && !hasColor)
                return;

            Material material = renderer.material;
            if (hasBaseColor && material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (hasColor && material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }
    }

    public sealed class Phase02DebrisHazard : Phase02ResettableHazard
    {
        private float activationX;
        private float impactX;
        private float surfaceY;
        private GameObject warningMarker;
        private GameObject debrisVisual;
        private Coroutine routine;

        public float WarningDuration { get; private set; } = 1f;
        public float ImpactX => impactX;

        public void Configure(
            Phase02VolcanoEscapeController controller,
            int sectionIndex,
            float activationPosition,
            float impactPosition,
            float groundHeight,
            GameObject marker,
            GameObject debris)
        {
            ConfigureBase(controller, sectionIndex);
            activationX = activationPosition;
            impactX = impactPosition;
            surfaceY = groundHeight;
            warningMarker = marker;
            debrisVisual = debris;
            ResetHazard();
        }

        private void Update()
        {
            if (Controller == null
                || !Controller.IsPlayable
                || Controller.IsResetting
                || HasActivated
                || Controller.PlayerX < activationX)
                return;

            HasActivated = true;
            routine = StartCoroutine(StrikeRoutine());
        }

        private IEnumerator StrikeRoutine()
        {
            warningMarker.SetActive(true);
            Controller.NotifyHazardWarning("FallingDebris");
            yield return new WaitForSeconds(WarningDuration);

            debrisVisual.SetActive(true);
            Vector3 start = new Vector3(impactX, surfaceY + 12f, 0f);
            Vector3 end = new Vector3(impactX, surfaceY + 0.8f, 0f);
            debrisVisual.transform.position = start;
            float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                debrisVisual.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            Controller.NotifyHazardTriggered("FallingDebris");
            if (Mathf.Abs(Controller.PlayerX - impactX) <= 1.35f
                && Controller.PlayerY <= surfaceY + 2.7f)
                Controller.Fail(SectionIndex, "FallingDebris");

            yield return new WaitForSeconds(0.35f);
            warningMarker.SetActive(false);
            debrisVisual.SetActive(false);
            routine = null;
        }

        public override void ResetHazard()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            HasActivated = false;
            if (warningMarker != null)
                warningMarker.SetActive(false);
            if (debrisVisual != null)
                debrisVisual.SetActive(false);
        }
    }

    public sealed class Phase02ShockwaveHazard : Phase02ResettableHazard
    {
        private float activationX;
        private float startX;
        private float endX;
        private float surfaceY;
        private GameObject waveVisual;
        private Coroutine routine;

        public float WarningDuration { get; private set; } = 0.85f;
        public float TravelDuration { get; private set; } = 2.4f;

        public void Configure(
            Phase02VolcanoEscapeController controller,
            int sectionIndex,
            float activationPosition,
            float waveStart,
            float waveEnd,
            float groundHeight,
            GameObject visual)
        {
            ConfigureBase(controller, sectionIndex);
            activationX = activationPosition;
            startX = waveStart;
            endX = waveEnd;
            surfaceY = groundHeight;
            waveVisual = visual;
            ResetHazard();
        }

        private void Update()
        {
            if (Controller == null
                || !Controller.IsPlayable
                || Controller.IsResetting
                || HasActivated
                || Controller.PlayerX < activationX)
                return;

            HasActivated = true;
            routine = StartCoroutine(WaveRoutine());
        }

        private IEnumerator WaveRoutine()
        {
            waveVisual.SetActive(true);
            waveVisual.transform.position = new Vector3(startX, surfaceY + 0.55f, 0f);
            Controller.NotifyHazardWarning("RedShockwave");
            yield return new WaitForSeconds(WarningDuration);
            Controller.NotifyHazardTriggered("RedShockwave");

            float elapsed = 0f;
            while (elapsed < TravelDuration)
            {
                elapsed += Time.deltaTime;
                float x = Mathf.Lerp(startX, endX, Mathf.Clamp01(elapsed / TravelDuration));
                waveVisual.transform.position = new Vector3(x, surfaceY + 0.55f, 0f);
                if (Mathf.Abs(Controller.PlayerX - x) <= 0.85f
                    && Controller.PlayerY <= surfaceY + 1.65f)
                {
                    Controller.Fail(SectionIndex, "RedShockwave");
                    yield break;
                }
                yield return null;
            }

            waveVisual.SetActive(false);
            routine = null;
        }

        public override void ResetHazard()
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = null;
            HasActivated = false;
            if (waveVisual != null)
                waveVisual.SetActive(false);
        }
    }

    public sealed class Phase02CollapseFront : Phase02ResettableHazard
    {
        private GameObject frontVisual;
        private float resetX;
        private float speed;
        private bool active;

        public bool IsActive => active;
        public float FrontX => frontVisual != null ? frontVisual.transform.position.x : resetX;
        public float ProximityNormalized
        {
            get
            {
                if (!active || Controller == null)
                    return 0f;
                float distance = Controller.PlayerX - FrontX;
                return 1f - Mathf.Clamp01(distance / 24f);
            }
        }

        public void Configure(
            Phase02VolcanoEscapeController controller,
            int sectionIndex,
            GameObject visual,
            float initialX,
            float unitsPerSecond)
        {
            ConfigureBase(controller, sectionIndex);
            frontVisual = visual;
            resetX = initialX;
            speed = unitsPerSecond;
            ResetHazard();
        }

        public void Activate(float checkpointX)
        {
            resetX = checkpointX - 18f;
            frontVisual.transform.position = new Vector3(resetX, 2.5f, 0f);
            frontVisual.SetActive(true);
            active = true;
            HasActivated = true;
        }

        public void Deactivate()
        {
            active = false;
            if (frontVisual != null)
                frontVisual.SetActive(false);
        }

        private void Update()
        {
            if (!active || Controller == null || Controller.IsResetting)
                return;

            frontVisual.transform.position += Vector3.right * (speed * Time.deltaTime);
            if (Controller.PlayerX - FrontX <= 0.85f)
                Controller.Fail(SectionIndex, "CollapseFront");
        }

        public override void ResetHazard()
        {
            active = false;
            HasActivated = false;
            if (frontVisual != null)
            {
                frontVisual.transform.position = new Vector3(resetX, 2.5f, 0f);
                frontVisual.SetActive(false);
            }
        }
    }
}
