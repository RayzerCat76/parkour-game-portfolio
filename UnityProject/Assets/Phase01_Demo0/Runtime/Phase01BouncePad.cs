using UnityEngine;

namespace Phase01.Demo0
{
    /// <summary>
    /// TASK 006 interaction: a pad that launches the player upward when they land on it.
    ///
    /// This is the difficulty style Branch 1 now uses - the player interacts with an authored component
    /// instead of being asked to clear a longer gap. The launch is a fixed authored speed (no random, no
    /// physics impulse tuning), it fires once per landing so it cannot loop in mid-air, and its apex is
    /// deliberately above the jump apex so the pad is the route across its crossing.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class Phase01BouncePad : MonoBehaviour
    {
        [SerializeField] private int branchIndex;
        [SerializeField] private float launchSpeed = 12f;
        [SerializeField] private float footprintHalfWidth = 1.2f;
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01PlayerMotor player;

        /// <summary>How far off the lane a player may be and still count as being on the pad.</summary>
        private const float LaneTolerance = 0.6f;

        public float LaunchSpeed => launchSpeed;

        /// <summary>Apex of the authored launch: 12 m/s reaches about 3.24 m.</summary>
        public float LaunchApex => launchSpeed * launchSpeed / (2f * 22.222f);
        public int LaunchCount { get; private set; }

        /// <summary>Guards against re-firing while a launch is already in flight.</summary>
        private int lastSeenLaunchCount;
        public int BranchIndex => branchIndex;

        public void Configure(
            int index,
            float speed,
            float halfWidth,
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor)
        {
            branchIndex = index;
            launchSpeed = speed;
            footprintHalfWidth = halfWidth;
            controller = demoController;
            player = playerMotor;
        }

        private void FixedUpdate()
        {
            if (controller == null || player == null)
                return;
            if (controller.ActiveBranchIndex != branchIndex || !player.IsGrounded)
                return;

            // Trigger is authored geometry, not a collider: "close along the route, on the route". The old
            // test compared the cross product's magnitude against a threshold, which is 1 for every
            // horizontal direction, so the pad returned early every frame and could never launch anyone.
            Vector3 offset = player.transform.position - transform.position;
            float along = Vector3.Dot(offset, controller.ActiveDirection);
            if (Mathf.Abs(along) > footprintHalfWidth)
                return;
            float lateral = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, controller.ActiveDirection)));
            if (lateral > LaneTolerance)
                return;

            // A launch has to go through the movement module: writing the rigidbody velocity here left the
            // module's internal state untouched, so its ground snap pulled the launch straight back down.
            if (player.ExternalLaunchCount != lastSeenLaunchCount)
                return;

            player.LaunchVertically(launchSpeed);
            lastSeenLaunchCount = player.ExternalLaunchCount;
            LaunchCount++;
        }
    }
}
