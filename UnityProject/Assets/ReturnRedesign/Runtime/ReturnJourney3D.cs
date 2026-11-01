using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Return.Redesign {
 // The last two movements occupy one continuous volume. The camera travels through the actual doorway.
 public sealed class ReturnJourney3D {
  public const float HospitalRecollectionAt=11f,HospitalEyesCloseAt=19f,HospitalCreditsAt=23f;
  public bool Active {get;private set;}
  public Movement Scene {get;private set;}=Movement.StillWater;
  public float SceneElapsed {get;private set;}
  public Vector3 Position {get;private set;}
  public Vector3 Velocity {get;private set;}
  public bool Resting {get;private set;}
  public float RestPoseAmount=>player==null?0:player.RestAmount;
  public float Colours {get;private set;}
  public float Yaw {get;private set;}
  public float NurseSpeech,MotherSpeech;
  Vector3 previousWaterLeftPalm,previousWaterRightPalm;float leftWaterContact,rightWaterContact;bool waterHandsSampled;
  Vector3 previousWaterLeftFoot,previousWaterRightFoot;public float SwimFootSpeed {get;private set;}
  public float LeafZ=>leaf==null?0:leaf.localPosition.z;
  public float LeafSpan=>leafMesh==null?0:Mathf.Max(leafMesh.bounds.size.x,leafMesh.bounds.size.z)*ReturnVolumeArt.LeafPlanScale;
  public float IslandSpan {get;private set;}
  public float LeafNoseZ=>leaf==null?0:leaf.TransformPoint(new Vector3(0,0,ReturnVolumeArt.LeafHalfLength)).z;
  public bool CanRest=>Scene==Movement.StillWater&&coloursTime<0&&!LeafBoarding&&OnLeaf(Position)&&AtLeafRestSpot(Position)&&Mathf.Abs(Position.y-LeafSurfaceHeight(Position)-player.StandingHeightOffset)<.08f&&Mathf.Abs(Velocity.y)<.1f&&new Vector2(Velocity.x,Velocity.z).sqrMagnitude<.0144f;
  public const float LeafBoardSeconds=1.25f;
  float leafBoardTime=-1;bool leafBoardCompleted;Vector3 leafBoardStart,leafBoardEnd,leafBoardOrigin,leafBoardWorldOrigin,leafBoardBrace;Quaternion leafBoardHeading;
  public bool LeafBoarding=>leafBoardTime>=0;
  public float LeafBoardingProgress=>LeafBoarding?Mathf.Clamp01(leafBoardTime/LeafBoardSeconds):leafBoardCompleted?1:-1;
  public Vector3 CharacterRootPosition=>player.root.position;
  public Quaternion CharacterRootRotation=>player.root.rotation;
  public Vector3 CharacterBodyFocus=>player.BodyFocus;
  public bool QuietTraveller=>player!=null&&player.QuietTraveller;
  public bool TravellerHandsVisible=>player!=null&&player.TravellerHandsVisible;
  public bool TravellerVolumeVisible=>player!=null&&player.TravellerVolumeVisible;
  public bool TravellerDetailsConcealed=>player!=null&&player.TravellerDetailsConcealed;
  public bool QuietLandscape=>landscape!=null&&landscape.HasVolume&&landscape.MatteSurfaces&&landscape.RecoveryBound&&leafMaterial.GetFloat("_SurfaceDetail")==0&&leafMaterial.GetFloat("_RecoveryChannel")==0&&islandMaterial.GetFloat("_SurfaceDetail")==0&&islandMaterial.GetFloat("_RecoveryChannel")>0;
  public int LandscapeMeshes=>landscape==null?0:landscape.MeshCount;
  public int LandscapeVertices=>landscape==null?0:landscape.VertexCount;
  public Vector2 BoardingHandClearance()=>player.MeasureHandClearance(LeafSurfaceHeight);
  public const float IslandWalkSpeed=1.35f;
  public bool OnIsland=>Scene==Movement.StillWater&&!Resting&&player.RestAmount==0&&Position.y>=-.25f&&Position.y<=Floor(Position,out bool leafSupport)+.035f&&!leafSupport&&ReturnVolumeArt.IslandContains(Position.x,Position.z-26);
  public bool OnLeafSurface=>Active&&Scene==Movement.StillWater&&onLeaf&&!Resting&&player.RestAmount==0&&!LeafBoarding;
  public bool CanFruit=>Scene==Movement.StillWater&&coloursTime<0&&new Vector2(Position.x-1,Position.z-26).magnitude<.65f&&Position.y>.1f&&Mathf.Abs(Position.y-Floor(Position,out _))<.05f&&Mathf.Abs(Velocity.y)<.1f&&player.CanReachFruit(fruitHangCentre,FruitRadius);
  public float FruitActionTime=>coloursTime;
  public float FruitRadius {get;private set;}
  public float FruitHandDistance=>Vector3.Distance(fruit.TransformPoint(fruitCentreOffset),player.RightPalmPosition);
  public float FruitMouthDistance=>Vector3.Distance(fruit.TransformPoint(fruitCentreOffset),player.MouthPosition);
  public const float StreetReadyAt=7.2f,StreetSpeed=1.55f;
  public bool StreetPlayable=>Scene==Movement.Reunion&&streetEntered&&SceneElapsed>=StreetReadyAt&&!openingDoor;
  public float StreetStartX=>reverse.CityCentre.x-20;
  public float StreetDoorX=>reverse.CityCentre.x+20;
  public Vector3 StreetColourChannels=>new Vector3(Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,3.2f,SceneElapsed)),Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.2f,5.2f,SceneElapsed)),Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.2f,StreetReadyAt,SceneElapsed)));
  public float FruitWhiteAlpha=>Scene==Movement.StillWater&&coloursTime>=3.15f?Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.15f,3.5f,coloursTime)):Scene==Movement.Reunion?SceneElapsed<=1?1:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,2,SceneElapsed)):0;
  public bool LightDoorOpening=>Scene==Movement.Reunion&&openingDoor;
  public bool PlainLightDoorVisible {get{if(streetDoor==null||!streetDoor.gameObject.activeInHierarchy||recoveredWorlds.gameObject.activeInHierarchy)return false;var renderer=streetDoor.Find("Light doorway opening").GetComponent<Renderer>();Color c=renderer.sharedMaterial.color;return renderer.enabled&&Mathf.Abs(c.r-c.g)<.01f&&Mathf.Abs(c.g-c.b)<.01f&&c.r>.9f;}}
  public bool CanDoor=>StreetPlayable&&Mathf.Abs(Position.x-StreetDoorX)<1.4f;
  public float DoorOpeningElapsed=>openingDoor?doorTime:-1;
  public float DoorWhiteout=>openingDoor?Mathf.SmoothStep(0,1,(doorTime-2.4f)/.6f):0;
  public float HospitalEyeClosure {get{if(Scene!=Movement.Hospital)return 1;float t=SceneElapsed;return Mathf.Max(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,4.1f,t)),Mathf.Max(HospitalBlink(t,5.4f),Mathf.Max(HospitalBlink(t,8.8f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(HospitalEyesCloseAt,22,t)))));}}
  static float HospitalBlink(float t,float at)=>t<at+.16f?Mathf.SmoothStep(0,1,Mathf.InverseLerp(at,at+.16f,t)):1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(at+.16f,at+.44f,t));
  public Vector3 HospitalEyePosition=>hospital.TransformPoint(new Vector3(0,1.12f,-.68f));
  public int HospitalCeilingPanels=>hospitalCeiling==null?0:hospitalCeiling.childCount;
  public int HospitalLightFixtures=>hospitalFixtures.Count;
  public int HospitalRenderedPeople=>hospital.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
  public bool HospitalPatientHidden=>!player.root.gameObject.activeInHierarchy;
  public bool HospitalCeilingInView=>HospitalPointInView(hospital.TransformPoint(new Vector3(0,3.25f,.32f)))&&Vector3.Dot(camera.transform.forward,hospital.up)>.82f;
  public bool HospitalLightsInView {get{foreach(Transform fixture in hospitalFixtures)if(!HospitalPointInView(fixture.position))return false;return hospitalFixtures.Count==2;}}
  public Vector3 CityCentre=>reverse.CityCentre;
  public int CityBuildingCount=>reverse==null?0:reverse.CityBuildingCount;
  bool cityOpened;
  public float CityRecovery=>Scene==Movement.Reunion?Colours:0;
  public bool CityCameraOnly=>false;
  Transform streetDoor;bool streetEntered;
  public readonly List<string> events=new List<string>();
  public int GeometryCount=>root==null?0:root.GetComponentsInChildren<MeshRenderer>(true).Length;
  public int GeometryVertexCount {get{int count=0;if(root==null)return count;foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))if(mesh.sharedMesh!=null)count+=mesh.sharedMesh.vertexCount;foreach(var mesh in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(mesh.sharedMesh!=null)count+=mesh.sharedMesh.vertexCount;return count;}}
  public bool CameraIsOrthographic=>camera.orthographic;
  public Vector3 CameraPosition=>camera.transform.position;
  public Quaternion CameraRotation=>camera.transform.rotation;
  public float CameraFieldOfView=>camera.fieldOfView;
  public float SwimPhase=>player.SwimPhase;
  public Vector4 SwimShape=>player.SwimShape;
  public Vector4 SwimPosture=>player.SwimPosture;
  public ReturnPerson3D.SwimFootReport DescribeSwimFeet()=>player.DescribeSwimFeet();
  public ReturnPerson3D.GroundStanceState DescribeStreetStance(int leg)=>player.DescribeGroundStance(leg);
  public void DescribeStreetLanding(int leg,out Vector3 raw,out Vector3 planned,out Vector3 laneShift,out Vector3 previousVelocity,out float poseDt,out int episode,out int cycle,out bool swing,out bool reversed)=>player.DescribeStreetLanding(leg,out raw,out planned,out laneShift,out previousVelocity,out poseDt,out episode,out cycle,out swing,out reversed);
  public Vector3 StreetBodyForward=>player.GroundBodyForward;
  readonly bool streetCloseup=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-street-closeup")>=0;
  [System.Serializable] public sealed class StreetGeometrySample {public float elapsed,torsoLean;public ReturnPerson3D.LegSkinReport skin;public ReturnPerson3D.GroundPoseReport pose;public Vector2 rightTwist,leftTwist;}
  public StreetGeometrySample DescribeStreetGeometry()=>new StreetGeometrySample{elapsed=SceneElapsed,torsoLean=player.TorsoForwardLean,skin=player.DescribeLegSkin(),pose=player.DescribeGroundPose(point=>0),rightTwist=player.DescribeAbsoluteLegTwist(0),leftTwist=player.DescribeAbsoluteLegTwist(1)};
  public void BeginStreetPlaytest(){Scene=Movement.Reunion;SceneElapsed=time=StreetReadyAt;coloursTime=-1;Resting=false;EnterStreet();StepStreet(0,default(ReturnInput));}
  public void BeginDreamerSurfaceReview(){Position=new Vector3(-32,-.48f,-6.5f);Velocity=Vector3.zero;Yaw=75;pitch=20;}
  public void BeginLeafPlaytest(){
   SceneElapsed=time=38.5f;PlaceLeaf(0,ReturnVolumeArt.LeafDockZ);
   boarded=leafBoardCompleted=true;leafDrifting=Resting=false;leafBoardTime=-1;leafVelocity=Vector3.zero;
   Vector3 start=new Vector3(0,0,LeafZ);start.y=Floor(start,out onLeaf);
   Position=safePosition=start;Velocity=Vector3.zero;Yaw=0;pitch=25;
  }
  public void BeginShoreHandoffReview(){Position=new Vector3(-6.3f,-.48f,26);Velocity=Vector3.zero;Yaw=90;pitch=20;}
  public ReturnDreamerSurfaceReview CreateDreamerSurfaceReview(string previewRoot,Camera view)=>new ReturnDreamerSurfaceReview(player.root,water,previewRoot,view,SwimPhase);
  public float SwimCameraDistance=>Vector3.Distance(camera.transform.position,player.BodyFocus);
  public bool RecallSideView=>false;
  public Vector3 DoorwayPosition=>doorwayPosition;
  public int RecallRuptureSupports=>reverse==null?0:reverse.RuptureSupports;
   public int RecallFragmentSupports=>reverse==null?0:reverse.FragmentSupports;
   public int RecallSolidHouses=>reverse==null?0:reverse.SolidHouses;
   public float RecallHouseMinimumDepth=>reverse==null?0:reverse.HouseMinimumDepth;
   public float RecallPaperMaximumOpacity=>reverse==null?1:reverse.PaperMaximumOpacity;
  readonly Camera camera;readonly ReturnFocus focus;readonly ReturnVolumeArt art;
  readonly RenderPipelineAsset originalPipeline,volumePipeline;
  readonly UniversalAdditionalCameraData cameraData;readonly CameraOverrideOption originalDepthOption;
  readonly AntialiasingMode originalAntialiasing;readonly AntialiasingQuality originalAntialiasingQuality;
  Transform root,leaf,fruit,bittenFruit,recoveredWorlds,hospital,doorHinge,hand,water;
  ReturnReverseJourney reverse;ReturnDoorReunion doorReunion;
  ReturnQuietLandscape landscape;
  public bool DoorColoursJoined=>doorReunion!=null&&doorReunion.Ready;
  Vector3 doorwayPosition,fruitPosition;
  ReturnPerson3D player,mother,nurse,familyVisitor;Light sun;ReturnSky sky;
  ReturnPerson3D flatSkinControl;bool reviewGroundSkin,groundSkinView;
  ReturnStanceReview stanceReview;
  [System.Serializable] public sealed class GroundSkinComparison {public string surface;public float elapsed;public ReturnPerson3D.LegSkinReport ground,flat;public ReturnPerson3D.GroundPoseReport groundPose,flatPose;}
  [System.Serializable] public sealed class GroundSkinReview {public int leafFrames,islandFrames;public GroundSkinComparison leaf,island;}
  public GroundSkinReview SkinReview {get;private set;}
  float hospitalFloorY;string hospitalFloorPart;int hospitalFloorSamples;
  System.Func<Vector3,float> hospitalGroundSurface;
  [System.Serializable] public sealed class HospitalFloorReport {public bool passed;public int samples;public float localHeight;public string part;public ReturnPerson3D.GroundPoseReport[] people;}
  ReflectionProbe hospitalProbe;
  Transform hospitalCeiling;readonly List<Transform> hospitalFixtures=new List<Transform>();
  [System.Serializable] sealed class MugPlacement {public int index;public string support;public Vector3 position;}
  [System.Serializable] sealed class MugPlacementReport {public bool passed=true;public string method="Flat cabinet upper mesh bounds, with a 34mm base footprint inside the support bounds";public float cupHeightMetres=.096f,cupDiameterMetres=.088f;public MugPlacement[] placements;}
  sealed class BedSurface {public MeshCollider collider;public Bounds bounds;public Vector3 position;public Quaternion rotation;}
  readonly List<BedSurface> hospitalBedSurfaces=new List<BedSurface>();SphereCollider hospitalVertexProbe;
  public bool HospitalGeometryControlsPassed {get;private set;}
  public int HospitalBedMeshCount=>hospitalBedSurfaces.Count;
  public int HospitalSkinVerticesChecked {get;private set;}
  Material waterMaterial,leafMaterial,islandMaterial,skyStone;
  Mesh leafMesh;Vector3[] leafShape,leafPose;Color32[] leafPins;Bounds islandViewBounds;
  readonly bool driftViewProbe=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-drift-view-probe")>=0;
  int driftViewSamples;float driftViewSampleAt;
  public bool DriftViewObservationComplete=>driftViewProbe&&driftViewSamples>=2;
  public Mesh DriftLeafMesh=>leafMesh;
  public MeshRenderer DriftLeafRenderer=>leaf.GetComponent<MeshRenderer>();
  public Renderer[] DriftSceneRenderers=>root.GetComponentsInChildren<Renderer>(false);
  public bool ReverseRoutesActive=>reverse!=null&&reverse.Root.gameObject.activeInHierarchy;
  public int ReverseInkRendererCount=>reverse==null?0:reverse.RouteInkRendererCount;
  public bool ReverseAtmosphereActive=>reverse!=null&&reverse.AtmosphereBackdropActive;
  public Matrix4x4 DriftLeafMatrix=>leaf.localToWorldMatrix;
  public bool DriftWaterVisible {get=>water.GetComponent<MeshRenderer>().enabled;set=>water.GetComponent<MeshRenderer>().enabled=value;}
  public bool DriftReflectionEnabled {get=>water.GetComponent<ReturnWaterReflection>().enabled;set=>water.GetComponent<ReturnWaterReflection>().enabled=value;}
  public Material DriftLeafMaterial {get=>leaf.GetComponent<MeshRenderer>().sharedMaterial;set=>leaf.GetComponent<MeshRenderer>().sharedMaterial=value;}
  readonly List<Material> owned=new List<Material>();
  Vector3 previousCamera,previousTarget,safePosition,fruitStart,cameraTarget;float previousFieldOfView=53;
  float time,pitch=25,coloursTime=-1,doorTime,islandWalkFraming,swimFraming,quietWalkUntil;bool boarded,leafDrifting,onLeaf,openingDoor,reunited,doorFormed,ruptureRecalled,fragmentsRecalled;
  Vector3 leafVelocity,fruitCentreOffset,fruitHangCentre;Quaternion fruitRotation;float fruitFacingStart,naturalFruitRadius;bool fruitPicked,fruitTasted;
  const float TreeBendHeight=3.2f;
  Vector3 treeBaseLocal,treeAnchor,treeBreeze,fruitRestLocalPosition,fruitStemLocal,fruitStemRoot;Quaternion fruitRestLocalRotation;
  bool originalFog;Color originalAmbient,originalFogColour,originalAmbientSky,originalAmbientEquator,originalAmbientGround;float originalFogDensity;FogMode originalFogMode;AmbientMode originalAmbientMode;
  public ReturnJourney3D(Camera c) {
   camera=c;art=new ReturnVolumeArt();focus=c.gameObject.AddComponent<ReturnFocus>();focus.Initialize(c);focus.enabled=false;
   cameraData=c.GetUniversalAdditionalCameraData();originalDepthOption=cameraData.requiresDepthOption;
   originalAntialiasing=cameraData.antialiasing;originalAntialiasingQuality=cameraData.antialiasingQuality;
   originalPipeline=QualitySettings.renderPipeline;volumePipeline=Resources.Load<RenderPipelineAsset>("ReturnVolumePipeline");if(volumePipeline==null)throw new System.InvalidOperationException("3D renderer missing");
   originalFog=RenderSettings.fog;originalAmbient=RenderSettings.ambientLight;originalFogColour=RenderSettings.fogColor;originalFogDensity=RenderSettings.fogDensity;originalFogMode=RenderSettings.fogMode;originalAmbientMode=RenderSettings.ambientMode;originalAmbientSky=RenderSettings.ambientSkyColor;originalAmbientEquator=RenderSettings.ambientEquatorColor;originalAmbientGround=RenderSettings.ambientGroundColor;
  }
  Material M(float r,float g,float b,float smooth=.25f,float glow=0)=>art.Material(new Color(r,g,b),smooth,glow);
  Transform Box(string n,Transform parent,Vector3 p,Vector3 s,Material m)=>art.Shape(n,PrimitiveType.Cube,parent,p,s,m);
  Transform Ball(string n,Transform parent,Vector3 p,Vector3 s,Material m)=>art.Shape(n,PrimitiveType.Sphere,parent,p,s,m);
  public void Begin() {
   if(sky!=null){sky.Dispose();sky=null;}
   if(root!=null){reverse?.Dispose();reverse=null;Object.DestroyImmediate(root.gameObject);art.Dispose();foreach(var m in owned)Object.DestroyImmediate(m);owned.Clear();}
   root=new GameObject("RETURN · a continuous three dimensional dream").transform;sky=new ReturnSky(camera);
   cityOpened=false;leafBoardTime=-1;leafBoardCompleted=false;quietWalkUntil=0;Shader.SetGlobalVector("_ReturnRecallGradient",Vector4.zero);Shader.SetGlobalVector("_ReturnCityRecovery",Vector4.zero);
   Active=true;Scene=Movement.StillWater;SceneElapsed=time=doorTime=0;waterHandsSampled=false;leftWaterContact=rightWaterContact=0;Colours=0;coloursTime=-1;boarded=leafDrifting=onLeaf=openingDoor=Resting=false;leafVelocity=Vector3.zero;reunited=doorFormed=ruptureRecalled=fragmentsRecalled=false;events.Clear();Yaw=0;pitch=25;Shader.SetGlobalVector("_ReturnRecoveryOrigin",new Vector4(1,0,26,0));Shader.SetGlobalFloat("_ReturnYellowRadius",-5);Shader.SetGlobalFloat("_ReturnRedRadius",-5);Shader.SetGlobalFloat("_ReturnBlueRadius",-5);
   Position=new Vector3(0,6.5f,-6.5f);Velocity=Vector3.zero;safePosition=new Vector3(0,-.48f,-6.5f);
   camera.ResetProjectionMatrix();camera.orthographic=false;camera.fieldOfView=53;camera.farClipPlane=280;camera.nearClipPlane=.08f;
    QualitySettings.renderPipeline=volumePipeline;
    cameraData.requiresDepthOption=CameraOverrideOption.On;
    cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cameraData.antialiasingQuality=AntialiasingQuality.High;
   islandWalkFraming=swimFraming=0;
   camera.backgroundColor=new Color(.98f,.98f,.97f);focus.enabled=true;focus.blur=0;focus.warmth=0;focus.white=0;
   RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0065f;RenderSettings.fogColor=camera.backgroundColor;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.62f,.7f,.66f);
   sky.Apply(0);
   var lightObject=new GameObject("Soft afternoon light");lightObject.transform.SetParent(root,false);sun=lightObject.AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.97f,.83f);sun.intensity=1.08f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.60f;sun.shadowBias=.015f;sun.shadowNormalBias=.025f;sun.GetUniversalAdditionalLightData().usePipelineSettings=false;lightObject.transform.rotation=Quaternion.Euler(48,-34,0);
   var waterSource=Resources.Load<Material>("ReturnWater");if(waterSource==null)throw new System.InvalidOperationException("Water material missing");waterMaterial=new Material(waterSource);owned.Add(waterMaterial);ReturnWaterSurface.Configure(waterMaterial);Shader.SetGlobalFloat("_ReturnWaterTime",0);
   BuildWater();
   leafMaterial=art.Material(new Color(.25f,.52f,.40f),.12f);leafMaterial.SetFloat("_SurfaceDetail",0);var vein=M(.53f,.70f,.54f,.12f);vein.SetFloat("_SurfaceDetail",0);leaf=art.Leaf(root,leafMaterial,vein);PlaceLeaf(0,0);
   leafMesh=leaf.GetComponent<MeshFilter>().sharedMesh;leafShape=leafMesh.vertices;leafPose=new Vector3[leafShape.Length];leafPins=new Color32[leafShape.Length];ReturnVolumeArt.DrapeLeaf(leaf,leafMesh,leafShape,leafPose,leafPins);
   leafMaterial.SetColor("_BackColour",new Color(.48f,.65f,.52f,.65f));leafMaterial.SetFloat("_Smoothness",.12f);leafMaterial.SetFloat("_ThinSurface",.52f);leafMaterial.SetFloat("_WindMode",2);
   islandMaterial=art.Material(new Color(.66f,.73f,.69f),.08f);islandMaterial.SetFloat("_SurfaceDetail",0);islandMaterial.SetFloat("_ShoreSoil",0);ReturnQuietLandscape.UseAuthoredRecovery(islandMaterial,"Island_Ground");var island=art.Island(root,new Vector3(0,0,26),islandMaterial);
   Bounds islandBounds=island.GetComponent<MeshRenderer>().bounds;IslandSpan=Mathf.Max(islandBounds.size.x,islandBounds.size.z);
   var bark=M(.37f,.43f,.40f,.09f);var leaves=M(.45f,.66f,.54f,.08f);bark.SetFloat("_SurfaceDetail",0);leaves.SetFloat("_SurfaceDetail",0);ReturnQuietLandscape.UseAuthoredRecovery(bark,"Tree_Bark");ReturnQuietLandscape.UseAuthoredRecovery(leaves,"Tree_Leaves");treeBaseLocal=new Vector3(-.55f,ReturnVolumeArt.IslandHeight(-.55f,.4f),26.4f);landscape=new ReturnQuietLandscape(art,root,treeBaseLocal,bark,leaves);var livingTree=landscape.Tree;EnableTreeBreeze(livingTree);
   islandViewBounds=islandBounds;foreach(var renderer in livingTree.GetComponentsInChildren<Renderer>())islandViewBounds.Encapsulate(renderer.bounds);islandViewBounds.Expand(.4f);
   fruitStart=new Vector3(1,2.28f,26);fruit=art.Model("BlueFruit",root,fruitStart);fruit.localScale=Vector3.one;fruitRotation=fruit.localRotation;
   bittenFruit=art.Model("BlueFruitBitten",root,fruitStart);bittenFruit.gameObject.SetActive(false);
   var fruitRenderers=fruit.GetComponentsInChildren<MeshRenderer>();MeshRenderer flesh=fruitRenderers[0];foreach(var r in fruitRenderers)if(r.bounds.size.x*r.bounds.size.y*r.bounds.size.z>flesh.bounds.size.x*flesh.bounds.size.y*flesh.bounds.size.z)flesh=r;
   Bounds fruitBounds=flesh.bounds;naturalFruitRadius=Mathf.Max(fruitBounds.extents.x,Mathf.Max(fruitBounds.extents.y,fruitBounds.extents.z));fruit.localScale=Vector3.one*(.06f/naturalFruitRadius);fruitBounds=flesh.bounds;fruitCentreOffset=fruit.InverseTransformPoint(fruitBounds.center);fruitHangCentre=fruitBounds.center;FruitRadius=Mathf.Max(fruitBounds.extents.x,Mathf.Max(fruitBounds.extents.y,fruitBounds.extents.z));fruitPicked=fruitTasted=false;
   fruitRestLocalPosition=fruit.localPosition;fruitRestLocalRotation=fruit.localRotation;fruitStemLocal=new Vector3(.036f,.32f,0)/.43f;fruitStemRoot=root.InverseTransformPoint(fruit.TransformPoint(fruitStemLocal));
   EnableTreeBreeze(art.FruitBough(root,new Vector3(-.49f,ReturnVolumeArt.IslandHeight(-.55f,.4f)+1.65f,26.46f),fruitStemRoot-Vector3.up*.003f,bark));
   UpdateTreeBreeze();
   player=new ReturnPerson3D(art,root,"The dreamer",M(.94f,.95f,.86f,.4f),M(.92f,.93f,.85f,.45f),M(.08f,.15f,.13f),true,quietTraveller:true);
   reviewGroundSkin=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-review-ground-skin")>=0;SkinReview=reviewGroundSkin?new GroundSkinReview():null;
   stanceReview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-review-ground-stance")>=0?new ReturnStanceReview():null;
   if(reviewGroundSkin){flatSkinControl=new ReturnPerson3D(art,root,"Flat-plane walking control",null,null,null,true);flatSkinControl.root.gameObject.SetActive(false);}
    reverse=new ReturnReverseJourney(root,art);BuildRecoveredWorlds();BuildHospital();BuildStreetDoor();streetEntered=false;recoveredWorlds.gameObject.SetActive(false);hospital.gameObject.SetActive(false);Shader.SetGlobalVector("_ReturnRecallColour",Vector4.zero);
   previousTarget=Position+Vector3.up*.85f;previousCamera=previousTarget+new Vector3(0,3.3f,-7.8f);camera.transform.position=previousCamera;camera.transform.LookAt(previousTarget);cameraTarget=previousTarget;
   player.Pose(Position,Velocity,false,true,time);events.Add("Entered three-dimensional Still Water");
  }
  void BuildStreetDoor() {
   streetDoor=new GameObject("A plain light doorway at the end of the street").transform;streetDoor.SetParent(root,false);streetDoor.position=reverse.CityCentre+Vector3.right*20;
   Material ivory=art.Material(new Color(.96f,.97f,.91f),.12f),light=art.Material(Color.white,.05f,4);
   art.Shape("Light doorway opening",PrimitiveType.Cube,streetDoor,new Vector3(0,1.45f,0),new Vector3(1.8f,2.9f,.05f),light);
   for(int side=-1;side<=1;side+=2)ReturnDoorReunion.Bevel(art,"Quiet doorway jamb",streetDoor,new Vector3(side*1.04f,1.5f,0),new Vector3(.22f,3.0f,.25f),.025f,ivory);
   ReturnDoorReunion.Bevel(art,"Quiet doorway lintel",streetDoor,new Vector3(0,3.06f,0),new Vector3(2.3f,.22f,.25f),.025f,ivory);
   streetDoor.gameObject.SetActive(false);
  }
  void EnterStreet() {
   streetEntered=true;foreach(Transform child in root)if(child!=player.root&&child!=reverse.Root&&child!=hospital&&child!=streetDoor&&child.GetComponentInChildren<Renderer>()!=null)child.gameObject.SetActive(false);
   reverse.ShowStreet();streetDoor.gameObject.SetActive(true);player.root.gameObject.SetActive(true);
   sun.color=new Color(1,.98f,.94f);sun.intensity=1.05f;sun.shadowStrength=.42f;
   Position=new Vector3(StreetStartX,player.StandingHeightOffset,reverse.CityCentre.z);Velocity=Vector3.zero;Resting=false;
  }
  void StepStreet(float dt,ReturnInput input) {
   if(!streetEntered&&SceneElapsed>=.25f)EnterStreet();
   if(!streetEntered)return;
   Vector3 channels=StreetColourChannels;Colours=(channels.x+channels.y+channels.z)/3;
   Shader.SetGlobalFloat("_ReturnYellowRadius",ColourWaveRadius(channels.x));Shader.SetGlobalFloat("_ReturnRedRadius",ColourWaveRadius(channels.y));Shader.SetGlobalFloat("_ReturnBlueRadius",ColourWaveRadius(channels.z));
   Shader.SetGlobalVector("_ReturnRecallColour",new Vector4(channels.x,channels.y,channels.z,1));reverse.ShowStreet();sky.ApplyCarvedStreet(Colours);
   if(SceneElapsed>=StreetReadyAt&&!cityOpened){cityOpened=reunited=true;events.Add("Yellow, red and blue reunited");events.Add("The four routes became city streets");}
   float before=Position.x;float move=StreetPlayable?Mathf.Clamp(input.move,-1,1):0;
   Position=new Vector3(Mathf.Clamp(before+move*StreetSpeed*dt,StreetStartX-2,StreetDoorX-.4f),player.StandingHeightOffset,reverse.CityCentre.z);
   // Short, quiet street steps; retain the supported rig's gradual turn when direction changes.
   Velocity=new Vector3(dt>0?(Position.x-before)/dt:0,0,0);player.Pose(Position,Velocity,false,false,time,facing:StreetPlayable?(float?)null:90,groundSurface:point=>0,groundStrideLimit:.72f,levelGroundWalk:true);
   if(StreetPlayable&&Position.x>StreetStartX+12&&!ruptureRecalled){ruptureRecalled=true;events.Add("Remembered the voice through the rupture");}
   if(StreetPlayable&&Position.x>StreetStartX+24&&!fragmentsRecalled){fragmentsRecalled=true;events.Add("Remembered the places behind the fragments");}
   if(CanDoor&&input.interact){openingDoor=true;doorTime=0;events.Add("Opened the doorway home");}
   if(openingDoor){
    doorTime+=dt;Velocity=Vector3.zero;focus.white=DoorWhiteout*5;if(doorTime>=2.4f)player.root.gameObject.SetActive(false);
    if(doorTime>=3.3f&&!hospital.gameObject.activeSelf)hospital.gameObject.SetActive(true);
    if(doorTime>4.2f){Scene=Movement.Hospital;SceneElapsed=0;focus.white=0;sun.enabled=false;reverse.Root.gameObject.SetActive(false);streetDoor.gameObject.SetActive(false);RenderSettings.fog=false;ApplyHospitalAmbient();events.Add("Entered Hospital / 0");events.Add("Remembered the hospital cause");}
   }
  }
  void BuildWater() {
   const int n=128;var v=new Vector3[(n+1)*(n+1)];var t=new List<int>();
   for(int z=0;z<=n;z++)for(int x=0;x<=n;x++) {
    v[z*(n+1)+x]=new Vector3((x-n/2)*4f,ReturnWaterSurface.BaseHeight,(z-n/2)*4f);
    if(x<n&&z<n){int k=z*(n+1)+x;t.AddRange(new[]{k,k+n+1,k+1,k+1,k+n+1,k+n+2});}
   }
   // Preserve the near grid used by the leaf and bank. Outer rings cover the
   // complete remembered-camera frustum without coarsening nearby waves.
   var vertices=new List<Vector3>(v);var edge=new int[4*n];
   for(int i=0;i<n;i++){edge[i]=i;edge[n+i]=i*(n+1)+n;edge[2*n+i]=n*(n+1)+n-i;edge[3*n+i]=(n-i)*(n+1);}
   foreach(float radius in new[]{320f,512f,768f,1024f}) {
    var next=new int[edge.Length];
    for(int side=0;side<4;side++)for(int i=0;i<n;i++) {
     float along=-radius+2*radius*i/n;Vector3 point;
     switch(side){case 0:point=new Vector3(along,ReturnWaterSurface.BaseHeight,-radius);break;case 1:point=new Vector3(radius,ReturnWaterSurface.BaseHeight,along);break;case 2:point=new Vector3(-along,ReturnWaterSurface.BaseHeight,radius);break;default:point=new Vector3(-radius,ReturnWaterSurface.BaseHeight,-along);break;}
     int k=side*n+i;next[k]=vertices.Count;vertices.Add(point);
    }
    for(int i=0;i<edge.Length;i++){int j=(i+1)%edge.Length;t.AddRange(new[]{edge[i],edge[j],next[i],edge[j],next[j],next[i]});}
    edge=next;
   }
   water=art.Mesh("White liquid · waves catch the light",root,vertices.ToArray(),t.ToArray(),waterMaterial);waterMaterial.color=new Color(.98f,.98f,.97f);water.gameObject.AddComponent<ReturnWaterReflection>().Initialize(camera,waterMaterial,sky);
  }
  void BuildRecoveredWorlds() {
   doorwayPosition=new Vector3(0,Mathf.Max(ReturnVolumeArt.IslandHeight(-1.13f,2.5f),ReturnVolumeArt.IslandHeight(1.13f,2.5f)),28.5f);
   doorReunion=new ReturnDoorReunion(art,root,doorwayPosition,fruitStart);recoveredWorlds=doorReunion.Root;doorHinge=doorReunion.Hinge;
  }
  void ConfigureHospitalSurfaces() {
   var copies=new Dictionary<Material,Material>();
   foreach(var renderer in hospital.GetComponentsInChildren<Renderer>(true)) {
    Material[] slots=renderer.sharedMaterials;bool changed=false;
    for(int i=0;i<slots.Length;i++) {
     Material source=slots[i];if(source==null)continue;
     bool ceiling=source.name.StartsWith("Room_Ceiling"),wall=source.name.StartsWith("Room_Wall"),diffuser=source.name.StartsWith("Room_Diffuser");
     if(!ceiling&&!wall&&!diffuser)continue;
     if(!copies.TryGetValue(source,out Material material)) {
      material=art.Copy(source);copies.Add(source,material);
      if(ceiling||wall) {
       material.color=ceiling?new Color(.86f,.86f,.83f):new Color(.81f,.8f,.77f);
       material.SetFloat("_Smoothness",ceiling?.035f:.08f);material.SetFloat("_SurfaceDetail",ceiling?.05f:.06f);
       material.SetFloat("_IndirectFilter",ceiling?1:.65f);if(ceiling)material.SetFloat("_IndirectStrength",.4f);
      }else material.SetColor("_EmissionColor",new Color(.74f,.78f,.77f));
     }
     slots[i]=material;changed=true;
    }
    if(changed)renderer.sharedMaterials=slots;
   }
  }
  void ApplyHospitalAmbient() {
   RenderSettings.ambientMode=AmbientMode.Trilight;
   RenderSettings.ambientSkyColor=new Color(.32f,.35f,.39f);RenderSettings.ambientEquatorColor=new Color(.23f,.22f,.20f);RenderSettings.ambientGroundColor=new Color(.12f,.10f,.08f);
  }
  void BuildHospitalCeiling() {
   Material surface=null;Bounds bounds=new Bounds();
   foreach(var renderer in hospital.GetComponentsInChildren<Renderer>(true)) {
    Material[] slots=renderer.sharedMaterials;
    if(slots.Length!=1||slots[0]==null||!slots[0].name.StartsWith("Room_Ceiling"))continue;
    if(surface==null){surface=art.Copy(slots[0]);bounds=renderer.bounds;}else bounds.Encapsulate(renderer.bounds);
    renderer.enabled=false;
   }
   if(surface==null)throw new System.InvalidOperationException("Hospital ceiling surface missing");
   surface.SetFloat("_CeilingTiles",1);surface.SetVector("_CeilingOrigin",new Vector4(bounds.min.x,0,bounds.min.z,0));
   // The old lighting atlas belongs to individual bevelled panel UVs.
   surface.SetFloat("_UseIndirect",0);
   surface.SetVector("_CeilingIrradiance",new Vector4(.29f,.32f,.30f,0));
   Vector3 centre=bounds.center;centre.y=bounds.max.y-.014f;
   Box("Continuous acoustic tile ceiling",hospital,hospital.InverseTransformPoint(centre),new Vector3(bounds.size.x,.028f,bounds.size.z),surface);
  }
  void BuildHospital() {
   hospital=new GameObject("The hospital ceiling above the patient").transform;hospital.SetParent(root,false);
   hospital.localPosition=new Vector3(reverse.CityCentre.x+20,0,reverse.CityCentre.z+10);
   Material tile=M(.86f,.88f,.86f,.12f),joint=M(.66f,.70f,.70f,.08f),frame=M(.68f,.73f,.75f,.4f),diffuser=M(.97f,.98f,.96f,.18f,.65f);
   Box("Ceiling backing",hospital,new Vector3(0,3.30f,0),new Vector3(8.4f,.12f,9.8f),joint);
   hospitalCeiling=new GameObject("Acoustic ceiling").transform;hospitalCeiling.SetParent(hospital,false);
   for(int x=0;x<12;x++)for(int z=0;z<14;z++)Box("Acoustic panel",hospitalCeiling,new Vector3((x-5.5f)*.68f,3.25f,(z-6.5f)*.68f),new Vector3(.667f,.055f,.667f),tile);
   hospitalFixtures.Clear();
   for(int i=0;i<2;i++) {
    Vector3 at=new Vector3(i==0?-1.10f:1.10f,3.185f,.32f);
    Box("Ceiling light frame",hospital,at,new Vector3(.72f,.10f,1.36f),frame);
    Transform panel=Box("Soft ceiling light",hospital,at-Vector3.up*.062f,new Vector3(.63f,.026f,1.25f),diffuser);hospitalFixtures.Add(panel);
    var lamp=new GameObject("Ceiling light illumination");lamp.transform.SetParent(hospital,false);lamp.transform.localPosition=at-Vector3.up*.20f;
    var source=lamp.AddComponent<Light>();source.type=LightType.Point;source.color=new Color(.94f,.97f,1);source.intensity=1.4f;source.range=7;
   }
  }
  Vector3 leafSurfaceLocalPosition;float leafSurfaceWorldY;Matrix4x4 leafSurfaceToWorld,leafSurfaceFromWorld;
  void CacheLeafSurfaceTransform(){leafSurfaceLocalPosition=leaf.localPosition;leafSurfaceWorldY=leaf.position.y;leafSurfaceToWorld=leaf.localToWorldMatrix;leafSurfaceFromWorld=leaf.worldToLocalMatrix;}
  void PlaceLeaf(float x,float z){leaf.localPosition=new Vector3(x,ReturnWaterSurface.Height(x,z,time)-ReturnVolumeArt.LeafHeight(0,0)+.005f,z);CacheLeafSurfaceTransform();}
  bool OnLeaf(Vector3 p){Vector3 q=p-leafSurfaceLocalPosition;return q.x*q.x/(2.1f*2.1f*ReturnVolumeArt.LeafPlanScale*ReturnVolumeArt.LeafPlanScale)+q.z*q.z/(2.9f*2.9f*ReturnVolumeArt.LeafPlanScale*ReturnVolumeArt.LeafPlanScale)<1;}
  bool AtLeafRestSpot(Vector3 p){Vector3 q=p-leaf.localPosition;return Mathf.Abs(q.x)<.25f&&Mathf.Abs(q.z)<.35f;}
  bool TryBeginLeafBoarding(Vector3 direction,ReturnInput input){
   if(input.jumpPressed||Mathf.Abs(Velocity.y)>.1f||Position.y>=-.25f||OnLeaf(Position)||direction.sqrMagnitude<.01f)return false;
   direction.y=0;direction.Normalize();if(!OnLeaf(Position+direction*1.6f))return false;
   float low=0,high=1.6f;for(int i=0;i<12;i++){float middle=(low+high)*.5f;if(OnLeaf(Position+direction*middle))high=middle;else low=middle;}
   Vector3 edge=Position+direction*high,inward=leaf.localPosition-edge;inward.y=0;inward.Normalize();Vector3 end=edge+inward*.35f;
   SurfaceHeight(end,out bool leafSupport);if(!leafSupport)return false;
   end.y=LeafSurfaceHeight(end)+player.StandingHeightOffset;leafBoardStart=Position;leafBoardEnd=end;leafBoardOrigin=leaf.localPosition;leafBoardWorldOrigin=leaf.position;
   Vector3 facing=end-Position;facing.y=0;leafBoardHeading=Quaternion.LookRotation(facing.normalized);Vector3 brace=edge+inward*.22f;brace.y=leaf.localPosition.y;leafBoardBrace=leaf.parent.TransformPoint(brace);
   leafBoardTime=0;leafBoardCompleted=false;Velocity=Vector3.zero;player.BeginLeafBoarding(Position);events.Add("Pulling onto the green leaf");return true;
  }
  void StepLeafBoarding(float dt){
   leafBoardTime=Mathf.Min(LeafBoardSeconds,leafBoardTime+dt);float u=leafBoardTime/LeafBoardSeconds;Vector3 delta=leaf.localPosition-leafBoardOrigin,start=leafBoardStart+delta,end=leafBoardEnd+delta;
   end.y=LeafSurfaceHeight(end)+player.StandingHeightOffset;Vector3 next=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,(u-.16f)/.84f));next.y=Mathf.Lerp(start.y,end.y,Mathf.SmoothStep(0,1,u/.35f))+.1f*Mathf.Sin(u*Mathf.PI);Position=next;Velocity=Vector3.zero;
   Vector3 worldDelta=leaf.position-leafBoardWorldOrigin;player.LeafBoardPose(Position,end,leafBoardHeading,worldDelta,leafBoardBrace+worldDelta,u,time,LeafSurfaceHeight);
   if(u>=1){leafBoardTime=-1;leafBoardCompleted=true;onLeaf=true;safePosition=Position;if(!boarded)events.Add("Boarded the green leaf in 3D");boarded=true;}
  }
  float LeafSurfaceHeight(Vector3 p){Vector3 q=leafSurfaceFromWorld.MultiplyPoint3x4(new Vector3(p.x,leafSurfaceWorldY,p.z));Vector3 point=new Vector3(q.x,ReturnVolumeArt.LeafHeight(q.x,q.z),q.z),world=leafSurfaceToWorld.MultiplyPoint3x4(point);return world.y+ReturnVolumeArt.LeafBankLift(point,world);}
  public bool ValidateRestPose(out string detail){if(!Resting){detail="body is not resting";return false;}return player.ValidateRestSupport(LeafSurfaceHeight,out detail);}
  public ReturnPerson3D.RestContactPoint DescribeRestContact()=>player.DescribeRestContact(LeafSurfaceHeight);
  public Vector2 ShoeClearance()=>player.MeasureShoeClearance(SurfaceHeight);
  [System.Serializable] sealed class ShoreViewFoot {public string side;public int vertices,islandOccluded,leafOccluded,opaqueClear,waterInFront,waterWithoutOpaqueOcclusion;public Vector3 foot;public float minimumWaterDepth,maximumWaterDepth;}
  [System.Serializable] sealed class ShoreViewReport {public string method="Camera-to-posed-shoe vertex segments against rendered island/leaf triangles; water crossing uses the animated surface. Counts describe geometry, not pixel alpha or body self-occlusion. Leaf shader wind is not included.";public bool active;public Vector3 position,cameraPosition;public Quaternion cameraRotation;public float sceneElapsed;public ShoreViewFoot[] feet;}
  sealed class ShoreOccluder {
   public readonly string name;readonly Vector3[] vertices;readonly int[] triangles;readonly Bounds bounds;
   public ShoreOccluder(Transform surface){if(surface==null)throw new System.InvalidOperationException("Shore diagnostic surface missing");name=surface.name;var mesh=surface.GetComponent<MeshFilter>().sharedMesh;vertices=mesh.vertices;triangles=mesh.triangles;bounds=surface.GetComponent<MeshRenderer>().bounds;for(int i=0;i<vertices.Length;i++)vertices[i]=surface.TransformPoint(vertices[i]);}
   public float Hit(Vector3 origin,Vector3 direction,float limit){if(!bounds.IntersectRay(new Ray(origin,direction),out float entry)||entry>limit)return float.PositiveInfinity;float closest=limit;
    for(int i=0;i<triangles.Length;i+=3){Vector3 a=vertices[triangles[i]],ab=vertices[triangles[i+1]]-a,ac=vertices[triangles[i+2]]-a,p=Vector3.Cross(direction,ac);float determinant=Vector3.Dot(ab,p);if(Mathf.Abs(determinant)<.0000001f)continue;float inverse=1/determinant;Vector3 offset=origin-a;float u=Vector3.Dot(offset,p)*inverse;if(u<0||u>1)continue;Vector3 q=Vector3.Cross(offset,ab);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)continue;float distance=Vector3.Dot(ac,q)*inverse;if(distance>.001f&&distance<closest)closest=distance;}
    return closest<limit?closest:float.PositiveInfinity;
   }
  }
  public void WriteShoreViewDiagnostic(string previewRoot){
   var islandView=new ShoreOccluder(root.Find("A small living island"));var leafView=new ShoreOccluder(leaf);var shoes=player.DescribeShoeVertices();var feet=new ShoreViewFoot[2];Vector3 origin=camera.transform.position;
   for(int side=0;side<2;side++){var foot=new ShoreViewFoot{side=side==0?"right":"left",vertices=shoes[side].Length,foot=side==0?player.RightFootPosition:player.LeftFootPosition,minimumWaterDepth=float.PositiveInfinity};if(foot.vertices==0)throw new System.InvalidOperationException("Shore diagnostic shoe vertices missing");
    foreach(Vector3 point in shoes[side]){Vector3 delta=point-origin;float distance=delta.magnitude;Vector3 direction=delta/distance;float islandHit=islandView.Hit(origin,direction,distance-.002f),leafHit=leafView.Hit(origin,direction,distance-.002f);bool clear=float.IsInfinity(islandHit)&&float.IsInfinity(leafHit);if(clear)foot.opaqueClear++;else if(islandHit<leafHit)foot.islandOccluded++;else foot.leafOccluded++;
     float depth=Mathf.Max(0,ReturnWaterSurface.Height(point.x,point.z,time)-point.y);foot.minimumWaterDepth=Mathf.Min(foot.minimumWaterDepth,depth);foot.maximumWaterDepth=Mathf.Max(foot.maximumWaterDepth,depth);
     float previous=origin.y-ReturnWaterSurface.Height(origin.x,origin.z,time);bool waterFront=false;for(int step=1;step<=32;step++){Vector3 sample=Vector3.Lerp(origin,point,step/32f);float separation=sample.y-ReturnWaterSurface.Height(sample.x,sample.z,time);if(previous>0&&separation<=0){waterFront=true;break;}previous=separation;}
     if(waterFront){foot.waterInFront++;if(clear)foot.waterWithoutOpaqueOcclusion++;}
    }feet[side]=foot;
   }
   var report=new ShoreViewReport{active=Active,position=Position,cameraPosition=origin,cameraRotation=camera.transform.rotation,sceneElapsed=SceneElapsed,feet=feet};System.IO.File.WriteAllText(System.IO.Path.Combine(previewRoot,"Evidence","shore-view-diagnostic.json"),JsonUtility.ToJson(report,true));
  }
  public ReturnPerson3D.GroundPoseReport DescribeFruitGroundPose(string label){var report=player.DescribeGroundPose(SurfaceHeight);report.label=label;report.scene=Scene.ToString();report.sceneElapsed=SceneElapsed;report.fruitActionTime=coloursTime;return report;}
  float SurfaceHeight(Vector3 p)=>SurfaceHeight(p,out _);
  bool ApproachingShore(Vector3 p,Vector3 direction){
   direction.y=0;if(direction.sqrMagnitude<.001f)direction=Vector3.ProjectOnPlane(player.root.forward,Vector3.up);
   if(direction.sqrMagnitude<.001f)return false;
   Vector3 ahead=p+direction.normalized*1.6f;
   return ReturnVolumeArt.IslandContains(ahead.x,ahead.z-26)&&ReturnVolumeArt.IslandHeight(ahead.x,ahead.z-26)>-.45f;
  }
  float SurfaceHeight(Vector3 p,out bool leafSupport) {
    if(Scene==Movement.Reunion&&streetEntered){leafSupport=false;return 0;}
   float height=ReturnVolumeArt.IslandContains(p.x,p.z-26)?ReturnVolumeArt.IslandHeight(p.x,p.z-26):-.48f;leafSupport=false;
   if(OnLeaf(p)){float leafHeight=LeafSurfaceHeight(p);if(leafHeight>height){height=leafHeight;leafSupport=true;}}
   return height;
  }
  float Floor(Vector3 p,out bool leafSupport) {
   float height=SurfaceHeight(p,out leafSupport);return height>-.48f?height+player.StandingHeightOffset:height;
  }
  public Vector2 DirectionTo(Vector3 target) {
   Vector3 d=target-Position;d.y=0;if(d.magnitude<.2f)return Vector2.zero;d.Normalize();var local=Quaternion.Euler(0,-Yaw,0)*d;return new Vector2(local.x,local.z);
  }
  void CaptureCameraView(){previousCamera=camera.transform.position;previousTarget=cameraTarget;previousFieldOfView=camera.fieldOfView;}
  Vector3 LoweredFruitCentre=>player.root.TransformPoint(new Vector3(.28f,1.02f,.22f));
  void PlaceHeldFruit(Vector3 held,float yaw){
   fruit.localRotation=Quaternion.Euler(0,yaw,0)*fruitRotation;
   fruit.localScale=Vector3.one*(.06f/naturalFruitRadius);
   fruit.position=held-fruit.TransformVector(fruitCentreOffset);
   bittenFruit.localPosition=fruit.localPosition;bittenFruit.localRotation=fruit.localRotation;bittenFruit.localScale=fruit.localScale;
  }
  void EnableTreeBreeze(Transform item){
   foreach(var renderer in item.GetComponentsInChildren<Renderer>()){var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)if(materials[i]!=null&&materials[i].HasProperty("_TreeSway")){materials[i]=art.Copy(materials[i]);materials[i].SetFloat("_TreeSway",1);}renderer.sharedMaterials=materials;}
  }
  Vector3 TreeBentPoint(Vector3 point){float h=Mathf.Max(0,(point.y-treeAnchor.y)/TreeBendHeight);return point+treeBreeze*(h*h);}
  void UpdateTreeBreeze(){
   treeAnchor=root.TransformPoint(treeBaseLocal);treeBreeze=new Vector3(.085f*Mathf.Sin(time*.72f)+.022f*Mathf.Sin(time*1.37f+.4f),0,.035f*Mathf.Sin(time*.51f+.8f));
   Shader.SetGlobalVector("_ReturnTreeAnchor",new Vector4(treeAnchor.x,treeAnchor.y,treeAnchor.z,TreeBendHeight));Shader.SetGlobalVector("_ReturnTreeBreeze",new Vector4(treeBreeze.x,0,treeBreeze.z,0));
   if(!fruitPicked){Vector3 stem=root.TransformPoint(fruitStemRoot);float h=Mathf.Max(0,(stem.y-treeAnchor.y)/TreeBendHeight);Vector3 slope=treeBreeze*(2*h/TreeBendHeight);
    fruit.SetPositionAndRotation(root.TransformPoint(fruitRestLocalPosition),Quaternion.FromToRotation(Vector3.up,Vector3.up+slope)*root.rotation*fruitRestLocalRotation);fruit.position+=TreeBentPoint(stem)-fruit.TransformPoint(fruitStemLocal);fruitHangCentre=fruit.TransformPoint(fruitCentreOffset);
   }
  }
  public void Step(float dt,ReturnInput input) {
   events.Clear();if(!Active)return;leafVelocity=Vector3.zero;time+=dt;SceneElapsed+=dt;UpdateTreeBreeze();
   if(Scene==Movement.Credits)return;
   if(Scene!=Movement.Hospital){PlaceLeaf(leaf.localPosition.x,LeafZ);if(Scene==Movement.Reunion||coloursTime>=0)ReturnVolumeArt.DrapeLeaf(leaf,leafMesh,leafShape,leafPose,leafPins);}
   if(Scene==Movement.Hospital){HospitalPose(dt);if(SceneElapsed>=HospitalCreditsAt){Scene=Movement.Credits;SceneElapsed=0;events.Add("Credits reached");}return;}
   if(Scene==Movement.Reunion) {
     StepStreet(dt,input);
    return;
   }
   if(coloursTime>=0) {
    coloursTime+=dt;Velocity=Vector3.zero;Colours=fruitTasted?.001f:0;
    float facing=Mathf.Atan2(fruitHangCentre.x-Position.x,fruitHangCentre.z-Position.z)*Mathf.Rad2Deg;
    player.Pose(Position,Vector3.zero,false,false,time,facing:Mathf.LerpAngle(fruitFacingStart,facing,Mathf.SmoothStep(0,1,coloursTime/.65f)),groundSurface:SurfaceHeight);
    float reach=Mathf.SmoothStep(0,1,coloursTime/1.05f),bring=Mathf.SmoothStep(0,1,(coloursTime-1.05f)/1.05f);
    float bite=Mathf.SmoothStep(0,1,(coloursTime-1.55f)/.45f)*(1-Mathf.SmoothStep(0,1,(coloursTime-2.08f)/.25f));
    float showBite=Mathf.SmoothStep(0,1,(coloursTime-2.55f)/.4f),release=Mathf.SmoothStep(0,1,(coloursTime-3.05f)/.45f);
    Vector3 mouth=player.MouthPosition+player.root.forward*(FruitRadius*.55f);
    Vector3 approach=mouth+player.root.forward*(FruitRadius*1.6f)*(1-Mathf.SmoothStep(0,1,(coloursTime-1.8f)/.4f));
    Vector3 desired=Vector3.Lerp(fruitHangCentre,approach,bring)+showBite*FruitRadius*(player.root.forward*1.7f-player.root.up*.7f);
    Vector3 held=player.HoldFruit(Vector3.Lerp(desired,LoweredFruitCentre,release),FruitRadius,reach,bite,-60*showBite,coloursTime);
    if(coloursTime>=1.05f){
     if(!fruitPicked){fruitPicked=true;events.Add("Picked the blue fruit");}
     PlaceHeldFruit(held,facing-60*showBite);
    }
    if(coloursTime>=2.2f&&!fruitTasted){fruitTasted=true;fruit.gameObject.SetActive(false);bittenFruit.gameObject.SetActive(true);Colours=.001f;events.Add("Ate the blue fruit");}
    if(coloursTime>=3.5f){Scene=Movement.Reunion;SceneElapsed=0;fruitPosition=Position;events.Add("Entered Reunion / 0");CaptureCameraView();}
    return;
   }
   Yaw+=input.look.x;pitch=Mathf.Clamp(pitch-input.look.y,8,62);
   if(CanRest&&input.interact){Resting=!Resting;if(Resting){boarded=true;onLeaf=true;Velocity=Vector3.zero;events.Add("Resting on the green leaf");if(!leafDrifting){leafDrifting=true;events.Add("Let the green leaf carry me");}}}
   if(Resting&&(Mathf.Abs(input.move)+Mathf.Abs(input.vertical)>.1f||input.jumpPressed))Resting=false;
   float oldLeafZ=LeafZ;Vector3 oldLeafPosition=leaf.position;
   if(leafDrifting)PlaceLeaf(Mathf.Sin(time*.35f)*.035f,Mathf.MoveTowards(LeafZ,ReturnVolumeArt.LeafDockZ,dt*1.22f));
   leafVelocity=dt>0?(leaf.position-oldLeafPosition)/dt:Vector3.zero;
    leaf.localRotation=Quaternion.Euler(0,0,Mathf.Sin(time*.48f)*.7f);CacheLeafSurfaceTransform();
   if(LeafZ>ReturnVolumeArt.LeafDockZ-2.6f)ReturnVolumeArt.DrapeLeaf(leaf,leafMesh,leafShape,leafPose,leafPins);
    if(onLeaf||Resting)Position+=Vector3.forward*(LeafZ-oldLeafZ);
    if(LeafBoarding){StepLeafBoarding(dt);return;}
   if(Resting&&player.RestAmount<.45f){Vector2 offset=new Vector2(Position.x-leaf.position.x,Position.z-leaf.position.z);offset=Vector2.MoveTowards(offset,Vector2.zero,dt*.45f);Position=new Vector3(leaf.position.x+offset.x,Position.y,leaf.position.z+offset.y);}
   bool shoreWading=false;
   if(!Resting&&player.RestAmount==0) {
     Vector3 direction=Quaternion.Euler(0,Yaw,0)*new Vector3(input.move,0,input.vertical);direction=Vector3.ClampMagnitude(direction,1);
     if(TryBeginLeafBoarding(direction,input)){StepLeafBoarding(0);return;}
     float floor=Floor(Position,out bool supportedByLeaf);bool grounded=Position.y<=floor+.035f;
     bool underWater=Position.y<-.25f,onShore=grounded&&!supportedByLeaf&&!underWater&&ReturnVolumeArt.IslandContains(Position.x,Position.z-26),quietGround=grounded&&!underWater&&(supportedByLeaf||onShore);
      shoreWading=underWater&&!supportedByLeaf&&ApproachingShore(Position,direction);
     if(input.jumpPressed||underWater||Velocity.y>.1f)quietWalkUntil=0;else if(quietGround)quietWalkUntil=time+.30f;
      bool quietWalking=!input.jumpPressed&&(shoreWading||!underWater&&(quietGround||(time<quietWalkUntil&&Velocity.y<=.1f)));float speed=underWater&&!shoreWading?2.6f:quietWalking?IslandWalkSpeed:3.8f,acceleration=quietWalking?6.5f:15;
     Vector3 velocity=Velocity;
      if(underWater&&!shoreWading&&SurfaceHeight(Position)<=-.48f){
      float phase=player.SwimPhase;
      float pull=Mathf.SmoothStep(0,1,(phase-.08f)/.12f)*(1-Mathf.SmoothStep(0,1,(phase-.20f)/.12f));
      float kick=Mathf.SmoothStep(0,1,(phase-.46f)/.14f)*(1-Mathf.SmoothStep(0,1,(phase-.60f)/.14f));
      Vector3 planar=new Vector3(velocity.x,0,velocity.z);
      float turn=planar.sqrMagnitude>.01f?Mathf.Clamp01(1-Vector3.Dot(planar.normalized,direction.normalized)):1;
      float response=direction.sqrMagnitude>.001f?Mathf.Lerp(4,9,turn):7;
      // Pull and kick build speed; drag eases it back during the held glide.
      planar=Vector3.MoveTowards(planar,direction*speed*(.86f+.24f*pull+.44f*kick),dt*response);
      velocity.x=planar.x;velocity.z=planar.z;
     }else{velocity.x=Mathf.MoveTowards(velocity.x,direction.x*speed,dt*acceleration);velocity.z=Mathf.MoveTowards(velocity.z,direction.z*speed,dt*acceleration);}
    if(input.jumpPressed&&(grounded||underWater)){velocity.y=underWater?4.8f:7;events.Add("3D jump");}
    velocity.y-=dt*(underWater?5:18);
    Vector3 next=Position+velocity*dt;float nextFloor=Floor(next,out supportedByLeaf);
    if(next.y<nextFloor){next.y=nextFloor;velocity.y=0;if(nextFloor>-.2f)safePosition=next;}
    Position=next;Velocity=velocity;onLeaf=supportedByLeaf&&Mathf.Abs(Position.y-nextFloor)<.05f;
    if(onLeaf&&!boarded){boarded=true;events.Add("Boarded the green leaf in 3D");}
    if(Mathf.Abs(Position.x)>42||Position.z<-45||Position.z>60)RetrySafe();
   }else{Position=new Vector3(Position.x,LeafSurfaceHeight(Position)+player.StandingHeightOffset,Position.z);Velocity=Vector3.zero;onLeaf=true;}
   bool airborne=!Resting&&player.RestAmount==0&&Position.y>Floor(Position,out _)+.04f;
   if(!Resting&&player.RestAmount==0&&Position.y<-.25f&&SurfaceHeight(Position)<=-.48f&&!shoreWading)player.SwimPose(Position,Velocity,time);
   else player.Pose(Position,Velocity,Resting,airborne,time,restingSurface:Resting||player.RestAmount>0?LeafSurfaceHeight:(System.Func<Vector3,float>)null,groundSurface:SurfaceHeight,restingHeading:leaf.localEulerAngles.y);
    // Sample the solved feet before another simulation tick replaces this pose.
    if(stanceReview!=null&&!stanceReview.Saved){
     if(!Resting&&!LeafBoarding&&(OnLeafSurface||OnIsland))stanceReview.Sample(player,OnLeafSurface,OnLeafSurface?leaf.position:root.TransformPoint(new Vector3(0,0,26)),SurfaceHeight,SceneElapsed);
     else stanceReview.ResetContacts();
    }
   if(CanFruit&&input.interact&&player.RestAmount==0){coloursTime=0;Colours=0;Resting=false;Velocity=Vector3.zero;fruitFacingStart=player.root.localEulerAngles.y;CaptureCameraView();events.Add("Reached for the blue fruit");}
  }
  static float ColourWaveRadius(float progress) {
   if(progress<=0)return -5;
   // Preserve the nearby front's pace, then complete recovery across the return view.
   return progress*350f+(1024f-350f)*Mathf.SmoothStep(0,1,(progress-.85f)/.15f);
  }
  void RecoverColours(float t) {
   float progress=Mathf.InverseLerp(0,reverse.RadioX,reverse.CameraX(t));
   float yellow=Mathf.Clamp01((t-1.5f)/6.5f),red=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.03f,.68f,progress)),blue=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.48f,1,progress));Colours=(yellow+red+blue)/3;
   Shader.SetGlobalFloat("_ReturnYellowRadius",ColourWaveRadius(yellow));Shader.SetGlobalFloat("_ReturnRedRadius",ColourWaveRadius(red));Shader.SetGlobalFloat("_ReturnBlueRadius",ColourWaveRadius(blue));
   Shader.SetGlobalVector("_ReturnCityRecovery",new Vector4(reverse.CityCentre.x,reverse.CityCentre.z,CityRecovery,t>=ReturnReverseJourney.UniteAt?ReturnRecallArchitecture.CityRadius+.5f:0));
    reverse.Step(t,yellow,red,blue);sky.Apply(Colours);camera.backgroundColor=sky.RecoveredHorizon;RenderSettings.fogColor=camera.backgroundColor;
   // The opaque liquid must not occlude the paper view while the lens is flat.
   water.gameObject.SetActive(t<ReturnReverseJourney.FlattenAt||t>=ReturnReverseJourney.UniteAt);
   sun.color=Color.Lerp(Color.Lerp(Color.Lerp(new Color(1,.97f,.83f),new Color(1,.84f,.54f),yellow),new Color(1,.72f,.57f),red),new Color(1,.97f,.9f),blue);
   sun.intensity=Mathf.Lerp(Mathf.Lerp(1.08f,1.12f,yellow),1.02f,blue);sun.shadowStrength=Mathf.Lerp(.5f,.7f,yellow);
   if(t>=24&&!reunited){reunited=true;events.Add("Yellow, red and blue reunited");}
  }
  void HospitalPose(float dt) {
   float t=SceneElapsed;Velocity=Vector3.zero;focus.white=0;
   focus.blur=Mathf.Lerp(2.8f,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,4.1f,t)));focus.warmth=.4f;
  }
  float HospitalFloorSurface(Vector3 at)=>hospital.TransformPoint(new Vector3(0,hospitalFloorY,0)).y;
  public bool HospitalFeetClearOfFloor() {
   var people=new[]{mother.DescribeGroundPose(hospitalGroundSurface),nurse.DescribeGroundPose(hospitalGroundSurface),familyVisitor.DescribeGroundPose(hospitalGroundSurface)};bool clear=true;
   for(int i=0;i<people.Length;i++) {
    var pose=people[i];pose.label=i==0?"mother":i==1?"nurse":"family";pose.scene="Hospital";pose.sceneElapsed=SceneElapsed;
    clear&=pose.active&&!pose.airborne&&pose.groundSurfaceProvided&&pose.feet.Length==2;
    foreach(var foot in pose.feet)clear&=!float.IsNaN(foot.minimumShoeGap)&&!float.IsInfinity(foot.minimumShoeGap)&&foot.minimumShoeGap>=-.004f;
   }
   var report=new HospitalFloorReport{passed=clear,samples=++hospitalFloorSamples,localHeight=hospitalFloorY,part=hospitalFloorPart,people=people};
   string evidence=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(evidence))System.IO.File.WriteAllText(System.IO.Path.Combine(evidence,"hospital-floor-contact.json"),JsonUtility.ToJson(report,true));
   return clear;
  }
  static bool IsBedPart(string name){name=name.ToLowerInvariant();foreach(string word in new[]{"bed","rail","duvet","mattress","sheet","pillow","blanket","headboard","footboard"})if(name.Contains(word))return true;return false;}
  bool HospitalVertexContact(Vector3 world,out BedSurface hit,out float depth){
   foreach(var surface in hospitalBedSurfaces){if(!surface.bounds.Contains(world))continue;
    if(Physics.ComputePenetration(hospitalVertexProbe,world,Quaternion.identity,surface.collider,surface.position,surface.rotation,out _,out depth)&&depth>.00001f){hit=surface;return true;}
   }hit=null;depth=0;return false;
  }
  [System.Serializable] public sealed class HospitalSurfaceControl {
   public string name,mesh,cookingOptions;public bool readable,active,enabled,railInBounds,clearInBounds,railContact,clearContact;public int vertexCount;public float railDepth,clearDepth;public Vector3 position,scale,boundsMin,boundsMax,meshBoundsMin,meshBoundsMax;public Quaternion rotation;
  }
  [System.Serializable] public sealed class HospitalGeometryControlReport {
   public bool railContact,clearContact,railContactWithoutBounds,clearContactWithoutBounds,sphereBoxControl,hospitalActive,probeActive,probeEnabled;public float probeRadius,sceneElapsed;public int nonemptyMeshCount;public Vector3 hospitalPosition,hospitalScale,railWorld,clearWorld;public Quaternion hospitalRotation;public HospitalSurfaceControl[] surfaces;
  }
  HospitalGeometryControlReport RecordHospitalGeometryControls(Vector3 rail,Vector3 clear){
   var samples=new List<HospitalSurfaceControl>();var report=new HospitalGeometryControlReport{hospitalActive=hospital.gameObject.activeInHierarchy,probeActive=hospitalVertexProbe.gameObject.activeInHierarchy,probeEnabled=hospitalVertexProbe.enabled,probeRadius=hospitalVertexProbe.radius,sceneElapsed=SceneElapsed,hospitalPosition=hospital.position,hospitalScale=hospital.lossyScale,hospitalRotation=hospital.rotation,railWorld=rail,clearWorld=clear};
   foreach(var surface in hospitalBedSurfaces){var collider=surface.collider;if(collider.bounds.size.sqrMagnitude>1e-8f)report.nonemptyMeshCount++;bool railContact=Physics.ComputePenetration(hospitalVertexProbe,rail,Quaternion.identity,collider,surface.position,surface.rotation,out _,out float railDepth)&&railDepth>.00001f;bool clearContact=Physics.ComputePenetration(hospitalVertexProbe,clear,Quaternion.identity,collider,surface.position,surface.rotation,out _,out float clearDepth)&&clearDepth>.00001f;bool railInBounds=surface.bounds.Contains(rail),clearInBounds=surface.bounds.Contains(clear);report.railContact|=railInBounds&&railContact;report.clearContact|=clearInBounds&&clearContact;report.railContactWithoutBounds|=railContact;report.clearContactWithoutBounds|=clearContact;samples.Add(new HospitalSurfaceControl{name=collider.name,mesh=collider.sharedMesh.name,readable=collider.sharedMesh.isReadable,vertexCount=collider.sharedMesh.vertexCount,active=collider.gameObject.activeInHierarchy,enabled=collider.enabled,cookingOptions=collider.cookingOptions.ToString(),position=surface.position,rotation=surface.rotation,scale=collider.transform.lossyScale,boundsMin=surface.bounds.min,boundsMax=surface.bounds.max,meshBoundsMin=collider.sharedMesh.bounds.min,meshBoundsMax=collider.sharedMesh.bounds.max,railInBounds=railInBounds,clearInBounds=clearInBounds,railContact=railContact,clearContact=clearContact,railDepth=railContact?railDepth:0,clearDepth=clearContact?clearDepth:0});}
   // Measure the original queries before this independent control creates or synchronizes anything.
   var boxObject=new GameObject("Native acceptance independent sphere-box control");boxObject.transform.SetParent(hospital,false);boxObject.transform.localPosition=Vector3.up*20;var box=boxObject.AddComponent<BoxCollider>();box.size=Vector3.one*.1f;Physics.SyncTransforms();report.sphereBoxControl=Physics.ComputePenetration(hospitalVertexProbe,box.transform.position+Vector3.right*.05f,Quaternion.identity,box,box.transform.position,box.transform.rotation,out _,out float boxDepth)&&boxDepth>0;Object.DestroyImmediate(boxObject);
   report.surfaces=samples.ToArray();string directory=System.Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"hospital-geometry-controls.json"),JsonUtility.ToJson(report,true));return report;
  }
  void PrepareHospitalGeometryCheck(){
   if(hospitalVertexProbe!=null){if(!HospitalGeometryControlsPassed)throw new System.InvalidOperationException("Hospital geometry initialization previously failed its controls");return;}
   // Only the native acceptance path calls this method; no gameplay physics uses these probes.
    var prepared=Resources.Load<GameObject>("ReturnRealism/HospitalBedCollision");if(prepared==null)throw new System.InvalidOperationException("Prepared hospital bed collision asset missing");
    var collisionRoot=Object.Instantiate(prepared,hospital,false);
    foreach(var collider in collisionRoot.GetComponentsInChildren<MeshCollider>(true))hospitalBedSurfaces.Add(new BedSurface{collider=collider,position=collider.transform.position,rotation=collider.transform.rotation});
   if(hospitalBedSurfaces.Count!=24)throw new System.InvalidOperationException("Actual hospital bed geometry does not match the24 measured mesh parts");
   var probe=new GameObject("Native acceptance skin clearance probe");probe.transform.SetParent(hospital,false);probe.transform.localPosition=Vector3.down*10000;
   hospitalVertexProbe=probe.AddComponent<SphereCollider>();hospitalVertexProbe.radius=.005f;
   Physics.SyncTransforms();foreach(var surface in hospitalBedSurfaces){surface.bounds=surface.collider.bounds;surface.bounds.Expand(.01f);}
   // These points come from the read-only imported-triangle query in Evidence/v166.
   Vector3 rail=hospital.TransformPoint(new Vector3(.7809224129f,1.1033232212f,.2068492472f));
   Vector3 clear=hospital.TransformPoint(new Vector3(.8199462891f,1.1435135603f,.2069129944f));
   var controls=RecordHospitalGeometryControls(rail,clear);HospitalGeometryControlsPassed=controls.nonemptyMeshCount==24&&controls.railContact&&!controls.clearContact&&controls.sphereBoxControl;
   if(!HospitalGeometryControlsPassed)throw new System.InvalidOperationException("Actual hospital geometry failed its known rail-contact or clear-fingertip control");
  }
  [System.Serializable] public sealed class HospitalContact {
   public string person,renderer,mesh,nearestRigTransform,bedSurface;public float geometryProbeRadius,penetrationDepth;public int vertex;public float sceneElapsed,nearestTransformDistance;public Vector3 hospitalLocal,world,nearestTransformLocal;
   public string predicate="A5mm sphere at the actual posed skin vertex overlaps an imported bed/rail mesh using Physics.ComputePenetration. This is sampled vertex-to-surface clearance, not proof about every triangle or all animation times.";
  }
  public HospitalContact LastHospitalContact {get;private set;}
  public bool HospitalPeopleClearOfBed() {
   LastHospitalContact=null;PrepareHospitalGeometryCheck();
   foreach(var person in new[]{mother,nurse,familyVisitor})foreach(var renderer in person.root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
    var mesh=new Mesh();try {renderer.BakeMesh(mesh);var vertices=mesh.vertices;for(int index=0;index<vertices.Length;index++){
     Vector3 world=renderer.transform.TransformPoint(vertices[index]),p=hospital.InverseTransformPoint(world);
     HospitalSkinVerticesChecked++;if(HospitalVertexContact(world,out var bedSurface,out float penetration)){
      Transform nearest=null;float distance=float.PositiveInfinity;foreach(var joint in person.root.GetComponentsInChildren<Transform>()){float d=Vector3.Distance(world,joint.position);if(d<distance){nearest=joint;distance=d;}}
      LastHospitalContact=new HospitalContact{bedSurface=bedSurface.collider.name,geometryProbeRadius=.005f,penetrationDepth=penetration,person=person==mother?"mother":person==nurse?"nurse":"family",renderer=renderer.name,mesh=renderer.sharedMesh.name,vertex=index,sceneElapsed=SceneElapsed,hospitalLocal=p,world=world,nearestRigTransform=nearest==null?"":nearest.name,nearestTransformDistance=distance,nearestTransformLocal=nearest==null?Vector3.zero:hospital.InverseTransformPoint(nearest.position)};return false;
     }
    }}finally{Object.DestroyImmediate(mesh);}
   }return true;
  }
  bool HospitalPointInView(Vector3 point){Vector3 p=camera.WorldToViewportPoint(point);return p.z>camera.nearClipPlane&&p.x>.07f&&p.x<.93f&&p.y>.08f&&p.y<.92f;}
  public ReturnPerson3D.FaceArmClearanceReport LastHospitalFaceClearance {get;private set;}
  public bool HospitalFaceClear(out float gap){
   bool clear=mother.FaceClearOfRightArm(camera,out gap);
   if(!clear){
    LastHospitalFaceClearance=mother.DescribeFaceArmClearance(camera);LastHospitalFaceClearance.sceneElapsed=SceneElapsed;
    if(LastHospitalFaceClearance.geometryControlsPassed&&LastHospitalFaceClearance.geometryAvailable&&LastHospitalFaceClearance.triangleClearanceAtLeastSixPixels){LastHospitalFaceClearance.acceptedUsingProjectedTriangles=true;LastHospitalFaceClearance.certifiedGapLowerBoundPixels=6;gap=6;clear=true;}
   }
   return clear;
  }
  public bool HospitalMotherFaceInView=>mother.FaceInView(camera);
  public bool HospitalFamilyFacesInView=>mother.FaceInView(camera)&&familyVisitor.FaceInView(camera);
  public float HospitalHandDistance=>Vector3.Distance(mother.RightPalmPosition,camera.transform.position);
  public bool HospitalSpeakerVisible(bool familiar)=>familiar?HospitalPointInView(mother.MouthPosition)&&HospitalPointInView(mother.RightPalmPosition):HospitalPointInView(nurse.MouthPosition);
  Vector4 WaterHandState(Vector3 palm,ref Vector3 previous,ref float contact,float dt,bool swimming) {
   float speed=waterHandsSampled&&dt>.0001f?Vector3.Distance(palm,previous)/dt:0;
   float surface=ReturnWaterSurface.Height(palm.x,palm.z,time);
   float target=swimming?1-Mathf.SmoothStep(0,1,Mathf.Abs(palm.y-surface)/.24f):0;
   contact=Mathf.Lerp(contact,target,1-Mathf.Exp(-dt*12));previous=palm;
   return new Vector4(palm.x,palm.z,Mathf.Clamp01(speed/1.8f),contact);
  }
  static float DriftFramingDistance(Bounds bounds,Vector3 target,Quaternion view,float fieldOfView,float aspect) {
   float vertical=Mathf.Tan(fieldOfView*.5f*Mathf.Deg2Rad),horizontal=vertical*Mathf.Max(.2f,aspect),distance=1;
   Quaternion inverse=Quaternion.Inverse(view);
   for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){
    Vector3 point=inverse*(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))-target);
    float height=Mathf.Abs(point.y)/(vertical*(point.y>=0?.80f:.48f))-point.z;
    distance=Mathf.Max(distance,Mathf.Max(Mathf.Abs(point.x)/(horizontal*.82f)-point.z,Mathf.Max(height,.2f-point.z)));
   }
   return distance;
  }
  [System.Serializable] sealed class DriftViewObservation {
   public string sample;public int frame;public float sceneElapsed,fieldOfView,nearClip,farClip;
   public Vector3 cameraPosition,cameraTarget,leafPosition,leafBoundsCenter,leafBoundsSize,bodyWorld,bodyViewport,mouthViewport;public Quaternion cameraRotation;public Vector3[] leafCornerViewports;
    public DriftRendererState[] renderers;
    public Matrix4x4 projection,cullingMatrix;
    public DriftMeshState mesh;
  }
  [System.Serializable] sealed class DriftMeshState {
    public bool readable,assignedToRenderer;public string name,topology;
    public int vertexCount,indexCount,invalidVertices,invalidIndices,degenerateTriangles,fullyInViewTriangles,nonzeroProjectedTriangles;
    public float summedAreaPixels,maxTriangleAreaPixels;public Vector2 viewportMin,viewportMax,pixelMin,pixelMax;public Rect pixelRect,viewportRect;
  }
  static bool DriftFinite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y)&&!float.IsInfinity(p.z);
  DriftMeshState ObserveDriftMesh() {
    Mesh mesh=leaf.GetComponent<MeshFilter>().sharedMesh;
    var state=new DriftMeshState{readable=mesh!=null&&mesh.isReadable,assignedToRenderer=mesh==leafMesh,pixelRect=camera.pixelRect,viewportRect=camera.rect};
    if(!state.readable)return state;
    state.name=mesh.name;state.topology=mesh.GetTopology(0).ToString();var vertices=mesh.vertices;var indices=mesh.triangles;state.vertexCount=vertices.Length;state.indexCount=indices.Length;
    var world=new Vector3[vertices.Length];var screen=new Vector3[vertices.Length];var inside=new bool[vertices.Length];state.viewportMin=Vector2.one*10000;state.viewportMax=Vector2.one*-10000;state.pixelMin=Vector2.one*10000000;state.pixelMax=Vector2.one*-10000000;
    for(int i=0;i<vertices.Length;i++){
      world[i]=leaf.TransformPoint(vertices[i]);Vector3 viewport=camera.WorldToViewportPoint(world[i]);screen[i]=camera.WorldToScreenPoint(world[i]);
      if(!DriftFinite(world[i])||!DriftFinite(viewport)||!DriftFinite(screen[i])){state.invalidVertices++;continue;}
      state.viewportMin=Vector2.Min(state.viewportMin,new Vector2(viewport.x,viewport.y));state.viewportMax=Vector2.Max(state.viewportMax,new Vector2(viewport.x,viewport.y));state.pixelMin=Vector2.Min(state.pixelMin,new Vector2(screen[i].x,screen[i].y));state.pixelMax=Vector2.Max(state.pixelMax,new Vector2(screen[i].x,screen[i].y));
      inside[i]=viewport.x>=0&&viewport.x<=1&&viewport.y>=0&&viewport.y<=1&&viewport.z>=camera.nearClipPlane&&viewport.z<=camera.farClipPlane;
    }
    foreach(int index in indices)if(index<0||index>=vertices.Length)state.invalidIndices++;
    for(int i=0;i+2<indices.Length;i+=3){
      int a=indices[i],b=indices[i+1],c=indices[i+2];if(a<0||b<0||c<0||a>=vertices.Length||b>=vertices.Length||c>=vertices.Length)continue;
      if(Vector3.Cross(world[b]-world[a],world[c]-world[a]).sqrMagnitude<=1e-14f)state.degenerateTriangles++;
      if(!inside[a]||!inside[b]||!inside[c])continue;state.fullyInViewTriangles++;
      Vector3 ab=screen[b]-screen[a],ac=screen[c]-screen[a];float area=Mathf.Abs(ab.x*ac.y-ab.y*ac.x)*.5f;
      if(area>1e-6f){state.nonzeroProjectedTriangles++;state.summedAreaPixels+=area;state.maxTriangleAreaPixels=Mathf.Max(state.maxTriangleAreaPixels,area);}
    }
    return state;
  }
  [System.Serializable] sealed class DriftRendererState {
    public string name,type,shadowCasting;public bool enabled,active,forceRenderingOff,visible,layerIncluded,inFrustum,inCullingFrustum;
    public int layer;public Vector3 boundsCenter,boundsSize;public string[] materials;
  }
  DriftRendererState[] DriftRenderers() {
    var renderers=new List<Renderer>(leaf.GetComponentsInChildren<Renderer>(true));renderers.AddRange(player.root.GetComponentsInChildren<Renderer>(true));
    var states=new List<DriftRendererState>();var frustum=GeometryUtility.CalculateFrustumPlanes(camera);var culling=GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix);
    foreach(var renderer in renderers){
      var materials=new List<string>();foreach(var material in renderer.sharedMaterials)materials.Add(material==null?"missing":material.name+" | "+material.shader.name+" | supported="+material.shader.isSupported+" | queue="+material.renderQueue+" | passes="+material.passCount);
      states.Add(new DriftRendererState{name=renderer.name,type=renderer.GetType().Name,shadowCasting=renderer.shadowCastingMode.ToString(),enabled=renderer.enabled,active=renderer.gameObject.activeInHierarchy,forceRenderingOff=renderer.forceRenderingOff,visible=renderer.isVisible,layerIncluded=(camera.cullingMask&(1<<renderer.gameObject.layer))!=0,layer=renderer.gameObject.layer,inFrustum=GeometryUtility.TestPlanesAABB(frustum,renderer.bounds),inCullingFrustum=GeometryUtility.TestPlanesAABB(culling,renderer.bounds),boundsCenter=renderer.bounds.center,boundsSize=renderer.bounds.size,materials=materials.ToArray()});
    }
    return states.ToArray();
  }
  void ObserveDriftView() {
   if(!driftViewProbe||Scene!=Movement.StillWater||!Resting||player.RestAmount<.99f||driftViewSamples>=2)return;
   if(driftViewSamples==1&&SceneElapsed<driftViewSampleAt+2.5f)return;
   Bounds bounds=leaf.GetComponent<MeshRenderer>().bounds;var corners=new List<Vector3>();
   for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)corners.Add(camera.WorldToViewportPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
   var observation=new DriftViewObservation{sample=driftViewSamples==0?"first-rest-frame":"settled-rest-frame",frame=Time.frameCount,sceneElapsed=SceneElapsed,fieldOfView=camera.fieldOfView,nearClip=camera.nearClipPlane,farClip=camera.farClipPlane,cameraPosition=camera.transform.position,cameraTarget=cameraTarget,cameraRotation=camera.transform.rotation,leafPosition=leaf.position,leafBoundsCenter=bounds.center,leafBoundsSize=bounds.size,bodyWorld=player.BodyFocus,bodyViewport=camera.WorldToViewportPoint(player.BodyFocus),mouthViewport=camera.WorldToViewportPoint(player.MouthPosition),leafCornerViewports=corners.ToArray()};
   observation.renderers=DriftRenderers();observation.projection=camera.projectionMatrix;observation.cullingMatrix=camera.cullingMatrix;
    observation.mesh=ObserveDriftMesh();
   Debug.Log("[RETURN drift-view] "+JsonUtility.ToJson(observation));if(driftViewSamples==0)driftViewSampleAt=SceneElapsed;driftViewSamples++;
  }
  public void UpdateCamera(float dt,bool reducedMotion) {
   if(!Active)return;Vector3 position,target;
    if(stanceReview!=null&&!stanceReview.Saved&&(Scene!=Movement.StillWater||coloursTime>=0))stanceReview.Save();
   if(reviewGroundSkin){
    groundSkinView=Scene==Movement.StillWater&&coloursTime<0&&!Resting&&!LeafBoarding&&(OnLeafSurface||OnIsland)&&player.GroundSkinReviewSupported;
    flatSkinControl.root.gameObject.SetActive(groundSkinView);
    if(groundSkinView){
     Vector3 offset=player.root.right*2.2f;float plane=player.PrepareFlatWalkingControl(flatSkinControl,offset);var normal=player.DescribeLegSkin();var flat=flatSkinControl.DescribeLegSkin();bool onLeaf=OnLeafSurface;
     var previous=onLeaf?SkinReview.leaf:SkinReview.island;if(onLeaf)SkinReview.leafFrames++;else SkinReview.islandFrames++;
     if(previous==null||normal.maximumStretch>previous.ground.maximumStretch){var sample=new GroundSkinComparison{surface=onLeaf?"leaf":"island",elapsed=SceneElapsed,ground=normal,flat=flat,groundPose=player.DescribeGroundPose(SurfaceHeight),flatPose=flatSkinControl.DescribeGroundPose(point=>plane)};if(onLeaf)SkinReview.leaf=sample;else SkinReview.island=sample;}
     Vector3 centre=(player.root.position+flatSkinControl.root.position)*.5f;focus.blur=0;camera.ResetProjectionMatrix();camera.orthographic=false;camera.fieldOfView=44;camera.transform.position=centre-player.root.forward*4.2f+Vector3.up*1.6f;camera.transform.LookAt(centre+Vector3.up*.85f);return;
    }
   }
   if(Scene==Movement.Hospital||Scene==Movement.Credits) {
    camera.ResetProjectionMatrix();camera.orthographic=false;camera.farClipPlane=280;
    float t=SceneElapsed;position=HospitalEyePosition;
    target=hospital.TransformPoint(new Vector3(.035f*Mathf.Sin(t*.22f),3.25f,.32f));
    camera.fieldOfView=Mathf.Lerp(68,64,Mathf.SmoothStep(0,1,t/4.1f));
   }else if(Scene==Movement.Reunion) {
     camera.ResetProjectionMatrix();camera.orthographic=false;camera.fieldOfView=60;focus.blur=0;
     if(!streetEntered){position=previousCamera;target=previousTarget;}
     else {target=root.TransformPoint(Position+Vector3.up*(streetCloseup ? .95f : 4.1f));position=target+(streetCloseup?new Vector3(0,.1f,-3.6f):new Vector3(0,1.1f,-18));reverse.FrameStreetView(position,root.TransformPoint(Position));}
   }else {
    float reveal=coloursTime<0?0:Mathf.SmoothStep(0,1,Colours)*.45f;
     float islandFraming=Mathf.SmoothStep(0,1,(Position.z-20f)/4.5f);
     float distance=Mathf.Lerp(Mathf.Lerp(7.8f,13,reveal),12.2f,islandFraming);
     bool swimming=!Resting&&player.RestAmount==0&&Position.y<-.25f&&SurfaceHeight(Position)<=-.48f;
     swimFraming=Mathf.MoveTowards(swimFraming,swimming?1:0,dt*1.5f);
     float swimBlend=Mathf.SmoothStep(0,1,swimFraming);
     distance=Mathf.Lerp(distance,5.2f,swimBlend);
     // Reframe through standing up at the dock, before the short walk starts.
     bool walkingAshore=OnIsland||(!Resting&&boarded&&LeafZ>=ReturnVolumeArt.LeafDockZ-.05f&&Position.y>=-.25f&&(OnLeaf(Position)||ReturnVolumeArt.IslandContains(Position.x,Position.z-26)));
     islandWalkFraming=Mathf.MoveTowards(islandWalkFraming,walkingAshore?Mathf.Clamp01(1-player.RestAmount):0,dt*2);
     float walkingBlend=Mathf.SmoothStep(0,1,islandWalkFraming);
     distance=Mathf.Lerp(distance,7.3f,walkingBlend);
     float dockSightline=1-Mathf.SmoothStep(0,1,(Position.z-(ReturnVolumeArt.LeafDockZ+ReturnVolumeArt.LeafHalfLength*ReturnVolumeArt.LeafPlanScale+.35f))/1.1f);
     float walkingPitch=Mathf.Min(62,Mathf.Max(8,pitch-9)+16*dockSightline);
     float viewPitch=Mathf.Lerp(Mathf.Lerp(pitch,Mathf.Max(12,pitch-5),swimBlend),walkingPitch,walkingBlend);
     Vector3 offset=Quaternion.Euler(viewPitch,Yaw,0)*new Vector3(0,0,-distance);
     float islandFocusZ=Mathf.Lerp(ReturnVolumeArt.LeafDockZ,26f,.52f);
     target=Vector3.Lerp(player.BodyFocus,root.TransformPoint(new Vector3(0,1.55f,islandFocusZ)),islandFraming);
     Vector3 swimFocus=player.BodyFocus+Vector3.up*.1f+Quaternion.Euler(0,Yaw,0)*Vector3.forward*.9f;
     target=Vector3.Lerp(target,swimFocus,swimBlend);
     Vector3 walkingFocus=player.BodyFocus+Vector3.up*.95f+Quaternion.Euler(0,Yaw,0)*Vector3.forward*.85f;
     walkingFocus.y=Mathf.Max(walkingFocus.y,Position.y+1.7f)+.40f*dockSightline;
     target=Vector3.Lerp(target,walkingFocus,walkingBlend);position=target+offset;
    float driftBlend=Mathf.SmoothStep(0,1,player.RestAmount)*(1-walkingBlend);
    if(driftBlend>0&&coloursTime<0){
     Bounds group=islandViewBounds;group.Encapsulate(leaf.GetComponent<MeshRenderer>().bounds);group.Encapsulate(player.BodyFocus);
     Quaternion view=Quaternion.Euler(viewPitch,Yaw,0);Vector3 driftTarget=group.center-view*Vector3.up*(group.extents.magnitude*.12f);
     float separation=Vector3.Distance(leaf.position,root.TransformPoint(new Vector3(0,0,26)));
     float driftDistance=Mathf.Max(separation*2.2f,DriftFramingDistance(group,driftTarget,view,30,camera.aspect));
     position=Vector3.Lerp(position,driftTarget-view*Vector3.forward*driftDistance,driftBlend);target=Vector3.Lerp(target,driftTarget,driftBlend);
    }
    camera.fieldOfView=Mathf.Lerp(53,30,driftBlend);float lerp=1-Mathf.Exp(-dt*(reducedMotion?9:4));position=Vector3.Lerp(camera.transform.position,position,lerp);target=Vector3.Lerp(cameraTarget,target,lerp);
    if(coloursTime>=0&&!reducedMotion){float close=Mathf.SmoothStep(0,1,coloursTime/1.35f);Vector3 fruitTarget=(player.BodyFocus+player.MouthPosition)*.5f+player.root.forward*.15f;Vector3 fruitView=fruitTarget+player.root.right*1.8f+player.root.forward*.12f+Vector3.up*.16f;position=Vector3.Lerp(previousCamera,fruitView,close);target=Vector3.Lerp(previousTarget,fruitTarget,close);camera.fieldOfView=Mathf.Lerp(previousFieldOfView,49,close);}
   }
   camera.transform.position=position;camera.transform.rotation=Quaternion.LookRotation(target-position,Vector3.up);cameraTarget=target;ObserveDriftView();
   Shader.SetGlobalVector("_ReturnLeafPosition",leaf.position);Shader.SetGlobalVector("_ReturnLeafContactPosition",leaf.TransformPoint(new Vector3(0,ReturnVolumeArt.LeafHeight(0,0),0)));Shader.SetGlobalVector("_ReturnBodyPosition",Position);Shader.SetGlobalFloat("_ReturnWaterTime",time);
   Shader.SetGlobalVector("_ReturnWaterBodyFocus",player.BodyFocus);
   Shader.SetGlobalVector("_ReturnWaterBodyMotion",new Vector4(Velocity.x,Velocity.z,0,Scene==Movement.StillWater&&!Resting&&Position.y<-.25f?1:0));
   Shader.SetGlobalVector("_ReturnWaterLeafMotion",new Vector4(leafVelocity.x,leafVelocity.z,2.1f*ReturnVolumeArt.LeafPlanScale,2.9f*ReturnVolumeArt.LeafPlanScale));
   bool swimmingContact=Scene==Movement.StillWater&&!Resting&&player.RestAmount==0&&Position.y<-.25f;
   Shader.SetGlobalVector("_ReturnWaterHandLeft",WaterHandState(player.LeftPalmPosition,ref previousWaterLeftPalm,ref leftWaterContact,dt,swimmingContact));
   Shader.SetGlobalVector("_ReturnWaterHandRight",WaterHandState(player.RightPalmPosition,ref previousWaterRightPalm,ref rightWaterContact,dt,swimmingContact));
   Shader.SetGlobalVector("_ReturnWaterPelvis",player.PelvisPosition);
   Shader.SetGlobalVector("_ReturnWaterKneeLeft",player.LeftKneePosition);Shader.SetGlobalVector("_ReturnWaterKneeRight",player.RightKneePosition);
   Vector3 leftLocal=player.LeftFootLocalPosition,rightLocal=player.RightFootLocalPosition;
   float leftSpeed=swimmingContact&&waterHandsSampled&&dt>.0001f?Mathf.Min(3,Vector3.Distance(leftLocal,previousWaterLeftFoot)/dt):0;
   float rightSpeed=swimmingContact&&waterHandsSampled&&dt>.0001f?Mathf.Min(3,Vector3.Distance(rightLocal,previousWaterRightFoot)/dt):0;
   Vector3 leftFoot=player.LeftFootPosition,rightFoot=player.RightFootPosition;
   Shader.SetGlobalVector("_ReturnWaterFootLeft",new Vector4(leftFoot.x,leftFoot.y,leftFoot.z,leftSpeed));
   Shader.SetGlobalVector("_ReturnWaterFootRight",new Vector4(rightFoot.x,rightFoot.y,rightFoot.z,rightSpeed));
   previousWaterLeftFoot=leftLocal;previousWaterRightFoot=rightLocal;SwimFootSpeed=Mathf.Max(leftSpeed,rightSpeed);waterHandsSampled=true;
   focus.Apply();
  }
  public void RetrySafe(){if(LeafBoarding)player.root.localRotation=Quaternion.Euler(0,player.root.localEulerAngles.y,0);leafBoardTime=-1;leafBoardCompleted=false;quietWalkUntil=0;Position=safePosition;Velocity=Vector3.zero;Resting=false;player.ResetRest();onLeaf=OnLeaf(Position);}
  public string Hint() {
    if(Scene==Movement.Reunion)return openingDoor?"A way home.":CanDoor?"E  ·  Step through the light":StreetPlayable?"A / D  ·  Walk through the recovered streets":SceneElapsed<1.2f?"":SceneElapsed<3.2f?"Yellow returns.":SceneElapsed<5.2f?"Red returns.":"Blue returns.";
   if(coloursTime>=0)return "";
   if(LeafBoarding)return "Climbing onto the leaf";
   if(CanFruit)return "E  ·  Take the blue fruit";
   if(Resting)return LeafZ<ReturnVolumeArt.LeafDockZ-.3f?"The leaf carries you. Right-drag to look around. Move or Space to rise.":"W A S D  ·  Step onto the island";
   if(Position.z>ReturnVolumeArt.LeafDockZ+1||(LeafZ>=ReturnVolumeArt.LeafDockZ-.3f&&OnLeaf(Position)))return "W A S D  ·  Step onto the island and walk to the tree";
   if(CanRest)return leafDrifting?"E  ·  Rest on the green leaf":"E  ·  Lie down and let the leaf carry you";
   if(boarded&&!leafDrifting)return "Walk to the middle of the leaf, then stop to rest.";
   if(!boarded)return "W A S D  ·  Swim to the leaf     Space  ·  Rise     Right-drag  ·  Look";
   return "W A S D  move     Right-drag  look around     Space  jump / rise";
  }
  public void Hide() {
   if(sky!=null)sky.Restore();
   Active=false;if(root!=null)root.gameObject.SetActive(false);focus.enabled=false;focus.white=0;camera.ResetProjectionMatrix();camera.orthographic=true;camera.fieldOfView=53;camera.farClipPlane=100;camera.transform.rotation=Quaternion.identity;
    QualitySettings.renderPipeline=originalPipeline;
    cameraData.requiresDepthOption=originalDepthOption;
    cameraData.antialiasing=originalAntialiasing;cameraData.antialiasingQuality=originalAntialiasingQuality;
   RenderSettings.fog=originalFog;RenderSettings.ambientMode=originalAmbientMode;RenderSettings.ambientLight=originalAmbient;RenderSettings.ambientSkyColor=originalAmbientSky;RenderSettings.ambientEquatorColor=originalAmbientEquator;RenderSettings.ambientGroundColor=originalAmbientGround;RenderSettings.fogMode=originalFogMode;RenderSettings.fogColor=originalFogColour;RenderSettings.fogDensity=originalFogDensity;
  }
  public void Dispose(){Hide();if(sky!=null){sky.Dispose();sky=null;}reverse?.Dispose();reverse=null;if(root!=null)Object.DestroyImmediate(root.gameObject);art.Dispose();foreach(var m in owned)Object.DestroyImmediate(m);}
 }
}
