using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// A band of spikes across the lane: Hollow Knight's signature difficulty. Unlike a gate it cannot be
    /// waited out and unlike a bar it cannot be followed - it has to be cleared, which forces the player to
    /// commit to a landing instead of shuffling along a ledge. Contact uses the same soft reset the fall flow
    /// already uses, so no health, damage or new failure state is introduced.
    ///
    /// The band spans the full lane on purpose: the movement module projects the player onto the lane every
    /// physics step, so lateral dodging is not a skill this game can ask for.
    /// </summary>
    public sealed class Phase01SpikeStrip : MonoBehaviour
    {
        [SerializeField] private int branchIndex;
        [SerializeField] private float innerScalar;
        [SerializeField] private float outerScalar;
        [SerializeField] private float spikeHeight = 0.44f;
        [SerializeField] private float clearance = 0.18f;
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01PlayerMotor player;

        private float lastContactTime = -10f;

        public float InnerScalar => Mathf.Min(innerScalar, outerScalar);
        public float OuterScalar => Mathf.Max(innerScalar, outerScalar);
        public float Span => Mathf.Abs(outerScalar - innerScalar);
        public float SpikeHeight => spikeHeight;
        public int BranchIndex => branchIndex;
        public int ContactCount { get; private set; }

        /// <summary>Grace window after a hit, so a reset that lands inside the band cannot fire forever.</summary>
        private const float ContactCooldown = 0.4f;

        public void Configure(
            int index,
            float inner,
            float outer,
            float height,
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor)
        {
            branchIndex = index;
            innerScalar = inner;
            outerScalar = outer;
            spikeHeight = height;
            controller = demoController;
            player = playerMotor;
        }

        /// <summary>True when the given feet height and lane scalar stand in the spikes.</summary>
        public bool WouldStrike(float scalar, float feetHeight)
        {
            if (scalar < InnerScalar || scalar > OuterScalar)
                return false;
            if (controller == null)
                return false;
            float surface = controller.ActiveSurfaceHeight(scalar);
            return feetHeight <= surface + spikeHeight + clearance;
        }

        private void FixedUpdate()
        {
            if (controller == null || player == null)
                return;
            if (controller.ActiveBranchIndex != branchIndex)
                return;

            float scalar = player.CurrentScalar;
            // The player object's origin IS the feet: the capsule collider is offset above it (see
            // Phase01PlayerMotor.groundOffset, applied as the collider centre). Subtracting that offset here
            // misreported the feet by 0.72 m, which is what made the first integrated band strike late.
            float feet = player.transform.position.y;
            if (!WouldStrike(scalar, feet))
                return;
            if (Time.time - lastContactTime < ContactCooldown)
                return;

            lastContactTime = Time.time;
            ContactCount++;
            player.ResetToCheckpoint();
        }
    }
}
