using System.IO;
using ProjectReturn.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectReturn.Core.Editor
{
    public static class ProjectScaffoldBuilder
    {
        public const string SceneFolder = "Assets/Scenes";
        public const string BootScenePath = SceneFolder + "/Boot.unity";
        public const string PrologueScenePath = SceneFolder + "/Prologue.unity";
        public const string Phase01ScenePath = SceneFolder + "/Phase01_Demo0.unity";
        public const string Phase02ScenePath = SceneFolder + "/Phase02.unity";
        public const string Phase03ScenePath = SceneFolder + "/Phase03.unity";
        public const string Phase04ScenePath = SceneFolder + "/Phase04.unity";
        public const string EndingScenePath = SceneFolder + "/Ending.unity";
        public const string MovementSandboxScenePath = SceneFolder + "/Phase01_MovementSandbox.unity";

        public static readonly string[] RequiredScenePaths =
        {
            BootScenePath,
            PrologueScenePath,
            Phase01ScenePath,
            Phase02ScenePath,
            Phase03ScenePath,
            Phase04ScenePath,
            EndingScenePath
        };

        [MenuItem("Tools/Codex/Project/Build TASK 001 Scaffold")]
        public static void BuildScaffold()
        {
            if (!File.Exists(Phase01ScenePath))
                throw new FileNotFoundException("The accepted Phase01_Demo0 scene is missing.", Phase01ScenePath);

            CreateBootScene();
            CreatePlaceholderScene(
                PrologueScenePath,
                ProjectPhase.Prologue,
                ProjectPhase.Phase01,
                "PROLOGUE",
                true);
            CreatePlaceholderScene(
                Phase02ScenePath,
                ProjectPhase.Phase02,
                ProjectPhase.Phase03,
                "PHASE 02",
                true);
            CreatePlaceholderScene(
                Phase03ScenePath,
                ProjectPhase.Phase03,
                ProjectPhase.Phase04,
                "PHASE 03",
                true);
            CreatePlaceholderScene(
                Phase04ScenePath,
                ProjectPhase.Phase04,
                ProjectPhase.Ending,
                "PHASE 04",
                true);
            CreatePlaceholderScene(
                EndingScenePath,
                ProjectPhase.Ending,
                ProjectPhase.Ending,
                "ENDING",
                false);

            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log("[ProjectScaffold] TASK 001 scaffold built. Boot is the first enabled scene.");
        }

        [MenuItem("Tools/Codex/Project/Build macOS Development")]
        public static void BuildMacDevelopment()
        {
            BuildScaffold();

            string outputDirectory = Path.Combine(Path.GetTempPath(), "ProjectReturnBuild");
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "Project Return.app");
            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, true);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = RequiredScenePaths,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
                    | BuildOptions.CleanBuildCache
                    | BuildOptions.StrictMode
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"TASK 001 macOS development build failed: {report.summary.result}");

            Debug.Log($"[ProjectScaffold] macOS development build succeeded: {outputPath}");
        }

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneEnvironment(new Color(0.015f, 0.02f, 0.04f));

            GameObject boot = new GameObject("Boot Controller");
            boot.AddComponent<BootSceneController>();

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreatePlaceholderScene(
            string scenePath,
            ProjectPhase phase,
            ProjectPhase nextPhase,
            string title,
            bool canContinue)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureSceneEnvironment(BackgroundForPhase(phase));

            GameObject controllerObject = new GameObject($"{title} Placeholder Controller");
            PlaceholderSceneController controller = controllerObject.AddComponent<PlaceholderSceneController>();
            controller.Configure(phase, nextPhase, title, canContinue);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void ConfigureSceneEnvironment(Color background)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.2f, 0.28f);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();
        }

        private static Color BackgroundForPhase(ProjectPhase phase)
        {
            return phase switch
            {
                ProjectPhase.Prologue => new Color(0.025f, 0.03f, 0.055f),
                ProjectPhase.Phase02 => new Color(0.04f, 0.025f, 0.055f),
                ProjectPhase.Phase03 => new Color(0.025f, 0.045f, 0.05f),
                ProjectPhase.Phase04 => new Color(0.055f, 0.035f, 0.025f),
                ProjectPhase.Ending => new Color(0.015f, 0.015f, 0.02f),
                _ => new Color(0.02f, 0.025f, 0.04f)
            };
        }

        private static void ConfigureBuildSettings()
        {
            int extraSceneCount = File.Exists(MovementSandboxScenePath) ? 1 : 0;
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[RequiredScenePaths.Length + extraSceneCount];
            for (int i = 0; i < RequiredScenePaths.Length; i++)
            {
                string path = RequiredScenePaths[i];
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Required scene was not generated: {path}", path);
                scenes[i] = new EditorBuildSettingsScene(path, true);
            }

            if (extraSceneCount == 1)
            {
                // Keep the accepted movement sandbox enabled for editor PlayMode regression tests.
                // Standalone builds still receive only RequiredScenePaths in BuildMacDevelopment().
                scenes[RequiredScenePaths.Length] = new EditorBuildSettingsScene(MovementSandboxScenePath, true);
            }

            EditorBuildSettings.scenes = scenes;
        }
    }
}
