using UnityEngine;

namespace ProjectReturn.Core
{
    public sealed class VisualStateController : MonoBehaviour
    {
        public ProjectPhase LastRequestedPhase { get; private set; } = ProjectPhase.Boot;
        public string LastPaletteState { get; private set; } = string.Empty;
        public float LastFadeDuration { get; private set; }
        public bool LastFadeToBlack { get; private set; }
        public int RequestCount { get; private set; }

        public void RequestPhaseState(ProjectPhase phase)
        {
            LastRequestedPhase = phase;
            RequestCount++;
        }

        public void RequestPalette(string paletteState)
        {
            LastPaletteState = paletteState ?? string.Empty;
            RequestCount++;
        }

        public void RequestFade(float duration, bool fadeToBlack)
        {
            LastFadeDuration = Mathf.Max(0f, duration);
            LastFadeToBlack = fadeToBlack;
            RequestCount++;
        }
    }
}
