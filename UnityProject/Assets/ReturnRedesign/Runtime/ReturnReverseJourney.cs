using System;
using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign {
 // Replay the authored routes in their original coordinate systems. No miniature substitute worlds.
 public sealed class ReturnReverseJourney : IDisposable {
 public const float FlattenAt=9,UniteAt=24,CityPassAt=26,DoorAt=34,IslandReturnAt=38;
  readonly ReturnArt ink=new ReturnArt();
  readonly ReturnWorld rupture,fragments;
  readonly ReturnMotor still=new ReturnMotor();
  readonly Transform backdrop;
  readonly Renderer backdropRenderer;
   readonly MaterialPropertyBlock matteAtmosphere=new MaterialPropertyBlock();
   readonly Renderer[] apertureRenderers;
   readonly MaterialPropertyBlock apertureReveal=new MaterialPropertyBlock();
   readonly Transform volume;
   readonly ReturnRecallArchitecture architecture;
  readonly Dictionary<Transform,float> supportTops=new Dictionary<Transform,float>();
  readonly float radioX;
  public Transform Root {get;}
  public bool AtmosphereBackdropActive=>backdrop.gameObject.activeInHierarchy;
  public int RouteInkRendererCount {get{int count=0;foreach(var owner in new[]{rupture.root,fragments.root})foreach(var renderer in owner.GetComponentsInChildren<Renderer>(true)){if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;foreach(var material in renderer.sharedMaterials)if(material!=null&&material.shader.name=="Return/Ink"){count++;break;}}return count;}}
  public int RuptureSupports=>rupture.surfaces.Count;
   public int FragmentSupports=>fragments.surfaces.Count;
   public int SolidHouses=>architecture.HouseCount;
   public float HouseMinimumDepth=>architecture.MinimumDepth;
   public float PaperMaximumOpacity=>architecture.PaperMaximumOpacity;
  public float RadioX=>radioX;
 public Vector3 CityCentre=>architecture.CityCentre;
 public Vector3 CityExit=>CityCentre+Vector3.forward*17;
  public int CityBuildingCount=>architecture.CityBuildingCount;
    public Vector3 BlockOrigin(int index)=>fragments.RecallBlockPosition(index);
  public Vector3 RedOrigin=>rupture.far.TransformPoint(new Vector3(39,12,0));
  public ReturnReverseJourney(Transform parent,ReturnVolumeArt art) {
   Root=new GameObject("The routes we actually crossed · reverse journey").transform;Root.SetParent(parent,false);
    rupture=new ReturnWorld(ink);rupture.Build(Movement.Rupture,0,4);Prepare(rupture,new Vector3(-rupture.goalX,0,-12));
    apertureRenderers=rupture.door.GetComponentsInChildren<Renderer>(true);
   fragments=new ReturnWorld(ink);fragments.Build(Movement.Fragments,3,4);Prepare(fragments,new Vector3(-rupture.goalX-fragments.goalX,0,-12));
   fragments.fragment.gameObject.SetActive(false);fragments.OpenReturn(true);foreach(var renderer in fragments.root.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterial.SetFloat("_Remembered",1);radioX=fragments.root.position.x+fragments.radioX;
   // One continuous atmosphere avoids exposing a panel boundary as the view flattens.
   backdrop=ink.Shape("The remembered atmosphere flows between stages",Root,new Vector2(0,7),new Vector2(1000,200),new Color(.12f,.045f,.052f),-70);
   backdrop.localPosition=new Vector3(0,7,-10.5f);backdropRenderer=backdrop.GetComponent<Renderer>();backdropRenderer.sharedMaterial.SetFloat("_Remembered",1);
   volume=new GameObject("The same supports regain depth").transform;volume.SetParent(Root,false);
    var stone=ReturnConstructionSurface.Material(art,"Plaster",new Color(.52f,.55f,.55f));
   foreach(var world in new[]{rupture,fragments})foreach(var support in world.surfaces) {
    Vector3 p=world.root.position+new Vector3(support.x,support.y-.22f,.25f);
     var visual=ReturnDoorReunion.Bevel(art,"Remembered "+world.chapter+" support",volume,p,new Vector3(support.width,.44f,2.6f),.04f,stone);art.MetricUV(visual,2.2f);supportTops.Add(visual,p.y+.22f);
    }
    architecture=new ReturnRecallArchitecture(art,Root,rupture,fragments);
     volume.gameObject.SetActive(false);architecture.SetVolume(0,0);backdrop.gameObject.SetActive(false);rupture.SetRecallAtmosphere(0);fragments.SetRecallAtmosphere(0);Root.gameObject.SetActive(false);
  }
  void Prepare(ReturnWorld world,Vector3 position) {
   world.root.SetParent(Root,false);world.root.localPosition=position;world.player.gameObject.SetActive(false);
   if(world.collapse!=null)world.collapse.gameObject.SetActive(false);
   foreach(var renderer in world.root.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterial.SetFloat("_Remembered",1);
  }
   bool streetPrepared;
   public void ShowStreet() {
    Root.gameObject.SetActive(true);rupture.root.gameObject.SetActive(false);fragments.root.gameObject.SetActive(false);volume.gameObject.SetActive(false);backdrop.gameObject.SetActive(false);
    architecture.SetVolume(1,0);
    if(!streetPrepared){
     streetPrepared=true;foreach(var renderer in Root.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials){
      if(material==null||!material.HasProperty("_RecoveryChannel"))continue;Color c=material.color;
      material.SetFloat("_RecoveryChannel",c.r>c.g*1.3f&&c.r>c.b*1.3f?2:c.b>c.r*1.1f||c.g>c.r*1.35f?3:1);
     }
    }
    Shader.SetGlobalVector("_ReturnRecallGradient",Vector4.zero);
    Shader.SetGlobalVector("_ReturnCityRecovery",new Vector4(CityCentre.x,CityCentre.z,1,ReturnRecallArchitecture.CityRadius+.5f));
   }
   public void FrameStreetView(Vector3 cameraPosition,Vector3 travellerPosition)=>architecture.FrameStreetView(cameraPosition,travellerPosition);
   public void Step(float t,float yellow,float red,float blue) {
  Root.gameObject.SetActive(t<DoorAt+.9f);Shader.SetGlobalVector("_ReturnRecallColour",new Vector4(yellow,red,blue,1));
   Shader.SetGlobalVector("_ReturnRecallGradient",new Vector4(CameraX(t),Mathf.InverseLerp(0,radioX,CameraX(t)),28,t<FlattenAt?0:1-Mathf.SmoothStep(0,1,(t-UniteAt)/2)));
   // The atmosphere belongs to the settled side view, not the island pullback.
    float atmosphere=Mathf.SmoothStep(0,1,(t-7)/(FlattenAt-7));
    float stageReveal=Mathf.SmoothStep(0,1,(t-FlattenAt)/.6f);
   float stageBlend=Mathf.SmoothStep(0,1,(t-16.4f)/1.2f);
   backdrop.localPosition=new Vector3(CameraX(t),7,-10.5f);
   matteAtmosphere.SetColor("_Color",Color.Lerp(new Color(.12f,.045f,.052f),new Color(.018f,.018f,.018f),stageBlend));
   matteAtmosphere.SetFloat("_RecallOpacity",atmosphere);matteAtmosphere.SetFloat("_RecallBlueReveal",stageBlend);backdropRenderer.SetPropertyBlock(matteAtmosphere);
   backdrop.gameObject.SetActive(t<UniteAt&&atmosphere>.001f);
     float depth=t<UniteAt?0:Mathf.SmoothStep(.002f,1,(t-UniteAt)/2);
     volume.gameObject.SetActive(depth>.001f&&t<26);architecture.SetVolume(depth,stageReveal);
     foreach(var renderer in apertureRenderers){renderer.GetPropertyBlock(apertureReveal);apertureReveal.SetFloat("_RecallOpacity",stageReveal);renderer.SetPropertyBlock(apertureReveal);}
    foreach(var entry in supportTops){Transform support=entry.Key;support.localScale=new Vector3(1,depth,depth);Vector3 position=support.localPosition;position.y=entry.Value-.22f*depth;support.localPosition=position;}
   fragments.SetRecallBlocksVisible(t<25);
   float x=CameraX(t);
  foreach(var world in new[]{rupture,fragments}) {
   world.root.gameObject.SetActive(stageReveal>.001f&&t<DoorAt);
    world.clock=t;float localX=x-world.root.position.x;still.Reset(new Vector2(localX,0));
    // Preserve the original stage's parallax and painted atmosphere while retracing it.
    world.Pose(still,new Vector2(localX,7),false,1,framing:11,suspended:true);
    world.player.gameObject.SetActive(false);
    float throughFire=1-stageBlend;
    world.SetRecallAtmosphere(stageReveal*(t>=UniteAt?0:world==rupture?throughFire:1-throughFire));
   }
  }
  public float CameraX(float t) {
   if(t<FlattenAt)return 0;
   if(t<17)return Mathf.Lerp(0,-rupture.goalX+6,Mathf.SmoothStep(0,1,(t-FlattenAt)/8));
   return Mathf.Lerp(-rupture.goalX+6,radioX,Mathf.SmoothStep(0,1,(t-17)/7));
  }
  public float CityRecoveryProgress(float t) {
   if(t<=CityPassAt)return 0;if(t>=CityPassAt+3.5f)return 1;
   Vector3 east=CityCentre+new Vector3(22,11,0),crossroads=CityCentre+new Vector3(0,4.4f,0);
   Vector3 position=CityPassPoint(east,crossroads,CityExit+new Vector3(0,1.68f,-5),t-CityPassAt);
   return Mathf.InverseLerp(east.x,crossroads.x,position.x);
  }
   static Vector3 CityPassPoint(Vector3 start,Vector3 corner,Vector3 end,float elapsed) {
    const float first=3.5f;float second=DoorAt-CityPassAt-first;
    Vector3 before=(corner-start)/first,after=(end-corner)/second,tangent=(end-start)/(first+second);
    // Keep each axis monotone while sharing one velocity through the crossroads.
    for(int axis=0;axis<3;axis++)tangent[axis]=before[axis]*after[axis]<=0?0:Mathf.Sign(tangent[axis])*Mathf.Min(Mathf.Abs(tangent[axis]),3*Mathf.Min(Mathf.Abs(before[axis]),Mathf.Abs(after[axis])));
    bool approaching=elapsed<first;float duration=approaching?first:second,u=Mathf.Clamp01((approaching?elapsed:elapsed-first)/duration),u2=u*u,u3=u2*u;
    Vector3 a=approaching?start:corner,b=approaching?corner:end,va=approaching?Vector3.zero:tangent,vb=approaching?tangent:Vector3.zero;
    return (2*u3-3*u2+1)*a+(u3-2*u2+u)*duration*va+(-2*u3+3*u2)*b+(u3-u2)*duration*vb;
   }
  public void CameraPose(float t,Vector3 start,Vector3 startTarget,float startFieldOfView,Vector3 colourBundle,Camera camera,out Vector3 position,out Vector3 target) {
   camera.ResetProjectionMatrix();camera.orthographic=false;camera.farClipPlane=650;
   // Remain in front of the earlier routes while the view turns toward them.
   Vector3 wide=new Vector3(18,14,-30),wideTarget=new Vector3(0,1.4f,14);
   if(t<5) {float u=Mathf.SmoothStep(0,1,t/5);position=Vector3.Lerp(start,wide,u);target=Vector3.Lerp(startTarget,wideTarget,u);camera.fieldOfView=Mathf.Lerp(startFieldOfView,53,u);return;}
   if(t<FlattenAt) {
    float u=Mathf.SmoothStep(0,1,(t-5)/4);position=Vector3.Lerp(wide,new Vector3(0,7,-132),u);target=Vector3.Lerp(wideTarget,new Vector3(0,7,-12),u);
    // A dolly zoom tends continuously to the side-view lens. Switch only at matching plane scale.
    float distance=Vector3.Distance(position,target),height=Mathf.Lerp(Mathf.Tan(53*Mathf.Deg2Rad*.5f)*Vector3.Distance(wide,wideTarget),11,u);
    camera.fieldOfView=2*Mathf.Atan(height/distance)*Mathf.Rad2Deg;return;
   }
   if(t<UniteAt) {camera.orthographic=true;camera.orthographicSize=11;position=new Vector3(CameraX(t),7,-132);target=position+Vector3.forward*120;return;}
   Vector3 from=new Vector3(radioX,7,-132),fromTarget=new Vector3(radioX,7,-12);
   Vector3 east=CityCentre+new Vector3(22,11,0),crossroads=CityCentre+new Vector3(0,4.4f,0),centreTarget=CityCentre+Vector3.up*1.6f,northTarget=centreTarget+Vector3.forward*7;
   if(t<CityPassAt) {float u=Mathf.SmoothStep(0,1,(t-UniteAt)/(CityPassAt-UniteAt));position=Vector3.Lerp(from,east,u);target=Vector3.Lerp(fromTarget,centreTarget,u);float height=Mathf.Lerp(11,Mathf.Tan(53*Mathf.Deg2Rad*.5f)*Vector3.Distance(east,centreTarget),u);camera.fieldOfView=2*Mathf.Atan(height/Vector3.Distance(position,target))*Mathf.Rad2Deg;return;}
  camera.fieldOfView=53;
   if(t>=DoorAt){
    // Follow the rising colours into sky before retiring the recalled city, then settle onto the island.
    Vector3 skyOffset=new Vector3(0,-5,-3);
    float rise=Mathf.SmoothStep(0,1,(t-DoorAt)/.9f),settle=Mathf.SmoothStep(0,1,(t-35.2f)/(IslandReturnAt-35.2f));
    Vector3 offset=t<35.2f?Vector3.Lerp(new Vector3(0,.08f,-5),skyOffset,rise):Vector3.Lerp(skyOffset,new Vector3(3,1.3f,-7),settle);
    position=colourBundle+offset;target=colourBundle;return;
   }
   position=CityPassPoint(east,crossroads,CityExit+new Vector3(0,1.68f,-5),t-CityPassAt);
   target=CityPassPoint(centreTarget,northTarget,CityExit+Vector3.up*1.6f,t-CityPassAt);

  }
  public void Dispose(){if(Root!=null)UnityEngine.Object.DestroyImmediate(Root.gameObject);ink.Dispose();}
 }
}
