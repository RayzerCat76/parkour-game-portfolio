using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    /// <summary>
    /// Applies renderer-safe, high-contrast presentation after the runtime
    /// greybox is generated. The project renderer can display default runtime
    /// primitive materials as nearly white, so Phase 02 uses explicit unlit
    /// materials and the camera clear color instead of a full-screen sky cube.
    /// Temporary per-object materials created during greybox construction are
    /// released immediately after the shared unlit materials are assigned.
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public sealed class Phase02VisibilityPass : MonoBehaviour
    {
        private static readonly Dictionary<string, Material> Materials =
            new Dictionary<string, Material>();

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

            if (FindAnyObjectByType<Phase02VisibilityPass>() != null)
                return;

            new GameObject("Phase02 Visibility Pass")
                .AddComponent<Phase02VisibilityPass>();
        }

        private IEnumerator Start()
        {
            yield return null;
            ApplyPresentation();
        }

        private static void ApplyPresentation()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.008f, 0.014f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 250f;
                camera.cullingMask = ~0;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.12f, 0.13f);

            int releasedMaterials = 0;
            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                string objectName = renderer.gameObject.name;
                if (objectName == "Dark Red Sky")
                {
                    renderer.enabled = false;
                    continue;
                }

                Color color = ResolveColor(objectName);
                Material replacement = MaterialFor(color);
                Material previous = renderer.sharedMaterial;
                renderer.sharedMaterial = replacement;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                if (previous != null
                    && previous != replacement
                    && previous.name.StartsWith("Phase02 Runtime Tint"))
                {
                    Object.Destroy(previous);
                    releasedMaterials++;
                }
            }

            Debug.Log(
                $"[Phase02] Visibility pass applied with shared unlit materials; " +
                $"released {releasedMaterials} temporary material instances.");
        }

        private static Color ResolveColor(string objectName)
        {
            if (objectName.Contains("Player Visual"))
                return new Color(0.35f, 0.9f, 1f, 1f);

            if (objectName.Contains("White Opening Glow"))
                return new Color(1f, 1f, 1f, 1f);
            if (objectName.Contains("White Opening Halo") || objectName.Contains("White Opening Beacon"))
                return new Color(0.7f, 0.82f, 1f, 1f);

            if (objectName.Contains("Volcano Left Slope") || objectName.Contains("Volcano Right Slope"))
                return new Color(0.07f, 0.055f, 0.065f, 1f);
            if (objectName.Contains("Volcano Crater Glow"))
                return new Color(1f, 0.14f, 0.025f, 1f);
            if (objectName.Contains("Distant Ridge"))
                return new Color(0.025f, 0.02f, 0.028f, 1f);

            if (objectName.Contains("Warning Marker"))
                return new Color(1f, 0.18f, 0.02f, 1f);
            if (objectName.Contains("Debris Visual"))
                return new Color(0.12f, 0.09f, 0.1f, 1f);
            if (objectName.Contains("Shockwave Visual"))
                return new Color(1f, 0.025f, 0.01f, 1f);
            if (objectName.Contains("Collapse Front Visual"))
                return new Color(0.62f, 0.015f, 0.01f, 1f);

            if (objectName.Contains("Collapsing Ground"))
                return new Color(0.72f, 0.16f, 0.08f, 1f);

            if (objectName.StartsWith("S1 "))
                return new Color(0.28f, 0.24f, 0.26f, 1f);
            if (objectName.StartsWith("S2 "))
                return new Color(0.38f, 0.18f, 0.2f, 1f);
            if (objectName.StartsWith("S3 "))
                return new Color(0.48f, 0.13f, 0.14f, 1f);
            if (objectName.StartsWith("S4 "))
                return new Color(0.58f, 0.09f, 0.085f, 1f);
            if (objectName.StartsWith("S5 "))
                return new Color(0.58f, 0.6f, 0.66f, 1f);

            return new Color(0.22f, 0.2f, 0.22f, 1f);
        }

        private static Material MaterialFor(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            if (Materials.TryGetValue(key, out Material existing) && existing != null)
                return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader)
            {
                name = $"Phase02 Unlit {key}",
                hideFlags = HideFlags.DontSave,
                renderQueue = 2000
            };

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            material.color = color;

            Materials[key] = material;
            return material;
        }
    }
}
