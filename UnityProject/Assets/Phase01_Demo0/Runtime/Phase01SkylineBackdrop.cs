using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// The painted distance: a single panel parented to the camera, held at a fixed depth in front of it and
    /// scaled to cover the frame with a margin.
    ///
    /// It has to be a painted panel rather than a skybox or a field of geometry, and the reason is the
    /// camera. The gameplay view is orthographic at size 4.2 and pitched 5 degrees, so it sees about 8.4 m of
    /// the world: a skybox shows one flat colour across that window (there is no room for a gradient), and
    /// distance does not shrink geometry for an orthographic camera, so a "distant" spire has to be sized
    /// against the 8.4 m frame - which is how an earlier pass ended up with 46 m slabs standing across the
    /// level. Painting the distance makes the composition explicit and scale-free: the haze gradient, the
    /// layered ruined silhouettes and their values are authored exactly as the player sees them, while the
    /// level's own geometry still occludes the panel correctly, because the panel is real geometry behind it.
    ///
    /// The panel is a child of the camera, so its plane is the image plane: the composition holds through
    /// the branch turns and the size changes, and it can never be reached, walked behind, or collided with.
    /// </summary>
    public sealed class Phase01SkylineBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private float distance = 32f;
        [SerializeField] private float margin = 1.35f;

        public float Distance => distance;
        public float Margin => margin;

        public void Configure(Camera camera, float backdropDistance, float backdropMargin)
        {
            viewCamera = camera;
            distance = backdropDistance;
            margin = backdropMargin;
            Apply();
        }

        public void Apply()
        {
            if (viewCamera == null)
                return;

            // A perfect billboard in camera space: the panel's plane is the image plane, so a texel of the
            // painted texture is a fixed fraction of the frame at every camera size the demo uses.
            Vector3 local = transform.localPosition;
            local.x = 0f;
            local.y = 0f;
            local.z = distance;
            transform.localPosition = local;
            transform.localRotation = Quaternion.identity;
            float halfHeight = viewCamera.orthographicSize * margin;
            float halfWidth = halfHeight * viewCamera.aspect;
            transform.localScale = new Vector3(halfWidth * 2f, halfHeight * 2f, 0.5f);
        }

        private void LateUpdate() => Apply();
    }
}
