using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnAudioPlayerSchemaExport
    {
        [Serializable] sealed class Property
        {
            public string name, type, assignedValue;
            public bool canWrite;
        }
        [Serializable] sealed class Output { public string path; public long bytes; }
        [Serializable] sealed class Report
        {
            public string unityVersion, outputFolder, failure, writeResultType;
            public bool passed, serializationInvoked;
            public List<Property> settingsProperties = new List<Property>();
            public List<Output> outputs = new List<Output>();
        }

        public static void Run()
        {
            var report = new Report { unityVersion = Application.unityVersion };
            string project = Directory.GetParent(Application.dataPath).FullName;
            string root = Directory.GetParent(project).FullName;
            string evidence = Path.Combine(root, "Redesign/Evidence/v334");
            report.outputFolder = Path.Combine(evidence, "PlayerManagers");
            Directory.CreateDirectory(report.outputFolder);
            object referenceMap = null;
            try
            {
                if (Application.unityVersion != "6000.4.11f1") throw new InvalidOperationException("Unexpected editor version");
                Type api = typeof(AssetDatabase).Assembly.GetType("UnityEditor.Build.Content.ContentBuildInterface", true);
                MethodInfo writer = null;
                foreach (MethodInfo candidate in api.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    if (candidate.Name == "WriteGameManagersSerializedFile" && candidate.GetParameters().Length == 2) { writer = candidate; break; }
                if (writer == null) throw new InvalidOperationException("Verified manager writer is unavailable");
                Type parameterType = writer.GetParameters()[1].ParameterType;
                FieldInfo settingsField = RequiredField(parameterType, "settings");
                FieldInfo usageField = RequiredField(parameterType, "globalUsage");
                FieldInfo mapField = RequiredField(parameterType, "referenceMap");
                object settings = Activator.CreateInstance(settingsField.FieldType);
                foreach (PropertyInfo property in settingsField.FieldType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    report.settingsProperties.Add(new Property { name = property.Name, type = property.PropertyType.FullName, canWrite = property.CanWrite });
                Set(settings, "target", BuildTarget.StandaloneOSX, report);
                Set(settings, "group", BuildTargetGroup.Standalone, report);
                PropertyInfo flags = settingsField.FieldType.GetProperty("buildFlags", BindingFlags.Public | BindingFlags.Instance);
                if (flags == null || !flags.PropertyType.IsEnum) throw new InvalidOperationException("BuildSettings.buildFlags is not an available enum property");
                // Zero flags retains the native type trees; do not request DisableWriteTypeTree.
                Set(settings, "buildFlags", Enum.ToObject(flags.PropertyType, 0), report);
                object parameters = Activator.CreateInstance(parameterType);
                settingsField.SetValue(parameters, settings);
                usageField.SetValue(parameters, Activator.CreateInstance(usageField.FieldType));
                referenceMap = Activator.CreateInstance(mapField.FieldType);
                mapField.SetValue(parameters, referenceMap);
                report.serializationInvoked = true;
                object result = writer.Invoke(null, new[] { (object)report.outputFolder, parameters });
                report.writeResultType = result == null ? "null" : result.GetType().FullName;
                foreach (string file in Directory.GetFiles(report.outputFolder, "*", SearchOption.AllDirectories))
                    report.outputs.Add(new Output { path = file, bytes = new FileInfo(file).Length });
                if (report.outputs.Count == 0) throw new InvalidOperationException("Manager writer produced no output files");
                report.passed = true;
            }
            catch (Exception error)
            {
                var invocation = error as TargetInvocationException;
                Exception cause = invocation != null && invocation.InnerException != null ? invocation.InnerException : error;
                report.failure = cause.GetType().Name + ": " + cause.Message;
            }
            finally
            {
                var disposable = referenceMap as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
            File.WriteAllText(Path.Combine(evidence, "player-export.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[RETURN] PLAYER AUDIO SCHEMA " + (report.passed ? "PASS" : "FAIL") + " " + report.failure);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static FieldInfo RequiredField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) throw new InvalidOperationException("Verified manager parameter field is unavailable: " + name);
            return field;
        }

        static void Set(object settings, string name, object value, Report report)
        {
            PropertyInfo property = settings.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite) throw new InvalidOperationException("BuildSettings setter is unavailable: " + name);
            property.SetValue(settings, value);
            foreach (Property record in report.settingsProperties)
                if (record.name == name) record.assignedValue = Convert.ToString(property.GetValue(settings));
        }
    }
}
