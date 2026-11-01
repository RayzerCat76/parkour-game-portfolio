using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// An authored air current: the ride across a void that is too wide to jump.
    ///
    /// This is the Phase 01 answer to "the parkour is just jumps". A draught is not a longer jump and it is
    /// not a platform: it is a current of air the player commits to. While the player holds jump inside the
    /// current it carries them along the lane at an authored speed and holds their feet on an authored ride
    /// line, so a chasm is crossed by reading the current and holding on. Let go and the current stops
    /// carrying them and the void is the void. Nothing about it touches the jump: the movement module counts
    /// the ride separately from jumps, so a crossing is proven to be a ride and not a jump in disguise.
    ///
    /// It is deliberately symmetric. The 1-D branch lane is walked out and back, so a current has to work in
    /// both directions: the carry direction is taken from which half of the current the player is in, aim
    /// the ride line at the higher of the two lips and the return trip is carried out as fairly as the
    /// outbound one. A vertical lift on its own would not survive the return trip, which is why the authored
    /// elevation change on this branch comes from the profile around the current, not from the current
    /// throwing the player upward.
    ///
    /// Currents can pulse (an on/off cycle) so the beat can become "read the gust, then commit" without the
    /// legs being re-authored - that is the knob the later branches escalate with.
    /// </summary>
    [DefaultExecutionOrder(-140)]
    public sealed class Phase01Draught : MonoBehaviour
    {
        [SerializeField] private int branchIndex;
        [SerializeField] private float innerScalar;
        [SerializeField] private float outerScalar;
        [SerializeField] private float rideHeight;
        [SerializeField] private float bandFloor;
        [SerializeField] private float bandCeiling;
        [SerializeField] private float alongSpeed = 4.6f;
        [SerializeField] private float pulseOnSeconds;
        [SerializeField] private float pulseOffSeconds;
        [SerializeField] private float pulsePhase;
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01PlayerMotor player;
        [SerializeField] private Transform visuals;

        /// <summary>How far off the lane a player may be and still be inside the current.</summary>
        private const float LaneTolerance = 0.6f;

        public int BranchIndex => branchIndex;
        public float InnerScalar => innerScalar;
        public float OuterScalar => outerScalar;
        public float RideHeight => rideHeight;
        public float AlongSpeed => alongSpeed;
        public float CentreScalar => (innerScalar + outerScalar) * 0.5f;
        public float Width => outerScalar - innerScalar;

        /// <summary>True while the current is blowing. A steady current is always blowing.</summary>
        public bool IsBlowing => pulseOnSeconds <= 0f
            || pulseOffSeconds <= 0f
            || CyclePosition() < pulseOnSeconds;

        /// <summary>True while the current is holding the player right now.</summary>
        public bool IsCarrying { get; private set; }

        /// <summary>Clockwise from the start of the current's blow/dry cycle, in seconds.</summary>
        public float CyclePosition()
        {
            float period = pulseOnSeconds + pulseOffSeconds;
            if (period <= 0.0001f)
                return 0f;
            float phase = (Time.time + pulsePhase) % period;
            return phase < 0f ? phase + period : phase;
        }

        /// <summary>Seconds until the current next starts blowing, or 0 when it already is.</summary>
        public float SecondsUntilBlowing()
        {
            if (IsBlowing)
                return 0f;
            return Mathf.Max(0f, (pulseOnSeconds + pulseOffSeconds) - CyclePosition());
        }

        public int CarryStartCount { get; private set; }
        public int CrossingCount { get; private set; }
        public float CarriedSeconds { get; private set; }

        public void Configure(
            int index,
            float inner,
            float outer,
            float ride,
            float floor,
            float ceiling,
            float speed,
            float onSeconds,
            float offSeconds,
            float phase,
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor,
            Transform currentVisuals)
        {
            branchIndex = index;
            innerScalar = inner;
            outerScalar = outer;
            rideHeight = ride;
            bandFloor = floor;
            bandCeiling = ceiling;
            alongSpeed = speed;
            pulseOnSeconds = onSeconds;
            pulseOffSeconds = offSeconds;
            pulsePhase = phase;
            controller = demoController;
            player = playerMotor;
            visuals = currentVisuals;
        }

        /// <summary>
        /// "Close along the route, on the route, and inside the band". Geometry, not a trigger collider, for
        /// the same reason the launcher uses geometry: the lane clamp makes a collider-based volume either
        /// never fire or fire from off the lane.
        /// </summary>
        public bool Contains(float scalar, float feetHeight)
        {
            if (scalar < innerScalar || scalar > outerScalar)
                return false;
            if (feetHeight < bandFloor || feetHeight > bandCeiling)
                return false;
            return true;
        }

        private void FixedUpdate()
        {
            IsCarrying = false;
            if (controller == null || player == null)
                return;
            if (controller.ActiveBranchIndex != branchIndex)
                return;

            Vector3 offset = player.transform.position - transform.position;
            float lateral = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, controller.ActiveDirection)));
            if (lateral > LaneTolerance)
                return;

            float scalar = Vector3.Dot(
                player.transform.position - controller.RadioPosition,
                controller.ActiveDirection);
            if (!Contains(scalar, player.transform.position.y))
                return;

            if (!IsBlowing)
                return;

            // A current only carries a player who holds on. That is the whole beat: read it, commit to it,
            // and do not let go until the far lip.
            if (!player.IsJumpHeld)
                return;

            // Carry the player through the current, in the direction they are already travelling. Taking the
            // direction from their motion is what makes one authored current work in both directions on a
            // branch that is walked out and back: a rule based on which side of the middle they are on would
            // push a player entering at the inner lip straight back out of it again. The fallback - carry a
            // stationary player towards the far lip - only applies to a player who stepped in without moving.
            float direction = Mathf.Abs(player.AlongLaneVelocity) > 0.2f
                ? Mathf.Sign(player.AlongLaneVelocity)
                : (scalar < CentreScalar ? 1f : -1f);

            float exitEdge = direction > 0f ? outerScalar : innerScalar;
            if (!IsCarrying)
            {
                CarryStartCount++;
                IsCarrying = true;
                crossingRecorded = false;
            }

            CarriedSeconds += Time.fixedDeltaTime;
            player.ApplyDraught(direction * alongSpeed, rideHeight);

            // A crossing is recorded on approach to the far lip rather than on arrival, so a player who is
            // carried to the lip and then walks on still counts as having crossed.
            if (!crossingRecorded && Mathf.Abs(scalar - exitEdge) <= 1.2f)
            {
                crossingRecorded = true;
                CrossingCount++;
            }
        }

        private bool crossingRecorded;
        private void LateUpdate()
        {
            if (visuals == null)
                return;
            bool visible = IsBlowing && controller != null && controller.IsBranchPresentationVisible(branchIndex);
            if (visuals.gameObject.activeSelf != visible)
                visuals.gameObject.SetActive(visible);
        }
    }
}
