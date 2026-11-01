using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Envelope maths shared by the TASK 005 Branch 1 tests. Everything is derived from the accepted
    /// motor constants, so the difficulty claims are measured against the real jump envelope rather
    /// than against a hardcoded table.
    /// </summary>
    public static class Phase01Branch1Envelope
    {
        /// <summary>Mirrors Phase01ParkourMotor.fallGravityMultiplier for the accepted fall phase.</summary>
        public const float FallGravityMultiplier = 1.45f;

        public const float MaximumFallSpeed = 16f;
        /// <summary>
        /// Comfort cap: a required jump may use at most 65% of the reach available at its elevation, so
        /// every jump has real margin. Difficulty comes from authored interactions, not from distance.
        /// </summary>
        public const float ComfortCap = 0.65f;

        /// <summary>An authored gap must still be a genuine gap, not a step the player can walk.</summary>
        public const float MinimumAuthoredGap = 1.2f;

        public static float Gravity(float jumpHeight, float timeToApex)
        {
            return 2f * jumpHeight / (timeToApex * timeToApex);
        }

        public static float LaunchSpeed(float jumpHeight, float timeToApex)
        {
            return 2f * jumpHeight / timeToApex;
        }

        /// <summary>
        /// Airtime from takeoff until the player is back at <paramref name="rise"/> metres above the
        /// takeoff height while descending, or NaN when the height is above the jump apex.
        /// </summary>
        public static float Airtime(float rise, float jumpHeight, float timeToApex)
        {
            if (rise >= jumpHeight)
                return float.NaN;

            float gravity = Gravity(jumpHeight, timeToApex);
            float fallGravity = gravity * FallGravityMultiplier;
            float elapsed = LaunchSpeed(jumpHeight, timeToApex) / gravity;
            float height = jumpHeight;
            float fallSpeed = 0f;
            const float step = 0.0005f;
            int guard = 0;
            while (height > rise + 0.000001f && guard++ < 400000)
            {
                fallSpeed = Mathf.Min(fallSpeed + fallGravity * step, MaximumFallSpeed);
                height -= fallSpeed * step;
                elapsed += step;
            }
            return elapsed;
        }

        public static float Reach(float rise, float moveSpeed, float jumpHeight, float timeToApex)
        {
            float airtime = Airtime(rise, jumpHeight, timeToApex);
            return float.IsNaN(airtime) ? float.NaN : moveSpeed * airtime;
        }

        public static List<Vector2> AuthoredGapSpans()
        {
            return AuthoredGapSpans(0);
        }

        public static List<Vector2> AuthoredGapSpans(int branch)
        {
            IReadOnlyList<float> bounds = branch == 0
                ? Phase01BranchTerrain.Branch1AuthoredGapBounds
                : branch == 1
                    ? Phase01BranchTerrain.Branch2AuthoredGapBounds
                    : branch == 2
                        ? Phase01BranchTerrain.Branch3AuthoredGapBounds
                        : Phase01BranchTerrain.Branch4AuthoredGapBounds;
            List<Vector2> spans = new List<Vector2>(bounds.Count / 2);
            for (int i = 0; i + 1 < bounds.Count; i += 2)
                spans.Add(new Vector2(bounds[i], bounds[i + 1]));
            return spans;
        }

        public static bool IsPlatformVoid(Vector2 span)
        {
            return IsPlatformVoid(0, span);
        }

        public static bool IsPlatformVoid(int branch, Vector2 span)
        {
            Vector2[] voids = branch == 0
                ? Phase01BranchTerrain.Branch1PlatformVoids
                : branch == 1
                    ? Phase01BranchTerrain.Branch2PlatformVoids
                    : branch == 2
                        ? Phase01BranchTerrain.Branch3PlatformVoids
                        : Phase01BranchTerrain.Branch4PlatformVoids;
            return voids.Any(
                authored => Mathf.Abs(authored.x - span.x) < 0.1f && Mathf.Abs(authored.y - span.y) < 0.1f);
        }

        /// <summary>Landing-platform midpoints derived from any branch's authored gap bounds.</summary>
        public static List<float> AuthoredStandingSamples(int branch)
        {
            List<Vector2> spans = AuthoredGapSpans(branch);
            List<float> samples = new List<float> { spans[0].x - 1.5f };
            for (int i = 0; i + 1 < spans.Count; i++)
                samples.Add((spans[i].y + spans[i + 1].x) * 0.5f);
            samples.Add(spans[spans.Count - 1].y + 1.5f);
            return samples;
        }

        public static int AuthoredJumpGapCount(Phase01DemoController controller)
        {
            return AuthoredJumpGapCount(0);
        }

        public static int AuthoredJumpGapCount(int branch)
        {
            return AuthoredGapSpans(branch).Count(span => !IsPlatformVoid(branch, span));
        }
    }

    public sealed class Phase01Branch1DifficultyPlayModeTests
    {
        /// <summary>
        /// Cheap invariant guard: a terrain profile with mismatched scalars/heights/widths silently
        /// reports no gaps and a zero surface height, which is how a single stray array entry produced a
        /// whole branch of driver failures during TASK 006.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryBranchProfileIsInternallyConsistent()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Assert.That(controller, Is.Not.Null);

            for (int branch = 0; branch < 4; branch++)
            {
                Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
                Assert.That(terrain, Is.Not.Null);
                Assert.That(terrain.HasValidProfile, Is.True,
                    $"Branch {branch + 1} profile arrays do not align "
                    + $"(points {terrain.ProfilePointCount}).");
                Assert.That(terrain.ProfilePointCount, Is.GreaterThanOrEqualTo(4),
                    $"Branch {branch + 1} needs at least four profile points.");
                Assert.That(terrain, Is.Not.Null, $"Branch {branch + 1} terrain is missing.");
                Assert.That(terrain.TopMesh, Is.Not.Null, $"Branch {branch + 1} top mesh is missing.");
                Assert.That(terrain.IsCollisionEnabled, Is.True);

                foreach (float sample in Phase01Branch1Envelope.AuthoredStandingSamples(branch))
                {
                    Assert.That(
                        Physics.Raycast(
                            controller.RadioPosition
                                + terrain.OutwardDirection * sample
                                + Vector3.up * (Mathf.Max(terrain.SampleHeight(sample), -0.5f) + 2.5f),
                            Vector3.down,
                            6f,
                            ~0,
                            QueryTriggerInteraction.Ignore),
                        Is.True,
                        $"Branch {branch + 1} landing at {sample:F2} has no support "
                        + $"(gapAt={terrain.IsGapAt(sample)}, surface={terrain.SampleHeight(sample):F2}, "
                        + $"points={terrain.ProfilePointCount}).");
                }
            }
        }

        [UnityTest]
        public IEnumerator Branch1_EveryAuthoredGapStaysInsideTheUphillReachEnvelope()
        {
            yield return LoadBranch();

            int measured = 0;
            float hardest = 0f;
            foreach (Vector2 span in Phase01Branch1Envelope.AuthoredGapSpans())
            {
                if (Phase01Branch1Envelope.IsPlatformVoid(span))
                    continue;

                float rise = UphillRise(span);
                float reach = Phase01Branch1Envelope.Reach(
                    rise,
                    player.MoveSpeed,
                    player.JumpHeight,
                    player.TimeToApex);
                Assert.That(float.IsNaN(reach), Is.False,
                    $"Branch 1 gap {span} rises {rise:F2} m, which is above the {player.JumpHeight:F2} m jump apex.");
                Assert.That(rise, Is.LessThanOrEqualTo(1.75f),
                    $"Branch 1 gap {span} rises {rise:F2} m and could not be climbed on the return trip.");

                float ratio = (span.y - span.x) / reach;
                hardest = Mathf.Max(hardest, ratio);
                measured++;
                Assert.That(ratio, Is.LessThanOrEqualTo(Phase01Branch1Envelope.ComfortCap + 0.01f),
                    $"Branch 1 gap {span} uses {(span.y - span.x):F2} m of a {reach:F2} m uphill reach "
                    + $"({ratio:P0}); jumps stay at or below {Phase01Branch1Envelope.ComfortCap:P0} of reach.");
                Assert.That(span.y - span.x,
                    Is.GreaterThanOrEqualTo(Phase01Branch1Envelope.MinimumAuthoredGap - 0.01f),
                    $"Branch 1 gap {span} is not a real gap.");
            }

            Assert.That(measured, Is.EqualTo(11),
                "Branch 1 should author eleven deliberate jump gaps: the branch spends its length on "
                + "mechanics now, not on another run of small jumps.");
            // Difficulty on this branch is meant to come from authored interactions, not distance: the
            // jumps stay comfortable and the branch must carry a real interaction inventory.
            Assert.That(hardest, Is.LessThanOrEqualTo(Phase01Branch1Envelope.ComfortCap + 0.01f),
                $"Branch 1 hardest gap is {hardest:P0} of reach; jumps must stay comfortable.");
            Phase01BranchTerrain branch1 = controller.BranchTerrains[0];
            Assert.That(branch1.GetComponentsInChildren<Phase01MovingPlatform>(true), Has.Length.EqualTo(2),
                "Branch 1 should carry a riser lift and a ferry.");
            Phase01Draught[] currents = branch1.GetComponentsInChildren<Phase01Draught>(true);
            Assert.That(currents, Has.Length.EqualTo(2),
                "Branch 1's headline mechanics are two air currents: the parkour is not only jumps.");
            Assert.That(
                currents.Count(current => current.Width > 10f),
                Is.EqualTo(2),
                "Both currents must cross a void no jump can cross.");
            Phase01SweeperGate[] beats = branch1.GetComponentsInChildren<Phase01SweeperGate>(true);
            Assert.That(beats, Has.Length.EqualTo(2),
                "Branch 1's difficulty comes from two authored interaction beats, not from jump distance.");
            Assert.That(beats.Count(beat => beat.Axis == Phase01SweeperGate.AxisVertical), Is.EqualTo(1),
                "Branch 1 authors one patrol door.");
            Assert.That(
                beats.Count(beat => beat.Axis == Phase01SweeperGate.AxisHorizontalAlongBranch),
                Is.EqualTo(1),
                "Branch 1 authors one sweeping bar.");
        }

        [UnityTest]
        public IEnumerator Branch1_PlatformVoidsAreCrossableOnlyByTheirAuthoredMechanics()
        {
            yield return LoadBranch();

            Vector2 riserVoid = Phase01BranchTerrain.Branch1PlatformVoids[0];
            Vector2 ferryVoid = Phase01BranchTerrain.Branch1PlatformVoids[1];

            Assert.That(UphillRise(riserVoid), Is.GreaterThan(player.JumpHeight),
                "The riser void must be taller than the jump apex so the lift is required.");
            Assert.That(terrain.IsGapAt((riserVoid.x + riserVoid.y) * 0.5f), Is.True);
            Assert.That(terrain.IsGapAt((ferryVoid.x + ferryVoid.y) * 0.5f), Is.True);

            float flatReach = Phase01Branch1Envelope.Reach(
                0f, player.MoveSpeed, player.JumpHeight, player.TimeToApex);
            Assert.That(ferryVoid.y - ferryVoid.x, Is.GreaterThan(flatReach * 2f),
                "The ferry void must be far wider than any jump so the platform is the only route.");

            // The air currents are voids in exactly the same sense, carrying the same requirement: a jump
            // cannot cross them, so the ride is the route and not a shortcut past one.
            Phase01BranchTerrain branch1 = controller.BranchTerrains[0];
            Phase01Draught[] currents = branch1.GetComponentsInChildren<Phase01Draught>(true);
            Assert.That(currents, Has.Length.GreaterThanOrEqualTo(2), "The currents must be authored.");
            foreach (Phase01Draught current in currents)
            {
                Assert.That(current.Width, Is.GreaterThan(flatReach * 2f),
                    $"Air current {current.name} spans {current.Width:F2} m, which a jump could cross.");
                Assert.That(terrain.IsGapAt(current.CentreScalar), Is.True,
                    $"Air current {current.name} must span an authored void.");
                // Symmetric by construction: the ride line sits above both lips, so the current carries the
                // player out and back rather than only out.
                float inner = terrain.SampleHeight(current.InnerScalar + 0.05f);
                float outer = terrain.SampleHeight(current.OuterScalar - 0.05f);
                Assert.That(current.RideHeight, Is.GreaterThan(Mathf.Max(inner, outer) + 0.1f),
                    $"Air current {current.name} must hold the player above both of its lips "
                    + $"(ride {current.RideHeight:F2} against lips {inner:F2}/{outer:F2}).");
            }
        }

        [UnityTest]
        public IEnumerator Branch1_ElevationReplacesTheLegacyFlatCeiling()
        {
            yield return LoadBranch();

            float[] authoredHeights = Phase01Branch1Envelope.AuthoredGapSpans()
                .SelectMany(span => new[] { terrain.SampleHeight(span.x + 0.05f), terrain.SampleHeight(span.y + 0.05f) })
                .ToArray();
            float summit = authoredHeights.Max();
            Assert.That(summit, Is.GreaterThanOrEqualTo(9f),
                "Branch 1 should climb to the authored +9 m summit terrace.");

            List<float> rises = Phase01Branch1Envelope.AuthoredGapSpans()
                .Where(span => !Phase01Branch1Envelope.IsPlatformVoid(span))
                .Select(UphillRise)
                .ToList();
            Assert.That(rises.Count(rise => rise >= 1f), Is.GreaterThanOrEqualTo(6),
                "At least six Branch 1 gaps should demand a full-height rise.");
            Assert.That(rises.Max(), Is.GreaterThanOrEqualTo(1.5f),
                "The legacy Branch 1 ceiling of 0.20 m of rise must be clearly exceeded.");
        }

        /// <summary>
        /// The interaction beats are the whole of Branch 1's difficulty, so their authored shape is worth
        /// pinning down: each one must block the ledge it stands on (otherwise it can simply be walked
        /// around), must patrol slower than the accepted player speed, and must re-arm before the player
        /// arrives so the outbound and homebound beats are identical.
        /// </summary>
        [UnityTest]
        public IEnumerator Branch1_InteractionBeatsBlockTheirLedgeAndStaySlowerThanThePlayer()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            Phase01BranchTerrain branch1 = controller.BranchTerrains[0];
            Phase01SweeperGate[] beats = branch1.GetComponentsInChildren<Phase01SweeperGate>(true);
            Assert.That(beats, Has.Length.EqualTo(2));

            foreach (Phase01SweeperGate beat in beats)
            {
                Assert.That(beat.TravelSpeed, Is.LessThan(player.MoveSpeed),
                    $"{beat.name} patrols at {beat.TravelSpeed:F2} m/s, which is not slower than the "
                    + $"{player.MoveSpeed:F2} m/s player; the beat would become a chase.");

                // The authored patrol must sit on solid ledge at both ends: a beat that overhangs a void
                // would stop being a timing read and become an unavoidable fall.
                Assert.That(branch1.IsGapAt(beat.PatrolInnerScalar), Is.False,
                    $"{beat.name}'s inner end at {beat.PatrolInnerScalar:F2} is over a void.");
                Assert.That(branch1.IsGapAt(beat.PatrolOuterScalar), Is.False,
                    $"{beat.name}'s outer end at {beat.PatrolOuterScalar:F2} is over a void.");

                // A bar narrower than the ledge it guards can simply be walked around.
                float barHalfWidth = Mathf.Abs(beat.transform.localScale.z) * 0.5f;
                float ledgeHalfWidth = branch1.SampleHalfWidth(beat.PatrolInnerScalar);
                Assert.That(barHalfWidth, Is.GreaterThanOrEqualTo(ledgeHalfWidth),
                    $"{beat.name} spans {barHalfWidth:F2} m of half-width against a {ledgeHalfWidth:F2} m "
                    + "ledge, so it could be walked around.");
            }

            Phase01SweeperGate door = beats.Single(beat => beat.Axis == Phase01SweeperGate.AxisVertical);
            Assert.That(door.OneWayDuration, Is.GreaterThan(0.5f),
                "The patrol door needs a readable cycle rather than a reflex test.");
            Assert.That(door.UpperHeight - door.LowerHeight, Is.GreaterThan(2f),
                "The patrol door must rise well clear of a walking player.");

            Phase01SweeperGate sweep = beats.Single(
                beat => beat.Axis == Phase01SweeperGate.AxisHorizontalAlongBranch);
            Assert.That(sweep.PatrolDistance, Is.GreaterThan(2f),
                "The sweeping bar should patrol a real stretch of the terrace.");
            Assert.That(sweep.PatrolOuterScalar - sweep.PatrolInnerScalar, Is.EqualTo(sweep.PatrolDistance),
                "The sweeping bar's patrol span should match the distance it actually travels.");
        }

        /// <summary>
        /// The beating heart of Branch 1's difficulty: the sweeping bar has to actually travel, and the
        /// bar has to actually touch the player. The second half is deliberately passive - the player
        /// stands still in the bar's path and lets it sweep through them - because that isolates "does the
        /// bar have any physical presence at all" from every other question about jumps, gaps and input.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Branch1_SweepingBarTravelsAndStopsAWalkingPlayer()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            Phase01SweeperGate sweep = controller.BranchTerrains[0]
                .GetComponentsInChildren<Phase01SweeperGate>(true)
                .Single(beat => beat.Axis == Phase01SweeperGate.AxisHorizontalAlongBranch);

            // Half one: the bar must genuinely travel along the terrace.
            float first = sweep.CurrentScalar;
            float furthest = first;
            float travelDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < travelDeadline)
            {
                furthest = Mathf.Max(furthest, sweep.CurrentScalar);
                yield return null;
            }
            Assert.That(furthest - first, Is.GreaterThan(1f),
                $"The sweeping bar must travel, but it only moved {(furthest - first):F2} m in three seconds.");

            // Half two: walking into the bar must stop the player dead. The bar may not carry them along
            // the ledge and may not squeeze them off it - both of which were exactly what the raw physics
            // contact did, and both of which read to a player as "the bar does nothing".
            float waitDeadline = Time.realtimeSinceStartup + 6f;
            while (sweep.CurrentScalar - sweep.PatrolInnerScalar > 0.4f
                && Time.realtimeSinceStartup < waitDeadline)
                yield return null;

            float probe = sweep.PatrolInnerScalar - 0.6f;
            player.SetPositionDuringTransition(controller.PointOnActiveLane(probe, 0f));
            for (int i = 0; i < 20 && !player.IsGrounded; i++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(player.IsGrounded, Is.True, "The probe placement must be grounded.");

            float before = player.CurrentScalar;
            float closestLead = float.MaxValue;
            float driveDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < driveDeadline)
            {
                player.SetSimulatedAxis(player.InputForScalarDirection(1f));
                closestLead = Mathf.Min(closestLead, sweep.CurrentScalar - player.CurrentScalar);
                yield return null;
            }
            player.SetSimulatedAxis(0f);

            float after = player.CurrentScalar;
            Debug.Log($"[DiagSweep] probe={probe:F2} before={before:F2} after={after:F2} "
                + $"bar={sweep.CurrentScalar:F2} closestLead={closestLead:F3} grounded={player.IsGrounded}");

            Assert.That(player.IsGrounded, Is.True,
                "The player must still be standing on the terrace, not squeezed off the ledge.");
            Assert.That(after - before, Is.GreaterThan(0.3f),
                $"The player should walk up to the bar (from {before:F2} to {after:F2}).");
            Assert.That(closestLead, Is.GreaterThan(-0.15f),
                $"The sweeping bar must stop the player. Closest approach {closestLead:F2} m means the "
                + "player walked straight through it.");
        }

        [UnityTest]
        public IEnumerator AuthoredRisersAndFerriesAreNotJumpableOnEveryBranch()
        {
            yield return LoadBranch();

            float flatReach = Phase01Branch1Envelope.Reach(
                0f, player.MoveSpeed, player.JumpHeight, player.TimeToApex);

            for (int branch = 0; branch <= 1; branch++)
            {
                Phase01BranchTerrain branchTerrain = controller.BranchTerrains[branch];
                Phase01MovingPlatform[] platforms =
                    branchTerrain.GetComponentsInChildren<Phase01MovingPlatform>(true);
                Assert.That(platforms.Length, Is.EqualTo(2),
                    $"Branch {branch + 1} should author exactly one riser lift and one ferry.");

                Phase01MovingPlatform riser = platforms.Single(platform => platform.name.Contains("Riser"));
                Phase01MovingPlatform ferry = platforms.Single(platform => platform.name.Contains("Ferry"));

                Vector3 riserOffset = PrivateVector3(riser, "localOffset");
                Vector3 ferryOffset = PrivateVector3(ferry, "localOffset");
                Assert.That(riserOffset.y, Is.GreaterThan(player.JumpHeight),
                    $"Branch {branch + 1} riser lifts {riserOffset.y:F2} m, which must exceed the "
                    + $"{player.JumpHeight:F2} m jump apex.");
                Assert.That(ferryOffset.magnitude, Is.GreaterThan(flatReach * 2f),
                    $"Branch {branch + 1} ferry travels {ferryOffset.magnitude:F2} m, which must exceed "
                    + $"twice the {flatReach:F2} m flat jump reach.");
                Assert.That(PrivateFloat(riser, "oneWayDuration"), Is.GreaterThan(0f));
                Assert.That(PrivateFloat(ferry, "oneWayDuration"), Is.GreaterThan(0f));
                // Fairness: the authored platform must stay slower than the player could run.
                Assert.That(ferryOffset.magnitude / PrivateFloat(ferry, "oneWayDuration"),
                    Is.LessThan(player.MoveSpeed),
                    $"Branch {branch + 1} ferry must not outrun the accepted player speed.");
            }
        }

        [UnityTest]
        public IEnumerator Branch1_EveryLandingHasAuthoredGeometryWiderThanThePlayer()
        {
            yield return LoadBranch();

            List<Vector2> spans = Phase01Branch1Envelope.AuthoredGapSpans();
            List<float> landings = new List<float> { spans[0].x - 1.5f };
            for (int i = 0; i + 1 < spans.Count; i++)
                landings.Add((spans[i].y + spans[i + 1].x) * 0.5f);
            landings.Add(spans[spans.Count - 1].y + 1.5f);

            foreach (float landing in landings)
            {
                Assert.That(
                    SurfaceAt(landing),
                    Is.True,
                    $"Branch 1 landing near {landing:F1} m has no authored geometry.");
            }

            // Half widths are authored data; assert the thinnest required beam is still landable
            // (the player capsule is 0.64 m wide).
            Assert.That(ThinnestAuthoredHalfWidth(), Is.GreaterThanOrEqualTo(0.42f),
                "Branch 1 beams must stay at or above the authored 0.84 m landing width.");
        }

        private IEnumerator LoadBranch()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;

            controller = Object.FindAnyObjectByType<Phase01DemoController>();
            player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            terrain = controller.BranchTerrains[0];
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.OuterScalar, Is.EqualTo(178f).Within(0.01f));
        }

        private float UphillRise(Vector2 span)
        {
            float inner = terrain.SampleHeight(span.x - 0.05f);
            float outer = terrain.SampleHeight(span.y + 0.05f);
            return Mathf.Abs(outer - inner);
        }

        private bool SurfaceAt(float scalar)
        {
            // Branch 1 climbs to +9 m, so probes must start relative to the authored surface at the
            // sample rather than at a fixed world height.
            float surface = terrain.SampleHeight(scalar);
            Vector3 origin = controller.RadioPosition
                + terrain.OutwardDirection * scalar
                + Vector3.up * (Mathf.Max(surface, -0.5f) + 2.5f);
            return Physics.Raycast(origin, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        }

        private float ThinnestAuthoredHalfWidth()
        {
            float thinnest = float.MaxValue;
            FieldInfo field = typeof(Phase01BranchTerrain).GetField(
                "Branch1Widths",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            float[] widths = (float[])field.GetValue(null);
            foreach (float width in widths)
                thinnest = Mathf.Min(thinnest, width);
            return thinnest;
        }

        private static float PrivateFloat(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
            return (float)field.GetValue(target);
        }

        private static Vector3 PrivateVector3(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} was not found on {target.GetType().Name}.");
            return (Vector3)field.GetValue(target);
        }

        private Phase01DemoController controller;
        private Phase01PlayerMotor player;
        private Phase01BranchTerrain terrain;
    }
}
