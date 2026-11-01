using System.Collections;
using UnityEngine;

namespace ProjectReturn.Core
{
    public sealed class BootSceneController : MonoBehaviour
    {
        private bool requested;

        private IEnumerator Start()
        {
            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            yield return null;

            if (requested)
                yield break;

            requested = true;
            services.GameFlow.RequestPhase(ProjectPhase.Prologue);
        }
    }
}
