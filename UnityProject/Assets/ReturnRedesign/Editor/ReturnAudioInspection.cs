using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnAudioInspection
    {
        [Serializable] sealed class Field
        {
            public string path, type, value;
        }
        [Serializable] sealed class Report
        {
            public string unityVersion, input, schema, objectType, objectName, schemaObjectType, failure;
            public bool passed, sourceAudioUnchanged, schemaObjectPersistent;
            public string disableAudio, editorStandaloneSubtarget;
            public List<Field> fields = new List<Field>();
        }
        static Report report;
        static string evidence, sourceAudio;
        static byte[] sourceBefore;

        public static void Run()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string root = Directory.GetParent(project).FullName;
            evidence = Path.Combine(root, "Redesign/Evidence/v331");
            Directory.CreateDirectory(evidence);
            sourceAudio = Path.Combine(project, "ProjectSettings/AudioManager.asset");
            report = new Report {
                unityVersion = Application.unityVersion,
                input = sourceAudio,
                schema = Path.Combine(evidence, "AudioManager-schema.bin")
            };
            try
            {
                sourceBefore = File.ReadAllBytes(sourceAudio);
                if (Application.unityVersion != "6000.4.11f1")
                    throw new InvalidOperationException("Editor version does not match the existing player");
                var subtarget = typeof(EditorUserBuildSettings).GetProperty("standaloneBuildSubtarget");
                report.editorStandaloneSubtarget = subtarget == null ? "unavailable" : Convert.ToString(subtarget.GetValue(null));
                UnityEngine.Object audio = null;
                foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset"))
                {
                    if (candidate == null) continue;
                    using (var serializedCandidate = new SerializedObject(candidate))
                        if (serializedCandidate.FindProperty("m_DisableAudio") != null) { audio = candidate; break; }
                }
                if (audio == null) throw new InvalidOperationException("Source settings expose no AudioManager object");
                report.objectType = audio.GetType().FullName;
                report.objectName = audio.name;
                using (var serialized = new SerializedObject(audio))
                {
                    var field = serialized.GetIterator();
                    while (field.Next(true) && report.fields.Count < 100)
                    {
                        string value;
                        switch (field.propertyType)
                        {
                            case SerializedPropertyType.Boolean: value = field.boolValue ? "true" : "false"; break;
                            case SerializedPropertyType.Integer:
                            case SerializedPropertyType.Enum: value = field.intValue.ToString(CultureInfo.InvariantCulture); break;
                            case SerializedPropertyType.Float: value = field.floatValue.ToString("R", CultureInfo.InvariantCulture); break;
                            case SerializedPropertyType.String: value = field.stringValue; break;
                            default: value = "unread"; break;
                        }
                        report.fields.Add(new Field { path = field.propertyPath, type = field.propertyType.ToString(), value = value });
                    }
                    var disabled = serialized.FindProperty("m_DisableAudio");
                    if (disabled == null) throw new InvalidOperationException("Loaded object exposes no m_DisableAudio property");
                    report.disableAudio = disabled.propertyType == SerializedPropertyType.Boolean
                        ? (disabled.boolValue ? "true" : "false") : disabled.intValue.ToString(CultureInfo.InvariantCulture);
                }
                UnityEngine.Object transient = UnityEngine.Object.Instantiate(audio);
                if (transient == null) throw new InvalidOperationException("Unity did not clone the source AudioManager");
                report.schemaObjectType = transient.GetType().FullName;
                report.schemaObjectPersistent = EditorUtility.IsPersistent(transient);
                if (transient == audio || report.schemaObjectPersistent)
                    throw new InvalidOperationException("AudioManager clone is not an independent transient object");
                try { InternalEditorUtility.SaveToSerializedFileAndForget(new[] { transient }, report.schema, false); }
                finally { UnityEngine.Object.DestroyImmediate(transient); }
                if (!File.Exists(report.schema)) throw new InvalidOperationException("AudioManager schema binary was not written");
                report.passed = true;
                Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        static void Finish(Exception error)
        {
            if (error != null) { report.passed = false; report.failure = error.GetType().Name + ": " + error.Message; }
            try
            {
                byte[] after = File.ReadAllBytes(sourceAudio);
                report.sourceAudioUnchanged = sourceBefore != null && after.Length == sourceBefore.Length;
                if (report.sourceAudioUnchanged)
                    for (int i = 0; i < after.Length; i++)
                        if (after[i] != sourceBefore[i]) { report.sourceAudioUnchanged = false; break; }
                if (!report.sourceAudioUnchanged) { report.passed = false; report.failure = "Source AudioManager changed during inspection"; }
                File.WriteAllText(Path.Combine(evidence, "editor-audio-inspection.json"), JsonUtility.ToJson(report, true));
            }
            catch (Exception writeError) { Debug.LogError(writeError.Message); report.passed = false; }
            Debug.Log("[RETURN] AUDIO INSPECTION " + (report.passed ? "PASS" : "FAIL") + " " + report.failure);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }
    }
}
