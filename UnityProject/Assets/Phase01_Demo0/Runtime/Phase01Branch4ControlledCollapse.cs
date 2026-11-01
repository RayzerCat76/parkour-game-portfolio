using System;
using System.Linq;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01Branch4ControlledCollapse : MonoBehaviour
    {
        [SerializeField] private Phase01CollapsibleSegment[] segments;
        [SerializeField] private GameObject[] outboundGapFillers;
        [SerializeField] private float finalApproachInnerScalar = 1.4f;
        [SerializeField] private float finalApproachOuterScalar = 7f;
        [SerializeField] private float outerApproachInnerScalar = 24f;

        private int nextCollapseOrder;
        private Phase01PlayerMotor player;
        private int observedSoftResetCount;
        private bool hasObservedSoftResetCount;

        public event Action Activated;

        public Phase01CollapsibleSegment[] Segments => segments;
        public int SegmentCount => segments != null ? segments.Length : 0;
        public int ActivationCount { get; private set; }
        public int OutboundGapFillerDisableCount { get; private set; }
        public bool IsActivated { get; private set; }
        public bool AreOutboundGapFillersEnabled => outboundGapFillers != null
            && outboundGapFillers.All(filler => filler != null
                && filler.GetComponent<Collider>() != null
                && filler.GetComponent<Collider>().enabled);
        public int OutboundGapFillerCount => outboundGapFillers != null ? outboundGapFillers.Length : 0;
        public int NextCollapseOrder => nextCollapseOrder;
        public float FinalApproachInnerScalar => finalApproachInnerScalar;
        public float FinalApproachOuterScalar => finalApproachOuterScalar;
        public float OuterApproachInnerScalar => outerApproachInnerScalar;

        public void Configure(
            Phase01CollapsibleSegment[] orderedSegments,
            GameObject[] gapFillers,
            float finalInner,
            float finalOuter,
            float outerInner)
        {
            segments = orderedSegments.OrderBy(segment => segment.CollapseOrder).ToArray();
            outboundGapFillers = gapFillers;
            finalApproachInnerScalar = finalInner;
            finalApproachOuterScalar = finalOuter;
            outerApproachInnerScalar = outerInner;
            ArmSegments();
        }

        private void Awake()
        {
            if (segments == null || segments.Length == 0)
                segments = GetComponentsInChildren<Phase01CollapsibleSegment>(true)
                    .OrderBy(segment => segment.CollapseOrder)
                    .ToArray();
            ArmSegments();
        }

        private void FixedUpdate()
        {
            player ??= FindAnyObjectByType<Phase01PlayerMotor>();
            if (player == null)
                return;
            if (!hasObservedSoftResetCount)
            {
                observedSoftResetCount = player.SoftResetCount;
                hasObservedSoftResetCount = true;
                return;
            }
            if (player.SoftResetCount == observedSoftResetCount)
                return;
            observedSoftResetCount = player.SoftResetCount;
            if (IsActivated)
                RestorePlayableReturnState();
        }

        private void ArmSegments()
        {
            if (segments == null)
                return;
            foreach (Phase01CollapsibleSegment segment in segments)
                segment?.Arm(this);
        }

        public bool Activate()
        {
            if (IsActivated)
                return false;
            IsActivated = true;
            ActivationCount++;
            nextCollapseOrder = 0;
            SetOutboundGapFillersEnabled(false);
            OutboundGapFillerDisableCount++;
            ObservePlayerResetCount();
            Activated?.Invoke();
            return true;
        }

        private void ObservePlayerResetCount()
        {
            player ??= FindAnyObjectByType<Phase01PlayerMotor>();
            if (player == null)
                return;
            observedSoftResetCount = player.SoftResetCount;
            hasObservedSoftResetCount = true;
        }

        private void SetOutboundGapFillersEnabled(bool enabled)
        {
            if (outboundGapFillers == null)
                return;
            foreach (GameObject filler in outboundGapFillers)
            {
                if (filler == null)
                    continue;
                Collider collider = filler.GetComponent<Collider>();
                Renderer renderer = filler.GetComponent<Renderer>();
                if (collider != null)
                    collider.enabled = enabled;
                if (renderer != null)
                    renderer.enabled = enabled;
            }
        }

        public void RestorePlayableReturnState()
        {
            if (!IsActivated)
                return;
            nextCollapseOrder = 0;
            SetOutboundGapFillersEnabled(false);
            foreach (Phase01CollapsibleSegment segment in segments)
                segment?.RestoreArmed();
        }

        public bool TryBeginSegment(int order)
        {
            if (!IsActivated || order != nextCollapseOrder || order < 0 || order >= SegmentCount)
                return false;
            nextCollapseOrder++;
            return true;
        }

        public void SetPresentationVisible(bool visible)
        {
            if (segments == null)
                return;
            foreach (Phase01CollapsibleSegment segment in segments)
                segment?.SetPresentationVisible(visible);
            if (outboundGapFillers == null || IsActivated)
                return;
            foreach (GameObject filler in outboundGapFillers)
            {
                Renderer renderer = filler != null ? filler.GetComponent<Renderer>() : null;
                if (renderer != null)
                    renderer.enabled = visible;
            }
        }
    }
}
