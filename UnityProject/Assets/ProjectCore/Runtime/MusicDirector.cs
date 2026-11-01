using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectReturn.Core
{
    public sealed class MusicDirector : MonoBehaviour
    {
        public event Action<ProjectPhase> PhaseEntered;

        public ProjectPhase LastEnteredPhase { get; private set; } = ProjectPhase.Boot;
        public int PhaseEntryRequestCount { get; private set; }
        public string LastEventName { get; private set; } = string.Empty;
        public string LastEventDetail { get; private set; } = string.Empty;
        public int LastBranchIndex { get; private set; } = -1;
        public int RecordedEventCount { get; private set; }
        public float Phase2Progress { get; private set; }
        public float Phase2DangerIntensity { get; private set; }
        public float Phase2CollapseProximity { get; private set; }
        public float Phase2HazeDensity { get; private set; }
        public float Phase2FailurePressure { get; private set; }

        private readonly Dictionary<string, int> eventCounts = new Dictionary<string, int>();

        public void NotifyPhaseEntered(ProjectPhase phase)
        {
            LastEnteredPhase = phase;
            PhaseEntryRequestCount++;
            Record("PhaseEntered", -1);
            PhaseEntered?.Invoke(phase);
        }

        public void NotifyParticlePickedUp(int branchIndex) => Record("ParticlePickedUp", branchIndex);
        public void NotifyParticleDepositStarted(int branchIndex) => Record("ParticleDepositStarted", branchIndex);
        public void NotifyParticleDeposited(int branchIndex) => Record("ParticleDeposited", branchIndex);
        public void NotifyViewRotationStarted(int branchIndex) => Record("ViewRotationStarted", branchIndex);
        public void NotifyViewRotationCompleted(int branchIndex) => Record("ViewRotationCompleted", branchIndex);
        public void NotifyActiveBranchChanged(int branchIndex) => Record("ActiveBranchChanged", branchIndex);
        public void NotifyReceiverCompleted() => Record("ReceiverCompleted", -1);
        public void NotifyBranch2RouteTransformationStarted() => Record("Branch2RouteTransformationStarted", -1);
        public void NotifyBranch3FadingSupportActivated(int supportIndex) => Record("Branch3FadingSupportActivated", supportIndex);
        public void NotifyBranch4ControlledCollapseActivated() => Record("Branch4ControlledCollapseActivated", -1);
        public void NotifyPhase01CollapseStarted() => NotifyBranch2RouteTransformationStarted();
        public void NotifyPhase01Completed() => Record("Phase01Completed", -1);

        public void NotifyPhase2Entered() => Record("Phase2Entered", -1);
        public void NotifyPhase2RuptureStarted() => Record("Phase2RuptureStarted", -1);
        public void NotifyPhase2SectionChanged(int sectionIndex) => Record("Phase2SectionChanged", sectionIndex);
        public void NotifyPhase2HazardWarning(string hazardType) => Record("Phase2HazardWarning", -1, hazardType);
        public void NotifyPhase2HazardTriggered(string hazardType) => Record("Phase2HazardTriggered", -1, hazardType);
        public void NotifyPhase2CollapseFrontStarted() => Record("Phase2CollapseFrontStarted", -1);
        public void NotifyPhase2CheckpointReached(int checkpointIndex) => Record("Phase2CheckpointReached", checkpointIndex);
        public void NotifyPhase2PlayerFailed(int sectionIndex) => Record("Phase2PlayerFailed", sectionIndex);
        public void NotifyPhase2PlayerReset(int sectionIndex) => Record("Phase2PlayerReset", sectionIndex);
        public void NotifyPhase2WhiteOpeningSighted() => Record("Phase2WhiteOpeningSighted", -1);
        public void NotifyPhase2WhiteOpeningApproached() => Record("Phase2WhiteOpeningApproached", -1);
        public void NotifyPhase2FinalJumpStarted() => Record("Phase2FinalJumpStarted", -1);
        public void NotifyPhase2Completed() => Record("Phase2Completed", -1);

        public void SetPhase2Parameters(
            float phaseProgress,
            float dangerIntensity,
            float collapseProximity,
            float hazeDensity,
            float failurePressure)
        {
            Phase2Progress = Mathf.Clamp01(phaseProgress);
            Phase2DangerIntensity = Mathf.Clamp01(dangerIntensity);
            Phase2CollapseProximity = Mathf.Clamp01(collapseProximity);
            Phase2HazeDensity = Mathf.Clamp01(hazeDensity);
            Phase2FailurePressure = Mathf.Clamp01(failurePressure);
        }

        public int GetEventCount(string eventName)
        {
            return eventName != null && eventCounts.TryGetValue(eventName, out int count)
                ? count
                : 0;
        }

        public void ResetRecordedEventsForTests()
        {
            eventCounts.Clear();
            RecordedEventCount = 0;
            LastEventName = string.Empty;
            LastEventDetail = string.Empty;
            LastBranchIndex = -1;
            Phase2Progress = 0f;
            Phase2DangerIntensity = 0f;
            Phase2CollapseProximity = 0f;
            Phase2HazeDensity = 0f;
            Phase2FailurePressure = 0f;
        }

        private void Record(string eventName, int branchIndex, string detail = "")
        {
            LastEventName = eventName;
            LastEventDetail = detail ?? string.Empty;
            LastBranchIndex = branchIndex;
            RecordedEventCount++;
            eventCounts[eventName] = GetEventCount(eventName) + 1;
        }
    }
}
