using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// Slow drifting motes around the camera - the flecks of dust and pale light that give a dark, hazy
    /// cavern a sense of air and distance. They are authored up front, carry no colliders and no rigidbodies,
    /// and drift on a fixed function of their index, so the atmosphere costs almost nothing and never
    /// becomes a simulation.
    /// </summary>
    public sealed class Phase01AtmosphereMotes : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform[] motes;
        [SerializeField] private Vector3 fieldExtents = new Vector3(34f, 13f, 18f);
        [SerializeField] private float followLerp = 1.6f;
        [SerializeField] private float driftSpeed = 0.14f;

        private Vector3[] anchors;
        private float elapsed;

        public void Configure(Camera camera, Transform[] moteTransforms, Vector3 extents)
        {
            viewCamera = camera;
            motes = moteTransforms;
            fieldExtents = extents;
            anchors = new Vector3[motes != null ? motes.Length : 0];
            for (int i = 0; i < anchors.Length; i++)
                anchors[i] = motes[i] != null ? motes[i].localPosition : Vector3.zero;
        }

        public void ApplyProgress(float time)
        {
            if (motes == null)
                return;

            for (int i = 0; i < motes.Length; i++)
            {
                Transform mote = motes[i];
                if (mote == null)
                    continue;

                Vector3 anchor = anchors != null && i < anchors.Length ? anchors[i] : Vector3.zero;
                // Deterministic per-mote phases: no random, so the atmosphere looks identical every run.
                float phase = i * 1.7f;
                Vector3 drift = new Vector3(
                    Mathf.Sin(time * driftSpeed + phase) * 1.9f,
                    Mathf.Sin(time * driftSpeed * 0.73f + phase * 1.3f) * 1.1f,
                    Mathf.Sin(time * driftSpeed * 0.41f + phase * 2.1f) * 1.2f);
                mote.localPosition = anchor + drift;
            }
        }

        private void LateUpdate()
        {
            if (viewCamera == null)
                return;

            elapsed += Time.deltaTime;
            ApplyProgress(elapsed);

            // The field follows the camera with a lag, so a long run through a branch keeps air around the
            // player without the motes reading as pinned to the view.
            Vector3 target = viewCamera.transform.position + Vector3.up * 2f;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            // ...and turns with it. The field is authored on the far side of the play plane, so it has to
            // be turned by the same yaw the camera has: the branches are seen from four different directions,
            // and an unrotated field put the motes on the walkable side of the plane on whichever branches
            // happened to face along Z - dust drifting straight through the player and the ledges, which
            // reads as geometry clipping rather than as air.
            transform.rotation = Quaternion.Euler(0f, viewCamera.transform.eulerAngles.y, 0f);
        }
    }
}
