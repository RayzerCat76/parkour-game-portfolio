using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01DebugOverlay : MonoBehaviour
    {
        private const string ControlsCopy = "Move: A / D or ← / →     Jump: Space     Interact: E";

        [SerializeField] private Phase01DemoController controller;
        [SerializeField] private Phase01CameraRig cameraRig;
        [SerializeField] private Phase01PerformanceMonitor performanceMonitor;
        [SerializeField] private Phase01PacingMonitor pacingMonitor;

        private GUIStyle objectiveStyle;
        private GUIStyle hudLabelStyle;
        private GUIStyle promptStyle;
        private GUIStyle controlsStyle;
        private GUIStyle diagnosticStyle;
        private GUIStyle completeStyle;

        public string ObjectiveText { get; private set; } = "Reach the yellow particle";
        public string BranchProgressText { get; private set; } = "Branch 1 / 4";
        public string ReceiverProgressText { get; private set; } = "Receiver charge: 0 / 4";
        public string CarryStatusText { get; private set; } = "Particle: not carried";
        public string ContextPromptText { get; private set; } = string.Empty;
        public string ControlsText => ControlsCopy;
        public bool ControlsVisible { get; private set; } = true;
        public bool DepositPromptVisible { get; private set; }
        public bool TransitionMessageVisible { get; private set; }
        public bool CompletionBannerVisible { get; private set; }
        public bool ProductionHudAvailable => controller != null;
        public bool DebugDiagnosticsEnabled => Debug.isDebugBuild || Application.isEditor;

        public void Configure(
            Phase01DemoController demoController,
            Phase01CameraRig rig,
            Phase01PerformanceMonitor monitor,
            Phase01PacingMonitor pacing)
        {
            controller = demoController;
            cameraRig = rig;
            performanceMonitor = monitor;
            pacingMonitor = pacing;
            RefreshHudState();
        }

        public void RefreshHudState()
        {
            if (controller == null)
                return;

            BranchProgressText = $"Branch {controller.ActiveBranchNumber} / 4";
            ReceiverProgressText = $"Receiver charge: {controller.ReceiverCharge} / 4";
            bool carrying = controller.CarriedParticle != null;
            CarryStatusText = carrying
                ? $"Particle: carrying Branch {controller.CarriedParticle.BranchIndex + 1}"
                : "Particle: not carried";

            CompletionBannerVisible = controller.DemoComplete;
            DepositPromptVisible = false;
            TransitionMessageVisible = false;
            ControlsVisible = !controller.IsInputLocked && !CompletionBannerVisible;
            ContextPromptText = string.Empty;

            if (CompletionBannerVisible)
            {
                ObjectiveText = "Phase 01 complete";
                return;
            }

            if (controller.IsInputLocked)
            {
                ObjectiveText = carrying
                    ? "Particle transfer in progress"
                    : "Route transition in progress";
                ContextPromptText = "Transition in progress...";
                TransitionMessageVisible = true;
                return;
            }

            ObjectiveText = carrying
                ? "Return the particle to the radio"
                : "Reach the yellow particle";

            DepositPromptVisible = carrying
                && controller.CanAcceptInput
                && controller.PlayerScalar <= controller.RadioStopDistance + 0.35f
                && controller.CameraRig != null
                && controller.CameraRig.IsStableRadioLock;
            if (DepositPromptVisible)
                ContextPromptText = "Press E to deposit";
        }

        private void OnGUI()
        {
            if (controller == null)
                return;

            RefreshHudState();
            EnsureStyles();
            DrawProductionHud();

            if (DebugDiagnosticsEnabled)
                DrawDebugDiagnostics();
        }

        private void EnsureStyles()
        {
            objectiveStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.88f, 0.2f) }
            };
            hudLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            promptStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            controlsStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.92f, 1f) }
            };
            diagnosticStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.75f, 0.9f, 1f) }
            };
            completeStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.88f, 0.2f) }
            };
        }

        private void DrawProductionHud()
        {
            float hudWidth = Mathf.Min(540f, Screen.width - 28f);
            float hudX = (Screen.width - hudWidth) * 0.5f;
            GUI.Box(new Rect(hudX, 14f, hudWidth, 106f), string.Empty);
            GUILayout.BeginArea(new Rect(hudX + 12f, 20f, hudWidth - 24f, 94f));
            GUILayout.Label(ObjectiveText, objectiveStyle, GUILayout.Height(30f));
            GUILayout.Label($"{BranchProgressText}     {ReceiverProgressText}", hudLabelStyle, GUILayout.Height(24f));
            GUILayout.Label(CarryStatusText, hudLabelStyle, GUILayout.Height(24f));
            GUILayout.EndArea();

            if (DepositPromptVisible || TransitionMessageVisible)
            {
                float promptWidth = Mathf.Min(340f, Screen.width - 28f);
                GUI.Box(
                    new Rect((Screen.width - promptWidth) * 0.5f, Screen.height - 104f, promptWidth, 44f),
                    ContextPromptText,
                    promptStyle);
            }

            if (ControlsVisible)
            {
                float controlsWidth = Mathf.Min(560f, Screen.width - 28f);
                GUI.Box(
                    new Rect(14f, Screen.height - 50f, controlsWidth, 36f),
                    ControlsText,
                    controlsStyle);
            }

            if (CompletionBannerVisible)
            {
                float completionWidth = Mathf.Min(500f, Screen.width - 40f);
                GUI.Box(
                    new Rect((Screen.width - completionWidth) * 0.5f, Screen.height * 0.5f - 58f, completionWidth, 116f),
                    "PHASE 01 COMPLETE\nReceiver charged 4 / 4",
                    completeStyle);
            }
        }

        private void DrawDebugDiagnostics()
        {
            float width = Mathf.Min(316f, Screen.width - 28f);
            float x = Screen.width - width - 14f;
            GUI.Box(new Rect(x, 132f, width, 260f), string.Empty);
            GUILayout.BeginArea(new Rect(x + 12f, 140f, width - 24f, 244f));
            GUILayout.Label("DEBUG DIAGNOSTICS", diagnosticStyle);
            GUILayout.Label($"State: {controller.State}", diagnosticStyle);
            GUILayout.Label($"Input locked: {controller.IsInputLocked}", diagnosticStyle);
            GUILayout.Label($"Player scalar: {controller.PlayerScalar:F2}", diagnosticStyle);
            if (cameraRig != null)
            {
                float radioX = cameraRig.ViewCamera.WorldToViewportPoint(controller.RadioPosition + Vector3.up).x;
                GUILayout.Label($"Camera: {cameraRig.State}", diagnosticStyle);
                GUILayout.Label($"Lock side: {cameraRig.LatchedLockSide}", diagnosticStyle);
                GUILayout.Label($"Radio viewport X: {radioX:F3}", diagnosticStyle);
            }
            if (performanceMonitor != null)
            {
                GUILayout.Label($"Frame: {performanceMonitor.CurrentFrameMs:F2} ms / {performanceMonitor.CurrentFps:F1} FPS", diagnosticStyle);
                GUILayout.Label($"GC allocated: {performanceMonitor.CurrentGcBytes} B", diagnosticStyle);
            }
            if (pacingMonitor != null)
            {
                GUILayout.Label($"Pacing total: {pacingMonitor.CurrentTotalSeconds:F1}s", diagnosticStyle);
                GUILayout.Label($"Splits: {pacingMonitor.SplitSummary}", diagnosticStyle);
            }
            GUILayout.EndArea();
        }
    }
}
