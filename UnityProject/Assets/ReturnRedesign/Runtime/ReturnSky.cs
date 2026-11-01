using UnityEngine;
using UnityEngine.Rendering;
namespace Return.Redesign {
 // One panorama supplies the visible sky, water fallback and environment reflections.
 public sealed class ReturnSky {
  readonly Camera camera;readonly Material material,originalSky;readonly CameraClearFlags originalClear;
  readonly AmbientMode originalAmbientMode;readonly Color originalAmbient,originalSkyAmbient,originalEquator,originalGround;
  readonly DefaultReflectionMode originalReflectionMode;readonly Texture originalReflection;readonly Cubemap environment;
  readonly float originalReflectionIntensity;readonly Texture2D panorama;readonly Color32[] pixels;readonly bool linear;
  readonly Color[][] cubeColours=new Color[6][],cubeBlend=new Color[6][];readonly Color white,pole;
  readonly Color horizon,skyAmbient;float cubeRecovery=-1;
  public float Recovery {get;private set;}
   public Color Horizon=>linear?horizon.gamma:horizon;
   public Color RecoveredHorizon {get{Color c=Recover(horizon,Recovery);return linear?c.gamma:c;}}
  public ReturnSky(Camera camera) {
   this.camera=camera;linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
   originalSky=RenderSettings.skybox;originalClear=camera.clearFlags;originalAmbientMode=RenderSettings.ambientMode;originalAmbient=RenderSettings.ambientLight;originalSkyAmbient=RenderSettings.ambientSkyColor;originalEquator=RenderSettings.ambientEquatorColor;originalGround=RenderSettings.ambientGroundColor;
   originalReflectionMode=RenderSettings.defaultReflectionMode;originalReflection=RenderSettings.customReflectionTexture;originalReflectionIntensity=RenderSettings.reflectionIntensity;
   panorama=Resources.Load<Texture2D>("ReturnSkyPanorama-v26");var source=Resources.Load<Material>("ReturnDreamSky");if(panorama==null||source==null)throw new System.InvalidOperationException("Recovered sky resources missing");
   pixels=panorama.GetPixels32();material=new Material(source);white=Working(new Color(.98f,.98f,.97f));
   Color h=Color.black,p=Color.black,s=Color.black;for(int i=0;i<48;i++){float u=(i+.5f)/48;h+=Raw(u,.505f);p+=Raw(u,1);s+=Raw(u,.72f);}horizon=h/48;pole=p/48;skyAmbient=linear?(s/48).gamma:s/48;
   environment=new Cubemap(32,TextureFormat.RGBAHalf,true){name="RETURN recovered sky reflections",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
   for(int face=0;face<6;face++){cubeColours[face]=new Color[32*32];cubeBlend[face]=new Color[32*32];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float a=(x+.5f)/16-1,b=(y+.5f)/16-1;Vector3 ray=CubeRay(face,a,b);cubeColours[face][y*32+x]=ray.y<0?Working(new Color(.19f,.17f,.14f)):Sample(ray,1);}}
  }
  static Vector3 CubeRay(int face,float x,float y) {switch(face){case 0:return new Vector3(1,-y,-x).normalized;case 1:return new Vector3(-1,-y,x).normalized;case 2:return new Vector3(x,1,y).normalized;case 3:return new Vector3(x,-1,-y).normalized;case 4:return new Vector3(x,-y,1).normalized;default:return new Vector3(-x,-y,-1).normalized;}}
  Color Working(Color c)=>linear?c.linear:c;
  Color Pixel(int x,int y){x=(x%panorama.width+panorama.width)%panorama.width;y=Mathf.Clamp(y,0,panorama.height-1);return Working(pixels[y*panorama.width+x]);}
  Color Raw(float u,float v){float x=Mathf.Repeat(u,1)*panorama.width-.5f,y=Mathf.Clamp01(v)*panorama.height-.5f;int ix=Mathf.FloorToInt(x),iy=Mathf.FloorToInt(y);return Color.Lerp(Color.Lerp(Pixel(ix,iy),Pixel(ix+1,iy),x-ix),Color.Lerp(Pixel(ix,iy+1),Pixel(ix+1,iy+1),x-ix),y-iy);}
   Color Recover(Color colour,float recovery) {
    float yellow=Mathf.Clamp01(recovery*3),red=Mathf.Clamp01(recovery*3-1),blue=Mathf.Clamp01(recovery*3-2);
    float luminance=colour.r*.2126f+colour.g*.7152f+colour.b*.0722f;
    Color gold=new Color(luminance*1.10f,luminance*1.02f,luminance*.60f,colour.a),rose=new Color(luminance*1.12f,luminance*.83f,luminance*.73f,colour.a);
    return Color.Lerp(white,Color.Lerp(Color.Lerp(gold,rose,red),colour,blue),yellow);
   }
   Color RecoveredAmbient(Color colour) {Color c=Recover(Working(colour),Recovery);return linear?c.gamma:c;}
   public Color Sample(Vector3 direction,float recovery) {
   direction.Normalize();float u=Mathf.Atan2(direction.z,direction.x)/(Mathf.PI*2)+.5f,v=Mathf.Asin(Mathf.Clamp(direction.y,-1,1))/Mathf.PI+.5f;
    Color colour=Raw(u,v);float edge=Mathf.Min(u,1-u),seam=1-Mathf.SmoothStep(0,1,edge/.03f);colour=Color.Lerp(colour,(colour+Raw(1-u,v))*.5f,seam);colour=Color.Lerp(colour,pole,Mathf.SmoothStep(0,1,(v-.94f)/.06f));return Recover(colour,Mathf.Clamp01(recovery));
  }
  public void Apply(float recovery,float indoor=0) {
   Recovery=Mathf.Clamp01(recovery);RenderSettings.skybox=material;camera.clearFlags=CameraClearFlags.Skybox;Shader.SetGlobalFloat("_ReturnStreetSky",0);
   Shader.SetGlobalTexture("_ReturnSkyTexture",panorama);Shader.SetGlobalVector("_ReturnSkyWhite",white);Shader.SetGlobalVector("_ReturnSkyPole",pole);Shader.SetGlobalFloat("_ReturnSkyRecovered",Recovery);Shader.SetGlobalFloat("_ReturnSkyEnabled",1);
   Color dream=new Color(.62f,.7f,.66f),room=new Color(.42f,.435f,.45f);RenderSettings.ambientMode=AmbientMode.Trilight;
   Color dreamSky=dream*.9f,dreamHorizon=dream*.5f,dreamGround=dream*.18f;
    float reveal=Mathf.Clamp01(Recovery*3);
    RenderSettings.ambientSkyColor=Color.Lerp(Color.Lerp(dreamSky,RecoveredAmbient(skyAmbient*.8f),reveal),room,indoor);
    RenderSettings.ambientEquatorColor=Color.Lerp(Color.Lerp(dreamHorizon,RecoveredAmbient(Horizon*.46f),reveal),room,indoor);
    RenderSettings.ambientGroundColor=Color.Lerp(Color.Lerp(dreamGround,RecoveredAmbient(new Color(.19f,.17f,.14f)),reveal),room,indoor);
   // ambientLight aliases ambientSkyColor and would overwrite this directional sky fill.
    if(cubeRecovery<0||Mathf.Abs(Recovery-cubeRecovery)>=.08f||(Recovery>=1&&cubeRecovery<1)){cubeRecovery=Recovery;for(int face=0;face<6;face++){for(int i=0;i<cubeBlend[face].Length;i++)cubeBlend[face][i]=Recover(cubeColours[face][i],Recovery);environment.SetPixels(cubeBlend[face],(CubemapFace)face);}environment.Apply(true,false);}
   RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=environment;RenderSettings.reflectionIntensity=Mathf.Lerp(originalReflectionIntensity,.8f,Recovery);
  }
  public void ApplyCarvedStreet(float recovery) {
   Recovery=Mathf.Clamp01(recovery);camera.clearFlags=CameraClearFlags.Skybox;RenderSettings.skybox=material;
   Shader.SetGlobalFloat("_ReturnStreetSky",1);Shader.SetGlobalFloat("_ReturnSkyRecovered",Recovery);
   camera.backgroundColor=RecoveredAmbient(new Color(.80f,.90f,.87f));RenderSettings.fogColor=camera.backgroundColor;
   RenderSettings.ambientMode=AmbientMode.Trilight;
   RenderSettings.ambientSkyColor=RecoveredAmbient(new Color(.78f,.83f,.84f));
   RenderSettings.ambientEquatorColor=RecoveredAmbient(new Color(.54f,.61f,.61f));
   RenderSettings.ambientGroundColor=RecoveredAmbient(new Color(.28f,.34f,.32f));
   RenderSettings.reflectionIntensity=.22f;Shader.SetGlobalFloat("_ReturnSkyEnabled",0);
  }
  public void Restore() {
   Shader.SetGlobalFloat("_ReturnStreetSky",0);
   camera.clearFlags=originalClear;RenderSettings.skybox=originalSky;RenderSettings.ambientMode=originalAmbientMode;RenderSettings.ambientLight=originalAmbient;RenderSettings.ambientSkyColor=originalSkyAmbient;RenderSettings.ambientEquatorColor=originalEquator;RenderSettings.ambientGroundColor=originalGround;
   RenderSettings.defaultReflectionMode=originalReflectionMode;RenderSettings.customReflectionTexture=originalReflection;RenderSettings.reflectionIntensity=originalReflectionIntensity;Shader.SetGlobalFloat("_ReturnSkyEnabled",0);
  }
  public void Dispose(){Restore();Object.DestroyImmediate(material);Object.DestroyImmediate(environment);}
 }
}
