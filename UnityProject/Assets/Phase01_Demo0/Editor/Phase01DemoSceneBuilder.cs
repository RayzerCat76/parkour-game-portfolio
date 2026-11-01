using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Phase01.MovementSandbox;

namespace Phase01.Demo0.Editor
{
    public static class Phase01DemoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Phase01_Demo0.unity";
        private const string PlaceholderMaterialPath = "Assets/Phase01_Demo0/Runtime/Phase01PlaceholderUnlit.mat";
        private const string PhysicsMaterialPath = "Assets/Phase01_Demo0/Runtime/Phase01DemoNoFriction.physicsMaterial";
        private const string Branch3SupportTintPath = "Assets/Phase01_Demo0/GeneratedMaterials/Tint_F2A82EFF.mat";
        private const string Branch4CollapseTintPath = "Assets/Phase01_Demo0/GeneratedMaterials/Tint_C7613DFF.mat";
        /// <summary>How much bigger than the gameplay frame the painted backdrop panel is.</summary>
        private const float BackdropMargin = 1.35f;
        /// <summary>How far in front of the camera the painted backdrop sits, in metres.</summary>
        private const float BackdropDistance = 32f;
        private const string SceneBuildSignatureObjectName = "Phase01 Generated Scene Signature TASK007 BranchOneInteractionBeats V74 TeachBeforeTighten";
        private static Material placeholderMaterial;
        private static readonly Vector3[] Directions =
        {
            Vector3.left,
            Vector3.forward,
            Vector3.right,
            Vector3.back
        };
        private static readonly float[] ProfileScalars = { 0f, 1.4f, 44f, 88f, 132f, 174f, 178f };
        private static readonly float[] ProfileHeights = { 0f, 0f, 0.2f, 0.35f, 0.25f, 0.2f, 0.15f };
        private static readonly float[] ProfileHalfWidths = ConstantWidths(ProfileScalars.Length, 0.72f);
        // TASK 005/008 Branch 1 "Fracture Ascent": elevation-led route, 0 m to +9 m, eleven authored jump
        // gaps plus four authored voids - a required riser lift, a ferry, and two air currents that are
        // ridden by holding jump. Branch 1 now spends its length on mechanics rather than on another run
        // of small jumps. Every profile array below is taken straight from Phase01BranchTerrain, so the
        // serialized scene and the runtime data can never drift apart.
        private const float Branch1ParticleScalar = 109.1f;
        private const float Branch2ParticleScalar = 131f;
        private static readonly float[] Branch1Scalars = Phase01BranchTerrain.AuthoredScalars(0);
        private static readonly float[] Branch1Heights = Phase01BranchTerrain.AuthoredHeights(0);
        private static readonly float[] Branch1HalfWidths = Phase01BranchTerrain.AuthoredWidths(0);
        private static readonly bool[] Branch1Gaps = GapFlagsFor(Branch1Scalars, Phase01BranchTerrain.AuthoredGapBounds(0));
        // TASK 006 Branch 2 "Fault Ascent": rung 2 of the difficulty ladder, kept byte-identical to
        // Phase01BranchTerrain.Branch2* so the serialized scene cannot drift from the runtime data.
        private static readonly float[] Branch2Scalars = Phase01BranchTerrain.AuthoredScalars(1);
        private static readonly float[] Branch2Heights = Phase01BranchTerrain.AuthoredHeights(1);
        private static readonly float[] Branch2HalfWidths = Phase01BranchTerrain.AuthoredWidths(1);
        private static readonly bool[] Branch2Gaps = GapFlagsFor(Branch2Scalars, Phase01BranchTerrain.AuthoredGapBounds(1));
        // TASK 006 Branch 3 "Fading Gauntlet": rung 3, kept byte-identical to Phase01BranchTerrain.Branch3*.
        private static readonly float[] Branch3Scalars = Phase01BranchTerrain.AuthoredScalars(2);
        private static readonly float[] Branch3Heights = Phase01BranchTerrain.AuthoredHeights(2);
        private static readonly float[] Branch3HalfWidths = Phase01BranchTerrain.AuthoredWidths(2);
        private static readonly bool[] Branch3Gaps = GapFlagsFor(Branch3Scalars, Phase01BranchTerrain.AuthoredGapBounds(2));
        private static readonly float[] Branch4Scalars = Phase01BranchTerrain.AuthoredScalars(3);
        private static readonly float[] Branch4Heights = Phase01BranchTerrain.AuthoredHeights(3);
        private static readonly float[] Branch4HalfWidths = Phase01BranchTerrain.AuthoredWidths(3);
        private static readonly bool[] Branch4Gaps = GapFlagsFor(Branch4Scalars, Phase01BranchTerrain.AuthoredGapBounds(3));

        [MenuItem("Tools/Codex/Phase01 Demo/Build Scene")]
        public static void BuildScene()
        {
            if (HasCurrentSceneBuildSignature())
            {
                EnsureSceneInBuildSettings();
                Debug.Log($"[Phase01Demo] {ScenePath} already matches {SceneBuildSignatureObjectName}.");
                return;
            }

            placeholderMaterial = GetOrCreatePlaceholderMaterial();
            PhysicsMaterial playerPhysicsMaterial = GetCanonicalPhysicsMaterial();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.58f, 0.72f);
            // The level is drawn with unlit materials, so the only levers that give it depth are the sky it
            // sits against and the fog that separates near ledges from far ones. Without them the branches
            // read as flat slabs floating in a black void, which is what made the greybox look unfinished.
            RenderSettings.skybox = GetOrCreateSkyboxMaterial();
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // The haze is authored brighter than the level's own stone. That inversion is the whole look of
            // the phase: pale luminous air behind dark carved silhouettes, which is the Hollow Knight
            // register of a world much larger and vaguer than the path through it. A dark fog behind pale
            // ledges (the first two passes) is what made the level read as grey slabs in a void.
            RenderSettings.fogColor = new Color(0.52f, 0.58f, 0.68f);
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 100f;

            GameObject environment = new GameObject("Environment");
            // There is deliberately no ground plane. One was authored here (a 4000 m slab under the level)
            // and it did real damage to the look: with an orthographic camera pitched 5 degrees down, that
            // slab's surface covers the *whole* frame above the ledges, so every background in the demo was
            // one flat expanse of fogged slab - which is exactly the "flat, dead background" the art was
            // being judged on. The distance is painted instead (see the backdrop panel below), and the void
            // the player falls into is the fog itself, as the design sheet's side-on drawings show.
            Transform branches = new GameObject("Four Long Isolated Sloped Terrain Ledges").transform;
            branches.SetParent(environment.transform);
            Phase01BranchTerrain[] branchTerrains = new Phase01BranchTerrain[Directions.Length];
            GameObject branch2UpperGapColliders = null;
            for (int i = 0; i < Directions.Length; i++)
            {
                Transform branchRoot = new GameObject($"Branch {i + 1} Sloped Terrain Ledge").transform;
                branchRoot.SetParent(branches);
                branchRoot.rotation = Quaternion.LookRotation(
                    Vector3.Cross(Directions[i], Vector3.up),
                    Vector3.up);
                // Solid white, per the Phase 1 design sheet. The form comes from the baked sun shading
                // rather than from colour, and the faint alternation keeps neighbouring branches from
                // merging into one silhouette.
                // Pale carved stone, with the walls dropped well below the tops: in a dark, hazy level the
                // silhouette carries the read, so the contrast between a lit top and a falling wall matters
                // more than the absolute brightness.
                // White content, per the Phase 1 sheet. The wall is authored as the same pale stone as the
                // top: the value it actually shows comes from the height ramp and the sun bake in the
                // shared surface gradient, which takes it from white at the lip to near black a few metres
                // below. Painting the wall grey here as well would double up on that falloff and flatten
                // the contrast the sheet asks for.
                Color topColor = i % 2 == 0
                    ? new Color(0.97f, 0.98f, 1f)
                    : new Color(0.92f, 0.94f, 0.98f);
                Color sideColor = i % 2 == 0
                    ? new Color(0.86f, 0.9f, 0.98f)
                    : new Color(0.8f, 0.84f, 0.92f);

                GameObject topObject = new GameObject($"Branch {i + 1} Sloped Walkable Top");
                topObject.transform.SetParent(branchRoot, false);
                MeshFilter topFilter = topObject.AddComponent<MeshFilter>();
                MeshRenderer topRenderer = topObject.AddComponent<MeshRenderer>();
                MeshCollider topCollider = topObject.AddComponent<MeshCollider>();

                GameObject cliffObject = new GameObject($"Branch {i + 1} Irregular Cliff Face");
                cliffObject.transform.SetParent(branchRoot, false);
                MeshFilter cliffFilter = cliffObject.AddComponent<MeshFilter>();
                MeshRenderer cliffRenderer = cliffObject.AddComponent<MeshRenderer>();
                MeshCollider cliffCollider = cliffObject.AddComponent<MeshCollider>();

                Phase01BranchTerrain terrain = branchRoot.gameObject.AddComponent<Phase01BranchTerrain>();
                float[] scalars = i == 0 ? (float[])Branch1Scalars.Clone()
                    : i == 1 ? (float[])Branch2Scalars.Clone()
                    : i == 2 ? (float[])Branch3Scalars.Clone()
                    : i == 3 ? (float[])Branch4Scalars.Clone()
                    : (float[])ProfileScalars.Clone();
                float[] heights = i == 0 ? (float[])Branch1Heights.Clone()
                    : i == 1 ? (float[])Branch2Heights.Clone()
                    : i == 2 ? (float[])Branch3Heights.Clone()
                    : i == 3 ? (float[])Branch4Heights.Clone()
                    : (float[])ProfileHeights.Clone();
                float[] bottoms = BottomHeights(scalars.Length);
                float[] bottomScalars = BottomScalars(scalars);
                float[] widths = i == 0 ? (float[])Branch1HalfWidths.Clone()
                    : i == 1 ? (float[])Branch2HalfWidths.Clone()
                    : i == 2 ? (float[])Branch3HalfWidths.Clone()
                    : i == 3 ? (float[])Branch4HalfWidths.Clone()
                    : (float[])ProfileHalfWidths.Clone();
                bool[] gaps = i == 0 ? (bool[])Branch1Gaps.Clone()
                    : i == 1 ? (bool[])Branch2Gaps.Clone()
                    : i == 2 ? (bool[])Branch3Gaps.Clone()
                    : i == 3 ? (bool[])Branch4Gaps.Clone()
                    : new bool[scalars.Length - 1];
                terrain.Configure(
                    i,
                    Directions[i],
                    scalars,
                    heights,
                    bottoms,
                    bottomScalars,
                    widths,
                    gaps,
                    topFilter,
                    topRenderer,
                    topCollider,
                    cliffFilter,
                    cliffRenderer,
                    cliffCollider,
                    placeholderMaterial,
                    topColor,
                    sideColor);
                topRenderer.sharedMaterial = GetOrCreateTintMaterial(topColor);
                cliffRenderer.sharedMaterial = GetOrCreateTintMaterial(sideColor);
                if (i == 0)
                {
                    // Branch 1 first gap is now an authored elevation step, not a tutorial wall: the
                    // collision wall must follow the new lip so the required hop cannot be walked over.
                    AddGapLipCollider(branchRoot, "Branch 1 First Step Collision Wall", 7.81f, 0.75f, -6f, 0.72f, 0.2f);
                }
                else if (i == 1)
                {
                    branch2UpperGapColliders = new GameObject("Branch 2 Upper Gap Edge Colliders");
                    branch2UpperGapColliders.transform.SetParent(branchRoot, false);
                    AddGapLipCollider(branch2UpperGapColliders.transform, "Branch 2 Low Step Collision Wall", 13.5f, 0.25f, -6f, 0.76f, 0.3f);
                }
                AddLandingLamps(branchRoot, Directions[i], scalars, heights, widths, gaps);
                branchTerrains[i] = terrain;
            }

            ConfigureBranch2Transformation(branchTerrains[1], branch2UpperGapColliders);
            ConfigureBranch3FadingSupports(branchTerrains[2], playerPhysicsMaterial);
            ConfigureBranch4ControlledCollapse(branchTerrains[3], playerPhysicsMaterial);

            // A bright collar around the hub column, so the goal of the whole route reads from any branch.
            GameObject beacon = Primitive(
                "Central Hub Beacon",
                PrimitiveType.Cylinder,
                environment.transform,
                new Vector3(0f, -0.35f, 0f),
                new Vector3(4.1f, 0.22f, 4.1f),
                new Color(0.99f, 0.99f, 1f),
                false);
            beacon.transform.SetParent(environment.transform);

            // Global post-processing volume. The project's default profile ships Bloom switched off, so this
            // scene carries its own overrides: enough bloom for the yellow blocks and the radio core to glow
            // against the flat blue, and a light vignette to hold the frame together.
            GameObject volumeObject = new GameObject("Phase01 Post FX Volume");
            volumeObject.transform.SetParent(environment.transform);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = GetOrCreatePostFxProfile();

            // The painted distance is authored with the camera, further down.
            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(environment.transform);
            lightObject.transform.rotation = Quaternion.Euler(
                Phase01BranchTerrain.ShadingLightPitch,
                Phase01BranchTerrain.ShadingLightYaw,
                0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.88f);
            // The procedural sky needs to know which light is the sun, otherwise it renders a flat wash.
            RenderSettings.sun = light;

            GameObject cameraRigObject = new GameObject("Camera Rig (90 Degree POV)");
            Phase01CameraRig cameraRig = cameraRigObject.AddComponent<Phase01CameraRig>();
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameraRigObject.transform);
            cameraObject.transform.localPosition = new Vector3(0f, 4.8f, -14f);
            cameraObject.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.2f;
            // The skybox is part of the level's look: clearing to a solid colour here is why the branches
            // used to sit on a black background even after a sky was authored.
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.3f, 0.34f, 0.42f);
            camera.nearClipPlane = 0.1f;
            // Far enough to hold the layered skyline; the fog ends well before this, so nothing beyond reads.
            camera.farClipPlane = 260f;
            cameraObject.AddComponent<AudioListener>();

            // Air: drifting pale motes around the camera. Cheap, collider-free, deterministic.
            Transform motesRoot = new GameObject("Atmosphere Motes").transform;
            Transform[] motes = new Transform[48];
            for (int i = 0; i < motes.Length; i++)
            {
                float golden = i * 2.399963f;
                // Authored beyond the play plane - the camera sits 14 m back from the lane, so local z here
                // is measured from the camera along the view direction and 16-30 m is the far side of the
                // branch. Motes on the near side drift through the player and the ledges, which reads as the
                // player passing through floating props rather than as air. The field is turned with the
                // camera's yaw at runtime (Phase01AtmosphereMotes), so "beyond" stays beyond on all four
                // branches rather than only on the one that faces along Z.
                Vector3 scattered = new Vector3(
                    Mathf.Sin(golden) * 17f,
                    Mathf.Sin(golden * 1.7f) * 6.5f,
                    16f + Mathf.Abs(Mathf.Cos(golden * 0.7f)) * 14f);
                GameObject mote = Primitive(
                    "Mote",
                    PrimitiveType.Sphere,
                    motesRoot,
                    scattered,
                    Vector3.one * 0.055f,
                    new Color(1.1f, 1.16f, 1.25f));
                mote.transform.localPosition = scattered;
                motes[i] = mote.transform;
            }
            Phase01AtmosphereMotes atmosphere = motesRoot.gameObject.AddComponent<Phase01AtmosphereMotes>();
            atmosphere.Configure(camera, motes, new Vector3(34f, 13f, 18f));
            // Bloom is what makes the level's glowing blocks read as glowing (the Phase 1 sheet asks for
            // "发光黄色小方块"). The camera has to opt into post-processing, and that opt-in only sticks from
            // the runtime component: URP's own camera-data component does not survive a batch-mode save.
            cameraObject.AddComponent<Phase01PostFxCamera>().Configure(camera);

            // The painted distance: one camera-locked panel carrying the haze, the layered ruins and their
            // values. Geometry could not do this job - the camera is orthographic and sees 8.4 m of world,
            // so a tower either fills the whole frame or is washed into the fog, and an earlier pass that
            // authored a field of 46 m spires ended up standing them across the level. See
            // Phase01SkylineBackdrop for the reasoning, and GetOrCreateBackdropMaterial for the painting.
            GameObject backdropPanel = Primitive(
                "Distant Skyline",
                PrimitiveType.Cube,
                cameraObject.transform,
                Vector3.zero,
                Vector3.one,
                Color.white,
                false,
                GetOrCreateBackdropMaterial());
            backdropPanel.transform.SetParent(cameraObject.transform, false);
            backdropPanel.AddComponent<Phase01SkylineBackdrop>().Configure(camera, BackdropDistance, BackdropMargin);

            GameObject radioObject = new GameObject("Central Radio Receiver");
            Phase01Receiver receiver = radioObject.AddComponent<Phase01Receiver>();
            GameObject body = Primitive("Radio Body", PrimitiveType.Cube, radioObject.transform, new Vector3(0f, 0.765f, 0f), new Vector3(1.75f, 1.45f, 1.35f), new Color(0.97f, 0.98f, 1f));
            // Sits on top of the shell rather than inside it: the shell used to hide it completely.
            Primitive("Receiver Core", PrimitiveType.Sphere, radioObject.transform, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.66f, new Color(2.1f, 1.6f, 0.28f));
            GameObject antenna = Primitive("Antenna", PrimitiveType.Cylinder, radioObject.transform, new Vector3(0.45f, 1.845f, 0f), new Vector3(0.07f, 0.7f, 0.07f), new Color(0.45f, 0.47f, 0.52f));
            antenna.transform.rotation = Quaternion.Euler(0f, 0f, -12f);
            Transform depositTarget = new GameObject("Particle Deposit Target").transform;
            depositTarget.SetParent(radioObject.transform);
            depositTarget.localPosition = new Vector3(0f, 0.995f, 0f);

            GameObject[] chargeLights = new GameObject[4];
            Transform lightsRoot = new GameObject("Charge 0 to 4").transform;
            lightsRoot.SetParent(radioObject.transform);
            for (int i = 0; i < chargeLights.Length; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * 1f) + Vector3.up * 1.12f;
                chargeLights[i] = Primitive($"Charge Light {i + 1}", PrimitiveType.Sphere, lightsRoot, position, Vector3.one * 0.22f, new Color(1.9f, 1.5f, 0.3f));
            }
            // Shards for the design sheet's "radio explodes" payoff. Authored up front and hidden, so the
            // burst itself allocates nothing when it plays.
            Transform burstOrigin = new GameObject("Burst Origin").transform;
            burstOrigin.SetParent(radioObject.transform);
            burstOrigin.localPosition = new Vector3(0f, 1.78f, 0f);
            Transform[] burstShards = new Transform[12];
            for (int i = 0; i < burstShards.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * (360f / burstShards.Length), 0f) * Vector3.forward;
                GameObject shard = Primitive(
                    $"Radio Burst Shard {i + 1}",
                    PrimitiveType.Cube,
                    burstOrigin,
                    burstOrigin.position + direction,
                    Vector3.one * 0.26f,
                    new Color(1.9f, 1.55f, 0.35f));
                shard.transform.localPosition = direction;
                shard.SetActive(false);
                burstShards[i] = shard.transform;
            }
            Phase01RadioBurst radioBurst = radioObject.AddComponent<Phase01RadioBurst>();
            radioBurst.Configure(burstOrigin, burstShards);

            receiver.Configure(depositTarget, chargeLights);

            const float supportRadius = 1.78f;
            const float supportTopHeight = 0.04f;
            const float supportBottomHeight = -5.98f;
            Transform supportRoot = new GameObject("Central Radio Terrain Support").transform;
            supportRoot.SetParent(environment.transform);
            GameObject supportMass = Primitive(
                "Central Support Lower Mass",
                PrimitiveType.Cylinder,
                supportRoot,
                new Vector3(0f, -2.98f, 0f),
                new Vector3(3.44f, 3f, 3.44f),
                new Color(0.93f, 0.95f, 0.99f));
            GameObject supportTop = Primitive(
                "Central Support Walkable Top",
                PrimitiveType.Cylinder,
                supportRoot,
                new Vector3(0f, 0.01f, 0f),
                new Vector3(supportRadius * 2f, 0.03f, supportRadius * 2f),
                new Color(1f, 1f, 1f));
            Phase01CentralSupport centralSupport = supportRoot.gameObject.AddComponent<Phase01CentralSupport>();
            centralSupport.Configure(
                supportTop.GetComponent<MeshRenderer>(),
                supportMass.GetComponent<MeshRenderer>(),
                body.GetComponent<Renderer>(),
                supportRadius,
                supportTopHeight,
                supportBottomHeight);

            float playerGroundOffset = 0.72f;
            GameObject playerObject = new GameObject("Placeholder Player");
            const float initialPlayerScalar = 1.8f;
            playerObject.transform.position = Directions[0] * initialPlayerScalar
                + Vector3.up * branchTerrains[0].SampleHeight(initialPlayerScalar);
            Rigidbody playerBody = playerObject.AddComponent<Rigidbody>();
            playerBody.mass = 1f;
            CapsuleCollider playerCollider = playerObject.AddComponent<CapsuleCollider>();
            playerCollider.radius = 0.32f;
            playerCollider.height = 1.44f;
            playerCollider.center = Vector3.up * playerGroundOffset;
            playerCollider.material = playerPhysicsMaterial;
            Primitive(
                "Player Visual",
                PrimitiveType.Capsule,
                playerObject.transform,
                playerObject.transform.position + Vector3.up * playerGroundOffset,
                new Vector3(0.64f, 0.72f, 0.64f),
                new Color(0.14f, 0.15f, 0.18f));
            playerObject.AddComponent<Phase01ParkourMotor>();
            Phase01PlayerMotor player = playerObject.AddComponent<Phase01PlayerMotor>();

            Transform particlesRoot = new GameObject("Yellow Branch Particles").transform;
            Phase01Particle[] particles = new Phase01Particle[4];
            float[] particleScalars = { Branch1ParticleScalar, Branch2ParticleScalar, 165f, 174f };
            for (int i = 0; i < particles.Length; i++)
            {
                float particleScalar = particleScalars[i];
                // The design sheet calls these "glowing small yellow blocks": cubes, not spheres.
                GameObject particleObject = Primitive(
                    $"Branch {i + 1} Yellow Particle",
                    PrimitiveType.Cube,
                    particlesRoot,
                    Directions[i] * particleScalar + Vector3.up * (branchTerrains[i].SampleHeight(particleScalar) + 1.25f),
                    Vector3.one * 0.48f,
                    new Color(1.9f, 1.55f, 0.3f));
                particles[i] = particleObject.AddComponent<Phase01Particle>();
                particles[i].Configure(i, cameraObject.transform, particleObject.GetComponent<Renderer>());
            }

            GameObject systems = new GameObject("Phase01 Demo Systems");
            Phase01DemoController controller = systems.AddComponent<Phase01DemoController>();
            Phase01PerformanceMonitor performanceMonitor = systems.AddComponent<Phase01PerformanceMonitor>();
            controller.Configure(player, cameraRig, receiver, particles, Directions, branchTerrains, centralSupport);
            Phase01PacingMonitor pacingMonitor = systems.AddComponent<Phase01PacingMonitor>();
            pacingMonitor.Configure(controller);
            cameraRig.Configure(camera, player.transform, radioObject.transform, controller);
            player.Configure(controller, camera);
            ConfigureBranch1ElevationMechanics(branchTerrains[0], playerPhysicsMaterial, controller, player);
            ConfigureBranch1AirCurrents(branchTerrains[0], controller, player);
            ConfigureBranch1Interactions(branchTerrains[0], playerPhysicsMaterial, controller, player);
            ConfigureBranch2ElevationMechanics(branchTerrains[1], playerPhysicsMaterial);
            ConfigureBranch3ElevationMechanics(branchTerrains[2], playerPhysicsMaterial);
            systems.AddComponent<Phase01DebugOverlay>().Configure(controller, cameraRig, performanceMonitor, pacingMonitor);
            systems.AddComponent<Phase01AcceptanceDriver>().Configure(controller, player, performanceMonitor);
            new GameObject(SceneBuildSignatureObjectName).transform.SetAsLastSibling();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            Selection.activeGameObject = systems;
            AssetDatabase.SaveAssets();
            Debug.Log($"[Phase01Demo] Built {ScenePath} with serialized runtime references.");
        }

        private static bool HasCurrentSceneBuildSignature()
        {
            if (!File.Exists(ScenePath))
                return false;

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return scene.GetRootGameObjects().Any(root => root.name == SceneBuildSignatureObjectName);
        }

        /// <summary>
        /// Diagnostic player that starts directly in Phase 01 and runs the acceptance driver in batch mode.
        /// It lets the built player data be verified without a human pressing through the title cards, which
        /// is what you need when a build aborts while loading the Phase 01 level. Run it with:
        ///   Builds/Phase01SelfTest.app/Contents/MacOS/CMM demo -phase01SelfTest -batchmode -nographics -logFile -
        /// </summary>
        [MenuItem("Tools/Codex/Phase01 Demo/Build Phase01 Self-Test Player")]
        public static void BuildPhase01SelfTestPlayer()
        {
            // Always run the builder: it is signature-guarded, so this is a no-op when the scene is current,
            // and it stops a stale scene file from being packed into the player. A stale scene whose
            // serialized data no longer matches the runtime arrays makes the built level unreadable and the
            // player aborts on load with "level file is corrupted / position out of bounds".
            BuildScene();

            string output = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Builds",
                "Phase01SelfTest.app");
            if (Directory.Exists(output))
                Directory.Delete(output, true);
            Directory.CreateDirectory(Path.GetDirectoryName(output));

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development | BuildOptions.CleanBuildCache
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Phase 01 self-test build failed: {report.summary.result}");
            Debug.Log($"[Phase01Demo] Self-test player built: {output}");
        }

        /// <summary>
        /// macOS standalone output path. Deterministic and inside the (gitignored) project Builds folder,
        /// because Path.GetTempPath() resolves differently depending on how Unity was launched, which
        /// previously scattered two different apps with the same name.
        /// </summary>
        public static string StandalonePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Builds", "Phase01_Demo0.app"));

        /// <summary>Standalone built with Phase 01 as the first scene, for automated smoke verification.</summary>
        public static string SmokePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Builds", "Phase01_Smoke.app"));

        [MenuItem("Tools/Codex/Phase01 Demo/Build macOS Standalone")]
        public static void BuildStandalone()
        {
            BuildSceneIfDataChanged();

            BuildPlayerTo(EditorBuildSettings.scenes
                .Where(buildScene => buildScene.enabled)
                .Select(buildScene => buildScene.path)
                .ToArray(), StandalonePath);
        }

        /// <summary>
        /// Builds a player that starts directly in Phase 01 so the acceptance driver can run with
        /// -phase01SelfTest and prove the built level loads and traverses. Does not touch build settings.
        /// </summary>
        [MenuItem("Tools/Codex/Phase01 Demo/Build Phase01 Smoke Player")]
        public static void BuildPhase01SmokePlayer()
        {
            BuildSceneIfDataChanged();
            BuildPlayerTo(new[] { ScenePath }, SmokePath);
            VerifyPackedLevelLoads();
        }

        /// <summary>
        /// Runs the freshly packed smoke player headlessly and requires it to report a passing acceptance
        /// run. A level the player cannot read is not a level: the editor reads the scene through its own
        /// serialization path and cannot see a fault like a component written one field short, which is
        /// exactly the fault that shipped a crash-on-load Phase 01 three times. The packed player is the
        /// only artefact that can prove the level loads, so the smoke build now insists on asking it.
        /// </summary>
        private static void VerifyPackedLevelLoads()
        {
            string executable = Path.Combine(SmokePath, "Contents", "MacOS", "CMM demo");
            if (!File.Exists(executable))
                throw new BuildFailedException($"Smoke player executable is missing at {executable}.");

            string logPath = Path.Combine(Path.GetTempPath(), "phase01SmokeSelfTest.log");
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = executable,
                Arguments = $"-phase01SelfTest -batchmode -nographics -logFile \"{logPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
            {
                // Drain both pipes before waiting so a chatty player can never deadlock the build.
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();

                string contents = File.Exists(logPath) ? File.ReadAllText(logPath) : string.Empty;
                if (contents.Contains("is corrupted"))
                    throw new BuildFailedException(
                        $"The packed Phase 01 level is unreadable; the player refused it. See {logPath}.");
                if (process.ExitCode != 0 || !contents.Contains("[Phase01Acceptance] PASS"))
                    throw new BuildFailedException(
                        $"The packed Phase 01 level did not load and traverse (exit {process.ExitCode}). "
                        + $"See {logPath}.");
            }

            Debug.Log($"[Phase01Demo] Smoke player proved the packed level loads and traverses: {logPath}");
        }

        private static void BuildSceneIfDataChanged()
        {
            // Never build from a scene the AssetDatabase has not re-imported: editing the .unity file
            // outside Unity and then building produced a level the player could not deserialize.
            File.SetLastWriteTimeUtc(ScenePath, System.DateTime.UtcNow.AddSeconds(-5));
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (!File.Exists(ScenePath))
                BuildScene();
        }

        private static void BuildPlayerTo(string[] scenes, string output)
        {
            if (Directory.Exists(output))
                Directory.Delete(output, true);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                // Release player. A Development build carries script debugging, no code optimisation and a
                // debug HUD, and runs far below the frame rate the game is tuned for - "it lags like crazy"
                // was a Development player, not the level.
                options = BuildOptions.CleanBuildCache
            };
            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new BuildFailedException($"Phase01 standalone build failed: {report.summary.result}");

            VerifyBuiltLevels(output, scenes);
            Debug.Log($"[Phase01Demo] Standalone build succeeded: {output}");
        }

        /// <summary>
        /// Fails the build if any level data is missing or the Phase 01 level is suspiciously small, so a
        /// truncated or stale level can never be handed to a player silently.
        /// </summary>
        private static void VerifyBuiltLevels(string output, string[] scenes)
        {
            string dataFolder = Path.Combine(output, "Contents", "Resources", "Data");
            for (int i = 0; i < scenes.Length; i++)
            {
                string level = Path.Combine(dataFolder, $"level{i}");
                if (!File.Exists(level))
                    throw new BuildFailedException($"Built player is missing {Path.GetFileName(level)}.");
                if (scenes[i] != ScenePath)
                    continue;
                long size = new FileInfo(level).Length;
                if (size < 20000)
                    throw new BuildFailedException(
                        $"Built Phase 01 level looks truncated ({size} bytes); refusing to ship it.");
                string contents = File.ReadAllText(level, System.Text.Encoding.UTF8);
                if (!contents.Contains("Phase01 Demo Systems"))
                    throw new BuildFailedException("Built Phase 01 level does not contain the Phase 01 systems object.");
            }
        }

        private static void EnsureSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .ToList();
            if (scenes.Any(scene => scene.path == ScenePath))
                return;
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static GameObject Primitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool keepCollider = false,
            Material materialOverride = null)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            if (parent != null)
                gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null && !keepCollider)
                Object.DestroyImmediate(collider);
            gameObject.GetComponent<Renderer>().sharedMaterial = materialOverride != null
                ? materialOverride
                : GetOrCreateTintMaterial(color);
            return gameObject;
        }

        private static void AddGapLipCollider(
            Transform parent,
            string name,
            float scalar,
            float topHeight,
            float bottomHeight,
            float halfWidth,
            float surfaceOpening)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = new Vector3(scalar, bottomHeight, 0f);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            float blockingTop = Mathf.Max(bottomHeight + 0.08f, topHeight - surfaceOpening);
            collider.size = new Vector3(0.32f, blockingTop - bottomHeight, halfWidth * 2.2f);
            collider.center = Vector3.up * (collider.size.y * 0.5f);
        }

        private static void ConfigureBranch2Transformation(
            Phase01BranchTerrain terrain,
            GameObject upperGapColliders)
        {
            GameObject lowerRoot = new GameObject("Branch 2 Lower Return Route (After Pickup)");
            lowerRoot.transform.SetParent(terrain.transform, false);
            // Colourless, like the rest of Phase 1: the recovery route is told apart from the main ledge by
            // VALUE (a step darker, so it reads as the lower, secondary route) rather than by a green tint
            // that would spend the colour the later phases are supposed to regain.
            RoutePlatform(lowerRoot.transform, "Outer Pickup Recovery", 121.8f, 176f, 0.3f, 1.6f, new Color(0.62f, 0.66f, 0.73f), true);
            RoutePlatform(lowerRoot.transform, "Outer Return Island", 102f, 120f, 0f, 1.6f, new Color(0.58f, 0.62f, 0.69f), true);
            RoutePlatform(lowerRoot.transform, "High Drop Recovery Island", 79.6f, 100f, -0.35f, 1.6f, new Color(0.54f, 0.58f, 0.65f), true);
            RoutePlatform(lowerRoot.transform, "Middle Outer Return Island", 57.8f, 78f, 0.15f, 1.6f, new Color(0.58f, 0.62f, 0.69f), true);
            RoutePlatform(lowerRoot.transform, "Middle Inner Return Island", 36f, 56f, -0.45f, 1.6f, new Color(0.54f, 0.58f, 0.65f), true);
            RoutePlatform(lowerRoot.transform, "Inner Return Island", 15.6f, 34f, 0f, 1.6f, new Color(0.58f, 0.62f, 0.69f), true);
            RoutePlatform(lowerRoot.transform, "Lower Return Radio Approach", 1.4f, 14f, 0f, 1.6f, new Color(0.99f, 0.99f, 1f), true);

            GameObject fallingRoot = new GameObject("Branch 2 Falling Upper Debris");
            fallingRoot.transform.SetParent(lowerRoot.transform, false);
            RouteCube(fallingRoot.transform, "Outer Gap Edge Debris", new Vector3(120.9f, 0.6f, 0f), new Vector3(1.6f, 0.3f, 1.5f), new Color(0.8f, 0.82f, 0.86f));
            RouteCube(fallingRoot.transform, "Middle Gap Edge Debris", new Vector3(79f, 0.45f, 0f), new Vector3(1.8f, 0.3f, 1.5f), new Color(0.28f, 0.32f, 0.36f));
            RouteCube(fallingRoot.transform, "Inner Gap Edge Debris", new Vector3(35f, 0.25f, 0f), new Vector3(1.5f, 0.3f, 1.5f), new Color(0.8f, 0.82f, 0.86f));

            Phase01Branch2RouteTransformation transformation = terrain.gameObject.AddComponent<Phase01Branch2RouteTransformation>();
            transformation.Configure(terrain, upperGapColliders, lowerRoot);
        }

        private static void ConfigureBranch3FadingSupports(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial)
        {
            Transform root = new GameObject("Branch 3 Exactly Three Fading Supports").transform;
            root.SetParent(terrain.transform, false);
            float[] innerScalars = { 81.8f, 85.6f, 89.4f };
            float[] outerScalars = { 83.8f, 87.6f, 91.4f };
            float[] topHeights = { 0.4f, 0.65f, 0.35f };
            // Authored as ordinary pale stone, because in Phase 1 the path is white and the *only* hue in
            // the level is the yellow of the blocks. The support's warning is a value flare instead - see
            // Phase01FadingSupport - which reads harder in a colourless world than an orange tint did.
            Color supportColor = new Color(0.9f, 0.92f, 0.96f);
            for (int i = 0; i < innerScalars.Length; i++)
            {
                GameObject platform = RoutePlatform(
                    root,
                    $"Fading Support {i + 1}",
                    innerScalars[i],
                    outerScalars[i],
                    topHeights[i],
                    1.5f,
                    supportColor,
                    true);
                platform.GetComponent<Collider>().sharedMaterial = physicsMaterial;
                platform.GetComponent<Renderer>().sharedMaterial = GetOrCreateTintMaterialAtPath(
                    supportColor,
                    Branch3SupportTintPath);
                Phase01FadingSupport support = platform.AddComponent<Phase01FadingSupport>();
                support.Configure(
                    i,
                    innerScalars[i],
                    outerScalars[i],
                    platform.GetComponent<Renderer>(),
                    platform.GetComponent<Collider>());
            }
        }

        private static void ConfigureBranch4ControlledCollapse(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial)
        {
            Transform root = new GameObject("Branch 4 Exactly Four Controlled Collapse Segments").transform;
            root.SetParent(terrain.transform, false);
            float[] innerScalars = { 101.2f, 97.4f, 93.6f, 89.8f };
            float[] outerScalars = { 103.2f, 99.4f, 95.6f, 91.8f };
            Phase01CollapsibleSegment[] segments = new Phase01CollapsibleSegment[4];
            for (int i = 0; i < segments.Length; i++)
            {
                GameObject platform = RoutePlatform(
                    root,
                    $"Collapse Segment {i + 1} (Order {i}: Outer to Inner)",
                    innerScalars[i],
                    outerScalars[i],
                    0.3f,
                    1.6f,
                    new Color(0.62f, 0.6f, 0.58f),
                    true);
                platform.GetComponent<Collider>().sharedMaterial = physicsMaterial;
                platform.GetComponent<Renderer>().sharedMaterial = GetOrCreateTintMaterialAtPath(
                    new Color(0.6f, 0.63f, 0.68f),
                    Branch4CollapseTintPath);
                segments[i] = platform.AddComponent<Phase01CollapsibleSegment>();
                segments[i].Configure(
                    i,
                    innerScalars[i],
                    outerScalars[i],
                    platform.GetComponent<Renderer>(),
                    platform.GetComponent<Collider>());
            }

            Transform fillerRoot = new GameObject("Branch 4 Dedicated Outbound Gap Fillers").transform;
            fillerRoot.SetParent(root, false);
            float[] fillerInnerScalars = { 103.2f, 99.4f, 95.6f, 91.8f, 88f };
            float[] fillerOuterScalars = { 105f, 101.2f, 97.4f, 93.6f, 89.8f };
            GameObject[] gapFillers = new GameObject[fillerInnerScalars.Length];
            for (int i = 0; i < gapFillers.Length; i++)
            {
                gapFillers[i] = RoutePlatform(
                    fillerRoot,
                    $"Outbound Gap Filler {i + 1}",
                    fillerInnerScalars[i],
                    fillerOuterScalars[i],
                    0.3f,
                    1.6f,
                    new Color(0.62f, 0.6f, 0.58f),
                    true);
                gapFillers[i].GetComponent<Collider>().sharedMaterial = physicsMaterial;
                gapFillers[i].GetComponent<Renderer>().sharedMaterial = GetOrCreateTintMaterialAtPath(
                    new Color(0.6f, 0.63f, 0.68f),
                    Branch4CollapseTintPath);
            }

            Phase01Branch4ControlledCollapse collapse = terrain.gameObject
                .AddComponent<Phase01Branch4ControlledCollapse>();
            collapse.Configure(segments, gapFillers, 1.4f, 88f, 105f);
        }

        /// <summary>
        /// Branch 1's air currents: the ride across a void too wide to jump.
        ///
        /// These replace jump-ladder stretches, not the jumps themselves. Each current crosses a void far
        /// wider than any jump (14.0 m and 11.3 m against a 4.4 m flat reach), so the only route is to hold
        /// jump and ride it - which is the point. Branch 1's difficulty reads as interaction with the level
        /// rather than as distance, and a ride can be read, committed to, and let go of in mid-air.
        ///
        /// The ride line is authored 0.4 m above the higher of the two lips so the same current carries the
        /// player out and back: every branch lane is walked both ways, and a one-way mechanic would strand
        /// the return trip. The baseline branch's currents are steady - the later branches escalate them by
        /// pulsing them and tightening the window - so the beat stays a read on branch 1.
        /// </summary>
        private static void ConfigureBranch1AirCurrents(
            Phase01BranchTerrain terrain,
            Phase01DemoController controller,
            Phase01PlayerMotor player)
        {
            Transform root = new GameObject("Branch 1 Air Currents").transform;
            root.SetParent(terrain.transform, false);

            // Inner crossing: 14 m of open air past the summit plateau. The profile steps back down on the
            // far side, so the ride also carries a real change of elevation rather than being a flat hop.
            AuthorAirCurrent(
                root,
                controller,
                player,
                "Branch 1 Inner Chasm Current",
                innerScalar: 61.60f,
                outerScalar: 74.40f,
                rideHeight: 9.40f,
                bandFloor: 5.0f,
                bandCeiling: 13.5f,
                alongSpeed: 4.4f,
                pulseOn: 0f,
                pulseOff: 0f,
                phase: 0f);

            // Outer crossing: 11.3 m past the patrol door, on the way to the pickup shelf.
            AuthorAirCurrent(
                root,
                controller,
                player,
                "Branch 1 Outer Chasm Current",
                innerScalar: 92.90f,
                outerScalar: 104.20f,
                rideHeight: 7.60f,
                bandFloor: 3.6f,
                bandCeiling: 11.5f,
                alongSpeed: 4.6f,
                pulseOn: 0f,
                pulseOff: 0f,
                phase: 0f);
        }

        /// <summary>
        /// One authored air current, with the pale streaks and motes that read as moving air. At gameplay
        /// framing these are the only things in the air inside a void, so the player can see the current
        /// before committing to it. A pulsed current is hidden while it is dry, which reads honestly.
        /// </summary>
        private static void AuthorAirCurrent(
            Transform root,
            Phase01DemoController controller,
            Phase01PlayerMotor player,
            string name,
            float innerScalar,
            float outerScalar,
            float rideHeight,
            float bandFloor,
            float bandCeiling,
            float alongSpeed,
            float pulseOn,
            float pulseOff,
            float phase)
        {
            GameObject currentObject = new GameObject(name);
            currentObject.transform.SetParent(root, false);
            currentObject.transform.localPosition = new Vector3(
                (innerScalar + outerScalar) * 0.5f,
                rideHeight,
                0f);

            Transform visuals = new GameObject($"{name} Gust").transform;
            visuals.SetParent(currentObject.transform, false);
            float halfWidth = (outerScalar - innerScalar) * 0.5f;
            for (int i = 0; i < 7; i++)
            {
                GameObject streak = Primitive(
                    $"{name} Streak {i + 1}",
                    PrimitiveType.Cube,
                    visuals,
                    Vector3.zero,
                    new Vector3(halfWidth * 1.4f, 0.05f, 0.05f),
                    new Color(0.94f, 0.96f, 1f));
                streak.transform.localPosition = new Vector3(
                    Mathf.Lerp(-halfWidth * 0.7f, halfWidth * 0.7f, i / 6f),
                    Mathf.Lerp(-1.2f, 2.2f, Mathf.Abs(Mathf.Sin(i * 1.9f))),
                    0f);
            }
            for (int i = 0; i < 9; i++)
            {
                GameObject mote = Primitive(
                    $"{name} Mote {i + 1}",
                    PrimitiveType.Sphere,
                    visuals,
                    Vector3.zero,
                    Vector3.one * 0.14f,
                    new Color(1.35f, 1.4f, 1.5f));
                mote.transform.localPosition = new Vector3(
                    Mathf.Lerp(-halfWidth * 0.88f, halfWidth * 0.88f, i / 8f),
                    Mathf.Lerp(-1.5f, 2.4f, Mathf.Abs(Mathf.Cos(i * 2.3f))),
                    0f);
            }

            Phase01Draught draught = currentObject.AddComponent<Phase01Draught>();
            draught.Configure(
                0,
                innerScalar,
                outerScalar,
                rideHeight,
                bandFloor,
                bandCeiling,
                alongSpeed,
                pulseOn,
                pulseOff,
                phase,
                controller,
                player,
                visuals);
        }

        /// <summary>
        /// TASK 006 / TASK 007 Branch 1 interaction beats. Branch 1 is the difficulty baseline, and its
        /// difficulty is authored as *interaction* rather than distance: every jump on the branch stays at
        /// or below the comfort cap of its uphill reach, and the branch instead asks the player to read and
        /// time two moving hazards built out of the ledge they sit on.
        ///
        ///  - A sweeping bar on the first long terrace. It spans the full width of the ledge, so it cannot
        ///    be walked around, and it travels at 1.5 m/s against a 4.5 m/s player: the beat is "hop it as
        ///    it arrives, or follow it out" - a reading beat, never a precision demand.
        ///  - A patrol door at the summit terrace. The bar blocks the ledge at rest and rises clear on a
        ///    fixed cycle, so the beat is "wait for the window, then walk through".
        ///
        /// Both are deterministic, re-arm on approach (so the beat is identical outbound and homebound),
        /// and introduce no health, damage, checkpoint or failure state: contact is resolved by ordinary
        /// physics and the existing fall / soft-reset flow.
        /// </summary>
        private static void ConfigureBranch1Interactions(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial,
            Phase01DemoController controller,
            Phase01PlayerMotor player)
        {
            Transform root = new GameObject("Branch 1 Interaction Beats").transform;
            root.SetParent(terrain.transform, false);

            // Sweeping bar: authored terrace 7.81 m - 16.5 m, flat at +0.75 m, so the bar's centre sits
            // at +1.20 m and its 0.9 m body spans the walkable height (0.75 - 1.65) and blocks a walker.
            // It patrols 5.0 m of the middle of the terrace, leaving 3.2 m of clear ledge either side so
            // the player always has somewhere to read it from.
            GameObject sweepBar = InteractionBeat(
                "Branch 1 Sweeping Bar",
                root,
                new Vector3(8.8f, 1.2f, 0f),
                new Color(0.16f, 0.17f, 0.2f),
                physicsMaterial);
            Phase01SweeperGate sweep = sweepBar.AddComponent<Phase01SweeperGate>();
            sweep.ConfigureHorizontalSweep(
                0,
                8.8f,
                13.8f,
                1.2f,
                3.4f,
                6.6f,
                16.4f,
                controller,
                player);

            // Patrol door: authored terrace 86.36 m - 89.1 m, flat at +5.8 m, so the bar's centre sits at
            // +6.25 m and its body spans 5.8 - 6.7 at rest - the whole walking height - and finishes at
            // +9.85 m, well clear of the player. 1.8 s per leg against a 0.5 m bar is a generous window:
            // the beat is a read, not a reflex test.
            GameObject patrolDoor = InteractionBeat(
                "Branch 1 Patrol Door",
                root,
                new Vector3(87.7f, 6.25f, 0f),
                new Color(0.2f, 0.21f, 0.24f),
                physicsMaterial);
            Phase01SweeperGate door = patrolDoor.AddComponent<Phase01SweeperGate>();
            door.Configure(
                0,
                87.7f,
                6.25f,
                9.85f,
                0.9f,
                1.8f,
                85.5f,
                91f,
                controller,
                player);
        }

        /// <summary>
        /// A small pale lamp at every few landings, set at the ledge edge rather than in the route. They give
        /// the dark somewhere to glow, make the authored landings readable from a distance, and carry no
        /// colliders at all, so they can never obstruct the player.
        /// </summary>
        private static void AddLandingLamps(
            Transform branchRoot,
            Vector3 direction,
            float[] scalars,
            float[] heights,
            float[] halfWidths,
            bool[] gaps)
        {
            Transform root = new GameObject("Landing Lamps").transform;
            root.SetParent(branchRoot, false);
            Vector3 lateral = Vector3.Cross(Vector3.up, direction.normalized);
            for (int i = 0; i < scalars.Length; i += 3)
            {
                if (i > 0 && i - 1 < gaps.Length && gaps[i - 1])
                    continue;

                float side = (i % 6 == 0) ? 1f : -1f;
                Vector3 position = direction * scalars[i]
                    + Vector3.up * (heights[i] + 0.42f)
                    + lateral * (halfWidths[i] + 0.3f) * side;
                Primitive(
                    "Landing Lamp",
                    PrimitiveType.Sphere,
                    root,
                    position,
                    Vector3.one * 0.17f,
                    new Color(1.35f, 1.3f, 1.18f));
            }
        }

        /// <summary>
        /// One authored Branch 1 interaction bar: a kinematic, collidable cube that spans the full width of
        /// its ledge, so the beat can never be walked around. It reuses the ordinary physics push, which is
        /// what the branch's existing fall / soft-reset flow already handles.
        /// </summary>
        private static GameObject InteractionBeat(
            string name,
            Transform parent,
            Vector3 localPosition,
            Color color,
            PhysicsMaterial physicsMaterial)
        {
            GameObject bar = Primitive(
                name,
                PrimitiveType.Cube,
                parent,
                Vector3.zero,
                new Vector3(0.5f, 0.9f, 1.6f),
                color,
                true);
            bar.transform.localPosition = localPosition;
            bar.GetComponent<Collider>().sharedMaterial = physicsMaterial;
            Rigidbody body = bar.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            return bar;
        }

        /// <summary>
        // NOTE (thorn lips): Phase01SpikeStrip is authored and verified - contact uses the existing soft
        // reset, and passing above the band is safe - but it is deliberately not authored into the level yet.
        // Placing a band on a gap lip is the fair construction (the pad before it is the run-up and the gap
        // beyond it is the landing, so no pad has to be lengthened), and the thorn lips were authored that
        // way. The blocker is the acceptance driver rather than the level: its jump cadence holds for 0.5 s
        // and its takeoffs are spaced for gaps, so on the stretch into the thorns it lands inside the band
        // (diagnostic: hit at scalar 26.03 with the feet still on the surface) and resets into it. Author the
        // bands once the driver can make a short, committed hop at an arbitrary scalar.

        /// TASK 005 Branch 1 elevation mechanics. The riser lift and the ferry reuse the accepted
        /// Phase01MovingPlatform component that the movement sandbox already validates, so Branch 1
        /// introduces no second motion model. Both are authored, deterministic, resettable and slower than
        /// the accepted player speed.
        /// </summary>
        private static void ConfigureBranch1ElevationMechanics(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial,
            Phase01DemoController controller,
            Phase01PlayerMotor player)
        {
            // Branch-local X is the outward direction and Phase01MovingPlatform applies its offset in
            // world space, so the offset is rotated here rather than in the runtime component.
            Vector3 outward = terrain.OutwardDirection;
            Transform root = new GameObject("Branch 1 Elevation Mechanics").transform;
            root.SetParent(terrain.transform, false);

            // Required riser: lifts 3.0 m, which is above the accepted 2.25 m jump apex, so the upper
            // terrace is genuinely unreachable on foot in both directions. Top edge lands flush with
            // the upper terrace lip at scalar 38.2.
            CreateAuthoredMovingPlatform(
                root,
                "Branch 1 Riser Lift (Required 3.0 m)",
                35.1f,
                36.5f,
                6.0f,
                0.9f,
                outward * 1.5f + Vector3.up * 3f,
                2.5f,
                new Color(0.88f, 0.89f, 0.92f),
                physicsMaterial);

            // Ferry: carries the player over the 12 m void at 2.2 m/s, well below the accepted 4.5 m/s
            // player speed, so boarding is a timing read rather than a precision demand. Its inner edge
            // starts flush with the upper terrace lip at scalar 41.0 and its outer edge finishes flush
            // with the ferry landing pad at scalar 53.0.
            CreateAuthoredMovingPlatform(
                root,
                "Branch 1 Ferry Across Void",
                41.1f,
                44.1f,
                9.0f,
                1.1f,
                outward * 8.9f,
                4.0f,
                new Color(0.82f, 0.84f, 0.88f),
                physicsMaterial);

        }

        /// <summary>
        /// TASK 006 Branch 2 elevation mechanics: a 3.5 m riser (above the 2.25 m jump apex, so the upper
        /// terrace is unreachable on foot) and an 18 m ferry. Both reuse the sandbox-validated moving
        /// platform; Branch 2's return leg is already handled by its route transformation.
        /// </summary>
        private static void ConfigureBranch2ElevationMechanics(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial)
        {
            Vector3 outward = terrain.OutwardDirection;
            Transform root = new GameObject("Branch 2 Elevation Mechanics").transform;
            root.SetParent(terrain.transform, false);

            CreateAuthoredMovingPlatform(
                root,
                "Branch 2 Riser Lift (Required 3.5 m)",
                66.8f,
                69.2f,
                8f,
                0.9f,
                outward * 0.5f + Vector3.up * 3.5f,
                3f,
                new Color(0.88f, 0.89f, 0.92f),
                physicsMaterial);

            CreateAuthoredMovingPlatform(
                root,
                "Branch 2 Ferry Across Void",
                72.8f,
                77.8f,
                11.5f,
                1.1f,
                outward * 12.9f,
                5f,
                new Color(0.82f, 0.84f, 0.88f),
                physicsMaterial);
        }

        /// <summary>
        /// TASK 006 Branch 3 ferry: 12 m of authored platform over the void at 117.1-129.1, on the
        /// climb to the +13.5 m particle terrace. Reuses the sandbox-validated moving platform.
        /// </summary>
        private static void ConfigureBranch3ElevationMechanics(
            Phase01BranchTerrain terrain,
            PhysicsMaterial physicsMaterial)
        {
            Transform root = new GameObject("Branch 3 Elevation Mechanics").transform;
            root.SetParent(terrain.transform, false);
            CreateAuthoredMovingPlatform(
                root,
                "Branch 3 Ferry Across Void",
                117.2f,
                120.2f,
                3.25f,
                1.1f,
                terrain.OutwardDirection * 8.9f,
                4f,
                new Color(0.82f, 0.84f, 0.88f),
                physicsMaterial);
        }

        private static GameObject CreateAuthoredMovingPlatform(
            Transform parent,
            string name,
            float innerScalar,
            float outerScalar,
            float topHeight,
            float width,
            Vector3 worldOffset,
            float oneWayDuration,
            Color color,
            PhysicsMaterial physicsMaterial)
        {
            float length = outerScalar - innerScalar;
            GameObject platform = Primitive(
                name,
                PrimitiveType.Cube,
                parent,
                Vector3.zero,
                new Vector3(length, 0.3f, width),
                color,
                true);
            platform.transform.localPosition = new Vector3(
                (innerScalar + outerScalar) * 0.5f,
                topHeight - 0.15f,
                0f);
            platform.transform.localScale = new Vector3(length, 0.3f, width);
            platform.GetComponent<Collider>().sharedMaterial = physicsMaterial;
            Rigidbody body = platform.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            Phase01MovingPlatform movingPlatform = platform.AddComponent<Phase01MovingPlatform>();
            movingPlatform.Configure(worldOffset, oneWayDuration);
            return platform;
        }

        private static GameObject RouteCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 size,
            Color color,
            float localZRotation = 0f)
        {
            GameObject cube = Primitive(name, PrimitiveType.Cube, parent, Vector3.zero, size, color);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.Euler(0f, 0f, localZRotation);
            cube.transform.localScale = size;
            return cube;
        }

        private static GameObject RouteRamp(
            Transform parent,
            string name,
            float innerScalar,
            float innerHeight,
            float outerScalar,
            float outerHeight,
            float width,
            Color color,
            bool keepCollider = false)
        {
            float run = outerScalar - innerScalar;
            float rise = outerHeight - innerHeight;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + rise * rise);
            GameObject visual = Primitive(
                name,
                PrimitiveType.Cube,
                parent,
                Vector3.zero,
                new Vector3(length, 0.26f, width),
                color,
                keepCollider);
            visual.transform.localPosition = new Vector3(
                (innerScalar + outerScalar) * 0.5f,
                (innerHeight + outerHeight) * 0.5f - 0.13f,
                0f);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            visual.transform.localScale = new Vector3(length, 0.26f, width);
            return visual;
        }

        private static GameObject RoutePlatform(
            Transform parent,
            string name,
            float innerScalar,
            float outerScalar,
            float topHeight,
            float width,
            Color color,
            bool keepCollider = false)
        {
            GameObject visual = Primitive(
                name,
                PrimitiveType.Cube,
                parent,
                Vector3.zero,
                new Vector3(outerScalar - innerScalar, 0.3f, width),
                color,
                keepCollider);
            visual.transform.localPosition = new Vector3(
                (innerScalar + outerScalar) * 0.5f,
                topHeight - 0.15f,
                0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(outerScalar - innerScalar, 0.3f, width);
            return visual;
        }

        private static void CreateRouteStrip(
            Transform parent,
            string name,
            float width,
            float[] scalars,
            float[] heights)
        {
            GameObject top = new GameObject($"{name} Walkable Top");
            top.transform.SetParent(parent, false);
            MeshFilter filter = top.AddComponent<MeshFilter>();
            MeshCollider collider = top.AddComponent<MeshCollider>();
            float halfWidth = width * 0.5f;
            Vector3[] vertices = new Vector3[scalars.Length * 2];
            int[] triangles = new int[(scalars.Length - 1) * 6];
            for (int i = 0; i < scalars.Length; i++)
            {
                vertices[i * 2] = new Vector3(scalars[i], heights[i], -halfWidth);
                vertices[i * 2 + 1] = new Vector3(scalars[i], heights[i], halfWidth);
                if (i == scalars.Length - 1)
                    continue;
                int triangle = i * 6;
                int vertex = i * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 3;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            Mesh mesh = new Mesh { name = $"Phase01 Generated {name}" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            const string meshFolder = "Assets/Phase01_Demo0/GeneratedMeshes";
            if (!AssetDatabase.IsValidFolder(meshFolder))
                AssetDatabase.CreateFolder("Assets/Phase01_Demo0", "GeneratedMeshes");
            string safeName = name.Replace(" ", string.Empty);
            string meshPath = $"{meshFolder}/{safeName}.asset";
            Mesh stored = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (stored == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                stored = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, stored);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            mesh = stored;
            filter.sharedMesh = mesh;
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// The painted distance, as a texture on the camera-locked backdrop panel.
        ///
        /// Everything here is authored in FRAME terms: t runs 0 at the bottom of the gameplay frame to 1 at
        /// the top, and the panel is <see cref="BackdropMargin"/> frames tall, so t maps onto the texture's
        /// vertical axis linearly. That is the whole point of painting it rather than building it: with an
        /// orthographic camera at size 4.2 there is no perspective to work with, so the composition - how
        /// bright the haze is behind the walkway, how tall the ruins read above the roofline - has to be
        /// decided directly in frame terms rather than hoped for from world positions.
        ///
        /// The Phase 1 sheet asks for "blue background, white content" and a strong black/white contrast. So
        /// the horizon band behind the walkway is the brightest thing in the level after the walkway itself,
        /// the pit below it falls away darker, and the sky above it deepens. The ruins stand on the frame
        /// line where the ground at their distance projects (t = 0.72 for a panel 32 m out at this pitch),
        /// every layer closer in value to the haze than the layer behind it: aerial perspective, painted.
        /// </summary>
        private static Material GetOrCreateBackdropMaterial()
        {
            // The painting is written as a PNG and imported, NOT as a native Texture2D asset built with
            // AssetDatabase.CreateAsset. A native texture has no importer, so a player build copies its raw
            // uncompressed bytes into the scene's asset file - which measurably corrupted the packed level
            // (the player refused level0 with "Position out of bounds"). Going through the file pipeline
            // gives the texture an importer, the platform's normal compression, and a normal place in the
            // build.
            const string texturePath = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01Backdrop.png";
            const string legacyTexturePath = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01Backdrop.asset";
            const string materialPath = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01Backdrop.mat";
            const int width = 1024;
            const int height = 576;

            if (AssetDatabase.LoadMainAssetAtPath(legacyTexturePath) != null)
                AssetDatabase.DeleteAsset(legacyTexturePath);
            Texture2D painted = new Texture2D(width, height, TextureFormat.RGB24, false)
            {
                name = "Phase01 Backdrop",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            // These are the values the player sees. The panel carries the level's fog (it is real geometry
            // 32 m out) but it is a single flat surface, so the fog only shifts every pixel by the same
            // amount - which means the painting can simply include it. Verified against a rendered still of
            // the panel alone: the numbers below are what comes out on screen.
            //
            // t 0.00 is the bottom of the frame (the pit the branches run over), t 0.38 the walkway line,
            // t 1.00 the top of the frame.
            Color pitColour = new Color(0.42f, 0.47f, 0.55f);
            Color glowColour = new Color(0.68f, 0.72f, 0.78f);
            Color bandColour = new Color(0.64f, 0.68f, 0.75f);
            Color upperSky = new Color(0.52f, 0.58f, 0.67f);
            Color zenith = new Color(0.38f, 0.44f, 0.56f);
            Color[] layers =
            {
                new Color(0.52f, 0.57f, 0.65f),
                new Color(0.43f, 0.48f, 0.57f),
                new Color(0.29f, 0.33f, 0.41f)
            };
            float windowLight = 0.86f;

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                // Inverse of FrameTToTextureV: the texture row to the frame fraction it lands on.
                float t = 0.5f - (v - 0.5f) * BackdropMargin;
                Color sky = t < 0.44f
                    ? Color.Lerp(pitColour, glowColour, Mathf.SmoothStep(0f, 1f, t / 0.44f))
                    : t < 0.62f
                        ? Color.Lerp(glowColour, bandColour, Mathf.SmoothStep(0f, 1f, (t - 0.44f) / 0.18f))
                        : t < 0.8f
                            ? Color.Lerp(bandColour, upperSky, Mathf.SmoothStep(0f, 1f, (t - 0.62f) / 0.18f))
                            : Color.Lerp(upperSky, zenith, Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 0.2f));
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = sky;
            }

            // Three layers of ruined towers. The floor they stand on, the heights and the widths are all in
            // frame fractions, and every layer is kept inside the top half of the frame: at gameplay framing
            // the ledges themselves already own everything below the walkway line and most of the middle, so
            // a silhouette that is going to be seen at all has to live above them.
            // Bases sit a little above the frame's middle (t 0.66-0.68, against a walkway line at 0.38), so the
            // towers clear the ledges that rise ahead of the player without their roofs running off the top of
            // the frame - a spire cut off by the frame edge reads as an error, not as scale.
            float[,] layerFloor = { { 0.7f, 0.04f, 0.07f, 0.0048f }, { 0.685f, 0.06f, 0.11f, 0.0075f }, { 0.67f, 0.08f, 0.15f, 0.0105f } };
            int[] layerCount = { 11, 9, 7 };
            for (int layer = 0; layer < 3; layer++)
            {
                Color layerColour = layers[layer];
                float floor = layerFloor[layer, 0];
                float minimumHeight = layerFloor[layer, 1];
                float heightRange = layerFloor[layer, 2];
                float widthFraction = layerFloor[layer, 3];
                for (int i = 0; i < layerCount[layer]; i++)
                {
                    float jitter = Mathf.Abs(Mathf.Sin(i * 2.399963f + layer * 1.7f));
                    float centreX = (i + 0.5f + Mathf.Sin(i * 4.1f + layer) * 0.22f) / layerCount[layer];
                    float ruinHeight = minimumHeight + heightRange * jitter;
                    float halfWidth = widthFraction * (0.6f + 0.8f * Mathf.Abs(Mathf.Sin(i * 1.3f + layer * 2.1f)));
                    PaintRuin(pixels, width, height, centreX, floor, ruinHeight, halfWidth, layerColour, windowLight, layer);
                }
            }

            painted.SetPixels(pixels);
            painted.Apply();
            File.WriteAllBytes(texturePath, painted.EncodeToPNG());
            Object.DestroyImmediate(painted);
            AssetDatabase.ImportAsset(
                texturePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (imported == null)
                throw new BuildFailedException($"The painted backdrop failed to import from '{texturePath}'.");
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Shader shader = ResolveSurfaceShader();
            if (material == null)
            {
                material = new Material(shader) { name = "Phase01 Backdrop" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", imported);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// One ruined tower: a stepped block stack standing on a frame-floor line, with a couple of lit
        /// windows on the nearest layer. Stepped rather than tapered because a stack of blocks is what a
        /// broken tower actually looks like in silhouette.
        /// </summary>
        private static void PaintRuin(
            Color[] pixels,
            int width,
            int height,
            float centreX,
            float floorT,
            float ruinHeight,
            float halfWidth,
            Color colour,
            float windowLight,
            int layer)
        {
            const int steps = 3;
            for (int step = 0; step < steps; step++)
            {
                float stepHeight = ruinHeight * (step == steps - 1 ? 0.3f : 0.35f);
                float stepHalfWidth = halfWidth * (1f - step * 0.24f);
                float bottomT = floorT + ruinHeight * step * 0.35f;
                int x0 = Mathf.RoundToInt((centreX - stepHalfWidth) * width);
                int x1 = Mathf.RoundToInt((centreX + stepHalfWidth) * width);
                // The v axis runs the other way on screen, so the step's lower edge is the *higher* row
                // index. Ordering the pair here keeps the fill loop from being empty on a flipped axis.
                int edgeA = Mathf.RoundToInt(FrameTToTextureV(bottomT) * (height - 1f));
                int edgeB = Mathf.RoundToInt(FrameTToTextureV(bottomT + stepHeight) * (height - 1f));
                int y0 = Mathf.Min(edgeA, edgeB);
                int y1 = Mathf.Max(edgeA, edgeB);
                for (int y = Mathf.Max(0, y0); y <= Mathf.Min(height - 1, y1); y++)
                {
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(width - 1, x1); x++)
                        pixels[y * width + x] = colour;
                }

                // One lit window per step on the near layer: the "distant city" read, and the only bright
                // marks in the distance. They stay inside the tower so they never soften its silhouette.
                if (layer == 2 && step == 0 && y1 > y0 + 2)
                {
                    int wx = Mathf.RoundToInt((centreX - stepHalfWidth * 0.35f) * width);
                    int wy = Mathf.RoundToInt(FrameTToTextureV(bottomT + stepHeight * 0.45f) * (height - 1f));
                    for (int y = wy; y < Mathf.Min(height, wy + 2); y++)
                    {
                        if (y < 0)
                            continue;
                        pixels[y * width + Mathf.Clamp(wx, 0, width - 1)] = new Color(
                            windowLight, windowLight, windowLight);
                        pixels[y * width + Mathf.Clamp(wx + 1, 0, width - 1)] = new Color(
                            windowLight, windowLight, windowLight);
                    }
                }
            }
        }

        /// <summary>
        /// Frame fraction (0 bottom, 1 top) to the backdrop texture's v axis. Inverted, because the panel is
        /// a cube seen from behind (it is a child of the camera, so the camera looks at the cube's -Z face)
        /// and that face's v axis runs the other way: without this the whole painting comes out upside down,
        /// with the ruins hanging off the haze instead of standing on it.
        /// </summary>
        private static float FrameTToTextureV(float t) => 0.5f - (t - 0.5f) / BackdropMargin;

        /// <summary>
        /// A vertical ramp multiplied into every generated surface. The fade towards the base stands in for
        /// the shading a lit material would give, so a ledge reads as a face catching light at its top edge
        /// instead of one flat slab of colour.
        /// </summary>
        /// <summary>
        /// The scene's post-processing profile: bloom for the glowing blocks and a mild vignette.
        /// </summary>
        private static VolumeProfile GetOrCreatePostFxProfile()
        {
            const string path = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01PostFx.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            Bloom bloom = profile.Has<Bloom>() ? profile.components.Find(c => c is Bloom) as Bloom : profile.Add<Bloom>();
            bloom.active = true;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.8f;
            // Deliberately above white. The level is white content on blue, so a luminance bloom below 1
            // would smear the whole structure; instead only the emissive props - authored above 1 - glow.
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.7f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(1f, 0.97f, 0.9f);

            Vignette vignette = profile.Has<Vignette>()
                ? profile.components.Find(c => c is Vignette) as Vignette
                : profile.Add<Vignette>();
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.34f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.42f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Texture2D GetOrCreateSurfaceGradient()
        {
            const string path = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01SurfaceGradient.asset";
            const int shadingSteps = 32;
            const int heightSteps = 128;

            Texture2D gradient = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (gradient != null && (gradient.width != shadingSteps || gradient.height != heightSteps))
            {
                AssetDatabase.DeleteAsset(path);
                gradient = null;
            }
            if (gradient == null)
            {
                gradient = new Texture2D(shadingSteps, heightSteps, TextureFormat.RGBA32, false)
                {
                    name = "Phase01 Surface Gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                AssetDatabase.CreateAsset(gradient, path);
            }

            for (int x = 0; x < gradient.width; x++)
            {
                // u: how much light the face receives, baked from the sun direction in the mesh builder.
                // The range is wide (rather than a token 30%) because the level is unlit: a face turned
                // away from the sun has no other way of reading as a face. A branch seen side-on shows
                // one lit wall and one shaded wall, and that difference is most of what gives a ledge
                // its form.
                float shade = Mathf.Lerp(0.54f, 1.02f, x / (gradient.width - 1f));
                for (int y = 0; y < gradient.height; y++)
                {
                    // v: the wall is white where it meets the walkable lip and falls away to near black
                    // a few metres down, so a ledge reads as pale stone dropping into shadow instead of
                    // one flat panel. The v axis of a cliff quad is its height, so this is a vertical
                    // value ramp: the "strong black/white contrast" the Phase 1 sheet asks for, achieved
                    // without any extra geometry and without touching the collider.
                    float v = y / (gradient.height - 1f);
                    // The exponent is what decides how much of the falloff the player actually sees. A cliff
                    // quad runs from the walkable lip (v = 1) down to 6 m below it, but the gameplay frame
                    // only reaches about 3.2 m below the lip, so an even ramp would spend its whole black end
                    // off screen and leave a flat mid grey in the frame. Falling fast early puts the dark end
                    // inside the frame, where it silhouettes the walkway against the luminous haze.
                    float ramp = Mathf.Lerp(0.13f, 1f, Mathf.Pow(v, 2f));
                    // Sedimentary banding, so a wall reads as carved rock at gameplay framing. Faded out
                    // as it approaches the lip, which keeps the walkable edge a clean unbroken bright
                    // line - the one cue the player must never have to read through texture.
                    float strata = 1f
                        + 0.09f * Mathf.Sin(v * 58f)
                        + 0.06f * Mathf.Sin(v * 23f + 1.7f)
                        + 0.05f * Mathf.Sin(v * 9.5f + 0.6f);
                    strata = Mathf.Lerp(strata, 1f, Mathf.SmoothStep(0.8f, 1f, v));
                    float value = Mathf.Clamp(shade * ramp * strata, 0.02f, 1.05f);
                    gradient.SetPixel(x, y, new Color(value, value, value, 1f));
                }
            }
            gradient.Apply();
            EditorUtility.SetDirty(gradient);
            return gradient;
        }

        private static Material GetOrCreateSkyboxMaterial()
        {
            const string path = "Assets/Phase01_Demo0/GeneratedMaterials/Phase01Skybox.mat";
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
                return AssetDatabase.LoadAssetAtPath<Material>(path);

            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (skybox == null)
            {
                skybox = new Material(shader) { name = "Phase01 Skybox" };
                AssetDatabase.CreateAsset(skybox, path);
            }
            skybox.shader = shader;
            // The Phase 1 design sheet calls the look directly: blue background, white content.
            // Phase 1 is the colourless phase: the sheet asks for blue background / white content and the
            // archive summary calls the presentation "strong black/white contrast". Only the collectibles
            // carry hue, because colour is what each later phase regains (phase 3 sheet: "gradual return of
            // color"). A saturated sky here would spend that progression in phase 1.
            // Hollow Knight's depth read is a LUMINOUS haze behind dark silhouettes, not a dark void behind
            // pale shapes. The first passes had it inverted, which is why they read as slabs in a void.
            // The atmosphere is thick on purpose: it is what puts a bright band on the horizon and lets the
            // sky deepen towards the top of the frame, so the palest value in the level is the far distance
            // rather than any surface the player can touch.
            skybox.SetColor("_SkyTint", new Color(0.5f, 0.57f, 0.68f));
            skybox.SetColor("_GroundColor", new Color(0.34f, 0.39f, 0.47f));
            skybox.SetFloat("_AtmosphereThickness", 2.4f);
            skybox.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(skybox);
            return skybox;
        }

        private static Material GetOrCreatePlaceholderMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PlaceholderMaterialPath);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new BuildFailedException("URP Unlit shader is unavailable for Phase01 placeholder material.");

            material = new Material(shader)
            {
                name = "Phase01 Placeholder Unlit"
            };
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, PlaceholderMaterialPath);
            return material;
        }

        private static float[] ConstantWidths(int count, float width)
        {
            float[] widths = new float[count];
            for (int i = 0; i < widths.Length; i++)
                widths[i] = width;
            return widths;
        }

        /// <summary>
        /// Gap flags derived from the runtime's authored inner/outer bounds, so the authored scene and the
        /// runtime profile can never disagree about which segments are voids.
        /// </summary>
        private static bool[] GapFlagsFor(float[] scalars, float[] bounds)
        {
            bool[] flags = new bool[scalars.Length - 1];
            for (int i = 0; i < flags.Length; i++)
            {
                float midpoint = (scalars[i] + scalars[i + 1]) * 0.5f;
                for (int b = 0; b + 1 < bounds.Length; b += 2)
                {
                    if (midpoint > bounds[b] && midpoint < bounds[b + 1])
                    {
                        flags[i] = true;
                        break;
                    }
                }
            }
            return flags;
        }

        private static float[] BottomHeights(int count)
        {
            float[] heights = new float[count];
            for (int i = 0; i < heights.Length; i++)
                heights[i] = -5.8f - (i % 3) * 0.25f;
            return heights;
        }

        private static float[] BottomScalars(float[] scalars)
        {
            float[] bottoms = new float[scalars.Length];
            for (int i = 0; i < bottoms.Length; i++)
                bottoms[i] = Mathf.Max(0f, scalars[i] - (i == 0 ? 0f : 0.2f));
            return bottoms;
        }

        private static Material GetOrCreateTintMaterial(Color color)
        {
            // A colour authored above 1 is the scene's "glowing" register - the collectible blocks, the
            // radio core, the lamps. Those must NOT inherit the terrain's stone gradient: the gradient
            // multiplies towards black at the bottom of every surface, which would quarter the glow off
            // every small prop and destroy the one cue the design sheet names ("发光黄色小方块").
            if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 1f)
                return GetOrCreateFlatMaterial(color);

            const string folder = "Assets/Phase01_Demo0/GeneratedMaterials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Phase01_Demo0", "GeneratedMaterials");
            Color32 bytes = color;
            string path = $"{folder}/Tint_{bytes.r:X2}{bytes.g:X2}{bytes.b:X2}{bytes.a:X2}.mat";
            return GetOrCreateTintMaterialAtPath(color, path);
        }

        /// <summary>
        /// A flat, textureless surface for the emissive props. Unlit with no base map, so the authored
        /// colour - which is already above 1 and therefore clips to the brightest value the fog and bloom
        /// can carry - is what the player sees, evenly, on every face.
        /// </summary>
        private static Material GetOrCreateFlatMaterial(Color color)
        {
            const string folder = "Assets/Phase01_Demo0/GeneratedMaterials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Phase01_Demo0", "GeneratedMaterials");
            Color32 bytes = color;
            string path = $"{folder}/Emissive_{bytes.r:X2}{bytes.g:X2}{bytes.b:X2}{bytes.a:X2}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new BuildFailedException($"Expected a Material at deterministic emissive path '{path}'.");
            material = new Material(ResolveSurfaceShader())
            {
                name = $"Phase01 Emissive {bytes.r:X2}{bytes.g:X2}{bytes.b:X2}"
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// The shader every generated surface uses. Lit shading is what gives the greybox form - an unlit
        /// tint renders each ledge as one flat slab, which is why the level read as programmer art. The
        /// placeholder unlit shader stays as the fallback so the scene can always be rebuilt.
        /// </summary>
        // The project renders through the URP 2D renderer, which does not drive 3D lights: a Lit surface comes
        // out unlit here and also loses the skybox. The generated surfaces therefore stay on the unlit shader
        // and get their form from the baked surface gradient instead.
        private const string SurfaceShaderName = "Universal Render Pipeline/Unlit";

        private static Shader ResolveSurfaceShader()
        {
            Shader lit = Shader.Find(SurfaceShaderName);
            return lit != null ? lit : placeholderMaterial.shader;
        }

        private static Material GetOrCreateTintMaterialAtPath(Color color, string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                // Keep existing generated materials on the current surface shader without disturbing the
                // per-renderer tints, which are applied as property blocks.
                Shader surface = ResolveSurfaceShader();
                if (surface != null && material.shader != surface)
                    material.shader = surface;
                // Sync the authored colour as well. These materials are addressed by a fixed path rather
                // than by their colour, so a prop whose colour is retuned in code would otherwise keep
                // rendering the colour it was first created with - which is how a red-hot collapse segment
                // survived a pass that had already retuned it to cool stone.
                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                material.SetTexture("_BaseMap", GetOrCreateSurfaceGradient());
                EditorUtility.SetDirty(material);
                return material;
            }
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new BuildFailedException($"Expected a Material at deterministic tint path '{path}'.");
            material = new Material(ResolveSurfaceShader())
            {
                name = $"Phase01 Tint {((Color32)color).r:X2}{((Color32)color).g:X2}{((Color32)color).b:X2}{((Color32)color).a:X2}"
            };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", 0.18f);
            material.SetFloat("_Metallic", 0f);
            material.SetTexture("_BaseMap", GetOrCreateSurfaceGradient());
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PhysicsMaterial GetCanonicalPhysicsMaterial()
        {
            PhysicsMaterial existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
            if (existing != null)
                return existing;
            AssetDatabase.ImportAsset(
                PhysicsMaterialPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
            if (existing != null)
                return existing;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                GameObject player = GameObject.Find("Placeholder Player");
                existing = player != null
                    ? player.GetComponent<CapsuleCollider>()?.sharedMaterial
                    : null;
                if (existing != null
                    && AssetDatabase.GetAssetPath(existing) == PhysicsMaterialPath)
                    return existing;
            }
            throw new BuildFailedException(
                $"Canonical Phase 01 no-friction material is missing at '{PhysicsMaterialPath}'.");
        }
    }
}
