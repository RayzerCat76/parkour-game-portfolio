using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phase01.MovementSandbox;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// The parkour strategy, written as invariants. These are the properties the level's difficulty is
    /// allowed to come from, checked across all four branches rather than on whichever branch was authored
    /// last. See Design/Phase01_Parkour_Strategy.md for the reading behind them.
    ///
    /// What popular parkour/platformers actually do, and what is encoded here:
    ///  - Celeste ("simply jump, air-dash, and climb - but with layers of expressive depth", "lightning-fast
    ///    respawns", 700+ screens of small stages, B-sides as optional harder variants): difficulty is a
    ///    VERB problem and a retry problem, not a distance problem, and harder variants repeat the same
    ///    mechanic tighter instead of inventing a new one.
    ///  - Super Meat Boy (300+ levels, streamlined twitch platforming): tiny stages, instant retry, ramp by
    ///    combination.
    ///  - Mirror's Edge (Runner Vision: "see the flow"): the route has to be readable from a distance.
    ///  - ClusterTruck (the floor moves, "dangers are added"): escalate by adding hazards to a verb the
    ///    player already owns.
    ///
    /// In this level that becomes four invariants:
    ///  1. Every jump stays inside the comfort envelope, on every branch - no difficulty from distance.
    ///  2. A mechanic is never introduced at its tightest: the first instance of a verb is its most
    ///     forgiving one.
    ///  3. Failure is cheap and safe: every lip is standable and no void can be mistaken for ground.
    ///  4. The route is readable: every authored air current is visible before the player commits to it.
    /// </summary>
    public sealed class Phase01ParkourStrategyPlayModeTests
    {
        private Phase01DemoController controller;
        private Phase01PlayerMotor player;

        private IEnumerator LoadLevel()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;
            controller = Object.FindAnyObjectByType<Phase01DemoController>();
            player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
        }

        private float UphillRise(int branch, Vector2 span)
        {
            Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
            float inner = terrain.SampleHeight(span.x - 0.05f);
            float outer = terrain.SampleHeight(span.y + 0.05f);
            return Mathf.Abs(outer - inner);
        }

        /// <summary>
        /// Invariant 1: no branch buys difficulty with distance. Every authored gap is inside the same
        /// comfort envelope the baseline branch is held to, so the later branches have to be harder in the
        /// way the earlier ones are: by what surrounds the jump, not by how far it is.
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator EveryBranchKeepsItsJumpsInsideTheComfortEnvelope()
        {
            yield return LoadLevel();

            var report = new List<string>();
            for (int branch = 0; branch < 4; branch++)
            {
                int jumps = 0;
                float worstRatio = 0f;
                foreach (Vector2 span in Phase01Branch1Envelope.AuthoredGapSpans(branch))
                {
                    if (Phase01Branch1Envelope.IsPlatformVoid(branch, span))
                        continue;

                    float rise = UphillRise(branch, span);
                    float length = span.y - span.x;
                    Assert.That(rise, Is.LessThanOrEqualTo(1.75f + 0.01f),
                        $"Branch {branch + 1} gap {span} rises {rise:F2} m: that is a climb, not a jump, and it "
                        + "cannot be taken back on the return trip.");
                    float reach = Phase01Branch1Envelope.Reach(
                        rise, player.MoveSpeed, player.JumpHeight, player.TimeToApex);
                    Assert.That(float.IsNaN(reach), Is.False,
                        $"Branch {branch + 1} gap {span} is above the jump apex.");
                    worstRatio = Mathf.Max(worstRatio, length / reach);
                    jumps++;
                }

                report.Add($"branch {branch + 1}: {jumps} jumps, worst {worstRatio:P0} of reach");
                Assert.That(worstRatio, Is.LessThanOrEqualTo(
                        Phase01Branch1Envelope.ComfortCap + 0.02f),
                    $"Branch {branch + 1} uses {worstRatio:P0} of a jump's reach; jumps must stay comfortable "
                    + "on every branch, with difficulty coming from the mechanics around them.");
            }

            Debug.Log($"[DiagStrategy] {string.Join(" | ", report)}");
            // Every branch has to carry mechanics, not only gaps: that is the strategy, and it is checked
            // per branch so a future pass cannot quietly turn one arm back into a jump ladder.
            for (int branch = 0; branch < 4; branch++)
            {
                Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
                int movingPlatforms = terrain.GetComponentsInChildren<Phase01MovingPlatform>(true).Length;
                int gates = terrain.GetComponentsInChildren<Phase01SweeperGate>(true).Length;
                int currents = terrain.GetComponentsInChildren<Phase01Draught>(true).Length;
                int fading = terrain.GetComponentsInChildren<Phase01FadingSupport>(true).Length;
                int collapse = terrain.GetComponentsInChildren<Phase01CollapsibleSegment>(true).Length;
                int mechanics = movingPlatforms + gates + currents + fading + collapse;
                Assert.That(mechanics, Is.GreaterThanOrEqualTo(2),
                    $"Branch {branch + 1} carries {mechanics} authored mechanics; each branch is supposed to "
                    + "be a set of beats rather than a ladder of jumps.");
            }
        }

        /// <summary>
        /// Invariant 2: teach before tightening. A player meets each verb at its most forgiving first -
        /// Celeste's A-side before its B-side - so the first air current on a branch must be at least as
        /// wide, as slow and as generous in its gust window as any later one on the same branch.
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator AMechanicIsTaughtBeforeItIsTightened()
        {
            yield return LoadLevel();

            for (int branch = 0; branch < 4; branch++)
            {
                Phase01Draught[] currents = controller.BranchTerrains[branch]
                    .GetComponentsInChildren<Phase01Draught>(true)
                    .OrderBy(current => current.InnerScalar)
                    .ToArray();
                if (currents.Length < 2)
                    continue;

                Phase01Draught first = currents[0];
                foreach (Phase01Draught later in currents.Skip(1))
                {
                    Assert.That(first.Width, Is.GreaterThanOrEqualTo(later.Width - 0.01f),
                        $"Branch {branch + 1}: '{first.name}' is the first current a player meets, so it must "
                        + $"not be narrower than '{later.name}' ({first.Width:F1} m vs {later.Width:F1} m).");
                    Assert.That(first.AlongSpeed, Is.LessThanOrEqualTo(later.AlongSpeed + 0.01f),
                        $"Branch {branch + 1}: the first current must not carry the player faster than the "
                        + $"later one ('{first.name}' {first.AlongSpeed:F1} m/s vs '{later.name}' "
                        + $"{later.AlongSpeed:F1} m/s).");
                }
            }

            // The baseline branch teaches the verb with a steady, wide current and never pulses it; that is
            // the difference between the teaching instance and the escalation.
            Phase01Draught[] branchOne = controller.BranchTerrains[0]
                .GetComponentsInChildren<Phase01Draught>(true);
            Assert.That(branchOne.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(branchOne.All(current => current.IsBlowing),
                "Branch 1 introduces the currents steadily; a gust window is an escalation, not a tutorial.");
        }

        /// <summary>
        /// Invariant 3: failure is cheap and safe. Every authored lip has ground to stand on, and no authored
        /// void can be sampled as if it were ground - a fall onto a "checkpoint" inside a hole is exactly the
        /// kind of unfair retry the strategy forbids.
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator EveryLipIsStandableAndNoVoidLooksLikeGround()
        {
            yield return LoadLevel();

            for (int branch = 0; branch < 4; branch++)
            {
                Phase01BranchTerrain terrain = controller.BranchTerrains[branch];
                foreach (float landing in Phase01Branch1Envelope.AuthoredStandingSamples(branch))
                {
                    Assert.That(terrain.IsGapAt(landing), Is.False,
                        $"Branch {branch + 1} landing {landing:F2} is authored over a void.");
                    float surface = terrain.SampleHeight(landing);
                    Assert.That(surface, Is.GreaterThan(-3f),
                        $"Branch {branch + 1} landing {landing:F2} has no ground (sampled {surface:F2}).");
                }

                foreach (Vector2 void_ in PlatformVoids(branch))
                {
                    float middle = (void_.x + void_.y) * 0.5f;
                    Assert.That(terrain.SampleHeight(middle), Is.LessThan(-3f),
                        $"Branch {branch + 1} void {void_} samples as ground ({terrain.SampleHeight(middle):F2}); "
                        + "a fall there would be treated as a safe place to stand.");
                    // Both lips of a void are where a failed attempt puts the player back: they have to be
                    // real ground, so a miss costs a step back and not a fall into nothing.
                    Assert.That(terrain.SampleHeight(void_.x - 0.6f), Is.GreaterThan(-3f),
                        $"Branch {branch + 1} inner lip of {void_} has no ground to retry from.");
                    Assert.That(terrain.SampleHeight(void_.y + 0.6f), Is.GreaterThan(-3f),
                        $"Branch {branch + 1} outer lip of {void_} has no ground to retry from.");
                }
            }
        }

        /// <summary>
        /// Invariant 4: the route is readable. Mirror's Edge puts Runner Vision on the critical path; here
        /// every authored air current - the one mechanic that is invisible until it moves you - has to be
        /// drawn in the world before the player steps into it.
        /// </summary>
        [UnityTest]
        [Timeout(240000)]
        public IEnumerator EveryAirCurrentIsVisibleBeforeItIsEntered()
        {
            yield return LoadLevel();

            Phase01Draught[] currents = Object.FindObjectsByType<Phase01Draught>(FindObjectsSortMode.None);
            Assert.That(currents.Length, Is.GreaterThanOrEqualTo(2), "The currents are the headline mechanic.");
            foreach (Phase01Draught current in currents)
            {
                Renderer[] renders = current.GetComponentsInChildren<Renderer>(true);
                Assert.That(renders.Length, Is.GreaterThanOrEqualTo(8),
                    $"Air current '{current.name}' carries {renders.Length} pieces of readable geometry; the "
                    + "player has to be able to see the current from the lip.");
                Assert.That(
                    renders.Any(renderer => renderer.transform.position.y > current.RideHeight),
                    Is.True,
                    $"Air current '{current.name}' is drawn only below its ride line, so a player standing on "
                    + "the lip cannot see it.");
            }
        }

        private Vector2[] PlatformVoids(int branch)
        {
            return branch == 0
                ? Phase01BranchTerrain.Branch1PlatformVoids
                : branch == 1
                    ? Phase01BranchTerrain.Branch2PlatformVoids
                    : branch == 2
                        ? Phase01BranchTerrain.Branch3PlatformVoids
                        : Phase01BranchTerrain.Branch4PlatformVoids;
        }
    }
}
