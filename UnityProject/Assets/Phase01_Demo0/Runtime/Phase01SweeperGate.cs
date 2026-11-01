using System;
using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// TASK 005 Branch 1 patrol hazard. One solid bar rises and falls across a fixed door position at
    /// the midpoint of an authored platform, on a fixed linear cycle. It patrols *across* the route
    /// rather than along it, so it can never chase, catch or shove the player at a takeoff lip: the
    /// encounter is a readable "wait for the door" decision.
    ///
    /// Contact is resolved by ordinary physics: the player is pushed off the line and the existing
    /// Phase 01 fall / soft-reset flow takes over. No health, damage, checkpoint or failure state is
    /// introduced. The bar is never random, never physics-driven debris, and it is authored to leave a
    /// passage window wide enough to walk through at the accepted 4.5 m/s player speed.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Phase01SweeperGate : MonoBehaviour
    {
        [SerializeField] private int branchIndex;
        [SerializeField] private float doorScalar = 101.3f;
        [SerializeField] private float lowerHeight;
        [SerializeField] private float upperHeight;
        [SerializeField] private float barHeight = 1.5f;
        [SerializeField] private float oneWayDuration = 2f;
        [SerializeField] private float rearmInnerScalar = 95f;
        [SerializeField] private float rearmOuterScalar = 107f;
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01PlayerMotor player;

        /// <summary>0 = vertical door the player waits for; 1 = horizontal bar sweeping along a platform.</summary>
        public const int AxisVertical = 0;
        public const int AxisHorizontalAlongBranch = 1;

        /// <summary>
        /// Derived rather than serialized. This component's serialized type information did not refresh
        /// after the field was added, so the player build wrote the object one field short and the level
        /// could not be read back ("Position out of bounds"). Deriving the axis from a field the build
        /// already writes removes the fragile field entirely: a vertical door carries two distinct
        /// heights, a horizontal sweep leaves the upper height unused.
        /// </summary>
        private int patrolAxis = AxisVertical;

        private Rigidbody body;

        /// <summary>
        /// Anchors are held in the parent's space and resolved as the bar moves, so a bar parented to moving
        /// geometry rides it. Resolving them once into world space pinned a bar to wherever its platform
        /// happened to be at Awake, which is what stopped hazards and moving geometry being composed.
        /// </summary>
        private Transform anchorParent;
        private Vector3 innerLocal;
        private Vector3 outerLocal;
        private float elapsed;
        private int lastLeg;
        private bool rearmArmed = true;
        private bool playerInsideZone;

        public event Action<Phase01SweeperGate> CycleRestarted;

        public int BranchIndex => branchIndex;
        public float DoorScalar => doorScalar;
        public float LowerHeight => lowerHeight;
        public float UpperHeight => upperHeight;
        public float BarHeight => barHeight;
        public float OneWayDuration => oneWayDuration;

        /// <summary>
        /// Distance the bar actually travels in one direction. A vertical door travels between its two
        /// heights; a horizontal sweep travels between its two authored scalars. Deriving this from the
        /// heights alone under-reported the sweep's speed by reporting the outer scalar as a distance, so
        /// "the bar is authored slower than the player" was only ever true by accident.
        /// </summary>
        public float PatrolDistance => patrolAxis == AxisHorizontalAlongBranch
            ? Mathf.Abs(lowerHeight - doorScalar)
            : Mathf.Abs(upperHeight - lowerHeight);

        /// <summary>Authored patrol speed in metres per second.</summary>
        public float TravelSpeed => PatrolDistance / Mathf.Max(0.01f, oneWayDuration);
        public float CycleDuration => oneWayDuration * 2f;

        /// <summary>
        /// Authored ends of the patrol along the branch, independent of where the bar happens to be right
        /// now. For a horizontal sweep these are the two scalars it travels between; for a vertical door
        /// both are the single scalar it gates. Exposed so tooling can prove a patrol stays on authored
        /// ledge instead of overhanging a void.
        /// </summary>
        public float PatrolInnerScalar => doorScalar;

        /// <inheritdoc cref="PatrolInnerScalar"/>
        public float PatrolOuterScalar => patrolAxis == AxisHorizontalAlongBranch ? lowerHeight : doorScalar;

        /// <summary>
        /// The bar's along-lane footprint and its vertical extent, so the lane can treat the bar as a wall
        /// while it covers the player's body. Contact alone is not enough here: the player's velocity and
        /// position are rewritten every physics step, so a kinematic bar resolves as a shove along the lane
        /// (or a squeeze off a narrow ledge) rather than as something the player has to deal with. The lane
        /// asks for these numbers instead, which makes the block deterministic and lets a jump clear it -
        /// once the player's feet are above <see cref="topHeight"/> the bar no longer covers them.
        /// </summary>
        public void GetBarExtent(
            out float innerScalar,
            out float outerScalar,
            out float bottomHeight,
            out float topHeight)
        {
            Vector3 scale = transform.lossyScale;
            float halfAlong = Mathf.Abs(scale.x) * 0.5f;
            float halfTall = Mathf.Abs(scale.y) * 0.5f;

            // Derived from where the bar actually is, so a bar riding moving geometry blocks the lane it
            // really occupies rather than the lane it was authored over.
            Vector3 world = transform.position;
            float scalar = controller != null
                ? Vector3.Dot(world - controller.RadioPosition, controller.ActiveDirection)
                : CurrentScalar;

            innerScalar = scalar - halfAlong;
            outerScalar = scalar + halfAlong;
            bottomHeight = world.y - halfTall;
            topHeight = world.y + halfTall;
        }

        /// <summary>
        /// True once the bar has risen clear of a walking player (1.6 m of clearance over the platform
        /// top). The window is deliberately long: roughly 1.5 s per 4 s cycle at the authored timings.
        /// </summary>
        public bool IsPassageOpen => CycleProgress >= 0.62f;

        /// <summary>True when the bar is at the far end of its patrol, i.e. clear of the entry side.</summary>
        public bool IsClearOfEntry => CycleProgress >= 0.62f;
        public int Axis => patrolAxis;

        /// <summary>
        /// Authored position along the route: constant for a vertical door, travelling for a horizontal
        /// sweeper. Exact and independent of physics timing.
        /// </summary>
        public float CurrentScalar { get; private set; }
        public float CurrentHeight { get; private set; }
        public float CycleProgress { get; private set; }
        public int ReversalCount { get; private set; }
        public int RestartCount { get; private set; }
        public bool IsRearmArmed => rearmArmed;
        public bool IsPlayerInsideZone => playerInsideZone;

        public void Configure(
            int index,
            float scalar,
            float lower,
            float upper,
            float height,
            float duration,
            float rearmInner,
            float rearmOuter,
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor)
        {
            patrolAxis = AxisVertical;
            branchIndex = index;
            doorScalar = scalar;
            lowerHeight = lower;
            upperHeight = upper;
            barHeight = height;
            oneWayDuration = Mathf.Max(0.1f, duration);
            rearmInnerScalar = rearmInner;
            rearmOuterScalar = rearmOuter;
            controller = demoController;
            player = playerMotor;
            ResolveAnchors();
            ApplyPosition(0f, true);
        }

        /// <summary>
        /// Authors a horizontal sweeper: the bar travels along the branch between two scalars at a fixed
        /// height, so it interacts with the player's route rather than gating it like a door.
        /// </summary>
        public void ConfigureHorizontalSweep(
            int index,
            float inner,
            float outer,
            float height,
            float duration,
            float rearmInner,
            float rearmOuter,
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor)
        {
            patrolAxis = AxisHorizontalAlongBranch;
            branchIndex = index;
            doorScalar = inner;
            lowerHeight = outer;
            barHeight = height;
            oneWayDuration = Mathf.Max(0.1f, duration);
            rearmInnerScalar = rearmInner;
            rearmOuterScalar = rearmOuter;
            controller = demoController;
            player = playerMotor;
            ResolveAnchors();
            ApplyPosition(0f, true);
        }

        /// <summary>Deterministic rearm: the bar always continues from its authored inner end.</summary>
        public void RestartCycle()
        {
            elapsed = 0f;
            lastLeg = 0;
            RestartCount++;
            ApplyPosition(0f, true);
            CycleRestarted?.Invoke(this);
        }

        private void Awake()
        {
            patrolAxis = ResolveAuthoredAxis();
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            ResolveAnchors();
            ApplyPosition(0f, true);
        }

        /// <summary>
        /// Recovers the authored axis from the serialized data the build is known to write.
        /// </summary>
        private int ResolveAuthoredAxis()
        {
            return Mathf.Approximately(upperHeight, 0f) ? AxisHorizontalAlongBranch : AxisVertical;
        }

        private void ResolveAnchors()
        {
            anchorParent = transform.parent;
            if (anchorParent == null)
                return;

            float height = transform.localPosition.y;
            if (patrolAxis == AxisHorizontalAlongBranch)
            {
                innerLocal = new Vector3(doorScalar, height, 0f);
                outerLocal = new Vector3(lowerHeight, height, 0f);
                return;
            }

            innerLocal = new Vector3(doorScalar, lowerHeight, 0f);
            outerLocal = new Vector3(doorScalar, upperHeight, 0f);
        }

        private void FixedUpdate()
        {
            UpdateRearm();

            elapsed += Time.fixedDeltaTime;
            float phase = Mathf.Repeat(elapsed / Mathf.Max(0.01f, oneWayDuration), 2f);
            int leg = phase < 1f ? 0 : 1;
            if (leg != lastLeg)
            {
                lastLeg = leg;
                ReversalCount++;
            }

            ApplyPosition(leg == 0 ? phase : 2f - phase, false);
        }

        private void UpdateRearm()
        {
            if (controller == null || player == null)
                return;

            bool inside = controller.ActiveBranchIndex == branchIndex
                && player.CurrentScalar >= rearmInnerScalar
                && player.CurrentScalar <= rearmOuterScalar;
            if (inside == playerInsideZone)
                return;

            playerInsideZone = inside;
            if (inside && rearmArmed)
            {
                rearmArmed = false;
                RestartCycle();
            }
            else if (!inside)
            {
                rearmArmed = true;
            }
        }

        private void ApplyPosition(float t, bool immediate)
        {
            CycleProgress = Mathf.Clamp01(t);
            if (patrolAxis == AxisHorizontalAlongBranch)
            {
                CurrentHeight = innerLocal.y;
                CurrentScalar = Mathf.Lerp(doorScalar, lowerHeight, CycleProgress);
            }
            else
            {
                CurrentHeight = Mathf.Lerp(lowerHeight, upperHeight, CycleProgress);
                CurrentScalar = doorScalar;
            }
            // Written in the parent's space. Driving the kinematic body with MovePosition added its motion
            // on top of the parent's own, so a bar parented to a ferry slid out of the platform it was meant
            // to be riding (measured: 2.34 m of drift across a 1.98 m crossing).
            transform.localPosition = Vector3.Lerp(innerLocal, outerLocal, CycleProgress);
        }
    }
}
