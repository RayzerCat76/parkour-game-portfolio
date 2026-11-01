using System.Collections;
using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxReset : MonoBehaviour
    {
        [SerializeField] private Phase01ParkourMotor motor;
        [SerializeField] private Phase01SandboxCamera cameraRig;
        [SerializeField] private Phase01SandboxCheckpoint[] checkpoints;
        [SerializeField] private float resetDuration = 0.35f;
        [SerializeField] private float failSafeY = -8f;

        private Phase01SandboxCheckpoint currentCheckpoint;

        public bool IsResetting { get; private set; }
        public string CurrentCheckpointId => currentCheckpoint != null ? currentCheckpoint.CheckpointId : "None";
        public float LastResetDuration { get; private set; }
        public int ResetCount { get; private set; }
        public int ActivatedCheckpointCount { get; private set; }
        public float ResetDuration => resetDuration;
        public Vector3 CurrentRespawnPosition => currentCheckpoint != null ? currentCheckpoint.RespawnPosition : Vector3.zero;

        public void Configure(
            Phase01ParkourMotor targetMotor,
            Phase01SandboxCamera targetCamera,
            Phase01SandboxCheckpoint[] sceneCheckpoints)
        {
            motor = targetMotor;
            cameraRig = targetCamera;
            checkpoints = sceneCheckpoints;
        }

        private void Start()
        {
            if (checkpoints != null && checkpoints.Length > 0)
                ActivateCheckpoint(checkpoints[0]);
        }

        private void Update()
        {
            if (!IsResetting && motor != null && motor.transform.position.y < failSafeY)
                RequestReset();
        }

        public void ActivateCheckpoint(Phase01SandboxCheckpoint checkpoint)
        {
            if (checkpoint == null || currentCheckpoint == checkpoint)
                return;
            currentCheckpoint = checkpoint;
            ActivatedCheckpointCount++;
            if (checkpoints == null)
                return;
            foreach (Phase01SandboxCheckpoint item in checkpoints)
                item?.SetActiveVisual(item == currentCheckpoint);
        }

        public void RequestReset()
        {
            if (!IsResetting && currentCheckpoint != null)
                StartCoroutine(ResetRoutine());
        }

        private IEnumerator ResetRoutine()
        {
            IsResetting = true;
            motor.SetInputLocked(true);
            float started = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(resetDuration * 0.5f);
            motor.TeleportAndReset(CurrentRespawnPosition);
            cameraRig.BeginResetSettle(CurrentRespawnPosition);
            yield return new WaitForSecondsRealtime(resetDuration * 0.5f);
            motor.SetInputLocked(false);
            IsResetting = false;
            ResetCount++;
            LastResetDuration = Time.realtimeSinceStartup - started;
        }
    }

}
