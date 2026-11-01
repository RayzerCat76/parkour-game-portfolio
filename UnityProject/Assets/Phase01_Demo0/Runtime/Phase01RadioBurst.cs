using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// The Phase 1 design sheet ends the loop with "收音机 explode!": once the radio has felt all four
    /// glowing blocks it bursts. This is the presentation of that beat - the shards are authored up front
    /// and simply animated, so the burst allocates nothing and can be posed exactly for capture.
    /// </summary>
    public sealed class Phase01RadioBurst : MonoBehaviour
    {
        [SerializeField] private Transform burstOrigin;
        [SerializeField] private Transform[] shards;

        /// <summary>
        /// Captured when the burst is authored. The shards are parked at the origin once the burst is set up,
        /// so their own local positions can no longer describe the direction they should fly; these are
        /// serialized so the packed scene keeps the ring.
        /// </summary>
        [SerializeField] private Vector3[] directions;
        [SerializeField] private float duration = 1.1f;
        [SerializeField] private float distance = 3.6f;

        private float elapsed = -1f;

        public bool IsPlaying => elapsed >= 0f;
        public float Duration => duration;

        public void Configure(Transform origin, Transform[] burstShards)
        {
            burstOrigin = origin;
            shards = burstShards;
            directions = new Vector3[burstShards != null ? burstShards.Length : 0];
            for (int i = 0; i < directions.Length; i++)
            {
                Transform shard = burstShards[i];
                Vector3 authored = shard != null ? shard.localPosition : Vector3.up;
                directions[i] = authored.sqrMagnitude > 1e-6f ? authored.normalized : Vector3.up;
            }
            ApplyProgress(0f, false);
        }

        /// <summary>Starts the burst from the beginning.</summary>
        public void Play()
        {
            elapsed = 0f;
            ApplyProgress(0f, true);
        }

        /// <summary>
        /// Poses the burst at a point in its life. Exposed so a capture can hold a mid-burst frame: edit mode
        /// has no game loop to advance the animation.
        /// </summary>
        public void ApplyProgress(float progress, bool visible = true)
        {
            if (shards == null)
                return;

            progress = Mathf.Clamp01(progress);
            float eased = 1f - Mathf.Pow(1f - progress, 2.2f);
            Vector3 origin = burstOrigin != null ? burstOrigin.position : transform.position;
            for (int i = 0; i < shards.Length; i++)
            {
                Transform shard = shards[i];
                if (shard == null)
                    continue;

                Vector3 direction = directions != null && i < directions.Length
                    ? directions[i]
                    : Vector3.up;
                shard.gameObject.SetActive(visible);
                shard.position = origin + direction * (distance * eased) + Vector3.up * (0.5f * eased);
                float scale = Mathf.Lerp(1f, 0.15f, progress);
                shard.localScale = Vector3.one * scale;
            }
        }

        private void Update()
        {
            if (elapsed < 0f)
                return;

            elapsed += Time.deltaTime;
            float progress = elapsed / Mathf.Max(0.05f, duration);
            ApplyProgress(progress);
            if (progress >= 1f)
            {
                elapsed = -1f;
                ApplyProgress(1f, false);
            }
        }
    }
}
