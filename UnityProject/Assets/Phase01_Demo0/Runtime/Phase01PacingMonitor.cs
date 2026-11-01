using System;
using System.Linq;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01PacingMonitor : MonoBehaviour
    {
        [SerializeField] private Phase01DemoController controller;

        private readonly float[] branchPlayableTimes = { -1f, -1f, -1f, -1f };
        private readonly float[] pickupTimes = { -1f, -1f, -1f, -1f };
        private readonly float[] depositTimes = { -1f, -1f, -1f, -1f };
        private readonly float[] branchSplits = { -1f, -1f, -1f, -1f };
        // Falls per branch, and how many branches were crossed without one. This is the mastery readout:
        // the route can always be finished by falling and retrying (see the strategy note), and this records
        // whether it was finished *cleanly*, without ever gating anything on it.
        private readonly int[] branchFallBaseline = { -1, -1, -1, -1 };
        private readonly int[] branchFalls = { 0, 0, 0, 0 };
        private Phase01PlayerMotor player;
        private int cleanBranchCount;
        private float phaseStartedAt;
        private bool subscribed;
        private bool completionRecorded;

        public int PhaseEntryCount { get; private set; }
        public int PickupRecordCount => pickupTimes.Count(value => value >= 0f);
        public int DepositRecordCount => depositTimes.Count(value => value >= 0f);
        public int CompletedSplitCount => branchSplits.Count(value => value >= 0f);
        public int FinalCompletionCount => completionRecorded ? 1 : 0;
        public int TotalFalls { get; private set; }
        public int CleanBranchCount => cleanBranchCount;
        public bool CleanRunCompleted => completionRecorded && cleanBranchCount == branchFalls.Length;
        public int GetBranchFalls(int branchIndex) => branchFalls[branchIndex];
        public string CleanRunSummary => string.Join(
            " / ",
            branchFalls.Select((falls, index) =>
                branchPlayableTimes[index] < 0f ? "--" : (falls == 0 ? "clean" : $"{falls} fall{(falls == 1 ? "" : "s")}")));

        public float CurrentTotalSeconds => PhaseEntryCount == 0
            ? 0f
            : (completionRecorded ? FinalTotalSeconds : Time.realtimeSinceStartup - phaseStartedAt);
        public float FinalTotalSeconds { get; private set; }

        public void Configure(Phase01DemoController demoController)
        {
            Unsubscribe();
            controller = demoController;
            player = demoController != null ? demoController.PlayerMotor : null;
            Subscribe();
        }

        private Phase01PlayerMotor ResolvePlayer()
        {
            if (player == null && controller != null)
                player = controller.PlayerMotor;
            if (player == null)
                player = UnityEngine.Object.FindAnyObjectByType<Phase01PlayerMotor>();
            return player;
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Start()
        {
            if (PhaseEntryCount != 0)
                return;
            phaseStartedAt = Time.realtimeSinceStartup;
            PhaseEntryCount = 1;
        }

        private void Update()
        {
            if (controller == null || PhaseEntryCount == 0)
                return;

            int activeBranch = controller.ActiveBranchIndex;
            if (controller.CanAcceptInput
                && activeBranch >= 0
                && activeBranch < branchPlayableTimes.Length
                && branchPlayableTimes[activeBranch] < 0f)
            {
                float now = Elapsed;
                branchPlayableTimes[activeBranch] = now;
                Phase01PlayerMotor motor = ResolvePlayer();
                branchFallBaseline[activeBranch] = motor != null ? motor.SoftResetCount : 0;
                if (activeBranch > 0 && branchSplits[activeBranch - 1] < 0f)
                    branchSplits[activeBranch - 1] = now - branchPlayableTimes[activeBranch - 1];
            }

            if (!controller.DemoComplete || completionRecorded)
                return;
            float completedAt = Elapsed;
            if (branchPlayableTimes[3] >= 0f)
                branchSplits[3] = completedAt - branchPlayableTimes[3];
            FinalTotalSeconds = completedAt;
            completionRecorded = true;
        }

        public float GetBranchPlayableTime(int branchIndex) => branchPlayableTimes[branchIndex];
        public float GetPickupTime(int branchIndex) => pickupTimes[branchIndex];
        public float GetDepositTime(int branchIndex) => depositTimes[branchIndex];
        public float GetBranchSplit(int branchIndex) => branchSplits[branchIndex];

        public string SplitSummary => string.Join(" / ", branchSplits.Select(value => value < 0f ? "--" : $"{value:F1}s"));

        private float Elapsed => Time.realtimeSinceStartup - phaseStartedAt;

        private void Subscribe()
        {
            if (subscribed || controller == null)
                return;
            controller.ParticlePickedUp += OnParticlePickedUp;
            controller.ParticleDeposited += OnParticleDeposited;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || controller == null)
                return;
            controller.ParticlePickedUp -= OnParticlePickedUp;
            controller.ParticleDeposited -= OnParticleDeposited;
            subscribed = false;
        }

        private void OnParticlePickedUp(int branchIndex)
        {
            if (branchIndex >= 0 && branchIndex < pickupTimes.Length && pickupTimes[branchIndex] < 0f)
                pickupTimes[branchIndex] = Elapsed;
        }

        private void OnParticleDeposited(int branchIndex)
        {
            if (branchIndex < 0 || branchIndex >= depositTimes.Length)
                return;

            Phase01PlayerMotor motor = ResolvePlayer();
            if (motor != null && branchFallBaseline[branchIndex] >= 0 && branchFalls[branchIndex] == 0)
            {
                int falls = Mathf.Max(0, motor.SoftResetCount - branchFallBaseline[branchIndex]);
                branchFalls[branchIndex] = falls;
                TotalFalls += falls;
                if (falls == 0)
                    cleanBranchCount++;
            }

            if (depositTimes[branchIndex] < 0f)
                depositTimes[branchIndex] = Elapsed;
        }
    }
}
