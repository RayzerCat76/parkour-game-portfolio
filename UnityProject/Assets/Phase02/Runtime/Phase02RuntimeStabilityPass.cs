using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    /// <summary>
    /// Runs one deterministic post-build geometry pass for Phase 02. Respawn
    /// ownership remains entirely in Phase02VolcanoEscapeController so the
    /// player and camera are moved exactly once per failure.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class Phase02RuntimeStabilityPass : MonoBehaviour
    {
        private readonly struct MissingCollapseSpan
        {
            public MissingCollapseSpan(int sectionIndex, float designStart, float designEnd)
            {
                SectionIndex = sectionIndex;
                DesignStart = designStart;
                DesignEnd = designEnd;
            }

            public int SectionIndex { get; }
            public float DesignStart { get; }
            public float DesignEnd { get; }
        }

        private static readonly MissingCollapseSpan[] MissingCollapseSpans =
        {
            new MissingCollapseSpan(2, 512f, 518f),
            new MissingCollapseSpan(2, 580f, 586f),
            new MissingCollapseSpan(3, 774f, 780f),
            new MissingCollapseSpan(3, 880f, 886f)
        };

        private static readonly FieldInfo CollapseCountField =
            typeof(Phase02VolcanoEscapeController).GetField(
                "<CollapsingPlatformCount>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CompletePhaseMethod =
            typeof(Phase02VolcanoEscapeController).GetMethod(
                "CompletePhase",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private Phase02VolcanoEscapeController controller;
        private bool geometryApplied;

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

            if (FindAnyObjectByType<Phase02RuntimeStabilityPass>() != null)
                return;

            new GameObject("Phase02 Runtime Stability Pass")
                .AddComponent<Phase02RuntimeStabilityPass>();
        }

        private IEnumerator Start()
        {
            yield return null;
            controller = FindAnyObjectByType<Phase02VolcanoEscapeController>();
            if (controller == null)
            {
                yield return null;
                controller = FindAnyObjectByType<Phase02VolcanoEscapeController>();
            }

            if (controller == null)
            {
                Debug.LogError("[Phase02] Runtime stability pass could not find the volcano-escape controller.");
                enabled = false;
                yield break;
            }

            ApplyGeometryOnce();
        }

        private void ApplyGeometryOnce()
        {
            if (geometryApplied)
                return;

            geometryApplied = true;
            RepairOverlappingStableColliders(controller.GapIntervals);
            RestoreMissingCollapseSpans();

            Phase02CollapsingPlatform[] collapses =
                FindObjectsByType<Phase02CollapsingPlatform>(FindObjectsSortMode.None);
            if (CollapseCountField != null)
                CollapseCountField.SetValue(controller, collapses.Length);

            Physics.SyncTransforms();
            if (collapses.Length != Phase02VolcanoEscapeController.AuthoredCollapsingPlatformCount)
            {
                Debug.LogError(
                    $"[Phase02] Expected {Phase02VolcanoEscapeController.AuthoredCollapsingPlatformCount} " +
                    $"collapse spans after the stability pass but found {collapses.Length}.");
            }
            else
            {
                Debug.Log("[Phase02] Runtime geometry pass applied once: gaps open and 10 collapse spans present.");
            }
        }

        private static void RepairOverlappingStableColliders(IReadOnlyList<Vector2> gaps)
        {
            BoxCollider[] colliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.name.Contains("Stable Platform"))
                    continue;

                Bounds bounds = collider.bounds;
                List<Vector2> overlaps = new List<Vector2>();
                for (int gapIndex = 0; gapIndex < gaps.Count; gapIndex++)
                {
                    Vector2 gap = gaps[gapIndex];
                    float start = Mathf.Max(bounds.min.x, gap.x);
                    float end = Mathf.Min(bounds.max.x, gap.y);
                    if (end > start + 0.01f)
                        overlaps.Add(new Vector2(start, end));
                }

                if (overlaps.Count == 0)
                    continue;

                overlaps.Sort((a, b) => a.x.CompareTo(b.x));
                Renderer sourceRenderer = collider.GetComponent<Renderer>();
                Material material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
                Transform parent = collider.transform.parent;
                string baseName = collider.gameObject.name;
                float cursor = bounds.min.x;
                int pieceIndex = 0;

                for (int overlapIndex = 0; overlapIndex < overlaps.Count; overlapIndex++)
                {
                    Vector2 overlap = overlaps[overlapIndex];
                    CreateStablePiece(parent, baseName, pieceIndex++, cursor, overlap.x, bounds, material);
                    cursor = Mathf.Max(cursor, overlap.y);
                }

                CreateStablePiece(parent, baseName, pieceIndex, cursor, bounds.max.x, bounds, material);
                collider.gameObject.SetActive(false);
                Destroy(collider.gameObject);
            }
        }

        private static void CreateStablePiece(
            Transform parent,
            string baseName,
            int pieceIndex,
            float start,
            float end,
            Bounds sourceBounds,
            Material material)
        {
            float length = end - start;
            if (length <= 0.06f)
                return;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = $"{baseName} Gap-Safe {pieceIndex + 1}";
            piece.transform.SetParent(parent);
            piece.transform.position = new Vector3(
                (start + end) * 0.5f,
                sourceBounds.center.y,
                sourceBounds.center.z);
            piece.transform.localScale = new Vector3(
                length,
                sourceBounds.size.y,
                sourceBounds.size.z);

            Renderer renderer = piece.GetComponent<Renderer>();
            if (renderer != null && material != null)
                renderer.sharedMaterial = material;
        }

        private void RestoreMissingCollapseSpans()
        {
            for (int i = 0; i < MissingCollapseSpans.Length; i++)
            {
                MissingCollapseSpan spec = MissingCollapseSpans[i];
                float start = Phase02VolcanoEscapeController.MapDesignX(spec.SectionIndex, spec.DesignStart);
                float end = Phase02VolcanoEscapeController.MapDesignX(spec.SectionIndex, spec.DesignEnd);
                float midpoint = (start + end) * 0.5f;

                if (HasCollapseAt(midpoint))
                    continue;

                float topY = controller.SurfaceHeightAt(midpoint);
                CarveStableSpan(start, end);
                CreateCollapsePlatform(spec.SectionIndex, start, end, topY);
            }
        }

        private static bool HasCollapseAt(float worldX)
        {
            Phase02CollapsingPlatform[] existing =
                FindObjectsByType<Phase02CollapsingPlatform>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                Collider collider = existing[i].GetComponent<Collider>();
                if (collider != null
                    && worldX >= collider.bounds.min.x - 0.02f
                    && worldX <= collider.bounds.max.x + 0.02f)
                    return true;
            }
            return false;
        }

        private static void CarveStableSpan(float cutStart, float cutEnd)
        {
            BoxCollider[] colliders = FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
            for (int i = 0; i < colliders.Length; i++)
            {
                BoxCollider collider = colliders[i];
                if (collider == null
                    || !collider.enabled
                    || !collider.gameObject.name.Contains("Stable Platform"))
                    continue;

                Bounds bounds = collider.bounds;
                if (cutEnd <= bounds.min.x + 0.01f || cutStart >= bounds.max.x - 0.01f)
                    continue;

                Renderer sourceRenderer = collider.GetComponent<Renderer>();
                Material material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
                Transform parent = collider.transform.parent;
                string baseName = collider.gameObject.name;
                collider.gameObject.SetActive(false);

                CreateStablePiece(parent, baseName, 0, bounds.min.x, Mathf.Max(bounds.min.x, cutStart), bounds, material);
                CreateStablePiece(parent, baseName, 1, Mathf.Min(bounds.max.x, cutEnd), bounds.max.x, bounds, material);
                Destroy(collider.gameObject);
            }
        }

        private void CreateCollapsePlatform(int sectionIndex, float start, float end, float topY)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = $"S{sectionIndex + 1} Restored Collapsing Ground {start:F0}-{end:F0}";
            Transform routeRoot = GameObject.Find("Foreground One-Way Parkour Route")?.transform;
            if (routeRoot != null)
                platform.transform.SetParent(routeRoot);
            platform.transform.position = new Vector3((start + end) * 0.5f, topY - 0.4f, 0f);
            platform.transform.localScale = new Vector3(end - start, 0.8f, 3.2f);

            Renderer renderer = platform.GetComponent<Renderer>();
            Collider collider = platform.GetComponent<Collider>();
            Phase02CollapsingPlatform hazard = platform.AddComponent<Phase02CollapsingPlatform>();
            hazard.Configure(controller, sectionIndex, renderer, collider);
        }

        private void Update()
        {
            if (!geometryApplied
                || controller == null
                || controller.CompletionRequested
                || controller.Motor == null
                || controller.PlayerX < Phase02VolcanoEscapeController.AuthoredRouteLength - 3f
                || controller.Motor.VerticalVelocity <= 0.25f)
                return;

            float surfaceY = controller.SurfaceHeightAt(controller.PlayerX);
            if (controller.PlayerY >= surfaceY + 0.02f)
                CompletePhaseMethod?.Invoke(controller, null);
        }
    }
}
