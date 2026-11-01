using UnityEngine;

namespace ProjectReturn.Core
{
    [DefaultExecutionOrder(-10000)]
    public sealed class ProjectServicesRoot : MonoBehaviour
    {
        public static ProjectServicesRoot Instance { get; private set; }

        public GameFlowController GameFlow { get; private set; }
        public MusicDirector Music { get; private set; }
        public VisualStateController Visuals { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeFirstScene()
        {
            EnsureExists();
        }

        public static ProjectServicesRoot EnsureExists()
        {
            if (Instance != null)
                return Instance;

            ProjectServicesRoot existing = FindAnyObjectByType<ProjectServicesRoot>();
            if (existing != null)
            {
                existing.InstallComponents();
                return existing;
            }

            GameObject rootObject = new GameObject("Project Services");
            return rootObject.AddComponent<ProjectServicesRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InstallComponents();
        }

        private void InstallComponents()
        {
            if (Instance == null)
                Instance = this;

            GameFlow = GetComponent<GameFlowController>() ?? gameObject.AddComponent<GameFlowController>();
            Music = GetComponent<MusicDirector>() ?? gameObject.AddComponent<MusicDirector>();
            Visuals = GetComponent<VisualStateController>() ?? gameObject.AddComponent<VisualStateController>();
            GameFlow.Configure(Music, Visuals);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ResetForTests()
        {
            ProjectServicesRoot[] roots = FindObjectsByType<ProjectServicesRoot>(FindObjectsSortMode.None);
            foreach (ProjectServicesRoot root in roots)
            {
                if (root != null)
                    DestroyImmediate(root.gameObject);
            }

            Instance = null;
        }
    }
}
