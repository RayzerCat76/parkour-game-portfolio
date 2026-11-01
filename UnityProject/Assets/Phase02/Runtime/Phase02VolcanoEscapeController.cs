using System;
using System.Collections;
using System.Collections.Generic;
using Phase01.MovementSandbox;
using ProjectReturn.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Phase02.VolcanoEscape
{
    [DefaultExecutionOrder(-100)]
    public sealed class Phase02VolcanoEscapeController : MonoBehaviour
    {
        public const float AuthoredRouteLength = 1250f;
        public const int AuthoredGapCount = 50;
        public const int AuthoredCheckpointCount = 4;
        public const int AuthoredCollapsingPlatformCount = 10;
        public const int AuthoredDebrisCount = 11;
        public const int AuthoredShockwaveCount = 4;

        private static readonly float[] DesignSectionBoundaries = { 0f, 220f, 440f, 680f, 970f, 1140f };
        private static readonly float[] SectionBoundaries = { 0f, 230f, 460f, 710f, 1050f, AuthoredRouteLength };
        private static readonly int[] SectionJumpCounts = { 8, 10, 11, 14, 7 };
        private static readonly string[] SectionNames =
        {
            "Rupture and First Tremors",
            "Fault Line",
            "Eruption Field",
            "Collapse Chase",
            "White Opening"
        };

        private static readonly float[][] GapStarts =
        {
            new[] { 25f, 41f, 75f, 90f, 125f, 142f, 176f, 192f },
            new[] { 242f, 258f, 276f, 310f, 327f, 344f, 371f, 389f, 407f, 425f },
            new[] { 460f, 478f, 496f, 530f, 548f, 566f, 596f, 614f, 632f, 650f, 666f },
            new[] { 700f, 718f, 736f, 754f, 790f, 808f, 826f, 844f, 862f, 898f, 916f, 934f, 950f, 963f },
            new[] { 990f, 1010f, 1030f, 1060f, 1080f, 1102f, 1124f }
        };

        private static readonly float[][] GapLengths =
        {
            new[] { 1.45f, 1.7f, 1.55f, 1.9f, 1.5f, 1.75f, 1.6f, 1.85f },
            new[] { 1.5f, 1.8f, 1.6f, 1.95f, 1.45f, 1.75f, 1.55f, 2f, 1.65f, 1.8f },
            new[] { 1.55f, 1.9f, 1.65f, 2f, 1.5f, 1.8f, 1.6f, 1.95f, 1.55f, 1.85f, 1.7f },
            new[] { 1.6f, 1.95f, 1.55f, 1.85f, 1.7f, 2f, 1.6f, 1.9f, 1.5f, 1.8f, 1.65f, 1.95f, 1.55f, 1.85f },
            new[] { 1.5f, 1.8f, 1.55f, 1.9f, 1.6f, 1.75f, 1.85f }
        };

        private static readonly float[][] LandingHeights =
        {
            new[] { 0.2f, 0.45f, 0.15f, 0.6f, 0.3f, 0f, 0.35f, 0.1f },
            new[] { 0.25f, 0.65f, 0.35f, 0.8f, 0.45f, 0.1f, 0.55f, 0.2f, 0.7f, 0.15f },
            new[] { 0.4f, 0.9f, 0.45f, 1f, 0.6f, 0.2f, 0.75f, 0.35f, 0.95f, 0.5f, 0.15f },
            new[] { 0.3f, 0.8f, 0.4f, 1f, 0.55f, 0.15f, 0.7f, 0.25f, 0.9f, 0.45f, 0.1f, 0.65f, 0.3f, 0.05f },
            new[] { 0.25f, 0.55f, 0.25f, 0.6f, 0.3f, 0.15f, 0f }
        };

        private static readonly CollapseSpec[] CollapseSpecs =
        {
            new CollapseSpec(0, 58f, 64f),
            new CollapseSpec(0, 156f, 162f),
            new CollapseSpec(1, 296f, 302f),
            new CollapseSpec(1, 357f, 363f),
            new CollapseSpec(2, 512f, 518f),
            new CollapseSpec(2, 520f, 526f),
            new CollapseSpec(2, 580f, 586f),
            new CollapseSpec(2, 588f, 594f),
            new CollapseSpec(3, 774f, 780f),
            new CollapseSpec(3, 880f, 886f)
        };

        private static readonly HazardPoint[] DebrisSpecs =
        {
            new HazardPoint(0, 110f), new HazardPoint(0, 205f),
            new HazardPoint(1, 287f), new HazardPoint(1, 365f), new HazardPoint(1, 432f),
            new HazardPoint(2, 505f), new HazardPoint(2, 574f), new HazardPoint(2, 642f),
            new HazardPoint(3, 768f), new HazardPoint(3, 868f), new HazardPoint(3, 944f)
        };

        private static readonly HazardPoint[] ShockwaveSpecs =
        {
            new HazardPoint(1, 400f),
            new HazardPoint(2, 540f),
            new HazardPoint(2, 620f),
            new HazardPoint(3, 905f)
        };

        private readonly List<Vector2> gapIntervals = new List<Vector2>(AuthoredGapCount);
        private readonly List<PlatformSpan> platformSpans = new List<PlatformSpan>(128);
        private readonly List<Phase02ResettableHazard> hazards = new List<Phase02ResettableHazard>(32);
        private readonly Vector3[] checkpointPositions = new Vector3[AuthoredCheckpointCount];
        private readonly bool[] checkpointReached = new bool[AuthoredCheckpointCount];

        private ProjectServicesRoot services;
        private Transform generatedRoot;
        private Transform playerTransform;
        private Phase01ParkourMotor motor;
        private Phase02CameraRig cameraRig;
        private Phase02PacingMonitor pacing;
        private Phase02AcceptanceDriver acceptanceDriver;
        private Phase02CollapseFront collapseFront;
        private GameObject volcanoBackground;
        private GameObject distantOpeningBeacon;
        private GameObject whiteOpening;
        private Coroutine resetRoutine;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle debugStyle;
        private float failureFlash;
        private bool playable;
        private bool completing;
        private bool whiteOpeningSighted;
        private bool whiteOpeningApproached;
        private bool finalJumpStarted;
        private int currentSectionIndex;
        private int currentCheckpointIndex = -1;
        private int totalFailures;

        public bool IsBuilt { get; private set; }
        public bool IsPlayable => playable && !completing;
        public bool CompletionRequested => completing;
        public bool IsResetting => resetRoutine != null;
        public int CurrentSectionIndex => currentSectionIndex;
        public int CurrentCheckpointIndex => currentCheckpointIndex;
        public int TotalGapCount => gapIntervals.Count;
        public int CheckpointCount => checkpointPositions.Length;
        public int CollapsingPlatformCount { get; private set; }
        public int DebrisHazardCount { get; private set; }
        public int ShockwaveHazardCount { get; private set; }
        public int CollapseFrontCount => collapseFront == null ? 0 : 1;
        public int TotalFailures => totalFailures;
        public float RouteLength => AuthoredRouteLength;
        public float PlayerX => playerTransform != null ? playerTransform.position.x : 0f;
        public float PlayerY => playerTransform != null ? playerTransform.position.y : 0f;
        public float PhaseProgress => Mathf.Clamp01(PlayerX / AuthoredRouteLength);
        public float CollapseProximity => collapseFront != null ? collapseFront.ProximityNormalized : 0f;
        public float FailurePressure => Mathf.Clamp01(totalFailures * 0.08f);
        public float HazeDensity
        {
            get
            {
                if (PlayerX < SectionBoundaries[4])
                    return Mathf.Lerp(0.88f, 0.58f, PhaseProgress);
                return Mathf.Lerp(0.58f, 0.04f, Mathf.InverseLerp(SectionBoundaries[4], AuthoredRouteLength, PlayerX));
            }
        }
        public float DangerIntensity
        {
            get
            {
                switch (currentSectionIndex)
                {
                    case 0: return 0.28f;
                    case 1: return 0.48f;
                    case 2: return 0.7f;
                    case 3: return Mathf.Max(0.88f, CollapseProximity);
                    default: return Mathf.Lerp(0.45f, 0.1f, Mathf.InverseLerp(SectionBoundaries[4], AuthoredRouteLength, PlayerX));
                }
            }
        }
        public IReadOnlyList<Vector2> GapIntervals => gapIntervals;
        public IReadOnlyList<float> SectionSplits => pacing != null ? pacing.SectionSplits : Array.Empty<float>();
        public Phase01ParkourMotor Motor => motor;
        public Phase02AcceptanceDriver AcceptanceDriver => acceptanceDriver;
        public Transform PlayerTransform => playerTransform;
        public GameObject VolcanoBackground => volcanoBackground;
        public GameObject WhiteOpening => whiteOpening;
        public static IReadOnlyList<int> AuthoredSectionJumpCounts => SectionJumpCounts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Phase02")
                return;

            PlaceholderSceneController[] placeholders = FindObjectsByType<PlaceholderSceneController>(FindObjectsSortMode.None);
            foreach (PlaceholderSceneController placeholder in placeholders)
            {
                if (placeholder != null)
                    placeholder.enabled = false;
            }

            if (FindAnyObjectByType<Phase02VolcanoEscapeController>() == null)
            {
                GameObject controllerObject = new GameObject("PHASE 02 Volcano Escape Controller");
                controllerObject.AddComponent<Phase02VolcanoEscapeController>();
            }
        }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "Phase02")
            {
                enabled = false;
                return;
            }

            services = ProjectServicesRoot.EnsureExists();
            BuildGreybox();
        }

        private IEnumerator Start()
        {
            if (!IsBuilt)
                yield break;

            yield return null;
            Physics.SyncTransforms();
            playable = true;
            pacing.Begin();
            services.Music.NotifyPhase2Entered();
            services.Music.NotifyPhase2RuptureStarted();
            services.Music.NotifyPhase2SectionChanged(0);
            UpdateMusicParameters();
        }

        private void Update()
        {
            if (!IsPlayable || playerTransform == null)
                return;

            failureFlash = Mathf.MoveTowards(failureFlash, 0f, Time.unscaledDeltaTime * 2.8f);
            if (PlayerY < -12f)
            {
                Fail(currentSectionIndex, "Fall");
                return;
            }

            UpdateSectionAndCheckpoint();
            UpdateMilestones();
            UpdateMusicParameters();

            if (!finalJumpStarted
                && PlayerX >= AuthoredRouteLength - 20f
                && motor != null
                && motor.VerticalVelocity > 0.25f)
            {
                finalJumpStarted = true;
                pacing.MarkFinalJumpStarted();
                services.Music.NotifyPhase2FinalJumpStarted();
            }

            if (!completing && PlayerX >= AuthoredRouteLength - 3f && PlayerY >= 1.05f)
                CompletePhase();
        }

        public void RegisterHazard(Phase02ResettableHazard hazard)
        {
            if (hazard != null && !hazards.Contains(hazard))
                hazards.Add(hazard);
        }

        public bool IsGapAt(float x)
        {
            for (int i = 0; i < gapIntervals.Count; i++)
            {
                Vector2 gap = gapIntervals[i];
                if (x > gap.x + 0.02f && x < gap.y - 0.02f)
                    return true;
            }
            return false;
        }

        public float SurfaceHeightAt(float x)
        {
            for (int i = 0; i < platformSpans.Count; i++)
            {
                PlatformSpan span = platformSpans[i];
                if (x >= span.Start && x <= span.End)
                    return span.TopY;
            }
            return 0f;
        }

        public void NotifyHazardWarning(string hazardType)
        {
            services.Music.NotifyPhase2HazardWarning(hazardType);
        }

        public void NotifyHazardTriggered(string hazardType)
        {
            services.Music.NotifyPhase2HazardTriggered(hazardType);
        }

        public void Fail(int sectionIndex, string hazardType)
        {
            if (!IsPlayable || IsResetting)
                return;

            resetRoutine = StartCoroutine(ResetRoutine(Mathf.Clamp(sectionIndex, 0, 4), hazardType));
        }

        private IEnumerator ResetRoutine(int sectionIndex, string hazardType)
        {
            playable = false;
            totalFailures++;
            failureFlash = 1f;
            motor.SetInputLocked(true);
            pacing.MarkFailure(sectionIndex);
            services.Music.NotifyPhase2PlayerFailed(sectionIndex);
            Debug.Log($"[Phase02] Local reset in Section {sectionIndex + 1}: {hazardType}");
            yield return new WaitForSecondsRealtime(0.22f);

            for (int i = 0; i < hazards.Count; i++)
            {
                Phase02ResettableHazard hazard = hazards[i];
                if (hazard != null && hazard.SectionIndex == sectionIndex)
                    hazard.ResetHazard();
            }

            Vector3 respawn = currentCheckpointIndex >= 0
                ? checkpointPositions[currentCheckpointIndex]
                : new Vector3(4f, SurfaceHeightAt(4f), 0f);
            motor.TeleportAndReset(respawn);
            cameraRig.SnapToTarget();
            yield return new WaitForSecondsRealtime(0.18f);

            if (sectionIndex == 3 && currentSectionIndex == 3)
                collapseFront.Activate(SectionBoundaries[3]);

            services.Music.NotifyPhase2PlayerReset(sectionIndex);
            motor.SetInputLocked(false);
            playable = true;
            resetRoutine = null;
        }

        private void CompletePhase()
        {
            if (completing)
                return;

            if (!services.GameFlow.RequestPhase(ProjectPhase.Phase03))
                return;

            completing = true;
            playable = false;
            motor.SetInputLocked(true);
            pacing.MarkCompleted(currentSectionIndex);
            services.Music.NotifyPhase2Completed();
            UpdateMusicParameters();
        }

        private void UpdateSectionAndCheckpoint()
        {
            int resolved = Mathf.Max(currentSectionIndex, ResolveSection(PlayerX));
            if (resolved != currentSectionIndex)
            {
                int previous = currentSectionIndex;
                currentSectionIndex = resolved;
                pacing.MarkSectionChanged(previous, resolved);
                services.Music.NotifyPhase2SectionChanged(resolved);

                if (resolved == 3)
                {
                    collapseFront.Activate(SectionBoundaries[3]);
                    services.Music.NotifyPhase2CollapseFrontStarted();
                }
                else if (resolved >= 4)
                {
                    collapseFront.Deactivate();
                }
            }

            for (int i = 0; i < checkpointPositions.Length; i++)
            {
                if (checkpointReached[i] || PlayerX < SectionBoundaries[i + 1] + 2f)
                    continue;

                checkpointReached[i] = true;
                currentCheckpointIndex = i;
                pacing.MarkCheckpoint(i);
                services.Music.NotifyPhase2CheckpointReached(i);
            }
        }

        private void UpdateMilestones()
        {
            if (!whiteOpeningSighted && PlayerX >= SectionBoundaries[3] + 230f)
            {
                whiteOpeningSighted = true;
                if (distantOpeningBeacon != null)
                    distantOpeningBeacon.SetActive(true);
                pacing.MarkWhiteOpeningSighted();
                services.Music.NotifyPhase2WhiteOpeningSighted();
            }

            if (!whiteOpeningApproached && PlayerX >= AuthoredRouteLength - 80f)
            {
                whiteOpeningApproached = true;
                if (distantOpeningBeacon != null)
                    distantOpeningBeacon.transform.localScale = new Vector3(1.1f, 3.4f, 0.35f);
                pacing.MarkWhiteOpeningApproached();
                services.Music.NotifyPhase2WhiteOpeningApproached();
            }
        }

        private void UpdateMusicParameters()
        {
            services.Music.SetPhase2Parameters(
                PhaseProgress,
                DangerIntensity,
                CollapseProximity,
                HazeDensity,
                FailurePressure);
        }

        public static float GetSectionBoundary(int index)
        {
            return SectionBoundaries[Mathf.Clamp(index, 0, SectionBoundaries.Length - 1)];
        }

        public static float MapDesignX(int sectionIndex, float designX)
        {
            int section = Mathf.Clamp(sectionIndex, 0, 4);
            float normalized = Mathf.InverseLerp(
                DesignSectionBoundaries[section],
                DesignSectionBoundaries[section + 1],
                designX);
            return Mathf.Lerp(SectionBoundaries[section], SectionBoundaries[section + 1], normalized);
        }

        private int ResolveSection(float x)
        {
            for (int i = 0; i < SectionBoundaries.Length - 1; i++)
            {
                if (x < SectionBoundaries[i + 1])
                    return i;
            }
            return 4;
        }

        private void BuildGreybox()
        {
            generatedRoot = new GameObject("Phase02 Generated Greybox TASK004").transform;
            BuildGapIntervals();
            BuildForegroundRoute();
            BuildPlayer();
            BuildCameraAndBackground();
            BuildHazards();
            BuildWhiteOpening();
            BuildCheckpoints();
            pacing = gameObject.AddComponent<Phase02PacingMonitor>();
            acceptanceDriver = gameObject.AddComponent<Phase02AcceptanceDriver>();
            acceptanceDriver.Configure(this, motor);
            IsBuilt = true;
        }

        private void BuildGapIntervals()
        {
            gapIntervals.Clear();
            for (int section = 0; section < GapStarts.Length; section++)
            {
                for (int i = 0; i < GapStarts[section].Length; i++)
                {
                    float designStart = GapStarts[section][i];
                    float designEnd = designStart + GapLengths[section][i];
                    gapIntervals.Add(new Vector2(
                        MapDesignX(section, designStart),
                        MapDesignX(section, designEnd)));
                }
            }
            gapIntervals.Sort((a, b) => a.x.CompareTo(b.x));
        }

        private void BuildForegroundRoute()
        {
            Transform routeRoot = new GameObject("Foreground One-Way Parkour Route").transform;
            routeRoot.SetParent(generatedRoot);
            float cursor = 0f;
            float topY = 0f;
            int globalGapIndex = 0;

            for (int section = 0; section < GapStarts.Length; section++)
            {
                for (int i = 0; i < GapStarts[section].Length; i++)
                {
                    float designGapStart = GapStarts[section][i];
                    float gapStart = MapDesignX(section, designGapStart);
                    CreateSpanWithCollapseSplits(routeRoot, cursor, gapStart, topY, section);
                    float gapEnd = MapDesignX(section, designGapStart + GapLengths[section][i]);
                    cursor = gapEnd;
                    topY = LandingHeights[section][i];
                    globalGapIndex++;
                }
            }

            CreateSpanWithCollapseSplits(routeRoot, cursor, AuthoredRouteLength + 7f, topY, 4);
            if (globalGapIndex != AuthoredGapCount)
                Debug.LogError($"[Phase02] Authored gap count mismatch: {globalGapIndex}");
        }

        private void CreateSpanWithCollapseSplits(
            Transform parent,
            float start,
            float end,
            float topY,
            int sectionIndex)
        {
            if (end - start < 0.05f)
                return;

            float cursor = start;
            for (int i = 0; i < CollapseSpecs.Length; i++)
            {
                CollapseSpec spec = CollapseSpecs[i];
                if (spec.SectionIndex != sectionIndex || spec.End <= start || spec.Start >= end)
                    continue;

                float authoredStart = MapDesignX(sectionIndex, spec.Start);
                float authoredEnd = MapDesignX(sectionIndex, spec.End);
                float collapseStart = Mathf.Max(cursor, authoredStart);
                float collapseEnd = Mathf.Min(end, authoredEnd);
                if (collapseStart > cursor + 0.05f)
                    CreatePlatform(parent, cursor, collapseStart, topY, sectionIndex, false);
                if (collapseEnd > collapseStart + 0.05f)
                    CreatePlatform(parent, collapseStart, collapseEnd, topY, sectionIndex, true);
                cursor = collapseEnd;
            }

            if (end > cursor + 0.05f)
                CreatePlatform(parent, cursor, end, topY, sectionIndex, false);
        }

        private void CreatePlatform(
            Transform parent,
            float start,
            float end,
            float topY,
            int sectionIndex,
            bool collapsible)
        {
            Color color = SectionColor(sectionIndex);
            if (collapsible)
                color = new Color(0.5f, 0.17f, 0.11f);
            GameObject platform = CreatePrimitive(
                collapsible ? $"S{sectionIndex + 1} Collapsing Ground {start:F0}-{end:F0}" : $"S{sectionIndex + 1} Stable Platform {start:F0}-{end:F0}",
                PrimitiveType.Cube,
                parent,
                new Vector3((start + end) * 0.5f, topY - 0.4f, 0f),
                new Vector3(end - start, 0.8f, 3.2f),
                color,
                true);
            platformSpans.Add(new PlatformSpan(start, end, topY));

            if (collapsible)
            {
                Phase02CollapsingPlatform hazard = platform.AddComponent<Phase02CollapsingPlatform>();
                hazard.Configure(this, sectionIndex, platform.GetComponent<Renderer>(), platform.GetComponent<Collider>());
                CollapsingPlatformCount++;
            }
        }

        private void BuildPlayer()
        {
            GameObject player = new GameObject("Phase02 Player");
            player.transform.SetParent(generatedRoot);
            player.transform.position = new Vector3(4f, SurfaceHeightAt(4f), 0f);
            Rigidbody body = player.AddComponent<Rigidbody>();
            body.mass = 1f;
            CapsuleCollider capsule = player.AddComponent<CapsuleCollider>();
            capsule.radius = 0.32f;
            capsule.height = 1.44f;
            capsule.center = Vector3.up * 0.72f;
            PhysicsMaterial noFriction = new PhysicsMaterial("Phase02 No Friction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            capsule.material = noFriction;

            GameObject visual = CreatePrimitive(
                "Player Visual",
                PrimitiveType.Capsule,
                player.transform,
                player.transform.position + Vector3.up * 0.72f,
                new Vector3(0.64f, 0.72f, 0.64f),
                new Color(0.72f, 0.82f, 0.9f),
                false);
            visual.transform.localPosition = Vector3.up * 0.72f;
            motor = player.AddComponent<Phase01ParkourMotor>();
            motor.SetMovementAxis(Vector3.right, Vector3.zero, 0f, AuthoredRouteLength + 4f);
            motor.SetGroundingValidityProbe(position => !IsGapAt(position.x));
            playerTransform = player.transform;
        }

        private void BuildCameraAndBackground()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            cameraRig = camera.gameObject.GetComponent<Phase02CameraRig>();
            if (cameraRig == null)
                cameraRig = camera.gameObject.AddComponent<Phase02CameraRig>();
            cameraRig.Configure(camera, playerTransform, this);

            Transform background = new GameObject("Static Volcano Background").transform;
            background.SetParent(camera.transform, false);
            volcanoBackground = background.gameObject;
            CreateCameraPrimitive("Dark Red Sky", PrimitiveType.Cube, background, new Vector3(0f, 0f, 30f), new Vector3(40f, 24f, 0.3f), new Color(0.12f, 0.018f, 0.025f));
            GameObject leftSlope = CreateCameraPrimitive("Volcano Left Slope", PrimitiveType.Cube, background, new Vector3(-3.7f, -1.8f, 27f), new Vector3(11f, 5.5f, 0.7f), new Color(0.045f, 0.04f, 0.045f));
            leftSlope.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
            GameObject rightSlope = CreateCameraPrimitive("Volcano Right Slope", PrimitiveType.Cube, background, new Vector3(3.7f, -1.8f, 27f), new Vector3(11f, 5.5f, 0.7f), new Color(0.045f, 0.04f, 0.045f));
            rightSlope.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
            CreateCameraPrimitive("Volcano Crater Glow", PrimitiveType.Sphere, background, new Vector3(0f, 1.15f, 26.5f), new Vector3(2.6f, 0.6f, 0.7f), new Color(0.72f, 0.07f, 0.025f));
            CreateCameraPrimitive("Distant Ridge Left", PrimitiveType.Cube, background, new Vector3(-12f, -4.4f, 28f), new Vector3(12f, 3f, 0.5f), new Color(0.025f, 0.026f, 0.03f)).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            CreateCameraPrimitive("Distant Ridge Right", PrimitiveType.Cube, background, new Vector3(12f, -4.6f, 28f), new Vector3(12f, 3f, 0.5f), new Color(0.025f, 0.026f, 0.03f)).transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            distantOpeningBeacon = CreateCameraPrimitive(
                "Distant White Opening Beacon",
                PrimitiveType.Cube,
                background,
                new Vector3(8.2f, 2.35f, 25.5f),
                new Vector3(0.45f, 1.8f, 0.3f),
                new Color(0.92f, 0.95f, 1f));
            distantOpeningBeacon.SetActive(false);
        }

        private void BuildHazards()
        {
            Transform hazardRoot = new GameObject("Deterministic Foreground Hazards").transform;
            hazardRoot.SetParent(generatedRoot);

            for (int i = 0; i < DebrisSpecs.Length; i++)
            {
                HazardPoint spec = DebrisSpecs[i];
                float hazardX = MapDesignX(spec.SectionIndex, spec.X);
                float groundY = SurfaceHeightAt(hazardX);
                GameObject hazardObject = new GameObject($"S{spec.SectionIndex + 1} Falling Debris {i + 1}");
                hazardObject.transform.SetParent(hazardRoot);
                GameObject marker = CreatePrimitive(
                    "Warning Marker",
                    PrimitiveType.Cylinder,
                    hazardObject.transform,
                    new Vector3(hazardX, groundY + 0.035f, 0f),
                    new Vector3(1.5f, 0.035f, 1.5f),
                    new Color(0.9f, 0.08f, 0.025f),
                    false);
                GameObject debris = CreatePrimitive(
                    "Debris Visual",
                    PrimitiveType.Sphere,
                    hazardObject.transform,
                    new Vector3(hazardX, groundY + 12f, 0f),
                    Vector3.one * 1.35f,
                    new Color(0.12f, 0.1f, 0.1f),
                    false);
                Phase02DebrisHazard hazard = hazardObject.AddComponent<Phase02DebrisHazard>();
                hazard.Configure(this, spec.SectionIndex, hazardX - 11f, hazardX, groundY, marker, debris);
                DebrisHazardCount++;
            }

            for (int i = 0; i < ShockwaveSpecs.Length; i++)
            {
                HazardPoint spec = ShockwaveSpecs[i];
                float hazardX = MapDesignX(spec.SectionIndex, spec.X);
                float groundY = SurfaceHeightAt(hazardX);
                GameObject hazardObject = new GameObject($"S{spec.SectionIndex + 1} Red Shockwave {i + 1}");
                hazardObject.transform.SetParent(hazardRoot);
                GameObject visual = CreatePrimitive(
                    "Shockwave Visual",
                    PrimitiveType.Cube,
                    hazardObject.transform,
                    new Vector3(hazardX - 8f, groundY + 0.55f, 0f),
                    new Vector3(0.45f, 1.1f, 3.6f),
                    new Color(0.95f, 0.05f, 0.02f),
                    false);
                Phase02ShockwaveHazard hazard = hazardObject.AddComponent<Phase02ShockwaveHazard>();
                hazard.Configure(this, spec.SectionIndex, hazardX - 12f, hazardX - 8f, hazardX + 11f, groundY, visual);
                ShockwaveHazardCount++;
            }

            GameObject frontObject = new GameObject("Section 4 Deterministic Collapse Front");
            frontObject.transform.SetParent(hazardRoot);
            GameObject frontVisual = CreatePrimitive(
                "Collapse Front Visual",
                PrimitiveType.Cube,
                frontObject.transform,
                new Vector3(SectionBoundaries[3] - 18f, 2.5f, 0f),
                new Vector3(2.4f, 8f, 4f),
                new Color(0.52f, 0.025f, 0.018f),
                false);
            collapseFront = frontObject.AddComponent<Phase02CollapseFront>();
            collapseFront.Configure(this, 3, frontVisual, SectionBoundaries[3] - 18f, 3.15f);
        }

        private void BuildWhiteOpening()
        {
            Transform root = new GameObject("Large White Opening Destination").transform;
            root.SetParent(generatedRoot);
            whiteOpening = root.gameObject;
            CreatePrimitive(
                "White Opening Glow",
                PrimitiveType.Cube,
                root,
                new Vector3(AuthoredRouteLength, 3.4f, 0f),
                new Vector3(2.2f, 7.2f, 4.5f),
                new Color(0.98f, 0.99f, 1f),
                false);
            CreatePrimitive(
                "White Opening Halo",
                PrimitiveType.Cube,
                root,
                new Vector3(AuthoredRouteLength + 0.6f, 3.4f, 0.5f),
                new Vector3(4.4f, 9f, 0.4f),
                new Color(0.55f, 0.58f, 0.62f),
                false);
        }

        private void BuildCheckpoints()
        {
            for (int i = 0; i < checkpointPositions.Length; i++)
            {
                float x = SectionBoundaries[i + 1] + 3f;
                checkpointPositions[i] = new Vector3(x, SurfaceHeightAt(x), 0f);
            }
        }

        private GameObject CreateCameraPrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 scale,
            Color color)
        {
            GameObject created = CreatePrimitive(
                objectName,
                primitiveType,
                parent,
                Vector3.zero,
                scale,
                color,
                false);
            created.transform.localPosition = localPosition;
            return created;
        }

        private GameObject CreatePrimitive(
            string objectName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool keepCollider)
        {
            GameObject created = GameObject.CreatePrimitive(primitiveType);
            created.name = objectName;
            if (parent != null)
                created.transform.SetParent(parent);
            created.transform.position = position;
            created.transform.localScale = scale;
            Collider collider = created.GetComponent<Collider>();
            if (collider != null && !keepCollider)
            {
                collider.enabled = false;
                Destroy(collider);
            }
            Renderer renderer = created.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = renderer.material;
                material.name = $"Phase02 Runtime Tint {ColorUtility.ToHtmlStringRGBA(color)}";
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
                material.color = color;
            }
            return created;
        }

        private static Color SectionColor(int sectionIndex)
        {
            switch (sectionIndex)
            {
                case 0: return new Color(0.19f, 0.17f, 0.18f);
                case 1: return new Color(0.24f, 0.15f, 0.16f);
                case 2: return new Color(0.3f, 0.13f, 0.13f);
                case 3: return new Color(0.38f, 0.1f, 0.09f);
                default: return new Color(0.48f, 0.48f, 0.5f);
            }
        }

        private void OnGUI()
        {
            if (!IsBuilt)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(0.42f, 0f, 0f, Mathf.Clamp01(HazeDensity * 0.22f));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;

            if (failureFlash > 0f)
            {
                GUI.color = new Color(1f, 0.12f, 0.04f, failureFlash * 0.35f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previousColor;
            }

            EnsureGuiStyles();
            float width = Mathf.Min(620f, Screen.width - 30f);
            Rect header = new Rect(15f, 15f, width, 92f);
            GUI.Box(header, string.Empty);
            GUI.Label(new Rect(header.x + 14f, header.y + 8f, header.width - 28f, 30f),
                $"PHASE 02 — {SectionNames[currentSectionIndex]}", titleStyle);
            string objective = currentSectionIndex < 4
                ? "Move toward the white opening"
                : "Jump into the white opening";
            GUI.Label(new Rect(header.x + 14f, header.y + 43f, header.width - 28f, 24f), objective, bodyStyle);
            GUI.Label(new Rect(header.x + 14f, header.y + 67f, header.width - 28f, 20f),
                "Move: A / D or ← / →     Jump: Space", bodyStyle);

            if (Debug.isDebugBuild || Application.isEditor)
            {
                Rect debug = new Rect(Screen.width - 330f, 15f, 315f, 196f);
                GUI.Box(debug, string.Empty);
                string splits = pacing == null ? string.Empty : pacing.BuildSummary();
                string text =
                    $"DEBUG — Volcano Escape\n" +
                    $"Section: {currentSectionIndex + 1}/5\n" +
                    $"Elapsed: {(pacing == null ? 0f : pacing.ElapsedSeconds):F1}s\n" +
                    $"Checkpoint: {currentCheckpointIndex + 1}/4\n" +
                    $"Progress: {PhaseProgress:P0}\n" +
                    $"Danger: {DangerIntensity:F2}\n" +
                    $"Collapse proximity: {CollapseProximity:F2}\n" +
                    $"Failures: {totalFailures}\n" + splits;
                GUI.Label(new Rect(debug.x + 10f, debug.y + 8f, debug.width - 20f, debug.height - 16f), text, debugStyle);
            }
        }

        private void EnsureGuiStyles()
        {
            titleStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = new Color(0.9f, 0.9f, 0.92f) }
            };
            debugStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.78f, 0.72f) }
            };
        }

        private readonly struct CollapseSpec
        {
            public CollapseSpec(int sectionIndex, float start, float end)
            {
                SectionIndex = sectionIndex;
                Start = start;
                End = end;
            }

            public int SectionIndex { get; }
            public float Start { get; }
            public float End { get; }
        }

        private readonly struct HazardPoint
        {
            public HazardPoint(int sectionIndex, float x)
            {
                SectionIndex = sectionIndex;
                X = x;
            }

            public int SectionIndex { get; }
            public float X { get; }
        }

        private readonly struct PlatformSpan
        {
            public PlatformSpan(float start, float end, float topY)
            {
                Start = start;
                End = end;
                TopY = topY;
            }

            public float Start { get; }
            public float End { get; }
            public float TopY { get; }
        }
    }
}
