using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace Return.Redesign {
 // Water reads opaque depth before transparents; explicit hospital evidence captures it later.
 public sealed class ReturnDepthCaptureFeature : ScriptableRendererFeature {
  static readonly int WaterBodyMotion=Shader.PropertyToID("_ReturnWaterBodyMotion");
  Material material;CapturePass pass;WaterDepthInput waterDepth;
  sealed class WaterDepthInput : ScriptableRenderPass {
   public WaterDepthInput(){renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Depth);}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData) {
    // Declaring this input schedules URP's own opaque-depth copy before water.
    if(!frameData.Get<UniversalResourceData>().cameraDepthTexture.IsValid())throw new System.InvalidOperationException("Water opaque scene depth is invalid");
   }
  }
  sealed class CapturePass : ScriptableRenderPass {
   readonly Material material;
   sealed class PassData {public TextureHandle depth;public Material material;public ReturnFocus owner;}
   public CapturePass(Material source){material=source;renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing;ConfigureInput(ScriptableRenderPassInput.Depth);}
   public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData) {
    var camera=frameData.Get<UniversalCameraData>();var owner=ReturnFocus.DepthRequest;if(owner==null||owner.DepthCamera!=camera.camera)return;
    var resources=frameData.Get<UniversalResourceData>();if(!resources.cameraDepthTexture.IsValid())throw new System.InvalidOperationException("Current hospital camera depth texture is invalid");
    using(var builder=graph.AddRasterRenderPass<PassData>("RETURN hospital depth evidence",out var data)) {
     data.depth=resources.cameraDepthTexture;data.material=material;data.owner=owner;
     builder.UseTexture(data.depth,AccessFlags.Read);builder.SetRenderAttachment(graph.ImportTexture(owner.DepthTarget),0,AccessFlags.Write);builder.AllowPassCulling(false);
     builder.SetRenderFunc((PassData d,RasterGraphContext context)=>{d.material.SetVector("_ReturnDepthZParams",d.owner.DepthZParams);Blitter.BlitTexture(context.cmd,d.depth,new Vector4(1,1,0,0),d.material,0);d.owner.MarkDepthCaptured();});
    }
   }
  }
  public override void Create(){waterDepth=new WaterDepthInput();CoreUtils.Destroy(material);var source=Resources.Load<Material>("ReturnDepthProbe");if(source!=null){material=new Material(source);pass=new CapturePass(material);}}
  public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){var camera=data.cameraData.camera;var focus=camera.GetComponent<ReturnFocus>();if(camera.cameraType==CameraType.Game&&!camera.orthographic&&focus!=null&&focus.enabled&&Shader.GetGlobalVector(WaterBodyMotion).w>.001f)renderer.EnqueuePass(waterDepth);var request=ReturnFocus.DepthRequest;if(request!=null&&request.DepthCamera==camera){if(pass==null)throw new System.InvalidOperationException("Hospital depth capture material missing");renderer.EnqueuePass(pass);}}
  protected override void Dispose(bool disposing){CoreUtils.Destroy(material);material=null;pass=null;waterDepth=null;}
 }
}
