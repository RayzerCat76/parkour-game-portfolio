using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Phase01.Demo0;
using ProjectReturn.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectReturn.Core.Tests
{
    public sealed class ProjectScaffoldPlayModeTests
    {
        private static readonly string[] ExpectedProductionScenes =
        {
            "Assets/Scenes/Boot.unity",
            "Assets/Scenes/Prologue.unity",
            "Assets/Scenes/Phase01_Demo0.unity",
            "Assets/Scenes/Phase02.unity",
            "Assets/Scenes/Phase03.unity",
            "Assets/Scenes/Phase04.unity",
            "Assets/Scenes/Ending.unity"
        };

        [UnityTearDown]
        public IEnumerator CleanupPersistentProjectServices()
        {
            ProjectServicesRoot.ResetForTests();
            yield return null;
        }

        [Test]
        public void BuildSettings_ContainRequiredScenesExactlyOnceAndInOrder()
        {
            Assert.That(
                SceneManager.sceneCountInBuildSettings,
                Is.GreaterThanOrEqualTo(ExpectedProductionScenes.Length));

            for (int i = 0; i < ExpectedProductionScenes.Length; i++)
            {
                Assert.That(
                    SceneUtility.GetScenePathByBuildIndex(i),
                    Is.EqualTo(ExpectedProductionScenes[i]),
                    $"Unexpected production scene at Build Settings index {i}.");
            }

            Assert.That(
                Application.CanStreamedLevelBeLoaded(ProjectSceneNames.MovementSandbox),
                Is.True,
                "The accepted movement sandbox must remain loadable for editor PlayMode regression tests.");
        }

        [UnityTest]
        public IEnumerator ProjectServices_AreSingletonAndInterfacesRecordRequestsWithoutAssets()
        {
            ProjectServicesRoot.ResetForTests();
            yield return null;

            ProjectServicesRoot first = ProjectServicesRoot.EnsureExists();
            ProjectServicesRoot second = ProjectServicesRoot.EnsureExists();
            Assert.That(second, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<ProjectServicesRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameFlowController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<MusicDirector>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<VisualStateController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            int musicCountBefore = first.Music.PhaseEntryRequestCount;
            first.Music.NotifyPhaseEntered(ProjectPhase.Phase03);
            Assert.That(first.Music.PhaseEntryRequestCount, Is.EqualTo(musicCountBefore + 1));
            Assert.That(first.Music.LastEnteredPhase, Is.EqualTo(ProjectPhase.Phase03));
            first.Music.NotifyParticlePickedUp(2);
            Assert.That(first.Music.LastEventName, Is.EqualTo("ParticlePickedUp"));
            Assert.That(first.Music.LastBranchIndex, Is.EqualTo(2));

            int visualCountBefore = first.Visuals.RequestCount;
            first.Visuals.RequestPhaseState(ProjectPhase.Phase04);
            first.Visuals.RequestPalette("placeholder");
            first.Visuals.RequestFade(0.25f, true);
            Assert.That(first.Visuals.RequestCount, Is.EqualTo(visualCountBefore + 3));
            Assert.That(first.Visuals.LastRequestedPhase, Is.EqualTo(ProjectPhase.Phase04));
            Assert.That(first.Visuals.LastPaletteState, Is.EqualTo("placeholder"));
            Assert.That(first.Visuals.LastFadeToBlack, Is.True);
        }

        [UnityTest]
        public IEnumerator Boot_InitializesOneServicesRootAndAdvancesToPrologueOnce()
        {
            ProjectServicesRoot.ResetForTests();
            yield return null;

            SceneManager.LoadScene(ProjectSceneNames.Boot, LoadSceneMode.Single);
            yield return WaitForScene(ProjectSceneNames.Prologue, 3f);

            ProjectServicesRoot services = ProjectServicesRoot.Instance;
            Assert.That(services, Is.Not.Null);
            Assert.That(services.GameFlow.CurrentPhase, Is.EqualTo(ProjectPhase.Prologue));
            Assert.That(services.GameFlow.TransitionCount, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ProjectServicesRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<GameFlowController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<MusicDirector>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<VisualStateController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PlaceholderScenes_AdvanceExactlyOnceAndEndingDoesNotRestart()
        {
            ProjectServicesRoot.ResetForTests();
            yield return null;

            yield return LoadAndAdvancePlaceholder(ProjectSceneNames.Prologue, ProjectSceneNames.Phase01);
            yield return LoadAndAdvancePlaceholder(ProjectSceneNames.Phase02, ProjectSceneNames.Phase03);
            yield return LoadAndAdvancePlaceholder(ProjectSceneNames.Phase03, ProjectSceneNames.Phase04);
            yield return LoadAndAdvancePlaceholder(ProjectSceneNames.Phase04, ProjectSceneNames.Ending);

            PlaceholderSceneController ending = Object.FindAnyObjectByType<PlaceholderSceneController>();
            Assert.That(ending, Is.Not.Null);
            Assert.That(ending.Phase, Is.EqualTo(ProjectPhase.Ending));
            Assert.That(ending.CanContinue, Is.False);
            Assert.That(ending.Continue(), Is.False);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ProjectSceneNames.Ending));
        }

        [Test]
        public void DevelopmentNavigation_IsUnavailableInAReleasePlayer()
        {
            Assert.That(GameFlowController.IsDevelopmentNavigationAllowed(false, false), Is.False);
            Assert.That(GameFlowController.IsDevelopmentNavigationAllowed(true, false), Is.True);
            Assert.That(GameFlowController.IsDevelopmentNavigationAllowed(false, true), Is.True);
        }

        [UnityTest]
        public IEnumerator Phase01Bridge_ObservesCompletionAndRequestsPhase02ExactlyOnce()
        {
            ProjectServicesRoot.ResetForTests();
            yield return null;

            SceneManager.LoadScene(ProjectSceneNames.Phase01, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Phase01DemoController controller = Object.FindAnyObjectByType<Phase01DemoController>();
            Phase01FlowBridge bridge = Object.FindAnyObjectByType<Phase01FlowBridge>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);

            SetAutoProperty(controller, "DemoComplete", true);
            Assert.That(bridge.CanContinue, Is.True);
            Assert.That(bridge.Continue(), Is.True);
            Assert.That(bridge.Continue(), Is.False);
            Assert.That(bridge.AdvanceRequestCount, Is.EqualTo(1));

            yield return WaitForScene(ProjectSceneNames.Phase02, 3f);
            Assert.That(ProjectServicesRoot.Instance.GameFlow.CurrentPhase, Is.EqualTo(ProjectPhase.Phase02));
        }

        private static IEnumerator LoadAndAdvancePlaceholder(string sceneName, string expectedNextScene)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;

            PlaceholderSceneController placeholder = Object.FindAnyObjectByType<PlaceholderSceneController>();
            Assert.That(placeholder, Is.Not.Null, $"Placeholder controller missing from {sceneName}.");
            Assert.That(placeholder.AdvanceRequested, Is.False);
            Assert.That(placeholder.Continue(), Is.True);
            Assert.That(placeholder.Continue(), Is.False, "A placeholder accepted a duplicate advance request.");

            yield return WaitForScene(expectedNextScene, 3f);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(sceneName),
                $"Timed out waiting for scene {sceneName}.");
        }

        private static void SetAutoProperty(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                $"<{propertyName}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Backing field for {propertyName} was not found.");
            field.SetValue(target, value);
        }
    }
}
