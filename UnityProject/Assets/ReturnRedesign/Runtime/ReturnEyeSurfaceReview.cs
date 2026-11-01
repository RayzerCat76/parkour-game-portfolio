using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Return.Redesign
{
    // Explicit native self-test mode. No changes occur during ordinary play.
    public sealed class ReturnEyeSurfaceReview
    {
        public bool Active {get;private set;}
        readonly float poseTime;
        readonly string previewRoot;
        readonly List<SkinnedMeshRenderer> cards=new List<SkinnedMeshRenderer>();
        readonly List<SkinnedMeshRenderer> eyes=new List<SkinnedMeshRenderer>();
        readonly List<SkinnedMeshRenderer> teeth=new List<SkinnedMeshRenderer>();
        readonly List<SkinnedMeshRenderer> skin=new List<SkinnedMeshRenderer>();
        readonly Dictionary<SkinnedMeshRenderer,bool> initial=new Dictionary<SkinnedMeshRenderer,bool>();
        readonly Dictionary<SkinnedMeshRenderer,Material[]> originalMaterials=new Dictionary<SkinnedMeshRenderer,Material[]>();
        readonly Dictionary<SkinnedMeshRenderer,Material[]> appliedMaterials=new Dictionary<SkinnedMeshRenderer,Material[]>();
        readonly List<Material> clones=new List<Material>();
        bool windowShadowsRecorded,windowShadowsRestored;
        bool faceBoundariesRecorded,faceBoundariesRestored;
        bool opaqueFacesRecorded,opaqueFacesRestored;
        bool skinCompositeRecorded,skinCompositeRestored,compositeDrawScheduled,compositeBypassScheduled;
        bool reflectionCorrectionRecorded,reflectionCorrectionRestored;
        bool jointCorrectionRecorded,jointCorrectionRestored;
        bool referenceSkinRecorded,referenceSkinRestored,referenceSkinResourcesReleased;
        bool litAlbedoRecorded,litAlbedoRestored;
        bool litSpecularRecorded,litSpecularRestored;
        public static ReturnEyeSurfaceReview SkinCompositeRequest {get;private set;}
        public Camera CompositeCamera {get;private set;}
        public bool CompositeBypassRequested {get;private set;}
        public void MarkSkinCompositeDraw(bool bypass){compositeDrawScheduled=true;compositeBypassScheduled=bypass;}
        Texture2D expectedLookup;
        public static ReturnEyeSurfaceReview VisibilityRequest {get;private set;}
        public Camera VisibilityCamera {get;private set;}
        public RTHandle VisibilityTarget {get;private set;}
        public RTHandle NormalStoredTarget {get;private set;}
        public RTHandle NormalProducerTarget {get;private set;}
        public RTHandle ReflectionBeforeTarget {get;private set;}
        public RTHandle ReflectionAfterTarget {get;private set;}
        public RTHandle ReflectionPayloadTarget {get;private set;}
        RenderTexture reflectionBeforeCapture,reflectionAfterCapture,reflectionPayloadCapture;
        ReflectionEnergyReport reflectionEnergyReport;
        RenderTexture normalStoredCapture,normalProducerCapture;bool normalTransportCaptured;
        string normalMrtFormat,normalProducerFormat,normalMrtMsaa;int normalMrtWidth,normalMrtHeight;
        public void RecordNormalFormats(string mrt,string producer,string msaa,int width,int height){normalMrtFormat=mrt;normalProducerFormat=producer;normalMrtMsaa=msaa;normalMrtWidth=width;normalMrtHeight=height;}
        public void MarkNormalTransportCaptured(){normalTransportCaptured=true;}
        RenderTexture visibilityCapture;bool visibilityCaptured;VisibilityGpuReport visibilityReport;
        public void MarkVisibilityCaptured(){visibilityCaptured=true;if(VisibilityRequest==this)VisibilityRequest=null;}
        [Serializable] sealed class VisibilityRegion {
            public string renderer;public float minX,minY,maxX,maxY;
            public int coveredPixels,blockedPixels,missingMrtPixels,missingPositionPixels,missingSurfacePointPixels,invalidNormalPixels,negativeRaySamples;
            public float minimumRawOcclusion,maximumRawOcclusion,meanOcclusion,maximumOcclusion,meanEnvironment,maximumEnvironment,meanPositionErrorMM,maximumPositionErrorMM;
            double obstructionSum,environmentSum,positionSum;int positionSamples;
            public bool Contains(float x,float y)=>x>=minX&&x<=maxX&&y>=minY&&y<=maxY;
            public void Add(Color value){
                if(value.r<.5f){if(value.a<-1.5f)missingMrtPixels++;return;}
                coveredPixels++;if(coveredPixels==1){minimumRawOcclusion=value.g;maximumRawOcclusion=value.g;}else{minimumRawOcclusion=Mathf.Min(minimumRawOcclusion,value.g);maximumRawOcclusion=Mathf.Max(maximumRawOcclusion,value.g);}if(value.g<0)negativeRaySamples++;float obstruction=Mathf.Clamp01(value.g);obstructionSum+=obstruction;maximumOcclusion=Mathf.Max(maximumOcclusion,obstruction);if(obstruction>.001f)blockedPixels++;
                environmentSum+=value.b;maximumEnvironment=Mathf.Max(maximumEnvironment,value.b);
                if(value.a<-2.5f)missingSurfacePointPixels++;if(value.a<0)missingPositionPixels++;else{positionSamples++;positionSum+=value.a;maximumPositionErrorMM=Mathf.Max(maximumPositionErrorMM,value.a);}
            }
            public void Finish(){if(coveredPixels>0){meanOcclusion=(float)(obstructionSum/coveredPixels);meanEnvironment=(float)(environmentSum/coveredPixels);}if(positionSamples>0)meanPositionErrorMM=(float)(positionSum/positionSamples);}
        }
        [Serializable] sealed class VisibilityGpuReport {
            public bool captured,finite,originalMrtCoveragePresent,eachFaceCoveragePresent,normalsValid,normalTransportMatchesProducer,projectionWithinTolerance,faceRayHitsDetected,resourcesReleased;
            public int width=256,height=144;public float frozenHospitalTime;
            public string channels="R actual production MRT mask; G blocked fraction independent of normal flags; B environment mean RGB; A used skin-origin error mm (-1 missing producer position,-2 expected surface absent from MRT,-3 missing production origin)";
            public string normalGateMeaning="normalsValid tests resolved decoded unit length only; transport comparison and negative ray samples are separate. MSAA resolved normals are renormalized by production tracing.";
            public string imageLegend="Green captured/unblocked skin; yellow-red blocked reflection; blue position error scaled to10mm; magenta missing input; dark background. Normal flags recorded separately. Diagnostic only.";
            public VisibilityRegion whole;public VisibilityRegion[] faces;
        }
        [Serializable] sealed class NormalSample {
            public string renderer;public float x,y;public Color encoded;public Vector3 decoded,producer;
            public float storedLength,producerLength,angleDegrees,directError,srgbDecodeError,srgbEncodeError,roughnessError;
        }
        static float ToLinear(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
        static float ToGamma(float c)=>c<=.0031308f?c*12.9232102f:1.055f*Mathf.Pow(c,1/2.4f)-.055f;
        static NormalSample CompareNormal(Color encoded,Color raw){
            Vector3 stored=new Vector3(encoded.r,encoded.g,encoded.b)*2-Vector3.one,producer=new Vector3(raw.r,raw.g,raw.b);
            Vector3 linear=new Vector3(ToLinear(encoded.r),ToLinear(encoded.g),ToLinear(encoded.b))*2-Vector3.one,gamma=new Vector3(ToGamma(encoded.r),ToGamma(encoded.g),ToGamma(encoded.b))*2-Vector3.one;
            return new NormalSample{encoded=encoded,decoded=stored,producer=producer,storedLength=stored.magnitude,producerLength=producer.magnitude,angleDegrees=Vector3.Angle(stored,producer),directError=Vector3.Distance(stored,producer),srgbDecodeError=Vector3.Distance(linear,producer),srgbEncodeError=Vector3.Distance(gamma,producer),roughnessError=Mathf.Abs(encoded.a-raw.a)};
        }
        [Serializable] sealed class NormalRegion {
            public string renderer;public int pixels,invalidStoredLengths,invalidProducerLengths,emptyProducerPixels;
            public float meanStoredLength,meanProducerLength,maximumStoredUnitError,maximumProducerUnitError,meanAngleDegrees,maximumAngleDegrees,meanDirectError,maximumDirectError,meanSrgbDecodeError,meanSrgbEncodeError,meanRoughnessError,maximumRoughnessError;
            double stored,producer,angles,direct,decode,encode,roughness;
            public void Add(NormalSample s){pixels++;stored+=s.storedLength;producer+=s.producerLength;angles+=s.angleDegrees;direct+=s.directError;decode+=s.srgbDecodeError;encode+=s.srgbEncodeError;roughness+=s.roughnessError;float e=Mathf.Abs(s.storedLength-1),p=Mathf.Abs(s.producerLength-1);if(e>.02f)invalidStoredLengths++;if(p>.02f)invalidProducerLengths++;if(s.producerLength<.1f)emptyProducerPixels++;maximumStoredUnitError=Mathf.Max(maximumStoredUnitError,e);maximumProducerUnitError=Mathf.Max(maximumProducerUnitError,p);maximumAngleDegrees=Mathf.Max(maximumAngleDegrees,s.angleDegrees);maximumDirectError=Mathf.Max(maximumDirectError,s.directError);maximumRoughnessError=Mathf.Max(maximumRoughnessError,s.roughnessError);}
            public void Finish(){if(pixels==0)return;meanStoredLength=(float)(stored/pixels);meanProducerLength=(float)(producer/pixels);meanAngleDegrees=(float)(angles/pixels);meanDirectError=(float)(direct/pixels);meanSrgbDecodeError=(float)(decode/pixels);meanSrgbEncodeError=(float)(encode/pixels);meanRoughnessError=(float)(roughness/pixels);}
        }
        [Serializable] sealed class NormalTransportReport {
            public bool captured,finite,resourcesReleased,producerNormalsUnit,transportMatchesProducer;
            public string mrtFormat,producerFormat,mrtMsaa;public int mrtWidth,mrtHeight,readbackWidth=256,readbackHeight=144;
            public string comparison="Actual encoded production normal texture versus independent signed ReturnSurface normal/roughness at the same pixels. Standard installed Core Color.hlsl sRGB decode/encode hypotheses; no production colour conversion changed.";
            public NormalRegion whole;public NormalRegion[] faces;public NormalSample[] samples;
        }
        [Serializable] sealed class Surface {public string renderer;public string[] materials;public bool initiallyEnabled;}
        [Serializable] sealed class Binding {
            public string renderer,material,shader,lookup,lookupFormat,bumpTexture;
            public int materialInstance,resourceMaterialInstance,lookupInstance,width,height;
            public float skinFlag,dfgFlag,visibilityFlag,lookupA,lookupB,lookupSkinFactor,skinDetail,skinOiliness,smoothness,bumpScale;
            public bool controlsPresent,specularSetup,sameMaterialAsResource,sameLookupAsResource,lookupReadable;
        }
        [Serializable] sealed class SurfaceSetting {
            public string mode,renderer,material,bumpTexture;
            public float detail,oil,smoothness,bumpScale,dfg,visibility,environmentRemoval;
        }
        [Serializable] sealed class SurfaceGpuCase {
            public string mode,dataDirectory;
            public bool captured,finite,resourcesReleased,normalTransportMatchesProducer,materialSlotsRetained,propertyBlocksPresent;
        }
        [Serializable] sealed class Report {
            public bool mechanicalPassed,statesRestored,materialsRestored,skinReviewDefaultsZero,reflectionModesApplied,cardsStayedEnabled,expectedLookupValid,originalBindingsValid,gpuReadbackCaptured;
            public int temporarySkinMaterials;public float frozenHospitalTime;
            public Surface[] facialCards,eyeSurfaces,skinSurfaces;public Binding[] runtimeSkin;public SurfaceSetting[] surfaceSettings;public SurfaceGpuCase[] gpuCases;public string[] frames;public string interpretation;
        }
        [Serializable] sealed class WindowShadowState {
            public string frame,shadows;public bool materialSlotsRetained,localBias;
            public float depthBias,normalBias;
        }
        [Serializable] sealed class WindowShadowReport {
            public bool recorded,restored;public float frozenHospitalTime;public string light,originalShadows;
            public WindowShadowState[] states;
        }
        sealed class FaceAlbedoOriginal {
            public Material material;public Texture map;public Color colour;public float[] controls;
        }
        static readonly string[] BoundaryControls={"_SkinDetail","_SkinOiliness","_Smoothness","_SkinDFGEnabled","_SkinReflectionVisibility","_SkinReflectionReview","_SurfaceDetail","_AlbedoRelief"};
        [Serializable] sealed class FaceAlbedoBinding {public string material,baseMap;public Color baseColour;}
        [Serializable] sealed class FaceBoundaryState {
            public string frame;public bool materialSlotsRetained,skinControlsRetained,inputsMatched;
            public string[] cardStates;public FaceAlbedoBinding[] bindings;
        }
        [Serializable] sealed class FaceBoundaryReport {
            public bool recorded,restored;public float frozenHospitalTime;public FaceBoundaryState[] states;
        }
        [Serializable] sealed class OpaqueFaceState {public string frame;public int enabledEyes,enabledTeeth;public bool materialSlotsRetained,inputsMatched;}
        [Serializable] sealed class OpaqueFaceReport {public bool recorded,restored;public float frozenHospitalTime;public Surface[] eyes,teeth;public OpaqueFaceState[] states;}
        [Serializable] sealed class SkinCompositeState {public string frame;public bool requestedBypass,drawScheduled,scheduledBypass,materialSlotsRetained;}
        [Serializable] sealed class SkinCompositeReport {public bool recorded,restored;public float frozenHospitalTime;public string meaning="Final composite shader review uniform only;forward skin inputs,reflection correction,MRT/blur/depth retained. Render callback records draw scheduling,not an independent shader-uniform readback. Actual final framebuffer response required.";public SkinCompositeState[] states;}
        [Serializable] sealed class ReflectionCorrectionState {public string frame;public bool enabled,materialSlotsRetained,otherControlsRetained;public float[] visibilityFlags;}
        [Serializable] sealed class ReflectionCorrectionReport {public bool recorded,restored;public float frozenHospitalTime;public ReflectionCorrectionState[] states;}
        [Serializable] sealed class JointCorrectionState {public string frame;public bool reflectionEnabled,requestedBypass,drawScheduled,scheduledBypass,materialSlotsRetained,otherControlsRetained;public float[] visibilityFlags;}
        [Serializable] sealed class JointCorrectionReport {public bool recorded,restored;public float frozenHospitalTime;public JointCorrectionState[] states;}
        [Serializable] sealed class ReferenceSkinBinding {public string renderer,sourceMaterial,referenceMaterial,sourceShader,referenceShader,baseMap,normalMap;public Color baseColour,specularF0;public Vector2 uvScale,uvOffset;public float smoothness,bumpScale,cull,sourceDetail,sourceOil;public bool commonInputsMatched;}
        [Serializable] sealed class ReferenceSkinState {public string frame;public bool standardLit,requestedBypass,drawScheduled,scheduledBypass,materialSlotsMatched,geometryRetained,sourceControlsRetained,commonInputsMatched;public string[] skinShaders;}
        [Serializable] sealed class ReferenceSkinReport {public bool recorded,restored,resourcesReleased,resourceSupported;public int referenceMaterials;public float frozenHospitalTime;public string resourceShader,meaning="Overall material comparison:base textures/colour/UVs,smoothness,normal map/scale,culling/queue,occlusion/emission and skin F0=.028 matched. Stock Lit does not reproduce custom detail/oil,geometric filtering or custom DFG/BRDF;pose,meshes,other slots,scene lighting and remaining postprocessing retained. Both corrective stages bypassed throughout. Final framebuffer effect required.";public ReferenceSkinBinding[] bindings;public ReferenceSkinState[] states;}
        [Serializable] sealed class LitAlbedoBinding {public string material,shader,baseMap;public Color baseColour;}
        [Serializable] sealed class LitAlbedoState {public string frame;public bool flat,albedoMatched,otherInputsRetained,materialSlotsMatched,geometryRetained,sourceControlsRetained,facialSurfacesRetained,requestedBypass,drawScheduled,scheduledBypass;public LitAlbedoBinding[] bindings;}
        [Serializable] sealed class LitAlbedoReport {public bool recorded,restored;public float frozenHospitalTime;public Color flatColour;public LitAlbedoState[] states;}
        [Serializable] sealed class LitSpecularBinding {public string material;public float highlights,reflections;public bool highlightsOff,reflectionsOff;}
        [Serializable] sealed class LitSpecularState {public string frame;public bool enabled,controlsMatched,otherKeywordsRetained,flatAlbedoRetained,otherInputsRetained,materialSlotsMatched,geometryRetained,sourceControlsRetained,facialSurfacesRetained,requestedBypass,drawScheduled,scheduledBypass;public LitSpecularBinding[] bindings;}
        [Serializable] sealed class LitSpecularReport {public bool recorded,restored,variantDeclared,resourceSupported;public float frozenHospitalTime;public string meaning="Stock direct highlights OFF and probe-reflection OFF keywords together. Reflection OFF retains _GlossyEnvironmentColor ambient fallback;this is not zero indirect specular. Flat albedo and both correction bypasses retained. Actual final pixels required.";public LitSpecularState[] states;}
        // Return/Physical declares Cull Off;it has no _Cull material property.
        const float PhysicalSkinCullOff=0;
        static readonly string[] ReferenceTextures={"_BaseMap","_BumpMap","_OcclusionMap"};
        static bool ReferenceInputsMatch(Material source,Material reference,bool skipAlbedo=false) {
            bool valid=reference.shader.name=="Universal Render Pipeline/Lit"&&reference.IsKeywordEnabled("_SPECULAR_SETUP")&&reference.GetFloat("_WorkflowMode")==0&&reference.GetColor("_SpecColor")==new Color(.028f,.028f,.028f,1)&&reference.renderQueue==source.renderQueue&&reference.GetFloat("_Cull")==PhysicalSkinCullOff;
            foreach(string name in ReferenceTextures)valid&=(skipAlbedo&&name=="_BaseMap"||reference.GetTexture(name)==source.GetTexture(name))&&reference.GetTextureScale(name)==source.GetTextureScale(name)&&reference.GetTextureOffset(name)==source.GetTextureOffset(name);
            foreach(string name in new[]{"_BaseColor","_EmissionColor"})if(!skipAlbedo||name!="_BaseColor")valid&=reference.GetColor(name)==source.GetColor(name);
            foreach(string name in new[]{"_Smoothness","_BumpScale"})valid&=reference.GetFloat(name)==source.GetFloat(name);
            return valid;
        }
        [Serializable] sealed class ReflectionEnergyRegion {
            public string renderer;public float minX,minY,maxX,maxY;
            public int pixels,invalidSubtractionPixels,negativeBeforeChannels,negativeAfterChannels;
            public float meanActualRemovedRgb,meanExpectedRemovedRgb,maximumRelationError,minimumBeforeRgb,minimumAfterRgb,maximumActualRemovedRgb;
            double actualSum,expectedSum;
            public bool Contains(float x,float y)=>x>=minX&&x<=maxX&&y>=minY&&y<=maxY;
            public void Add(Color before,Color after,Color payload){
                bool invalid=false;float loBefore=Mathf.Min(before.r,Mathf.Min(before.g,before.b)),loAfter=Mathf.Min(after.r,Mathf.Min(after.g,after.b));if(pixels==0){minimumBeforeRgb=loBefore;minimumAfterRgb=loAfter;}else{minimumBeforeRgb=Mathf.Min(minimumBeforeRgb,loBefore);minimumAfterRgb=Mathf.Min(minimumAfterRgb,loAfter);}
                for(int c=0;c<3;c++){float actual=before[c]-after[c],expected=payload[c]*(1-payload.a),error=Mathf.Abs(actual-expected),magnitude=Mathf.Max(Mathf.Abs(before[c]),Mathf.Max(Mathf.Abs(after[c]),Mathf.Abs(expected)));actualSum+=actual;expectedSum+=expected;maximumRelationError=Mathf.Max(maximumRelationError,error);maximumActualRemovedRgb=Mathf.Max(maximumActualRemovedRgb,actual);invalid|=error>Mathf.Max(.002f,.0015f*magnitude);if(before[c]<-.002f)negativeBeforeChannels++;if(after[c]<-.002f)negativeAfterChannels++;}
                pixels++;if(invalid)invalidSubtractionPixels++;
            }
            public void Finish(){if(pixels>0){meanActualRemovedRgb=(float)(actualSum/(pixels*3));meanExpectedRemovedRgb=(float)(expectedSum/(pixels*3));}}
        }
        [Serializable] sealed class ReflectionEnergyReport {
            public bool captured,finite=true,resourcesReleased,relationWithinTolerance;public int width=256,height=144;public float frozenHospitalTime;
            public string method="Independent float MRT samples of actual pre-correction scene,actual post-correction scene,and environmentRGB/visibility. Compare before-after with environment*(1-visibility) at covered samples;HDR-half tolerance max(.002,.0015*max absolute magnitude). Pre/post negative RGB counts use threshold-.002. Not post/SSS final colour or a realism metric.";
            public ReflectionEnergyRegion whole;public ReflectionEnergyRegion[] faces;
        }
        sealed class BakedFaceGeometry {public string person,role,renderer;public SkinnedMeshRenderer source;public Vector3[] positions,normals;public int[] triangles;public float transformParity;}
        [Serializable] sealed class PosedGeometrySurface {public string person,role,renderer;public int vertices,triangles;public Vector3 minimum,maximum;}
        [Serializable] sealed class PosedGeometryHit {public string role,renderer;public bool hit;public float distanceMM=-1;public Vector3 world;public PosedNormalSample normalSample;}
        [Serializable] sealed class PosedNormalSample {public bool captured,finite;public int triangle;public int[] vertices;public Vector3 barycentric,rawGeometricNormal,surfaceNormal,shadingNormal;public float transformParity,shadingSurfaceDot,shadingSurfaceAngleDegrees,rawWindingViewDot,surfaceViewDot,shadingViewDot;public float[] vertexSurfaceAngles;}
        [Serializable] sealed class PosedGeometryProbe {public string person,label,nearestRole;public Vector2 viewport;public PosedGeometryHit[] hits;}
        [Serializable] sealed class PosedGeometryReport {
            public bool finite=true,resourcesReleased,normalsFinite=true;public int snapshots,skinNormalSamples;public float frozenHospitalTime;
            public string method="BakeMesh(false),renderer-local vertices transformed once to world;double-sided nearest geometric ray intersections from owned camera at approximate eye-bone-based landmarks. CPU posed geometry only;not GPU depth/coverage ownership,lighting,exact feature segmentation or a proven overlap defect.";
            public PosedGeometrySurface[] surfaces;public PosedGeometryProbe[] probes;
        }
        [Serializable] sealed class NormalOriginInfluence {public int bone;public string name;public float weight,bindParity,poseParity;}
        [Serializable] sealed class NormalOriginBlend {public string name;public float weight;}
        [Serializable] sealed class NormalOriginAdjacent {public int triangle;public bool restDegenerate,posedDegenerate;public float restAngle=-1,posedAngle=-1;public Vector3 restSurface,posedSurface;}
        [Serializable] sealed class NormalExpressionDelta {public string name;public float weight,frameWeight,factor;public Vector3 normalDelta,positionDelta,weightedNormalDelta;}
        [Serializable] sealed class NormalExpressionPrediction {public bool finite;public Vector3 rigidNormal,expressionNormal,expressionPosition,expressionLocalInput;public float rigidSurfaceAngle,expressionSurfaceAngle,rigidActualAngle,expressionActualAngle,maximumNormalComponentError,positionErrorMM;public NormalExpressionDelta[] deltas;}
        [Serializable] sealed class NormalOriginVertex {public int vertex;public Vector3 restPosition,posedPosition,restNormal,restWorldNormal,posedNormal;public float restSurfaceAngle,posedSurfaceAngle,restPoseAngle,weightSum;public NormalOriginInfluence[] influences;public NormalOriginAdjacent[] adjacent;public NormalExpressionPrediction expression;}
        [Serializable] sealed class NormalOriginTriangle {public string person,renderer;public int triangle;public Vector3 restSurface,posedSurface;public NormalOriginVertex[] vertices;public NormalOriginBlend[] blends;}
        [Serializable] sealed class NormalOriginReport {public bool captured,finite=true,resourcesReleased=true,sourceCaptured,sourceBound;public int triangles,vertices,weightStreams,ownedNativeAllocations,borrowedViewsCleared,sourceMeshBindings,runtimeSourceArrayReads,expressionFrames,expressionDeltaSamples,expressionPredictions;public float frozenHospitalTime;public string weightOwnership="Rest/influence/expression snapshot captured in editor;four borrowed editor views cleared;no owned native allocations or runtime source mesh-array reads. Exact serialized Mesh references bind the snapshot.";public NormalOriginTriangle[] samples;}

        public ReturnEyeSurfaceReview(float hospitalTime,string root) {poseTime=hospitalTime;previewRoot=root;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        VisibilityRegion FaceRegion(SkinnedMeshRenderer renderer){
            Transform left=null,right=null;
            foreach(var bone in renderer.transform.parent.GetComponentsInChildren<Transform>(true)){if(bone.name.EndsWith("eye_l"))left=bone;if(bone.name.EndsWith("eye_r"))right=bone;}
            if(left==null||right==null)throw new InvalidOperationException("Visibility diagnosis needs the two existing eye bones: "+RendererPath(renderer.transform));
            Vector3 centre=(left.position+right.position)*.5f;float separation=Vector3.Distance(left.position,right.position);
            Vector3 minimum=VisibilityCamera.WorldToViewportPoint(centre-VisibilityCamera.transform.right*separation*1.8f-VisibilityCamera.transform.up*separation*2.2f);
            Vector3 maximum=VisibilityCamera.WorldToViewportPoint(centre+VisibilityCamera.transform.right*separation*1.8f+VisibilityCamera.transform.up*separation*.85f);
            return new VisibilityRegion{renderer=RendererPath(renderer.transform),minX=Mathf.Clamp01(minimum.x),minY=Mathf.Clamp01(minimum.y),maxX=Mathf.Clamp01(maximum.x),maxY=Mathf.Clamp01(maximum.y)};
        }
        IEnumerator CheckVisibility(string mode){
            string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))throw new InvalidOperationException("Visibility readback needs RETURN_EVIDENCE_DIR");
            output=Path.Combine(output,"skin-surface-gpu",mode);Directory.CreateDirectory(output);
            visibilityCaptured=false;normalTransportCaptured=false;normalMrtFormat=null;normalProducerFormat=null;normalMrtMsaa=null;normalMrtWidth=0;normalMrtHeight=0;
            foreach(var focus in UnityEngine.Object.FindObjectsByType<ReturnFocus>(FindObjectsSortMode.None))if(focus.DepthCamera!=null&&focus.DepthCamera.enabled){if(VisibilityCamera!=null)throw new InvalidOperationException("Multiple owned visibility cameras");VisibilityCamera=focus.DepthCamera;}
            if(VisibilityCamera==null)throw new InvalidOperationException("No owned camera for visibility readback");
            visibilityReport=new VisibilityGpuReport{finite=true,frozenHospitalTime=poseTime,whole=new VisibilityRegion{renderer="Actual production skin MRT",minX=0,minY=0,maxX=1,maxY=1},faces=new[]{FaceRegion(skin[0]),FaceRegion(skin[1])}};
            visibilityCapture=new RenderTexture(256,144,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name="RETURN local visibility GPU evidence",filterMode=FilterMode.Point};visibilityCapture.Create();VisibilityTarget=RTHandles.Alloc(visibilityCapture);
            normalStoredCapture=new RenderTexture(256,144,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name="RETURN encoded normal readback",filterMode=FilterMode.Point};normalStoredCapture.Create();NormalStoredTarget=RTHandles.Alloc(normalStoredCapture);
            normalProducerCapture=new RenderTexture(256,144,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name="RETURN producer normal readback",filterMode=FilterMode.Point};normalProducerCapture.Create();NormalProducerTarget=RTHandles.Alloc(normalProducerCapture);VisibilityRequest=this;
            reflectionEnergyReport=null;reflectionBeforeCapture=EnergyTexture("RETURN reflection source readback");ReflectionBeforeTarget=RTHandles.Alloc(reflectionBeforeCapture);reflectionAfterCapture=EnergyTexture("RETURN corrected reflection readback");ReflectionAfterTarget=RTHandles.Alloc(reflectionAfterCapture);reflectionPayloadCapture=EnergyTexture("RETURN reflection energy payload");ReflectionPayloadTarget=RTHandles.Alloc(reflectionPayloadCapture);
            RenderTexture previous=RenderTexture.active;Texture2D pixels=null,map=null,storedPixels=null,producerPixels=null;NormalTransportReport transport=null;
            try {
                yield return new WaitForEndOfFrame();visibilityReport.captured=visibilityCaptured&&normalTransportCaptured;if(!visibilityReport.captured)throw new InvalidOperationException("Visibility/normal transport GPU evidence pass did not execute");
                pixels=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=visibilityCapture;pixels.ReadPixels(new Rect(0,0,256,144),0,0);pixels.Apply();Color[] values=pixels.GetPixels(),colours=new Color[values.Length];
                storedPixels=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=normalStoredCapture;storedPixels.ReadPixels(new Rect(0,0,256,144),0,0);storedPixels.Apply();Color[] storedValues=storedPixels.GetPixels();
                producerPixels=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=normalProducerCapture;producerPixels.ReadPixels(new Rect(0,0,256,144),0,0);producerPixels.Apply();Color[] producerValues=producerPixels.GetPixels();
                CaptureReflectionEnergy(output,values);
                using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"surface-inputs.bytes")))) {
                    writer.Write(new byte[]{82,83,85,82,70,56,48,0});writer.Write(256);writer.Write(144);
                    for(int pixel=0;pixel<values.Length;pixel++)foreach(var value in new[]{values[pixel],storedValues[pixel],producerValues[pixel]}){writer.Write(value.r);writer.Write(value.g);writer.Write(value.b);writer.Write(value.a);}
                }
                transport=new NormalTransportReport{captured=normalTransportCaptured,finite=true,mrtFormat=normalMrtFormat,producerFormat=normalProducerFormat,mrtMsaa=normalMrtMsaa,mrtWidth=normalMrtWidth,mrtHeight=normalMrtHeight,whole=new NormalRegion{renderer="Production normal MRT"},faces=new[]{new NormalRegion{renderer=visibilityReport.faces[0].renderer},new NormalRegion{renderer=visibilityReport.faces[1].renderer}}};var samples=new List<NormalSample>();int[] sampleCounts=new int[2];
                for(int at=0;at<values.Length;at++){
                    Color value=values[at],encoded=storedValues[at],raw=producerValues[at];bool finite=Finite(value.r)&&Finite(value.g)&&Finite(value.b)&&Finite(value.a)&&Finite(encoded.r)&&Finite(encoded.g)&&Finite(encoded.b)&&Finite(encoded.a)&&Finite(raw.r)&&Finite(raw.g)&&Finite(raw.b)&&Finite(raw.a);visibilityReport.finite&=finite;transport.finite&=finite;if(!finite){colours[at]=Color.magenta;continue;}
                    float x=(at%256+.5f)/256,y=(at/256+.5f)/144;visibilityReport.whole.Add(value);foreach(var face in visibilityReport.faces)if(face.Contains(x,y))face.Add(value);
                    if(value.r>.5f){NormalSample comparison=CompareNormal(encoded,raw);transport.whole.Add(comparison);bool invalid=Mathf.Abs(comparison.storedLength-1)>.02f;if(invalid)visibilityReport.whole.invalidNormalPixels++;for(int f=0;f<2;f++)if(visibilityReport.faces[f].Contains(x,y)){transport.faces[f].Add(comparison);if(invalid)visibilityReport.faces[f].invalidNormalPixels++;if(sampleCounts[f]<4){comparison.renderer=visibilityReport.faces[f].renderer;comparison.x=x;comparison.y=y;samples.Add(comparison);sampleCounts[f]++;}}float blocked=Mathf.Clamp01(value.g);colours[at]=value.a<0?Color.magenta:new Color(.12f+.88f*blocked,.2f+.8f*(1-blocked),Mathf.Clamp01(value.a/10),1);}
                    else colours[at]=value.a<-1.5f?Color.magenta:new Color(.02f,.025f,.03f,1);
                }
                transport.whole.Finish();foreach(var face in transport.faces)face.Finish();transport.samples=samples.ToArray();transport.producerNormalsUnit=transport.whole.pixels>0&&transport.whole.invalidProducerLengths==0;transport.transportMatchesProducer=transport.whole.pixels>0&&transport.whole.meanDirectError<.005f&&transport.whole.maximumDirectError<.02f;visibilityReport.normalTransportMatchesProducer=transport.transportMatchesProducer;
                visibilityReport.whole.Finish();foreach(var face in visibilityReport.faces)face.Finish();
                visibilityReport.originalMrtCoveragePresent=visibilityReport.whole.coveredPixels>0;visibilityReport.eachFaceCoveragePresent=true;visibilityReport.projectionWithinTolerance=true;visibilityReport.normalsValid=visibilityReport.whole.invalidNormalPixels==0;
                foreach(var face in visibilityReport.faces){visibilityReport.eachFaceCoveragePresent&=face.coveredPixels>5;visibilityReport.projectionWithinTolerance&=face.coveredPixels>5&&face.missingPositionPixels==0&&face.meanPositionErrorMM<=2&&face.maximumPositionErrorMM<=10;visibilityReport.faceRayHitsDetected|=face.blockedPixels>0;}
                map=new Texture2D(256,144,TextureFormat.RGBA32,false,true);map.SetPixels(colours);map.Apply();Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"skin-visibility-gpu.png"),map.EncodeToPNG());
                if(!visibilityReport.finite||!transport.finite)throw new InvalidOperationException("Non-finite visibility/normal transport GPU data");
            } finally {
                RenderTexture.active=previous;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(map!=null)UnityEngine.Object.DestroyImmediate(map);if(storedPixels!=null)UnityEngine.Object.DestroyImmediate(storedPixels);if(producerPixels!=null)UnityEngine.Object.DestroyImmediate(producerPixels);ReleaseReflectionEnergy();ReleaseVisibility();visibilityReport.resourcesReleased=VisibilityRequest==null&&VisibilityTarget==null&&visibilityCapture==null&&NormalStoredTarget==null&&NormalProducerTarget==null&&normalStoredCapture==null&&normalProducerCapture==null;Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"skin-visibility-gpu.json"),JsonUtility.ToJson(visibilityReport,true));if(transport!=null){transport.resourcesReleased=visibilityReport.resourcesReleased;File.WriteAllText(Path.Combine(output,"skin-normal-transport.json"),JsonUtility.ToJson(transport,true));}if(reflectionEnergyReport!=null){reflectionEnergyReport.resourcesReleased=ReflectionBeforeTarget==null&&ReflectionAfterTarget==null&&ReflectionPayloadTarget==null&&reflectionBeforeCapture==null&&reflectionAfterCapture==null&&reflectionPayloadCapture==null;File.WriteAllText(Path.Combine(output,"reflection-energy-gpu.json"),JsonUtility.ToJson(reflectionEnergyReport,true));}
            }
            Debug.Log("[RETURN] Visibility GPU readback captured; actual MRT coverage "+visibilityReport.originalMrtCoveragePresent+", face hits "+visibilityReport.faceRayHitsDetected+", projection within tolerance "+visibilityReport.projectionWithinTolerance);
        }
        static RenderTexture EnergyTexture(string name){var texture=new RenderTexture(256,144,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name=name,filterMode=FilterMode.Point};texture.Create();return texture;}
        void ReleaseReflectionEnergy(){if(ReflectionBeforeTarget!=null){ReflectionBeforeTarget.Release();ReflectionBeforeTarget=null;}if(ReflectionAfterTarget!=null){ReflectionAfterTarget.Release();ReflectionAfterTarget=null;}if(ReflectionPayloadTarget!=null){ReflectionPayloadTarget.Release();ReflectionPayloadTarget=null;}foreach(var texture in new[]{reflectionBeforeCapture,reflectionAfterCapture,reflectionPayloadCapture})if(texture!=null){texture.Release();UnityEngine.Object.DestroyImmediate(texture);}reflectionBeforeCapture=null;reflectionAfterCapture=null;reflectionPayloadCapture=null;}
        void CaptureReflectionEnergy(string output,Color[] coverage) {
            Texture2D before=null,after=null,payload=null;reflectionEnergyReport=new ReflectionEnergyReport{captured=visibilityCaptured,frozenHospitalTime=poseTime,whole=new ReflectionEnergyRegion{renderer="Actual covered skin",minX=0,minY=0,maxX=1,maxY=1},faces=new ReflectionEnergyRegion[2]};
            for(int i=0;i<2;i++){var f=visibilityReport.faces[i];reflectionEnergyReport.faces[i]=new ReflectionEnergyRegion{renderer=f.renderer,minX=f.minX,minY=f.minY,maxX=f.maxX,maxY=f.maxY};}
            try {
                before=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=reflectionBeforeCapture;before.ReadPixels(new Rect(0,0,256,144),0,0);before.Apply();Color[] b=before.GetPixels();
                after=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=reflectionAfterCapture;after.ReadPixels(new Rect(0,0,256,144),0,0);after.Apply();Color[] a=after.GetPixels();
                payload=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=reflectionPayloadCapture;payload.ReadPixels(new Rect(0,0,256,144),0,0);payload.Apply();Color[] p=payload.GetPixels();
                using(var writer=new BinaryWriter(File.Create(Path.Combine(output,"reflection-energy-inputs.bytes")))){writer.Write(new byte[]{82,69,78,69,82,56,54,0});writer.Write(256);writer.Write(144);for(int pixel=0;pixel<b.Length;pixel++)foreach(var value in new[]{b[pixel],a[pixel],p[pixel]}){writer.Write(value.r);writer.Write(value.g);writer.Write(value.b);writer.Write(value.a);}}
                for(int at=0;at<b.Length;at++){for(int c=0;c<4;c++)reflectionEnergyReport.finite&=Finite(b[at][c])&&Finite(a[at][c])&&Finite(p[at][c]);if(coverage[at].r<=.5f)continue;float x=(at%256+.5f)/256,y=(at/256+.5f)/144;reflectionEnergyReport.whole.Add(b[at],a[at],p[at]);foreach(var face in reflectionEnergyReport.faces)if(face.Contains(x,y))face.Add(b[at],a[at],p[at]);}
                reflectionEnergyReport.whole.Finish();reflectionEnergyReport.relationWithinTolerance=reflectionEnergyReport.whole.pixels>0&&reflectionEnergyReport.whole.invalidSubtractionPixels==0;foreach(var face in reflectionEnergyReport.faces){face.Finish();reflectionEnergyReport.relationWithinTolerance&=face.pixels>5&&face.invalidSubtractionPixels==0;}
                if(!reflectionEnergyReport.finite||!reflectionEnergyReport.relationWithinTolerance)throw new InvalidOperationException("Reflection subtraction GPU evidence is non-finite or mismatched");
            } finally {if(before!=null)UnityEngine.Object.DestroyImmediate(before);if(after!=null)UnityEngine.Object.DestroyImmediate(after);if(payload!=null)UnityEngine.Object.DestroyImmediate(payload);}
        }
        void ReleaseVisibility(){if(VisibilityRequest==this)VisibilityRequest=null;if(VisibilityTarget!=null){VisibilityTarget.Release();VisibilityTarget=null;}if(visibilityCapture!=null){visibilityCapture.Release();UnityEngine.Object.DestroyImmediate(visibilityCapture);visibilityCapture=null;}if(NormalStoredTarget!=null){NormalStoredTarget.Release();NormalStoredTarget=null;}if(NormalProducerTarget!=null){NormalProducerTarget.Release();NormalProducerTarget=null;}if(normalStoredCapture!=null){normalStoredCapture.Release();UnityEngine.Object.DestroyImmediate(normalStoredCapture);normalStoredCapture=null;}if(normalProducerCapture!=null){normalProducerCapture.Release();UnityEngine.Object.DestroyImmediate(normalProducerCapture);normalProducerCapture=null;}VisibilityCamera=null;}
        static bool Owned(string name) {return name.StartsWith("Mother_")||name.StartsWith("Nurse_");}
        static string MaterialName(Material material) {return material==null?"":material.name.Replace(" (Instance)","");}
        static string RendererPath(Transform at) {return at.parent==null?at.name:RendererPath(at.parent)+"/"+at.name;}
        void Collect()
        {
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
            {
                bool card=false,eye=false,tooth=false,other=false;
                bool hasSkin=false;
                foreach(var material in renderer.sharedMaterials)
                {
                    string name=MaterialName(material);
                    bool c=Owned(name)&&(name.EndsWith("_Brows")||name.EndsWith("_Lashes"));
                    bool e=Owned(name)&&name.EndsWith("_Eyes");
                    bool t=Owned(name)&&name.EndsWith("_Teeth");
                    card|=c;eye|=e;tooth|=t;other|=!c&&!e&&!t;
                    hasSkin|=name=="Mother_Skin"||name=="Nurse_Skin";
                }
                if(hasSkin){skin.Add(renderer);originalMaterials.Add(renderer,renderer.sharedMaterials);}
                if(!card&&!eye&&!tooth)continue;
                if(other||(card?1:0)+(eye?1:0)+(tooth?1:0)!=1)throw new InvalidOperationException("Face isolation cannot disable mixed-material renderer: "+RendererPath(renderer.transform));
                if(!renderer.enabled)throw new InvalidOperationException("Eye isolation target was already disabled: "+RendererPath(renderer.transform));
                initial.Add(renderer,renderer.enabled);if(card)cards.Add(renderer);else if(eye)eyes.Add(renderer);else teeth.Add(renderer);
            }
            if(cards.Count!=4||eyes.Count!=2||teeth.Count!=2||skin.Count!=2)throw new InvalidOperationException("Face isolation needs four card/two eye/two tooth/two skin renderers; found "+cards.Count+"/"+eyes.Count+"/"+teeth.Count+"/"+skin.Count);
        }
        Surface[] Describe(List<SkinnedMeshRenderer> group)
        {
            var result=new List<Surface>();
            foreach(var renderer in group){var names=new List<string>();foreach(var material in renderer.sharedMaterials)names.Add(MaterialName(material));result.Add(new Surface{renderer=RendererPath(renderer.transform),materials=names.ToArray(),initiallyEnabled=renderer.enabled});}
            return result.ToArray();
        }
        static void Enable(List<SkinnedMeshRenderer> group,bool enabled) {foreach(var renderer in group)renderer.enabled=enabled;}
        void Restore()
        {
            foreach(var pair in initial)if(pair.Key!=null)pair.Key.enabled=pair.Value;
            foreach(var pair in originalMaterials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            foreach(var clone in clones)UnityEngine.Object.Destroy(clone);
        }
        bool SkinDefaultsZero()
        {
            bool valid=true;
            foreach(var pair in originalMaterials)foreach(var material in pair.Value)
            {
                string name=MaterialName(material);if(name!="Mother_Skin"&&name!="Nurse_Skin")continue;
                valid&=material.HasProperty("_SkinDiffuseReview")&&material.GetFloat("_SkinDiffuseReview")==0
                    &&material.HasProperty("_SkinReflectionReview")&&material.GetFloat("_SkinReflectionReview")==0;
            }
            return valid;
        }
        Binding[] DescribeBindings()
        {
            var result=new List<Binding>();
            foreach(var pair in originalMaterials)foreach(var material in pair.Value)
            {
                string name=MaterialName(material);if(name!="Mother_Skin"&&name!="Nurse_Skin")continue;
                bool controls=material.HasProperty("_SkinDFGEnabled")&&material.HasProperty("_SkinDFG");
                var texture=controls?material.GetTexture("_SkinDFG") as Texture2D:null;
                var resource=Resources.Load<Material>("ReturnRealism/"+name);
                var item=new Binding{renderer=RendererPath(pair.Key.transform),material=name,shader=material.shader.name,materialInstance=material.GetInstanceID(),resourceMaterialInstance=resource==null?0:resource.GetInstanceID(),sameMaterialAsResource=material==resource,controlsPresent=controls,skinFlag=material.GetFloat("_Skin"),dfgFlag=controls?material.GetFloat("_SkinDFGEnabled"):-1,visibilityFlag=material.HasProperty("_SkinReflectionVisibility")?material.GetFloat("_SkinReflectionVisibility"):-1,specularSetup=material.IsKeywordEnabled("_SPECULAR_SETUP"),lookup=texture==null?"none":texture.name,lookupInstance=texture==null?0:texture.GetInstanceID(),sameLookupAsResource=texture!=null&&texture==expectedLookup,width=texture==null?0:texture.width,height=texture==null?0:texture.height,lookupFormat=texture==null?"none":texture.format.ToString(),lookupReadable=texture!=null&&texture.isReadable};
                if(item.lookupReadable){Color sample=texture.GetPixelBilinear(.1f,(.56f-.25f)/.60f);item.lookupA=sample.r;item.lookupB=sample.g;item.lookupSkinFactor=.028f*sample.r+sample.g;}
                item.skinDetail=material.GetFloat("_SkinDetail");item.skinOiliness=material.GetFloat("_SkinOiliness");item.smoothness=material.GetFloat("_Smoothness");item.bumpScale=material.GetFloat("_BumpScale");var bump=material.GetTexture("_BumpMap");item.bumpTexture=bump==null?"":bump.name;
                result.Add(item);
            }
            return result.ToArray();
        }
        bool ReflectionSkin(float dfg,float environmentRemoval,float visibility,float? detail,float? oil,string mode,List<SurfaceSetting> settings)
        {
            int count=0;bool valid=true;
            foreach(var pair in originalMaterials)
            {
                Material[] temporary=(Material[])pair.Value.Clone();
                for(int slot=0;slot<temporary.Length;slot++)
                {
                    string name=MaterialName(temporary[slot]);if(name!="Mother_Skin"&&name!="Nurse_Skin")continue;
                    var original=temporary[slot];
                    if(!original.HasProperty("_SkinDFGEnabled")||!original.HasProperty("_SkinDFG")||!original.HasProperty("_SkinDetail")||!original.HasProperty("_SkinOiliness"))throw new InvalidOperationException("Native skin material lacks surface controls: "+name);
                    float expectedDetail=detail??original.GetFloat("_SkinDetail"),expectedOil=oil??original.GetFloat("_SkinOiliness");
                    var clone=new Material(original);clone.name=name+" [surface review "+mode+"]";clone.SetFloat("_SkinDFGEnabled",dfg);clone.SetTexture("_SkinDFG",expectedLookup);clone.SetFloat("_SkinReflectionReview",environmentRemoval);clone.SetFloat("_SkinReflectionVisibility",visibility);clone.SetFloat("_SkinDetail",expectedDetail);clone.SetFloat("_SkinOiliness",expectedOil);clones.Add(clone);temporary[slot]=clone;count++;
                    valid&=clone.GetFloat("_SkinReflectionVisibility")==visibility&&clone.GetFloat("_SkinDFGEnabled")==dfg&&clone.GetTexture("_SkinDFG")==expectedLookup&&clone.GetFloat("_SkinReflectionReview")==environmentRemoval&&clone.GetFloat("_SkinDiffuseReview")==0;
                    valid&=clone.GetFloat("_SkinDetail")==expectedDetail&&clone.GetFloat("_SkinOiliness")==expectedOil&&clone.GetFloat("_Smoothness")==original.GetFloat("_Smoothness")&&clone.GetFloat("_BumpScale")==original.GetFloat("_BumpScale")&&clone.GetTexture("_BumpMap")==original.GetTexture("_BumpMap")&&clone.IsKeywordEnabled("_SPECULAR_SETUP")==original.IsKeywordEnabled("_SPECULAR_SETUP");
                    var bump=clone.GetTexture("_BumpMap");settings.Add(new SurfaceSetting{mode=mode,renderer=RendererPath(pair.Key.transform),material=name,detail=clone.GetFloat("_SkinDetail"),oil=clone.GetFloat("_SkinOiliness"),smoothness=clone.GetFloat("_Smoothness"),bumpScale=clone.GetFloat("_BumpScale"),bumpTexture=bump==null?"":bump.name,dfg=dfg,visibility=visibility,environmentRemoval=environmentRemoval});
                }
                pair.Key.sharedMaterials=temporary;
                appliedMaterials[pair.Key]=temporary;
            }
            return valid&&count==2;
        }
        bool AppliedSlotsRetained() {
            foreach(var pair in appliedMaterials){if(pair.Key==null)return false;var current=pair.Key.sharedMaterials;if(current.Length!=pair.Value.Length)return false;for(int slot=0;slot<current.Length;slot++)if(current[slot]!=pair.Value[slot])return false;}
            return appliedMaterials.Count==2;
        }
        IEnumerator CaptureSurfaceCase(string mode,Func<string,IEnumerator> capture,List<SurfaceGpuCase> cases) {
            bool slots=AppliedSlotsRetained();yield return CheckVisibility(mode);slots&=AppliedSlotsRetained();yield return capture(mode);slots&=AppliedSlotsRetained();
            string root=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR"),folder=Path.Combine(root,"skin-surface-gpu",mode);
            bool blocks=false;foreach(var pair in appliedMaterials)blocks|=pair.Key!=null&&pair.Key.HasPropertyBlock();
            var item=new SurfaceGpuCase{mode=mode,dataDirectory=folder,captured=visibilityReport.captured,finite=visibilityReport.finite,resourcesReleased=visibilityReport.resourcesReleased,normalTransportMatchesProducer=visibilityReport.normalTransportMatchesProducer,materialSlotsRetained=slots,propertyBlocksPresent=blocks};cases.Add(item);
            if(!slots)throw new InvalidOperationException("Temporary skin material slots changed during capture: "+mode);
            if(mode=="Skin-Surface-Original")foreach(string file in new[]{"skin-visibility-gpu.json","skin-normal-transport.json","skin-visibility-gpu.png"})File.Copy(Path.Combine(folder,file),Path.Combine(root,file),true);
        }
        IEnumerator CaptureWindowShadows(Func<string,IEnumerator> capture) {
            Light window=null;int found=0;
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude,FindObjectsSortMode.None))
                if(light.name=="Soft window light"&&light.enabled){window=light;found++;}
            if(found!=1||window.shadows!=LightShadows.Soft)throw new InvalidOperationException("Window shadow comparison needs the unique active soft-shadow hospital key");
            var data=window.GetComponent<UniversalAdditionalLightData>();
            if(data==null||data.usePipelineSettings)throw new InvalidOperationException("Window shadow comparison needs the production local-bias override");
            LightShadows original=window.shadows;var states=new List<WindowShadowState>();
            var report=new WindowShadowReport{light=window.name,originalShadows=original.ToString(),frozenHospitalTime=poseTime};
            string[] frames={"Hospital-Window-Shadow-On","Hospital-Window-Shadow-Off","Hospital-Window-Shadow-On-Again"};
            try {
                for(int index=0;index<frames.Length;index++){
                    window.shadows=index==1?LightShadows.None:original;
                    // Give culling/shadow setup a full frame before the ordinary forward-colour screenshot.
                    yield return null;yield return capture(frames[index]);
                    bool slots=AppliedSlotsRetained();states.Add(new WindowShadowState{frame=frames[index],shadows=window.shadows.ToString(),materialSlotsRetained=slots,localBias=!data.usePipelineSettings,depthBias=window.shadowBias,normalBias=window.shadowNormalBias});
                    if(!slots)throw new InvalidOperationException("Skin material slots changed during window shadow capture");
                }
                windowShadowsRecorded=states.Count==3;
            } finally {
                window.shadows=original;windowShadowsRestored=window.shadows==original;
                report.recorded=windowShadowsRecorded;report.restored=windowShadowsRestored;report.states=states.ToArray();
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"window-shadow-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        FaceBoundaryState BoundaryState(string frame,List<FaceAlbedoOriginal> originals,bool flat,bool overlaysOff) {
            var bindings=new List<FaceAlbedoBinding>();var cardStates=new List<string>();bool controls=true,inputs=true;
            foreach(var original in originals){var m=original.material;Texture map=m.GetTexture("_BaseMap");Color colour=m.GetColor("_BaseColor");bindings.Add(new FaceAlbedoBinding{material=m.name,baseMap=map==null?"none":map.name,baseColour=colour});inputs&=map==(flat?Texture2D.whiteTexture:original.map)&&colour==(flat?new Color(.55f,.36f,.24f,1):original.colour);for(int i=0;i<BoundaryControls.Length;i++)controls&=Mathf.Abs(m.GetFloat(BoundaryControls[i])-original.controls[i])<.00001f;}
            foreach(var card in cards){cardStates.Add(RendererPath(card.transform)+" enabled="+card.enabled);inputs&=card.enabled==(overlaysOff?false:initial[card]);}
            return new FaceBoundaryState{frame=frame,materialSlotsRetained=AppliedSlotsRetained(),skinControlsRetained=controls,inputsMatched=inputs,cardStates=cardStates.ToArray(),bindings=bindings.ToArray()};
        }
        IEnumerator CaptureFaceBoundaries(Func<string,IEnumerator> capture) {
            var originals=new List<FaceAlbedoOriginal>();
            foreach(var pair in appliedMaterials)foreach(var m in pair.Value)if(m!=null&&m.GetFloat("_Skin")>.5f){var controls=new float[BoundaryControls.Length];for(int i=0;i<controls.Length;i++)controls[i]=m.GetFloat(BoundaryControls[i]);originals.Add(new FaceAlbedoOriginal{material=m,map=m.GetTexture("_BaseMap"),colour=m.GetColor("_BaseColor"),controls=controls});}
            if(originals.Count!=2||cards.Count!=4)throw new InvalidOperationException("Face-boundary comparison needs two original-profile skins and four existing overlays");
            var states=new List<FaceBoundaryState>();var report=new FaceBoundaryReport{frozenHospitalTime=poseTime};
            try {
                foreach(var original in originals){original.material.SetTexture("_BaseMap",Texture2D.whiteTexture);original.material.SetColor("_BaseColor",new Color(.55f,.36f,.24f,1));}
                yield return null;yield return capture("Hospital-Face-Flat-Albedo");states.Add(BoundaryState("Hospital-Face-Flat-Albedo",originals,true,false));
                foreach(var original in originals){original.material.SetTexture("_BaseMap",original.map);original.material.SetColor("_BaseColor",original.colour);}
                foreach(var card in cards)card.enabled=false;
                yield return null;yield return capture("Hospital-Face-Overlays-Off");states.Add(BoundaryState("Hospital-Face-Overlays-Off",originals,false,true));
                foreach(var card in cards)card.enabled=initial[card];
                yield return null;yield return capture("Hospital-Face-Original-Restored");states.Add(BoundaryState("Hospital-Face-Original-Restored",originals,false,false));
                faceBoundariesRecorded=states.Count==3;foreach(var state in states)faceBoundariesRecorded&=state.materialSlotsRetained&&state.skinControlsRetained&&state.inputsMatched;
                if(!faceBoundariesRecorded)throw new InvalidOperationException("Face-boundary comparison inputs changed during capture");
            } finally {
                foreach(var original in originals){original.material.SetTexture("_BaseMap",original.map);original.material.SetColor("_BaseColor",original.colour);}
                foreach(var card in cards)card.enabled=initial[card];
                var restored=BoundaryState("finally restored",originals,false,false);faceBoundariesRestored=restored.inputsMatched&&restored.skinControlsRetained&&restored.materialSlotsRetained;
                report.recorded=faceBoundariesRecorded;report.restored=faceBoundariesRestored;report.states=states.ToArray();
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"face-boundary-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        static float TriangleDistance(Ray ray,Vector3 a,Vector3 b,Vector3 c) {
            Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)<.00000001f)return -1;float inverse=1/det;Vector3 s=ray.origin-a;float u=Vector3.Dot(s,p)*inverse;if(u<0||u>1)return -1;
            Vector3 q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return -1;float distance=Vector3.Dot(e2,q)*inverse;return distance>0?distance:-1;
        }
        static Vector3 NormalizeTriangleNormal(Vector3 cross) {
            double length=Math.Sqrt((double)cross.x*cross.x+(double)cross.y*cross.y+(double)cross.z*cross.z);
            if(double.IsNaN(length)||double.IsInfinity(length)||length<=0)throw new InvalidOperationException("Invalid posed triangle cross-product");
            Vector3 normal=new Vector3((float)(cross.x/length),(float)(cross.y/length),(float)(cross.z/length));
            if(!Finite(normal.x)||!Finite(normal.y)||!Finite(normal.z)||Mathf.Abs(normal.sqrMagnitude-1)>1e-4f)throw new InvalidOperationException("Invalid unit posed triangle normal");
            return normal;
        }
        static PosedNormalSample SamplePosedNormal(BakedFaceGeometry surface,int at,Ray ray,float distance) {
            int ia=surface.triangles[at],ib=surface.triangles[at+1],ic=surface.triangles[at+2];Vector3 a=surface.positions[ia],b=surface.positions[ib],c=surface.positions[ic],ab=b-a,ac=c-a,ap=ray.GetPoint(distance)-a;
            float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ab,ac),cc=Vector3.Dot(ac,ac),pa=Vector3.Dot(ap,ab),pc=Vector3.Dot(ap,ac),denominator=aa*cc-bb*bb;
            if(Mathf.Abs(denominator)<1e-20f)throw new InvalidOperationException("Degenerate posed normal sample triangle");float u=(cc*pa-bb*pc)/denominator,v=(aa*pc-bb*pa)/denominator;Vector3 bary=new Vector3(1-u-v,u,v),raw=NormalizeTriangleNormal(Vector3.Cross(ab,ac)),geometric=raw*surface.transformParity;
            Vector3 na=surface.normals[ia],nb=surface.normals[ib],nc=surface.normals[ic],weighted=na*bary.x+nb*bary.y+nc*bary.z;if(weighted.sqrMagnitude<1e-12f)throw new InvalidOperationException("Zero interpolated posed shading normal");Vector3 shading=weighted.normalized;float dot=Mathf.Clamp(Vector3.Dot(shading,geometric),-1,1);
            var result=new PosedNormalSample{captured=true,finite=true,triangle=at/3,vertices=new[]{ia,ib,ic},barycentric=bary,rawGeometricNormal=raw,surfaceNormal=geometric,shadingNormal=shading,transformParity=surface.transformParity,shadingSurfaceDot=dot,shadingSurfaceAngleDegrees=Mathf.Acos(dot)*Mathf.Rad2Deg,rawWindingViewDot=Vector3.Dot(raw,-ray.direction),surfaceViewDot=Vector3.Dot(geometric,-ray.direction),shadingViewDot=Vector3.Dot(shading,-ray.direction),vertexSurfaceAngles=new[]{Mathf.Acos(Mathf.Clamp(Vector3.Dot(na,geometric),-1,1))*Mathf.Rad2Deg,Mathf.Acos(Mathf.Clamp(Vector3.Dot(nb,geometric),-1,1))*Mathf.Rad2Deg,Mathf.Acos(Mathf.Clamp(Vector3.Dot(nc,geometric),-1,1))*Mathf.Rad2Deg}};
            foreach(float value in new[]{bary.x,bary.y,bary.z,raw.x,raw.y,raw.z,shading.x,shading.y,shading.z,result.shadingSurfaceAngleDegrees,result.rawWindingViewDot,result.surfaceViewDot,result.shadingViewDot})result.finite&=Finite(value);foreach(float angle in result.vertexSurfaceAngles)result.finite&=Finite(angle);result.finite&=bary.x>=-.002f&&bary.y>=-.002f&&bary.z>=-.002f&&Mathf.Abs(bary.x+bary.y+bary.z-1)<.002f;
            result.finite&=Mathf.Abs(raw.sqrMagnitude-1)<1e-4f&&Mathf.Abs(geometric.sqrMagnitude-1)<1e-4f&&Mathf.Abs(shading.sqrMagnitude-1)<1e-4f;
            if(!result.finite)throw new InvalidOperationException("Invalid posed normal interpolation");return result;
        }
        static float NormalAngle(Vector3 a,Vector3 b)=>Mathf.Acos(Mathf.Clamp(Vector3.Dot(a,b),-1,1))*Mathf.Rad2Deg;
        static bool FiniteVector(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static Vector3 OriginSurface(Vector3[] positions,int[] triangles,int triangle,out bool degenerate) {
            int at=triangle*3;Vector3 cross=Vector3.Cross(positions[triangles[at+1]]-positions[triangles[at]],positions[triangles[at+2]]-positions[triangles[at]]);
            degenerate=cross.x==0&&cross.y==0&&cross.z==0;return degenerate?Vector3.zero:NormalizeTriangleNormal(cross);
        }
        static NormalExpressionPrediction PredictExpressions(BakedFaceGeometry surface,ReturnNormalRestSnapshot.SourceMesh saved,ReturnNormalRestSnapshot.Vertex rest,Vector3 posedSurface,NormalOriginReport report) {
            if(rest.influences.Length!=1||rest.influences[0].boneName!="head"||Mathf.Abs(rest.influences[0].weight-1)>1e-6f||saved.frames==null||saved.frames.Length!=3)throw new InvalidOperationException("Expression prediction requires the captured head-only source");
            SkinnedMeshRenderer renderer=surface.source;var influence=rest.influences[0];Matrix4x4 skinMatrix=renderer.worldToLocalMatrix*renderer.bones[influence.bone].localToWorldMatrix*influence.bindPose,normalMatrix=renderer.localToWorldMatrix.inverse.transpose;
            Vector3 localNormal=rest.normal,localPosition=rest.position;var deltas=new List<NormalExpressionDelta>();
            foreach(var frame in saved.frames){
                if(renderer.sharedMesh.GetBlendShapeName(frame.shape)!=frame.name||frame.frame!=0||!Finite(frame.weight)||frame.weight<=0)throw new InvalidOperationException("Expression frame binding mismatch");ReturnNormalRestSnapshot.ExpressionDelta delta=null;foreach(var candidate in frame.deltas)if(candidate.vertex==rest.vertex){if(delta!=null)throw new InvalidOperationException("Duplicate expression vertex delta");delta=candidate;}
                if(delta==null)throw new InvalidOperationException("Missing expression vertex delta");float weight=renderer.GetBlendShapeWeight(frame.shape),factor=weight/frame.weight;if(!Finite(weight)||!Finite(factor)||!FiniteVector(delta.normal)||!FiniteVector(delta.position))throw new InvalidOperationException("Invalid expression prediction input");localNormal+=delta.normal*factor;localPosition+=delta.position*factor;deltas.Add(new NormalExpressionDelta{name=frame.name,weight=weight,frameWeight=frame.weight,factor=factor,normalDelta=delta.normal,positionDelta=delta.position,weightedNormalDelta=delta.normal*factor});report.expressionDeltaSamples++;
            }
            for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++){bool captured=false;foreach(var frame in saved.frames)captured|=frame.shape==i;if(!captured&&Mathf.Abs(renderer.GetBlendShapeWeight(i))>1e-6f)throw new InvalidOperationException("Uncaptured active expression in prediction");}
            Vector3 rigid=NormalizeTriangleNormal(normalMatrix.MultiplyVector(skinMatrix.MultiplyVector(rest.normal))),predicted=NormalizeTriangleNormal(normalMatrix.MultiplyVector(skinMatrix.MultiplyVector(localNormal))),worldPosition=renderer.transform.TransformPoint(skinMatrix.MultiplyPoint3x4(localPosition)),actual=surface.normals[rest.vertex],difference=predicted-actual;
            var result=new NormalExpressionPrediction{finite=true,rigidNormal=rigid,expressionNormal=predicted,expressionPosition=worldPosition,expressionLocalInput=localNormal,rigidSurfaceAngle=NormalAngle(rigid,posedSurface),expressionSurfaceAngle=NormalAngle(predicted,posedSurface),rigidActualAngle=NormalAngle(rigid,actual),expressionActualAngle=NormalAngle(predicted,actual),maximumNormalComponentError=Mathf.Max(Mathf.Abs(difference.x),Mathf.Abs(difference.y),Mathf.Abs(difference.z)),positionErrorMM=Vector3.Distance(worldPosition,surface.positions[rest.vertex])*1000,deltas=deltas.ToArray()};
            result.finite&=FiniteVector(rigid)&&FiniteVector(predicted)&&FiniteVector(worldPosition)&&FiniteVector(localNormal);foreach(float value in new[]{result.rigidSurfaceAngle,result.expressionSurfaceAngle,result.rigidActualAngle,result.expressionActualAngle,result.maximumNormalComponentError,result.positionErrorMM})result.finite&=Finite(value);if(!result.finite)throw new InvalidOperationException("Invalid finite expression prediction");report.expressionPredictions++;return result;
        }
        static void CaptureNormalOrigins(BakedFaceGeometry surface,ReturnNormalRestSnapshot snapshot,NormalOriginReport report,List<NormalOriginTriangle> samples) {
            if(surface.role!="Skin")return;SkinnedMeshRenderer renderer=surface.source;Mesh shared=renderer.sharedMesh;ReturnNormalRestSnapshot.SourceMesh saved=null;
            foreach(var candidate in snapshot.meshes)if(candidate.person==surface.person){if(saved!=null)throw new InvalidOperationException("Duplicate rest snapshot source");saved=candidate;}
            Transform[] bones=renderer.bones;ulong indexCount=0;for(int i=0;i<shared.subMeshCount;i++)indexCount+=shared.GetIndexCount(i);
            if(saved==null||saved.mesh!=shared||saved.vertexCount!=shared.vertexCount||saved.indexCount!=(long)indexCount||saved.vertexCount!=surface.positions.Length||saved.indexCount!=surface.triangles.Length||saved.boneNames.Length!=bones.Length||saved.importedReadable!=shared.isReadable)throw new InvalidOperationException("Rest snapshot exact mesh binding mismatch");
            for(int i=0;i<bones.Length;i++)if(bones[i]==null||bones[i].name!=saved.boneNames[i])throw new InvalidOperationException("Rest snapshot bone binding mismatch");report.sourceMeshBindings++;
            var blends=new List<NormalOriginBlend>();for(int i=0;i<shared.blendShapeCount;i++){float weight=renderer.GetBlendShapeWeight(i);report.finite&=Finite(weight);blends.Add(new NormalOriginBlend{name=shared.GetBlendShapeName(i),weight=weight});}
            Matrix4x4 normalMatrix=renderer.localToWorldMatrix.inverse.transpose;
            foreach(var target in saved.triangles){
                int triangle=target.triangle;for(int i=0;i<3;i++)if(surface.triangles[triangle*3+i]!=target.indices[i])throw new InvalidOperationException("Rest snapshot target index mismatch");
                bool posedDegenerate;Vector3 restSurface=target.restSurface,posedSurface=OriginSurface(surface.positions,surface.triangles,triangle,out posedDegenerate)*surface.transformParity;if(posedDegenerate)throw new InvalidOperationException("Degenerate posed origin target");
                var vertices=new List<NormalOriginVertex>();
                foreach(var rest in target.vertices){
                    int vertex=rest.vertex;Vector3 restNormal=NormalizeTriangleNormal(rest.normal),restWorldNormal=NormalizeTriangleNormal(normalMatrix.MultiplyVector(restNormal)),posedNormal=surface.normals[vertex];var influences=new List<NormalOriginInfluence>();float sum=0;
                    foreach(var influence in rest.influences){
                        int bone=influence.bone;if(bone<0||bone>=bones.Length||bones[bone].name!=influence.boneName||!Finite(influence.weight)||influence.weight<0)throw new InvalidOperationException("Invalid snapshot origin influence");float poseParity=(renderer.worldToLocalMatrix*bones[bone].localToWorldMatrix*influence.bindPose).determinant;report.finite&=Finite(influence.bindParity)&&Finite(poseParity);sum+=influence.weight;
                        influences.Add(new NormalOriginInfluence{bone=bone,name=RendererPath(bones[bone]),weight=influence.weight,bindParity=influence.bindParity,poseParity=poseParity});
                    }
                    if(influences.Count==0||!Finite(sum)||Mathf.Abs(sum-1)>.001f)throw new InvalidOperationException("Invalid snapshot origin influence sum");var adjacent=new List<NormalOriginAdjacent>();
                    foreach(var face in rest.adjacent){
                        for(int i=0;i<3;i++)if(surface.triangles[face.triangle*3+i]!=face.indices[i])throw new InvalidOperationException("Rest snapshot adjacent index mismatch");bool posedZero;Vector3 posedFace=OriginSurface(surface.positions,surface.triangles,face.triangle,out posedZero)*surface.transformParity;
                        adjacent.Add(new NormalOriginAdjacent{triangle=face.triangle,restDegenerate=face.degenerate,posedDegenerate=posedZero,restSurface=face.restSurface,posedSurface=posedFace,restAngle=face.degenerate?-1:NormalAngle(restNormal,face.restSurface),posedAngle=posedZero?-1:NormalAngle(posedNormal,posedFace)});
                    }
                    report.finite&=FiniteVector(rest.position)&&FiniteVector(surface.positions[vertex])&&FiniteVector(rest.normal)&&FiniteVector(restWorldNormal)&&FiniteVector(posedNormal);foreach(var face in adjacent)report.finite&=Finite(face.restAngle)&&Finite(face.posedAngle)&&FiniteVector(face.restSurface)&&FiniteVector(face.posedSurface);
                    var expression=PredictExpressions(surface,saved,rest,posedSurface,report);report.finite&=expression.finite;vertices.Add(new NormalOriginVertex{vertex=vertex,restPosition=rest.position,posedPosition=surface.positions[vertex],restNormal=rest.normal,restWorldNormal=restWorldNormal,posedNormal=posedNormal,restSurfaceAngle=NormalAngle(restNormal,restSurface),posedSurfaceAngle=NormalAngle(posedNormal,posedSurface),restPoseAngle=NormalAngle(restWorldNormal,posedNormal),weightSum=sum,influences=influences.ToArray(),adjacent=adjacent.ToArray(),expression=expression});report.vertices++;
                }
                samples.Add(new NormalOriginTriangle{person=surface.person,renderer=surface.renderer,triangle=triangle,restSurface=restSurface,posedSurface=posedSurface,vertices=vertices.ToArray(),blends=blends.ToArray()});report.triangles++;
            }
        }
        void CapturePosedGeometry() {
            Camera camera=null;foreach(var focus in UnityEngine.Object.FindObjectsByType<ReturnFocus>(FindObjectsSortMode.None))if(focus.DepthCamera!=null&&focus.DepthCamera.enabled){if(camera!=null)throw new InvalidOperationException("Multiple owned geometry cameras");camera=focus.DepthCamera;}
            if(camera==null)throw new InvalidOperationException("No owned geometry camera");
            var owned=new List<Mesh>();var geometry=new List<BakedFaceGeometry>();var surfaces=new List<PosedGeometrySurface>();var probes=new List<PosedGeometryProbe>();var report=new PosedGeometryReport{frozenHospitalTime=poseTime};var origins=new NormalOriginReport{frozenHospitalTime=poseTime};var originSamples=new List<NormalOriginTriangle>();
            var sourceSnapshot=Resources.Load<ReturnNormalRestSnapshot>(ReturnNormalRestSnapshot.ResourceName);if(sourceSnapshot==null||!sourceSnapshot.captured||!sourceSnapshot.finite||sourceSnapshot.meshes.Length!=2||!sourceSnapshot.borrowedViewsCleared||sourceSnapshot.clearedViewCount!=4||sourceSnapshot.ownedNativeAllocations!=0||sourceSnapshot.expressionFrames!=6||sourceSnapshot.expressionDeltaSamples!=27)throw new InvalidOperationException("Editor rest snapshot missing or invalid");origins.expressionFrames=sourceSnapshot.expressionFrames;origins.sourceCaptured=true;origins.weightStreams=sourceSnapshot.weightStreams;origins.borrowedViewsCleared=sourceSnapshot.clearedViewCount;
            try {
                foreach(string role in new[]{"Skin","Eyes","Teeth"})foreach(var renderer in role=="Skin"?skin:role=="Eyes"?eyes:teeth){
                    var mesh=new Mesh{name="RETURN posed face geometry snapshot"};owned.Add(mesh);renderer.BakeMesh(mesh,false);Vector3[] positions=mesh.vertices,normals=mesh.normals;int[] triangles=mesh.triangles;Matrix4x4 world=renderer.localToWorldMatrix,normalMatrix=world.inverse.transpose;float determinant=world.determinant;
                    if(positions.Length==0||triangles.Length==0||triangles.Length%3!=0)throw new InvalidOperationException("Empty posed face geometry");
                    if(normals.Length!=positions.Length||!Finite(determinant)||Mathf.Abs(determinant)<1e-12f)throw new InvalidOperationException("Missing normals or singular posed renderer transform");
                    Vector3 minimum=Vector3.one*float.PositiveInfinity,maximum=Vector3.one*float.NegativeInfinity;
                    for(int i=0;i<positions.Length;i++){positions[i]=renderer.transform.TransformPoint(positions[i]);Vector3 p=positions[i];report.finite&=Finite(p.x)&&Finite(p.y)&&Finite(p.z);minimum=Vector3.Min(minimum,p);maximum=Vector3.Max(maximum,p);Vector3 n=normalMatrix.MultiplyVector(normals[i]);report.normalsFinite&=Finite(n.x)&&Finite(n.y)&&Finite(n.z)&&n.sqrMagnitude>1e-12f;normals[i]=n.normalized;}
                    string person=MaterialName(renderer.sharedMaterials[0]).StartsWith("Mother_")?"Mother":"Nurse",name=RendererPath(renderer.transform);
                    var baked=new BakedFaceGeometry{person=person,role=role,renderer=name,source=renderer,positions=positions,normals=normals,triangles=triangles,transformParity=Mathf.Sign(determinant)};geometry.Add(baked);CaptureNormalOrigins(baked,sourceSnapshot,origins,originSamples);surfaces.Add(new PosedGeometrySurface{person=person,role=role,renderer=name,vertices=positions.Length,triangles=triangles.Length/3,minimum=minimum,maximum=maximum});
                }
                foreach(var renderer in skin){
                    string person=MaterialName(renderer.sharedMaterials[0]).StartsWith("Mother_")?"Mother":"Nurse";Transform left=null,right=null;foreach(var bone in renderer.transform.parent.GetComponentsInChildren<Transform>(true)){if(bone.name.EndsWith("eye_l"))left=bone;if(bone.name.EndsWith("eye_r"))right=bone;}
                    if(left==null||right==null)throw new InvalidOperationException("Missing posed face eye landmarks");Vector3 centre=(left.position+right.position)*.5f;float separation=Vector3.Distance(left.position,right.position);Vector3 up=camera.transform.up*separation,horizontal=camera.transform.right*separation;
                    string[] labels={"LeftEyeCentre","RightEyeCentre","LeftEyeUpper","RightEyeUpper","LeftEyeLower","RightEyeLower","Nose","LipLeft","LipCentre","LipRight"};
                    Vector3[] targets={left.position,right.position,left.position+up*.10f,right.position+up*.10f,left.position-up*.10f,right.position-up*.10f,centre-up*.58f,centre-up*1.25f-horizontal*.33f,centre-up*1.25f,centre-up*1.25f+horizontal*.33f};
                    for(int index=0;index<targets.Length;index++){
                        Vector3 viewport=camera.WorldToViewportPoint(targets[index]);Ray ray=camera.ViewportPointToRay(new Vector3(viewport.x,viewport.y,0));var hits=new List<PosedGeometryHit>();float closest=float.PositiveInfinity;string nearest="None";
                        foreach(var surface in geometry)if(surface.person==person){float best=float.PositiveInfinity;int bestAt=-1;for(int at=0;at<surface.triangles.Length;at+=3){float d=TriangleDistance(ray,surface.positions[surface.triangles[at]],surface.positions[surface.triangles[at+1]],surface.positions[surface.triangles[at+2]]);if(d>0&&d<best){best=d;bestAt=at;}}bool hit=!float.IsPositiveInfinity(best);if(hit){report.finite&=Finite(best);if(best<closest){closest=best;nearest=surface.role;}}PosedNormalSample normalSample=null;if(hit&&surface.role=="Skin"){normalSample=SamplePosedNormal(surface,bestAt,ray,best);report.skinNormalSamples++;report.normalsFinite&=normalSample.finite;}hits.Add(new PosedGeometryHit{role=surface.role,renderer=surface.renderer,hit=hit,distanceMM=hit?best*1000:-1,world=hit?ray.GetPoint(best):Vector3.zero,normalSample=normalSample});}
                        report.finite&=Finite(viewport.x)&&Finite(viewport.y)&&Finite(viewport.z);probes.Add(new PosedGeometryProbe{person=person,label=labels[index],viewport=new Vector2(viewport.x,viewport.y),nearestRole=nearest,hits=hits.ToArray()});
                    }
                }
                if(!report.finite||!report.normalsFinite||report.skinNormalSamples!=20)throw new InvalidOperationException("Invalid posed geometry/normal samples");
                origins.sourceBound=origins.sourceMeshBindings==2;origins.captured=origins.sourceCaptured&&origins.sourceBound&&origins.runtimeSourceArrayReads==0&&origins.finite&&origins.resourcesReleased&&origins.triangles==3&&origins.vertices==9&&origins.weightStreams==2&&origins.borrowedViewsCleared==4&&origins.ownedNativeAllocations==0&&origins.expressionFrames==6&&origins.expressionDeltaSamples==27&&origins.expressionPredictions==9;if(!origins.captured)throw new InvalidOperationException("Invalid normal origin report");
            } finally {
                report.snapshots=owned.Count;foreach(var mesh in owned)UnityEngine.Object.DestroyImmediate(mesh);report.resourcesReleased=owned.Count==6;foreach(var mesh in owned)report.resourcesReleased&=mesh==null;report.surfaces=surfaces.ToArray();report.probes=probes.ToArray();
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"posed-face-geometry.json"),JsonUtility.ToJson(report,true));
                origins.samples=originSamples.ToArray();File.WriteAllText(Path.Combine(output,"normal-origin-geometry.json"),JsonUtility.ToJson(origins,true));
            }
        }
        static int EnabledCount(List<SkinnedMeshRenderer> group){int count=0;foreach(var renderer in group)if(renderer.enabled)count++;return count;}
        IEnumerator CaptureOpaqueFaces(Func<string,IEnumerator> capture) {
            CapturePosedGeometry();var states=new List<OpaqueFaceState>();var report=new OpaqueFaceReport{frozenHospitalTime=poseTime,eyes=Describe(eyes),teeth=Describe(teeth)};
            string[] frames={"Hospital-Face-Eyes-Off","Hospital-Face-Teeth-Off","Hospital-Face-Opaque-Off","Hospital-Face-Opaque-Restored"};
            try {
                for(int index=0;index<frames.Length;index++){
                    bool eyesOn=index==1||index==3,teethOn=index==0||index==3;Enable(eyes,eyesOn);Enable(teeth,teethOn);
                    yield return null;yield return capture(frames[index]);int eyeCount=EnabledCount(eyes),toothCount=EnabledCount(teeth);bool slots=AppliedSlotsRetained(),inputs=eyeCount==(eyesOn?2:0)&&toothCount==(teethOn?2:0);
                    states.Add(new OpaqueFaceState{frame=frames[index],enabledEyes=eyeCount,enabledTeeth=toothCount,materialSlotsRetained=slots,inputsMatched=inputs});if(!slots||!inputs)throw new InvalidOperationException("Opaque face comparison inputs changed during capture");
                }
                opaqueFacesRecorded=states.Count==4;
            } finally {
                opaqueFacesRestored=true;foreach(var renderer in eyes){renderer.enabled=initial[renderer];opaqueFacesRestored&=renderer.enabled==initial[renderer];}foreach(var renderer in teeth){renderer.enabled=initial[renderer];opaqueFacesRestored&=renderer.enabled==initial[renderer];}
                report.recorded=opaqueFacesRecorded;report.restored=opaqueFacesRestored;report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"opaque-face-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureSkinComposite(Func<string,IEnumerator> capture) {
            foreach(var focus in UnityEngine.Object.FindObjectsByType<ReturnFocus>(FindObjectsSortMode.None))if(focus.DepthCamera!=null&&focus.DepthCamera.enabled){if(CompositeCamera!=null)throw new InvalidOperationException("Multiple owned composite cameras");CompositeCamera=focus.DepthCamera;}
            if(CompositeCamera==null||SkinCompositeRequest!=null)throw new InvalidOperationException("Skin composite review needs one owned camera and no pending request");
            var states=new List<SkinCompositeState>();var report=new SkinCompositeReport{frozenHospitalTime=poseTime};string[] frames={"Hospital-Skin-Composite-On","Hospital-Skin-Composite-Off","Hospital-Skin-Composite-On-Again"};
            try {
                for(int index=0;index<frames.Length;index++){
                    CompositeBypassRequested=index==1;compositeDrawScheduled=false;compositeBypassScheduled=false;SkinCompositeRequest=this;
                    yield return null;yield return capture(frames[index]);bool slots=AppliedSlotsRetained();
                    states.Add(new SkinCompositeState{frame=frames[index],requestedBypass=CompositeBypassRequested,drawScheduled=compositeDrawScheduled,scheduledBypass=compositeBypassScheduled,materialSlotsRetained=slots});
                    if(!compositeDrawScheduled||compositeBypassScheduled!=CompositeBypassRequested||!slots)throw new InvalidOperationException("Skin composite review draw/input was not retained");
                }
                skinCompositeRecorded=states.Count==3;
            } finally {
                if(SkinCompositeRequest==this)SkinCompositeRequest=null;CompositeBypassRequested=false;CompositeCamera=null;
                skinCompositeRestored=SkinCompositeRequest==null&&!CompositeBypassRequested&&CompositeCamera==null;report.recorded=skinCompositeRecorded;report.restored=skinCompositeRestored;report.states=states.ToArray();
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"skin-composite-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureReflectionCorrection(Func<string,IEnumerator> capture) {
            var originals=new List<FaceAlbedoOriginal>();foreach(var pair in appliedMaterials)foreach(var m in pair.Value)if(m!=null&&m.GetFloat("_Skin")>.5f){var controls=new float[BoundaryControls.Length];for(int i=0;i<controls.Length;i++)controls[i]=m.GetFloat(BoundaryControls[i]);originals.Add(new FaceAlbedoOriginal{material=m,map=m.GetTexture("_BaseMap"),colour=m.GetColor("_BaseColor"),controls=controls});}
            if(originals.Count!=2||SkinCompositeRequest!=null)throw new InvalidOperationException("Reflection correction review needs two original-profile skins and normal composite");
            var states=new List<ReflectionCorrectionState>();var report=new ReflectionCorrectionReport{frozenHospitalTime=poseTime};string[] frames={"Hospital-Reflection-Correction-On","Hospital-Reflection-Correction-Off","Hospital-Reflection-Correction-On-Again"};
            try {
                for(int index=0;index<frames.Length;index++){
                    bool enabled=index!=1;foreach(var original in originals)original.material.SetFloat("_SkinReflectionVisibility",enabled?1:0);yield return null;yield return capture(frames[index]);
                    bool controls=true,slots=AppliedSlotsRetained();var flags=new float[originals.Count];for(int m=0;m<originals.Count;m++){var original=originals[m];flags[m]=original.material.GetFloat("_SkinReflectionVisibility");controls&=flags[m]==(enabled?1:0)&&original.material.GetTexture("_BaseMap")==original.map&&original.material.GetColor("_BaseColor")==original.colour;for(int c=0;c<BoundaryControls.Length;c++)if(BoundaryControls[c]!="_SkinReflectionVisibility")controls&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-original.controls[c])<.00001f;}
                    states.Add(new ReflectionCorrectionState{frame=frames[index],enabled=enabled,materialSlotsRetained=slots,otherControlsRetained=controls,visibilityFlags=flags});if(!slots||!controls)throw new InvalidOperationException("Reflection correction review controls changed during capture");
                }
                reflectionCorrectionRecorded=states.Count==3;
            } finally {
                reflectionCorrectionRestored=true;foreach(var original in originals){float expected=0;for(int c=0;c<BoundaryControls.Length;c++)if(BoundaryControls[c]=="_SkinReflectionVisibility")expected=original.controls[c];original.material.SetFloat("_SkinReflectionVisibility",expected);reflectionCorrectionRestored&=original.material.GetFloat("_SkinReflectionVisibility")==expected;}
                report.recorded=reflectionCorrectionRecorded;report.restored=reflectionCorrectionRestored;report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"reflection-correction-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureJointCorrection(Func<string,IEnumerator> capture) {
            var originals=new List<FaceAlbedoOriginal>();foreach(var pair in appliedMaterials)foreach(var m in pair.Value)if(m!=null&&m.GetFloat("_Skin")>.5f){var controls=new float[BoundaryControls.Length];for(int i=0;i<controls.Length;i++)controls[i]=m.GetFloat(BoundaryControls[i]);originals.Add(new FaceAlbedoOriginal{material=m,map=m.GetTexture("_BaseMap"),colour=m.GetColor("_BaseColor"),controls=controls});}
            if(originals.Count!=2||SkinCompositeRequest!=null||CompositeCamera!=null)throw new InvalidOperationException("Joint correction review needs two original-profile skins and no pending request");
            var states=new List<JointCorrectionState>();var report=new JointCorrectionReport{frozenHospitalTime=poseTime};string[] frames={"Hospital-Both-Corrections-On","Hospital-Both-Corrections-Off","Hospital-Both-Corrections-On-Again"};
            try {
                foreach(var focus in UnityEngine.Object.FindObjectsByType<ReturnFocus>(FindObjectsSortMode.None))if(focus.DepthCamera!=null&&focus.DepthCamera.enabled){if(CompositeCamera!=null)throw new InvalidOperationException("Multiple owned joint-composite cameras");CompositeCamera=focus.DepthCamera;}
                if(CompositeCamera==null)throw new InvalidOperationException("Joint correction review needs an owned camera");
                for(int index=0;index<frames.Length;index++){
                    bool enabled=index!=1;foreach(var original in originals)original.material.SetFloat("_SkinReflectionVisibility",enabled?1:0);
                    CompositeBypassRequested=!enabled;compositeDrawScheduled=false;compositeBypassScheduled=false;SkinCompositeRequest=this;
                    yield return null;yield return capture(frames[index]);
                    bool controls=true,slots=AppliedSlotsRetained();var flags=new float[originals.Count];for(int m=0;m<originals.Count;m++){var original=originals[m];flags[m]=original.material.GetFloat("_SkinReflectionVisibility");controls&=flags[m]==(enabled?1:0)&&original.material.GetTexture("_BaseMap")==original.map&&original.material.GetColor("_BaseColor")==original.colour;for(int c=0;c<BoundaryControls.Length;c++)if(BoundaryControls[c]!="_SkinReflectionVisibility")controls&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-original.controls[c])<.00001f;}
                    states.Add(new JointCorrectionState{frame=frames[index],reflectionEnabled=enabled,requestedBypass=CompositeBypassRequested,drawScheduled=compositeDrawScheduled,scheduledBypass=compositeBypassScheduled,materialSlotsRetained=slots,otherControlsRetained=controls,visibilityFlags=flags});
                    if(!compositeDrawScheduled||compositeBypassScheduled!=CompositeBypassRequested||!slots||!controls)throw new InvalidOperationException("Joint correction review draw/input was not retained");
                }
                jointCorrectionRecorded=states.Count==3;
            } finally {
                if(SkinCompositeRequest==this)SkinCompositeRequest=null;CompositeBypassRequested=false;CompositeCamera=null;
                jointCorrectionRestored=SkinCompositeRequest==null&&!CompositeBypassRequested&&CompositeCamera==null;
                foreach(var original in originals){float expected=0;for(int c=0;c<BoundaryControls.Length;c++)if(BoundaryControls[c]=="_SkinReflectionVisibility")expected=original.controls[c];original.material.SetFloat("_SkinReflectionVisibility",expected);jointCorrectionRestored&=original.material.GetFloat("_SkinReflectionVisibility")==expected;}
                report.recorded=jointCorrectionRecorded;report.restored=jointCorrectionRestored;report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"joint-correction-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureLitSpecular(Func<string,IEnumerator> capture,List<FaceAlbedoOriginal> originals,List<Material> references,Dictionary<SkinnedMeshRenderer,Material[]> slots,Dictionary<SkinnedMeshRenderer,Mesh> meshes) {
            var resource=Resources.Load<Material>("ReturnRealism/Skin-Lit-Specular-Off-v91");bool supported=resource!=null&&resource.shader!=null&&resource.shader.name=="Universal Render Pipeline/Lit"&&resource.shader.isSupported;
            bool declared=supported&&resource.IsKeywordEnabled("_SPECULAR_SETUP")&&resource.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")&&resource.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")&&resource.GetFloat("_SpecularHighlights")==0&&resource.GetFloat("_EnvironmentReflections")==0;
            if(!declared||references.Count!=2||originals.Count!=2||CompositeCamera==null||!CompositeBypassRequested)throw new InvalidOperationException("Lit specular review needs the declared OFF variant,two references and owned bypass camera");
            var highlights=new List<float>();var reflections=new List<float>();var keywords=new List<string[]>();foreach(var material in references){highlights.Add(material.GetFloat("_SpecularHighlights"));reflections.Add(material.GetFloat("_EnvironmentReflections"));keywords.Add(material.shaderKeywords);}
            var states=new List<LitSpecularState>();var report=new LitSpecularReport{frozenHospitalTime=poseTime,variantDeclared=declared,resourceSupported=supported};string[] frames={"Hospital-Lit-Specular-On","Hospital-Lit-Specular-Off","Hospital-Lit-Specular-On-Again"};
            try {
                for(int index=0;index<frames.Length;index++){
                    bool enabled=index!=1;foreach(var material in references){material.SetFloat("_SpecularHighlights",enabled?1:0);material.SetFloat("_EnvironmentReflections",enabled?1:0);if(enabled){material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");}else{material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");}}
                    compositeDrawScheduled=false;compositeBypassScheduled=false;SkinCompositeRequest=this;yield return null;yield return capture(frames[index]);
                    bool controls=true,otherKeywords=true,albedo=true,other=true,slotsMatched=true,geometry=true,sourceControls=true,facial=true;var bindings=new List<LitSpecularBinding>();
                    foreach(var pair in slots){var actual=pair.Key.sharedMaterials;slotsMatched&=actual.Length==pair.Value.Length;for(int s=0;s<actual.Length&&s<pair.Value.Length;s++)slotsMatched&=actual[s]==pair.Value[s];geometry&=pair.Key.sharedMesh==meshes[pair.Key];}foreach(var pair in initial)facial&=pair.Key!=null&&pair.Key.enabled==pair.Value;
                    for(int m=0;m<references.Count;m++){var material=references[m];var original=originals[m];controls&=material.GetFloat("_SpecularHighlights")==((enabled)?1:0)&&material.GetFloat("_EnvironmentReflections")==((enabled)?1:0)&&material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")==!enabled&&material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")==!enabled;var expected=new HashSet<string>(keywords[m]);if(enabled){expected.Remove("_SPECULARHIGHLIGHTS_OFF");expected.Remove("_ENVIRONMENTREFLECTIONS_OFF");}else{expected.Add("_SPECULARHIGHLIGHTS_OFF");expected.Add("_ENVIRONMENTREFLECTIONS_OFF");}otherKeywords&=expected.SetEquals(material.shaderKeywords);albedo&=material.GetTexture("_BaseMap")==Texture2D.whiteTexture&&material.GetColor("_BaseColor")==new Color(.55f,.36f,.24f,1);other&=ReferenceInputsMatch(original.material,material,true);sourceControls&=original.material.GetTexture("_BaseMap")==original.map&&original.material.GetColor("_BaseColor")==original.colour;for(int c=0;c<BoundaryControls.Length;c++)sourceControls&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-(BoundaryControls[c]=="_SkinReflectionVisibility"?0:original.controls[c]))<.00001f;bindings.Add(new LitSpecularBinding{material=material.name,highlights=material.GetFloat("_SpecularHighlights"),reflections=material.GetFloat("_EnvironmentReflections"),highlightsOff=material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"),reflectionsOff=material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF")});}
                    states.Add(new LitSpecularState{frame=frames[index],enabled=enabled,controlsMatched=controls,otherKeywordsRetained=otherKeywords,flatAlbedoRetained=albedo,otherInputsRetained=other,materialSlotsMatched=slotsMatched,geometryRetained=geometry,sourceControlsRetained=sourceControls,facialSurfacesRetained=facial,requestedBypass=CompositeBypassRequested,drawScheduled=compositeDrawScheduled,scheduledBypass=compositeBypassScheduled,bindings=bindings.ToArray()});
                    if(!controls||!otherKeywords||!albedo||!other||!slotsMatched||!geometry||!sourceControls||!facial||!CompositeBypassRequested||!compositeDrawScheduled||!compositeBypassScheduled)throw new InvalidOperationException("Lit specular review draw/input was not retained");
                }
                litSpecularRecorded=states.Count==3;
            } finally {
                litSpecularRestored=true;for(int m=0;m<references.Count;m++){references[m].SetFloat("_SpecularHighlights",highlights[m]);references[m].SetFloat("_EnvironmentReflections",reflections[m]);references[m].shaderKeywords=keywords[m];litSpecularRestored&=references[m].GetFloat("_SpecularHighlights")==highlights[m]&&references[m].GetFloat("_EnvironmentReflections")==reflections[m]&&new HashSet<string>(keywords[m]).SetEquals(references[m].shaderKeywords);}
                report.recorded=litSpecularRecorded;report.restored=litSpecularRestored;report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"lit-specular-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureLitAlbedo(Func<string,IEnumerator> capture,List<FaceAlbedoOriginal> originals,List<Material> references,Dictionary<SkinnedMeshRenderer,Material[]> slots,Dictionary<SkinnedMeshRenderer,Mesh> meshes) {
            var maps=new List<Texture>();var colours=new List<Color>();foreach(var material in references){maps.Add(material.GetTexture("_BaseMap"));colours.Add(material.GetColor("_BaseColor"));}
            var states=new List<LitAlbedoState>();var flatColour=new Color(.55f,.36f,.24f,1);var report=new LitAlbedoReport{frozenHospitalTime=poseTime,flatColour=flatColour};string[] frames={"Hospital-Lit-Albedo-Original","Hospital-Lit-Albedo-Flat","Hospital-Lit-Albedo-Original-Again"};
            try {
                if(references.Count!=2||originals.Count!=2||CompositeCamera==null||!CompositeBypassRequested)throw new InvalidOperationException("Lit albedo review needs two live references and the owned bypass camera");
                foreach(var pair in slots)pair.Key.sharedMaterials=pair.Value;
                for(int index=0;index<frames.Length;index++){
                    bool flat=index==1;for(int m=0;m<references.Count;m++){references[m].SetTexture("_BaseMap",flat?Texture2D.whiteTexture:maps[m]);references[m].SetColor("_BaseColor",flat?flatColour:colours[m]);}
                    compositeDrawScheduled=false;compositeBypassScheduled=false;SkinCompositeRequest=this;yield return null;yield return capture(frames[index]);
                    bool albedo=true,other=true,slotsMatched=true,geometry=true,controls=true,facial=true;var bindings=new List<LitAlbedoBinding>();
                    foreach(var pair in slots){var actual=pair.Key.sharedMaterials;slotsMatched&=actual.Length==pair.Value.Length;for(int s=0;s<actual.Length&&s<pair.Value.Length;s++)slotsMatched&=actual[s]==pair.Value[s];geometry&=pair.Key.sharedMesh==meshes[pair.Key];}
                    foreach(var pair in initial)facial&=pair.Key!=null&&pair.Key.enabled==pair.Value;
                    for(int m=0;m<references.Count;m++){var material=references[m];var original=originals[m];albedo&=material.GetTexture("_BaseMap")==((flat)?Texture2D.whiteTexture:maps[m])&&material.GetColor("_BaseColor")==((flat)?flatColour:colours[m]);other&=ReferenceInputsMatch(original.material,material,true);controls&=original.material.GetTexture("_BaseMap")==original.map&&original.material.GetColor("_BaseColor")==original.colour;for(int c=0;c<BoundaryControls.Length;c++)controls&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-(BoundaryControls[c]=="_SkinReflectionVisibility"?0:original.controls[c]))<.00001f;bindings.Add(new LitAlbedoBinding{material=material.name,shader=material.shader.name,baseMap=material.GetTexture("_BaseMap")==null?"":material.GetTexture("_BaseMap").name,baseColour=material.GetColor("_BaseColor")});}
                    states.Add(new LitAlbedoState{frame=frames[index],flat=flat,albedoMatched=albedo,otherInputsRetained=other,materialSlotsMatched=slotsMatched,geometryRetained=geometry,sourceControlsRetained=controls,facialSurfacesRetained=facial,requestedBypass=CompositeBypassRequested,drawScheduled=compositeDrawScheduled,scheduledBypass=compositeBypassScheduled,bindings=bindings.ToArray()});
                    if(!albedo||!other||!slotsMatched||!geometry||!controls||!facial||!CompositeBypassRequested||!compositeDrawScheduled||!compositeBypassScheduled)throw new InvalidOperationException("Lit albedo review draw/input was not retained");
                    if(flat)yield return CaptureLitSpecular(capture,originals,references,slots,meshes);
                }
                litAlbedoRecorded=states.Count==3;
            } finally {
                litAlbedoRestored=true;for(int m=0;m<references.Count;m++){references[m].SetTexture("_BaseMap",maps[m]);references[m].SetColor("_BaseColor",colours[m]);litAlbedoRestored&=references[m].GetTexture("_BaseMap")==maps[m]&&references[m].GetColor("_BaseColor")==colours[m]&&ReferenceInputsMatch(originals[m].material,references[m]);}
                report.recorded=litAlbedoRecorded;report.restored=litAlbedoRestored;report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"lit-albedo-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        IEnumerator CaptureReferenceSkin(Func<string,IEnumerator> capture) {
            var resource=Resources.Load<Material>("ReturnRealism/Skin-Lit-Reference-v88");
            if(resource==null||resource.shader==null||resource.shader.name!="Universal Render Pipeline/Lit"||!resource.shader.isSupported)throw new InvalidOperationException("Standard Lit reference resource/shader unavailable");
            if(SkinCompositeRequest!=null||CompositeCamera!=null)throw new InvalidOperationException("Standard Lit review needs no pending request");
            var originals=new List<FaceAlbedoOriginal>();var standardSlots=new Dictionary<SkinnedMeshRenderer,Material[]>();var meshes=new Dictionary<SkinnedMeshRenderer,Mesh>();var referenceMaterials=new List<Material>();var bindings=new List<ReferenceSkinBinding>();var states=new List<ReferenceSkinState>();
            var report=new ReferenceSkinReport{frozenHospitalTime=poseTime,resourceShader=resource.shader.name,resourceSupported=resource.shader.isSupported};string[] frames={"Hospital-Custom-Forward","Hospital-Standard-Lit","Hospital-Custom-Forward-Again"};
            try {
                foreach(var pair in appliedMaterials){var slots=(Material[])pair.Value.Clone();meshes.Add(pair.Key,pair.Key.sharedMesh);for(int slot=0;slot<slots.Length;slot++){
                    var source=slots[slot];if(source==null||source.GetFloat("_Skin")<.5f)continue;var controls=new float[BoundaryControls.Length];for(int c=0;c<controls.Length;c++)controls[c]=source.GetFloat(BoundaryControls[c]);originals.Add(new FaceAlbedoOriginal{material=source,map=source.GetTexture("_BaseMap"),colour=source.GetColor("_BaseColor"),controls=controls});
                    var reference=new Material(resource);reference.name=source.name+" [standard Lit reference]";referenceMaterials.Add(reference);
                    foreach(string name in ReferenceTextures){reference.SetTexture(name,source.GetTexture(name));reference.SetTextureScale(name,source.GetTextureScale(name));reference.SetTextureOffset(name,source.GetTextureOffset(name));}
                    foreach(string name in new[]{"_BaseColor","_EmissionColor"})reference.SetColor(name,source.GetColor(name));foreach(string name in new[]{"_Smoothness","_BumpScale"})reference.SetFloat(name,source.GetFloat(name));reference.SetFloat("_Cull",PhysicalSkinCullOff);
                    reference.SetFloat("_WorkflowMode",0);reference.SetColor("_SpecColor",new Color(.028f,.028f,.028f,1));reference.EnableKeyword("_SPECULAR_SETUP");reference.renderQueue=source.renderQueue;
                    if(reference.GetTexture("_BumpMap")!=null)reference.EnableKeyword("_NORMALMAP");else reference.DisableKeyword("_NORMALMAP");if(reference.GetTexture("_OcclusionMap")!=null)reference.EnableKeyword("_OCCLUSIONMAP");else reference.DisableKeyword("_OCCLUSIONMAP");
                    if(reference.GetColor("_EmissionColor").maxColorComponent>0)reference.EnableKeyword("_EMISSION");else reference.DisableKeyword("_EMISSION");
                    bool inputs=ReferenceInputsMatch(source,reference);if(!inputs)throw new InvalidOperationException("Standard Lit common skin inputs did not match");
                    bindings.Add(new ReferenceSkinBinding{renderer=RendererPath(pair.Key.transform),sourceMaterial=source.name,referenceMaterial=reference.name,sourceShader=source.shader.name,referenceShader=reference.shader.name,baseMap=source.GetTexture("_BaseMap")==null?"":source.GetTexture("_BaseMap").name,normalMap=source.GetTexture("_BumpMap")==null?"":source.GetTexture("_BumpMap").name,baseColour=source.GetColor("_BaseColor"),specularF0=reference.GetColor("_SpecColor"),uvScale=source.GetTextureScale("_BaseMap"),uvOffset=source.GetTextureOffset("_BaseMap"),smoothness=source.GetFloat("_Smoothness"),bumpScale=source.GetFloat("_BumpScale"),cull=PhysicalSkinCullOff,sourceDetail=source.GetFloat("_SkinDetail"),sourceOil=source.GetFloat("_SkinOiliness"),commonInputsMatched=inputs});slots[slot]=reference;
                }standardSlots.Add(pair.Key,slots);}
                if(originals.Count!=2||referenceMaterials.Count!=2)throw new InvalidOperationException("Standard Lit reference needs exactly two skins");
                foreach(var focus in UnityEngine.Object.FindObjectsByType<ReturnFocus>(FindObjectsSortMode.None))if(focus.DepthCamera!=null&&focus.DepthCamera.enabled){if(CompositeCamera!=null)throw new InvalidOperationException("Multiple owned reference cameras");CompositeCamera=focus.DepthCamera;}
                if(CompositeCamera==null)throw new InvalidOperationException("Standard Lit reference needs an owned camera");
                foreach(var original in originals)original.material.SetFloat("_SkinReflectionVisibility",0);CompositeBypassRequested=true;
                for(int index=0;index<frames.Length;index++){
                    bool standard=index==1;foreach(var pair in appliedMaterials)pair.Key.sharedMaterials=standard?standardSlots[pair.Key]:pair.Value;
                    compositeDrawScheduled=false;compositeBypassScheduled=false;SkinCompositeRequest=this;yield return null;yield return capture(frames[index]);
                    bool slotsMatched=true,geometry=true,controls=true,inputs=true;var shaders=new List<string>();foreach(var pair in appliedMaterials){var expected=standard?standardSlots[pair.Key]:pair.Value;var actual=pair.Key.sharedMaterials;slotsMatched&=actual.Length==expected.Length;for(int s=0;s<actual.Length&&s<expected.Length;s++)slotsMatched&=actual[s]==expected[s];geometry&=pair.Key.sharedMesh==meshes[pair.Key];}
                    for(int m=0;m<originals.Count;m++){var original=originals[m];controls&=original.material.GetTexture("_BaseMap")==original.map&&original.material.GetColor("_BaseColor")==original.colour;for(int c=0;c<BoundaryControls.Length;c++)controls&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-(BoundaryControls[c]=="_SkinReflectionVisibility"?0:original.controls[c]))<.00001f;inputs&=ReferenceInputsMatch(original.material,referenceMaterials[m]);shaders.Add((standard?referenceMaterials[m]:original.material).shader.name);}
                    states.Add(new ReferenceSkinState{frame=frames[index],standardLit=standard,requestedBypass=CompositeBypassRequested,drawScheduled=compositeDrawScheduled,scheduledBypass=compositeBypassScheduled,materialSlotsMatched=slotsMatched,geometryRetained=geometry,sourceControlsRetained=controls,commonInputsMatched=inputs,skinShaders=shaders.ToArray()});
                    if(!compositeDrawScheduled||!compositeBypassScheduled||!slotsMatched||!geometry||!controls||!inputs)throw new InvalidOperationException("Standard Lit reference draw/input was not retained");
                }
                referenceSkinRecorded=states.Count==3;
                yield return CaptureLitAlbedo(capture,originals,referenceMaterials,standardSlots,meshes);
            } finally {
                foreach(var pair in appliedMaterials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;foreach(var original in originals){for(int c=0;c<BoundaryControls.Length;c++)if(BoundaryControls[c]=="_SkinReflectionVisibility")original.material.SetFloat("_SkinReflectionVisibility",original.controls[c]);}
                if(SkinCompositeRequest==this)SkinCompositeRequest=null;CompositeBypassRequested=false;CompositeCamera=null;
                referenceSkinRestored=AppliedSlotsRetained()&&SkinCompositeRequest==null&&!CompositeBypassRequested&&CompositeCamera==null;foreach(var original in originals)for(int c=0;c<BoundaryControls.Length;c++)referenceSkinRestored&=Mathf.Abs(original.material.GetFloat(BoundaryControls[c])-original.controls[c])<.00001f;
                foreach(var material in referenceMaterials)UnityEngine.Object.DestroyImmediate(material);referenceSkinResourcesReleased=true;foreach(var material in referenceMaterials)referenceSkinResourcesReleased&=material==null;
                report.recorded=referenceSkinRecorded;report.restored=referenceSkinRestored;report.resourcesReleased=referenceSkinResourcesReleased;report.referenceMaterials=referenceMaterials.Count;report.bindings=bindings.ToArray();report.states=states.ToArray();string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"reference-skin-comparison.json"),JsonUtility.ToJson(report,true));
            }
        }
        public IEnumerator Capture(Func<string,IEnumerator> capture)
        {
            if(string.IsNullOrEmpty(previewRoot))throw new InvalidOperationException("Eye isolation requires an isolated preview root");
            Collect();expectedLookup=Resources.Load<Texture2D>("ReturnRealism/SkinDFG-v70");
            var report=new Report{frozenHospitalTime=poseTime,facialCards=Describe(cards),eyeSurfaces=Describe(eyes),skinSurfaces=Describe(skin),skinReviewDefaultsZero=SkinDefaultsZero(),cardsStayedEnabled=true,expectedLookupValid=expectedLookup!=null&&expectedLookup.width==128&&expectedLookup.height==128&&expectedLookup.format==TextureFormat.RGHalf,frames=new[]{"Skin-Surface-Original","Skin-Surface-No-Detail","Skin-Surface-No-Oil","Skin-Surface-Neutral"},interpretation="Complete faces at one frozen pose: original surface, procedural detail off, oil off, both off. Reflection/DFG/visibility/other controls retained. Procedural detail couples normals and roughness; this is not a normal-map-only test. Actual original bindings and all temporary case controls recorded; no production profile change or realism acceptance."};
            report.runtimeSkin=DescribeBindings();report.originalBindingsValid=report.runtimeSkin.Length==2;
            foreach(var binding in report.runtimeSkin)report.originalBindingsValid&=binding.shader=="Return/Physical"&&binding.controlsPresent&&binding.skinFlag==1&&binding.dfgFlag==1&&binding.visibilityFlag==1&&binding.specularSetup&&binding.sameLookupAsResource&&binding.width==128&&binding.height==128&&binding.lookupFormat=="RGHalf";
            var settings=new List<SurfaceSetting>();var cases=new List<SurfaceGpuCase>();
            Active=true;
            try
            {
                if(!report.skinReviewDefaultsZero||!report.expectedLookupValid)throw new InvalidOperationException("Native DFG review needs zero original diagnostic flags and a valid expected lookup");
                report.reflectionModesApplied=ReflectionSkin(1,0,1,null,null,report.frames[0],settings);yield return CaptureSurfaceCase(report.frames[0],capture,cases);
                yield return CaptureWindowShadows(capture);
                yield return CaptureFaceBoundaries(capture);
                yield return CaptureOpaqueFaces(capture);
                yield return CaptureSkinComposite(capture);
                yield return CaptureReflectionCorrection(capture);
                yield return CaptureJointCorrection(capture);
                yield return CaptureReferenceSkin(capture);
                foreach(var pair in initial)report.cardsStayedEnabled&=pair.Key.enabled==pair.Value;
                report.reflectionModesApplied&=ReflectionSkin(1,0,1,0,null,report.frames[1],settings);yield return CaptureSurfaceCase(report.frames[1],capture,cases);
                foreach(var pair in initial)report.cardsStayedEnabled&=pair.Key.enabled==pair.Value;
                report.reflectionModesApplied&=ReflectionSkin(1,0,1,null,0,report.frames[2],settings);yield return CaptureSurfaceCase(report.frames[2],capture,cases);
                foreach(var pair in initial)report.cardsStayedEnabled&=pair.Key.enabled==pair.Value;
                report.reflectionModesApplied&=ReflectionSkin(1,0,1,0,0,report.frames[3],settings);yield return CaptureSurfaceCase(report.frames[3],capture,cases);
                foreach(var pair in initial)report.cardsStayedEnabled&=pair.Key.enabled==pair.Value;
            }
            finally {
                ReleaseReflectionEnergy();ReleaseVisibility();Restore();Active=false;
                report.statesRestored=true;foreach(var pair in initial)report.statesRestored&=pair.Key!=null&&pair.Key.enabled==pair.Value;
                report.materialsRestored=true;foreach(var pair in originalMaterials){var restored=pair.Key.sharedMaterials;report.materialsRestored&=restored.Length==pair.Value.Length;for(int slot=0;slot<restored.Length;slot++)report.materialsRestored&=restored[slot]==pair.Value[slot];}
                report.temporarySkinMaterials=clones.Count;report.skinReviewDefaultsZero&=SkinDefaultsZero();
                report.surfaceSettings=settings.ToArray();
                report.gpuCases=cases.ToArray();report.gpuReadbackCaptured=cases.Count==4;foreach(var item in cases)report.gpuReadbackCaptured&=item.captured&&item.finite&&item.resourcesReleased&&item.normalTransportMatchesProducer&&item.materialSlotsRetained;
                report.mechanicalPassed=report.gpuReadbackCaptured&&report.statesRestored&&report.materialsRestored&&report.skinReviewDefaultsZero&&report.reflectionModesApplied&&report.expectedLookupValid&&report.cardsStayedEnabled&&windowShadowsRecorded&&windowShadowsRestored&&faceBoundariesRecorded&&faceBoundariesRestored&&opaqueFacesRecorded&&opaqueFacesRestored&&skinCompositeRecorded&&skinCompositeRestored&&reflectionCorrectionRecorded&&reflectionCorrectionRestored&&jointCorrectionRecorded&&jointCorrectionRestored&&referenceSkinRecorded&&referenceSkinRestored&&referenceSkinResourcesReleased&&litAlbedoRecorded&&litAlbedoRestored&&litSpecularRecorded&&litSpecularRestored&&cards.Count==4&&eyes.Count==2&&teeth.Count==2&&skin.Count==2&&clones.Count==8&&settings.Count==8;
                string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))throw new InvalidOperationException("DFG isolation requires RETURN_EVIDENCE_DIR");
                Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"eye-surface-isolation.json"),JsonUtility.ToJson(report,true));
            }
            Debug.Log("[RETURN] Native procedural skin complete-face comparison captured; original bindings valid: "+report.originalBindingsValid+"; states restored: "+report.statesRestored);
        }
    }
}
