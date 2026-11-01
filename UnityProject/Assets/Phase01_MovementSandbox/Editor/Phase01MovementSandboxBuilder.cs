using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase01.MovementSandbox.Editor
{
    public static class Phase01MovementSandboxBuilder
    {
        public const string ScenePath = "Assets/Scenes/Phase01_MovementSandbox.unity";
        private const string MaterialPath = "Assets/Phase01_MovementSandbox/Runtime/Phase01SandboxUnlit.mat";
        private const string PhysicsMaterialPath = "Assets/Phase01_MovementSandbox/Runtime/Phase01SandboxNoFriction.physicsMaterial";
        private static Material material;

        [MenuItem("Tools/Codex/Phase01 Movement Sandbox/Build Scene")]
        public static void BuildScene()
        {
            material = GetOrCreateMaterial();
            PhysicsMaterial noFriction = GetOrCreatePhysicsMaterial();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.35f, 0.42f);

            Transform environment = new GameObject("Sandbox Greybox Course").transform;
            Platform("Start Platform", environment, new Vector3(0f, -0.5f, 0f), new Vector3(12f, 1f, 3f), new Color(0.05f, 0.2f, 0.22f));
            GameObject lowStep = Platform("Low Traversal Ramp", environment, new Vector3(-0.8f, 0.09f, 0f), new Vector3(2.4f, 0.18f, 3f), new Color(0.18f, 0.55f, 0.48f));
            lowStep.transform.rotation = Quaternion.Euler(0f, 0f, 4f);
            Platform("After Gap Platform", environment, new Vector3(12.4f, -0.5f, 0f), new Vector3(7.2f, 1f, 3f), new Color(0.05f, 0.2f, 0.22f));
            Platform("Raised Ledge", environment, new Vector3(20f, 0.6f, 0f), new Vector3(6f, 1.2f, 3f), new Color(0.07f, 0.27f, 0.27f));
            Platform("Final Landing", environment, new Vector3(34f, 0.5f, 0f), new Vector3(8f, 1f, 3f), new Color(0.05f, 0.2f, 0.22f));
            Primitive("Background Base", PrimitiveType.Cube, environment, new Vector3(18f, -6.2f, 2f), new Vector3(52f, 0.4f, 8f), new Color(0.015f, 0.025f, 0.045f), false);

            GameObject movingObject = Platform("Moving Platform", environment, new Vector3(26f, 0.8f, 0f), new Vector3(3f, 0.5f, 3f), new Color(0.96f, 0.58f, 0.16f));
            Rigidbody movingBody = movingObject.AddComponent<Rigidbody>();
            movingBody.isKinematic = true;
            movingBody.interpolation = RigidbodyInterpolation.Interpolate;
            Phase01MovingPlatform movingPlatform = movingObject.AddComponent<Phase01MovingPlatform>();
            movingPlatform.Configure(new Vector3(2f, 0f, 0f), 3f);

            GameObject player = new GameObject("Sandbox Player");
            player.transform.position = new Vector3(-4f, 0.05f, 0f);
            Rigidbody playerBody = player.AddComponent<Rigidbody>();
            playerBody.mass = 1f;
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.32f;
            capsule.height = 1.44f;
            capsule.center = Vector3.up * 0.72f;
            capsule.material = noFriction;
            Phase01ParkourMotor motor = player.AddComponent<Phase01ParkourMotor>();
            Primitive("Player Visual", PrimitiveType.Capsule, player.transform, player.transform.position + Vector3.up * 0.72f, new Vector3(0.64f, 0.72f, 0.64f), new Color(0.3f, 0.82f, 1f), false);

            GameObject cameraObject = new GameObject("Sandbox Grounded Side Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(-4f, 2.75f, -14f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.028f, 0.052f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.AddComponent<AudioListener>();
            Phase01SandboxCamera cameraRig = cameraObject.AddComponent<Phase01SandboxCamera>();
            cameraRig.Configure(player.transform);

            Transform checkpointRoot = new GameObject("Visible Checkpoints").transform;
            checkpointRoot.SetParent(environment);
            Phase01SandboxCheckpoint[] checkpoints =
            {
                Checkpoint("Start", checkpointRoot, new Vector3(-4f, 0.05f, 0f)),
                Checkpoint("AfterGap", checkpointRoot, new Vector3(10f, 0.05f, 0f)),
                Checkpoint("BeforeMovingPlatform", checkpointRoot, new Vector3(22f, 1.25f, 0f))
            };

            GameObject systems = new GameObject("Movement Sandbox Systems");
            Phase01SandboxReset reset = systems.AddComponent<Phase01SandboxReset>();
            reset.Configure(motor, cameraRig, checkpoints);
            foreach (Phase01SandboxCheckpoint checkpoint in checkpoints)
                checkpoint.SetResetController(reset);
            Phase01SandboxPerformanceMonitor performance = systems.AddComponent<Phase01SandboxPerformanceMonitor>();
            systems.AddComponent<Phase01SandboxDebugOverlay>().Configure(motor, reset, performance);
            systems.AddComponent<Phase01SandboxAcceptanceDriver>().Configure(
                motor, reset, cameraRig, movingPlatform, checkpoints, performance);

            GameObject resetVolume = new GameObject("Soft Reset Volume");
            resetVolume.transform.SetParent(environment);
            resetVolume.transform.position = new Vector3(18f, -3.5f, 0f);
            BoxCollider resetCollider = resetVolume.AddComponent<BoxCollider>();
            resetCollider.size = new Vector3(60f, 2f, 6f);
            resetCollider.isTrigger = true;
            resetVolume.AddComponent<Phase01SandboxResetVolume>().Configure(reset);

            Transform labels = new GameObject("Development Course Markers").transform;
            labels.SetParent(environment);
            Marker("SHORT GAP", labels, new Vector3(7.4f, 2.2f, 0f));
            Marker("RAISED LEDGE", labels, new Vector3(20f, 3.2f, 0f));
            Marker("MOVING PLATFORM", labels, new Vector3(28f, 3.3f, 0f));
            Marker("FINAL LANDING", labels, new Vector3(34f, 3.2f, 0f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScene();
            Selection.activeGameObject = systems;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase01MovementSandbox] Built {ScenePath} with isolated serialized references.");
        }

        [MenuItem("Tools/Codex/Phase01 Movement Sandbox/Build macOS Standalone")]
        public static void BuildStandalone()
        {
            if (!File.Exists(ScenePath))
                BuildScene();
            string output = Path.Combine(Path.GetTempPath(), "Phase01_MovementSandbox.app");
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.CleanBuildCache
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Sandbox standalone build failed: {report.summary.result}");
            Debug.Log($"[Phase01MovementSandbox] Standalone build succeeded: {output}");
        }

        private static GameObject Platform(string name, Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            return Primitive(name, PrimitiveType.Cube, parent, position, scale, color, true);
        }

        private static GameObject Primitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool keepCollider)
        {
            GameObject target = GameObject.CreatePrimitive(type);
            target.name = name;
            target.transform.SetParent(parent);
            target.transform.position = position;
            target.transform.localScale = scale;
            if (!keepCollider)
            {
                Collider collider = target.GetComponent<Collider>();
                if (collider != null)
                    Object.DestroyImmediate(collider);
            }
            target.GetComponent<Renderer>().sharedMaterial = material;
            target.AddComponent<SandboxTint>().Configure(color);
            return target;
        }

        private static Phase01SandboxCheckpoint Checkpoint(string id, Transform parent, Vector3 position)
        {
            GameObject root = new GameObject($"Checkpoint {id}");
            root.transform.SetParent(parent);
            root.transform.position = position;
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 1f, 0f);
            trigger.size = new Vector3(1.2f, 2.4f, 3f);
            trigger.isTrigger = true;
            Transform respawn = new GameObject("Respawn Point").transform;
            respawn.SetParent(root.transform, false);
            GameObject marker = Primitive("Checkpoint Marker", PrimitiveType.Cylinder, root.transform,
                position + Vector3.up * 1.4f, new Vector3(0.12f, 1.4f, 0.12f), new Color(0.25f, 0.68f, 0.72f), false);
            return root.AddComponent<Phase01SandboxCheckpoint>().ConfigureAndReturn(id, respawn, marker.GetComponent<Renderer>());
        }

        private static void Marker(string text, Transform parent, Vector3 position)
        {
            GameObject target = new GameObject(text);
            target.transform.SetParent(parent);
            target.transform.position = position;
            TextMesh label = target.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = text;
            label.fontSize = 36;
            label.characterSize = 0.08f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.75f, 0.86f, 0.92f);
        }

        private static void EnsureBuildScene()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(item => item.path != ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Material GetOrCreateMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Material created = new Material(shader) { name = "Phase01 Sandbox Unlit" };
            created.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(created, MaterialPath);
            return created;
        }

        private static PhysicsMaterial GetOrCreatePhysicsMaterial()
        {
            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
            if (existing != null)
                return existing;
            PhysicsMaterial created = new PhysicsMaterial("Phase01 Sandbox No Friction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(created, PhysicsMaterialPath);
            return created;
        }
    }

    internal static class CheckpointBuilderExtensions
    {
        public static Phase01SandboxCheckpoint ConfigureAndReturn(
            this Phase01SandboxCheckpoint checkpoint,
            string id,
            Transform point,
            Renderer marker)
        {
            checkpoint.Configure(id, point, marker);
            return checkpoint;
        }
    }
}
