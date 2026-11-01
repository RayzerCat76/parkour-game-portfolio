using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Return.Redesign {
 // A real reflected camera supplies off-screen scene geometry; this is planar reflection, not ray tracing.
 public sealed class ReturnWaterReflection : MonoBehaviour {
  Camera source,mirror;Renderer surface;RenderTexture texture;Material material;ReturnSky skyEnvironment;float nextFrame;bool checkReadback,readbackDone;
  public void Initialize(Camera camera,Material water,ReturnSky sky) {
   source=camera;material=water;skyEnvironment=sky;surface=GetComponent<Renderer>();material.SetFloat("_PlanarReflectionValid",0);gameObject.layer=4;checkReadback=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-self-test")>=0;
   var g=new GameObject("Water reflection camera");g.transform.SetParent(transform,false);mirror=g.AddComponent<Camera>();mirror.enabled=false;
   var data=g.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;data.renderShadows=true;
   texture=new RenderTexture(768,432,24,RenderTextureFormat.ARGBHalf){name="RETURN water reflection",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};texture.Create();material.SetTexture("_ReflectionTex",texture);
  }
  void LateUpdate() {
   if(source==null||mirror==null)return;
   if(source.orthographic){material.SetFloat("_PlanarReflectionValid",0);return;}
   if(Time.unscaledTime<nextFrame)return;nextFrame=Time.unscaledTime+1f/24;
   if(surface!=null&&!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(source),surface.bounds)){material.SetFloat("_PlanarReflectionValid",0);return;}
   mirror.CopyFrom(source);mirror.enabled=false;mirror.cullingMask=source.cullingMask&~(1<<4);mirror.targetTexture=texture;mirror.allowMSAA=false;
   const float height=ReturnWaterSurface.BaseHeight;Matrix4x4 reflection=Matrix4x4.identity;reflection.m11=-1;reflection.m13=height*2;
   Vector3 p=source.transform.position;p.y=2*height-p.y;mirror.transform.position=p;mirror.worldToCameraMatrix=source.worldToCameraMatrix*reflection;
   Vector3 normal=mirror.worldToCameraMatrix.MultiplyVector(Vector3.up).normalized;Vector3 point=mirror.worldToCameraMatrix.MultiplyPoint(new Vector3(0,height+.025f,0));
   mirror.projectionMatrix=source.CalculateObliqueMatrix(new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(point,normal)));
   Matrix4x4 capturedProjection=GL.GetGPUProjectionMatrix(mirror.projectionMatrix,true)*mirror.worldToCameraMatrix;
   Color sky=source.backgroundColor;if(QualitySettings.activeColorSpace==ColorSpace.Linear)sky=sky.linear;
   bool previous=GL.invertCulling;
   try{GL.invertCulling=!previous;var request=new UniversalRenderPipeline.SingleCameraRequest{destination=texture};RenderPipeline.SubmitRenderRequest(mirror,request);}finally{GL.invertCulling=previous;}
   // Project water positions through the same view that produced this texture.
   // This remains registered while the source camera moves between captures.
   material.SetMatrix("_ReflectionViewProjection",capturedProjection);material.SetFloat("_PlanarReflectionValid",1);
   material.SetVector("_ReflectionUVScaleBias",new Vector4(.5f,SystemInfo.graphicsUVStartsAtTop?-.5f:.5f,.5f,.5f));
   material.SetFloat("_ReflectionPlaneY",height);
   // The clear colour is already in the renderer's active colour space.
   // A vector avoids applying the colour property's SRGB conversion again.
   material.SetVector("_ReflectionSky",new Vector4(sky.r,sky.g,sky.b,sky.a));
   // Use the actual recovered water overview before the camera flattens and enters the city.
   // A mirror below the city's solid deck cannot see unobstructed sky through that deck.
   if(checkReadback&&!readbackDone&&skyEnvironment.Recovery>=.18f&&source.fieldOfView>=40){readbackDone=true;CheckReflectionReadback(capturedProjection);CheckWaveReadback();}
  }
  [System.Serializable] sealed class SkySample {public Vector2 uv;public Vector3 direction;public Color expected,fallback,capturedSky;public float fallbackError,skyError;}
  [System.Serializable] sealed class ReadbackReport {public float verticalFieldOfView,aspect,recovery;public Vector3 cameraPosition;public bool passed;public string colourSpace,centreBoundsBlocker;public int geometryRejected;public float fallbackError,skyError;public SkySample[] samples;}
  static float ColourError(Color a,Color b)=>Mathf.Max(Mathf.Abs(a.r-b.r),Mathf.Abs(a.g-b.g),Mathf.Abs(a.b-b.b));
  Vector3 ReflectionRay(Matrix4x4 inverse,Vector2 uv) {
   float yScale=SystemInfo.graphicsUVStartsAtTop?-.5f:.5f;Vector4 world=inverse*new Vector4((uv.x-.5f)/.5f,(uv.y-.5f)/yScale,SystemInfo.usesReversedZBuffer?0:1,1);
   // An oblique far plane can give a sky point negative w. Remove the camera's
   // homogeneous translation before normalizing, so that w cannot reverse the ray.
   return (new Vector3(world.x,world.y,world.z)-mirror.transform.position*world.w).normalized;
  }
  string SkyBoundsBlocker(Vector3 direction,Renderer[] renderers) {
   Ray ray=new Ray(mirror.transform.position,direction);
   foreach(var renderer in renderers) {
    if(!renderer.enabled||!renderer.gameObject.activeInHierarchy||(mirror.cullingMask&(1<<renderer.gameObject.layer))==0)continue;
    // Conservative bounds include a small allowance for shader-driven foliage movement.
    Bounds bounds=renderer.bounds;bounds.Expand(.1f);if(bounds.IntersectRay(ray,out float distance)&&distance<=mirror.farClipPlane)return renderer.name;
   }
   return null;
  }
  void CheckReflectionReadback(Matrix4x4 capturedProjection) {
   RenderTexture previous=RenderTexture.active;RenderTexture probe=null;Texture2D pixel=null;
   try {
    int pass=material.FindPass("ReflectionSkyProbe"),capturePass=material.FindPass("ReflectionCaptureProbe");if(pass<0||capturePass<0)throw new System.InvalidOperationException("Water reflection sky probe pass missing");
    probe=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);probe.Create();
    pixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var report=new ReadbackReport{verticalFieldOfView=source.fieldOfView,aspect=source.aspect,recovery=skyEnvironment.Recovery,cameraPosition=source.transform.position,passed=true,colourSpace=QualitySettings.activeColorSpace.ToString(),samples=new SkySample[3]};Matrix4x4 inverse=capturedProjection.inverse;
    var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);var candidates=new System.Collections.Generic.List<Vector2>{new Vector2(.15f,.1f),new Vector2(.5f,.1f),new Vector2(.85f,.1f),new Vector2(.15f,.9f),new Vector2(.5f,.9f),new Vector2(.85f,.9f)};
    foreach(float y in new[]{.05f,.15f,.25f,.75f,.85f,.95f})for(int x=0;x<10;x++)candidates.Add(new Vector2(.05f+x*.1f,y));
    // Select distinct, unobstructed directions before reading any pixel colours.
    var selected=new System.Collections.Generic.List<Vector2>();var directions=new System.Collections.Generic.List<Vector3>();
    report.centreBoundsBlocker=SkyBoundsBlocker(ReflectionRay(inverse,new Vector2(.5f,.1f)),renderers);
    foreach(Vector2 uv in candidates) {
     Vector3 direction=ReflectionRay(inverse,uv);if(direction.y<.05f)continue;string blocker=SkyBoundsBlocker(direction,renderers);if(blocker!=null){report.geometryRejected++;continue;}
     bool distinct=true;foreach(Vector3 chosen in directions)if(Vector3.Dot(chosen,direction)>.985f){distinct=false;break;}if(!distinct)continue;
     selected.Add(uv);directions.Add(direction);if(selected.Count==3)break;
    }
    if(selected.Count!=3)throw new System.InvalidOperationException("Reflection probe cannot find three geometrically unobstructed sky directions");
    for(int i=0;i<3;i++) {
     Vector2 uv=selected[i];Vector3 direction=directions[i];
     Color expected=skyEnvironment.Sample(direction,skyEnvironment.Recovery);Shader.SetGlobalVector("_ReturnSkyProbeDirection",direction);Shader.SetGlobalVector("_ReturnReflectionProbeUV",new Vector4(uv.x,uv.y,0,0));
     Graphics.Blit(Texture2D.whiteTexture,probe,material,pass);RenderTexture.active=probe;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();Color fallback=pixel.GetPixel(0,0);
     Graphics.Blit(Texture2D.whiteTexture,probe,material,capturePass);RenderTexture.active=probe;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();Color captured=pixel.GetPixel(0,0);
     var sample=new SkySample{uv=uv,direction=direction,expected=expected,fallback=fallback,capturedSky=captured,fallbackError=ColourError(fallback,expected),skyError=ColourError(captured,expected)};report.samples[i]=sample;
     report.fallbackError=Mathf.Max(report.fallbackError,sample.fallbackError);report.skyError=Mathf.Max(report.skyError,sample.skyError);report.passed&=sample.fallbackError<.01f&&sample.skyError<.01f;
    }
    string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"water-reflection-readback.json"),JsonUtility.ToJson(report,true));
    string message="[RETURN] Water reflection readback "+(report.passed?"PASS":"FAIL")+": fallback error "+report.fallbackError.ToString("F6")+", captured sky error "+report.skyError.ToString("F6");if(report.passed)Debug.Log(message);else Debug.LogError(message);
   } finally {RenderTexture.active=previous;if(probe!=null){probe.Release();DestroyImmediate(probe);}if(pixel!=null)DestroyImmediate(pixel);}
  }
  [System.Serializable] sealed class WaveSample {public Vector2 point;public float expectedHeight,renderedHeight,heightError,gradientError;}
  [System.Serializable] sealed class WaveReport {public bool passed;public float time,leafClearance;public WaveSample[] samples;}
  void CheckWaveReadback() {
   RenderTexture previous=RenderTexture.active;RenderTexture probe=null;Texture2D pixel=null;
   try {
    int pass=material.FindPass("WaveFieldProbe");if(pass<0)throw new System.InvalidOperationException("Water wave field probe pass missing");
    probe=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);probe.Create();pixel=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
    float t=Shader.GetGlobalFloat("_ReturnWaterTime");Vector4 leaf=Shader.GetGlobalVector("_ReturnLeafContactPosition");var points=new[]{new Vector2(leaf.x,leaf.z),new Vector2(4,26),new Vector2(-57,120)};var samples=new WaveSample[points.Length];bool passed=true;
    for(int i=0;i<points.Length;i++) {
     Vector2 p=points[i];material.SetVector("_ReturnWaveProbePoint",new Vector4(p.x,p.y,t,0));Graphics.Blit(Texture2D.whiteTexture,probe,material,pass);RenderTexture.active=probe;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();Color actual=pixel.GetPixel(0,0);
     const float epsilon=.02f;float height=ReturnWaterSurface.Height(p.x,p.y,t),dx=(ReturnWaterSurface.Height(p.x+epsilon,p.y,t)-ReturnWaterSurface.Height(p.x-epsilon,p.y,t))/(2*epsilon),dz=(ReturnWaterSurface.Height(p.x,p.y+epsilon,t)-ReturnWaterSurface.Height(p.x,p.y-epsilon,t))/(2*epsilon);
     samples[i]=new WaveSample{point=p,expectedHeight=height,renderedHeight=actual.r,heightError=Mathf.Abs(actual.r-height),gradientError=Mathf.Max(Mathf.Abs(actual.g-dx),Mathf.Abs(actual.b-dz))};passed&=samples[i].heightError<.001f&&samples[i].gradientError<.001f;
    }
    float clearance=leaf.y-ReturnWaterSurface.Height(leaf.x,leaf.z,t);passed&=clearance>=-.003f&&clearance<=.015f;var report=new WaveReport{passed=passed,time=t,leafClearance=clearance,samples=samples};string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"water-wave-readback.json"),JsonUtility.ToJson(report,true));
    string message="[RETURN] Water wave readback "+(passed?"PASS":"FAIL")+": 3 height/gradient samples, leaf centre clearance "+clearance.ToString("F6");if(passed)Debug.Log(message);else Debug.LogError(message);
   } finally {RenderTexture.active=previous;if(probe!=null){probe.Release();DestroyImmediate(probe);}if(pixel!=null)DestroyImmediate(pixel);}
  }
  void OnDestroy(){if(texture!=null){texture.Release();DestroyImmediate(texture);}}
 }
}
