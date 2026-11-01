using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Phase01.Demo0
{
    /// <summary>
    /// Opts the gameplay camera into URP post-processing at runtime.
    ///
    /// The Phase 1 design sheet asks for glowing blocks ("发光黄色小方块") and the scene carries a volume with
    /// bloom above white so only those props glow. The camera side of that opt-in cannot be authored in the
    /// scene: URP's camera-data component does not survive a batch-mode scene save because the render
    /// pipeline is not live then. Setting it from a runtime component always works.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class Phase01PostFxCamera : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;

        public void Configure(Camera camera)
        {
            viewCamera = camera;
        }

        private void Awake()
        {
            Camera camera = viewCamera != null ? viewCamera : GetComponent<Camera>();
            if (camera == null)
                return;

            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }
    }
}
