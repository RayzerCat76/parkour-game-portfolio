using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01Receiver : MonoBehaviour
    {
        [SerializeField] private Transform depositTarget;
        [SerializeField] private GameObject[] chargeLights;

        /// <summary>Resolved at runtime, so the receiver needs no new serialized reference for it.</summary>
        private Phase01RadioBurst burst;

        public Transform DepositTarget => depositTarget;

        private void Awake() => burst = GetComponentInChildren<Phase01RadioBurst>(true);

        public void Configure(Transform target, GameObject[] lights)
        {
            depositTarget = target;
            chargeLights = lights;
            SetCharge(0);
        }

        public void SetCharge(int charge)
        {
            if (chargeLights == null)
                return;

            for (int i = 0; i < chargeLights.Length; i++)
                chargeLights[i].SetActive(i < charge);

            // The design's payoff: the radio bursts once it has felt every block.
            if (charge >= chargeLights.Length)
            {
                if (burst == null)
                    burst = GetComponentInChildren<Phase01RadioBurst>(true);
                burst?.Play();
            }
        }
    }
}
