using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Phase01.Demo0.Tests
{
    /// <summary>
    /// Guards Phase 01's contact hazard. It is the Hollow Knight difficulty axis: the floor itself punishes a
    /// failed jump, so a stretch has to be committed to rather than shuffled through. Contact must use the
    /// existing soft reset - no health, no damage, no new failure state - and it must be safe to clear.
    /// </summary>
    public sealed class Phase01SpikeHazardPlayModeTests
    {
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ContactResetsThroughTheExistingSoftResetAndClearingIsSafe()
        {
            SceneManager.LoadScene("Phase01_Demo0", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01PlayerMotor player = Object.FindAnyObjectByType<Phase01PlayerMotor>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(player, Is.Not.Null);

            // Built here rather than authored in the level. See the thorn-lip note in the scene builder: the
            // fair construction is a band on a gap lip, and the blocker is the acceptance driver's jump
            // cadence rather than the level, so the capability is verified without shipping the beat.
            Phase01BranchTerrain terrain = controller.BranchTerrains[0];
            float lip = 26.5f;
            const float span = 0.55f;
            Assert.That(terrain.IsGapAt(lip + 0.06f), Is.True, "The probe band must sit on a gap lip.");

            GameObject bandObject = new GameObject("Test Thorn Band");
            bandObject.transform.SetParent(terrain.transform, false);
            Phase01SpikeStrip strip = bandObject.AddComponent<Phase01SpikeStrip>();
            strip.Configure(0, lip - span, lip, 0.44f, controller, player);

            float middle = lip - span * 0.5f;
            float surface = controller.ActiveSurfaceHeight(middle);
            Assert.That(strip.Span, Is.GreaterThan(0.3f), "A thorn band must be wide enough to be jumped.");
            Assert.That(strip.WouldStrike(middle, surface), Is.True,
                "Standing in the thorns must count as contact.");
            Assert.That(strip.WouldStrike(middle, surface + strip.SpikeHeight + 1.2f), Is.False,
                "Passing well above the thorns must not count as contact, or they could not be cleared.");

            // Contact: stand in the thorns and the existing soft reset must take the player off them.
            int resetsBefore = player.SoftResetCount;
            player.SetPositionDuringTransition(controller.PointOnActiveLane(middle, 0f));
            for (int i = 0; i < 10; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(strip.ContactCount, Is.GreaterThanOrEqualTo(1),
                "Standing in the thorns must register contact.");
            Assert.That(player.SoftResetCount, Is.GreaterThan(resetsBefore),
                "Contact must use the existing soft reset rather than a new failure state.");
            Assert.That(Mathf.Abs(player.CurrentScalar - middle), Is.GreaterThan(0.2f),
                "The reset must move the player out of the thorns.");

            Debug.Log($"[DiagSpikes] span={strip.Span:F2} contacts={strip.ContactCount} "
                + $"softResets={player.SoftResetCount} landedAt={player.CurrentScalar:F2}");
        }
    }
}
