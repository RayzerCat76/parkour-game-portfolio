using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnAudioBuildApiInspection
    {
        [Serializable] sealed class Parameter { public string name, type; }
        [Serializable] sealed class Method
        {
            public string name, returnType, obsoleteMessage;
            public bool isPublic, isObsolete, obsoleteIsError, nativeThrows;
            public List<Parameter> parameters = new List<Parameter>();
        }
        [Serializable] sealed class TypeInfo
        {
            public string name;
            public bool isEnum;
            public List<Parameter> fields = new List<Parameter>();
            public List<string> enumValues = new List<string>();
        }
        [Serializable] sealed class Report
        {
            public string unityVersion, apiType, failure;
            public bool passed, buildInProgress;
            public List<Method> methods = new List<Method>();
            public List<TypeInfo> parameterTypes = new List<TypeInfo>();
        }

        public static void Run()
        {
            var report = new Report { unityVersion = Application.unityVersion };
            string project = Directory.GetParent(Application.dataPath).FullName;
            string root = Directory.GetParent(project).FullName;
            string evidence = Path.Combine(root, "Redesign/Evidence/v335");
            Directory.CreateDirectory(evidence);
            try
            {
                if (Application.unityVersion != "6000.4.11f1") throw new InvalidOperationException("Unexpected editor version");
                Type api = typeof(AssetDatabase).Assembly.GetType("UnityEditor.Build.Content.ContentBuildInterface", false);
                if (api == null) throw new InvalidOperationException("Installed ContentBuildInterface type was not found");
                report.apiType = api.FullName;
                MethodInfo progress = api.GetMethod("IsBuildInProgress", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                if (progress == null || progress.GetParameters().Length != 0) throw new InvalidOperationException("Installed build-progress query is unavailable");
                report.buildInProgress = (bool)progress.Invoke(null, null);
                var visited = new HashSet<Type>();
                bool gameManagers = false;
                foreach (MethodInfo method in api.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (!method.Name.Contains("GameManagersSerializedFile") && method.Name != "WriteSerializedFile" && method.Name != "WriteSerializedFile_Internal") continue;
                    if (method.Name.Contains("GameManagersSerializedFile")) gameManagers = true;
                    var record = new Method { name = method.Name, returnType = method.ReturnType.FullName, isPublic = method.IsPublic };
                    foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                    {
                        if (attribute.AttributeType == typeof(ObsoleteAttribute))
                        {
                            record.isObsolete = true;
                            if (attribute.ConstructorArguments.Count > 0) record.obsoleteMessage = Convert.ToString(attribute.ConstructorArguments[0].Value);
                            if (attribute.ConstructorArguments.Count > 1) record.obsoleteIsError = Convert.ToBoolean(attribute.ConstructorArguments[1].Value);
                        }
                        if (attribute.AttributeType.Name == "NativeThrowsAttribute") record.nativeThrows = true;
                    }
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        record.parameters.Add(new Parameter { name = parameter.Name, type = parameter.ParameterType.FullName });
                        Describe(parameter.ParameterType, 0, visited, report.parameterTypes);
                    }
                    report.methods.Add(record);
                }
                if (!gameManagers) throw new InvalidOperationException("Installed game-manager serialization method was not found");
                report.passed = true;
            }
            catch (Exception error) { report.failure = error.GetType().Name + ": " + error.Message; }
            File.WriteAllText(Path.Combine(evidence, "build-api.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[RETURN] AUDIO BUILD API " + (report.passed ? "PASS" : "FAIL") + " " + report.failure);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static void Describe(Type type, int depth, HashSet<Type> visited, List<TypeInfo> records)
        {
            if (type.IsByRef || type.IsArray) type = type.GetElementType();
            if (type == null || depth > 2 || type.IsPrimitive || type == typeof(string) || !visited.Add(type)) return;
            var record = new TypeInfo { name = type.FullName, isEnum = type.IsEnum };
            records.Add(record);
            if (type.IsEnum)
            {
                foreach (string name in Enum.GetNames(type))
                {
                    if (type.Namespace != "UnityEditor.Build.Content" && name != "StandaloneOSX" && name != "Standalone") continue;
                    record.enumValues.Add(name + "=" + Convert.ToInt64(Enum.Parse(type, name), CultureInfo.InvariantCulture));
                }
                return;
            }
            if (type.Namespace != "UnityEditor.Build.Content") return;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                record.fields.Add(new Parameter { name = field.Name, type = field.FieldType.FullName });
                Describe(field.FieldType, depth + 1, visited, records);
            }
        }
    }
}
