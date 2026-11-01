using System.Collections;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01Branch2RouteTransformation : MonoBehaviour
    {
        [SerializeField] private Phase01BranchTerrain upperTerrain;
        [SerializeField] private GameObject upperGapColliders;
        [SerializeField] private GameObject lowerReturnRoot;
        [System.NonSerialized] private float collapseDuration = 1.4f;
        [SerializeField] private bool transformed;

        private bool isAnimating;
        private GameObject fallingPiecesRoot;
        private Transform[] settlingPieces;
        private Transform[] fallingPieces;
        private Vector3[] finalPositions;
        private Quaternion[] finalRotations;
        private Vector3[] startPositions;
        private Quaternion[] startRotations;
        private Vector3[] fallStartPositions;
        private Quaternion[] fallStartRotations;
        private static readonly Vector2[] CanonicalReturnGaps =
        {
            new Vector2(120f, 121.8f),
            new Vector2(100f, 102f),
            new Vector2(78f, 79.6f),
            new Vector2(56f, 57.8f),
            new Vector2(34f, 36f),
            new Vector2(14f, 15.6f)
        };
        private Vector2[] returnGapIntervals = (Vector2[])CanonicalReturnGaps.Clone();

        public bool IsTransformed => transformed;
        public bool IsAnimating => isAnimating;
        public int ActivationCount { get; private set; }
        public bool LowerReturnRouteActive => lowerReturnRoot != null && lowerReturnRoot.activeSelf;
        public bool UpperRouteVisible => upperTerrain != null && upperTerrain.IsPresentationVisible;
        public bool FallingPiecesVisible => fallingPiecesRoot != null && fallingPiecesRoot.activeSelf;
        public bool LowerCollisionReady { get; private set; }
        public int SettledPieceCount { get; private set; }
        public int FallingPieceCount => fallingPieces != null ? fallingPieces.Length : 0;
        public float CollapseDuration => collapseDuration;
        public float LastCollapseDuration { get; private set; }
        public float AnimationProgress { get; private set; }
        public int ReturnGapCount => returnGapIntervals.Length;
        public float ReturnGapCenter => ReturnGapCount == 0 ? 0f : (returnGapIntervals[0].x + returnGapIntervals[0].y) * 0.5f;

        /// <summary>True only for the intentionally open, transformed return-route hole.</summary>
        public bool IsReturnGap(float scalar)
        {
            if (!transformed)
                return false;
            for (int i = 0; i < returnGapIntervals.Length; i++)
            {
                Vector2 interval = returnGapIntervals[i];
                if (scalar > interval.x + 0.001f && scalar < interval.y - 0.001f)
                    return true;
            }
            return false;
        }

        public Vector2 GetReturnGap(int index) => returnGapIntervals[index];

        public void Configure(
            Phase01BranchTerrain terrain,
            GameObject gapColliders,
            GameObject lowerRoot)
        {
            upperTerrain = terrain;
            upperGapColliders = gapColliders;
            lowerReturnRoot = lowerRoot;
            transformed = false;
            isAnimating = false;
            ActivationCount = 0;
            LowerCollisionReady = false;
            SettledPieceCount = 0;
            AnimationProgress = 0f;
            returnGapIntervals = (Vector2[])CanonicalReturnGaps.Clone();
            if (lowerReturnRoot != null)
                lowerReturnRoot.SetActive(false);
        }

        public IEnumerator Activate()
        {
            if (transformed || isAnimating)
                yield break;

            isAnimating = true;
            ActivationCount++;
            if (upperTerrain == null)
                upperTerrain = GetComponent<Phase01BranchTerrain>();
            if (upperGapColliders != null)
                upperGapColliders.SetActive(false);

            CacheRuntimePieces();
            if (lowerReturnRoot != null)
                lowerReturnRoot.SetActive(true);
            if (fallingPiecesRoot != null)
                fallingPiecesRoot.SetActive(true);
            SetLowerRouteCollision(false);
            PreparePieces();
            if (upperTerrain != null)
                upperTerrain.SetPresentationVisible(false);

            float startedAt = Time.realtimeSinceStartup;
            float elapsed = 0f;
            const float pieceDuration = 0.68f;
            const float sequenceDelay = 0.12f;
            while (elapsed < collapseDuration)
            {
                elapsed += Time.deltaTime;
                AnimationProgress = Mathf.Clamp01(elapsed / collapseDuration);
                for (int i = 0; i < settlingPieces.Length; i++)
                {
                    int order = i == 0 ? 0 : i - 1;
                    ApplySettle(i, (elapsed - order * sequenceDelay) / pieceDuration);
                }
                for (int i = 0; i < fallingPieces.Length; i++)
                    ApplyFall(i, (elapsed - i * sequenceDelay) / pieceDuration);
                yield return null;
            }

            for (int i = 0; i < settlingPieces.Length; i++)
                ApplySettle(i, 1f);
            if (fallingPiecesRoot != null)
                fallingPiecesRoot.SetActive(false);

            if (upperTerrain != null)
                upperTerrain.SetCollisionEnabled(false);
            SetLowerRouteCollision(true);
            Physics.SyncTransforms();
            SettledPieceCount = settlingPieces.Length;
            LowerCollisionReady = true;
            AnimationProgress = 1f;
            LastCollapseDuration = Time.realtimeSinceStartup - startedAt;
            transformed = true;
            isAnimating = false;
        }

        public float SampleHeight(float scalar, float fallback)
        {
            if (!transformed)
                return fallback;
            if (IsReturnGap(scalar))
                return -8f;
            if (scalar >= 121.8f)
                return 0.3f;
            if (scalar >= 102f)
                return 0f;
            if (scalar >= 79.6f)
                return -0.35f;
            if (scalar >= 57.8f)
                return 0.15f;
            if (scalar >= 36f)
                return -0.45f;
            return 0f;
        }

        private void CacheRuntimePieces()
        {
            if (lowerReturnRoot == null || lowerReturnRoot.transform.childCount < 7)
            {
                settlingPieces = new Transform[0];
                fallingPieces = new Transform[0];
                return;
            }

            Transform lower = lowerReturnRoot.transform;
            settlingPieces = new Transform[Mathf.Min(6, lower.childCount - 1)];
            for (int i = 0; i < settlingPieces.Length; i++)
            {
                settlingPieces[i] = lower.GetChild(i);
            }

            fallingPiecesRoot = lower.GetChild(lower.childCount - 1).gameObject;
            Transform falling = fallingPiecesRoot.transform;
            fallingPieces = new Transform[falling.childCount];
            for (int i = 0; i < fallingPieces.Length; i++)
                fallingPieces[i] = falling.GetChild(i);
        }

        private void PreparePieces()
        {
            float[] offsets = { 0f, 0.8f, 1.05f, 1.3f, 1.55f, 1.8f };
            float[] angles = { 0f, -8f, 7f, -7f, 8f, -6f };
            finalPositions = new Vector3[settlingPieces.Length];
            finalRotations = new Quaternion[settlingPieces.Length];
            startPositions = new Vector3[settlingPieces.Length];
            startRotations = new Quaternion[settlingPieces.Length];
            for (int i = 0; i < settlingPieces.Length; i++)
            {
                finalPositions[i] = settlingPieces[i].localPosition;
                finalRotations[i] = settlingPieces[i].localRotation;
                startPositions[i] = finalPositions[i] + Vector3.up * offsets[Mathf.Min(i, offsets.Length - 1)];
                startRotations[i] = finalRotations[i] * Quaternion.Euler(0f, 0f, angles[Mathf.Min(i, angles.Length - 1)]);
                settlingPieces[i].localPosition = startPositions[i];
                settlingPieces[i].localRotation = startRotations[i];
            }

            fallStartPositions = new Vector3[fallingPieces.Length];
            fallStartRotations = new Quaternion[fallingPieces.Length];
            for (int i = 0; i < fallingPieces.Length; i++)
            {
                fallStartPositions[i] = fallingPieces[i].localPosition;
                fallStartRotations[i] = fallingPieces[i].localRotation;
            }
        }

        private void ApplySettle(int index, float progress)
        {
            progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            settlingPieces[index].localPosition = Vector3.LerpUnclamped(startPositions[index], finalPositions[index], progress);
            settlingPieces[index].localRotation = Quaternion.SlerpUnclamped(startRotations[index], finalRotations[index], progress);
        }

        private void ApplyFall(int index, float progress)
        {
            float[] distances = { 8.5f, 9f, 9.5f, 10f };
            float[] spins = { 65f, -55f, 70f, -60f };
            progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            Transform piece = fallingPieces[index];
            piece.localPosition = fallStartPositions[index] +
                Vector3.down * (distances[Mathf.Min(index, distances.Length - 1)] * progress * progress);
            piece.localRotation = fallStartRotations[index] *
                Quaternion.Euler(0f, 0f, spins[Mathf.Min(index, spins.Length - 1)] * progress);
        }

        private void SetLowerRouteCollision(bool enabled)
        {
            if (lowerReturnRoot == null)
                return;
            foreach (Collider item in lowerReturnRoot.GetComponentsInChildren<Collider>(true))
                item.enabled = enabled;
        }

    }
}
