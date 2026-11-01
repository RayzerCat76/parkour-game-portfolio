using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxDebugOverlay : MonoBehaviour
    {
        [SerializeField] private Phase01ParkourMotor motor;
        [SerializeField] private Phase01SandboxReset reset;
        [SerializeField] private Phase01SandboxPerformanceMonitor performance;
        private GUIStyle style;

        public void Configure(
            Phase01ParkourMotor targetMotor,
            Phase01SandboxReset resetController,
            Phase01SandboxPerformanceMonitor monitor)
        {
            motor = targetMotor;
            reset = resetController;
            performance = monitor;
        }

        private void OnGUI()
        {
            if ((!Debug.isDebugBuild && !Application.isEditor) || motor == null)
                return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            GUI.Box(new Rect(14f, 14f, 370f, 248f), string.Empty);
            GUILayout.BeginArea(new Rect(28f, 23f, 345f, 230f));
            GUILayout.Label($"Grounded: {motor.IsGrounded}", style);
            GUILayout.Label($"Velocity: X {motor.HorizontalVelocity:F2}  Y {motor.VerticalVelocity:F2}", style);
            GUILayout.Label($"Coyote: {motor.CoyoteTimer:F3} / {motor.CoyoteTime:F2}", style);
            GUILayout.Label($"Jump buffer: {motor.JumpBufferTimer:F3} / {motor.JumpBufferTime:F2}", style);
            GUILayout.Label($"Checkpoint: {reset.CurrentCheckpointId}", style);
            GUILayout.Label($"Resetting: {reset.IsResetting}", style);
            GUILayout.Label($"Moving platform: {(motor.GroundPlatform == null ? "None" : motor.GroundPlatform.name)}", style);
            GUILayout.Label($"Frame: {performance.CurrentFrameMs:F2} ms / {performance.CurrentFps:F1} FPS", style);
            GUILayout.Label($"GC allocated: {performance.CurrentGcBytes} B", style);
            GUILayout.EndArea();
            GUI.Box(new Rect(14f, Screen.height - 52f, 470f, 38f), "Move: A/D or ←/→     Jump: Space (hold for height)");
        }
    }
}
