using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectReturn.Core
{
    public sealed class GameFlowController : MonoBehaviour
    {
        private MusicDirector music;
        private VisualStateController visuals;
        private bool transitionPending;
        private bool subscribed;
        private int lastRegisteredSceneHandle = int.MinValue;

        public ProjectPhase CurrentPhase { get; private set; } = ProjectPhase.Boot;
        public int TransitionCount { get; private set; }
        public int SceneEntryCount { get; private set; }
        public bool TransitionPending => transitionPending;
        public string PendingSceneName { get; private set; } = string.Empty;
        public bool DevelopmentNavigationAvailable =>
            IsDevelopmentNavigationAllowed(Application.isEditor, Debug.isDebugBuild);

        public void Configure(MusicDirector musicDirector, VisualStateController visualStateController)
        {
            music = musicDirector;
            visuals = visualStateController;
            Subscribe();
            RegisterScene(SceneManager.GetActiveScene());
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (!subscribed)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            SceneManager.sceneLoaded += OnSceneLoaded;
            subscribed = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            transitionPending = false;
            PendingSceneName = string.Empty;
            RegisterScene(scene);
        }

        private void RegisterScene(Scene scene)
        {
            if (!scene.IsValid() || scene.handle == lastRegisteredSceneHandle)
                return;
            if (!ProjectSceneNames.TryGetPhase(scene.name, out ProjectPhase phase))
                return;

            lastRegisteredSceneHandle = scene.handle;
            CurrentPhase = phase;
            SceneEntryCount++;
            music?.NotifyPhaseEntered(phase);
            visuals?.RequestPhaseState(phase);
        }

        public bool RequestNextPhase()
        {
            return ProjectSceneNames.TryGetNext(CurrentPhase, out ProjectPhase next)
                && RequestPhase(next);
        }

        public bool RequestPhase(ProjectPhase phase)
        {
            if (transitionPending)
                return false;

            string sceneName = ProjectSceneNames.ForPhase(phase);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameFlow] Scene '{sceneName}' is not available in Build Settings.");
                return false;
            }

            transitionPending = true;
            PendingSceneName = sceneName;
            TransitionCount++;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            return true;
        }

        public bool TryDevelopmentLoad(ProjectPhase phase)
        {
            return DevelopmentNavigationAvailable && RequestPhase(phase);
        }

        public static bool IsDevelopmentNavigationAllowed(bool isEditor, bool isDebugBuild)
        {
            return isEditor || isDebugBuild;
        }

        private void Update()
        {
            if (!DevelopmentNavigationAvailable || transitionPending || !ProjectInput.ShiftIsPressed())
                return;

            switch (ProjectInput.PressedDevelopmentDigit())
            {
                case 1: TryDevelopmentLoad(ProjectPhase.Prologue); break;
                case 2: TryDevelopmentLoad(ProjectPhase.Phase01); break;
                case 3: TryDevelopmentLoad(ProjectPhase.Phase02); break;
                case 4: TryDevelopmentLoad(ProjectPhase.Phase03); break;
                case 5: TryDevelopmentLoad(ProjectPhase.Phase04); break;
                case 6: TryDevelopmentLoad(ProjectPhase.Ending); break;
            }
        }
    }
}
