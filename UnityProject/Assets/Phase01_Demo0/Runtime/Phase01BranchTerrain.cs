using System.Collections.Generic;
using UnityEngine;

namespace Phase01.Demo0
{
    public sealed class Phase01BranchTerrain : MonoBehaviour
    {
        [SerializeField] private int branchIndex;
        [SerializeField] private Vector3 outwardDirection = Vector3.right;
        private float[] profileScalars;
        private float[] profileHeights;
        private float[] cliffBottomHeights;
        private float[] cliffBottomScalars;
        private float[] halfWidths;
        private bool[] gapAfterPoint;
        private MeshFilter topFilter;
        private MeshRenderer topRenderer;
        private MeshCollider topCollider;
        private MeshFilter cliffFilter;
        private MeshRenderer cliffRenderer;
        private MeshCollider cliffCollider;
        /// <summary>
        /// Edit-time-only fallback material handed over by the scene builder. It is deliberately NOT
        /// serialized: carrying a Material reference on this component made the packed Phase 01 level
        /// unreadable in the player ("level is corrupted / Position out of bounds") while the editor was
        /// unaffected, so the runtime must rely on the material the scene already authored on the
        /// renderer instead. ApplyRenderer only ever uses this when a renderer has no material at all.
        /// </summary>
        private Material placeholderMaterial;
        private float topRed = 0.2f;
        private float topGreen = 0.58f;
        private float topBlue = 0.5f;
        private float cliffRed = 0.045f;
        private float cliffGreen = 0.16f;
        private float cliffBlue = 0.2f;

        private const string GeneratedPrefix = "Phase01 Generated";

        /// <summary>
        /// The scene's key light, shared with the scene builder so the light it authors and the shading baked
        /// into these meshes can never drift apart. The project renders through the URP 2D renderer, which
        /// does not drive 3D lights, so a ledge only gets a lit side and a shaded side from this bake.
        /// </summary>
        public const float ShadingLightPitch = 48f;
        public const float ShadingLightYaw = -32f;
        public static readonly Vector3 ShadingLightDirection =
            Quaternion.Euler(ShadingLightPitch, ShadingLightYaw, 0f) * Vector3.forward;

        /// <summary>Brightness of a face turned fully away from the sun.</summary>
        /// <summary>
        /// Brightness of a face turned fully away from the sun. Raised well above a normal shadow term
        /// because the Phase 1 design is white content on a blue background: the walls have to stay light
        /// enough to read as white, with the sun only modelling them.
        /// </summary>
        private const float ShadedFaceBrightness = 0.7f;

        /// <summary>
        /// Widens the skirt at the base of every cliff so a ledge reads as a buttress carved out of the
        /// pillar rather than a slab floating in the fog.
        /// </summary>
        private const float CliffBaseFlare = 1f;

        /// <summary>
        /// How far the underside of a cliff may wander from its authored profile. A ledge here is carved stone
        /// descending into haze, not a clean cut slab, and Breaking the silhouette is most of what separates a
        /// moody cavern from a greybox. Each branch gets its own shape so the four arms are not one stencil.
        /// </summary>
        private const float BottomRoughness = 1.7f;
        private static readonly float[] PlainScalars = { 0f, 1.4f, 44f, 88f, 132f, 174f, 178f };
        private static readonly float[] PlainHeights = { 0f, 0f, 0.2f, 0.35f, 0.25f, 0.2f, 0.15f };
        private static readonly float[] PlainWidths = ConstantWidths(PlainScalars.Length, 0.72f);
        private static readonly float[] Branch1Scalars =
        {
            1.4f,5.6f,7.81f,16.5f,18.45f,21.6f,23.55f,26.5f,28.45f,31.2f,
            33.41f,35f,38f,41f,53f,56f,58.41f,61.6f,74.4f,77.4f,
            79.3f,82.3f,83.9f,86.36f,89.1f,91f,92.9f,104.2f,105.6f,107.5f,
            111.1f,113.51f,178f,
        };
        private static readonly float[] Branch1Heights =
        {
            0f,0f,0.75f,0.75f,2.25f,2.25f,3.75f,3.75f,5.25f,5.25f,
            6f,6f,9f,9f,9f,9f,9f,9f,9f,9f,
            7.6f,7.6f,6.2f,5.8f,5.8f,7.2f,7.2f,7.2f,7.2f,8.6f,
            7f,7f,7f,
        };
        private static readonly float[] Branch1Widths =
        {
            0.72f,0.72f,0.72f,0.72f,0.6f,0.6f,0.58f,0.58f,0.56f,0.56f,
            0.6f,0.6f,0.72f,0.72f,0.7f,0.7f,0.55f,0.55f,0.72f,0.72f,
            0.72f,0.72f,0.72f,0.5f,0.5f,0.72f,0.72f,0.72f,0.72f,0.72f,
            0.72f,0.72f,0.72f,
        };
        private static readonly float[] Branch2Scalars =
        {
            1.4f,4.4f,6.61f,9.5f,11.54f,14.45f,16.4f,19.55f,21.5f,24.2f,26.15f,28.9f,30.85f,
            33.65f,36.06f,38.85f,40.91f,43.25f,45.31f,47.6f,49.66f,52f,54.06f,56.4f,58.81f,61.7f,
            64.11f,66.7f,69.7f,72.7f,90.7f,93.5f,95.45f,98.4f,100.35f,103.1f,105.05f,107.4f,109.35f,
            112.1f,114.05f,116.4f,118.35f,121.15f,123.1f,125.45f,127.4f,132f,140f,178f,
        };
        private static readonly float[] Branch2Heights =
        {
            0f,0f,0.75f,0.75f,2f,2f,3.5f,3.5f,5f,5f,
            6.5f,6.5f,8f,8f,8f,8f,6.8f,6.8f,8f,8f,
            6.8f,6.8f,8f,8f,8f,8f,8f,8f,11.5f,11.5f,
            11.5f,11.5f,10f,10f,8.5f,8.5f,10f,10f,8.5f,8.5f,
            10f,10f,8.5f,8.5f,10f,10f,8.5f,8.5f,8.5f,8.5f,
        };
        private static readonly float[] Branch2Widths =
        {
            0.72f,0.72f,0.72f,0.72f,0.65f,0.65f,0.7f,0.7f,0.55f,0.55f,0.5f,0.5f,0.48f,0.48f,
            0.7f,0.7f,0.4f,0.4f,0.4f,0.4f,0.4f,0.4f,0.42f,0.42f,0.7f,0.7f,0.65f,0.65f,
            0.72f,0.72f,0.7f,0.7f,0.55f,0.55f,0.5f,0.5f,0.42f,0.42f,0.48f,0.48f,0.4f,0.4f,
            0.5f,0.5f,0.42f,0.42f,0.72f,0.72f,0.72f,0.72f,
        };
        private static readonly float[] Branch3Scalars =
        {
            1.4f,4f,6.21f,8.9f,10.94f,13.65f,15.6f,18.2f,20.15f,22.55f,24.5f,26.85f,28.8f,
            31.15f,33.56f,36.05f,38f,39.95f,41.9f,43.85f,45.8f,47.75f,49.7f,51.65f,54.06f,56.55f,
            58.5f,61.2f,63.15f,65.85f,67.8f,70.5f,72.45f,75.15f,77.14f,80f,93.2f,96f,97.95f,
            101.3f,103.25f,106.6f,109.01f,111.8f,114.21f,117.1f,129.1f,131.9f,133.85f,137.15f,139.1f,142.4f,
            144.35f,147.65f,149.6f,152.9f,154.85f,158.15f,160.1f,163.4f,164.75f,168f,176f,178f,
        };
        private static readonly float[] Branch3Heights =
        {
            0f,0f,0.75f,0.75f,2f,2f,3.5f,3.5f,5f,5f,
            6.5f,6.5f,8f,8f,8f,8f,6.5f,6.5f,8f,8f,
            6.5f,6.5f,8f,8f,8f,8f,6.5f,6.5f,5f,5f,
            3.5f,3.5f,2f,2f,0.6f,0.6f,0.25f,0.25f,1.75f,1.75f,
            3.25f,3.25f,3.25f,3.25f,3.25f,3.25f,3.5f,3.5f,5f,5f,
            6.5f,6.5f,8f,8f,9.5f,9.5f,11f,11f,12.5f,12.5f,
            13.5f,13.5f,13.5f,13.5f,
        };
        private static readonly float[] Branch3Widths =
        {
            0.72f,0.72f,0.7f,0.7f,0.62f,0.62f,0.68f,0.68f,0.55f,0.55f,0.5f,0.5f,0.48f,0.48f,
            0.68f,0.68f,0.4f,0.4f,0.38f,0.38f,0.38f,0.38f,0.4f,0.4f,0.68f,0.68f,0.55f,0.55f,
            0.52f,0.52f,0.5f,0.5f,0.5f,0.5f,0.68f,0.68f,0.68f,0.68f,0.58f,0.58f,0.5f,0.5f,
            0.68f,0.68f,0.62f,0.62f,0.66f,0.66f,0.52f,0.52f,0.48f,0.48f,0.44f,0.44f,0.44f,0.44f,
            0.42f,0.42f,0.4f,0.4f,0.72f,0.72f,0.72f,0.72f,
        };
        private static readonly float[] Branch4Scalars =
        {
            1.4f,4.2f,6.41f,9.5f,11.54f,14.65f,16.6f,19.95f,21.9f,25f,26.95f,30.1f,32.05f,
            35.45f,37.4f,40.15f,42.1f,44.85f,46.8f,49.55f,51.5f,54.8f,56.75f,59.85f,61.8f,64.9f,
            66.85f,69.95f,71.9f,75f,77.19f,80.05f,82.21f,88f,105f,108.4f,110.35f,114.3f,116.25f,
            120.2f,122.61f,126.1f,128.05f,131.8f,133.75f,137.5f,139.45f,143.2f,145.15f,148.9f,150.85f,154.6f,
            156.55f,160.3f,162.25f,166f,168.41f,176f,178f,
        };
        private static readonly float[] Branch4Heights =
        {
            0f,0f,0.75f,0.75f,2f,2f,3.5f,3.5f,5f,5f,
            6.5f,6.5f,8f,8f,6.5f,6.5f,8f,8f,6.5f,6.5f,
            8f,8f,6.5f,6.5f,5f,5f,3.5f,3.5f,2f,2f,
            1.2f,1.2f,0.3f,0.3f,0.3f,0.3f,1.8f,1.8f,3.3f,3.3f,
            3.3f,3.3f,4.8f,4.8f,6.3f,6.3f,7.8f,7.8f,9.3f,9.3f,
            10.8f,10.8f,12.3f,12.3f,13.8f,13.8f,13.8f,13.8f,14f,
        };
        private static readonly float[] Branch4Widths =
        {
            0.72f,0.72f,0.7f,0.7f,0.62f,0.62f,0.68f,0.68f,0.55f,0.55f,0.5f,0.5f,0.68f,0.68f,
            0.36f,0.36f,0.36f,0.36f,0.36f,0.36f,0.68f,0.68f,0.55f,0.55f,0.52f,0.52f,0.5f,0.5f,
            0.5f,0.5f,0.55f,0.55f,0.68f,0.68f,0.68f,0.68f,0.58f,0.58f,0.52f,0.52f,0.68f,0.68f,
            0.48f,0.48f,0.44f,0.44f,0.42f,0.42f,0.4f,0.4f,0.38f,0.38f,0.36f,0.36f,0.36f,0.36f,
            0.72f,0.72f,0.72f,
        };

        private static readonly float[] Branch1GapBounds =
        {
            5.6f,7.81f,16.5f,18.45f,21.6f,23.55f,26.5f,28.45f,31.2f,33.41f,
            35f,38f,41f,53f,56f,58.41f,61.6f,74.4f,77.4f,79.3f,
            82.3f,83.9f,89.1f,91f,92.9f,104.2f,105.6f,107.5f,111.1f,113.51f,
        };

        /// <summary>Branch 1 authored voids: the riser lift, the ferry, and the two air currents.</summary>
        public static readonly Vector2[] Branch1PlatformVoids =
        {
            new Vector2(35f, 38f),
            new Vector2(41f, 53f),
            new Vector2(61.6f, 74.4f),
            new Vector2(92.9f, 104.2f),
        };

        /// <summary>Authored Branch 1 gap bounds as flat inner/outer pairs, for tests and tooling.</summary>
        public static IReadOnlyList<float> Branch1AuthoredGapBounds => Branch1GapBounds;

        private static readonly float[] Branch2GapBounds =
        {
            4.4f,6.61f,9.5f,11.54f,14.45f,16.4f,19.55f,21.5f,24.2f,26.15f,28.9f,30.85f,33.65f,36.06f,38.85f,40.91f,43.25f,45.31f,47.6f,49.66f,52f,54.06f,56.4f,58.81f,61.7f,64.11f,
            66.7f,69.7f,72.7f,90.7f,93.5f,95.45f,98.4f,100.35f,103.1f,105.05f,107.4f,109.35f,112.1f,114.05f,116.4f,118.35f,121.15f,123.1f,125.45f,127.4f,
        };

        /// <summary>Branch 2 authored voids: an authored riser and ferry.</summary>
        public static readonly Vector2[] Branch2PlatformVoids =
        {
            new Vector2(66.7f, 69.7f),
            new Vector2(72.7f, 90.7f),
        };

        /// <summary>Authored Branch 2 gap bounds as flat inner/outer pairs, for tests and tooling.</summary>
        public static IReadOnlyList<float> Branch2AuthoredGapBounds => Branch2GapBounds;

        private static readonly float[] Branch3GapBounds =
        {
            4f,6.21f,8.9f,10.94f,13.65f,15.6f,18.2f,20.15f,22.55f,24.5f,26.85f,28.8f,31.15f,33.56f,36.05f,38f,39.95f,41.9f,43.85f,45.8f,47.75f,49.7f,51.65f,54.06f,56.55f,58.5f,
            61.2f,63.15f,65.85f,67.8f,70.5f,72.45f,75.15f,77.14f,80f,93.2f,96f,97.95f,101.3f,103.25f,106.6f,109.01f,111.8f,114.21f,117.1f,129.1f,131.9f,133.85f,137.15f,139.1f,142.4f,144.35f,
            147.65f,149.6f,152.9f,154.85f,158.15f,160.1f,163.4f,164.75f,
        };

        /// <summary>Branch 3 authored voids: the accepted support span and the ferry.</summary>
        public static readonly Vector2[] Branch3PlatformVoids =
        {
            new Vector2(80f, 93.2f),
            new Vector2(117.1f, 129.1f),
        };

        /// <summary>Authored Branch 3 gap bounds as flat inner/outer pairs, for tests and tooling.</summary>
        public static IReadOnlyList<float> Branch3AuthoredGapBounds => Branch3GapBounds;

        private static readonly float[] Branch4GapBounds =
        {
            4.2f,6.41f,9.5f,11.54f,14.65f,16.6f,19.95f,21.9f,25f,26.95f,30.1f,32.05f,35.45f,37.4f,40.15f,42.1f,44.85f,46.8f,49.55f,51.5f,54.8f,56.75f,59.85f,61.8f,64.9f,66.85f,
            69.95f,71.9f,75f,77.19f,80.05f,82.21f,88f,105f,108.4f,110.35f,114.3f,116.25f,120.2f,122.61f,126.1f,128.05f,131.8f,133.75f,137.5f,139.45f,143.2f,145.15f,148.9f,150.85f,154.6f,156.55f,
            160.3f,162.25f,166f,168.41f,
        };

        /// <summary>Branch 4 authored voids: the riser, ferry and accepted control-collapse span.</summary>
        public static readonly Vector2[] Branch4PlatformVoids =
        {
            new Vector2(88f, 105f)
        };

        /// <summary>Authored Branch 4 gap bounds as flat inner/outer pairs, for tests and tooling.</summary>
        public static IReadOnlyList<float> Branch4AuthoredGapBounds => Branch4GapBounds;

        /// <summary>
        /// Authoritative per-branch profile, exposed so the scene builder authors exactly the data the
        /// runtime walks. The builder used to carry its own copy of these arrays and Branch 1 silently
        /// drifted (28 authored points against 40 runtime points with 40 widths), so HasValidProfile was
        /// false at authoring time, Branch 1's mesh was never written into the scene, and the branch
        /// finally rendered and collided as an empty void. Sourcing the builder from here makes that
        /// class of drift impossible. Clones are returned so a caller can never mutate the runtime profile.
        /// </summary>
        public static float[] AuthoredScalars(int branchIndex) => Clone(ProfileFor(branchIndex).scalars);

        /// <inheritdoc cref="AuthoredScalars"/>
        public static float[] AuthoredHeights(int branchIndex) => Clone(ProfileFor(branchIndex).heights);

        /// <inheritdoc cref="AuthoredScalars"/>
        public static float[] AuthoredWidths(int branchIndex) => Clone(ProfileFor(branchIndex).widths);

        /// <summary>Authored platform-void bounds as flat inner/outer pairs, one pair per authored gap.</summary>
        public static float[] AuthoredGapBounds(int branchIndex)
        {
            switch (branchIndex)
            {
                case 0: return Clone(Branch1GapBounds);
                case 1: return Clone(Branch2GapBounds);
                case 2: return Clone(Branch3GapBounds);
                case 3: return Clone(Branch4GapBounds);
                default: return new float[0];
            }
        }

        private static (float[] scalars, float[] heights, float[] widths) ProfileFor(int branchIndex)
        {
            switch (branchIndex)
            {
                case 0: return (Branch1Scalars, Branch1Heights, Branch1Widths);
                case 1: return (Branch2Scalars, Branch2Heights, Branch2Widths);
                case 2: return (Branch3Scalars, Branch3Heights, Branch3Widths);
                case 3: return (Branch4Scalars, Branch4Heights, Branch4Widths);
                default: return (PlainScalars, PlainHeights, PlainWidths);
            }
        }

        private static float[] Clone(float[] source) => source == null ? null : (float[])source.Clone();

        public int BranchIndex => branchIndex;
        public Vector3 OutwardDirection => outwardDirection;
        public int ProfilePointCount => profileScalars != null ? profileScalars.Length : 0;
        public float OuterScalar => ProfilePointCount > 0 ? profileScalars[ProfilePointCount - 1] : 0f;
        public float InnerHalfWidth => HasValidProfile ? halfWidths[0] : 0f;
        public float OuterHalfWidth => HasValidProfile ? halfWidths[ProfilePointCount - 1] : 0f;
        public float MaximumSegmentAngle { get; private set; }
        public Mesh TopMesh => topFilter != null ? topFilter.sharedMesh : null;
        public Mesh CliffMesh => cliffFilter != null ? cliffFilter.sharedMesh : null;
        public MeshRenderer TopRenderer => topRenderer;
        public MeshRenderer CliffRenderer => cliffRenderer;
        public static int RuntimeMeshCreationCount { get; private set; }
        public static int RuntimeMeshDestructionCount { get; private set; }
        public bool IsPresentationVisible => topRenderer != null
            && cliffRenderer != null
            && topRenderer.enabled
            && cliffRenderer.enabled;
        public bool IsCollisionEnabled => topCollider != null
            && cliffCollider != null
            && topCollider.enabled
            && cliffCollider.enabled;
        public bool HasValidProfile => ProfilePointCount >= 4
            && profileHeights != null
            && halfWidths != null
            && profileHeights.Length == ProfilePointCount
            && halfWidths.Length == ProfilePointCount;

        private void Awake()
        {
            ResolveTerrainParts();
            SelectProfile();
        }

        /// <summary>
        /// Cliff bottom profile. The editor builder authors these, but they are cheap pure functions of
        /// the scalars, so they are derived here too whenever they are absent - which is what lets the
        /// runtime fallback build a complete cliff mesh instead of throwing on missing data.
        /// </summary>
        private void EnsureCliffBottomProfile()
        {
            if (!HasValidProfile)
                return;
            if (cliffBottomHeights == null || cliffBottomHeights.Length != profileScalars.Length)
            {
                cliffBottomHeights = new float[profileScalars.Length];
                for (int i = 0; i < cliffBottomHeights.Length; i++)
                    cliffBottomHeights[i] = -5.8f - (i % 3) * 0.25f;
            }
            if (cliffBottomScalars == null || cliffBottomScalars.Length != profileScalars.Length)
            {
                cliffBottomScalars = new float[profileScalars.Length];
                for (int i = 0; i < cliffBottomScalars.Length; i++)
                    cliffBottomScalars[i] = Mathf.Max(0f, profileScalars[i] - (i == 0 ? 0f : 0.2f));
            }
        }

        private void Start()
        {
            // Serialized fields (the authored profile arrays and the mesh references) are only available
            // after deserialization, so the missing-mesh fallback runs here rather than in Awake. Without
            // it a stale or absent mesh asset leaves the branch rendering and colliding as an empty void.
            if (!HasValidProfile)
                return;

            EnsureCliffBottomProfile();
            if (TopMesh == null || CliffMesh == null)
                BuildMeshesForScene();
        }

        public void InitializeRuntime(int index, Vector3 direction)
        {
            branchIndex = index;
            outwardDirection = direction.normalized;
            ResolveTerrainParts();
            SelectProfile();
        }

        private void SelectProfile()
        {
            if (branchIndex == 0)
            {
                profileScalars = Branch1Scalars;
                profileHeights = Branch1Heights;
                halfWidths = Branch1Widths;
            }
            else if (branchIndex == 1)
            {
                profileScalars = Branch2Scalars;
                profileHeights = Branch2Heights;
                halfWidths = Branch2Widths;
            }
            else if (branchIndex == 2)
            {
                profileScalars = Branch3Scalars;
                profileHeights = Branch3Heights;
                halfWidths = Branch3Widths;
            }
            else if (branchIndex == 3)
            {
                profileScalars = Branch4Scalars;
                profileHeights = Branch4Heights;
                halfWidths = Branch4Widths;
            }
            else
            {
                profileScalars = PlainScalars;
                profileHeights = PlainHeights;
                halfWidths = PlainWidths;
            }
        }

        private void ResolveTerrainParts()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                MeshFilter filter = transform.GetChild(i).GetComponent<MeshFilter>();
                if (filter == null)
                    continue;
                bool isCliff = filter.gameObject.name.Contains("Cliff");
                if (isCliff)
                {
                    cliffFilter = filter;
                    cliffRenderer = filter.GetComponent<MeshRenderer>();
                    cliffCollider = filter.GetComponent<MeshCollider>();
                }
                else
                {
                    topFilter = filter;
                    topRenderer = filter.GetComponent<MeshRenderer>();
                    topCollider = filter.GetComponent<MeshCollider>();
                }
            }
        }

        public void Configure(
            int index,
            Vector3 direction,
            float[] scalars,
            float[] heights,
            float[] bottomHeights,
            float[] bottomScalars,
            float[] widths,
            bool[] gaps,
            MeshFilter walkableTopFilter,
            MeshRenderer walkableTopRenderer,
            MeshCollider walkableTopCollider,
            MeshFilter terrainCliffFilter,
            MeshRenderer terrainCliffRenderer,
            MeshCollider terrainCliffCollider,
            Material material,
            Color walkableTopColor,
            Color terrainCliffColor)
        {
            branchIndex = index;
            outwardDirection = direction.normalized;
            profileScalars = scalars;
            profileHeights = heights;
            cliffBottomHeights = bottomHeights;
            cliffBottomScalars = bottomScalars;
            halfWidths = widths;
            gapAfterPoint = gaps;
            topFilter = walkableTopFilter;
            topRenderer = walkableTopRenderer;
            topCollider = walkableTopCollider;
            cliffFilter = terrainCliffFilter;
            cliffRenderer = terrainCliffRenderer;
            cliffCollider = terrainCliffCollider;
            placeholderMaterial = material;
            topRed = walkableTopColor.r;
            topGreen = walkableTopColor.g;
            topBlue = walkableTopColor.b;
            cliffRed = terrainCliffColor.r;
            cliffGreen = terrainCliffColor.g;
            cliffBlue = terrainCliffColor.b;
            BuildMeshesForScene();
        }

        public float SampleHeight(float scalar)
        {
            if (!HasValidProfile)
                return 0f;

            scalar = Mathf.Clamp(scalar, profileScalars[0], profileScalars[ProfilePointCount - 1]);
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (scalar > profileScalars[i + 1])
                    continue;

                if (IsGapAfter(i))
                    return -8f;

                float t = Mathf.InverseLerp(profileScalars[i], profileScalars[i + 1], scalar);
                return Mathf.Lerp(profileHeights[i], profileHeights[i + 1], t);
            }

            return profileHeights[ProfilePointCount - 1];
        }

        public bool IsGapAt(float scalar)
        {
            if (!HasValidProfile)
                return false;
            scalar = Mathf.Clamp(scalar, profileScalars[0], profileScalars[ProfilePointCount - 1]);
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (scalar <= profileScalars[i + 1])
                    return IsGapAfter(i);
            }
            return false;
        }

        /// <summary>
        /// Authored ledge half-width at a scalar. Exposed so tooling and tests can prove an authored
        /// obstacle actually spans the ledge it guards rather than leaving a gap to walk around.
        /// </summary>
        public float SampleHalfWidth(float scalar)
        {
            if (!HasValidProfile)
                return 0f;

            scalar = Mathf.Clamp(scalar, profileScalars[0], profileScalars[ProfilePointCount - 1]);
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (scalar > profileScalars[i + 1])
                    continue;

                float t = Mathf.InverseLerp(profileScalars[i], profileScalars[i + 1], scalar);
                return Mathf.Lerp(halfWidths[i], halfWidths[i + 1], t);
            }

            return halfWidths[ProfilePointCount - 1];
        }

        private bool IsGapAfter(int index)
        {
            if (gapAfterPoint != null && index >= 0 && index < gapAfterPoint.Length)
                return gapAfterPoint[index];
            if (branchIndex == 0)
                return IsInterval(index, Branch1Scalars, Branch1GapBounds);
            if (branchIndex == 1)
                return IsInterval(index, Branch2Scalars, Branch2GapBounds);
            if (branchIndex == 2)
                return IsInterval(index, Branch3Scalars, Branch3GapBounds);
            if (branchIndex == 3)
                return IsInterval(index, Branch4Scalars, Branch4GapBounds);
            return false;
        }

        private static float[] ConstantWidths(int count, float width)
        {
            float[] widths = new float[count];
            for (int i = 0; i < widths.Length; i++)
                widths[i] = width;
            return widths;
        }

        private static bool IsInterval(int index, float[] scalars, params float[] bounds)
        {
            float midpoint = (scalars[index] + scalars[index + 1]) * 0.5f;
            for (int i = 0; i < bounds.Length; i += 2)
            {
                if (midpoint > bounds[i] && midpoint < bounds[i + 1])
                    return true;
            }
            return false;
        }

        public float SampleSlope(float scalar)
        {
            if (!HasValidProfile)
                return 0f;

            scalar = Mathf.Clamp(scalar, profileScalars[0], profileScalars[ProfilePointCount - 1]);
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (scalar > profileScalars[i + 1])
                    continue;
                return (profileHeights[i + 1] - profileHeights[i])
                    / Mathf.Max(0.001f, profileScalars[i + 1] - profileScalars[i]);
            }
            return 0f;
        }

        public Vector3 WorldSurfacePoint(float scalar)
        {
            return transform.TransformPoint(new Vector3(scalar, SampleHeight(scalar), 0f));
        }

        public void SetPresentationVisible(bool visible)
        {
            if (topRenderer != null)
                topRenderer.enabled = visible;
            if (cliffRenderer != null)
                cliffRenderer.enabled = visible;
            foreach (Phase01FadingSupport support in GetComponentsInChildren<Phase01FadingSupport>(true))
                support.SetPresentationVisible(visible);
            Phase01Branch4ControlledCollapse collapse = GetComponent<Phase01Branch4ControlledCollapse>();
            collapse?.SetPresentationVisible(visible);
        }

        public void SetCollisionEnabled(bool enabled)
        {
            if (topCollider != null) topCollider.enabled = enabled;
            if (cliffCollider != null) cliffCollider.enabled = enabled;
        }

        public void BuildMeshesForScene()
        {
            // In play mode the mesh is normally loading from its serialized asset. Rebuild from the authored
            // profile only as a fallback, so a missing or stale mesh reference can never leave a branch
            // rendering and colliding as an empty void.
            if (Application.isPlaying && TopMesh != null && CliffMesh != null)
                return;

            if (!HasValidProfile || topFilter == null || cliffFilter == null)
                return;

            MaximumSegmentAngle = 0f;
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                float rise = profileHeights[i + 1] - profileHeights[i];
                float run = profileScalars[i + 1] - profileScalars[i];
                MaximumSegmentAngle = Mathf.Max(MaximumSegmentAngle, Mathf.Abs(Mathf.Atan2(rise, run) * Mathf.Rad2Deg));
            }

            Mesh topMesh = BuildTopMesh();
            Mesh cliffMesh = BuildCliffMesh();
            // Counted so "this branch's geometry came from the scene, not from the runtime fallback" is
            // something a test can actually assert. A correctly authored scene never rebuilds anything.
            if (Application.isPlaying)
                RuntimeMeshCreationCount += 2;
#if UNITY_EDITOR
            // Persisting the generated mesh is an edit-time step only: AssetDatabase is not usable from
            // play mode, so the runtime fallback builds and assigns the mesh directly.
            if (!Application.isPlaying)
            {
                topMesh = PersistEditorMesh(topMesh, $"Branch{branchIndex + 1}_Top.asset");
                cliffMesh = PersistEditorMesh(cliffMesh, $"Branch{branchIndex + 1}_Cliff.asset");
            }
#endif
            ReplaceMesh(topFilter, topMesh);
            ReplaceMesh(cliffFilter, cliffMesh);
            if (topCollider != null)
                topCollider.sharedMesh = topMesh;
            if (cliffCollider != null)
                cliffCollider.sharedMesh = cliffMesh;

            ApplyRenderer(topRenderer, new Color(topRed, topGreen, topBlue, 1f));
            ApplyRenderer(cliffRenderer, new Color(cliffRed, cliffGreen, cliffBlue, 1f));
        }

        private Mesh BuildTopMesh()
        {
            Vector3[] vertices = new Vector3[ProfilePointCount * 2];
            Vector2[] uvs = new Vector2[ProfilePointCount * 2];
            List<int> triangles = new List<int>((ProfilePointCount - 1) * 6);
            for (int i = 0; i < ProfilePointCount; i++)
            {
                vertices[i * 2] = new Vector3(profileScalars[i], profileHeights[i], -halfWidths[i]);
                vertices[i * 2 + 1] = new Vector3(profileScalars[i], profileHeights[i], halfWidths[i]);
                // A walkable top is the lit end of the surface ramp, so it reads brighter than its own walls.
                uvs[i * 2] = new Vector2(0f, 1f);
                uvs[i * 2 + 1] = new Vector2(1f, 1f);
            }

            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (IsGapAfter(i))
                    continue;
                int vertex = i * 2;
                triangles.Add(vertex);
                triangles.Add(vertex + 3);
                triangles.Add(vertex + 2);
                triangles.Add(vertex);
                triangles.Add(vertex + 1);
                triangles.Add(vertex + 3);
            }

            Mesh mesh = new Mesh
            {
                name = $"{GeneratedPrefix} Branch {branchIndex + 1} Top"
            };
            BakeFaceShading(vertices, uvs, triangles);
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Mesh BuildCliffMesh()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            for (int i = 0; i < ProfilePointCount - 1; i++)
            {
                if (IsGapAfter(i))
                {
                    if (i == 0 || !IsGapAfter(i - 1))
                        AddEndCap(vertices, uvs, triangles, i, true);
                    if (i == ProfilePointCount - 2 || !IsGapAfter(i + 1))
                        AddEndCap(vertices, uvs, triangles, i + 1, false);
                    continue;
                }
                AddSideQuad(vertices, uvs, triangles, i, -1f, false);
                AddSideQuad(vertices, uvs, triangles, i, 1f, true);
                AddBottomQuad(vertices, uvs, triangles, i);
            }

            AddEndCap(vertices, uvs, triangles, 0, true);
            int last = ProfilePointCount - 1;
            AddEndCap(vertices, uvs, triangles, last, false);

            Mesh mesh = new Mesh
            {
                name = $"{GeneratedPrefix} Branch {branchIndex + 1} Cliff"
            };
            BakeFaceShading(vertices, uvs, triangles, true);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Underside height at a profile point, rough rather than the authored constant.</summary>
        private float RoughBottomHeight(int index)
        {
            // Deliberately unroughened. The jag was invisible at gameplay framing (the bases sit below the
            // frame) while its collider gave a falling player something to stand on inside a gap.
            return cliffBottomHeights[index];
        }

        /// <summary>Flared and slightly irregular base width, so the buttress is not a straight skirt.</summary>
        private float RoughBottomHalfWidth(int index, float halfWidth)
        {
            return halfWidth * CliffBaseFlare;
        }

        private void AddSideQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int i, float side, bool reverse)
        {
            AddQuad(
                vertices,
                uvs,
                triangles,
                new Vector3(profileScalars[i], profileHeights[i], side * halfWidths[i]),
                new Vector3(profileScalars[i + 1], profileHeights[i + 1], side * halfWidths[i + 1]),
                new Vector3(cliffBottomScalars[i + 1], RoughBottomHeight(i + 1), side * RoughBottomHalfWidth(i + 1, halfWidths[i + 1])),
                new Vector3(cliffBottomScalars[i], RoughBottomHeight(i), side * RoughBottomHalfWidth(i, halfWidths[i])),
                reverse);
        }

        private void AddBottomQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int i)
        {
            AddQuad(
                vertices,
                uvs,
                triangles,
                new Vector3(cliffBottomScalars[i], RoughBottomHeight(i), -RoughBottomHalfWidth(i, halfWidths[i])),
                new Vector3(cliffBottomScalars[i + 1], RoughBottomHeight(i + 1), -RoughBottomHalfWidth(i + 1, halfWidths[i + 1])),
                new Vector3(cliffBottomScalars[i + 1], RoughBottomHeight(i + 1), RoughBottomHalfWidth(i + 1, halfWidths[i + 1])),
                new Vector3(cliffBottomScalars[i], RoughBottomHeight(i), RoughBottomHalfWidth(i, halfWidths[i])),
                true);
        }

        private void AddEndCap(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int index, bool reverse)
        {
            AddQuad(
                vertices,
                uvs,
                triangles,
                new Vector3(profileScalars[index], profileHeights[index], -halfWidths[index]),
                new Vector3(profileScalars[index], profileHeights[index], halfWidths[index]),
                new Vector3(cliffBottomScalars[index], RoughBottomHeight(index), RoughBottomHalfWidth(index, halfWidths[index])),
                new Vector3(cliffBottomScalars[index], RoughBottomHeight(index), -RoughBottomHalfWidth(index, halfWidths[index])),
                reverse);
        }

        /// <summary>
        /// Bakes the fixed sun direction into each face's u coordinate. The surface texture reads u as
        /// "how much light this face receives", so walls turned away from the sun come out darker than the
        /// ones turned towards it. Quads share no vertices here, which makes the shading flat per face and
        /// free of the smearing a shared-vertex bake would produce.
        /// </summary>
        private static void BakeFaceShading(IList<Vector3> vertices, IList<Vector2> uvs, IList<int> triangles)
        {
            BakeFaceShading(vertices, uvs, triangles, false);
        }

        /// <summary>
        /// As above, with an optional per-face tonal step. A cliff wall is a single quad, so without this it
        /// renders as one flat panel at gameplay framing; a deterministic few-percent step per face makes the
        /// same geometry read as stacked, carved stone for no extra geometry and no extra draw calls.
        /// </summary>
        private static void BakeFaceShading(
            IList<Vector3> vertices,
            IList<Vector2> uvs,
            IList<int> triangles,
            bool varyPerFace)
        {
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];
                Vector3 normal = Vector3.Cross(vertices[i1] - vertices[i0], vertices[i2] - vertices[i0]);
                if (normal.sqrMagnitude < 1e-8f)
                    continue;

                float lambert = Mathf.Clamp01(Vector3.Dot(normal.normalized, ShadingLightDirection));
                float shade = Mathf.Lerp(ShadedFaceBrightness, 1f, lambert);
                if (varyPerFace)
                {
                    int face = t / 3;
                    float step = Mathf.Sin(face * 12.9898f) * Mathf.Sin(face * 78.233f);
                    shade = Mathf.Clamp(shade * (1f + 0.11f * step), 0.22f, 1.08f);
                }
                Vector2 uv = uvs[i0];
                uv.x = shade;
                uvs[i0] = uv;
                uv = uvs[i1];
                uv.x = shade;
                uvs[i1] = uv;
                uv = uvs[i2];
                uv.x = shade;
                uvs[i2] = uv;
            }
        }

        private static void AddQuad(
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            bool reverse)
        {
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            // a and b are always the upper edge of the quad, c and d the lower one, so the surface ramp
            // darkens every wall towards its base and end caps fade with it.
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 0f));
            if (!reverse)
            {
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
        }

        private void ReplaceMesh(MeshFilter filter, Mesh replacement)
        {
            Mesh previous = filter.sharedMesh;
            filter.sharedMesh = replacement;
            if (previous == null || previous == replacement || !previous.name.StartsWith(GeneratedPrefix))
                return;
#if UNITY_EDITOR
            if (!UnityEditor.AssetDatabase.Contains(previous))
                UnityEngine.Object.DestroyImmediate(previous);
#endif
        }

#if UNITY_EDITOR
        private static Mesh PersistEditorMesh(Mesh source, string fileName)
        {
            const string folder = "Assets/Phase01_Demo0/GeneratedMeshes";
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                UnityEditor.AssetDatabase.CreateFolder("Assets/Phase01_Demo0", "GeneratedMeshes");
            string path = $"{folder}/{fileName}";
            Mesh stored = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(source, path);
                return source;
            }
            UnityEditor.EditorUtility.CopySerialized(source, stored);
            UnityEngine.Object.DestroyImmediate(source);
            return stored;
        }
#endif

        private void ApplyRenderer(MeshRenderer renderer, Color color)
        {
            if (renderer == null)
                return;
            // Never blank a renderer that already has a material. The scene authors one tint material per
            // branch, and the runtime fallback has no placeholder to fall back to in a player build, so
            // assigning unconditionally here is what turned a rebuilt branch into an invisible one.
            if (renderer.sharedMaterial == null && placeholderMaterial != null)
                renderer.sharedMaterial = placeholderMaterial;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

    }
}
