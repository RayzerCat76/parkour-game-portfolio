using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxCheckpoint : MonoBehaviour
    {
        [SerializeField] private string checkpointId;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private Renderer marker;
        [SerializeField] private Phase01SandboxReset reset;

        public string CheckpointId => checkpointId;
        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

        public void Configure(string id, Transform point, Renderer markerRenderer)
        {
            checkpointId = id;
            respawnPoint = point;
            marker = markerRenderer;
        }

        public void SetResetController(Phase01SandboxReset controller) => reset = controller;

        public void SetActiveVisual(bool active)
        {
            if (marker == null)
                return;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            marker.GetPropertyBlock(block);
            Color color = active ? new Color(1f, 0.84f, 0.12f) : new Color(0.25f, 0.68f, 0.72f);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            marker.SetPropertyBlock(block);
        }

        private void OnTriggerEnter(Collider other)
        {
            Phase01ParkourMotor motor = other.GetComponent<Phase01ParkourMotor>();
            if (motor != null)
                reset?.ActivateCheckpoint(this);
        }
    }
}
