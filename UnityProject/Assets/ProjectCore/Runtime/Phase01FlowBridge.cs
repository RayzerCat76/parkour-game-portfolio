using System;
using Phase01.Demo0;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectReturn.Core
{
    public sealed class Phase01FlowBridge : MonoBehaviour
    {
        private Phase01DemoController controller;
        private MusicDirector music;
        private bool advanceRequested;
        private GUIStyle promptStyle;

        public bool CanContinue => controller != null && controller.DemoComplete && !advanceRequested;
        public int AdvanceRequestCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallAfterSceneLoad()
        {
            InstallForScene(SceneManager.GetActiveScene());
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForScene(scene);
        }

        private static void InstallForScene(Scene scene)
        {
            if (!scene.IsValid()
                || !string.Equals(scene.name, ProjectSceneNames.Phase01, StringComparison.Ordinal))
                return;

            Phase01DemoController demo = FindAnyObjectByType<Phase01DemoController>();
            if (demo == null || demo.GetComponent<Phase01FlowBridge>() != null)
                return;

            Phase01FlowBridge bridge = demo.gameObject.AddComponent<Phase01FlowBridge>();
            bridge.controller = demo;
        }

        private void Awake()
        {
            controller ??= GetComponent<Phase01DemoController>();
            music = ProjectServicesRoot.EnsureExists().Music;
            SubscribeMusicEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeMusicEvents();
        }

        private void SubscribeMusicEvents()
        {
            if (controller == null || music == null)
                return;
            controller.ParticlePickedUp += music.NotifyParticlePickedUp;
            controller.ParticleDepositStarted += music.NotifyParticleDepositStarted;
            controller.ParticleDeposited += music.NotifyParticleDeposited;
            controller.ViewRotationStarted += music.NotifyViewRotationStarted;
            controller.ViewRotationCompleted += music.NotifyViewRotationCompleted;
            controller.ActiveBranchChanged += music.NotifyActiveBranchChanged;
            controller.ReceiverCompleted += music.NotifyReceiverCompleted;
            controller.Branch2RouteTransformationStarted += music.NotifyBranch2RouteTransformationStarted;
            controller.Branch3FadingSupportActivated += music.NotifyBranch3FadingSupportActivated;
            controller.Branch4ControlledCollapseActivated += music.NotifyBranch4ControlledCollapseActivated;
        }

        private void UnsubscribeMusicEvents()
        {
            if (controller == null || music == null)
                return;
            controller.ParticlePickedUp -= music.NotifyParticlePickedUp;
            controller.ParticleDepositStarted -= music.NotifyParticleDepositStarted;
            controller.ParticleDeposited -= music.NotifyParticleDeposited;
            controller.ViewRotationStarted -= music.NotifyViewRotationStarted;
            controller.ViewRotationCompleted -= music.NotifyViewRotationCompleted;
            controller.ActiveBranchChanged -= music.NotifyActiveBranchChanged;
            controller.ReceiverCompleted -= music.NotifyReceiverCompleted;
            controller.Branch2RouteTransformationStarted -= music.NotifyBranch2RouteTransformationStarted;
            controller.Branch3FadingSupportActivated -= music.NotifyBranch3FadingSupportActivated;
            controller.Branch4ControlledCollapseActivated -= music.NotifyBranch4ControlledCollapseActivated;
        }

        private void Update()
        {
            if (CanContinue && ProjectInput.EnterPressedThisFrame())
                Continue();
        }

        public bool Continue()
        {
            if (!CanContinue)
                return false;

            ProjectServicesRoot services = ProjectServicesRoot.EnsureExists();
            if (!services.GameFlow.RequestPhase(ProjectPhase.Phase02))
                return false;

            advanceRequested = true;
            AdvanceRequestCount++;
            services.Music.NotifyPhase01Completed();
            return true;
        }

        private void OnGUI()
        {
            if (!CanContinue)
                return;

            promptStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Box(
                new Rect(Screen.width * 0.5f - 180f, Screen.height - 96f, 360f, 48f),
                "Press Enter to continue",
                promptStyle);
        }
    }
}
