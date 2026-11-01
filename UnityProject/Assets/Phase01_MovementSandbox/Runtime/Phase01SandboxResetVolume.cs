using UnityEngine;

namespace Phase01.MovementSandbox
{
    public sealed class Phase01SandboxResetVolume : MonoBehaviour
    {
        [SerializeField] private Phase01SandboxReset reset;

        public void Configure(Phase01SandboxReset controller) => reset = controller;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<Phase01ParkourMotor>() != null)
                reset?.RequestReset();
        }
    }
}
