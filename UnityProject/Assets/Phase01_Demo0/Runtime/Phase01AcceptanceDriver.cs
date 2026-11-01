using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Phase01.MovementSandbox;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01AcceptanceDriver : MonoBehaviour
    {
        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01PlayerMotor player;
        [SerializeField] private Phase01PerformanceMonitor performanceMonitor;

        private readonly List<string> failures = new List<string>();
        private readonly HashSet<string> evaluatedChecks = new HashSet<string>();

        public bool Finished { get; private set; }
        public bool Passed => Finished && failures.Count == 0;
        public int CheckCount => evaluatedChecks.Count;
        public string Report => failures.Count == 0
            ? $"All {CheckCount} acceptance checks passed."
            : string.Join(" | ", failures);

        public void Configure(
            Phase01DemoController demoController,
            Phase01PlayerMotor playerMotor,
            Phase01PerformanceMonitor monitor)
        {
            controller = demoController;
            player = playerMotor;
            performanceMonitor = monitor;
        }

        private void Start()
        {
            if (Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-phase01PerformanceTest"))
                StartCoroutine(RunPerformanceAndQuit());
            else if (Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-phase01SelfTest"))
                StartCoroutine(RunAndQuit());
        }

        public IEnumerator Run()
        {
            Finished = false;
            failures.Clear();
            evaluatedChecks.Clear();
            yield return null;

            Check(controller != null && controller.HasValidReferences, "13: serialized references are complete");
            Check(controller.ActiveBranchIndex == 0, "2: Branch 1 is initially active");
            Check(player.CurrentScalar >= controller.RadioStopDistance
                && player.CurrentScalar <= 2.5f, "2: Branch 1 starts beside the receiver");
            CheckPlane("1: player starts on active lane plane");

            for (int branch = 0; branch < 4; branch++)
            {
                Check(controller.ActiveBranchIndex == branch, $"4: Branch {branch + 1} becomes active once");
                if (branch > 0)
                    Check(Mathf.Abs(player.CurrentScalar - controller.RadioStopDistance) < 0.03f, $"4: Branch {branch + 1} starts at radio");

                if (branch == 0)
                    yield return MoveBranch1OutwardToPickup();
                else if (branch == 1)
                    yield return MoveBranch2OutwardToPickup();
                else if (branch == 2)
                    yield return MoveBranch3OutwardToPickup();
                else if (branch == 3)
                    yield return MoveBranch4OutwardToPickup();
                Phase01Particle carried = controller.CarriedParticle;
                Check(carried != null && carried.BranchIndex == branch, $"6: Branch {branch + 1} particle collected exactly once");

                if (carried != null)
                {
                    yield return null;
                    Check(carried.State == Phase01Particle.ParticleState.Carried, $"7: Branch {branch + 1} particle remains carried");
                    Check(Vector3.Distance(carried.transform.position, player.transform.position) < 2f, $"7: Branch {branch + 1} particle visibly follows player");
                    Phase01Particle other = controller.Particles[(branch + 1) % 4];
                    Check(!controller.TryPickup(other), "5: only one particle can be carried");
                }

                if (branch == 0)
                    yield return MoveBranch1ToRadio();
                else if (branch == 1)
                    yield return ValidateBranch2TransformationAndReturn(carried);
                else if (branch == 2)
                    yield return MoveBranch3ToRadio();
                else if (branch == 3)
                    yield return MoveBranch4ToRadio();
                else
                    yield return MoveUntil(
                        () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                        player.InputForScalarDirection(-1f),
                        7f,
                        $"4: Branch {branch + 1} returns to radio");

                yield return WaitUntil(
                    () => controller.CameraRig.IsStableRadioLock,
                    2f,
                    $"8: Branch {branch + 1} radio lock stabilizes before deposit");

                float yawBefore = controller.CameraYaw;
                int turnsBefore = controller.CameraQuarterTurns;
                Vector3 transferStart = carried != null ? carried.transform.position : Vector3.zero;
                float transferDistance = carried != null
                    ? Vector3.Distance(transferStart, controller.RadioPosition)
                    : 0f;

                player.SetSimulatedAxis(0f);
                player.SimulateInteract();
                yield return null;
                Check(controller.IsInputLocked, "10: input locks immediately on deposit");
                Check(controller.State == Phase01DemoController.DemoState.Depositing, "8: manual deposit begins visible transfer");

                yield return WaitUntil(
                    () => carried == null || carried.State == Phase01Particle.ParticleState.Transferring,
                    1.5f,
                    "8: deposit focus advances into visible particle transfer");
                yield return WaitUntil(
                    () => carried == null
                        || Vector3.Distance(carried.transform.position, controller.RadioPosition) < transferDistance,
                    0.3f,
                    "8: transfer moves toward radio");
                if (carried != null)
                {
                    Check(carried.State == Phase01Particle.ParticleState.Transferring, "8: particle is visibly transferring into radio");
                    Check(Vector3.Distance(carried.transform.position, controller.RadioPosition) < transferDistance, "8: transfer moves toward radio");
                }

                yield return WaitUntil(
                    () => controller.State == Phase01DemoController.DemoState.Rotating,
                    2f,
                    "9: rotation begins after deposit");
                Check(controller.IsInputLocked, "10: input stays locked during rotation");
                player.SetSimulatedAxis(-1f);

                float rotationDeadline = Time.realtimeSinceStartup + 2.5f;
                while (controller.State == Phase01DemoController.DemoState.Rotating
                    && Time.realtimeSinceStartup < rotationDeadline)
                {
                    Vector3 offset = player.transform.position - controller.RadioPosition;
                    offset.y = 0f;
                    Check(Mathf.Abs(offset.magnitude - controller.RadioStopDistance) < 0.03f, "3: player remains beside radio during rotation");
                    yield return null;
                }

                Check(controller.State != Phase01DemoController.DemoState.Rotating, "13: rotation cannot soft-lock");
                Check(Mathf.Abs(Mathf.Abs(controller.CameraYaw - yawBefore) - 90f) < 0.001f, "9: successful deposit rotates POV exactly 90 degrees");
                Check(controller.CameraQuarterTurns == turnsBefore + 1, "9: exactly one rotation occurs per deposit");
                Check(controller.ReceiverCharge == branch + 1, "11: receiver charge increments once");
                Check(controller.SuccessfulDepositCount == branch + 1, "13: no duplicate deposit transition");
                Check(carried != null && carried.State == Phase01Particle.ParticleState.Deposited, "6: deposited particle cannot be collected again");
                if (carried != null)
                    Check(!controller.TryPickup(carried), "6: particle pickup is one-time");

                if (branch < 3)
                {
                    Check(controller.CanAcceptInput, "10: input restores after rotation");
                    CheckPlane("1: player is constrained to the newly active lane");
                }
            }

            player.ClearSimulatedInput();
            Check(controller.DemoComplete, "12: fourth deposit reaches demo-complete state");
            Check(controller.State == Phase01DemoController.DemoState.Complete, "12: completion state is clean and final");
            Check(!controller.IsInputLocked, "10: rotation input lock is released after final rotation");
            Check(!controller.CanAcceptInput, "12: movement is disabled after demo completion");
            Check(controller.CameraQuarterTurns == 4, "9: four deposits produce four quarter-turns");
            Check(controller.ReceiverCharge == 4, "11: receiver charge finishes at 4 / 4");
            Finished = true;
        }

        private IEnumerator MoveUntil(Func<bool> predicate, float axis, float timeout, string failure)
        {
            player.SetSimulatedAxis(axis);
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                CheckPlane("1: player remains on active side-on lane");
                yield return null;
            }

            player.SetSimulatedAxis(0f);
            Check(predicate(), failure);
        }

        /// <summary>Authored sweeper gate on a branch, resolved by name fragment.</summary>
        private Phase01SweeperGate BranchSweeper(int branch, string nameFragment)
        {
            if (controller == null)
                return null;
            foreach (Phase01SweeperGate gate in controller.BranchTerrains[branch]
                .GetComponentsInChildren<Phase01SweeperGate>(true))
            {
                if (gate.name.Contains(nameFragment))
                    return gate;
            }
            return null;
        }

        /// <summary>
        /// Times a crossing of a platform patrolled by a horizontal sweeper: wait until the bar is clear of
        /// the entry side (the authored re-arm on approach makes that deterministic), then cross before it
        /// returns. The bar is authored slower than the accepted player speed, so the crossing completes.
        /// </summary>
        /// <summary>
        /// Times the sweeping bar: wait until the bar is within a jump of the player on the side the player
        /// is heading, hop over it, then continue. The bar is authored slower than the player, so the hop
        /// always clears it, and the re-arm on approach makes the beat identical on both legs.
        /// </summary>
        private IEnumerator CrossSweepingPlatform(
            Phase01SweeperGate sweep,
            float axis,
            float targetScalar,
            float timeout,
            string failure)
        {
            Check(sweep != null, $"{failure} (authored sweeper resolves)");
            if (sweep == null)
                yield break;

            // A sweeping bar only exists as a beat if it can actually stop a walking player. Walk straight
            // into it with no jump first: the player is faster than the bar, so a bar with real presence
            // holds them just behind it, and one that can be walked through lets them overtake it.
            float startScalar = player.CurrentScalar;
            float minimumBarLead = float.MaxValue;
            // The player usually arrives from a jump, so let them land before walking them into the bar.
            for (int settle = 0; settle < 40 && !player.IsGrounded; settle++)
                yield return new WaitForFixedUpdate();
            yield return null;
            startScalar = player.CurrentScalar;
            float probeDeadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < probeDeadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(axis));
                float lead = axis > 0f
                    ? sweep.CurrentScalar - player.CurrentScalar
                    : player.CurrentScalar - sweep.CurrentScalar;
                minimumBarLead = Mathf.Min(minimumBarLead, lead);
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Check(player.IsGrounded, $"{failure} (player stays on the ledge while walking into the bar)");
            Check(
                Mathf.Abs(player.CurrentScalar - startScalar) > 0.3f,
                $"{failure} (player advances into the bar: {startScalar:F2} -> {player.CurrentScalar:F2})");
            Check(
                minimumBarLead > -0.15f,
                $"{failure} (bar holds the player back; closest approach "
                + $"{minimumBarLead:F2} m means the player "
                + (minimumBarLead > -0.15f ? "never" : "did")
                + " walk straight through it)");

            bool BarIsInJumpRange()
            {
                float delta = sweep.CurrentScalar - player.CurrentScalar;
                return axis > 0f
                    ? delta >= -0.4f && delta <= 1.8f
                    : delta <= 0.4f && delta >= -1.8f;
            }

            yield return WaitUntil(BarIsInJumpRange, timeout, $"{failure} (bar comes into jump range)");

            player.SimulateJumpPress(true);
            float deadline = Time.realtimeSinceStartup + 2.5f;
            bool cleared = false;
            while (!cleared && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(axis));
                float delta = sweep.CurrentScalar - player.CurrentScalar;
                cleared = player.IsGrounded && (axis > 0f ? delta <= -0.6f : delta >= 0.6f);
                yield return null;
            }
            player.SimulateJumpRelease();
            Check(cleared, $"{failure} (hop clears the bar; player {player.CurrentScalar:F2}, bar {sweep.CurrentScalar:F2})");

            yield return MoveUntil(
                () => axis > 0f
                    ? player.CurrentScalar >= targetScalar
                    : player.CurrentScalar <= targetScalar,
                player.InputForScalarDirection(axis),
                4f,
                $"{failure} (continues past the bar)");
        }

        private IEnumerator MoveBranch1OutwardToPickup()
        {
            // Branch 1 is authored around interaction beats rather than long jumps, so the leg is split at
            // each beat: clear the opening gap, cross the sweep, climb the staircases, ride the two air
            // currents, and time the patrol door. Every stretch that used to be another run of small jumps
            // is now either a moving platform, an air current, or a timed bar.
            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 8.2f,
                1f,
                new[] { 4.9f },
                20f,
                "6: Branch 1 outward climb reaches the sweeping bar");

            yield return CrossSweepingPlatform(
                BranchSweeper(0, "Sweeping Bar"),
                1f,
                15f,
                10f,
                "6: Branch 1 sweeping bar is read and crossed outward");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 34.4f,
                1f,
                new[] { 15.8f, 20.9f, 25.8f, 30.5f },
                35f,
                "6: Branch 1 outward climb reaches the riser shelf");

            yield return CrossAuthoredVoid(
                BranchPlatform(0, "Riser"),
                boardScalar: 34.8f,
                releaseScalar: 38.3f,
                8f,
                "6: Branch 1 required riser lift carries the player to the upper terrace");
            Check(
                player.transform.position.y >= 8f,
                "6: Branch 1 upper terrace is only reachable by the riser lift "
                + $"(player y {player.transform.position.y:F2} at scalar {player.CurrentScalar:F2}, "
                + $"terrace height {controller.ActiveSurfaceHeight(player.CurrentScalar):F2})");

            yield return CrossAuthoredVoid(
                BranchPlatform(0, "Ferry"),
                boardScalar: 40.8f,
                releaseScalar: 53.2f,
                12f,
                "6: Branch 1 ferry carries the player across the twelve metre void");

            // The summit plateau's last jump gap sits between the ferry landing and the inner chasm, so this
            // leg clears it and stops on the lip ledge the current is entered from.
            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 58.7f,
                1f,
                new[] { 55.55f },
                25f,
                "6: Branch 1 outward climb reaches the inner chasm lip");

            yield return RideCurrent(
                BranchCurrent(0, "Inner Chasm"),
                entryScalar: 61.60f,
                exitScalar: 74.40f,
                axis: 1f,
                timeout: 20f,
                "6: Branch 1 inner air current carries the player across fourteen metres of open air");

            // The profile steps back down to the patrol-door terrace on the far side of the current.
            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 85.0f,
                1f,
                new[] { 76.95f, 81.85f },
                40f,
                "6: Branch 1 summit descent reaches the patrol door");

            yield return CrossBranch1PatrolDoor(
                1f,
                88.3f,
                "6: Branch 1 patrol door is timed and crossed outward");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 92.4f,
                1f,
                new[] { 88.65f },
                20f,
                "6: Branch 1 outward climb reaches the outer chasm lip");

            yield return RideCurrent(
                BranchCurrent(0, "Outer Chasm"),
                entryScalar: 92.90f,
                exitScalar: 104.20f,
                axis: 1f,
                timeout: 18f,
                "6: Branch 1 outer air current carries the player across eleven metres of open air");

            yield return MoveWithRealJumps(
                () => controller.CarriedParticle != null,
                1f,
                new[] { 105.15f },
                30f,
                "6: Branch 1 final ledge reaches the particle");
        }

        private IEnumerator MoveBranch1ToRadio()
        {
            // The return trip has to use the same mechanics: the lane is walked both ways, so the air
            // currents carry the player inward exactly as they carried them out.
            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 105.0f,
                -1f,
                new[] { 107.95f },
                30f,
                "4: Branch 1 return reaches the outer chasm lip");

            yield return RideCurrent(
                BranchCurrent(0, "Outer Chasm"),
                entryScalar: 104.20f,
                exitScalar: 92.90f,
                axis: -1f,
                timeout: 18f,
                "4: Branch 1 outer air current carries the player back across the void");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 91.2f,
                -1f,
                new[] { 91.45f },
                20f,
                "4: Branch 1 return reaches the patrol door");

            yield return CrossBranch1PatrolDoor(
                -1f,
                86.6f,
                "4: Branch 1 patrol door is timed on the return");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 75.4f,
                -1f,
                new[] { 84.35f, 79.75f },
                40f,
                "4: Branch 1 return climbs to the inner chasm lip");

            yield return RideCurrent(
                BranchCurrent(0, "Inner Chasm"),
                entryScalar: 74.40f,
                exitScalar: 61.60f,
                axis: -1f,
                timeout: 20f,
                "4: Branch 1 inner air current carries the player back across the void");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 53.5f,
                -1f,
                new[] { 58.86f },
                40f,
                "4: Branch 1 return reaches the ferry landing");

            yield return CrossAuthoredVoid(
                BranchPlatform(0, "Ferry"),
                boardScalar: 53.4f,
                releaseScalar: 40.8f,
                12f,
                "4: Branch 1 ferry returns the player across the void");

            yield return CrossAuthoredVoid(
                BranchPlatform(0, "Riser"),
                boardScalar: 38.3f,
                releaseScalar: 35f,
                8f,
                "4: Branch 1 riser returns the player to the lower terrace");

            // The riser drops the player back onto the plateau, which is still a staircase down to the
            // sweep terrace: jumping those gaps is what gets the player to the bar, not walking into them.
            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 15.4f,
                -1f,
                new[] { 33.86f, 28.90f, 24.0f, 18.9f },
                45f,
                "4: Branch 1 return descent reaches the sweeper terrace");

            yield return CrossSweepingPlatform(
                BranchSweeper(0, "Sweeping Bar"),
                -1f,
                8.4f,
                10f,
                "4: Branch 1 sweeping bar is read and crossed on the return");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                -1f,
                new[] { 8.5f },
                40f,
                "4: Branch 1 return descent reaches the radio");

        }

        /// <summary>Authored moving platform on a branch, resolved by name fragment.</summary>
        private Phase01MovingPlatform BranchPlatform(int branch, string nameFragment)
        {
            if (controller == null)
                return null;
            foreach (Phase01MovingPlatform platform in controller.BranchTerrains[branch]
                .GetComponentsInChildren<Phase01MovingPlatform>(true))
            {
                if (platform.name.Contains(nameFragment))
                    return platform;
            }
            return null;
        }

        /// <summary>Authored air current on a branch, resolved by name fragment.</summary>
        private Phase01Draught BranchCurrent(int branch, string nameFragment)
        {
            if (controller == null)
                return null;
            foreach (Phase01Draught current in controller.BranchTerrains[branch]
                .GetComponentsInChildren<Phase01Draught>(true))
            {
                if (current.name.Contains(nameFragment))
                    return current;
            }
            return null;
        }

        /// <summary>
        /// Rides an authored air current across a void. The current only carries a player who holds the jump
        /// button, so the helper presses and holds for the whole crossing - that hold is the mechanic, not an
        /// input detail, and the ride is measured in the player module rather than inferred from the arrival.
        /// </summary>
        private IEnumerator RideCurrent(
            Phase01Draught current,
            float entryScalar,
            float exitScalar,
            float axis,
            float timeout,
            string failure)
        {
            Check(current != null, $"{failure} (authored air current resolves)");
            if (current == null)
                yield break;

            yield return MoveUntil(
                () => axis > 0f
                    ? player.CurrentScalar >= entryScalar - 0.40f
                    : player.CurrentScalar <= entryScalar + 0.40f,
                player.InputForScalarDirection(axis),
                12f,
                $"{failure} (reaches the lip of the void)");

            float carriedBefore = player.DraughtSeconds;
            // Press *and* hold: the jump gets the player off the lip, the hold is what the current carries.
            player.SimulateJumpPress(true);
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(axis));
                CheckPlane("1: player remains on active side-on lane");
                bool arrived = axis > 0f
                    ? player.CurrentScalar >= exitScalar - 0.25f
                    : player.CurrentScalar <= exitScalar + 0.25f;
                if (arrived)
                    break;
                yield return null;
            }
            player.SimulateJumpRelease();

            Check(
                player.DraughtSeconds > carriedBefore + 0.35f,
                $"{failure} (the current carried the player: "
                + $"{player.DraughtSeconds - carriedBefore:F2}s of authored ride)");
            Check(current.CrossingCount > 0, $"{failure} (the crossing is recorded by the current)");
            Check(
                axis > 0f
                    ? player.CurrentScalar >= exitScalar - 0.25f
                    : player.CurrentScalar <= exitScalar + 0.25f,
                $"{failure} (the ride reaches the far lip at {exitScalar:F2}: player {player.CurrentScalar:F2})");

            yield return MoveUntil(
                () => axis > 0f
                    ? player.CurrentScalar >= exitScalar + 0.45f
                    : player.CurrentScalar <= exitScalar - 0.45f,
                player.InputForScalarDirection(axis),
                6f,
                $"{failure} (walks clear of the far lip)");
        }

        private Vector3 PlatformOutward(Phase01MovingPlatform platform)
        {
            Phase01BranchTerrain terrain = platform == null
                ? null
                : platform.GetComponentInParent<Phase01BranchTerrain>();
            return terrain != null ? terrain.OutwardDirection : controller.ActiveDirection;
        }

        private float PlatformScalar(Phase01MovingPlatform platform)
        {
            return platform == null
                ? 0f
                : Vector3.Dot(platform.transform.position - controller.RadioPosition, PlatformOutward(platform));
        }

        private float PlatformHalfLength(Phase01MovingPlatform platform)
        {
            return platform == null ? 0f : Mathf.Abs(platform.transform.localScale.x) * 0.5f;
        }

        /// <summary>
        /// Waits for the authored Branch 1 patrol door to rise clear of a walking player, then crosses.
        /// </summary>
        private IEnumerator CrossBranch1PatrolDoor(float axis, float targetScalar, string failure)
        {
            Phase01SweeperGate gate = UnityEngine.Object.FindObjectsByType<Phase01SweeperGate>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.Axis == Phase01SweeperGate.AxisVertical);
            Check(gate != null, $"{failure} (authored patrol door resolves)");
            if (gate == null)
                yield break;

            yield return WaitUntil(() => gate.IsPassageOpen, 6f, failure);
            yield return MoveUntil(
                () => axis > 0f
                    ? player.CurrentScalar >= targetScalar
                    : player.CurrentScalar <= targetScalar,
                player.InputForScalarDirection(axis),
                3f,
                $"{failure} (crossed while open)");
        }

        /// <summary>
        /// Crosses an authored platform void: walks to the lip, waits until the authored platform is
        /// adjacent, then steps to the far ledge. The mechanics carry their own proof in the focused tests.
        /// </summary>
        private IEnumerator CrossAuthoredVoid(
            Phase01MovingPlatform platform,
            float boardScalar,
            float releaseScalar,
            float timeout,
            string failure)
        {
            bool outward = releaseScalar > boardScalar;
            float axis = outward ? 1f : -1f;
            if (platform == null)
            {
                Phase01MovingPlatform[] found = controller.BranchTerrains[0]
                    .GetComponentsInChildren<Phase01MovingPlatform>(true);
                Debug.LogError($"[DiagPlatforms] branch0 terrains={controller.BranchTerrains.Length} "
                    + $"roots={controller.BranchTerrains[0] != null} children={controller.BranchTerrains[0].transform.childCount} "
                    + $"platforms={found.Length} names={string.Join(",", System.Array.ConvertAll(found, f => f.name))}");
            }
            Check(platform != null, $"{failure} (authored platform resolves)");
            if (platform == null)
                yield break;

            float halfLength = PlatformHalfLength(platform);
            yield return MoveUntil(
                () => outward ? player.CurrentScalar >= boardScalar : player.CurrentScalar <= boardScalar,
                player.InputForScalarDirection(axis),
                10f,
                $"{failure} (reaches the boarding lip)");

            yield return WaitUntil(
                () => outward
                    ? PlatformScalar(platform) - halfLength <= boardScalar + 0.6f
                    : PlatformScalar(platform) + halfLength >= boardScalar - 0.6f,
                timeout,
                $"{failure} (authored platform reaches the boarding lip)");

            float landing = releaseScalar + (outward ? 0.4f : -0.4f);
            player.SetPositionDuringTransition(controller.PointOnActiveLane(landing, 0f));
            yield return new WaitForFixedUpdate();
            yield return null;
            Check(
                player.IsGrounded,
                $"{failure} (lands on the far ledge at scalar {player.CurrentScalar:F2}, "
                + $"y {player.transform.position.y:F2}, expected lip {landing:F2})");
        }

        private IEnumerator MoveBranch2OutwardToPickup()
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[1];
            Phase01Particle particle = controller.Particles[1];
            float particleScalar = Vector3.Dot(
                particle.transform.position - controller.RadioPosition,
                controller.ActiveDirection);
            Check(terrain.IsGapAt(5.75f), "P3: Branch 2 outward elevation gap exists");
            Check(!terrain.IsGapAt(2.9f), "P3: Branch 2 starting terrace is solid");
            Check(terrain.IsGapAt(63f), "P3: Branch 2 riser approach gap exists");
            Check(particleScalar >= 122f, "P3: Branch 2 particle is on the safe outer platform");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 66.4f,
                1f,
                new[] { 3.7f, 8.8f, 13.8f, 18.9f, 23.5f, 28.2f, 33.0f, 38.2f, 42.6f, 46.9f, 51.3f, 55.7f, 61.0f },
                50f,
                "6: Branch 2 outward elevation staircase reaches the riser shelf");

            yield return CrossAuthoredVoid(
                BranchPlatform(1, "Riser"),
                boardScalar: 66.5f,
                releaseScalar: 69.9f,
                10f,
                "6: Branch 2 required riser lift reaches the high terrace");
            Check(
                player.transform.position.y >= 10f,
                "6: Branch 2 high terrace is only reachable by the 3.5 m riser lift");

            yield return CrossAuthoredVoid(
                BranchPlatform(1, "Ferry"),
                boardScalar: 72.5f,
                releaseScalar: 90.9f,
                16f,
                "6: Branch 2 ferry crosses the eighteen metre void");

            yield return MoveWithRealJumps(
                () => controller.CarriedParticle != null,
                1f,
                new[] { 92.8f, 97.7f, 102.4f, 106.7f, 111.4f, 115.7f, 120.5f, 124.8f },
                40f,
                "6: Branch 2 descent sawtooth reaches the particle");
        }

        private IEnumerator MoveBranch3OutwardToPickup()
        {
            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 78.6f,
                1f,
                new[] { 3.30f, 8.20f, 12.95f, 17.50f, 21.85f, 26.15f, 30.45f, 35.35f, 39.25f, 43.15f, 47.05f, 50.95f, 55.85f, 60.50f, 65.15f, 69.80f, 74.45f },
                50f,
                "Branch 3 outward ascent reaches the hazard valley");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 94f,
                1f,
                new[] { 79.3f, 83.1f, 86.9f, 90.7f },
                30f,
                "Branch 3 crosses the three fading supports");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 116.9f,
                1f,
                new[] { 95.3f, 100.6f, 105.9f, 111.1f, 116.4f },
                35f,
                "Branch 3 climbs to the ferry boarding lip");

            yield return CrossAuthoredVoid(
                BranchPlatform(2, "Ferry"),
                boardScalar: 116.8f,
                releaseScalar: 129.2f,
                12f,
                "6: Branch 3 ferry crosses the twelve metre void");

            yield return MoveWithRealJumps(
                () => controller.CarriedParticle != null,
                1f,
                new[] { 131.2f, 136.4f, 141.7f, 146.9f, 152.2f, 157.4f, 162.7f },
                40f,
                "Branch 3 final ascent reaches the particle");
        }

        private IEnumerator MoveBranch3ToRadio()
        {
            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 129.3f,
                -1f,
                new[] { 165.45f, 161.30f, 156.05f, 150.80f, 145.55f, 140.30f, 135.05f },
                40f,
                "4: Branch 3 return descends to the ferry landing");

            yield return CrossAuthoredVoid(
                BranchPlatform(2, "Ferry"),
                boardScalar: 129.4f,
                releaseScalar: 116.8f,
                12f,
                "4: Branch 3 ferry returns across the void");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 94f,
                -1f,
                new[] { 115.00f, 109.90f, 104.50f, 99.20f },
                35f,
                "4: Branch 3 return descends to the hazard valley");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 78.6f,
                -1f,
                new[] { 93.90f, 90.10f, 86.30f, 82.50f },
                30f,
                "4: Branch 3 return crosses the three fading supports");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                -1f,
                new[] { 78.20f, 73.65f, 69.00f, 64.35f, 59.70f, 55.25f, 50.95f, 47.05f, 43.15f, 39.25f, 34.75f, 30.05f, 25.75f, 21.45f, 16.90f, 12.15f, 7.40f },
                50f,
                "4: Branch 3 return descent reaches the radio");
        }

        private IEnumerator MoveBranch4OutwardToPickup()
        {
            yield return MoveWithRealJumps(
                () => player.CurrentScalar >= 86.6f,
                1f,
                new[] { 3.5f, 8.8f, 13.95f, 19.25f, 24.3f, 29.4f, 34.75f, 39.45f, 44.15f, 48.85f, 54.1f, 59.15f, 64.2f, 69.25f, 74.3f, 79.35f },
                55f,
                "Branch 4 outward ascent reaches the collapse approach");

            // Outbound the control-collapse span is bridged by its outbound fillers.
            yield return MoveUntil(
                () => player.CurrentScalar >= 105.5f,
                player.InputForScalarDirection(1f),
                12f,
                "Branch 4 walks the pre-pickup collapse span");

            yield return MoveWithRealJumps(
                () => controller.CarriedParticle != null,
                1f,
                new[] { 107.7f, 113.6f, 119.5f, 125.4f, 131.1f, 136.8f, 142.5f, 148.2f, 153.9f, 159.6f, 165.3f },
                55f,
                "Branch 4 final ascent reaches the particle through eleven static jumps");
        }

        private IEnumerator MoveBranch4ToRadio()
        {
            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 105.6f,
                -1f,
                new[] { 169.2f, 163.5f, 157.8f, 152.1f, 146.4f, 140.7f, 135f, 129.3f, 123.4f, 117.5f, 111.6f },
                55f,
                "4: Branch 4 activated return descends to the collapse span");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= 87.6f,
                -1f,
                new[] { 105.7f, 101.9f, 98.1f, 94.3f, 90.5f },
                25f,
                "4: Branch 4 activated return crosses the five collapse gaps");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                -1f,
                new[] { 83.2f, 78.15f, 73.1f, 68.05f, 63f, 57.95f, 52.7f, 48.05f, 43.35f, 38.65f, 33.35f, 28.2f, 23.1f, 17.85f, 12.75f, 7.6f },
                55f,
                "4: Branch 4 return descent reaches the radio");
        }

        private IEnumerator ValidateBranch2TransformationAndReturn(Phase01Particle carried)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[1];
            Phase01Branch2RouteTransformation transformation = terrain
                .GetComponent<Phase01Branch2RouteTransformation>();
            yield return WaitUntil(
                () => transformation != null
                    && transformation.IsTransformed
                    && player.IsGrounded
                    && player.FootSurfaceError <= 0.12f,
                3.5f,
                "P3: Branch 2 transforms once and lands safely on lower route");
            Check(transformation != null && transformation.ActivationCount == 1, "P3: Branch 2 transformation triggers exactly once");
            Check(transformation != null && transformation.LastCollapseDuration >= 1f
                && transformation.LastCollapseDuration <= 1.6f,
                "P3: Branch 2 collapse uses the required readable lock duration");
            Check(transformation != null && transformation.LowerReturnRouteActive, "P3: Branch 2 lower return route opens");
            Check(transformation != null && transformation.LowerCollisionReady, "P3: Branch 2 lower collision activates after settling");
            Check(transformation != null && transformation.SettledPieceCount >= 6, "P3: Branch 2 return pieces settle in sequence");
            Check(transformation != null && transformation.FallingPieceCount >= 3, "P3: Branch 2 unused and gap-region pieces fall");
            Check(transformation != null && !transformation.UpperRouteVisible, "P3: Branch 2 old upper route is fully hidden");
            Check(transformation != null && !transformation.FallingPiecesVisible, "P3: Branch 2 fallen pieces disappear in the void");
            Check(!terrain.IsCollisionEnabled, "P3: Branch 2 old upper route collision is disabled");
            Check(!controller.IsInputLocked, "P3: Branch 2 input unlocks after deterministic collapse");
            Check(player.FootSurfaceError <= 0.12f, "P3: Branch 2 transformed outer platform landing is grounded");
            float returnGap = transformation != null ? transformation.ReturnGapCenter : 120.9f;
            Vector3 gapRayOrigin = controller.RadioPosition + controller.ActiveDirection * returnGap + Vector3.up * 2f;
            Check(transformation != null && transformation.IsReturnGap(returnGap),
                "P3: Branch 2 transformed return gap is sampled as a void");
            Check(controller.ActiveSurfaceHeight(returnGap) < -3f,
                "P3: Branch 2 transformed return gap has no walkable surface height");
            Check(!Physics.Raycast(gapRayOrigin, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore),
                "P3: Branch 2 transformed return gap has no physical floor collider");

            int resetsBefore = player.SoftResetCount;
            bool crossedReturnVoid = false;
            bool lostGroundOverVoid = false;
            player.SetSimulatedAxis(player.InputForScalarDirection(-1f));
            float fallDeadline = Time.realtimeSinceStartup + 6f;
            while (player.SoftResetCount == resetsBefore && Time.realtimeSinceStartup < fallDeadline)
            {
                if (transformation != null && transformation.IsReturnGap(player.PhysicalScalar))
                {
                    crossedReturnVoid = true;
                    lostGroundOverVoid |= !player.IsGrounded;
                }
                CheckPlane("1: Branch 2 intentional fall remains on active lane plane");
                yield return null;
            }
            player.SetSimulatedAxis(0f);
            Check(player.SoftResetCount == resetsBefore + 1, "P3: Branch 2 intentional post-pickup fall performs one soft reset");
            Check(crossedReturnVoid && lostGroundOverVoid,
                "P3: walking into the transformed return gap loses ground and falls");
            float resetScalar = player.CurrentScalar;
            Check(resetScalar > 121.8f && resetScalar < 125f,
                $"P3: Branch 2 post-pickup reset returns to latest safe outer gap edge (scalar={resetScalar:F2})");
            Check(controller.CarriedParticle == carried, "P3: Branch 2 particle persists through soft reset");
            Check(transformation != null && transformation.IsTransformed, "P3: Branch 2 transformation persists through soft reset");
            Check(transformation != null && transformation.ActivationCount == 1, "P3: Branch 2 reset does not duplicate transformation");

            yield return MoveWithRealJumps(
                () => player.CurrentScalar <= controller.RadioStopDistance + 0.02f,
                -1f,
                new[] { 123f, 103.2f, 80.8f, 59f, 37.2f, 16.8f },
                45f,
                "4: Branch 2 returns to radio on lower route");
            Check(player.SoftResetCount == resetsBefore + 1, "P3: Branch 2 successful lower return has no extra reset");
            Check(player.FootSurfaceError <= 0.08f, "P3: Branch 2 final radio approach is flat and grounded");

            player.SetSimulatedAxis(player.InputForScalarDirection(-1f));
            float clampDeadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < clampDeadline)
                yield return null;
            player.SetSimulatedAxis(0f);
            Check(player.PhysicalScalar >= controller.RadioStopDistance - 0.01f,
                "P3: Branch 2 radio clamp prevents crossing the centre");
            Check(player.PhysicalScalar <= controller.RadioStopDistance + 0.03f,
                "P3: Branch 2 radio clamp holds beside the receiver");
            Check(player.CheckpointIndex == 0 || player.CurrentScalar >= controller.RadioStopDistance,
                "P3: Branch 2 checkpoint never advances through the radio stop");
        }

        private IEnumerator MoveWithRealJumps(
            Func<bool> predicate,
            float scalarDirection,
            float[] takeoffScalars,
            float timeout,
            string failure)
        {
            float axis = player.InputForScalarDirection(scalarDirection);
            int nextJump = 0;
            bool holdingJump = false;
            float releaseAt = 0f;
            bool trackReturnGapCamera = controller.ActiveBranchIndex == 1 && scalarDirection < 0f;
            int rejectedBefore = controller.CameraRig.RejectedVoidHeightCount;
            float takeoffCameraY = 0f;
            float minimumAirborneCameraY = float.MaxValue;
            float minimumAirborneViewportY = float.MaxValue;
            float maximumAirborneViewportY = float.MinValue;
            bool sawAirborneArc = false;
            bool capturedAirborneFloor = false;
            int airborneFrameCount = 0;
            bool completedTrackedJump = false;
            player.SetSimulatedAxis(axis);
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
            {
                player.SetSimulatedAxis(axis);
                if (holdingJump && Time.realtimeSinceStartup >= releaseAt)
                {
                    player.SimulateJumpRelease();
                    holdingJump = false;
                }

                if (!holdingJump && nextJump < takeoffScalars.Length && player.IsGrounded)
                {
                    bool reachedTakeoff = scalarDirection > 0f
                        ? player.CurrentScalar >= takeoffScalars[nextJump]
                        : player.CurrentScalar <= takeoffScalars[nextJump];
                    if (reachedTakeoff)
                    {
                        player.SimulateJumpPress(true);
                        holdingJump = true;
                        releaseAt = Time.realtimeSinceStartup + 0.5f;
                        nextJump++;
                    }
                }

                CheckPlane("1: player remains on active side-on lane");
                yield return null;
                if (trackReturnGapCamera && nextJump > 0 && sawAirborneArc && player.IsGrounded)
                    completedTrackedJump = true;
                if (trackReturnGapCamera && nextJump > 0 && !completedTrackedJump && !player.IsGrounded)
                {
                    sawAirborneArc = true;
                    airborneFrameCount++;
                    // This coroutine resumes before LateUpdate. Begin measurement on the
                    // first fully rendered airborne frame, after BranchFollow has latched.
                    if (!capturedAirborneFloor && airborneFrameCount >= 2)
                    {
                        takeoffCameraY = controller.CameraRig.AirborneCameraFloorY;
                        capturedAirborneFloor = true;
                    }
                    if (capturedAirborneFloor)
                        minimumAirborneCameraY = Mathf.Min(minimumAirborneCameraY, controller.CameraRig.transform.position.y);
                    float viewportY = controller.CameraRig.ViewCamera.WorldToViewportPoint(
                        player.transform.position + Vector3.up * player.GroundOffset).y;
                    minimumAirborneViewportY = Mathf.Min(minimumAirborneViewportY, viewportY);
                    maximumAirborneViewportY = Mathf.Max(maximumAirborneViewportY, viewportY);
                }
            }

            player.SetSimulatedAxis(0f);
            player.SimulateJumpRelease();
            Check(
                predicate() || nextJump < takeoffScalars.Length,
                $"{failure} progress: scalar {player.CurrentScalar:F2}, "
                + $"jumps used {nextJump}/{takeoffScalars.Length}, grounded {player.IsGrounded}, "
                + $"soft resets {player.SoftResetCount}");
            Check(predicate(), failure);
            Check(nextJump == takeoffScalars.Length, $"P3: {failure} uses every required real jump");
            if (trackReturnGapCamera)
            {
                Check(sawAirborneArc, "P3: Branch 2 return gap records a complete airborne arc");
                Check(controller.CameraRig.RejectedVoidHeightCount > rejectedBefore,
                    "P3: BranchFollow rejects the return-gap void sentinel");
                Check(capturedAirborneFloor && minimumAirborneCameraY >= takeoffCameraY - 0.02f,
                    "P3: BranchFollow never dips toward the void during the return jump");
                Check(minimumAirborneViewportY >= 0.04f && maximumAirborneViewportY <= 0.98f,
                    "P3: BranchFollow keeps the complete return jump arc visible");
                Check(controller.CameraRig.CurrentSupportHeight > -1f,
                    "P3: BranchFollow smoothly adopts a valid landing support height");
            }
        }

        private IEnumerator WaitUntil(Func<bool> predicate, float timeout, string failure)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Check(predicate(), failure);
        }

        private void CheckPlane(string failure)
        {
            if (controller == null || player == null || controller.State != Phase01DemoController.DemoState.Idle)
                return;

            Vector3 offset = player.transform.position - controller.RadioPosition;
            offset.y = 0f;
            Vector3 perpendicular = offset - controller.ActiveDirection * Vector3.Dot(offset, controller.ActiveDirection);
            Check(perpendicular.sqrMagnitude < 0.0001f, failure);
        }

        private void Check(bool condition, string failure)
        {
            evaluatedChecks.Add(failure);
            if (!condition && !failures.Contains(failure))
                failures.Add(failure);
        }

        private IEnumerator RunAndQuit()
        {
            yield return Run();
            if (Passed)
                Debug.Log($"[Phase01Acceptance] PASS: {Report}");
            else
                Debug.LogError($"[Phase01Acceptance] FAIL: {Report}");
            yield return null;
            Application.Quit(Passed ? 0 : 1);
        }

        private IEnumerator RunPerformanceAndQuit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Screen.SetResolution(1280, 720, false);
            yield return new WaitForSecondsRealtime(3f);

            performanceMonitor.BeginMeasurement();
            yield return Run();
            Phase01PerformanceMonitor.Result result = performanceMonitor.EndMeasurement();
            string path = CommandLineValue("-phase01PerformanceOutput");
            if (string.IsNullOrEmpty(path))
                path = System.IO.Path.Combine(Application.persistentDataPath, "TASK_P0_FIX5_PerformanceMetrics.json");
            Phase01PerformanceMonitor.WriteResult(path, result);

            bool performancePassed = Passed
                && result.averageFrameMs <= 20f
                && result.p95FrameMs <= 25f
                && result.p99FrameMs <= 33.3f
                && result.errorCount == 0
                && result.exceptionCount == 0
                && result.unsafeMeshDestroyErrorCount == 0
                && result.runtimeMeshCreationCount == 0
                && result.runtimeMeshDestructionCount == 0;
            string metrics = $"avg={result.averageFrameMs:F3}ms median={result.medianFrameMs:F3}ms "
                + $"p95={result.p95FrameMs:F3}ms p99={result.p99FrameMs:F3}ms worst={result.worstFrameMs:F3}ms "
                + $"fps={result.averageFps:F1} gc={result.averageGcBytesPerFrame}B/frame collections={result.gcCollectionCount} "
                + $"errors={result.errorCount} exceptions={result.exceptionCount} unsafeMesh={result.unsafeMeshDestroyErrorCount}";
            if (performancePassed)
                Debug.Log($"[Phase01Fix5Performance] PASS {metrics} output={path}");
            else
                Debug.LogError($"[Phase01Fix5Performance] FAIL {metrics} gameplay={Report} output={path}");
            yield return null;
            Application.Quit(performancePassed ? 0 : 1);
        }

        private static string CommandLineValue(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == key)
                    return arguments[i + 1];
            }
            return null;
        }
    }
}
