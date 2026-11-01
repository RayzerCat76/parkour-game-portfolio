using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Return.Redesign {
 // URP focus uses a camera volume rather than the built-in renderer's OnRenderImage.
 public sealed class ReturnFocus : MonoBehaviour {
  public float blur,warmth,white;
  Volume volume;VolumeProfile profile;DepthOfField depth;ColorAdjustments colour;Camera depthCamera;bool depthCheckStarted;
  public static ReturnFocus DepthRequest {get;private set;}
  public static ReturnFocus SkinRequest {get;private set;}
  public RTHandle SkinTarget {get;private set;}
  RenderTexture skinCapture;bool skinCaptured;
  string skinSourceFormat,skinDiffuseFormat,skinCompositeFormat;
  public void RecordSkinFormats(string source,string diffuse,string composite){skinSourceFormat=source;skinDiffuseFormat=diffuse;skinCompositeFormat=composite;}
  public void MarkSkinCaptured(){skinCaptured=true;if(SkinRequest==this)SkinRequest=null;}
  public Camera DepthCamera=>depthCamera;
  public RTHandle DepthTarget {get;private set;}
  RenderTexture depthCapture;bool depthCaptured;
  public Vector4 DepthZParams {get{float near=depthCamera.nearClipPlane,far=depthCamera.farClipPlane,ratio=far/near;return SystemInfo.usesReversedZBuffer?new Vector4(ratio-1,1,(ratio-1)/far,1/far):new Vector4(1-ratio,ratio,(1-ratio)/far,ratio/far);}}
  public void MarkDepthCaptured(){depthCaptured=true;if(DepthRequest==this)DepthRequest=null;}
  public System.Action RenderForReadback {get;set;}
  public void Initialize(Camera camera) {
   depthCamera=camera;
   var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.requiresDepthTexture=true;
   profile=ScriptableObject.CreateInstance<VolumeProfile>();volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.sharedProfile=profile;
   depth=profile.Add<DepthOfField>(true);depth.mode.Override(DepthOfFieldMode.Gaussian);depth.highQualitySampling.Override(true);depth.gaussianMaxRadius.Override(1.4f);
   var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.12f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.6f);
   colour=profile.Add<ColorAdjustments>(true);var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.11f);vignette.smoothness.Override(.72f);
  }
  void LateUpdate(){Apply();}
  public void Apply() {
   if(volume==null)return;volume.enabled=enabled;
   depth.active=blur>.03f;depth.gaussianStart.Override(Mathf.Lerp(30,.12f,Mathf.Clamp01(blur/9)));depth.gaussianEnd.Override(Mathf.Lerp(34,1.1f,Mathf.Clamp01(blur/9)));
   colour.postExposure.Override(white);
   colour.colorFilter.Override(Color.Lerp(Color.white,new Color(1,.97f,.91f),warmth));
  }
  [System.Serializable] sealed class DepthSample {public Vector2 uv;public float raw,eyeMetres;}
  [System.Serializable] sealed class DepthReport {public bool passed;public float near,far,minimum,maximum;public bool reversedZ;public DepthSample[] samples;}
  public void CheckHospitalDepth() {
   if(depthCheckStarted||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-self-test")<0)return;depthCheckStarted=true;
   CheckSkinRendering();
   depthCapture=new RenderTexture(3,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name="RETURN hospital depth evidence"};depthCapture.Create();DepthTarget=RTHandles.Alloc(depthCapture);DepthRequest=this;StartCoroutine(CheckDepthAfterRendering());
  }
  System.Collections.IEnumerator CheckDepthAfterRendering() {
   yield return new WaitForEndOfFrame();RenderTexture previous=RenderTexture.active;Texture2D pixel=null;
   try {
     if(!depthCaptured)RenderForReadback?.Invoke();
     if(!depthCaptured)throw new System.InvalidOperationException("Hospital depth render pass did not execute");
    float near=depthCamera.nearClipPlane,far=depthCamera.farClipPlane;bool reversed=SystemInfo.usesReversedZBuffer;
    pixel=new Texture2D(3,1,TextureFormat.RGBAFloat,false,true);RenderTexture.active=depthCapture;pixel.ReadPixels(new Rect(0,0,3,1),0,0);pixel.Apply();
    var report=new DepthReport{passed=true,near=near,far=far,reversedZ=reversed,minimum=float.PositiveInfinity,maximum=0,samples=new DepthSample[3]};
    for(int i=0;i<3;i++) {
     Vector2 uv=new Vector2(.5f,.25f+i*.3f);Color value=pixel.GetPixel(i,0);
     report.samples[i]=new DepthSample{uv=uv,raw=value.r,eyeMetres=value.g};report.passed&=!float.IsNaN(value.g)&&!float.IsInfinity(value.g)&&value.g>near*.9f&&value.g<12;report.minimum=Mathf.Min(report.minimum,value.g);report.maximum=Mathf.Max(report.maximum,value.g);
    }
    report.passed&=report.maximum-report.minimum>.1f;string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"hospital-depth-readback.json"),JsonUtility.ToJson(report,true));
    string message="[RETURN] Hospital depth readback "+(report.passed?"PASS":"FAIL")+": eye-depth range "+report.minimum.ToString("F4")+" to "+report.maximum.ToString("F4")+" m";if(report.passed)Debug.Log(message);else Debug.LogError(message);
   }finally{RenderTexture.active=previous;if(pixel!=null)DestroyImmediate(pixel);ReleaseDepthCapture();}
  }
  [System.Serializable] sealed class SkinReport {public bool passed;public string sourceFormat,diffuseFormat,compositeFormat;public int skinPixels,roomPixels;public float maximumSkinDelta,maximumRoomDelta,maximumCompositeError,maximumDiffuseLight;}
  void CheckSkinRendering() {
   skinCapture=new RenderTexture(256,144,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear){name="RETURN skin rendering evidence"};skinCapture.Create();SkinTarget=RTHandles.Alloc(skinCapture);SkinRequest=this;StartCoroutine(CheckSkinAfterRendering());
  }
  System.Collections.IEnumerator CheckSkinAfterRendering() {
   yield return new WaitForEndOfFrame();RenderTexture previous=RenderTexture.active;Texture2D pixels=null;
   try {
     if(!skinCaptured)RenderForReadback?.Invoke();
     if(!skinCaptured)throw new System.InvalidOperationException("Skin diffusion render pass did not execute");
    pixels=new Texture2D(256,144,TextureFormat.RGBAFloat,false,true);RenderTexture.active=skinCapture;pixels.ReadPixels(new Rect(0,0,256,144),0,0);pixels.Apply();var report=new SkinReport{passed=true,sourceFormat=skinSourceFormat,diffuseFormat=skinDiffuseFormat,compositeFormat=skinCompositeFormat};
    foreach(Color value in pixels.GetPixels()) {
     report.passed&=!float.IsNaN(value.r)&&!float.IsInfinity(value.r)&&!float.IsNaN(value.b)&&!float.IsInfinity(value.b);
     if(value.g>.5f){report.skinPixels++;report.maximumSkinDelta=Mathf.Max(report.maximumSkinDelta,value.r);report.maximumDiffuseLight=Mathf.Max(report.maximumDiffuseLight,value.a);}
     else{report.roomPixels++;report.maximumRoomDelta=Mathf.Max(report.maximumRoomDelta,value.r);}
     report.maximumCompositeError=Mathf.Max(report.maximumCompositeError,value.b);
    }
    // This bound allows HDR half-float quantisation; it is not a visual-quality threshold.
    report.passed&=report.skinPixels>20&&report.roomPixels>1000&&report.maximumSkinDelta>.00001f&&report.maximumDiffuseLight>.001f&&report.maximumRoomDelta<.001f&&report.maximumCompositeError<.01f;
    string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"skin-diffusion-readback.json"),JsonUtility.ToJson(report,true));
    string message="[RETURN] Skin diffusion readback "+(report.passed?"PASS":"FAIL")+": skin pixels "+report.skinPixels+", skin delta "+report.maximumSkinDelta.ToString("F6")+", room delta "+report.maximumRoomDelta.ToString("F6")+", composite error "+report.maximumCompositeError.ToString("F6");if(report.passed)Debug.Log(message);else Debug.LogError(message);
   }finally{RenderTexture.active=previous;if(pixels!=null)DestroyImmediate(pixels);ReleaseSkinCapture();}
  }
  void ReleaseSkinCapture(){if(SkinRequest==this)SkinRequest=null;if(SkinTarget!=null){SkinTarget.Release();SkinTarget=null;}if(skinCapture!=null){skinCapture.Release();DestroyImmediate(skinCapture);skinCapture=null;}}
  void ReleaseDepthCapture(){if(DepthRequest==this)DepthRequest=null;if(DepthTarget!=null){DepthTarget.Release();DepthTarget=null;}if(depthCapture!=null){depthCapture.Release();DestroyImmediate(depthCapture);depthCapture=null;}}
  void OnDisable(){if(volume!=null)volume.enabled=false;}
  void OnDestroy(){ReleaseSkinCapture();ReleaseDepthCapture();if(profile!=null){foreach(var c in profile.components)Destroy(c);Destroy(profile);}}
 }
}
