using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.TestTools.TestRunner;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ProjectReturn.TestInfrastructure
{
    public static class TestRunnerRecoveryDiagnostics
    {
        private const string MenuRoot = "Tools/Codex/Test Runner Recovery V2/";
        private static TestRunnerApi api;

        [MenuItem(MenuRoot + "Capture Compilation + Discovery Reports")]
        public static void CaptureReports()
        {
            Directory.CreateDirectory(ReportRoot);
            WriteCompilationReport();
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RetrieveTestList(TestMode.EditMode, root => WriteDiscoveryReport(root, TestMode.EditMode));
            api.RetrieveTestList(TestMode.PlayMode, root => WriteDiscoveryReport(root, TestMode.PlayMode));
            Debug.Log("[TestRunnerRecoveryV2] Official API discovery requested for EditMode and PlayMode.");
        }

        [MenuItem(MenuRoot + "Run Existing MovementSandbox Smoke Test")]
        public static void RunSmoke()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { "Phase01.MovementSandbox.Tests.Phase01MovementSandboxPlayModeTests.SandboxTour_PassesMovementAndReliabilityRequirements" }
            }));
        }

        [MenuItem(MenuRoot + "Run Demo0 Integration Test")]
        public static void RunDemo0IntegrationTest()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.PlayMode,
                testNames = new[] { "Phase01.Demo0.Tests.Phase01DemoPlayModeTests.CompleteCrossLoop_PassesAllAcceptanceChecks" }
            }));
        }

        [MenuItem(MenuRoot + "Run All Five PlayMode Tests")]
        public static void RunAllPlayModeTests()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
        }

        [MenuItem(MenuRoot + "Run P3 Branch 2 Acceptance Capture")]
        public static void RunP3Branch2AcceptanceCapture()
        {
            Directory.CreateDirectory(ReportRoot);
            File.WriteAllText(Path.Combine(ReportRoot, "TASK_P3_CAPTURE_REQUEST"), "requested");
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                testNames = new[]
                {
                    "Phase01.Demo0.Tests.ReviewCapture.Phase01ReviewCaptureTests.CaptureSupportedRadioAndStrictEdgeLockPass"
                }
            }));
        }

        [MenuItem(MenuRoot + "Show PlayMode Discovery In Test Runner")]
        public static void ShowPlayModeDiscoveryInTestRunner()
        {
            TestRunnerWindow.ShowWindow();
            var window = EditorWindow.GetWindow<TestRunnerWindow>();
            var windowType = typeof(TestRunnerWindow);
            var toolbarField = windowType.GetField("m_TestTypeToolbarIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var selectMethod = windowType.GetMethod("SelectTestListGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var retrieveMethod = windowType.GetMethod("StartRetrieveTestList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (toolbarField == null || selectMethod == null || retrieveMethod == null)
                throw new MissingMemberException("Unity Test Framework 1.6.0 TestRunnerWindow internals changed.");

            object playMode = Enum.ToObject(toolbarField.FieldType, 1);
            toolbarField.SetValue(window, playMode);
            selectMethod.Invoke(window, new[] { playMode });
            retrieveMethod.Invoke(window, null);
            window.Repaint();
            Debug.Log("[TestRunnerRecoveryV2] Test Runner UI switched to PlayMode discovery.");
        }

        private static void WriteCompilationReport()
        {
            var text = new StringBuilder();
            text.AppendLine($"Unity {Application.unityVersion}");
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies().OrderBy(item => item.name))
            {
                if (!assembly.name.Contains("Phase01") && !assembly.name.Contains("TestInfrastructure"))
                    continue;
                text.AppendLine($"ASSEMBLY {assembly.name}");
                text.AppendLine($"  outputPath={assembly.outputPath}");
                text.AppendLine($"  flags={assembly.flags}");
                text.AppendLine($"  sources={string.Join(";", assembly.sourceFiles)}");
                text.AppendLine($"  defines={string.Join(";", assembly.defines)}");
                text.AppendLine($"  references={string.Join(";", assembly.assemblyReferences.Select(reference => reference.name))}");
                text.AppendLine($"  compiledAssemblyReferences={string.Join(";", assembly.compiledAssemblyReferences)}");
            }
            File.WriteAllText(Path.Combine(ReportRoot, "TASK_INFRA_TestAssemblies_CompilationReport.txt"), text.ToString());
        }

        private static void WriteDiscoveryReport(ITestAdaptor root, TestMode mode)
        {
            var names = new List<string>();
            Flatten(root, names);
            string file = mode == TestMode.EditMode
                ? "TASK_INFRA_DiscoveredEditModeTests.txt"
                : "TASK_INFRA_DiscoveredPlayModeTests.txt";
            File.WriteAllLines(Path.Combine(ReportRoot, file), new[]
            {
                $"mode={mode}",
                $"root={(root == null ? "null" : root.FullName)}",
                $"testCaseCount={(root == null ? 0 : root.TestCaseCount)}"
            }.Concat(names));
            Debug.Log($"[TestRunnerRecoveryV2] {mode} API discovery count: {(root == null ? 0 : root.TestCaseCount)}");
        }

        private static void Flatten(ITestAdaptor test, List<string> names)
        {
            if (test == null)
                return;
            if (!test.IsSuite)
                names.Add(test.FullName);
            if (test.Children == null)
                return;
            foreach (ITestAdaptor child in test.Children)
                Flatten(child, names);
        }

        private static string ReportRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewCaptures"));

    }
}
