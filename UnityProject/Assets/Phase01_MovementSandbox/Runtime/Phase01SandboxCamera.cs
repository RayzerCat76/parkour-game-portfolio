using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxCamera : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private float horizontalDamping = 0.18f;
        [SerializeField] private float verticalDamping = 0.28f;
        [SerializeField] private float lookAheadDistance = 1.4f;
        [SerializeField] private float jumpVerticalFraming = 2.7f;
        [SerializeField] private float resetSettleDuration = 0.35f;

        private Vector3 velocity;
        private float lastHorizontalSign = 1f;
        private bool resetSettling;
        private Vector3 resetStart;
        private Vector3 resetTarget;
        private float resetElapsed;

        public float HorizontalDamping => horizontalDamping;
        public float VerticalDamping => verticalDamping;
        public float LookAheadDistance => lookAheadDistance;
        public float JumpVerticalFraming => jumpVerticalFraming;
        public float ResetSettleDuration => resetSettleDuration;
        public bool IsResetSettling => resetSettling;

        public void Configure(Transform target) => player = target;

        private void Start()
        {
            if (player != null)
                transform.position = DesiredPosition(player.position, 0f);
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            if (resetSettling)
            {
                resetElapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(resetElapsed / resetSettleDuration));
                transform.position = Vector3.LerpUnclamped(resetStart, resetTarget, t);
                if (t >= 1f)
                {
                    resetSettling = false;
                    velocity = Vector3.zero;
                }
                return;
            }

            Phase01ParkourMotor motor = player.GetComponent<Phase01ParkourMotor>();
            float horizontalVelocity = motor != null ? motor.HorizontalVelocity : 0f;
            if (Mathf.Abs(horizontalVelocity) > 0.15f)
                lastHorizontalSign = Mathf.Sign(horizontalVelocity);
            Vector3 target = DesiredPosition(player.position, lastHorizontalSign * lookAheadDistance);
            float x = Mathf.SmoothDamp(transform.position.x, target.x, ref velocity.x, horizontalDamping);
            float y = Mathf.SmoothDamp(transform.position.y, target.y, ref velocity.y, verticalDamping);
            transform.position = new Vector3(x, y, transform.position.z);
        }

        public void BeginResetSettle(Vector3 playerPosition)
        {
            resetStart = transform.position;
            resetTarget = DesiredPosition(playerPosition, 0f);
            resetElapsed = 0f;
            resetSettling = true;
            velocity = Vector3.zero;
        }

        private Vector3 DesiredPosition(Vector3 playerPosition, float lookAhead)
        {
            return new Vector3(playerPosition.x + lookAhead, playerPosition.y + jumpVerticalFraming, -14f);
        }
    }
}
