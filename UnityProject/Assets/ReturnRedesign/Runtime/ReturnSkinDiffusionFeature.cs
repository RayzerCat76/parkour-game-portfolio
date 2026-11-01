using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Return.Redesign {
  // Skin-only diffusion and short-range visibility of hospital probe reflections.
 public sealed class ReturnSkinDiffusionFeature : ScriptableRendererFeature {
  public static bool DriftReviewTracking,DriftReviewBypass;
  public static int DriftReviewFrames {get;private set;}
  public static Camera DriftDrawCamera;
  public static Mesh DriftDrawMesh;
  public static Renderer DriftDrawRenderer;
  public static Material DriftDrawMaterial;
  public static Matrix4x4 DriftDrawMatrix;
  public static bool DriftDrawEnabled,DriftDrawUseBuiltin,DriftDrawUseRenderer,DriftDrawUseList,DriftDrawBeforePost;
  public static uint DriftDrawLayerMask;
  public static int DriftDrawEventOverride=-1;
  public static int DriftDrawRecorded {get;private set;}
  public static int DriftDrawExecuted {get;private set;}
  public static int DriftDrawLastEvent {get;private set;}
  public static bool DriftDrawLastPostEnabled {get;private set;}
  DriftDrawPass driftDrawPass;
  sealed class DriftDrawPass : ScriptableRenderPass {
   sealed class DrawData {public Mesh mesh;public Renderer renderer;public bool useRenderer,useList,postEnabled;public RendererListHandle list;public Material material;public Matrix4x4 model;public MaterialPropertyBlock properties;public int leafPass,markerPass,passEvent;}
   public DriftDrawPass(){renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing;requiresIntermediateTexture=true;}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData){
    var camera=frameData.Get<UniversalCameraData>();var resources=frameData.Get<UniversalResourceData>();
    if(!DriftDrawEnabled||camera.camera!=DriftDrawCamera||DriftDrawMesh==null||DriftDrawMaterial==null||!resources.activeColorTexture.IsValid())return;
    if(DriftDrawUseRenderer&&DriftDrawRenderer==null)return;
    int leafPass=DriftDrawMaterial.FindPass(DriftDrawUseBuiltin?"DriftBuiltinLeaf":"DriftForcedLeaf"),markerPass=DriftDrawMaterial.FindPass("DriftMarker");if(leafPass<0||markerPass<0)return;
    RendererListHandle list=default;
    if(DriftDrawUseList){
     var rendering=frameData.Get<UniversalRenderingData>();var light=frameData.Get<UniversalLightData>();
     var drawing=RenderingUtils.CreateDrawingSettings(new ShaderTagId("UniversalForward"),rendering,camera,light,camera.defaultOpaqueSortFlags);
     drawing.overrideMaterial=DriftDrawMaterial;drawing.overrideMaterialPassIndex=leafPass;
     list=graph.CreateRendererList(new RendererListParams(rendering.cullResults,drawing,new FilteringSettings(RenderQueueRange.all,-1,DriftDrawLayerMask)));
    }
    using(var builder=graph.AddRasterRenderPass<DrawData>("RETURN drift explicit draw control",out var data)){
     data.mesh=DriftDrawMesh;data.material=DriftDrawMaterial;data.model=DriftDrawMatrix;data.leafPass=leafPass;data.markerPass=markerPass;data.properties=new MaterialPropertyBlock();
     data.renderer=DriftDrawRenderer;data.useRenderer=DriftDrawUseRenderer;
     data.useList=DriftDrawUseList;data.list=list;if(data.useList)builder.UseRendererList(list);
     data.passEvent=(int)renderPassEvent;data.postEnabled=camera.postProcessEnabled;
     data.properties.SetMatrix("_DriftLocalToWorld",data.model);
     data.properties.SetMatrix("_DriftWorldToClip",GL.GetGPUProjectionMatrix(camera.camera.projectionMatrix,true)*camera.camera.worldToCameraMatrix);
     builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);builder.AllowPassCulling(false);
     builder.SetRenderFunc(static(DrawData draw,RasterGraphContext context)=>{
      if(draw.useList)context.cmd.DrawRendererList(draw.list);
      else if(draw.useRenderer)context.cmd.DrawRenderer(draw.renderer,draw.material,0,draw.leafPass);
      else context.cmd.DrawMesh(draw.mesh,draw.model,draw.material,0,draw.leafPass,draw.properties);
      context.cmd.DrawProcedural(Matrix4x4.identity,draw.material,draw.markerPass,MeshTopology.Triangles,3,1,null);
      DriftDrawLastEvent=draw.passEvent;DriftDrawLastPostEnabled=draw.postEnabled;DriftDrawExecuted++;
     });DriftDrawRecorded++;
    }
   }
  }
  Material material;DiffusionPass pass;
  sealed class DiffusionPass : ScriptableRenderPass {
   readonly Material material;
   static readonly int Mask=Shader.PropertyToID("_ReturnSkinOriginal"),Blurred=Shader.PropertyToID("_ReturnSkinBlurred"),Depth=Shader.PropertyToID("_ReturnSkinDepth"),Composite=Shader.PropertyToID("_ReturnSkinComposite");
   static readonly int Environment=Shader.PropertyToID("_ReturnSkinEnvironment"),Normals=Shader.PropertyToID("_ReturnSkinNormals");
   static readonly int Positions=Shader.PropertyToID("_ReturnSkinVisibilityPositions");
   static readonly int RawNormals=Shader.PropertyToID("_ReturnSkinVisibilityRawNormals");
    static readonly int Surfaces=Shader.PropertyToID("_ReturnSkinSurfacePositions");
     static readonly int AlbedoRoot=Shader.PropertyToID("_ReturnSkinAlbedoRoot");
     static readonly int CompositeReviewBypass=Shader.PropertyToID("_ReturnSkinCompositeReviewBypass");
     static readonly int ReflectionBefore=Shader.PropertyToID("_ReturnSkinReflectionBefore");
   sealed class GeometryData {public RendererListHandle list;}
     sealed class BlitData {public TextureHandle source,skin,depth,blurred,composite,environment,normals,positions,rawNormals,surfaces,albedo,sourceBefore;public Material material;public int shaderPass;public Vector4 projection,z;public ReturnFocus probe;public ReturnEyeSurfaceReview visibilityProbe,compositeReview;public bool bypassComposite;}
   public DiffusionPass(Material m){material=m;renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;ConfigureInput(ScriptableRenderPassInput.Depth);requiresIntermediateTexture=true;}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData) {
    var camera=frameData.Get<UniversalCameraData>();var r=frameData.Get<UniversalResourceData>();
    if(r.isActiveTargetBackBuffer||!r.cameraDepthTexture.IsValid())return;
     if(DriftReviewTracking)DriftReviewFrames++;
    var source=r.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.name="RETURN skin diffuse";desc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;desc.msaaSamples=graph.GetTextureDesc(r.activeDepthTexture).msaaSamples;desc.bindTextureMS=false;desc.clearBuffer=true;desc.clearColor=Color.clear;desc.filterMode=FilterMode.Bilinear;
     var skin=graph.CreateTexture(desc);
     desc.name="RETURN hospital albedo entry exit split";var albedoRoot=graph.CreateTexture(desc);
    var rendering=frameData.Get<UniversalRenderingData>();var light=frameData.Get<UniversalLightData>();
    var draw=RenderingUtils.CreateDrawingSettings(new ShaderTagId("ReturnSkinDiffuse"),rendering,camera,light,camera.defaultOpaqueSortFlags);
    var list=graph.CreateRendererList(new RendererListParams(rendering.cullResults,draw,new FilteringSettings(RenderQueueRange.opaque)));
    using(var builder=graph.AddRasterRenderPass<GeometryData>("RETURN skin diffuse lighting",out var d)) {
      d.list=list;builder.UseRendererList(list);builder.UseAllGlobalTextures(true);builder.SetRenderAttachment(skin,0,AccessFlags.Write);builder.SetRenderAttachment(albedoRoot,1,AccessFlags.Write);builder.SetRenderAttachmentDepth(r.activeDepthTexture,AccessFlags.Read);
     builder.SetRenderFunc(static(GeometryData data,RasterGraphContext context)=>context.cmd.DrawRendererList(data.list));
    }
    desc.msaaSamples=MSAASamples.None;desc.clearBuffer=false;desc.name="RETURN skin horizontal";var horizontal=graph.CreateTexture(desc);desc.name="RETURN skin vertical";var vertical=graph.CreateTexture(desc);
    float near=camera.camera.nearClipPlane,far=camera.camera.farClipPlane,ratio=far/near;
    Vector4 z=SystemInfo.usesReversedZBuffer?new Vector4(ratio-1,1,(ratio-1)/far,1/far):new Vector4(1-ratio,ratio,(1-ratio)/far,ratio/far);
    var p=camera.camera.projectionMatrix;Vector4 projection=new Vector4(Mathf.Abs(p.m00),Mathf.Abs(p.m11),0,0);
    var reflectionDesc=desc;reflectionDesc.msaaSamples=graph.GetTextureDesc(r.activeDepthTexture).msaaSamples;reflectionDesc.clearBuffer=true;reflectionDesc.clearColor=Color.clear;reflectionDesc.filterMode=FilterMode.Point;reflectionDesc.name="RETURN local skin environment";var environment=graph.CreateTexture(reflectionDesc);
    reflectionDesc.name="RETURN local skin normals";var normals=graph.CreateTexture(reflectionDesc);
    var surfaceDesc=reflectionDesc;surfaceDesc.name="RETURN actual skin ray origins";surfaceDesc.colorFormat=GraphicsFormat.R32G32B32A32_SFloat;var surfaces=graph.CreateTexture(surfaceDesc);
    var visibilityDraw=RenderingUtils.CreateDrawingSettings(new ShaderTagId("ReturnSkinVisibility"),rendering,camera,light,camera.defaultOpaqueSortFlags);
    var visibilityList=graph.CreateRendererList(new RendererListParams(rendering.cullResults,visibilityDraw,new FilteringSettings(RenderQueueRange.opaque)));
    using(var builder=graph.AddRasterRenderPass<GeometryData>("RETURN depth-tested skin reflection inputs",out var d)) {
     d.list=visibilityList;builder.UseRendererList(visibilityList);builder.UseAllGlobalTextures(true);builder.SetRenderAttachment(environment,0,AccessFlags.Write);builder.SetRenderAttachment(normals,1,AccessFlags.Write);builder.SetRenderAttachment(surfaces,2,AccessFlags.Write);builder.SetRenderAttachmentDepth(r.activeDepthTexture,AccessFlags.Read);
     builder.SetRenderFunc(static(GeometryData data,RasterGraphContext context)=>context.cmd.DrawRendererList(data.list));
    }
    var visibilityDesc=graph.GetTextureDesc(source);visibilityDesc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;visibilityDesc.msaaSamples=MSAASamples.None;visibilityDesc.bindTextureMS=false;visibilityDesc.clearBuffer=false;visibilityDesc.name="RETURN locally visible skin reflection";var visible=graph.CreateTexture(visibilityDesc);
    var sourceBeforeReflection=source;Blit(graph,"RETURN local skin reflection visibility",source,visible,skin,r.cameraDepthTexture,skin,source,4,projection,z,null,environment,normals,surfaces:surfaces);source=visible;
    var visibilityProbe=ReturnEyeSurfaceReview.VisibilityRequest;
    if(visibilityProbe!=null&&visibilityProbe.VisibilityCamera==camera.camera){
     var positionDesc=reflectionDesc;positionDesc.name="RETURN diagnostic surface positions";positionDesc.colorFormat=GraphicsFormat.R32G32B32A32_SFloat;var positions=graph.CreateTexture(positionDesc);
     positionDesc.name="RETURN diagnostic producer normals";var rawNormals=graph.CreateTexture(positionDesc);visibilityProbe.RecordNormalFormats(reflectionDesc.colorFormat.ToString(),positionDesc.colorFormat.ToString(),reflectionDesc.msaaSamples.ToString(),reflectionDesc.width,reflectionDesc.height);
     var positionDraw=RenderingUtils.CreateDrawingSettings(new ShaderTagId("ReturnSkinVisibilityPosition"),rendering,camera,light,camera.defaultOpaqueSortFlags);
     var positionList=graph.CreateRendererList(new RendererListParams(rendering.cullResults,positionDraw,new FilteringSettings(RenderQueueRange.opaque)));
     using(var builder=graph.AddRasterRenderPass<GeometryData>("RETURN diagnostic skin surface positions",out var d)) {
      d.list=positionList;builder.UseRendererList(positionList);builder.UseAllGlobalTextures(true);builder.SetRenderAttachment(positions,0,AccessFlags.Write);builder.SetRenderAttachment(rawNormals,1,AccessFlags.Write);builder.SetRenderAttachmentDepth(r.activeDepthTexture,AccessFlags.Read);
      builder.SetRenderFunc(static(GeometryData data,RasterGraphContext context)=>context.cmd.DrawRendererList(data.list));
     }
     Blit(graph,"RETURN actual visibility GPU evidence",source,graph.ImportTexture(visibilityProbe.VisibilityTarget),skin,r.cameraDepthTexture,skin,source,5,projection,z,null,environment,normals,positions,visibilityProbe,secondaryOutput:graph.ImportTexture(visibilityProbe.ReflectionBeforeTarget),surfaces:surfaces,sourceBefore:sourceBeforeReflection,thirdOutput:graph.ImportTexture(visibilityProbe.ReflectionAfterTarget),fourthOutput:graph.ImportTexture(visibilityProbe.ReflectionPayloadTarget));
     Blit(graph,"RETURN producer and transported normal evidence",source,graph.ImportTexture(visibilityProbe.NormalStoredTarget),skin,r.cameraDepthTexture,skin,source,6,projection,z,null,environment,normals,positions,visibilityProbe,rawNormals,graph.ImportTexture(visibilityProbe.NormalProducerTarget),surfaces);
    }
     Blit(graph,"RETURN horizontal skin diffusion",skin,horizontal,skin,r.cameraDepthTexture,skin,source,0,projection,z,null,albedo:albedoRoot);
     Blit(graph,"RETURN vertical skin diffusion",horizontal,vertical,skin,r.cameraDepthTexture,horizontal,source,1,projection,z,null,albedo:albedoRoot);
    var outputDesc=graph.GetTextureDesc(source);outputDesc.colorFormat=GraphicsFormat.R16G16B16A16_SFloat;outputDesc.msaaSamples=MSAASamples.None;outputDesc.bindTextureMS=false;outputDesc.clearBuffer=false;outputDesc.name="RETURN skin composite";var output=graph.CreateTexture(outputDesc);
    var compositeReview=ReturnEyeSurfaceReview.SkinCompositeRequest;if(compositeReview!=null&&compositeReview.CompositeCamera!=camera.camera)compositeReview=null;
    Blit(graph,"RETURN preserve highlights and composite",source,output,skin,r.cameraDepthTexture,vertical,source,2,projection,z,null,compositeReview:compositeReview);r.cameraColor=output;
    var probe=ReturnFocus.SkinRequest;
    if(probe!=null&&probe.DepthCamera==camera.camera){
     probe.RecordSkinFormats(graph.GetTextureDesc(source).colorFormat.ToString(),desc.colorFormat.ToString(),outputDesc.colorFormat.ToString());
     Blit(graph,"RETURN skin rendering evidence",source,graph.ImportTexture(probe.SkinTarget),skin,r.cameraDepthTexture,vertical,output,3,projection,z,probe);
    }
   }
    void Blit(RenderGraph graph,string name,TextureHandle source,TextureHandle output,TextureHandle skin,TextureHandle depth,TextureHandle blurred,TextureHandle composite,int shaderPass,Vector4 projection,Vector4 z,ReturnFocus probe,TextureHandle environment=default,TextureHandle normals=default,TextureHandle positions=default,ReturnEyeSurfaceReview visibilityProbe=null,TextureHandle rawNormals=default,TextureHandle secondaryOutput=default,TextureHandle surfaces=default,TextureHandle albedo=default,ReturnEyeSurfaceReview compositeReview=null,TextureHandle sourceBefore=default,TextureHandle thirdOutput=default,TextureHandle fourthOutput=default) {
    using(var builder=graph.AddRasterRenderPass<BlitData>(name,out var d)) {
       d.source=source;d.skin=skin;d.depth=depth;d.blurred=blurred;d.composite=composite;d.material=material;d.shaderPass=shaderPass;d.projection=projection;d.z=z;d.probe=probe;d.environment=environment;d.normals=normals;d.positions=positions;d.visibilityProbe=visibilityProbe;d.rawNormals=rawNormals;d.surfaces=surfaces;d.albedo=albedo;
       d.compositeReview=compositeReview;d.bypassComposite=compositeReview!=null&&compositeReview.CompositeBypassRequested;
       d.sourceBefore=sourceBefore;
     builder.UseTexture(source,AccessFlags.Read);if(skin!=source)builder.UseTexture(skin,AccessFlags.Read);builder.UseTexture(depth,AccessFlags.Read);if(blurred!=source&&blurred!=skin)builder.UseTexture(blurred,AccessFlags.Read);if(composite!=source&&composite!=skin&&composite!=blurred)builder.UseTexture(composite,AccessFlags.Read);
     if(environment.IsValid())builder.UseTexture(environment,AccessFlags.Read);if(normals.IsValid())builder.UseTexture(normals,AccessFlags.Read);
     if(positions.IsValid())builder.UseTexture(positions,AccessFlags.Read);
     if(rawNormals.IsValid())builder.UseTexture(rawNormals,AccessFlags.Read);
      if(surfaces.IsValid())builder.UseTexture(surfaces,AccessFlags.Read);
       if(albedo.IsValid())builder.UseTexture(albedo,AccessFlags.Read);
       if(sourceBefore.IsValid())builder.UseTexture(sourceBefore,AccessFlags.Read);
     builder.SetRenderAttachment(output,0,AccessFlags.Write);if(secondaryOutput.IsValid())builder.SetRenderAttachment(secondaryOutput,1,AccessFlags.Write);builder.AllowGlobalStateModification(true);if(probe!=null||visibilityProbe!=null||compositeReview!=null)builder.AllowPassCulling(false);
     if(thirdOutput.IsValid())builder.SetRenderAttachment(thirdOutput,2,AccessFlags.Write);if(fourthOutput.IsValid())builder.SetRenderAttachment(fourthOutput,3,AccessFlags.Write);
     builder.SetRenderFunc(static(BlitData data,RasterGraphContext context)=> {
      context.cmd.SetGlobalTexture(Mask,data.skin);context.cmd.SetGlobalTexture(Blurred,data.blurred);context.cmd.SetGlobalTexture(Depth,data.depth);context.cmd.SetGlobalTexture(Composite,data.composite);
      if(data.environment.IsValid())context.cmd.SetGlobalTexture(Environment,data.environment);if(data.normals.IsValid())context.cmd.SetGlobalTexture(Normals,data.normals);
      if(data.positions.IsValid())context.cmd.SetGlobalTexture(Positions,data.positions);
      if(data.rawNormals.IsValid())context.cmd.SetGlobalTexture(RawNormals,data.rawNormals);
       if(data.surfaces.IsValid())context.cmd.SetGlobalTexture(Surfaces,data.surfaces);
       if(data.albedo.IsValid())context.cmd.SetGlobalTexture(AlbedoRoot,data.albedo);
       if(data.sourceBefore.IsValid())context.cmd.SetGlobalTexture(ReflectionBefore,data.sourceBefore);
       data.material.SetVector("_ReturnSkinProjection",data.projection);data.material.SetVector("_ReturnSkinZParams",data.z);
       context.cmd.SetGlobalFloat(CompositeReviewBypass,data.bypassComposite?1:0);
       Blitter.BlitTexture(context.cmd,data.source,new Vector4(1,1,0,0),data.material,data.shaderPass);if(data.probe!=null)data.probe.MarkSkinCaptured();if(data.visibilityProbe!=null){if(data.shaderPass==6)data.visibilityProbe.MarkNormalTransportCaptured();else data.visibilityProbe.MarkVisibilityCaptured();}
       context.cmd.SetGlobalFloat(CompositeReviewBypass,0);if(data.compositeReview!=null)data.compositeReview.MarkSkinCompositeDraw(data.bypassComposite);
     });
    }
   }
  }
  public override void Create(){var basis=Resources.Load<Material>("ReturnSkinDiffusion");if(basis==null)return;if(material!=null)CoreUtils.Destroy(material);material=new Material(basis);pass=new DiffusionPass(material);}
  public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){
    if(DriftDrawEnabled&&data.cameraData.camera==DriftDrawCamera){if(driftDrawPass==null)driftDrawPass=new DriftDrawPass();driftDrawPass.renderPassEvent=DriftDrawEventOverride>=0?(RenderPassEvent)DriftDrawEventOverride:DriftDrawBeforePost?(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing-1):RenderPassEvent.AfterRenderingPostProcessing;renderer.EnqueuePass(driftDrawPass);}
    if(DriftReviewBypass)return;
   var camera=data.cameraData.camera;var owner=camera.GetComponent<ReturnFocus>();
   // RETURN owns this component on its game camera. Reflection/probe cameras
   // have no such owner, regardless of copied camera settings or tags.
   if(pass!=null&&data.cameraData.cameraType==CameraType.Game&&owner!=null&&owner.DepthCamera==camera)renderer.EnqueuePass(pass);
  }
  protected override void Dispose(bool disposing){if(material!=null)CoreUtils.Destroy(material);}
 }
}
