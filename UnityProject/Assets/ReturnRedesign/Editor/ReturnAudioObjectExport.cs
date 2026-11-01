using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnAudioObjectExport
    {
        [Serializable] sealed class Output { public string path; public long bytes; }
        [Serializable] sealed class ResultMember { public string name, type, value; }
        [Serializable] sealed class Report
        {
            public string unityVersion, outputFolder, sourceType, identifier, failure, stage;
            public bool passed, serializationInvoked;
            public List<Output> outputs = new List<Output>();
            public List<ResultMember> writerResult = new List<ResultMember>();
        }

        public static void Run()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string root = Directory.GetParent(project).FullName;
            string evidence = Path.Combine(root, "Redesign/Evidence/v338");
            var report = new Report { unityVersion = Application.unityVersion,
                outputFolder = Path.Combine(evidence, "AudioOnly"), stage = "source-object" };
            Directory.CreateDirectory(report.outputFolder);
            object map = null, usage = null;
            try
            {
                if (Application.unityVersion != "6000.4.11f1") throw new InvalidOperationException("Unexpected editor version");
                UnityEngine.Object audio = null;
                foreach (UnityEngine.Object candidate in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset"))
                {
                    if (candidate == null) continue;
                    using (var serialized = new SerializedObject(candidate))
                        if (serialized.FindProperty("m_DisableAudio") != null) { audio = candidate; break; }
                }
                if (audio == null) throw new InvalidOperationException("Source AudioManager object was not found");
                report.sourceType = audio.GetType().FullName;
                if (!ObjectIdentifier.TryGetObjectIdentifier(audio, out ObjectIdentifier identifier))
                    throw new InvalidOperationException("Source AudioManager has no serialization object identifier");
                report.identifier = identifier.ToString();
                report.stage = "arguments";
                object parameters = new WriteParameters();
                Type parameterType = parameters.GetType();
                Type settingsType = Field(parameterType, "settings").FieldType;
                object settings = Activator.CreateInstance(settingsType);
                Set(settings, "target", BuildTarget.StandaloneOSX);
                Set(settings, "group", BuildTargetGroup.Standalone);
                PropertyInfo flags = settingsType.GetProperty("buildFlags", BindingFlags.Public | BindingFlags.Instance);
                if (flags == null || !flags.PropertyType.IsEnum) throw new InvalidOperationException("Build flags property is unavailable");
                Set(settings, "buildFlags", Enum.ToObject(flags.PropertyType, 0));
                Set(parameters, "settings", settings);
                Set(parameters, "globalUsage", Activator.CreateInstance(Field(parameterType, "globalUsage").FieldType));
                map = Activator.CreateInstance(Field(parameterType, "referenceMap").FieldType);
                ((BuildReferenceMap)map).AddMapping("audio-manager", 1L, identifier);
                Set(parameters, "referenceMap", map);
                usage = Activator.CreateInstance(Field(parameterType, "usageSet").FieldType);
                Set(parameters, "usageSet", usage);
                object command = Activator.CreateInstance(Field(parameterType, "writeCommand").FieldType);
                Set(command, "fileName", "audio-manager");
                Set(command, "internalName", "audio-manager");
                Type objectsType = MemberType(command.GetType(), "serializeObjects");
                if (!objectsType.IsGenericType || objectsType.GetGenericTypeDefinition() != typeof(List<>))
                    throw new InvalidOperationException("WriteCommand.serializeObjects is not the declared List<SerializationInfo>");
                Type infoType = objectsType.GetGenericArguments()[0];
                object info = Activator.CreateInstance(infoType);
                Set(info, "serializationObject", identifier);
                Set(info, "serializationIndex", 1L);
                var objects = (System.Collections.IList)Activator.CreateInstance(objectsType);
                objects.Add(info);
                Set(command, "serializeObjects", objects);
                Set(parameters, "writeCommand", command);
                report.stage = "before-native-writer";
                File.WriteAllText(Path.Combine(evidence, "export-arguments.json"), JsonUtility.ToJson(report, true));
                report.serializationInvoked = true;
                WriteResult result = ContentBuildInterface.WriteSerializedFile(report.outputFolder, (WriteParameters)parameters);
                CaptureResult(result, report.writerResult);
                report.stage = "writer-returned";
                File.WriteAllText(Path.Combine(evidence, "writer-return.json"), JsonUtility.ToJson(report, true));
                foreach (string file in Directory.GetFiles(report.outputFolder, "*", SearchOption.AllDirectories))
                    report.outputs.Add(new Output { path = file, bytes = new FileInfo(file).Length });
                if (report.outputs.Count == 0) throw new InvalidOperationException("Single-object writer produced no files");
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
                var disposableUsage = usage as IDisposable;
                if (disposableUsage != null) disposableUsage.Dispose();
                var disposableMap = map as IDisposable;
                if (disposableMap != null) disposableMap.Dispose();
            }
            File.WriteAllText(Path.Combine(evidence, "audio-object-export.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[RETURN] AUDIO OBJECT EXPORT " + (report.passed ? "PASS" : "FAIL") + " " + report.failure);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) throw new InvalidOperationException("Required parameter field is unavailable: " + name);
            return field;
        }
        static void CaptureResult(object result, List<ResultMember> members)
        {
            Type type = result.GetType();
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                AddResultMember(field.Name, field.FieldType, field.GetValue(result), members);
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                if (property.CanRead && property.GetIndexParameters().Length == 0)
                    AddResultMember(property.Name, property.PropertyType, property.GetValue(result), members);
        }
        static void AddResultMember(string name, Type type, object value, List<ResultMember> members)
        {
            string summary = value == null ? "null" : Convert.ToString(value);
            var collection = value as System.Collections.ICollection;
            if (collection != null) summary = "count=" + collection.Count;
            members.Add(new ResultMember { name = name, type = type.FullName, value = summary });
        }
        static Type MemberType(Type type, string name)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.FieldType;
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) throw new InvalidOperationException("Required serialization member is unavailable: " + name);
            return property.PropertyType;
        }
        static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) { field.SetValue(target, value); return; }
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !property.CanWrite) throw new InvalidOperationException("Required serialization setter is unavailable: " + name);
            property.SetValue(target, value);
        }
    }
}
