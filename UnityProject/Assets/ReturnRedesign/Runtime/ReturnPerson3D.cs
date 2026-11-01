using System.Collections.Generic;
using UnityEngine;
namespace Return.Redesign {
 // Imported anatomical skin, cloth, hair and eyes share the CC0 game rig.
 public sealed class ReturnPerson3D {
  public readonly Transform root;
  readonly ReturnTravellerStyle travellerStyle;
  public bool QuietTraveller=>travellerStyle!=null;
  public bool TravellerHandsVisible=>travellerStyle!=null&&travellerStyle.HandsVisible;
  public bool TravellerVolumeVisible=>travellerStyle!=null&&travellerStyle.VolumeVisible;
  public bool TravellerDetailsConcealed=>travellerStyle!=null&&travellerStyle.DetailsConcealed;
  readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
  readonly Dictionary<string,Quaternion> bind=new Dictionary<string,Quaternion>();
  readonly Dictionary<string,Vector3> bindPosition=new Dictionary<string,Vector3>();
  readonly Dictionary<string,Vector3> restPosition=new Dictionary<string,Vector3>();readonly Dictionary<string,Quaternion> restRotation=new Dictionary<string,Quaternion>();
  readonly Dictionary<string,Vector3> flexAxis=new Dictionary<string,Vector3>(),elbowAxis=new Dictionary<string,Vector3>();
  readonly Dictionary<string,Quaternion> handBasis=new Dictionary<string,Quaternion>();
  readonly List<SkinnedMeshRenderer> faces=new List<SkinnedMeshRenderer>();
  readonly Dictionary<SkinnedMeshRenderer,List<int>> viewFaceVertices=new Dictionary<SkinnedMeshRenderer,List<int>>(),viewArmVertices=new Dictionary<SkinnedMeshRenderer,List<int>>();
  readonly List<Vector3> restSupport=new List<Vector3>();readonly Dictionary<string,float> heelOffset=new Dictionary<string,float>(),palmSign=new Dictionary<string,float>();
  readonly Dictionary<SkinnedMeshRenderer,HashSet<int>>[] handVertices={new Dictionary<SkinnedMeshRenderer,HashSet<int>>(),new Dictionary<SkinnedMeshRenderer,HashSet<int>>()};
  readonly Dictionary<SkinnedMeshRenderer,HashSet<int>>[] footVertices={new Dictionary<SkinnedMeshRenderer,HashSet<int>>(),new Dictionary<SkinnedMeshRenderer,HashSet<int>>()};
  readonly List<Vector3>[] footSupport={new List<Vector3>(),new List<Vector3>()};
  readonly Vector3[] walkingHeelPivot=new Vector3[2],walkingToePivot=new Vector3[2];
  readonly Dictionary<SkinnedMeshRenderer,Mesh> shoePoseMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
  readonly Dictionary<SkinnedMeshRenderer,List<Vector3>> shoePoseVertices=new Dictionary<SkinnedMeshRenderer,List<Vector3>>();
  readonly bool measureShoeContact=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-measure-update")>=0;
  int measuredContactCalls,measuredContactBakes;
  double measuredContactMs,measuredContactBakeMs;
  readonly Dictionary<SkinnedMeshRenderer,Mesh> boardingHandPoseMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
  readonly Dictionary<SkinnedMeshRenderer,List<Vector3>> boardingHandPoseVertices=new Dictionary<SkinnedMeshRenderer,List<Vector3>>();
  readonly Dictionary<string,Quaternion> boardingStartBones=new Dictionary<string,Quaternion>();
  readonly Vector3[] boardingStartFeet=new Vector3[2],boardingStartHands=new Vector3[2];readonly Quaternion[] boardingStartFootRotations=new Quaternion[2];
  Vector3 boardingRootOffset;Quaternion boardingStartRotation;
  const float SwimExitDuration=.85f;
  readonly Dictionary<string,Quaternion> swimExitBones=new Dictionary<string,Quaternion>();
  readonly Dictionary<string,Vector3> swimExitBonePositions=new Dictionary<string,Vector3>();
  readonly Vector3[] swimExitFeet=new Vector3[2];readonly Quaternion[] swimExitFootRotations=new Quaternion[2];
  Vector3 lastSwimPosition,swimExitRootOffset;Quaternion swimExitRootRotation;float swimExitElapsed=SwimExitDuration;bool applyingSwimPose;
  readonly float[] handClearance=new float[2];bool handsCalibrated;
  readonly Vector3[] plantedFoot=new Vector3[2],swingStart=new Vector3[2],swingEnd=new Vector3[2];readonly bool[] swinging=new bool[2];
  readonly bool traceStreetLandings=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-trace-street-landings")>=0;
  readonly Vector3[] streetRawLanding=new Vector3[2],streetLaneShift=new Vector3[2];Vector3 streetPreviousVelocity;float streetPoseDt;bool streetReversed;
  readonly float[] groundReplantElapsed={-1,-1},groundReplantDuration=new float[2],groundSwingProgressStart=new float[2];readonly bool[] groundLandingHeld=new bool[2],groundSkipHeelStrike=new bool[2],groundLateSwing=new bool[2];
  readonly Vector3[] previousSolvedFoot=new Vector3[2];readonly bool[] groundLiftStarting=new bool[2];
  readonly Quaternion[] previousSolvedFootHeading=new Quaternion[2],groundReplantStartHeading=new Quaternion[2],groundTerrainHeadings=new Quaternion[2];
  float groundStrideScale=1;
  sealed class GroundContact {public bool valid;public SkinnedMeshRenderer renderer;public int vertex;public Vector3 anchor;}
  readonly GroundContact[] groundContacts={new GroundContact(),new GroundContact()};readonly bool[] groundContactWalking=new bool[2];
  readonly Vector3[] groundLowestWorld=new Vector3[2],groundHeldWorld=new Vector3[2];readonly SkinnedMeshRenderer[] groundLowestRenderer=new SkinnedMeshRenderer[2];readonly int[] groundLowestVertex=new int[2];readonly float[] groundHeldGap=new float[2];Vector3 groundSupportTranslation;
  readonly float[] maximumPlannedLiftGap=new float[2],maximumActualLiftStep=new float[2];readonly int[] walkingLiftStartCount=new int[2];
  int walkingEpisode;
   float previousTime,gait,faceLevel,concernLevel,reliefLevel,poseBlink,walkWeight,groundDrop,swimCycle,swimEffort;readonly float phaseOffset,walkingLegLength;bool wasResting,wasWalking,wasSwimming;Quaternion restHeading=Quaternion.identity,restStartHeading=Quaternion.identity,restTargetHeading=Quaternion.identity,gazeTurn=Quaternion.identity;
   Vector2 maximumRequestedSwimAnkleAngles,maximumAppliedSwimAnkleAngles;int swimAnkleLimitCount;
  public float RestAmount {get;private set;}
  public float StandingHeightOffset {get;private set;}
   readonly Vector3[] groundPoseTargets=new Vector3[2],groundPoseHints=new Vector3[2];readonly Quaternion[] groundPoseHeadings=new Quaternion[2],groundDesiredFootHeadings=new Quaternion[2];readonly float[] groundPoseClearance=new float[2];bool lastPoseAirborne,lastGroundSurfaceProvided,constrainGroundLegPose,constrainGroundAnklePose,levelGroundPose;
  Vector2 maximumRequestedLegTwist,maximumAppliedLegTwist,maximumRequestedAnkleAngles;int groundFootReachLimitCount,groundLegTwistLimitCount,groundAnkleLimitCount;
  sealed class LegSkinEdge {public int a,b;public float length;public Vector3 restA,restB;}
  readonly Dictionary<SkinnedMeshRenderer,List<LegSkinEdge>> legSkinEdges=new Dictionary<SkinnedMeshRenderer,List<LegSkinEdge>>();
  Vector3 lastSkinVelocity;float lastSkinTime;
  [System.Serializable] public sealed class LegSkinReport {public int edges;public float maximumStretch,minimumRatio=1;public string renderer;public int vertexA,vertexB;public Vector3 restA,restB,posedA,posedB;}
  public bool GroundSkinReviewSupported=>constrainGroundAnklePose&&new Vector2(lastSkinVelocity.x,lastSkinVelocity.z).magnitude>.2f;
  public bool GroundWalkReviewActive=>constrainGroundAnklePose&&wasWalking&&walkWeight>.99f;
  public bool TryGroundStanceKey(int leg,out int episode,out int cycle){episode=walkingEpisode;cycle=Mathf.FloorToInt(gait/(2*Mathf.PI)+(leg==1?.5f:0));return constrainGroundLegPose&&wasWalking&&walkWeight>.99f&&!swinging[leg];}
  [System.Serializable] public sealed class GroundStanceState {public float phase,recordedRoll,groundDrop,strideScale,headingDifference,replantElapsed;public bool contactHeld,correctiveSwing,lateSwing;public Vector2 requestedAnkle,appliedAnkle;public Vector3 plant,target,actual,hip,knee,pelvis,terrainNormal,desiredSoleUp,actualSoleUp,bodyRight,contactAnchor,rootPosition,velocity;public Quaternion actualHeading;}
  public void DescribeStreetLanding(int leg,out Vector3 raw,out Vector3 planned,out Vector3 laneShift,out Vector3 previousVelocity,out float poseDt,out int episode,out int cycle,out bool swing,out bool reversed){raw=streetRawLanding[leg];planned=swingEnd[leg];laneShift=streetLaneShift[leg];previousVelocity=streetPreviousVelocity;poseDt=streetPoseDt;episode=walkingEpisode;cycle=Mathf.FloorToInt(gait/(Mathf.PI*2)+(leg==1?.5f:0));swing=swinging[leg];reversed=streetReversed;}
  public Vector3 GroundBodyForward=>root.forward;
  public float TorsoForwardLean=>Vector3.SignedAngle(root.up,Vector3.ProjectOnPlane(B("head").position-B("pelvis").position,root.right),root.right);
  public Vector2 DescribeAbsoluteLegTwist(int leg){string suffix=leg==0?"_r":"_l";return new Vector2(AbsoluteHingeTwist(B("thigh"+suffix),B("calf"+suffix)),AbsoluteHingeTwist(B("calf"+suffix),B("foot"+suffix)));}
  float AbsoluteHingeTwist(Transform bone,Transform child){
   if(!elbowAxis.TryGetValue(bone.name,out var axis))return 0;Vector3 along=(child.position-bone.position).normalized;
   Vector3 reference=Vector3.ProjectOnPlane(root.rotation*restRotation[bone.name]*axis,along),current=Vector3.ProjectOnPlane(bone.rotation*axis,along);
   return reference.sqrMagnitude>.00001f&&current.sqrMagnitude>.00001f?Mathf.Abs(Vector3.SignedAngle(reference,current,along)):0;
  }
  public GroundStanceState DescribeGroundStance(int leg){
   string suffix=leg==0?"_r":"_l";Transform foot=B("foot"+suffix);float phase=Mathf.Repeat(gait/(Mathf.PI*2)+(leg==1?.5f:0),1);Quaternion actual=foot.rotation*Quaternion.Inverse(restRotation["foot"+suffix]);
   float roll=GroundWalkRoll(leg,phase);Quaternion desired=groundDesiredFootHeadings[leg],terrain=groundTerrainHeadings[leg];
    return new GroundStanceState{phase=phase,recordedRoll=roll,groundDrop=groundDrop,strideScale=groundStrideScale,headingDifference=Quaternion.Angle(desired,actual),requestedAnkle=GroundAnkleAngles(leg,desired,out _),appliedAnkle=GroundAnkleAngles(leg,actual,out _),plant=plantedFoot[leg],target=groundPoseTargets[leg],actual=foot.position,hip=B("thigh"+suffix).position,knee=B("calf"+suffix).position,pelvis=B("pelvis").position,terrainNormal=terrain*Vector3.up,desiredSoleUp=desired*Vector3.up,actualSoleUp=actual*Vector3.up,bodyRight=root.right,actualHeading=actual,contactHeld=groundContacts[leg].valid,correctiveSwing=groundReplantElapsed[leg]>=0,lateSwing=groundLateSwing[leg],replantElapsed=groundReplantElapsed[leg],contactAnchor=groundContacts[leg].anchor,rootPosition=root.position,velocity=root.parent==null?lastSkinVelocity:root.parent.TransformVector(lastSkinVelocity)};
  }
  [System.Serializable] public sealed class GroundFootReport {public string side;public Vector3 target,actual,hip,knee;public Quaternion targetHeading,actualRotation;public float targetSurface,actualSurface,minimumShoeGap,targetError,kneeFlexDegrees,anklePitchDegrees,ankleRollDegrees,maximumPlannedLiftGap,maximumActualLiftStep;public int walkingLiftStartCount;}
   [System.Serializable] public sealed class GroundPoseReport {public string label,scene;public bool active,airborne,groundSurfaceProvided;public float sceneElapsed,fruitActionTime,gait,walkWeight,groundDrop,standingOffset,pelvisYaw,chestYaw;public Vector2 maximumRequestedLegTwist,maximumAppliedLegTwist,maximumRequestedAnkleAngles;public int groundFootReachLimitCount,groundLegTwistLimitCount,groundAnkleLimitCount;public Vector3 rootPosition,pelvisPosition,pelvisOffsetLocal;public Quaternion rootRotation;public GroundFootReport[] feet;}
   float PosedYaw(string bone){Quaternion delta=B(bone).rotation*Quaternion.Inverse(root.rotation*restRotation[bone]);delta.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;return angle*Vector3.Dot(axis,root.up);}
  public GroundPoseReport DescribeGroundPose(System.Func<Vector3,float> surface){
   Vector2 clearance=MeasureShoeClearance(surface);var feet=new GroundFootReport[2];for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";Vector3 hip=B("thigh"+suffix).position,knee=B("calf"+suffix).position,foot=B("foot"+suffix).position;Vector2 ankle=GroundAnkleAngles(leg,B("foot"+suffix).rotation*Quaternion.Inverse(restRotation["foot"+suffix]),out var neutral);feet[leg]=new GroundFootReport{side=suffix,target=groundPoseTargets[leg],actual=foot,hip=hip,knee=knee,targetHeading=groundPoseHeadings[leg],actualRotation=B("foot"+suffix).rotation,targetSurface=surface(groundPoseTargets[leg]),actualSurface=surface(foot),minimumShoeGap=leg==0?clearance.x:clearance.y,targetError=Vector3.Distance(groundPoseTargets[leg],foot),kneeFlexDegrees=180-Vector3.Angle(hip-knee,foot-knee),anklePitchDegrees=ankle.x,ankleRollDegrees=ankle.y,maximumPlannedLiftGap=maximumPlannedLiftGap[leg],maximumActualLiftStep=maximumActualLiftStep[leg],walkingLiftStartCount=walkingLiftStartCount[leg]};}
    return new GroundPoseReport{active=root.gameObject.activeInHierarchy,airborne=lastPoseAirborne,groundSurfaceProvided=lastGroundSurfaceProvided,gait=gait,walkWeight=walkWeight,groundDrop=groundDrop,standingOffset=StandingHeightOffset,maximumRequestedLegTwist=maximumRequestedLegTwist,maximumAppliedLegTwist=maximumAppliedLegTwist,maximumRequestedAnkleAngles=maximumRequestedAnkleAngles,groundFootReachLimitCount=groundFootReachLimitCount,groundLegTwistLimitCount=groundLegTwistLimitCount,groundAnkleLimitCount=groundAnkleLimitCount,rootPosition=root.position,pelvisPosition=B("pelvis").position,pelvisOffsetLocal=root.InverseTransformPoint(B("pelvis").position)-restPosition["pelvis"],pelvisYaw=PosedYaw("pelvis"),chestYaw=PosedYaw("spine_03"),rootRotation=root.rotation,feet=feet};
  }

  public Vector3 BodyFocus=>B("spine_01").position;
  Vector3 mouthLocal;
  public int MouthVertexSamples {get;private set;}
  public Vector3 MouthPosition=>B("head").TransformPoint(mouthLocal);
  public Vector3 RightPalmPosition=>(B("index_01_r").position+B("pinky_01_r").position)*.5f;
  public Vector3 LeftPalmPosition=>(B("index_01_l").position+B("pinky_01_l").position)*.5f;
  public Vector3 PelvisPosition=>B("pelvis").position;
  public Vector3 LeftKneePosition=>B("calf_l").position;
  public Vector3 RightKneePosition=>B("calf_r").position;
  public Vector3 LeftFootPosition=>B("foot_l").position;
  public Vector3 RightFootPosition=>B("foot_r").position;
  public Vector3 LeftFootLocalPosition=>root.InverseTransformPoint(LeftFootPosition);
  public Vector3 RightFootLocalPosition=>root.InverseTransformPoint(RightFootPosition);
  public float SwimPhase=>swimCycle;
  public Vector4 SwimPosture=>new Vector4(B("head").position.y,B("pelvis").position.y,B("spine_03").position.y,Vector3.Angle(root.up,Vector3.up));
  public Vector4 SwimShape=>new Vector4(Vector3.Distance(LeftPalmPosition,RightPalmPosition),Vector3.Distance(B("foot_l").position,B("foot_r").position),Vector3.Dot((LeftPalmPosition+RightPalmPosition)*.5f-root.position,root.up),Vector3.Dot((B("foot_l").position+B("foot_r").position)*.5f-root.position,root.up));
  public ReturnPerson3D(ReturnVolumeArt art,Transform parent,string name,Material clothes,Material skin,Material hair,bool protagonist=false,int hairStyle=0,bool cacheFaceView=false,bool quietTraveller=false) {
   string asset=protagonist?"Dreamer":hairStyle==2?"Mother":"Nurse";
   phaseOffset=protagonist?0:hairStyle==2?1.37f:2.61f;
   root=art.Model(asset,parent,Vector3.zero);root.name=name;
   if(protagonist){
    var kneeSkin=Resources.Load<Mesh>("ReturnRealism/Dreamer_KneeSkin");if(kneeSkin==null)throw new System.InvalidOperationException("Dreamer knee skin missing");
    bool assigned=false;foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())if(renderer.name=="male_casualsuit02"){renderer.sharedMesh=kneeSkin;assigned=true;}
    if(!assigned)throw new System.InvalidOperationException("Dreamer trouser renderer missing");
   }
   foreach(var t in root.GetComponentsInChildren<Transform>()){if(t!=root&&!bones.ContainsKey(t.name)){bones[t.name]=t;bind[t.name]=t.localRotation;bindPosition[t.name]=t.localPosition;restPosition[t.name]=root.InverseTransformPoint(t.position);restRotation[t.name]=Quaternion.Inverse(root.rotation)*t.rotation;}}
   walkingLegLength=Vector3.Distance(restPosition["thigh_r"],restPosition["calf_r"])+Vector3.Distance(restPosition["calf_r"],restPosition["foot_r"]);
   if(protagonist)ReturnRecordedWalk.EnsureLoaded();
   foreach(string suffix in new[]{"_l","_r"}){
    Vector3 wrist=restPosition["hand"+suffix],index=restPosition["index_01"+suffix]-wrist,pinky=restPosition["pinky_01"+suffix]-wrist;
    Vector3 palm=Vector3.Cross(index,pinky).normalized;float sign=Vector3.Dot(palm,Vector3.right*-Mathf.Sign(wrist.x))<0?-1:1;palmSign[suffix]=sign;palm*=sign;
    Vector3 knuckles=((index+pinky)*.5f).normalized;Quaternion toLocal=Quaternion.Inverse(restRotation["hand"+suffix]);
    handBasis[suffix]=Quaternion.LookRotation(toLocal*knuckles,toLocal*-palm);
    Vector3 hinge=Vector3.Cross(restPosition["lowerarm"+suffix]-restPosition["upperarm"+suffix],wrist-restPosition["lowerarm"+suffix]).normalized;
    foreach(string arm in new[]{"upperarm","lowerarm"})elbowAxis[arm+suffix]=Quaternion.Inverse(restRotation[arm+suffix])*hinge;
     Vector3 kneeHinge=Vector3.Cross(Vector3.forward,restPosition["foot"+suffix]-restPosition["thigh"+suffix]).normalized;
     foreach(string leg in new[]{"thigh","calf"})elbowAxis[leg+suffix]=Quaternion.Inverse(restRotation[leg+suffix])*kneeHinge;
    foreach(string finger in new[]{"index","middle","ring","pinky","thumb"})for(int joint=1;joint<=3;joint++){
     string jointName=finger+"_0"+joint+suffix;Vector3 direction=joint<3?restPosition[finger+"_0"+(joint+1)+suffix]-restPosition[jointName]:restPosition[jointName]-restPosition[finger+"_02"+suffix];
     flexAxis[jointName]=Quaternion.Inverse(restRotation[jointName])*Vector3.Cross(direction,palm).normalized;
    }
   }
   foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>()){r.updateWhenOffscreen=true;if(ShapeIndex(r,"Blink")>=0)faces.Add(r);}
   // Bedside figures also use grounded walking and need their actual shoe-contact geometry.
   CacheRestSupport();CacheShoePoseMeshes(art);
   if(protagonist&&(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-review-ground-skin")>=0||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--return-review-motion")>=0))CacheLegSkinEdges();
   if(protagonist)CalibrateMouth();
   if(cacheFaceView||!protagonist&&hairStyle==2)CacheViewSilhouettes();
   if(protagonist&&quietTraveller)travellerStyle=new ReturnTravellerStyle(art,root,bones,restRotation,root.InverseTransformPoint(MouthPosition));
  }
  void CacheLegSkinEdges(){
   foreach(var entry in shoePoseMeshes){
    bool visibleLegMaterial=false;foreach(var material in entry.Key.sharedMaterials)if(material!=null&&(material.name.StartsWith("Dreamer_Clothes")||material.name.StartsWith("Dreamer_Shoes")))visibleLegMaterial=true;
    if(!visibleLegMaterial)continue;entry.Key.BakeMesh(entry.Value,false);var vertices=shoePoseVertices[entry.Key];entry.Value.GetVertices(vertices);var triangles=entry.Value.triangles;
    var edges=new List<LegSkinEdge>();var seen=new HashSet<ulong>();
    for(int t=0;t<triangles.Length;t+=3)for(int corner=0;corner<3;corner++){
     int a=triangles[t+corner],b=triangles[t+(corner+1)%3];if(a>b){int swap=a;a=b;b=swap;}ulong key=((ulong)(uint)a<<32)|(uint)b;if(!seen.Add(key))continue;
     Vector3 pa=root.InverseTransformPoint(entry.Key.transform.TransformPoint(vertices[a])),pb=root.InverseTransformPoint(entry.Key.transform.TransformPoint(vertices[b]));
     if(pa.y<.025f||pb.y<.025f||pa.y>.73f||pb.y>.73f||Mathf.Abs(pa.x)<.035f||Mathf.Abs(pb.x)<.035f||pa.x*pb.x<=0)continue;
     float length=Vector3.Distance(pa,pb);if(length<.006f)continue;edges.Add(new LegSkinEdge{a=a,b=b,length=length,restA=pa,restB=pb});
    }
    if(edges.Count>0)legSkinEdges.Add(entry.Key,edges);
   }
   if(legSkinEdges.Count==0)throw new System.InvalidOperationException("Visible leg skin review geometry missing");
  }
  public LegSkinReport DescribeLegSkin(){
   var report=new LegSkinReport();foreach(var entry in legSkinEdges){var mesh=shoePoseMeshes[entry.Key];entry.Key.BakeMesh(mesh,false);var vertices=shoePoseVertices[entry.Key];mesh.GetVertices(vertices);
    foreach(var edge in entry.Value){Vector3 a=root.InverseTransformPoint(entry.Key.transform.TransformPoint(vertices[edge.a])),b=root.InverseTransformPoint(entry.Key.transform.TransformPoint(vertices[edge.b]));float ratio=Vector3.Distance(a,b)/edge.length;report.edges++;report.minimumRatio=Mathf.Min(report.minimumRatio,ratio);
     if(ratio>report.maximumStretch){report.maximumStretch=ratio;report.renderer=entry.Key.name;report.vertexA=edge.a;report.vertexB=edge.b;report.restA=edge.restA;report.restB=edge.restB;report.posedA=a;report.posedB=b;}
    }
   }return report;
  }
  [System.Serializable] public sealed class SwimFootReport {public float phase;public Vector2 maximumRequestedAnkleAngles,maximumAppliedAnkleAngles;public int ankleLimitCount;public Vector2[] ankleAngles;public float[] toeAngles;public LegSkinReport skin;}
  public SwimFootReport DescribeSwimFeet(){
   var ankles=new Vector2[2];var toes=new float[2];
   for(int leg=0;leg<2;leg++){
    string suffix=leg==0?"_r":"_l",toe="ball"+suffix;ankles[leg]=GroundAnkleAngles(leg,B("foot"+suffix).rotation*Quaternion.Inverse(restRotation["foot"+suffix]),out var neutral);
    if(B(toe)!=null){Quaternion delta=Quaternion.Inverse(bind[toe])*B(toe).localRotation;delta.ToAngleAxis(out float angle,out Vector3 axis);if(angle>180)angle-=360;toes[leg]=angle*Vector3.Dot(axis,Quaternion.Inverse(restRotation[toe])*Vector3.right);}
   }
   return new SwimFootReport{phase=swimCycle,maximumRequestedAnkleAngles=maximumRequestedSwimAnkleAngles,maximumAppliedAnkleAngles=maximumAppliedSwimAnkleAngles,ankleLimitCount=swimAnkleLimitCount,ankleAngles=ankles,toeAngles=toes,skin=DescribeLegSkin()};
  }
  public float PrepareFlatWalkingControl(ReturnPerson3D control,Vector3 worldOffset){
   control.gait=gait;control.walkWeight=walkWeight;control.previousTime=lastSkinTime;control.wasWalking=wasWalking;control.wasResting=false;control.RestAmount=0;control.groundDrop=0;control.wasSwimming=false;control.swimExitElapsed=SwimExitDuration;control.root.localRotation=root.localRotation;
   for(int leg=0;leg<2;leg++){control.plantedFoot[leg]=plantedFoot[leg]+worldOffset;control.swingStart[leg]=swingStart[leg]+worldOffset;control.swingEnd[leg]=swingEnd[leg]+worldOffset;control.swinging[leg]=swinging[leg];}
   float plane=root.position.y-StandingHeightOffset;Vector3 localOffset=root.parent==null?worldOffset:root.parent.InverseTransformVector(worldOffset);
   control.Pose(root.localPosition+localOffset,lastSkinVelocity,false,false,lastSkinTime,facing:root.localEulerAngles.y,groundSurface:point=>plane,quietGroundWalk:true);return plane;
  }
  void VisitMesh(System.Action<Vector3,Vector3> visit)=>VisitMesh((r,i,p,w)=>visit(p,w));
  void VisitMesh(System.Action<SkinnedMeshRenderer,int,Vector3,Vector3> visit) {
   foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
    var mesh=new Mesh();try{renderer.BakeMesh(mesh,false);var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++){Vector3 world=renderer.transform.TransformPoint(vertices[i]);visit(renderer,i,root.InverseTransformPoint(world),world);}}finally{Object.DestroyImmediate(mesh);}
   }
  }
  static float SegmentDistanceSquared(Vector3 p,Vector3 a,Vector3 b){Vector3 d=b-a;float t=d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude);return (p-a-d*t).sqrMagnitude;}
  void CacheViewSilhouettes(){
   Vector3 head=restPosition["head"],elbow=restPosition["lowerarm_r"],wrist=restPosition["hand_r"],fingers=restPosition["middle_03_r"];
   // Select the rest-mesh vertices spatially: the imported mesh stays non-readable at runtime.
   // BakeMesh preserves their indices, so later checks use actual deformed skin and cloth.
   VisitMesh((renderer,index,p,world)=>{
    if(p.y>=head.y-.09f&&p.y<head.y+.27f&&new Vector2(p.x-head.x,p.z-head.z).sqrMagnitude<.0324f){if(!viewFaceVertices.TryGetValue(renderer,out var ids)){ids=new List<int>();viewFaceVertices[renderer]=ids;}ids.Add(index);}
    if(SegmentDistanceSquared(p,elbow,wrist)<.007225f||SegmentDistanceSquared(p,wrist,fingers)<.011025f){if(!viewArmVertices.TryGetValue(renderer,out var ids)){ids=new List<int>();viewArmVertices[renderer]=ids;}ids.Add(index);}
   });
  }
  static bool ExtendView(Camera view,Vector3 world,ref Vector2 minimum,ref Vector2 maximum){Vector3 p=view.WorldToViewportPoint(world);if(p.z<=view.nearClipPlane)return false;Vector2 point=new Vector2(p.x*1280,p.y*720);minimum=Vector2.Min(minimum,point);maximum=Vector2.Max(maximum,point);return true;}
  public bool FaceClearOfRightArm(Camera view,out float gap){
   Vector2 faceMin=Vector2.one*10000,faceMax=Vector2.one*-10000,armMin=faceMin,armMax=faceMax;int faceCount=0,armCount=0;bool visible=true;
   foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()){
    bool face=viewFaceVertices.TryGetValue(renderer,out var faceIds),arm=viewArmVertices.TryGetValue(renderer,out var armIds);if(!face&&!arm)continue;
    var mesh=new Mesh();try{renderer.BakeMesh(mesh,false);var vertices=mesh.vertices;
     if(face)foreach(int index in faceIds){visible&=ExtendView(view,renderer.transform.TransformPoint(vertices[index]),ref faceMin,ref faceMax);faceCount++;}
     if(arm)foreach(int index in armIds){visible&=ExtendView(view,renderer.transform.TransformPoint(vertices[index]),ref armMin,ref armMax);armCount++;}
    }finally{Object.DestroyImmediate(mesh);}
   }
   gap=Mathf.Max(Mathf.Max(faceMin.x-armMax.x,armMin.x-faceMax.x),Mathf.Max(faceMin.y-armMax.y,armMin.y-faceMax.y));
    return visible&&faceCount>=100&&armCount>=100&&gap>=6;
   }
  [System.Serializable] public sealed class FaceArmClearanceReport {
   public float sceneElapsed,rectangleGapPixels,minimumTestedTriangleGapPixels=-1;
   public bool rectangleGatePassed,geometryAvailable,geometryControlsPassed,allSelectedVerticesInFront,triangleClearanceAtLeastSixPixels;
   public bool acceptedUsingProjectedTriangles;public float certifiedGapLowerBoundPixels;
   public int faceVertexCount,armVertexCount,faceTriangleCount,armTriangleCount,testedTrianglePairs;
   public Vector2 faceMin,faceMax,armMin,armMax,faceTriangleA,faceTriangleB,faceTriangleC,armTriangleA,armTriangleB,armTriangleC;
   public string faceRenderer,armRenderer,geometryError;
   public string scope="Selected deformed face and arm mesh patches projected at 1280x720; all triangles touching a selected vertex are included. Accepted fallback gaps are certified lower bounds; the exact global minimum distance is not measured.";
  }
  struct ProjectedTriangle {
   public Vector2 a,b,c,min,max;public string renderer;
   public ProjectedTriangle(Vector2 a,Vector2 b,Vector2 c,string renderer){this.a=a;this.b=b;this.c=c;this.renderer=renderer;min=Vector2.Min(a,Vector2.Min(b,c));max=Vector2.Max(a,Vector2.Max(b,c));}
   public Vector2 Point(int i)=>i==0?a:i==1?b:c;
  }
  List<ProjectedTriangle> ProjectedPatch(Camera view,Dictionary<SkinnedMeshRenderer,List<int>> selected,out int count,out bool inFront,out Vector2 minimum,out Vector2 maximum) {
   var result=new List<ProjectedTriangle>();count=0;inFront=true;minimum=Vector2.one*10000;maximum=Vector2.one*-10000;
   foreach(var entry in selected){
    var mesh=new Mesh();try{
     entry.Key.BakeMesh(mesh,false);var vertices=mesh.vertices;var indices=mesh.triangles;var ids=new HashSet<int>(entry.Value);var points=new Vector2[vertices.Length];var front=new bool[vertices.Length];
     for(int i=0;i<vertices.Length;i++){Vector3 p=view.WorldToViewportPoint(entry.Key.transform.TransformPoint(vertices[i]));points[i]=new Vector2(p.x*1280,p.y*720);front[i]=p.z>view.nearClipPlane;}
     foreach(int i in entry.Value){count++;inFront&=front[i];minimum=Vector2.Min(minimum,points[i]);maximum=Vector2.Max(maximum,points[i]);}
     for(int i=0;i<indices.Length;i+=3){int a=indices[i],b=indices[i+1],c=indices[i+2];if(!ids.Contains(a)&&!ids.Contains(b)&&!ids.Contains(c))continue;if(!front[a]||!front[b]||!front[c]){inFront=false;continue;}result.Add(new ProjectedTriangle(points[a],points[b],points[c],entry.Key.name));}
    }finally{Object.DestroyImmediate(mesh);}
   }
   return result;
  }
  static float ProjectedCross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static bool ProjectedContains(ProjectedTriangle t,Vector2 p){
   if(Mathf.Abs(ProjectedCross(t.b-t.a,t.c-t.a))<.00001f)return false;
   float a=ProjectedCross(t.b-t.a,p-t.a),b=ProjectedCross(t.c-t.b,p-t.b),c=ProjectedCross(t.a-t.c,p-t.c);
   return (a>=0&&b>=0&&c>=0)||(a<=0&&b<=0&&c<=0);
  }
  static float ProjectedPointSegmentDistanceSquared(Vector2 p,Vector2 a,Vector2 b){
   Vector2 d=b-a;float t=d.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude);return (p-a-d*t).sqrMagnitude;
  }
  static float ProjectedSegmentDistanceSquared(Vector2 a,Vector2 b,Vector2 c,Vector2 d){
   bool boxesTouch=Mathf.Max(a.x,b.x)>=Mathf.Min(c.x,d.x)&&Mathf.Max(c.x,d.x)>=Mathf.Min(a.x,b.x)&&Mathf.Max(a.y,b.y)>=Mathf.Min(c.y,d.y)&&Mathf.Max(c.y,d.y)>=Mathf.Min(a.y,b.y);
   float abC=ProjectedCross(b-a,c-a),abD=ProjectedCross(b-a,d-a),cdA=ProjectedCross(d-c,a-c),cdB=ProjectedCross(d-c,b-c);
   if(boxesTouch&&abC*abD<=0&&cdA*cdB<=0)return 0;
   return Mathf.Min(Mathf.Min(ProjectedPointSegmentDistanceSquared(a,c,d),ProjectedPointSegmentDistanceSquared(b,c,d)),Mathf.Min(ProjectedPointSegmentDistanceSquared(c,a,b),ProjectedPointSegmentDistanceSquared(d,a,b)));
  }
  static float ProjectedTriangleDistanceSquared(ProjectedTriangle a,ProjectedTriangle b){
   for(int i=0;i<3;i++)if(ProjectedContains(a,b.Point(i))||ProjectedContains(b,a.Point(i)))return 0;
   float minimum=float.PositiveInfinity;for(int i=0;i<3;i++)for(int j=0;j<3;j++)minimum=Mathf.Min(minimum,ProjectedSegmentDistanceSquared(a.Point(i),a.Point((i+1)%3),b.Point(j),b.Point((j+1)%3)));return minimum;
  }
  static float MeasureProjectedTriangles(List<ProjectedTriangle> face,List<ProjectedTriangle> arm,FaceArmClearanceReport report){
   const float binSize=32;var bins=new Dictionary<Vector2Int,List<int>>();
   for(int i=0;i<arm.Count;i++){var a=arm[i];for(int x=Mathf.FloorToInt(a.min.x/binSize);x<=Mathf.FloorToInt(a.max.x/binSize);x++)for(int y=Mathf.FloorToInt(a.min.y/binSize);y<=Mathf.FloorToInt(a.max.y/binSize);y++){var key=new Vector2Int(x,y);if(!bins.TryGetValue(key,out var ids)){ids=new List<int>();bins[key]=ids;}ids.Add(i);}}
   var visited=new int[arm.Count];int stamp=0;float minimum=float.PositiveInfinity;
   foreach(var f in face){stamp++;for(int x=Mathf.FloorToInt((f.min.x-6)/binSize);x<=Mathf.FloorToInt((f.max.x+6)/binSize);x++)for(int y=Mathf.FloorToInt((f.min.y-6)/binSize);y<=Mathf.FloorToInt((f.max.y+6)/binSize);y++){
    if(!bins.TryGetValue(new Vector2Int(x,y),out var ids))continue;
    foreach(int i in ids){if(visited[i]==stamp)continue;visited[i]=stamp;var a=arm[i];
     float dx=Mathf.Max(0,Mathf.Max(f.min.x-a.max.x,a.min.x-f.max.x)),dy=Mathf.Max(0,Mathf.Max(f.min.y-a.max.y,a.min.y-f.max.y));if(dx*dx+dy*dy>=36)continue;
     report.testedTrianglePairs++;float distance=ProjectedTriangleDistanceSquared(f,a);
     if(distance<minimum){minimum=distance;report.faceRenderer=f.renderer;report.armRenderer=a.renderer;report.faceTriangleA=f.a;report.faceTriangleB=f.b;report.faceTriangleC=f.c;report.armTriangleA=a.a;report.armTriangleB=a.b;report.armTriangleC=a.c;}
    }
   }}
   if(report.testedTrianglePairs>0)report.minimumTestedTriangleGapPixels=Mathf.Sqrt(minimum);
   return minimum;
  }
  public FaceArmClearanceReport DescribeFaceArmClearance(Camera view) {
   var report=new FaceArmClearanceReport();report.rectangleGatePassed=FaceClearOfRightArm(view,out float gap);report.rectangleGapPixels=gap;
   var controlFace=new ProjectedTriangle(new Vector2(0,0),new Vector2(10,0),new Vector2(0,10),"control");
   float six=ProjectedTriangleDistanceSquared(controlFace,new ProjectedTriangle(new Vector2(16,0),new Vector2(26,0),new Vector2(16,10),"control"));
   float near=ProjectedTriangleDistanceSquared(controlFace,new ProjectedTriangle(new Vector2(15.5f,0),new Vector2(25.5f,0),new Vector2(15.5f,10),"control"));
   float overlap=ProjectedTriangleDistanceSquared(controlFace,new ProjectedTriangle(new Vector2(2,2),new Vector2(12,2),new Vector2(2,12),"control"));
   float diagonal=ProjectedTriangleDistanceSquared(new ProjectedTriangle(Vector2.zero,new Vector2(100,0),new Vector2(0,100),"control"),new ProjectedTriangle(new Vector2(80,80),new Vector2(180,80),new Vector2(80,180),"control"));
   var boundary=new FaceArmClearanceReport();MeasureProjectedTriangles(new List<ProjectedTriangle>{new ProjectedTriangle(new Vector2(20,0),new Vector2(30,0),new Vector2(20,10),"control")},new List<ProjectedTriangle>{new ProjectedTriangle(new Vector2(35.5f,0),new Vector2(45.5f,0),new Vector2(35.5f,10),"control")},boundary);
   report.geometryControlsPassed=Mathf.Abs(six-36)<.001f&&Mathf.Abs(near-30.25f)<.001f&&overlap<.001f&&Mathf.Abs(diagonal-1800)<.01f&&boundary.testedTrianglePairs==1&&Mathf.Abs(boundary.minimumTestedTriangleGapPixels-5.5f)<.001f;
   if(!report.geometryControlsPassed){report.geometryError="Projected triangle-distance controls failed";return report;}
   try{
    var face=ProjectedPatch(view,viewFaceVertices,out report.faceVertexCount,out bool faceFront,out report.faceMin,out report.faceMax);
    var arm=ProjectedPatch(view,viewArmVertices,out report.armVertexCount,out bool armFront,out report.armMin,out report.armMax);
    report.faceTriangleCount=face.Count;report.armTriangleCount=arm.Count;report.allSelectedVerticesInFront=faceFront&&armFront;
    report.geometryAvailable=face.Count>0&&arm.Count>0;if(!report.geometryAvailable)return report;
    float minimum=MeasureProjectedTriangles(face,arm,report);
    report.triangleClearanceAtLeastSixPixels=report.allSelectedVerticesInFront&&report.faceVertexCount>=100&&report.armVertexCount>=100&&(report.testedTrianglePairs==0||minimum>=36);
   }catch(System.Exception error){report.geometryError=error.Message;}
   return report;
  }
  public bool FaceInView(Camera view) {
   int count=0;
   foreach(var sample in viewFaceVertices) {
    var mesh=new Mesh();try {
     sample.Key.BakeMesh(mesh,false);var vertices=mesh.vertices;
     foreach(int index in sample.Value) {
      Vector3 point=view.WorldToViewportPoint(sample.Key.transform.TransformPoint(vertices[index]));count++;
      if(point.z<=view.nearClipPlane||point.x<=.07f||point.x>=.93f||point.y<=.08f||point.y>=.92f)return false;
     }
    }finally{Object.DestroyImmediate(mesh);}
   }
   return count>=100;
  }
  void CacheRestSupport() {
   var points=new Vector3[8];var found=new bool[8];float left=float.PositiveInfinity,right=float.PositiveInfinity;
   var shoePoints=new[]{new List<Vector3>(),new List<Vector3>()};var soleY=new[]{float.PositiveInfinity,float.PositiveInfinity};
   VisitMesh((r,i,p,w)=>{
    if(p.y>=.55f&&p.y<1.8f&&Mathf.Abs(p.x)<.24f){int bin=Mathf.Clamp((int)((p.y-.55f)/1.25f*8),0,7);if(!found[bin]||p.z<points[bin].z){found[bin]=true;points[bin]=p;}}
    if(p.y<.2f){if(Mathf.Abs(p.x-restPosition["foot_l"].x)<.1f)left=Mathf.Min(left,p.z);if(Mathf.Abs(p.x-restPosition["foot_r"].x)<.1f)right=Mathf.Min(right,p.z);
     int side=Mathf.Abs(p.x-restPosition["foot_r"].x)<Mathf.Abs(p.x-restPosition["foot_l"].x)?0:1;string suffix=side==0?"_r":"_l";
     if(Mathf.Abs(p.x-restPosition["foot"+suffix].x)<.1f){soleY[side]=Mathf.Min(soleY[side],p.y);shoePoints[side].Add(p-restPosition["foot"+suffix]);if(!footVertices[side].TryGetValue(r,out var ids)){ids=new HashSet<int>();footVertices[side][r]=ids;}ids.Add(i);}
    }
    for(int side=0;side<2;side++){string suffix=side==0?"_r":"_l";Vector3 wrist=restPosition["hand"+suffix],direction=(restPosition["middle_03"+suffix]-wrist).normalized,delta=p-wrist;float along=Vector3.Dot(delta,direction);if(along>=-.018f&&along<=.24f&&(delta-direction*along).sqrMagnitude<.0121f){if(!handVertices[side].TryGetValue(r,out var ids)){ids=new HashSet<int>();handVertices[side][r]=ids;}ids.Add(i);}}
   });
   for(int i=0;i<points.Length;i++)if(found[i])restSupport.Add(points[i]);
   if(restSupport.Count<4||float.IsInfinity(left)||float.IsInfinity(right))throw new System.InvalidOperationException("Dreamer resting contact geometry missing");
   heelOffset["_l"]=restPosition["foot_l"].z-left;heelOffset["_r"]=restPosition["foot_r"].z-right;
   if(float.IsInfinity(soleY[0])||float.IsInfinity(soleY[1]))throw new System.InvalidOperationException("Dreamer shoe soles missing");
   StandingHeightOffset=.006f-Mathf.Min(soleY[0],soleY[1]);
   // Walking rocks around the sole, rather than a heel collar or upper-shoe hull extremum.
   for(int side=0;side<2;side++){
    string suffix=side==0?"_r":"_l";float ceiling=soleY[side]-restPosition["foot"+suffix].y+.012f;bool foundSole=false;
    foreach(Vector3 point in shoePoints[side])if(point.y<=ceiling){
     if(!foundSole||point.z<walkingHeelPivot[side].z)walkingHeelPivot[side]=point;
     if(!foundSole||point.z>walkingToePivot[side].z)walkingToePivot[side]=point;
     foundSole=true;
    }
    if(!foundSole)throw new System.InvalidOperationException("Dreamer walking sole pivots missing");
   }
   // Retain mesh support extrema over the upright-to-supine foot rotation.
   for(int side=0;side<2;side++){var selected=new HashSet<int>();for(int angle=-90;angle<=60;angle+=6)for(int tilt=-1;tilt<=1;tilt++){float radians=angle*Mathf.Deg2Rad;Vector3 direction=new Vector3(tilt*.2f,-Mathf.Cos(radians),Mathf.Sin(radians));float best=float.NegativeInfinity;int index=0;for(int i=0;i<shoePoints[side].Count;i++){float score=Vector3.Dot(shoePoints[side][i],direction);if(score>best){best=score;index=i;}}selected.Add(index);}foreach(int i in selected)footSupport[side].Add(shoePoints[side][i]);}
  }
  void CacheShoePoseMeshes(ReturnVolumeArt art){
   foreach(var foot in footVertices)foreach(var renderer in foot.Keys)if(!shoePoseMeshes.ContainsKey(renderer)){
    shoePoseMeshes.Add(renderer,art.OwnedMesh(root.name+" posed shoe contact"));shoePoseVertices.Add(renderer,new List<Vector3>());
   }
   foreach(var hand in handVertices)foreach(var renderer in hand.Keys)if(!boardingHandPoseMeshes.ContainsKey(renderer)){
    bool shared=shoePoseMeshes.TryGetValue(renderer,out var mesh);
    boardingHandPoseMeshes.Add(renderer,shared?mesh:art.OwnedMesh(root.name+" posed boarding hand contact"));
    boardingHandPoseVertices.Add(renderer,shared?shoePoseVertices[renderer]:new List<Vector3>());
   }
  }
  void CorrectShoeContact(System.Func<Vector3,float> surface){
   long contactStarted=measureShoeContact?System.Diagnostics.Stopwatch.GetTimestamp():0;
   // Solving the leg can reshape the weighted shoe; measure the result before finishing.
   for(int pass=0;pass<3;pass++){
   if(constrainGroundAnklePose)for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";groundPoseHeadings[leg]=LimitGroundAnkle(leg,groundDesiredFootHeadings[leg]);B("foot"+suffix).rotation=groundPoseHeadings[leg]*restRotation["foot"+suffix];}
   Vector2 minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
    for(int leg=0;leg<2;leg++)groundHeldGap[leg]=float.PositiveInfinity;
   foreach(var entry in shoePoseMeshes){
     long bakeStarted=measureShoeContact?System.Diagnostics.Stopwatch.GetTimestamp():0;
    entry.Key.BakeMesh(entry.Value,false);var vertices=shoePoseVertices[entry.Key];entry.Value.GetVertices(vertices);
     if(measureShoeContact){measuredContactBakes++;measuredContactBakeMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-bakeStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;}
     Matrix4x4 posedToWorld=entry.Key.localToWorldMatrix;
     for(int leg=0;leg<2;leg++)if(footVertices[leg].TryGetValue(entry.Key,out var ids))foreach(int id in ids){
       Vector3 world=posedToWorld.MultiplyPoint3x4(vertices[id]);float gap=world.y-surface(world);
      if(gap<minimum[leg]){minimum[leg]=gap;groundLowestWorld[leg]=world;groundLowestRenderer[leg]=entry.Key;groundLowestVertex[leg]=id;}
      var contact=groundContacts[leg];if(contact.valid&&contact.renderer==entry.Key&&contact.vertex==id){groundHeldWorld[leg]=world;groundHeldGap[leg]=gap;}
     }
   }
   bool corrected=false;
   if(constrainGroundAnklePose){
    float needed=0;
    for(int leg=0;leg<2;leg++)if(groundContactWalking[leg]){
     string suffix=leg==0?"_r":"_l";Transform hip=B("thigh"+suffix),knee=B("calf"+suffix),ankle=B("foot"+suffix);
     Vector3 target=groundPoseTargets[leg]+Vector3.up*(groundPoseClearance[leg]-minimum[leg]);
     var contact=groundContacts[leg];if(contact.valid){Vector3 slip=groundHeldWorld[leg]-contact.anchor;target.x-=slip.x;target.z-=slip.z;}
     float reach=(Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position))*.985f;
     Vector3 delta=target-hip.position;float horizontalSquared=delta.x*delta.x+delta.z*delta.z;
     if(horizontalSquared<reach*reach)needed=Mathf.Max(needed,hip.position.y-target.y-Mathf.Sqrt(reach*reach-horizontalSquared));
    }
    needed=Mathf.Min(needed,Mathf.Max(0,.3f-groundDrop));
    if(needed>.0001f){
     // Fit the body to the measured held shoe before shortening or moving the plant.
     B("pelvis").position-=Vector3.up*needed;groundDrop+=needed;
     for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,groundPoseTargets[leg],groundPoseHints[leg].sqrMagnitude>.00001f?groundPoseHints[leg]:root.forward);}
     corrected=true;
    }
   }
   for(int leg=0;leg<2;leg++){
    float lift=(constrainGroundAnklePose?groundPoseClearance[leg]:.006f)-minimum[leg];if(float.IsNaN(lift)||float.IsInfinity(lift))throw new System.InvalidOperationException("Posed shoe contact geometry missing");
    if(GroundWalkReviewActive&&swinging[leg]&&!groundContactWalking[leg]&&groundReplantElapsed[leg]>=0&&lift<0)lift*=Mathf.SmoothStep(0,1,Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg]));
    string suffix=leg==0?"_r":"_l";Vector3 target=groundPoseTargets[leg]+Vector3.up*lift;
    if(constrainGroundAnklePose&&groundContactWalking[leg]){
     var contact=groundContacts[leg];
     if(!contact.valid||groundHeldGap[leg]>minimum[leg]+.003f){
       // Street contacts register the completed landing; terrain contacts inherit the preceding support.
      Vector3 local=Quaternion.Inverse(groundPoseHeadings[leg])*(groundLowestWorld[leg]-B("foot"+suffix).position);
       contact.anchor=levelGroundPose?groundLowestWorld[leg]:previousSolvedFoot[leg]+groundSupportTranslation+previousSolvedFootHeading[leg]*local;contact.renderer=groundLowestRenderer[leg];contact.vertex=groundLowestVertex[leg];contact.valid=true;groundHeldWorld[leg]=groundLowestWorld[leg];
     }
     Vector3 slip=groundHeldWorld[leg]-contact.anchor;target.x-=slip.x;target.z-=slip.z;
    }
    bool heightSatisfied=constrainGroundAnklePose?Mathf.Abs(lift)<.001f:lift<.001f;
    if(heightSatisfied&&new Vector2(target.x-groundPoseTargets[leg].x,target.z-groundPoseTargets[leg].z).sqrMagnitude<.000001f)continue;
    if(constrainGroundAnklePose)target=ReachableGroundFoot(leg,target,groundPoseClearance[leg]-.006f,surface,groundPoseHeadings[leg]);
     Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,target,groundPoseHints[leg].sqrMagnitude>.00001f?groundPoseHints[leg]:root.forward);
    if(constrainGroundAnklePose)groundPoseHeadings[leg]=LimitGroundAnkle(leg,groundDesiredFootHeadings[leg]);
    B("foot"+suffix).rotation=groundPoseHeadings[leg]*restRotation["foot"+suffix];groundPoseTargets[leg]=target;corrected=true;
   }
   if(!corrected)break;
   }
   if(measureShoeContact){
    measuredContactMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-contactStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency;
    if(++measuredContactCalls==120){
     Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,"[RETURN update timing] shoe-contact actor={0} calls={1} bakes={2} meanMs={3:F3} bakeMsPerCall={4:F3} frame={5} focused={6}",root.name,measuredContactCalls,measuredContactBakes,measuredContactMs/measuredContactCalls,measuredContactBakeMs/measuredContactCalls,Time.frameCount,Application.isFocused));
     measuredContactCalls=measuredContactBakes=0;measuredContactMs=measuredContactBakeMs=0;
    }
   }
  }
  public Vector3[][] DescribeShoeVertices() {
   var points=new[]{new List<Vector3>(),new List<Vector3>()};
   foreach(var entry in shoePoseMeshes){entry.Key.BakeMesh(entry.Value,false);var vertices=shoePoseVertices[entry.Key];entry.Value.GetVertices(vertices);
    for(int side=0;side<2;side++)if(footVertices[side].TryGetValue(entry.Key,out var ids))foreach(int id in ids)points[side].Add(entry.Key.transform.TransformPoint(vertices[id]));
   }
   return new[]{points[0].ToArray(),points[1].ToArray()};
  }
  [System.Serializable] public sealed class ShoePointSource {public string renderer,mesh;public int vertex;public bool weightsReadable;public string[] bones;public float[] weights;}
  public ShoePointSource DescribeShoePointSource(int leg,int point){
   int index=0;foreach(var entry in shoePoseMeshes)if(footVertices[leg].TryGetValue(entry.Key,out var ids))foreach(int id in ids){
    if(index++!=point)continue;var owner=entry.Key;var mesh=owner.sharedMesh;var source=new ShoePointSource{renderer=owner.name,mesh=mesh.name,vertex=id,weightsReadable=mesh.isReadable};
    if(source.weightsReadable){BoneWeight weight=mesh.boneWeights[id];int[] indices={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};source.weights=new[]{weight.weight0,weight.weight1,weight.weight2,weight.weight3};source.bones=new string[4];var rig=owner.bones;for(int i=0;i<4;i++)source.bones[i]=indices[i]>=0&&indices[i]<rig.Length&&rig[indices[i]]!=null?rig[indices[i]].name:"unavailable";}
    return source;
   }
   throw new System.InvalidOperationException("Tracked shoe point missing from pose cache");
  }
  public Vector2 MeasureShoeClearance(System.Func<Vector3,float> surface) {
   var minimum=new[]{float.PositiveInfinity,float.PositiveInfinity};
   VisitMesh((r,i,p,w)=>{for(int side=0;side<2;side++)if(footVertices[side].TryGetValue(r,out var ids)&&ids.Contains(i))minimum[side]=Mathf.Min(minimum[side],w.y-surface(w));});
   return new Vector2(minimum[0],minimum[1]);
  }
  void CorrectBoardingHandContact(System.Func<Vector3,float> surface,float weight){
   if(weight<=0)return;
   Vector2 minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
   foreach(var entry in boardingHandPoseMeshes){
    entry.Key.BakeMesh(entry.Value,false);var vertices=boardingHandPoseVertices[entry.Key];entry.Value.GetVertices(vertices);
    for(int side=0;side<2;side++)if(handVertices[side].TryGetValue(entry.Key,out var ids))foreach(int id in ids){Vector3 world=entry.Key.transform.TransformPoint(vertices[id]);minimum[side]=Mathf.Min(minimum[side],world.y-surface(world));}
   }
   for(int side=0;side<2;side++){
    float lift=Mathf.Max(0,.006f-minimum[side])*weight;if(float.IsNaN(lift)||float.IsInfinity(lift))throw new System.InvalidOperationException("Boarding hand contact geometry missing");if(lift<=0)continue;
    string suffix=side==0?"_r":"_l";float sign=Mathf.Sign(restPosition["hand"+suffix].x);var hand=B("hand"+suffix);Vector3 target=hand.position+Vector3.up*lift;Quaternion orientation=hand.rotation;
    Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,target,root.right*sign-Vector3.up*.3f-root.forward*.15f);hand.rotation=orientation;
   }
  }
  public Vector2 MeasureHandClearance(System.Func<Vector3,float> surface){
   Vector2 minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
   VisitMesh((r,i,p,w)=>{for(int side=0;side<2;side++)if(handVertices[side].TryGetValue(r,out var ids)&&ids.Contains(i))minimum[side]=Mathf.Min(minimum[side],w.y-surface(w));});return minimum;
  }
  public bool ValidateRestSupport(System.Func<Vector3,float> surface,out string detail) {
   float core=float.PositiveInfinity,heel=float.PositiveInfinity;
   VisitMesh((p,w)=>{float gap=w.y-surface(w);if(p.y>=.55f&&p.y<1.8f&&Mathf.Abs(p.x)<.24f)core=Mathf.Min(core,gap);if(p.y<.2f)heel=Mathf.Min(heel,gap);});
   float palmDown=1;foreach(string suffix in new[]{"_l","_r"}){Vector3 index=B("index_01"+suffix).position-B("hand"+suffix).position,pinky=B("pinky_01"+suffix).position-B("hand"+suffix).position;Vector3 palm=Vector3.Cross(index,pinky).normalized*palmSign[suffix];palmDown=Mathf.Min(palmDown,Vector3.Dot(palm,Vector3.down));}
   var minimum=DescribeRestContact(surface);detail="core gap "+core.ToString("F4")+", heel gap "+heel.ToString("F4")+", palm-down dot "+palmDown.ToString("F3")+", "+minimum.renderer+" vertex "+minimum.vertex;return core>=-.015f&&core<=.035f&&heel>=-.015f&&heel<=.035f&&palmDown>.9f;
  }
  [System.Serializable] public sealed class RestContactPoint {public string renderer,mesh;public int vertex;public Vector3 local,world;public float clearance;public bool weightsReadable;public string[] bones;public float[] weights;}
  public RestContactPoint DescribeRestContact(System.Func<Vector3,float> surface) {
   RestContactPoint point=null;SkinnedMeshRenderer owner=null;
   VisitMesh((r,i,p,w)=>{if(p.y<.55f||p.y>=1.8f||Mathf.Abs(p.x)>=.24f)return;float gap=w.y-surface(w);if(point==null||gap<point.clearance){owner=r;point=new RestContactPoint{renderer=r.name,mesh=r.sharedMesh.name,vertex=i,local=p,world=w,clearance=gap};}});
   if(point==null)throw new System.InvalidOperationException("No posed core vertex for contact diagnosis");
   point.weightsReadable=owner.sharedMesh.isReadable;
   if(point.weightsReadable){BoneWeight weight=owner.sharedMesh.boneWeights[point.vertex];int[] indices={weight.boneIndex0,weight.boneIndex1,weight.boneIndex2,weight.boneIndex3};point.weights=new[]{weight.weight0,weight.weight1,weight.weight2,weight.weight3};point.bones=new string[4];for(int i=0;i<4;i++)point.bones[i]=indices[i]>=0&&indices[i]<owner.bones.Length?owner.bones[indices[i]].name:"unavailable";}
   return point;
  }
  Transform B(string name)=>bones.TryGetValue(name,out var b)?b:null;
  static int ShapeIndex(SkinnedMeshRenderer r,string name){for(int i=0;i<r.sharedMesh.blendShapeCount;i++)if(r.sharedMesh.GetBlendShapeName(i).EndsWith(name))return i;return -1;}
  void Face(string name,float value){foreach(var r in faces){int i=ShapeIndex(r,name);if(i>=0)r.SetBlendShapeWeight(i,value);}}
  public void LeanToward(Vector3 target,float degrees) {
   var spine=B("spine_01");if(spine==null)return;
   Vector3 toward=Vector3.ProjectOnPlane(target-spine.position,root.up);
   if(toward.sqrMagnitude>.0001f)spine.rotation=Quaternion.AngleAxis(degrees,Vector3.Cross(root.up,toward.normalized))*spine.rotation;
  }
   public void Expression(float voice,float concern,Vector3 patient,float time,float dt,float relief=0,float nod=0,float blink=0) {
    faceLevel=Mathf.Lerp(faceLevel,voice,1-Mathf.Exp(-dt*18));Face("SpeechOpen",faceLevel*72);Face("SpeechRound",faceLevel*(.2f+.18f*Mathf.Sin(time*13+phaseOffset))*100);
    float expressionEase=1-Mathf.Exp(-dt*5);concernLevel=Mathf.Lerp(concernLevel,concern,expressionEase);reliefLevel=Mathf.Lerp(reliefLevel,relief,expressionEase);Face("SoftConcern",concernLevel*100);Face("SoftRelief",reliefLevel*100);Face("Blink",Mathf.Max(poseBlink,Mathf.Clamp01(blink)*100));
   var head=B("head");if(head==null)return;Vector3 direction=(patient-head.position).normalized;
    Vector3 headForward=head.rotation*(Quaternion.Inverse(restRotation["head"])*Vector3.forward);
    Quaternion desiredTurn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(headForward,direction),18);gazeTurn=Quaternion.Slerp(gazeTurn,desiredTurn,1-Mathf.Exp(-dt*6));Quaternion turn=Quaternion.AngleAxis(nod,root.right)*gazeTurn;head.rotation=turn*head.rotation;
   foreach(string side in new[]{"l","r"}){var eye=B("eye_"+side);if(eye==null)continue;Vector3 eyeDirection=(patient-eye.position).normalized,eyeForward=eye.rotation*(Quaternion.Inverse(restRotation["eye_"+side])*Vector3.forward);Quaternion eyeTurn=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(eyeForward,eyeDirection),14);eye.rotation=eyeTurn*eye.rotation;}
  }
  void Aim(Transform bone,Transform child,Vector3 direction){if(bone==null||child==null)return;Vector3 current=child.position-bone.position;if(current.sqrMagnitude>.00001f)bone.rotation=Quaternion.FromToRotation(current,direction.normalized)*bone.rotation;}
  void Hinge(Transform bone,Transform child,Vector3 planeNormal){
   if(!elbowAxis.TryGetValue(bone.name,out var local)||planeNormal.sqrMagnitude<.00001f)return;
   Vector3 along=(child.position-bone.position).normalized,current=Vector3.ProjectOnPlane(bone.rotation*local,along),desired=Vector3.ProjectOnPlane(planeNormal,along);
   if(current.sqrMagnitude>.00001f&&desired.sqrMagnitude>.00001f){
    float twist=Vector3.SignedAngle(current,desired,along);
    if(constrainGroundLegPose&&(bone.name.StartsWith("thigh_")||bone.name.StartsWith("calf_"))){
     bool thigh=bone.name.StartsWith("thigh_");int index=thigh?0:1;float requested=Mathf.Abs(twist),limit=thigh?30f:12f;
     maximumRequestedLegTwist[index]=Mathf.Max(maximumRequestedLegTwist[index],requested);
     twist=Mathf.Clamp(twist,-limit,limit);maximumAppliedLegTwist[index]=Mathf.Max(maximumAppliedLegTwist[index],Mathf.Abs(twist));
     if(requested>limit)groundLegTwistLimitCount++;
    }
    bone.rotation=Quaternion.AngleAxis(twist,along)*bone.rotation;
   }
  }
  void Chain(string upper,string lower,string end,Vector3 target,Vector3 hint) {
   Transform a=B(upper),b=B(lower),c=B(end);if(a==null||b==null||c==null)return;
   float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);Vector3 delta=target-a.position;float d=Mathf.Clamp(delta.magnitude,.01f,l1+l2-.0001f);Vector3 dir=delta.normalized;
   if(constrainGroundLegPose&&upper.StartsWith("thigh_"))target=a.position+dir*d;
   float along=(l1*l1-l2*l2+d*d)/(2*d);float perpendicular=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));Vector3 bend=Vector3.ProjectOnPlane(hint,dir).normalized;
    if(constrainGroundLegPose&&upper.StartsWith("thigh_")){
     // Pelvis roll and yaw must not flare or cross the walking knees.
     Vector3 normal=Vector3.ProjectOnPlane(root.right,dir),anatomical=Vector3.Cross(dir,normal);
     if(anatomical.sqrMagnitude>.00001f){anatomical.Normalize();bend=Vector3.Dot(anatomical,root.forward)<0?-anatomical:anatomical;}
    }
   Vector3 knee=a.position+dir*along+bend*perpendicular;Vector3 plane=Vector3.Cross(knee-a.position,target-knee).normalized;
   Aim(a,b,knee-a.position);Hinge(a,b,plane);Aim(b,c,target-b.position);Hinge(b,c,plane);
  }
  void Fingers(string suffix,float curl,float gentle=0){
   string[] names={"index","middle","ring","pinky","thumb"};
   for(int finger=0;finger<names.Length;finger++)for(int joint=1;joint<=3;joint++){
    string name=names[finger]+"_0"+joint+suffix;var bone=B(name);if(bone==null)continue;
    float angle=names[finger]=="thumb"?curl*.4f:curl;
    float relaxed=names[finger]=="thumb"?new[]{4f,7f,3f}[joint-1]:new[]{6f,10f,4f}[joint-1]+finger*2;angle=Mathf.Lerp(angle,relaxed,gentle);
    bone.localRotation=bind[name]*Quaternion.AngleAxis(angle,flexAxis[name]);
   }
  }
  void Wrist(string suffix,Vector3 forward,Vector3 palm,float weight=1){
   var hand=B("hand"+suffix);if(hand==null||forward.sqrMagnitude<.00001f)return;
   Quaternion pose=Quaternion.LookRotation(forward,-palm)*Quaternion.Inverse(handBasis[suffix]);hand.rotation=Quaternion.Slerp(hand.rotation,pose,Mathf.Clamp01(weight));
  }
  void RestHand(string suffix,System.Func<Vector3,float> surface) {
   int side=suffix=="_r"?0:1;float sign=Mathf.Sign(restPosition["hand"+suffix].x);Vector3 target=root.TransformPoint(new Vector3(sign*.29f,.89f,0));target.y=surface(target)+.025f+handClearance[side];
   Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,target,root.right*sign+root.up*.1f+root.forward*.03f);
   var hand=B("hand"+suffix);const float epsilon=.02f;float dx=(surface(hand.position+Vector3.right*epsilon)-surface(hand.position-Vector3.right*epsilon))/(2*epsilon),dz=(surface(hand.position+Vector3.forward*epsilon)-surface(hand.position-Vector3.forward*epsilon))/(2*epsilon);Vector3 normal=new Vector3(-dx,1,-dz).normalized;
   Wrist(suffix,Vector3.ProjectOnPlane(hand.position-B("lowerarm"+suffix).position,normal),-normal);Fingers(suffix,8);
  }
  void CalibrateRestHands(System.Func<Vector3,float> surface) {
   var minimum=new[]{float.PositiveInfinity,float.PositiveInfinity};
   VisitMesh((r,i,p,w)=>{for(int side=0;side<2;side++)if(handVertices[side].TryGetValue(r,out var ids)&&ids.Contains(i))minimum[side]=Mathf.Min(minimum[side],w.y-surface(w));});
   for(int side=0;side<2;side++){if(float.IsInfinity(minimum[side]))throw new System.InvalidOperationException("Resting hand contact geometry missing");handClearance[side]=Mathf.Clamp(.006f-minimum[side],-.04f,.1f);}
   handsCalibrated=true;RestHand("_r",surface);RestHand("_l",surface);
  }
  void TransitionHand(string suffix,System.Func<Vector3,float> surface,float amount,Vector3 anchor,Quaternion heading) {
   int side=suffix=="_r"?0:1;float sign=Mathf.Sign(restPosition["hand"+suffix].x);
   float contact=Mathf.SmoothStep(0,1,(amount-.1f)/.38f),release=Mathf.SmoothStep(0,1,(amount-.62f)/.38f);
   Vector3 brace=anchor+heading*new Vector3(sign*.32f,0,restPosition["pelvis"].z-.4f),relaxed=root.TransformPoint(new Vector3(sign*.29f,.89f,0));
   brace.y=surface(brace)+.025f+handClearance[side];relaxed.y=surface(relaxed)+.025f+handClearance[side];
   Vector3 target=Vector3.Lerp(root.TransformPoint(new Vector3(sign*.29f,.85f,.06f)),Vector3.Lerp(brace,relaxed,release),contact);
   Vector3 hint=Vector3.Lerp(-root.up-root.forward*.15f+root.right*sign*.25f,root.right*sign+root.up*.1f+root.forward*.03f,contact);
   Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,target,hint);
   var hand=B("hand"+suffix);const float e=.02f;float dx=(surface(hand.position+Vector3.right*e)-surface(hand.position-Vector3.right*e))/(2*e),dz=(surface(hand.position+Vector3.forward*e)-surface(hand.position-Vector3.forward*e))/(2*e);Vector3 normal=new Vector3(-dx,1,-dz).normalized;
   Wrist(suffix,hand.position-B("lowerarm"+suffix).position,-(heading*Vector3.right)*sign);
   Wrist(suffix,Vector3.ProjectOnPlane(hand.position-B("lowerarm"+suffix).position,normal),-normal,contact);Fingers(suffix,Mathf.Lerp(12,8,contact));
  }
  public void ResetRest(){RestAmount=0;wasResting=false;handsCalibrated=false;handClearance[0]=handClearance[1]=0;root.localRotation=restHeading;}
  public void ReachHand(Vector3 target,float weight=1,bool gentle=false,Vector3? elbowHint=null,Vector3? palmNormal=null){
   travellerStyle?.ShowHands(weight>.015f,false);
   var hand=B("hand_r");if(hand==null)return;
   float side=Mathf.Sign(restPosition["hand_r"].x);weight=Mathf.Clamp01(weight);
   if(gentle){var chest=B("spine_03");if(chest!=null)chest.rotation=Quaternion.AngleAxis(5*Mathf.SmoothStep(0,1,weight),root.right)*chest.rotation;}
   var upper=B("upperarm_r");var forearm=B("lowerarm_r");
   Vector3 desired=Vector3.Lerp(hand.position,target,weight),delta=desired-upper.position;
   float upperLength=Vector3.Distance(upper.position,forearm.position),lowerLength=Vector3.Distance(forearm.position,hand.position);
   // Interaction targets retain a soft elbow even when the requested point is beyond reach.
   // This path is separate from the accepted walking-arm retargeting.
   float limit=Mathf.Sqrt(upperLength*upperLength+lowerLength*lowerLength+2*upperLength*lowerLength*Mathf.Cos(25*Mathf.Deg2Rad));
   if(delta.magnitude>limit)desired=upper.position+delta.normalized*limit;
   Chain("upperarm_r","lowerarm_r","hand_r",desired,elbowHint??(-root.up*.7f+root.forward*.15f+root.right*side*.45f));
   if(gentle){
    float offer=Mathf.SmoothStep(0,1,(weight-.15f)/.65f);var lower=B("lowerarm_r");Vector3 axis=(hand.position-lower.position).normalized;
    // Grade forearm roll from a stable elbow to the wrist through the authored skin weights.
    Vector3 desiredPalm=palmNormal.HasValue&&palmNormal.Value.sqrMagnitude>.00001f?palmNormal.Value.normalized:root.up;
    Vector3 currentPalm=Vector3.ProjectOnPlane(-(hand.rotation*handBasis["_r"]*Vector3.up),axis),offeredPalm=Vector3.ProjectOnPlane(desiredPalm,axis);
    if(currentPalm.sqrMagnitude>.00001f&&offeredPalm.sqrMagnitude>.00001f){
     float roll=Mathf.Clamp(Vector3.SignedAngle(currentPalm,offeredPalm,axis),-60,60);
     for(int i=1;i<=8;i++){var twist=B("return_forearm_roll_"+i.ToString("00")+"_r");if(twist==null)throw new System.InvalidOperationException("Mother forearm roll skin is missing joint "+i);twist.rotation=Quaternion.AngleAxis(roll*offer*.75f*i/8f,axis)*twist.rotation;}
    }
    Vector3 forward=Vector3.ProjectOnPlane(axis,desiredPalm);
    if(forward.sqrMagnitude<.00001f)forward=Vector3.ProjectOnPlane(root.up,desiredPalm);
    forward=Vector3.RotateTowards(axis,forward.normalized,35*Mathf.Deg2Rad,0);
    Wrist("_r",forward,desiredPalm,offer);Fingers("_r",12,weight);
    if(palmNormal.HasValue){
     float settle=Mathf.SmoothStep(0,1,(weight-.4f)/.6f);
     string[] fingers={"index","middle","ring","pinky","thumb"};
     for(int finger=0;finger<fingers.Length;finger++)for(int joint=1;joint<=3;joint++){
      string name=fingers[finger]+"_0"+joint+"_r";var bone=B(name);if(bone==null)continue;
      float relaxed=finger==4?new[]{4f,7f,3f}[joint-1]:new[]{6f,10f,4f}[joint-1]+finger*2;
      float curled=finger==4?new[]{7f,9f,5f}[joint-1]:new[]{17f,22f,10f}[joint-1]+finger*(joint==2?4:2);
      bone.localRotation=Quaternion.Slerp(bone.localRotation,bind[name]*Quaternion.AngleAxis(Mathf.Lerp(relaxed,curled,settle),flexAxis[name]),offer);
     }
    }
   }
  }
  void CalibrateMouth() {
   Vector3 sum=Vector3.zero;float total=0;MouthVertexSamples=0;
   foreach(var renderer in faces){int shape=ShapeIndex(renderer,"SpeechRound");if(shape<0)continue;
    var neutral=new Mesh();var pursed=new Mesh();float previous=renderer.GetBlendShapeWeight(shape);
    try {
     renderer.SetBlendShapeWeight(shape,0);renderer.BakeMesh(neutral,false);
     renderer.SetBlendShapeWeight(shape,renderer.sharedMesh.GetBlendShapeFrameWeight(shape,0));renderer.BakeMesh(pursed,false);
     var vertices=neutral.vertices;var moved=pursed.vertices;
     if(vertices.Length!=moved.Length)throw new System.InvalidOperationException("Baked mouth topology changed");
     for(int i=0;i<vertices.Length;i++){float weight=(moved[i]-vertices[i]).magnitude;if(weight<=1e-6f)continue;sum+=renderer.transform.TransformPoint(vertices[i])*weight;total+=weight;MouthVertexSamples++;}
    }finally{renderer.SetBlendShapeWeight(shape,previous);Object.DestroyImmediate(neutral);Object.DestroyImmediate(pursed);}
   }
   if(total<=0||MouthVertexSamples<32)throw new System.InvalidOperationException("Localized mouth-pursing geometry missing");
   mouthLocal=B("head").InverseTransformPoint(sum/total);
  }
  const float FruitPalmHeight=1.05f;
  public bool CanReachFruit(Vector3 centre,float radius){float arm=Vector3.Distance(B("upperarm_r").position,B("lowerarm_r").position)+Vector3.Distance(B("lowerarm_r").position,B("hand_r").position);float palm=Vector3.Distance(B("hand_r").position,RightPalmPosition);Vector3 wrist=centre-root.up*(radius*FruitPalmHeight)-root.forward*palm;return Vector3.Distance(B("upperarm_r").position,wrist)<arm*.94f;}
  public Vector3 HoldFruit(Vector3 centre,float radius,float weight,float bite,float turn=0,float actionTime=-1) {
   Vector3 handForward=Quaternion.AngleAxis(Mathf.Clamp(turn,-18,18),root.up)*root.forward;
   // First place the arm, then choose a relaxed wrist relative to the solved forearm.
   Wrist("_r",handForward,root.up,weight);
   Vector3 palmOffset=RightPalmPosition-B("hand_r").position;
   ReachHand(centre-root.up*(radius*FruitPalmHeight)-palmOffset,weight);
   Vector3 axis=(B("hand_r").position-B("lowerarm_r").position).normalized;
   Wrist("_r",Vector3.RotateTowards(axis,handForward,35*Mathf.Deg2Rad,0),root.up,weight);
   // Keep this world orientation through the final IK solve: changing it afterwards
   // changes the knuckle offset and moves the held fruit away from the requested centre.
   Quaternion gripRotation=B("hand_r").rotation;
   palmOffset=RightPalmPosition-B("hand_r").position;
   ReachHand(centre-root.up*(radius*FruitPalmHeight)-palmOffset,weight);
   B("hand_r").rotation=gripRotation;Fingers("_r",Mathf.Lerp(12,48,weight));
   // Support the lower fruit edge without lifting the thumb across the lips.
   for(int joint=1;joint<=3;joint++){
    string name="thumb_0"+joint+"_r";var thumb=B(name);if(thumb==null)continue;
    float curl=joint==1?6:joint==2?10:4;
    thumb.localRotation=bind[name]*Quaternion.AngleAxis(Mathf.Lerp(4.8f,curl,weight),flexAxis[name]);
   }
   if(actionTime<0){Face("SpeechOpen",32*bite);Face("SoftRelief",12*bite);}else ReactToFruit(actionTime,centre,bite);
   return RightPalmPosition+root.up*(radius*FruitPalmHeight);
  }
  void ReactToFruit(float t,Vector3 fruit,float bite) {
   float taste=Mathf.SmoothStep(0,1,(t-2.6f)/.75f);
   float chewing=Mathf.SmoothStep(0,1,(t-2.6f)/.08f)*(1-Mathf.SmoothStep(0,1,(t-3.32f)/.12f));
   float chew=chewing*Mathf.Max(0,Mathf.Sin((t-2.6f)*Mathf.PI*2/.43f));
   float blink=Mathf.SmoothStep(0,1,(t-2.62f)/.1f)*(1-Mathf.SmoothStep(0,1,(t-2.74f)/.16f));
   Face("SpeechOpen",32*bite+9*chew);Face("SpeechRound",6*chew);Face("SoftRelief",18*taste);Face("Blink",Mathf.Max(poseBlink,100*blink));
   var head=B("head");if(head!=null)head.rotation=Quaternion.AngleAxis(1.8f*bite+.4f*chew-1.2f*taste,root.right)*head.rotation;
   float attention=Mathf.SmoothStep(0,1,(t-.9f)/.6f)*(1-.7f*taste);
   foreach(string side in new[]{"l","r"}){
    var eye=B("eye_"+side);if(eye==null)continue;
    Vector3 forward=eye.rotation*(Quaternion.Inverse(restRotation["eye_"+side])*Vector3.forward),direction=(fruit-eye.position).normalized;
    eye.rotation=Quaternion.RotateTowards(Quaternion.identity,Quaternion.FromToRotation(forward,direction),12*attention)*eye.rotation;
   }
  }
  Quaternion GroundFootRotation(Vector3 point,Quaternion heading,System.Func<Vector3,float> surface){const float e=.12f;float dx=(surface(point+Vector3.right*e)-surface(point-Vector3.right*e))/(2*e),dz=(surface(point+Vector3.forward*e)-surface(point-Vector3.forward*e))/(2*e);Vector3 normal=Vector3.RotateTowards(Vector3.up,new Vector3(-dx,1,-dz).normalized,30*Mathf.Deg2Rad,0);return Quaternion.LookRotation(Vector3.ProjectOnPlane(heading*Vector3.forward,normal),normal);}
  float GroundFootHeight(int leg,Vector3 foot,Quaternion rotation,System.Func<Vector3,float> surface){float height=float.NegativeInfinity;foreach(Vector3 point in footSupport[leg]){Vector3 offset=rotation*point;height=Mathf.Max(height,surface(foot+offset)-offset.y);}return height+.006f;}
  Vector2 GroundAnkleAngles(int leg,Quaternion soleHeading,out Quaternion neutral){
   string suffix=leg==0?"_r":"_l";Vector3 shin=(B("calf"+suffix).position-B("foot"+suffix).position).normalized;
   Vector3 forward=Vector3.ProjectOnPlane(soleHeading*Vector3.forward,shin);if(forward.sqrMagnitude<.00001f)forward=Vector3.ProjectOnPlane(root.forward,shin);if(forward.sqrMagnitude<.00001f)forward=Vector3.Cross(root.right,shin);
   neutral=Quaternion.LookRotation(forward,shin);Vector3 up=Quaternion.Inverse(neutral)*(soleHeading*Vector3.up);
   return new Vector2(Mathf.Atan2(up.z,up.y)*Mathf.Rad2Deg,Mathf.Atan2(-up.x,Mathf.Sqrt(up.y*up.y+up.z*up.z))*Mathf.Rad2Deg);
  }
  Quaternion LimitGroundAnkle(int leg,Quaternion desired){
   if(!constrainGroundAnklePose)return desired;Vector2 angles=GroundAnkleAngles(leg,desired,out var neutral);
   maximumRequestedAnkleAngles=Vector2.Max(maximumRequestedAnkleAngles,new Vector2(Mathf.Abs(angles.x),Mathf.Abs(angles.y)));
   float pitch=Mathf.Clamp(angles.x,-30,45),roll=Mathf.Clamp(angles.y,-12,12);
   if(Mathf.Abs(pitch-angles.x)<.001f&&Mathf.Abs(roll-angles.y)<.001f)return desired;
   groundAnkleLimitCount++;return neutral*Quaternion.Euler(pitch,0,roll);
  }
  float GroundWalkRoll(int leg,float phase){
   if(groundReplantElapsed[leg]>=0||groundLandingHeld[leg]||groundLateSwing[leg])return 0;
   float roll=ReturnRecordedWalk.SampleFoot(phase,leg).roll*walkWeight;
   // A corrective landing is already flat; retain toe-off without repeating a heel strike.
   return groundSkipHeelStrike[leg]?Mathf.Max(0,roll):roll;
  }
  Vector3 GroundFootLane(int leg,Vector3 target){
   string suffix=leg==0?"_r":"_l";Vector3 centre=B("pelvis").position,side=root.right*Mathf.Sign(restPosition["foot"+suffix].x);
   float hip=Vector3.Dot(B("thigh"+suffix).position-centre,side),lateral=Vector3.Dot(target-centre,side);
   float minimum=Mathf.Max(walkingLegLength*.04f,hip-walkingLegLength*.025f),maximum=Mathf.Max(minimum,hip+walkingLegLength*.025f);
   return target+side*(Mathf.Clamp(lateral,minimum,maximum)-lateral);
  }
  Vector3 ReachableGroundFoot(int leg,Vector3 target,float lift,System.Func<Vector3,float> surface,Quaternion soleHeading){
   string suffix=leg==0?"_r":"_l";Transform hip=B("thigh"+suffix),knee=B("calf"+suffix),ankle=B("foot"+suffix);
   float reach=(Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position))*.985f;
   if((target-hip.position).sqrMagnitude<=reach*reach)return target;
    if(GroundWalkReviewActive&&swinging[leg]&&!groundContactWalking[leg]){
     Vector3 airborneDelta=target-hip.position;float horizontalSquared=airborneDelta.x*airborneDelta.x+airborneDelta.z*airborneDelta.z;
     if(horizontalSquared<reach*reach){
      float minimumY=hip.position.y-Mathf.Sqrt(reach*reach-horizontalSquared);
      // A released foot can rise without snapping its planned horizontal path toward the body.
      if(target.y<minimumY){target.y=minimumY;groundFootReachLimitCount++;return target;}
     }
    }
   Vector3 safe=GroundFootLane(leg,root.TransformPoint(restPosition["foot"+suffix]));
   safe.y=GroundFootHeight(leg,safe,soleHeading,surface)+lift;
   if((safe-hip.position).sqrMagnitude>reach*reach){
    // Shorten the step beneath this hip without pulling either foot into the centre lane.
    safe=GroundFootLane(leg,hip.position);
    safe.y=GroundFootHeight(leg,safe,soleHeading,surface)+lift;
   }
   if((safe-hip.position).sqrMagnitude>reach*reach)return target;
   // Keep the foot on the terrain while reducing the step, rather than extending the leg toward an unreachable plant.
   float low=0,high=1;Vector3 start=safe;
   for(int i=0;i<6;i++){
    float u=(low+high)*.5f;Vector3 trial=Vector3.Lerp(start,target,u);
    trial.y=GroundFootHeight(leg,trial,soleHeading,surface)+lift;
    if((trial-hip.position).sqrMagnitude<=reach*reach){low=u;safe=trial;}else high=u;
   }
   groundFootReachLimitCount++;return safe;
  }
  public void Pose(Vector3 p,Vector3 velocity,bool rest,bool airborne,float time,float reach=0,float? facing=null,System.Func<Vector3,float> restingSurface=null,System.Func<Vector3,float> groundSurface=null,float? restingHeading=null,bool quietGroundWalk=true,float groundStrideLimit=1,bool levelGroundWalk=false) {
   travellerStyle?.ShowHands(false,false);
   if(wasSwimming&&!applyingSwimPose)BeginSwimExit();
   if(wasSwimming||swimExitElapsed<SwimExitDuration)root.localRotation=Quaternion.Euler(0,root.localEulerAngles.y,0);
   wasSwimming=false;
   float dt=Mathf.Clamp(time-previousTime,0,.1f);previousTime=time;
   if(rest&&RestAmount==0){restHeading=restStartHeading=Quaternion.Euler(0,root.localEulerAngles.y,0);restTargetHeading=Quaternion.Euler(0,restingHeading??root.localEulerAngles.y,0);}
   // Measure the final supported hands before displaying the first lowering frame.
   if(rest&&restingSurface!=null&&!handsCalibrated){Quaternion entryHeading=restHeading;restHeading=restTargetHeading;ApplyPose(p,Vector3.zero,false,time,0,null,restingSurface,1,0,groundSurface,quietGroundWalk);CalibrateRestHands(restingSurface);restHeading=entryHeading;}
   RestAmount=Mathf.MoveTowards(RestAmount,rest?1:0,dt/(rest?2.35f:2.05f));
   if(rest)restHeading=Quaternion.Slerp(restStartHeading,restTargetHeading,Mathf.SmoothStep(0,1,RestAmount/.32f));
   ApplyPose(p,velocity,airborne,time,reach,facing,restingSurface,RestAmount,dt,groundSurface,quietGroundWalk,groundStrideLimit,levelGroundWalk);wasResting=RestAmount>0;
   if(!applyingSwimPose){if(rest)swimExitElapsed=SwimExitDuration;else BlendSwimExit(p,dt,airborne,groundSurface);}
   if(!rest&&RestAmount==0){handsCalibrated=false;handClearance[0]=handClearance[1]=0;}
  }
  void BeginSwimExit(){
   swimExitElapsed=0;swimExitRootOffset=root.localPosition-lastSwimPosition;swimExitRootRotation=root.localRotation;
   foreach(var bone in bones){swimExitBones[bone.Key]=bone.Value.localRotation;swimExitBonePositions[bone.Key]=bone.Value.localPosition;}
   Vector3 pawn=root.parent==null?lastSwimPosition:root.parent.TransformPoint(lastSwimPosition);
   for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";swimExitFeet[leg]=B("foot"+suffix).position-pawn;swimExitFootRotations[leg]=B("foot"+suffix).rotation;}
  }
  void BlendSwimExit(Vector3 p,float dt,bool airborne,System.Func<Vector3,float> surface){
   if(swimExitElapsed>=SwimExitDuration)return;
   swimExitElapsed=Mathf.Min(SwimExitDuration,swimExitElapsed+dt);float blend=Mathf.SmoothStep(0,1,swimExitElapsed/SwimExitDuration);
   root.localPosition+=swimExitRootOffset*(1-blend);root.localRotation=Quaternion.Slerp(swimExitRootRotation,root.localRotation,blend);
   foreach(var bone in bones){bone.Value.localRotation=Quaternion.Slerp(swimExitBones[bone.Key],bone.Value.localRotation,blend);bone.Value.localPosition=Vector3.Lerp(swimExitBonePositions[bone.Key],bone.Value.localPosition,blend);}
   Vector3 pawn=root.parent==null?p:root.parent.TransformPoint(p);
   for(int leg=0;leg<2;leg++){
    string suffix=leg==0?"_r":"_l";
    Vector3 target=Vector3.Lerp(pawn+swimExitFeet[leg],groundPoseTargets[leg],blend);
    Quaternion rotation=Quaternion.Slerp(swimExitFootRotations[leg],groundPoseHeadings[leg]*restRotation["foot"+suffix],blend);
    Quaternion heading=rotation*Quaternion.Inverse(restRotation["foot"+suffix]);
    if(!airborne&&surface!=null)target.y=Mathf.Max(target.y,GroundFootHeight(leg,target,heading,surface));
    Vector3 knee=Vector3.Slerp((B("calf"+suffix).position-B("thigh"+suffix).position).normalized,groundPoseHints[leg].normalized,blend);
    groundPoseTargets[leg]=target;groundPoseHeadings[leg]=heading;groundPoseHints[leg]=knee;
    Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,target,knee);B("foot"+suffix).rotation=rotation;
   }
   if(!airborne&&surface!=null&&shoePoseMeshes.Count>0)CorrectShoeContact(surface);
  }
  public void BeginLeafBoarding(Vector3 p){
   Vector3 pawn=root.parent==null?p:root.parent.TransformPoint(p);boardingRootOffset=root.position-pawn;boardingStartRotation=root.rotation;
   boardingStartBones.Clear();foreach(var bone in bones)boardingStartBones[bone.Key]=bone.Value.localRotation;
   for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";boardingStartFeet[leg]=B("foot"+suffix).position;boardingStartFootRotations[leg]=B("foot"+suffix).rotation;boardingStartHands[leg]=B("hand"+suffix).position;}
   wasSwimming=wasWalking=wasResting=false;swimExitElapsed=SwimExitDuration;walkWeight=groundDrop=RestAmount=0;
  }
  public void LeafBoardPose(Vector3 p,Vector3 end,Quaternion heading,Vector3 leafDelta,Vector3 braceCentre,float progress,float time,System.Func<Vector3,float> surface){
   Quaternion worldHeading=root.parent==null?heading:root.parent.rotation*heading;
   if(progress>=1){root.rotation=worldHeading;Pose(p,Vector3.zero,false,false,time,groundSurface:surface);return;}
   travellerStyle?.ShowHands(true,true);
   float dt=Mathf.Clamp(time-previousTime,0,.1f);previousTime=time;ApplyPose(p,Vector3.zero,true,time,0,null,null,0,dt,null);
   Vector3 worldPawn=root.parent==null?p:root.parent.TransformPoint(p),worldEnd=root.parent==null?end:root.parent.TransformPoint(end);
   root.rotation=Quaternion.Slerp(boardingStartRotation,worldHeading,Mathf.SmoothStep(0,1,progress));root.position=worldPawn+boardingRootOffset*(1-Mathf.SmoothStep(0,1,progress/.35f));
   float draw=Mathf.SmoothStep(0,1,progress/.3f),release=Mathf.SmoothStep(0,1,(progress-.18f)/.22f);
   for(int leg=0;leg<2;leg++){
    string suffix=leg==0?"_r":"_l";Vector3 endFoot=worldEnd+worldHeading*restPosition["foot"+suffix];Quaternion footHeading=GroundFootRotation(endFoot,worldHeading,surface);endFoot.y=GroundFootHeight(leg,endFoot,footHeading,surface);
    float plantAt=leg==0?.42f:.48f,plant=Mathf.SmoothStep(0,1,(progress-plantAt)/(1-plantAt));Vector3 tucked=root.position+worldHeading*new Vector3(restPosition["foot"+suffix].x,.04f,.38f);
    Vector3 foot=Vector3.Lerp(Vector3.Lerp(boardingStartFeet[leg]+leafDelta,tucked,draw),endFoot,plant);foot.y+=.08f*Mathf.Sin(plant*Mathf.PI);
    groundPoseHints[leg]=worldHeading*Vector3.forward;Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,foot,groundPoseHints[leg]);B("foot"+suffix).rotation=Quaternion.Slerp(boardingStartFootRotations[leg],footHeading*restRotation["foot"+suffix],draw);groundPoseTargets[leg]=foot;groundPoseHeadings[leg]=footHeading;
    float sign=Mathf.Sign(restPosition["hand"+suffix].x);Vector3 brace=braceCentre+worldHeading*Vector3.right*sign*.18f;brace.y=surface(brace)+.045f;
    Vector3 hand=Vector3.Lerp(Vector3.Lerp(boardingStartHands[leg]+leafDelta,brace,Mathf.SmoothStep(0,1,progress/.12f)),root.TransformPoint(new Vector3(sign*.29f,.85f,.06f)),release);
    Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,hand,root.right*sign-Vector3.up*.3f-root.forward*.15f);
    Wrist(suffix,B("hand"+suffix).position-B("lowerarm"+suffix).position,Vector3.Slerp(-Vector3.up,-root.right*sign,release));Fingers(suffix,Mathf.Lerp(4,12,release));
   }
   float blend=Mathf.SmoothStep(0,1,progress/.12f);foreach(var bone in bones)bone.Value.localRotation=Quaternion.Slerp(boardingStartBones[bone.Key],bone.Value.localRotation,blend);
   CorrectBoardingHandContact(surface,Mathf.SmoothStep(0,1,(progress-.08f)/.04f)*(1-release));
   lastPoseAirborne=true;lastGroundSurfaceProvided=true;wasSwimming=wasWalking=wasResting=false;
  }
  public void SwimPose(Vector3 p,Vector3 velocity,float time) {
   float dt=Mathf.Clamp(time-previousTime,0,.1f);Vector3 flat=new Vector3(velocity.x,0,velocity.z);
   if(!wasSwimming){swimCycle=.7f;swimEffort=Mathf.Clamp01(flat.magnitude/1.1f);}
   Quaternion heading=Quaternion.Euler(0,root.localEulerAngles.y,0);
   if(flat.sqrMagnitude>.02f)heading=Quaternion.Slerp(heading,Quaternion.LookRotation(flat),1-Mathf.Exp(-dt*5));
   swimExitElapsed=SwimExitDuration;applyingSwimPose=true;Pose(p,Vector3.zero,false,true,time);applyingSwimPose=false;
   swimEffort=Mathf.MoveTowards(swimEffort,Mathf.Clamp01(flat.magnitude/1.1f),dt*2.5f);
   swimCycle=Mathf.Repeat(swimCycle+dt/Mathf.Lerp(3.4f,2.65f,swimEffort),1);
   float phase=swimCycle,breath=Mathf.SmoothStep(0,1,(phase-.1f)/.14f)*(1-Mathf.SmoothStep(0,1,(phase-.3f)/.14f))*swimEffort;
   float movingStroke=1-Mathf.SmoothStep(0,1,(phase-.72f)/.08f);
   float strokeBlend=Mathf.SmoothStep(0,1,swimEffort),scullCycle=phase*Mathf.PI*2;
   float bodyPitch=Mathf.Lerp(12,75-3*breath,strokeBlend);
   float pelvisPivot=restPosition["pelvis"].y*(Mathf.Cos(75*Mathf.Deg2Rad)-Mathf.Cos(bodyPitch*Mathf.Deg2Rad));
   float surfaceLift=ReturnWaterSurface.Height(p.x,p.z,time)-ReturnWaterSurface.BaseHeight;
   root.localRotation=heading*Quaternion.Euler(bodyPitch,0,(1-strokeBlend)*.8f*Mathf.Sin(scullCycle));
   root.localPosition=p+Vector3.up*(-.025f+surfaceLift+pelvisPivot+.008f*Mathf.Sin(phase*Mathf.PI*2)*movingStroke);
    B("spine_01").localRotation*=Quaternion.Euler(-2*breath,0,0);B("spine_02").localRotation*=Quaternion.Euler(-3*breath,0,0);
   B("spine_03").localRotation*=Quaternion.Euler(-4*strokeBlend-4*breath,0,0);B("head").localRotation*=Quaternion.Euler(-8*strokeBlend-52*breath,Mathf.Sin(scullCycle*.5f)*(1-strokeBlend)*1.5f,0);
   // Stay buoyant at rest, with the neck clear and the limbs working below the surface.
   float idleHeadHeight=ReturnWaterSurface.Height(p.x,p.z,time)+.045f+.012f*Mathf.Sin(scullCycle*2);
   root.position+=Vector3.up*(idleHeadHeight-B("head").position.y)*(1-strokeBlend);
   // Pull and breathe first; recover the hands before the kick, then hold the glide.
   Vector3 glideHand=new Vector3(.13f,1.94f,.13f),wideHand=new Vector3(.5f,1.72f,.21f),gatherHand=new Vector3(.15f,1.24f,.24f);
   Vector3 handPath=phase<.14f?Vector3.Lerp(glideHand,wideHand,Mathf.SmoothStep(0,1,phase/.14f)):phase<.3f?Vector3.Lerp(wideHand,gatherHand,Mathf.SmoothStep(0,1,(phase-.14f)/.16f)):phase<.44f?Vector3.Lerp(gatherHand,glideHand,Mathf.SmoothStep(0,1,(phase-.3f)/.14f)):glideHand;
   Vector3 glideFoot=new Vector3(.1f,.08f,.02f),drawFoot=new Vector3(.18f,.54f,-.14f);
   Vector3 footPath=glideFoot;
   // Recover while the arms pull, then sweep out and back without stopping at the widest point.
   if(phase>=.24f&&phase<.46f)footPath=Vector3.Lerp(glideFoot,drawFoot,Mathf.SmoothStep(0,1,(phase-.24f)/.22f));
   else if(phase>=.46f&&phase<.70f)
   {
    float kick=Mathf.SmoothStep(0,1,(phase-.46f)/.24f),remaining=1-kick;
    Vector3 outward=new Vector3(.36f,.45f,-.10f),backward=new Vector3(.35f,.065f,.03f);
    footPath=remaining*remaining*remaining*drawFoot+3*remaining*remaining*kick*outward+3*remaining*kick*kick*backward+kick*kick*kick*glideFoot;
   }
   float footTurn=Mathf.SmoothStep(0,1,(phase-.24f)/.22f)*(1-Mathf.SmoothStep(0,1,(phase-.60f)/.10f))*swimEffort;
   for(int side=-1;side<=1;side+=2) {
    string suffix=side>0?"_l":"_r";float outward=Mathf.Sign(restPosition["hand"+suffix].x);
    Vector3 scull=new Vector3(.19f+.035f*Mathf.Sin(scullCycle),1.16f+.015f*Mathf.Cos(scullCycle),.21f+.035f*Mathf.Cos(scullCycle));
    Vector3 handLocal=Vector3.Lerp(scull,handPath,strokeBlend);handLocal.x*=outward;Vector3 hand=root.TransformPoint(handLocal);
    Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,hand,root.right*outward-root.forward*.2f);
    Vector3 scullPalm=Vector3.Slerp(-root.up,root.right*outward,.28f*Mathf.Sin(scullCycle));
    Wrist(suffix,B("hand"+suffix).position-B("lowerarm"+suffix).position,Vector3.Slerp(scullPalm,Vector3.Slerp(root.forward,-root.up,.8f*breath),strokeBlend));Fingers(suffix,6+8*breath);
    float legCycle=scullCycle+(side>0?Mathf.PI:0);
    Vector3 idleFoot=new Vector3(.14f+.025f*Mathf.Cos(legCycle),.17f+.065f*Mathf.Sin(legCycle),.07f+.09f*Mathf.Cos(legCycle));
    Vector3 footLocal=Vector3.Lerp(idleFoot,footPath,strokeBlend);footLocal.x*=Mathf.Sign(restPosition["foot"+suffix].x);Vector3 foot=root.TransformPoint(footLocal);
     float footSide=Mathf.Sign(restPosition["foot"+suffix].x);
     Vector3 kneeHint=root.forward+root.right*footSide*.20f*footTurn;
     float anklePitch=Mathf.Lerp(12+8*Mathf.Sin(legCycle),Mathf.Lerp(80,8,footTurn),strokeBlend);
     Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,foot,kneeHint);
     PoseSwimFoot(side<0?0:1,root.rotation*Quaternion.Euler(anklePitch,footSide*Mathf.Lerp(-4,32,footTurn)*strokeBlend,0));
   }
   lastSwimPosition=p;wasSwimming=true;
  }
  void PoseSwimFoot(int leg,Quaternion desired){
   string suffix=leg==0?"_r":"_l",toe="ball"+suffix;Vector2 angles=GroundAnkleAngles(leg,desired,out var neutral);
   maximumRequestedSwimAnkleAngles=Vector2.Max(maximumRequestedSwimAnkleAngles,new Vector2(Mathf.Abs(angles.x),Mathf.Abs(angles.y)));
   float pitch=Mathf.Clamp(angles.x,-25,55),roll=Mathf.Clamp(angles.y,-12,12);
   if(Mathf.Abs(pitch-angles.x)>.001f||Mathf.Abs(roll-angles.y)>.001f)swimAnkleLimitCount++;
   // The shin supplies the ankle frame; share the remaining point with the existing toe joint.
   B("foot"+suffix).rotation=neutral*Quaternion.Euler(pitch,0,roll)*restRotation["foot"+suffix];
   if(B(toe)!=null)B(toe).localRotation=bind[toe]*Quaternion.AngleAxis(Mathf.Clamp(angles.x-pitch,-25,25),Quaternion.Inverse(restRotation[toe])*Vector3.right);
   Vector2 applied=GroundAnkleAngles(leg,B("foot"+suffix).rotation*Quaternion.Inverse(restRotation["foot"+suffix]),out var actualNeutral);
   maximumAppliedSwimAnkleAngles=Vector2.Max(maximumAppliedSwimAnkleAngles,new Vector2(Mathf.Abs(applied.x),Mathf.Abs(applied.y)));
  }
  void ApplyPose(Vector3 p,Vector3 velocity,bool airborne,float time,float reach,float? facing,System.Func<Vector3,float> restingSurface,float amount,float dt,System.Func<Vector3,float> groundSurface,bool quietGroundWalk=true,float groundStrideLimit=1,bool levelGroundWalk=false) {
   Vector3 previousRootPosition=root.position;
   for(int leg=0;leg<2;leg++){string suffix=leg==0?"_r":"_l";previousSolvedFoot[leg]=B("foot"+suffix).position;previousSolvedFootHeading[leg]=B("foot"+suffix).rotation*Quaternion.Inverse(restRotation["foot"+suffix]);groundLiftStarting[leg]=false;if(traceStreetLandings)streetLaneShift[leg]=Vector3.zero;}
   Vector3 previousVelocity=lastSkinVelocity;lastSkinVelocity=velocity;lastSkinTime=time;levelGroundPose=levelGroundWalk;
   if(traceStreetLandings){streetPreviousVelocity=previousVelocity;streetPoseDt=dt;}
   lastPoseAirborne=airborne;lastGroundSurfaceProvided=groundSurface!=null;
   bool rest=amount>0;constrainGroundLegPose=quietGroundWalk&&groundSurface!=null&&!rest&&!airborne;constrainGroundAnklePose=constrainGroundLegPose&&!applyingSwimPose&&swimExitElapsed>=SwimExitDuration;float seated=Mathf.SmoothStep(0,1,amount/.55f),recline=Mathf.SmoothStep(0,1,(amount-.45f)/.55f),crouch=Mathf.Sin(seated*Mathf.PI);
   root.localPosition=p;Vector3 flat=new Vector3(velocity.x,0,velocity.z);
   groundSupportTranslation=root.position-previousRootPosition-(root.parent==null?flat:root.parent.TransformVector(flat))*dt;groundSupportTranslation.y=0;
   if(rest)root.localRotation=restHeading*Quaternion.Euler(crouch*8-recline*90,0,0);
   else if(facing.HasValue)root.localRotation=Quaternion.Euler(0,facing.Value,0);
   else if(wasResting)root.localRotation=restHeading;
   else if(flat.sqrMagnitude>.02f)root.localRotation=Quaternion.Slerp(root.localRotation,Quaternion.LookRotation(flat),1-Mathf.Exp(-dt*12));
   foreach(var pair in bind){bones[pair.Key].localRotation=pair.Value;bones[pair.Key].localPosition=bindPosition[pair.Key];}
   Vector3 anchor=root.parent==null?p:root.parent.TransformPoint(p);Quaternion heading=root.parent==null?restHeading:root.parent.rotation*restHeading;
   if(rest&&restingSurface!=null){Vector3 hip=anchor+heading*new Vector3(restPosition["pelvis"].x,0,restPosition["pelvis"].z-.18f*seated);hip.y=Mathf.Lerp(anchor.y+restPosition["pelvis"].y,restingSurface(hip)+.16f,seated);root.position+=hip-B("pelvis").position;}
    float speed=flat.magnitude,cycleDistance=Mathf.Lerp(.7f,1.25f,Mathf.Clamp01(speed/3.8f));bool walking=!rest&&!airborne&&speed>.025f;
    bool recorded=quietGroundWalk&&groundSurface!=null&&!rest&&!airborne;
    if(recorded){
     Vector3 slopeHeading=root.parent==null?flat.normalized:root.parent.TransformDirection(flat.normalized);
     float span=walkingLegLength*.35f,grade=walking?(groundSurface(root.position+slopeHeading*span)-groundSurface(root.position-slopeHeading*span))/(span*2):0;
     // Take shorter steps on inclines instead of fitting a level-ground stride by crouching.
     float targetScale=Mathf.Clamp(1/(1+Mathf.Abs(grade)),.65f,1)*Mathf.Clamp(groundStrideLimit,.5f,1);
     groundStrideScale=Mathf.MoveTowards(groundStrideScale,targetScale,dt*1.8f);
     cycleDistance=ReturnRecordedWalk.Stride*walkingLegLength*groundStrideScale;
    }else groundStrideScale=1;
    Vector3 travel=root.parent==null?flat.normalized:root.parent.TransformDirection(flat.normalized);
    walkWeight=Mathf.MoveTowards(walkWeight,walking?1:0,dt*6);
     bool reverseLevelWalk=levelGroundWalk&&walking&&wasWalking&&Vector3.Dot(flat,new Vector3(previousVelocity.x,0,previousVelocity.z))<0;
     if(traceStreetLandings)streetReversed=reverseLevelWalk;
     if(walking&&(!wasWalking||reverseLevelWalk)){
      float entryPhase=.25f;
      if(reverseLevelWalk){
       bool rightSupport=groundContacts[0].valid&&!swinging[0],leftSupport=groundContacts[1].valid&&!swinging[1];
       bool supportRight=rightSupport!=leftSupport?rightSupport:Vector3.Dot(previousSolvedFoot[0]-previousSolvedFoot[1],travel)>=0;
       entryPhase=supportRight ? .25f : .75f;
      }
      walkingEpisode++;gait=entryPhase*Mathf.PI*2;
      for(int i=0;i<2;i++){
       // Transfer support on reversal, releasing the old trailing plant before it pulls the hips down.
       plantedFoot[i]=levelGroundWalk?previousSolvedFoot[i]:root.TransformPoint(restPosition[i==0?"foot_r":"foot_l"]);swingEnd[i]=plantedFoot[i];swinging[i]=false;
       if(traceStreetLandings)streetRawLanding[i]=plantedFoot[i];
       if(levelGroundWalk){groundContacts[i].valid=false;groundReplantElapsed[i]=-1;groundLandingHeld[i]=groundSkipHeelStrike[i]=groundLateSwing[i]=false;}
      }
     }
   if(walking)gait+=speed*dt*Mathf.PI*2/cycleDistance;
    float gaitSide=Mathf.Sign(restPosition["foot_l"].x),hipTurn=Mathf.Cos(gait)*walkWeight*gaitSide;
    var recordedBody=recorded?ReturnRecordedWalk.SampleBody(gait/(Mathf.PI*2)):default(ReturnRecordedWalk.Body);
    Vector3 recordedPelvisShift=MirrorWalk(recordedBody.pelvis,gaitSide)*walkingLegLength+Vector3.down*.027f;
    if(levelGroundWalk)recordedPelvisShift.y=recordedBody.pelvis.y*walkingLegLength*.35f;
   for(int leg=0;leg<2;leg++){
    float phase=Mathf.Repeat(gait/(Mathf.PI*2)+(leg==1?.5f:0),1),stance=recorded?ReturnRecordedWalk.Stance(leg):.6f;
     // The level street follows the recorded cycle; terrain catch-up steps must not insert a third stride.
     if(!constrainGroundAnklePose||!walking){groundReplantElapsed[leg]=-1;groundLandingHeld[leg]=groundSkipHeelStrike[leg]=groundLateSwing[leg]=false;}
     else if(levelGroundWalk){groundReplantElapsed[leg]=-1;groundLandingHeld[leg]=groundSkipHeelStrike[leg]=false;}
    else {
     if(groundReplantElapsed[leg]>=0){
      groundReplantElapsed[leg]+=dt;swingStart[leg]+=groundSupportTranslation;
      swingEnd[leg]=GroundFootLane(leg,root.TransformPoint(restPosition[leg==0?"foot_r":"foot_l"])+travel*(speed*groundReplantDuration[leg]*.5f));
      if(groundReplantElapsed[leg]>=groundReplantDuration[leg]){
       plantedFoot[leg]=swingEnd[leg];groundReplantElapsed[leg]=-1;groundLandingHeld[leg]=phase>=stance;groundSkipHeelStrike[leg]=true;groundLateSwing[leg]=false;
       // Inherit the preceding solved landing before registering a held plant.
       swinging[leg]=true;
      }
     }
     if(phase<stance)groundLandingHeld[leg]=false;
     if(groundReplantElapsed[leg]<0&&(phase<stance||groundLandingHeld[leg])&&groundReplantElapsed[1-leg]<0&&groundContacts[1-leg].valid&&walkWeight>.99f){
      int other=1-leg;float otherPhase=Mathf.Repeat(gait/(Mathf.PI*2)+(other==1?.5f:0),1),otherStance=recorded?ReturnRecordedWalk.Stance(other):.6f;
      float supportSeconds=(groundLandingHeld[other]?1-otherPhase+otherStance:otherStance-otherPhase)*cycleDistance/Mathf.Max(speed,.1f);
      Vector3 held=previousSolvedFoot[leg]+groundSupportTranslation,anticipated=held-travel*(speed*.08f);
      string suffix=leg==0?"_r":"_l";Transform hip=B("thigh"+suffix),knee=B("calf"+suffix),ankle=B("foot"+suffix);
      float legReach=(Vector3.Distance(hip.position,knee.position)+Vector3.Distance(knee.position,ankle.position))*.985f;
      Vector3 intendedHip=hip.position+root.TransformVector(recordedPelvisShift)*walkWeight;
      intendedHip.y+=groundSurface(root.position+travel*(speed*.08f))-groundSurface(root.position);
      bool pullsBodyDown=(anticipated-intendedHip).sqrMagnitude>legReach*legReach;
      if(supportSeconds>.14f&&(pullsBodyDown||(GroundFootLane(leg,anticipated)-anticipated).sqrMagnitude>.000025f)){
       // Release a rear plant before rising terrain forces the pelvis down to keep it reachable.
       groundReplantDuration[leg]=Mathf.Min(Mathf.Clamp(cycleDistance*(1-stance)/Mathf.Max(speed,.1f),.18f,.32f),supportSeconds*.85f);groundReplantElapsed[leg]=0;groundLandingHeld[leg]=groundSkipHeelStrike[leg]=groundLateSwing[leg]=false;
       groundReplantStartHeading[leg]=previousSolvedFootHeading[leg];
       swingStart[leg]=held;swingEnd[leg]=GroundFootLane(leg,root.TransformPoint(restPosition[leg==0?"foot_r":"foot_l"])+travel*(speed*groundReplantDuration[leg]*.5f));
       swinging[leg]=true;groundLiftStarting[leg]=true;groundContacts[leg].valid=false;
      }
     }
    }
    groundContactWalking[leg]=constrainGroundAnklePose&&walking&&walkWeight>.99f&&groundReplantElapsed[leg]<0&&(phase<stance||groundLandingHeld[leg]);
    if(!groundContactWalking[leg])groundContacts[leg].valid=false;else if(groundContacts[leg].valid)groundContacts[leg].anchor+=groundSupportTranslation;
   }
    float torsoSupport=0;var pelvis=B("pelvis");if(pelvis!=null&&!rest){
     // Transfer weight toward the planted leg; rise between the double-support poses.
     Vector3 shift=recorded?recordedPelvisShift:new Vector3(-Mathf.Sin(gait)*.025f*gaitSide,-.027f-.009f*Mathf.Cos(gait*2),0);
     for(int leg=0;leg<2;leg++)if(groundReplantElapsed[leg]>=0){float u=Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg]);shift.x=Mathf.Lerp(shift.x,Mathf.Sign(restPosition[leg==0?"foot_l":"foot_r"].x)*walkingLegLength*.018f,Mathf.Sin(u*Mathf.PI));}
     if(recorded&&constrainGroundAnklePose&&wasWalking&&walking){
      // Actual shoe support can differ from the recorded phase after a slope or corrective step.
      Vector3 rightSole=groundLowestWorld[0]+groundSupportTranslation,leftSole=groundLowestWorld[1]+groundSupportTranslation;
      float rightSupport=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.014f,.025f,rightSole.y-groundSurface(rightSole)));
      float leftSupport=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.014f,.025f,leftSole.y-groundSurface(leftSole)));
      int support=rightSupport>leftSupport?0:1;float blend=Mathf.Abs(rightSupport-leftSupport);
      torsoSupport=blend;
      float supportSide=Vector3.Dot(previousSolvedFoot[support]+groundSupportTranslation-root.position,root.right);
      shift.x=Mathf.Lerp(shift.x,Mathf.Sign(supportSide)*Mathf.Max(Mathf.Abs(shift.x),walkingLegLength*.018f),blend);
     }
     pelvis.position+=root.TransformVector(shift)*walkWeight;
     pelvis.rotation=recorded?Quaternion.AngleAxis(recordedBody.pelvisYaw*.75f*gaitSide*walkWeight,root.up)*Quaternion.AngleAxis(recordedBody.pelvisPitch*walkWeight,root.right)*Quaternion.AngleAxis(recordedBody.pelvisRoll*.65f*gaitSide*walkWeight,root.forward)*pelvis.rotation:Quaternion.AngleAxis(3*hipTurn,root.up)*pelvis.rotation;
    }
     if(!rest&&!airborne&&groundSurface!=null){
      float needed=0;
      for(int leg=0;leg<2;leg++){
       string suffix=leg==0?"_r":"_l";float phase=Mathf.Repeat(gait/(Mathf.PI*2)+(leg==1?.5f:0),1),stance=recorded?ReturnRecordedWalk.Stance(leg):.6f;
       if(walking&&(groundReplantElapsed[leg]>=0||phase>=stance&&!groundLandingHeld[leg]))continue;
       Vector3 foot=root.TransformPoint(restPosition["foot"+suffix]);if(walking)foot=swinging[leg]?swingEnd[leg]:plantedFoot[leg];
       Quaternion orientation=GroundFootRotation(foot,root.rotation,groundSurface);
       // A new plant inherits the solved landing pose before its contact is registered.
       if(groundContactWalking[leg]&&(groundContacts[leg].valid||swinging[leg])){foot=previousSolvedFoot[leg]+groundSupportTranslation;orientation=previousSolvedFootHeading[leg];}
        else if(recorded){float roll=GroundWalkRoll(leg,phase);foot+=RockingFootOffset(leg,orientation,roll);orientation=Quaternion.AngleAxis(roll,orientation*Vector3.right)*orientation;}
       foot.y=GroundFootHeight(leg,foot,orientation,groundSurface);
       // Fit the pelvis to the fixed stance first; shortening the plant toward the moving root makes it slide.
       var upper=B("thigh"+suffix);float length=Vector3.Distance(upper.position,B("calf"+suffix).position)+Vector3.Distance(B("calf"+suffix).position,B("foot"+suffix).position),horizontal=new Vector2(upper.position.x-foot.x,upper.position.z-foot.z).magnitude;
       if(constrainGroundLegPose)length*=.985f;
       needed=Mathf.Max(needed,upper.position.y-foot.y-Mathf.Sqrt(Mathf.Max(0,length*length-horizontal*horizontal))+(levelGroundWalk ? .003f : .012f));
      }
      needed=Mathf.Clamp(needed,0,.3f);groundDrop=needed>groundDrop?needed:Mathf.Lerp(groundDrop,needed,1-Mathf.Exp(-dt*12));pelvis.position-=Vector3.up*groundDrop;
     }
     else if(!rest&&airborne&&groundSurface!=null&&quietGroundWalk){
      // Release the supported pelvis height gradually when stepping across a short gap.
      groundDrop=Mathf.Lerp(groundDrop,0,1-Mathf.Exp(-dt*12));pelvis.position-=Vector3.up*groundDrop;
     }else groundDrop=0;
   var spine=B("spine_01");if(spine!=null)spine.localRotation*=Quaternion.Euler(crouch*8,0,0);
    var chest=B("spine_03");if(chest!=null)chest.localRotation*=Quaternion.Euler(Mathf.Sin(time*1.7f)*Mathf.Lerp(.7f,.4f,amount)+reach*4+crouch*12+recline*(1-recline)*15,0,recorded?0:-Mathf.Sin(gait)*walkWeight*(1-amount)*1.2f);
    if(chest!=null&&!rest){
     float yaw=recorded?recordedBody.chestYaw*.75f*gaitSide*walkWeight:-5.5f*hipTurn;
     // Preserve the clip in double support; counterbalance the adapted planted pose continuously.
     if(recorded&&torsoSupport>0)yaw=Mathf.Lerp(yaw,-PosedYaw("pelvis")*.875f,torsoSupport);
     // Bring the walking torso forward around the supported pelvis without displacing either hip.
     float forwardLean=levelGroundWalk?Mathf.Clamp(recordedBody.chestLean*.35f,1.5f,4f):recordedBody.chestLean+3f;
     chest.rotation=Quaternion.AngleAxis(yaw,root.up)*(recorded?Quaternion.AngleAxis(forwardLean*walkWeight,root.right):Quaternion.identity)*chest.rotation;
    }
   var head=B("head");if(head!=null)head.localRotation*=Quaternion.Slerp(Quaternion.Euler(Mathf.Sin(time*.65f)*1.4f,Mathf.Sin(time*.37f)*2,0),Quaternion.Euler(1,5,0),amount);
   if(rest&&restingSurface!=null&&amount>.75f){float gap=float.NegativeInfinity;foreach(Vector3 sample in restSupport){Vector3 world=root.TransformPoint(sample);gap=Mathf.Max(gap,restingSurface(world)-world.y);}root.position+=Vector3.up*(gap+.006f)*Mathf.SmoothStep(0,1,(amount-.75f)/.25f);}
   for(int side=-1;side<=1;side+=2) {
    int leg=side<0?0:1;string suffix=side>0?"_l":"_r";float phase=Mathf.Repeat(gait/(Mathf.PI*2)+(side>0?.5f:0),1),stance=recorded?ReturnRecordedWalk.Stance(leg):.6f;var recordedFoot=recorded?ReturnRecordedWalk.SampleFoot(phase,leg):default(ReturnRecordedWalk.Foot);Vector3 neutral=root.TransformPoint(restPosition["foot"+suffix]),foot=neutral;float roll=recorded?GroundWalkRoll(leg,phase):0;
    Quaternion footHeading=rest?Quaternion.Slerp(heading,root.rotation,Mathf.SmoothStep(0,1,(amount-.48f)/.52f)):root.rotation;
     if(walking&&groundReplantElapsed[leg]>=0){
      swingEnd[leg]=GroundFootLane(leg,swingEnd[leg]);float u=Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg]);foot=Vector3.Lerp(swingStart[leg],swingEnd[leg],Mathf.SmoothStep(0,1,u));swinging[leg]=true;
     }else if(walking){
      // Terrain follows the turning support lane; a street landing stays fixed in world space.
       if(constrainGroundAnklePose&&!levelGroundWalk&&swinging[leg]){Vector3 beforeLane=swingEnd[leg];swingEnd[leg]=GroundFootLane(leg,swingEnd[leg]);if(traceStreetLandings)streetLaneShift[leg]+=swingEnd[leg]-beforeLane;}
      bool swing=phase>=stance&&!groundLandingHeld[leg];float u=(phase-stance)/(1-stance);
      // Choose one landing point when the foot lifts, then plant at that same point.
      if(swing&&!swinging[leg]){
       bool supportedStart=constrainGroundLegPose&&wasWalking;
       swingStart[leg]=supportedStart?previousSolvedFoot[leg]:plantedFoot[leg];
       groundLateSwing[leg]=reverseLevelWalk;groundSkipHeelStrike[leg]=false;groundSwingProgressStart[leg]=recordedFoot.progress;
       if(supportedStart){Vector3 gap=plantedFoot[leg]-previousSolvedFoot[leg];maximumPlannedLiftGap[leg]=Mathf.Max(maximumPlannedLiftGap[leg],new Vector2(gap.x,gap.z).magnitude);walkingLiftStartCount[leg]++;groundLiftStarting[leg]=true;}
       Vector3 landingNeutral=levelGroundWalk?root.position+Quaternion.FromToRotation(root.forward,travel)*(neutral-root.position):neutral;
       swingEnd[leg]=landingNeutral+travel*(recorded?cycleDistance*(1-phase)+ReturnRecordedWalk.Lead(leg)*walkingLegLength*groundStrideScale:cycleDistance*(.3f+.4f*(1-u)));
       if(traceStreetLandings)streetRawLanding[leg]=swingEnd[leg];
       if(constrainGroundAnklePose&&!levelGroundWalk){Vector3 beforeLane=swingEnd[leg];swingEnd[leg]=GroundFootLane(leg,swingEnd[leg]);if(traceStreetLandings)streetLaneShift[leg]+=swingEnd[leg]-beforeLane;}
      }
      if(swing){float progress=recorded?recordedFoot.progress:Mathf.SmoothStep(0,1,u);if(groundLateSwing[leg]){progress=Mathf.InverseLerp(groundSwingProgressStart[leg],1,progress);roll=0;}foot=Vector3.Lerp(swingStart[leg],swingEnd[leg],progress)+root.up*(recorded?recordedFoot.lift*walkingLegLength:Mathf.Sin(u*Mathf.PI)*Mathf.Lerp(.09f,.17f,speed/3.8f));if(!recorded)roll=Mathf.Sin(u*Mathf.PI*2)*12;}
      else {if(swinging[leg])plantedFoot[leg]=swingEnd[leg];foot=plantedFoot[leg];groundLateSwing[leg]=false;}
     swinging[leg]=swing;foot=Vector3.Lerp(neutral,foot,walkWeight);
    }else if(airborne)foot=root.TransformPoint(restPosition["foot"+suffix]+new Vector3(0,.19f,side*.15f));
    else if(rest&&restingSurface!=null){Vector3 plant=anchor+heading*restPosition["foot"+suffix];foot=Vector3.Lerp(plant,root.TransformPoint(restPosition["foot"+suffix]+new Vector3(Mathf.Sign(restPosition["foot"+suffix].x)*.012f,side>0?.035f:.05f,0)),Mathf.SmoothStep(0,1,(amount-.48f)/.52f));float support=float.NegativeInfinity;foreach(Vector3 point in footSupport[leg]){Vector3 offset=footHeading*point;support=Mathf.Max(support,restingSurface(foot+offset)-offset.y);}foot.y=support+.006f;}
    else if(!rest&&walkWeight>0)foot=Vector3.Lerp(neutral,plantedFoot[leg],walkWeight);
    float groundLift=0;if(!rest&&!airborne&&groundSurface!=null){
     if(constrainGroundAnklePose&&!groundContactWalking[leg]&&groundReplantElapsed[leg]<0)foot=GroundFootLane(leg,foot);
     footHeading=GroundFootRotation(foot,root.rotation,groundSurface);groundTerrainHeadings[leg]=footHeading;
     if(GroundWalkReviewActive&&groundReplantElapsed[leg]>=0){
      float orientationProgress=Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg]);
      footHeading=Quaternion.Slerp(groundReplantStartHeading[leg],footHeading,Mathf.SmoothStep(0,1,orientationProgress));
     }
     if(recorded&&(!walking||phase<stance))foot+=RockingFootOffset(leg,footHeading,roll);
     if(groundReplantElapsed[leg]>=0)groundLift=Mathf.Sin(Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg])*Mathf.PI)*walkingLegLength*.055f;
     else if(walking&&swinging[leg]){
      float progress=groundLateSwing[leg]?Mathf.InverseLerp(groundSwingProgressStart[leg],1,recordedFoot.progress):0;
      groundLift=(groundLateSwing[leg]?Mathf.Sin(progress*Mathf.PI)*walkingLegLength*.055f:recorded?recordedFoot.lift*walkingLegLength:Mathf.Sin((phase-stance)/(1-stance)*Mathf.PI)*Mathf.Lerp(.09f,.17f,speed/3.8f))*walkWeight;
     }
     float plannedFootY=GroundFootHeight(leg,foot,Quaternion.AngleAxis(roll,footHeading*Vector3.right)*footHeading,groundSurface)+groundLift;
     if(groundReplantElapsed[leg]>=0){
      float liftProgress=Mathf.Clamp01(groundReplantElapsed[leg]/groundReplantDuration[leg]);
      // Leave from the solved ankle height before joining the terrain-relative swing arc.
      plannedFootY=Mathf.Max(plannedFootY,Mathf.Lerp(swingStart[leg].y,plannedFootY,Mathf.SmoothStep(0,1,liftProgress)));
     }
      else if(levelGroundWalk&&groundLateSwing[leg]&&swinging[leg]){
       float redirectProgress=Mathf.InverseLerp(groundSwingProgressStart[leg],1,recordedFoot.progress);
       plannedFootY=Mathf.Max(plannedFootY,Mathf.Lerp(swingStart[leg].y,plannedFootY,Mathf.SmoothStep(0,1,redirectProgress)));
      }
     foot.y=plannedFootY;if(constrainGroundLegPose)foot=ReachableGroundFoot(leg,foot,groundLift,groundSurface,Quaternion.AngleAxis(roll,footHeading*Vector3.right)*footHeading);
    }
     groundPoseTargets[leg]=foot;groundDesiredFootHeadings[leg]=groundPoseHeadings[leg]=Quaternion.AngleAxis(roll,footHeading*Vector3.right)*footHeading;groundPoseClearance[leg]=.006f+groundLift;groundPoseHints[leg]=recorded?Vector3.Slerp(root.forward,root.TransformDirection(MirrorWalk(recordedFoot.knee,gaitSide)),walkWeight):root.forward;
      if(!rest&&(walking||airborne||walkWeight>0||groundSurface!=null))Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,foot,groundPoseHints[leg]);
    if(rest&&restingSurface!=null)Chain("thigh"+suffix,"calf"+suffix,"foot"+suffix,foot,root.forward);
    groundPoseHeadings[leg]=LimitGroundAnkle(leg,groundDesiredFootHeadings[leg]);B("foot"+suffix).rotation=groundPoseHeadings[leg]*restRotation["foot"+suffix];
    Transform upper=B("upperarm"+suffix),lower=B("lowerarm"+suffix),hand=B("hand"+suffix);
    float armSwing=rest?0:Mathf.Sin(phase*Mathf.PI*2)*.16f*walkWeight;
    if(rest&&restingSurface!=null){if(amount>=1)RestHand(suffix,restingSurface);else TransitionHand(suffix,restingSurface,amount,anchor,heading);}
    else if(upper!=null&&hand!=null) {
     float armSide=Mathf.Sign(restPosition["hand"+suffix].x);Vector3 target=new Vector3(armSide*.29f,rest?.89f:airborne?1.05f:.85f,rest?-.04f:armSwing+.06f);
     if(side<0)target=Vector3.Lerp(target,new Vector3(armSide*.19f,1.37f,.59f),reach);
     Vector3 worldTarget=root.TransformPoint(target);if(rest&&restingSurface!=null)worldTarget.y=restingSurface(worldTarget)+.025f;
     Vector3 hint=rest?root.right*armSide+root.up*.1f+root.forward*.03f:-root.up-root.forward*.15f+root.right*armSide*.25f;
      if(recorded){
       Vector3 motionHand=side<0?recordedBody.rightHand:recordedBody.leftHand,motionElbow=side<0?recordedBody.rightElbow:recordedBody.leftElbow;
       worldTarget=Vector3.Lerp(root.TransformPoint(new Vector3(armSide*.29f,.85f,.06f)),pelvis.position+root.TransformVector(MirrorWalk(motionHand,gaitSide)*walkingLegLength),walkWeight);
       hint=Vector3.Slerp(hint.normalized,root.TransformDirection(MirrorWalk(motionElbow,gaitSide)),walkWeight);
       if(walkWeight>0){
        // Retarget the captured swing to this rig without locking the elbow at full reach.
        Vector3 shoulder=upper.position;float upperLength=Vector3.Distance(shoulder,lower.position),lowerLength=Vector3.Distance(lower.position,hand.position);
        float bend=Mathf.Lerp(22,42,Mathf.InverseLerp(-.18f,.28f,motionHand.z))*Mathf.Deg2Rad;
        float limit=Mathf.Sqrt(upperLength*upperLength+lowerLength*lowerLength+2*upperLength*lowerLength*Mathf.Cos(bend));
        Vector3 delta=worldTarget-shoulder;
        if(delta.sqrMagnitude>limit*limit)worldTarget=Vector3.Lerp(worldTarget,shoulder+delta.normalized*limit,walkWeight);
       }
       if(side<0)worldTarget=Vector3.Lerp(worldTarget,root.TransformPoint(new Vector3(armSide*.19f,1.37f,.59f)),reach);
      }
     Chain("upperarm"+suffix,"lowerarm"+suffix,"hand"+suffix,worldTarget,hint);
     Wrist(suffix,hand.position-lower.position,rest?-root.forward:-root.right*armSide);
    }
    if(!rest||restingSurface==null)Fingers(suffix,(rest?8:12)+(side<0?reach*25:0));
   }
    if(!rest&&!airborne&&groundSurface!=null&&shoePoseMeshes.Count>0)CorrectShoeContact(groundSurface);
   for(int leg=0;leg<2;leg++)if(!levelGroundWalk&&groundContactWalking[leg]&&groundContacts[leg].valid){
    float phase=Mathf.Repeat(gait/(Mathf.PI*2)+(leg==1?.5f:0),1),roll=GroundWalkRoll(leg,phase);Quaternion desired=groundDesiredFootHeadings[leg],terrain=Quaternion.AngleAxis(-roll,desired*Vector3.right)*desired;
    // Retain the unrolled ankle reference for a smooth stop and the next pose's support estimate.
    plantedFoot[leg]=B(leg==0?"foot_r":"foot_l").position-RockingFootOffset(leg,terrain,roll);
   }
    for(int leg=0;leg<2;leg++)if(groundLiftStarting[leg]){Vector3 step=B(leg==0?"foot_r":"foot_l").position-previousSolvedFoot[leg];maximumActualLiftStep[leg]=Mathf.Max(maximumActualLiftStep[leg],new Vector2(step.x,step.z).magnitude);}
    wasWalking=walking;
   float awakeBlink=Mathf.Clamp01(1-Mathf.Abs(((time+phaseOffset)%4.7f)-.12f)/.12f)*100,blink=Mathf.Lerp(awakeBlink,100,Mathf.SmoothStep(0,1,(amount-.72f)/.28f));
   poseBlink=blink;Face("Blink",blink);
  }
  static Vector3 MirrorWalk(Vector3 value,float mirror){value.x*=mirror;return value;}
  Vector3 RockingFootOffset(int leg,Quaternion heading,float roll)
  {
   Vector3 pivot=roll>=0?walkingToePivot[leg]:walkingHeelPivot[leg];
   Vector3 offset=heading*pivot-Quaternion.AngleAxis(roll,heading*Vector3.right)*(heading*pivot);offset.y=0;return offset;
  }
 }
}
