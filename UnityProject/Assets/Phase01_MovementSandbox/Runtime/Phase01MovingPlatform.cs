using UnityEngine;

namespace Phase01.MovementSandbox
{
    [DefaultExecutionOrder(-100)]
    public sealed class Phase01MovingPlatform : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new Vector3(4f, 0f, 0f);
        [SerializeField] private float oneWayDuration = 3f;

        private Rigidbody body;
        private Vector3 origin;
        private float elapsed;

        public Vector3 FrameDelta { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float RouteProgress { get; private set; }
        public int ReversalCount { get; private set; }

        public void Configure(Vector3 offset, float duration)
        {
            localOffset = offset;
            oneWayDuration = duration;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            origin = transform.position;
        }

        private void FixedUpdate()
        {
            float previousPhase = elapsed / Mathf.Max(0.01f, oneWayDuration);
            elapsed += Time.fixedDeltaTime;
            float phase = elapsed / Mathf.Max(0.01f, oneWayDuration);
            int previousLeg = Mathf.FloorToInt(previousPhase);
            int currentLeg = Mathf.FloorToInt(phase);
            if (currentLeg != previousLeg)
                ReversalCount++;

            float pingPong = Mathf.PingPong(phase, 1f);
            RouteProgress = Mathf.SmoothStep(0f, 1f, pingPong);
            Vector3 target = origin + localOffset * RouteProgress;
            FrameDelta = target - body.position;
            Velocity = FrameDelta / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            body.MovePosition(target);
        }
    }
}
