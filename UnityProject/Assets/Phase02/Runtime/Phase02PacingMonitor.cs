using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Phase02.VolcanoEscape
{
    public sealed class Phase02PacingMonitor : MonoBehaviour
    {
        private readonly float[] sectionStartedAt = new float[5];
        private readonly float[] sectionSplits = new float[5];
        private readonly float[] checkpointTimes = new float[4];
        private readonly int[] failuresBySection = new int[5];
        private float playableStartedAt;
        private bool running;
        private bool completed;

        public float ElapsedSeconds => running
            ? Time.realtimeSinceStartup - playableStartedAt
            : completed ? sectionSplits[0] + sectionSplits[1] + sectionSplits[2] + sectionSplits[3] + sectionSplits[4] : 0f;
        public IReadOnlyList<float> SectionSplits => sectionSplits;
        public IReadOnlyList<float> CheckpointTimes => checkpointTimes;
        public IReadOnlyList<int> FailuresBySection => failuresBySection;
        public float WhiteOpeningSightedAt { get; private set; } = -1f;
        public float WhiteOpeningApproachedAt { get; private set; } = -1f;
        public float FinalJumpStartedAt { get; private set; } = -1f;
        public float CompletedAt { get; private set; } = -1f;

        public void Begin()
        {
            playableStartedAt = Time.realtimeSinceStartup;
            sectionStartedAt[0] = playableStartedAt;
            running = true;
            completed = false;
            for (int i = 0; i < sectionSplits.Length; i++)
            {
                sectionSplits[i] = 0f;
                failuresBySection[i] = 0;
                if (i < checkpointTimes.Length)
                    checkpointTimes[i] = -1f;
            }
            WhiteOpeningSightedAt = -1f;
            WhiteOpeningApproachedAt = -1f;
            FinalJumpStartedAt = -1f;
            CompletedAt = -1f;
        }

        public void MarkSectionChanged(int previousSection, int nextSection)
        {
            if (!running)
                return;

            float now = Time.realtimeSinceStartup;
            if (previousSection >= 0 && previousSection < sectionSplits.Length)
                sectionSplits[previousSection] = now - sectionStartedAt[previousSection];
            if (nextSection >= 0 && nextSection < sectionStartedAt.Length)
                sectionStartedAt[nextSection] = now;
        }

        public void MarkCheckpoint(int checkpointIndex)
        {
            if (!running || checkpointIndex < 0 || checkpointIndex >= checkpointTimes.Length)
                return;
            checkpointTimes[checkpointIndex] = Time.realtimeSinceStartup - playableStartedAt;
        }

        public void MarkFailure(int sectionIndex)
        {
            if (sectionIndex >= 0 && sectionIndex < failuresBySection.Length)
                failuresBySection[sectionIndex]++;
        }

        public void MarkWhiteOpeningSighted()
        {
            if (WhiteOpeningSightedAt < 0f)
                WhiteOpeningSightedAt = Time.realtimeSinceStartup - playableStartedAt;
        }

        public void MarkWhiteOpeningApproached()
        {
            if (WhiteOpeningApproachedAt < 0f)
                WhiteOpeningApproachedAt = Time.realtimeSinceStartup - playableStartedAt;
        }

        public void MarkFinalJumpStarted()
        {
            if (FinalJumpStartedAt < 0f)
                FinalJumpStartedAt = Time.realtimeSinceStartup - playableStartedAt;
        }

        public void MarkCompleted(int finalSection)
        {
            if (!running || completed)
                return;

            float now = Time.realtimeSinceStartup;
            if (finalSection >= 0 && finalSection < sectionSplits.Length)
                sectionSplits[finalSection] = now - sectionStartedAt[finalSection];
            CompletedAt = now - playableStartedAt;
            running = false;
            completed = true;
            Debug.Log(BuildSummary());
        }

        public string BuildSummary()
        {
            StringBuilder builder = new StringBuilder("[Phase02Pacing]");
            for (int i = 0; i < sectionSplits.Length; i++)
                builder.Append($" S{i + 1}={sectionSplits[i]:F1}s");
            builder.Append($" total={(CompletedAt >= 0f ? CompletedAt : ElapsedSeconds):F1}s");
            builder.Append(" failures=");
            for (int i = 0; i < failuresBySection.Length; i++)
            {
                if (i > 0)
                    builder.Append('/');
                builder.Append(failuresBySection[i]);
            }
            return builder.ToString();
        }
    }
}
