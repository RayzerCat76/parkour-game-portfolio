using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnAudioFoundationInspection
    {
        [Serializable] sealed class EnumField
        {
            public string path, type, selectedName;
            public int value, index;
            public string[] names, displayNames;
        }
        [Serializable] sealed class Report
        {
            public bool passed, sourceAudioUnchanged;
            public string unityVersion, failure;
            public List<EnumField> fields = new List<EnumField>();
            public List<string> runtimeApis = new List<string>();
            public List<string> relatedEnums = new List<string>();
        }

        public static void Run()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string root = Directory.GetParent(project).FullName;
            string evidence = Path.Combine(root, "Redesign/Evidence/v339");
            string source = Path.Combine(project, "ProjectSettings/AudioManager.asset");
            Directory.CreateDirectory(evidence);
            byte[] before = File.ReadAllBytes(source);
            var report = new Report { unityVersion = Application.unityVersion };
            try
            {
                if (Application.unityVersion != "6000.4.11f1")
                    throw new InvalidOperationException("Installed editor does not match the compiled player");
                UnityEngine.Object audio = null;
                foreach (var candidate in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset"))
                {
                    if (candidate == null) continue;
                    using (var serialized = new SerializedObject(candidate))
                        if (serialized.FindProperty("m_AudioFoundation") != null) { audio = candidate; break; }
                }
                if (audio == null) throw new InvalidOperationException("AudioManager foundation property unavailable");
                using (var serialized = new SerializedObject(audio))
                {
                    foreach (string path in new[] { "m_AudioFoundation", "m_OutputChannelLayout", "m_OutputSamplingRate" })
                    {
                        var property = serialized.FindProperty(path);
                        if (property == null) throw new InvalidOperationException("Missing property: " + path);
                        var field = new EnumField {
                            path = path, type = property.propertyType.ToString(), value = property.intValue,
                            index = -1, names = new string[0], displayNames = new string[0], selectedName = "unavailable"
                        };
                        if (property.propertyType == SerializedPropertyType.Enum)
                        {
                            field.index = property.enumValueIndex;
                            field.names = property.enumNames;
                            field.displayNames = property.enumDisplayNames;
                            if (field.index >= 0 && field.index < field.names.Length)
                                field.selectedName = field.names[field.index];
                        }
                        report.fields.Add(field);
                    }
                }
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
                InspectApis(typeof(AudioSettings), flags, report.runtimeApis);
                foreach (var nested in typeof(AudioSettings).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                    InspectApis(nested, flags, report.runtimeApis);
                foreach (var type in typeof(AudioSettings).Assembly.GetTypes())
                {
                    if (!type.IsEnum || type.FullName == null || !type.FullName.Contains("Audio")) continue;
                    if (!type.Name.Contains("Foundation") && !type.Name.Contains("Output") && !type.Name.Contains("Device")) continue;
                    foreach (string name in Enum.GetNames(type))
                        report.relatedEnums.Add(type.FullName + "." + name + "=" + Convert.ToInt64(Enum.Parse(type, name)));
                }
                report.passed = true;
            }
            catch (Exception error) { report.failure = error.GetType().Name + ": " + error.Message; }
            byte[] after = File.ReadAllBytes(source);
            report.sourceAudioUnchanged = before.Length == after.Length;
            if (report.sourceAudioUnchanged)
                for (int i = 0; i < before.Length; i++)
                    if (before[i] != after[i]) { report.sourceAudioUnchanged = false; break; }
            if (!report.sourceAudioUnchanged) { report.passed = false; report.failure = "AudioManager changed during read-only inspection"; }
            File.WriteAllText(Path.Combine(evidence, "audio-foundation-inspection.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[RETURN] AUDIO FOUNDATION INSPECTION " + (report.passed ? "PASS" : "FAIL") + " " + report.failure);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static void InspectApis(Type type, BindingFlags flags, List<string> destination)
        {
            foreach (var member in type.GetMembers(flags))
            {
                string name = member.Name;
                if (!name.Contains("Output") && !name.Contains("Configuration") && !name.Contains("Reset") &&
                    !name.Contains("Started") && !name.Contains("Shutdown") && !name.Contains("SampleRate") &&
                    !name.Contains("DSP") && !name.Contains("dspTime") && !name.Contains("Foundation") &&
                    !name.Contains("Suspend") && !name.Contains("Initialize")) continue;
                string access = member is MethodBase method ? (method.IsPublic ? "public" : "nonpublic") : member.MemberType.ToString();
                destination.Add(type.FullName + " " + access + " " + member);
            }
            destination.Sort(StringComparer.Ordinal);
        }
    }
}
