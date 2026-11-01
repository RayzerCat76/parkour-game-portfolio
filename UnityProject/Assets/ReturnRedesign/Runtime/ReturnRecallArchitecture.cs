using System;
using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign {
 // The silhouettes belong to the authored memory, and gain an actual shell as its depth returns.
 public sealed class ReturnRecallArchitecture {
  sealed class House { public Transform root,paperRoot; public float paperY,width; public Vector3 paperPosition,cityPosition; public Quaternion cityRotation; public Renderer[] renderers; }
  sealed class DepthObject { public Transform root; public float paperY,baseY,restoredScale=1,floorInset; public bool relocate; public Vector3 paperPosition,cityPosition; public Quaternion cityRotation; }
  sealed class Pier { public Transform shaft,head; public float platformY,localZ,fullHeight; }
  sealed class StreetViewBuilding { public Renderer[] renderers; public Bounds bounds; public bool hidden; }
  readonly List<House> houses=new List<House>();
  readonly List<DepthObject> objects=new List<DepthObject>();
  readonly List<Pier> piers=new List<Pier>();
  Transform street;
  readonly List<Transform> cityBlocks=new List<Transform>();
  readonly List<StreetViewBuilding> streetViewBuildings=new List<StreetViewBuilding>();
  public Vector3 CityCentre {get;private set;}
  public const float CityRadius=180;
  public int CityBuildingCount=>houses.Count+cityBlocks.Count;
  readonly List<Renderer> paper=new List<Renderer>();
  readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
  readonly ReturnVolumeArt art;
  readonly ReturnStreetCraft craft;
  readonly Material plaster,roof,wood,glass,clearGlass,roomWall,roomFloor,curtain,metal,door,stone,shade;
  const float Depth=4.6f;
  const float PavementLevel=-.012f;
  public int HouseCount=>houses.Count;
  public float MinimumDepth {
   get {float minimum=float.PositiveInfinity;foreach(var house in houses) {
    if(!house.root.gameObject.activeInHierarchy)return 0;
    Bounds bounds=house.renderers[0].bounds;foreach(var r in house.renderers)bounds.Encapsulate(r.bounds);
    minimum=Mathf.Min(minimum,bounds.size.z);
   }return houses.Count==0?0:minimum;}
  }
  public float PaperMaximumOpacity {
   get {float maximum=0;foreach(var r in paper){r.GetPropertyBlock(properties);maximum=Mathf.Max(maximum,properties.GetFloat("_RecallOpacity"));}return maximum;}
  }
  public ReturnRecallArchitecture(ReturnVolumeArt art,Transform parent,ReturnWorld rupture,ReturnWorld fragments) {
   this.art=art;
   plaster=QuietSurface("Chalk walls",new Color(.87f,.86f,.79f));
   roof=QuietSurface("Terracotta roofs",new Color(.70f,.29f,.22f),2);
   wood=QuietSurface("Warm timber",new Color(.65f,.59f,.48f));
   glass=QuietSurface("Deep teal windows",new Color(.19f,.32f,.34f),3);
   clearGlass=art.Material(new Color(.93f,.98f,1,.04f),.96f);clearGlass.name="Clear city window glass";
   clearGlass.SetFloat("_Glazing",1);clearGlass.SetFloat("_SurfaceDetail",0);clearGlass.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.One);clearGlass.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);clearGlass.SetFloat("_ZWrite",0);clearGlass.SetOverrideTag("RenderType","Transparent");clearGlass.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
   roomWall=QuietSurface("Quiet room walls",new Color(.61f,.59f,.53f));
   roomFloor=QuietSurface("Quiet room floors",new Color(.55f,.45f,.34f));
   curtain=QuietSurface("Linen curtains",new Color(.83f,.81f,.72f));curtain.SetFloat("_ThinSurface",.22f);
   metal=QuietSurface("Slate fittings",new Color(.27f,.32f,.34f),3);
   door=QuietSurface("Remembered red doors",new Color(.62f,.27f,.21f),2);
   stone=QuietSurface("Carved kerbs",new Color(.58f,.62f,.64f),3);
   shade=QuietSurface("Teal shutters",new Color(.27f,.45f,.47f),3);
   craft=new ReturnStreetCraft(art,plaster,roof,wood,stone);
   RegisterPaper(fragments.beacon);
   var radio=new GameObject("The remembered radio regains its cabinet").transform;radio.SetParent(parent,false);
   radio.position=fragments.beacon.position;BuildRadio(radio);BatchConstruction(radio);
   var radioItem=new DepthObject{root=radio,paperY=radio.position.y,baseY=radio.position.y,restoredScale=.24f,floorInset=.06f,relocate=true,paperPosition=radio.position};objects.Add(radioItem);
   foreach(var world in new[]{rupture,fragments}) {
    foreach(var support in world.surfaces)RegisterPaper(support.visual);
    foreach(Transform source in world.root) {
     if(source.name.StartsWith("remembered presence "))RegisterPaper(source);
     // These are the original rain and radio signal strokes, belonging to the paper view.
     if(world==fragments&&source.name=="thread")RegisterPaper(source);
     if(source.name!="rooftop city")continue;
     var mesh=source.Find("roof").GetComponent<MeshFilter>().sharedMesh;
     Vector3[] points=mesh.vertices;
     if(points.Length!=5)throw new InvalidOperationException("Recalled roof contour changed");
     float width=points[4].x-points[0].x,height=points[1].y,ridgeX=points[2].x,rise=points[2].y-height;
     if(width<=0||height<=0||rise<=0||ridgeX<=0||ridgeX>=width)throw new InvalidOperationException("Invalid recalled roof contour");
     RegisterPaper(source);
     var root=new GameObject("Restored solid house "+houses.Count).transform;root.SetParent(parent,false);
     root.position=source.position+Vector3.forward; // Behind the original paper/radio plane.
     BuildHouse(root,width,height,ridgeX,rise,houses.Count);
     BatchConstruction(root);
     var visible=new List<Renderer>();foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(renderer.enabled)visible.Add(renderer);
     houses.Add(new House{root=root,paperRoot=source,paperY=source.position.y,width=width,renderers=visible.ToArray()});
    }
   }
   if(houses.Count!=8)throw new InvalidOperationException("The eight authored recalled houses were not restored");
   CityCentre=new Vector3(fragments.root.position.x+fragments.radioX,0,fragments.root.position.z);
   for(int i=0;i<houses.Count;i++) {
    var h=houses[i];h.paperPosition=h.root.position;int corner=i%4,row=i/4;
    bool east=corner%2==1,north=corner<2;float x=east?7+row*12:-7-h.width-row*12;
    if(!north)x+=h.width;
    h.cityPosition=CityCentre+new Vector3(x,0,north?7:-7);h.cityRotation=Quaternion.Euler(0,north?0:180,0);
   }
   BuildStreet(parent,fragments);
   var kitchen=street.Find("Kitchen window and two cups");radioItem.cityPosition=kitchen.TransformPoint(new Vector3(-.42f,.97f,.05f));radioItem.cityRotation=kitchen.rotation;radioItem.baseY=radioItem.cityPosition.y;
   SetVolume(1);
  }
  void RegisterPaper(Transform root){if(root!=null)paper.AddRange(root.GetComponentsInChildren<Renderer>(true));}
  public void SetVolume(float amount,float paperReveal=1) {
   amount=Mathf.Clamp01(amount);
   if(amount<.999f&&streetViewBuildings.Count>0){foreach(var building in streetViewBuildings)foreach(var renderer in building.renderers)renderer.forceRenderingOff=false;streetViewBuildings.Clear();}
   foreach(var house in houses) {
    house.root.gameObject.SetActive(amount>.001f);
    house.root.localScale=new Vector3(1,1,Mathf.Max(.001f,amount));
     house.root.position=Vector3.Lerp(house.paperPosition,house.cityPosition,Mathf.SmoothStep(0,1,amount));
     house.root.rotation=Quaternion.Slerp(Quaternion.identity,house.cityRotation,Mathf.SmoothStep(0,1,amount));
   }
   foreach(var block in cityBlocks){block.gameObject.SetActive(amount>.001f);block.localScale=new Vector3(1,Mathf.Max(.001f,amount),1);}
   foreach(var item in objects) {
    item.root.gameObject.SetActive(amount>.001f);float size=Mathf.Lerp(1,item.restoredScale,Mathf.SmoothStep(0,1,amount));
    item.root.localScale=new Vector3(size,size,size*Mathf.Max(.001f,amount));
    Vector3 at=item.relocate?Vector3.Lerp(item.paperPosition,item.cityPosition,Mathf.SmoothStep(0,1,amount)):item.root.position;at.y=Mathf.Lerp(item.paperY,item.baseY,amount)-item.floorInset*size*amount;item.root.position=at;
    if(item.relocate)item.root.rotation=Quaternion.Slerp(Quaternion.identity,item.cityRotation,Mathf.SmoothStep(0,1,amount));
   }
   foreach(var pier in piers) {
    float bottom=pier.platformY-.44f*amount,deck=street.position.y+PavementLevel,height=Mathf.Max(.001f,bottom-.08f-deck);
    Vector3 at=pier.shaft.localPosition;at.y=PavementLevel+height*.5f;at.z=pier.localZ/Mathf.Max(.001f,amount);pier.shaft.localPosition=at;
    pier.shaft.localScale=new Vector3(amount,height/pier.fullHeight,1);
    at=pier.head.localPosition;at.y=bottom-street.position.y-.04f;at.z=pier.localZ/Mathf.Max(.001f,amount);pier.head.localPosition=at;
    pier.head.localScale=new Vector3(amount,1,1);
   }
   foreach(var r in paper){r.GetPropertyBlock(properties);properties.SetFloat("_RecallOpacity",(1-amount)*Mathf.Clamp01(paperReveal));r.SetPropertyBlock(properties);}
  }
  public void FrameStreetView(Vector3 cameraPosition,Vector3 travellerPosition) {
   if(streetViewBuildings.Count==0) {
    foreach(var house in houses)RegisterStreetViewBuilding(house.renderers);
    foreach(var block in cityBlocks)RegisterStreetViewBuilding(block.GetComponentsInChildren<Renderer>());
   }
   foreach(var building in streetViewBuildings) {
    // Keep a stable cutaway along the whole walk, rather than popping individual
    // houses on and off as the traveller passes their windows.
    bool hidden=building.bounds.max.z<travellerPosition.z-1&&building.bounds.min.z>cameraPosition.z+.5f;
    if(hidden==building.hidden)continue;building.hidden=hidden;
    foreach(var renderer in building.renderers)renderer.forceRenderingOff=hidden;
   }
  }
  void RegisterStreetViewBuilding(Renderer[] sources) {
   var renderers=new List<Renderer>();foreach(var renderer in sources)if(renderer.enabled&&renderer.gameObject.activeInHierarchy)renderers.Add(renderer);
   if(renderers.Count==0)return;Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
   streetViewBuildings.Add(new StreetViewBuilding{renderers=renderers.ToArray(),bounds=bounds});
  }
  Material QuietSurface(string name,Color colour,int recovery=1) {
   var material=art.Material(colour,.08f);material.name="Carved city / "+name;
   material.SetFloat("_SurfaceDetail",0);material.SetFloat("_RecoveryChannel",recovery);return material;
  }
  Transform Frame(string name,Transform parent,Vector3 at,Vector3 size,Material material,float bevel=.015f) {
   var frame=ReturnDoorReunion.Bevel(art,name,parent,at,size,bevel,material);
   if(material==wood||material==door||material==shade)art.MetricUV(frame,.6f,true);
   else if(material==plaster||material==stone)art.MetricUV(frame,2.2f);
   return frame;
  }
  void BuildStreet(Transform parent,ReturnWorld fragments) {
   street=new GameObject("Four remembered routes become a city crossroads").transform;street.SetParent(parent,false);street.position=CityCentre;
   objects.Add(new DepthObject{root=street,paperY=0,baseY=0});
   var asphalt=QuietSurface("Blue grey road",new Color(.31f,.35f,.37f),3);
   var marking=QuietSurface("Ivory road markings",new Color(.89f,.86f,.72f));
   Frame("City foundation",street,new Vector3(0,-.37f,0),new Vector3(CityRadius*2,.7f,CityRadius*2),stone,.04f);
   for(int axis=0;axis<2;axis++) {
    var road=new GameObject(axis==0?"Kitchen to station street":"Stairwell to rooftop street").transform;road.SetParent(street,false);road.localRotation=Quaternion.Euler(0,axis*90,0);
    var roadMarking=art.Copy(marking);roadMarking.SetFloat("_StreetPaintAxis",axis);
    Frame("Continuous road surface",road,new Vector3(0,-.021f,0),new Vector3(6.6f,.025f,290.4f),asphalt,.005f);
    for(int side=-1;side<=1;side+=2)for(int bay=-64;bay<=64;bay++) {
     float z=bay*2.2f;if(Mathf.Abs(z)<6)continue;
     Frame("Jointed stone sidewalk",road,new Vector3(side*4.6f,-.022f,z),new Vector3(2.55f,.035f,2.18f),plaster,.008f);
     Frame("Kerb with drainage edge",road,new Vector3(side*3.37f,.005f,z),new Vector3(.14f,.08f,2.18f),stone,.012f);
    }
    for(int i=-34;i<=34;i++)if(Mathf.Abs(i)>1)Frame("Painted road centre dash",road,new Vector3(0,-.0075f,i*4.1f),new Vector3(.10f,.001f,1.4f),roadMarking,.0002f);
    foreach(float z in new[]{-5f,5f})for(int stripe=-3;stripe<=3;stripe++)Frame("Pedestrian crossing",road,new Vector3(stripe*.85f,-.0075f,z),new Vector3(.48f,.001f,1.7f),roadMarking,.0002f);
   }
   var soil=QuietSurface("Planter earth",new Color(.43f,.45f,.37f));
   foreach(float x in new[]{-5.1f,5.1f})foreach(float z in new[]{-5.1f,5.1f}) {
    Frame("Recovered red clay corner planter",street,new Vector3(x,.26f,z),new Vector3(.95f,.52f,.95f),roof,.035f);
    Frame("Planter soil",street,new Vector3(x,.53f,z),new Vector3(.84f,.025f,.84f),soil,.005f);
    art.Tube("Street lamp post",street,new Vector3(x,.5f,z),new Vector3(x,3.9f,z),.045f,metal);
    Frame("Street lamp housing",street,new Vector3(x,3.95f,z),new Vector3(.42f,.12f,.42f),metal,.015f);
    Frame("Warm lamp diffuser",street,new Vector3(x,3.88f,z),new Vector3(.33f,.025f,.33f),glass,.005f);
   }
   // The four memories become inhabited thresholds beside the recovered roads.
   for(int i=0;i<4;i++) {
    var place=new GameObject(new[]{"Kitchen window and two cups","Stairwell entrance","Station waiting bench","Rooftop stair entrance"}[i]).transform;
    place.SetParent(street,false);place.localPosition=CityEchoOffset(i);place.localRotation=Quaternion.Euler(0,i==0?90:i==1?180:i==2?0:-90,0);
    if(i==1){var house=houses[2];place.position=house.cityPosition+house.cityRotation*new Vector3(RaisedEntranceX(house.width),0,-2.35f);}
    else if(i==3){var house=houses[0];Quaternion face=house.cityRotation*Quaternion.Euler(0,-90,0);place.position=house.cityPosition+house.cityRotation*new Vector3(house.width,0,RaisedEntranceX(Depth))-face*Vector3.forward*2.35f;}
    if(i==0) {
     Frame("Timber counter for the two cups",place,new Vector3(0,.93f,.15f),new Vector3(1.55f,.08f,.55f),wood);
     foreach(float x in new[]{-.60f,.60f})Frame("Counter support",place,new Vector3(x,.445f,.15f),new Vector3(.075f,.89f,.38f),metal);
     Frame("Counter lower brace",place,new Vector3(0,.17f,.15f),new Vector3(1.2f,.06f,.05f),metal);
     for(int cup=0;cup<2;cup++) {
      Vector3 centre=new Vector3(cup==0?.16f:.52f,1.03f,.15f);
      art.Tube("Two remembered cups",place,centre-Vector3.up*.055f,centre+Vector3.up*.055f,.07f,plaster);
      art.Tube("Recessed cup opening",place,centre+Vector3.up*.0555f,centre+Vector3.up*.057f,.055f,shade);
      for(int part=0;part<5;part++) {
       float a=-Mathf.PI*.5f+part*Mathf.PI/5,b=-Mathf.PI*.5f+(part+1)*Mathf.PI/5;
       art.Tube("Curved cup handle",place,centre+new Vector3(.035f+Mathf.Cos(a)*.03f,Mathf.Sin(a)*.034f,0),centre+new Vector3(.035f+Mathf.Cos(b)*.03f,Mathf.Sin(b)*.034f,0),.009f,plaster);
      }
     }
    } else if(i==1||i==3) {
     for(int step=0;step<4;step++){float height=.14f*(step+1);Frame("Grounded entrance tread",place,new Vector3(0,height*.5f,.55f+step*.32f),new Vector3(1.45f,height,.34f),stone);}
     Frame("Entrance landing",place,new Vector3(0,.49f,1.96f),new Vector3(1.60f,.14f,.60f),stone);
    } else {
     for(int slat=0;slat<4;slat++)Frame("Station bench seat slat",place,new Vector3(-.35f,.43f,-.18f+slat*.11f),new Vector3(1.9f,.05f,.08f),wood,.009f);
     foreach(float x in new[]{-1.07f,.37f}) {
      Frame("Station bench feet",place,new Vector3(x,.195f,.02f),new Vector3(.09f,.39f,.38f),metal);
      Frame("Station bench seat bracket",place,new Vector3(x,.38f,.02f),new Vector3(.10f,.06f,.43f),metal);
      Frame("Station bench back support",place,new Vector3(x,.68f,.21f),new Vector3(.07f,.60f,.065f),metal);
     }
     for(int slat=0;slat<3;slat++)Frame("Station bench back slat",place,new Vector3(-.35f,.60f+slat*.13f,.23f),new Vector3(1.9f,.085f,.055f),wood,.009f);
     foreach(float x in new[]{-1.29f,.59f}) {
      Frame("Station bench armrest",place,new Vector3(x,.65f,.02f),new Vector3(.05f,.045f,.43f),metal);
      Frame("Station bench arm support",place,new Vector3(x,.55f,-.14f),new Vector3(.045f,.20f,.045f),metal);
     }
     foreach(float x in new[]{1.17f,1.73f})Frame("Timetable ground support",place,new Vector3(x,.40f,.34f),new Vector3(.075f,.8f,.08f),metal);
     Frame("Station timetable cabinet",place,new Vector3(1.45f,1.30f,.34f),new Vector3(.88f,1.14f,.12f),metal);
     Frame("Timetable glass",place,new Vector3(1.45f,1.30f,.27f),new Vector3(.77f,1.03f,.02f),glass);
     Frame("Timetable weather cap",place,new Vector3(1.45f,1.91f,.34f),new Vector3(.98f,.055f,.25f),metal);
     for(int row=0;row<6;row++)Frame("Timetable information line",place,new Vector3(1.45f,1.62f-row*.12f,.255f),new Vector3(row==0?.57f:.46f,.023f,.012f),plaster,.003f);
    }
    Frame("Recovered yellow way marker",place,new Vector3(0,.015f,-.58f),new Vector3(.35f,.018f,.35f),marking,.004f);
   }
   craft.Courtyards(street);BatchConstruction(street);
   // The recovered street grows the same carved trees as the island.
   var canopy=QuietSurface("Sculpted tree crowns",new Color(.47f,.65f,.56f),3);
   foreach(float x in new[]{-5.1f,5.1f})foreach(float z in new[]{-5.1f,5.1f}) {
    var plant=ReturnQuietLandscape.CreateTree(art,street,new Vector3(x,.543f,z),wood,canopy,.78f);plant.name="A carved tree in the recovered corner planter";plant.localRotation=Quaternion.Euler(0,x*z>0?37:119,0);
   }
   for(int i=0;i<8;i++) {
    bool east=i%2==1,north=i%4<2;float width=7.2f,height=6.8f+(i%3)*1.2f;
    var block=new GameObject("City block beyond the remembered route "+i).transform;block.SetParent(parent,false);
    float x=east?7+(i/4)*12:-7-width-(i/4)*12;if(!north)x+=width;
    block.position=CityCentre+new Vector3(x,0,north?20:-20);block.rotation=Quaternion.Euler(0,north?0:180,0);
    BuildHouse(block,width,height,width*.5f,2.1f,i+8);BatchConstruction(block);cityBlocks.Add(block);
   }
   // Keep the distant neighbourhood on the unfolding street's supported ground.
   for(int arm=0;arm<4;arm++) {
    var district=new GameObject("Continuing city street "+arm).transform;district.SetParent(street,false);district.localRotation=Quaternion.Euler(0,arm*90,0);
    Frame("Connected distant junction",district,new Vector3(0,-.021f,145.2f),new Vector3(36,.025f,6.6f),asphalt,.005f);
    Frame("Distant frontage pavement",district,new Vector3(0,-.022f,149.775f),new Vector3(36,.035f,2.55f),plaster,.008f);
    for(int side=-1;side<=1;side+=2)for(int row=0;row<5;row++) {
     float width=8.8f+(row%3)*.6f,distance=40+row*24;
     var block=new GameObject("Distant street frontage "+arm+" "+side+" "+row).transform;block.SetParent(district,false);
     block.localPosition=new Vector3(side*7,0,distance+side*width*.5f);block.localRotation=Quaternion.Euler(0,side*90,0);
     BuildHouse(block,width,7.2f+((row+arm)%3)*1.1f,width*.5f,2.1f,16+arm*10+(side+1)*5+row,true);BatchConstruction(block);cityBlocks.Add(block);
    }
    for(int slot=0;slot<3;slot++) {
     var block=new GameObject("City block beyond the distant junction "+arm+" "+slot).transform;block.SetParent(district,false);
     block.localPosition=new Vector3(-13.5f+slot*9,0,151.1f);
     BuildHouse(block,9,9.4f+((arm+slot)%3)*1.2f,4.5f,2.1f,56+arm*3+slot,true);BatchConstruction(block);cityBlocks.Add(block);
    }
   }
  }
  public static Vector3 CityEchoOffset(int i)=>i==0?new Vector3(4.65f,0,-10):i==1?new Vector3(-10,0,-4.65f):i==2?new Vector3(10,0,4.65f):new Vector3(-4.65f,0,10);

  static float RaisedEntranceX(float width)=>Mathf.Clamp(3,.8f,width-.8f);
  void BuildHouse(Transform root,float width,float h,float ridge,float rise,int identity,bool distant=false) {
   Color[] tones={new Color(.88f,.82f,.63f),new Color(.61f,.78f,.79f),new Color(.83f,.60f,.53f),new Color(.54f,.75f,.63f)};
   var facade=QuietSurface("Pastel house "+identity,tones[identity%tones.Length],identity%4==2?2:identity%4==0?1:3);
   BuildFace("Front facade",root,width,h,Vector3.zero,Quaternion.identity,facade,identity,true,false,identity==2,!distant);
   BuildFace("Rear facade",root,width,h,new Vector3(width,0,Depth),Quaternion.Euler(0,180,0),facade,identity+1,false,false,false,false);
   BuildFace("Left facade",root,Depth,h,new Vector3(0,0,Depth),Quaternion.Euler(0,90,0),facade,identity+2,false,true,false,!distant);
   BuildFace("Right facade",root,Depth,h,new Vector3(width,0,0),Quaternion.Euler(0,-90,0),facade,identity+3,false,true,identity==0,!distant);
   bool pitched=identity%3==0;
   if(pitched){Gable(root,width,h,ridge,rise,0,facade);Gable(root,width,h,ridge,rise,Depth-.22f,facade);}
   Frame("Solid stone foundation",root,new Vector3(width*.5f,-.13f,Depth*.5f),new Vector3(width+.15f,.26f,Depth+.15f),stone,.025f);
   Frame("Left carved eave",root,new Vector3(-.10f,h-.025f,Depth*.5f),new Vector3(.16f,.14f,Depth+.6f),plaster,.035f);
   Frame("Right carved eave",root,new Vector3(width+.10f,h-.025f,Depth*.5f),new Vector3(.16f,.14f,Depth+.6f),plaster,.035f);
   if(pitched)DistantRoof(root,width,h,ridge,rise);else craft.Crown(root,width,h,Depth,rise,identity,facade,!distant);
   craft.HouseDetails(root,width,h,Depth,identity,!distant);
   if(pitched)foreach(float z in new[]{-.21f,Depth+.21f})foreach(float x in new[]{0f,width}) {
    Vector3 a=new Vector3(x,h+.035f,z),b=new Vector3(ridge,h+rise+.035f,z);
    Vector3 delta=b-a;var bar=Frame("Continuous roof edge",root,(a+b)*.5f,new Vector3(delta.magnitude+.15f,.12f,.12f),roof,.025f);
    bar.localRotation=Quaternion.FromToRotation(Vector3.right,delta.normalized);
   }
  }
  void DistantRoof(Transform root,float width,float h,float ridge,float rise) {
   Vector3 peak=new Vector3(ridge,h+rise,-.3f);
   foreach(float eave in new[]{-.25f,width+.25f}) {
    Vector3 edge=new Vector3(eave,h,-.3f);float length=Vector3.Distance(edge,peak);
    Vector3 a=eave<ridge?edge:peak,b=eave<ridge?peak:edge;
    art.Mesh("Distant pitched clay roof",root,new[]{a,b,b+Vector3.forward*(Depth+.6f),a+Vector3.forward*(Depth+.6f)},new[]{0,2,1,0,3,2},roof,new[]{Vector2.zero,new Vector2(length,0),new Vector2(length,Depth+.6f),new Vector2(0,Depth+.6f)});
   }
   art.Tube("Distant roof ridge",root,new Vector3(ridge,h+rise+.03f,-.3f),new Vector3(ridge,h+rise+.03f,Depth+.3f),.075f,roof);
  }
  void BuildFace(string name,Transform root,float width,float h,Vector3 at,Quaternion rotation,Material facade,int identity,bool front,bool side,bool raisedEntry=false,bool interiors=false) {
   var face=new GameObject(name).transform;face.SetParent(root,false);face.localPosition=at;face.localRotation=rotation;
   var apertures=new List<Rect>();Rect entrance=raisedEntry?new Rect(RaisedEntranceX(width)-.6f,.56f,1.2f,2.2f):new Rect(width-1.9f,.10f,1.15f,2.15f);
   for(int row=0;2+row*3+1.6f<h-.25f;row++)
    for(int col=0;col<(side?1:2);col++){var window=new Rect(side?(width-1.25f)*.5f:1.5f+col*3.2f,2+row*3,side?1.25f:1.4f,1.6f);if(!raisedEntry||!window.Overlaps(entrance))apertures.Add(window);}
   if(front||raisedEntry)apertures.Add(entrance);
   Wall("Plaster with true openings",face,width,h,apertures,facade);
   foreach(var aperture in apertures) {
    bool entry=(front||raisedEntry)&&aperture==entrance;float x=aperture.center.x,y=aperture.center.y,w=aperture.width,v=aperture.height;
    ArchedOpening(face,aperture,entry?door:craft.Window(identity,aperture),facade);
    craft.Opening(face,aperture,entry,identity,interiors);
    if(entry) {
     art.Tube("Quiet door pull",face,new Vector3(x+.29f,y-.10f,.095f),new Vector3(x+.29f,y+.10f,.095f),.022f,metal);
     float lift=entrance.yMin-.10f;
     Frame("Carved doorstep",face,new Vector3(x,.04f+lift,-.22f),new Vector3(w+.24f,.09f,.5f),stone,.025f);
    }
   }
   Frame("Quiet wall footing",face,new Vector3(width*.5f,.11f,-.025f),new Vector3(width,.22f,.09f),stone,.025f);
   Frame("Single carved cornice",face,new Vector3(width*.5f,h-.13f,-.035f),new Vector3(width+.12f,.16f,.20f),plaster,.035f);
  }
  void ArchedOpening(Transform face,Rect opening,Material inset,Material facade) {
   float radius=opening.width*.5f,centreY=opening.yMax-radius;Vector3 centre=new Vector3(opening.center.x,opening.center.y,.17f);
   Frame("Quiet recessed opening",face,centre,new Vector3(opening.width,opening.height,.035f),inset,.005f);
   var caps=new List<Vector3>();var capUV=new List<Vector2>();var capTriangles=new List<int>();
   var rim=new List<Vector3>();var rimUV=new List<Vector2>();var rimTriangles=new List<int>();
   for(int i=0;i<16;i++) {
    float u=Mathf.PI*(1-i/16f),v=Mathf.PI*(1-(i+1)/16f);
    Vector3 a=new Vector3(opening.center.x+Mathf.Cos(u)*radius,centreY+Mathf.Sin(u)*radius,-.003f),b=new Vector3(opening.center.x+Mathf.Cos(v)*radius,centreY+Mathf.Sin(v)*radius,-.003f);
    Quad(caps,capUV,capTriangles,a,new Vector3(a.x,opening.yMax,a.z),new Vector3(b.x,opening.yMax,b.z),b);
    Vector3 innerA=new Vector3(a.x,a.y,-.045f),innerB=new Vector3(b.x,b.y,-.045f);
    Vector3 outerA=new Vector3(opening.center.x+Mathf.Cos(u)*(radius+.09f),centreY+Mathf.Sin(u)*(radius+.09f),-.045f),outerB=new Vector3(opening.center.x+Mathf.Cos(v)*(radius+.09f),centreY+Mathf.Sin(v)*(radius+.09f),-.045f);
    Quad(rim,rimUV,rimTriangles,innerA,outerA,outerB,innerB);
    Quad(rim,rimUV,rimTriangles,innerA,innerB,innerB+Vector3.forward*.20f,innerA+Vector3.forward*.20f);
   }
   art.Mesh("Carved arch shoulders",face,caps.ToArray(),capTriangles.ToArray(),facade,capUV.ToArray());
   art.Mesh("Continuous ivory arch",face,rim.ToArray(),rimTriangles.ToArray(),plaster,rimUV.ToArray());
   float jambHeight=opening.height-radius;
   foreach(float x in new[]{opening.xMin-.045f,opening.xMax+.045f})Frame("Ivory arch jamb",face,new Vector3(x,opening.yMin+jambHeight*.5f,.035f),new Vector3(.09f,jambHeight,.20f),plaster,.015f);
   Frame("Quiet opening sill",face,new Vector3(opening.center.x,opening.yMin-.04f,-.025f),new Vector3(opening.width+.18f,.08f,.30f),plaster,.02f);
  }
  void WindowRoom(Transform face,Rect opening,float width,float height,int identity) {
   float left=Mathf.Max(.24f,opening.xMin-.4f),right=Mathf.Min(width-.24f,opening.xMax+.4f),bottom=Mathf.Max(.12f,opening.yMin-.9f),top=Mathf.Min(height-.24f,opening.yMax+.4f),x=(left+right)*.5f,y=(bottom+top)*.5f;
   Frame("Interior back wall",face,new Vector3(x,y,1.4f),new Vector3(right-left,top-bottom,.06f),roomWall);
   Frame("Interior left wall",face,new Vector3(left,y,.8f),new Vector3(.06f,top-bottom,1.2f),roomWall);
   Frame("Interior right wall",face,new Vector3(right,y,.8f),new Vector3(.06f,top-bottom,1.2f),roomWall);
   Frame("Interior ceiling",face,new Vector3(x,top,.8f),new Vector3(right-left,.06f,1.2f),roomWall);
   Frame("Interior timber floor",face,new Vector3(x,bottom,.8f),new Vector3(right-left,.06f,1.2f),roomFloor);
   Frame("Small table beyond the window",face,new Vector3(x,bottom+.64f,1.03f),new Vector3(opening.width*.65f,.06f,.48f),wood,.012f);
   foreach(float dx in new[]{-.24f,.24f})foreach(float z in new[]{.86f,1.2f})Frame("Table leg",face,new Vector3(x+dx,bottom+.31f,z),new Vector3(.055f,.62f,.055f),wood,.005f);
   art.Tube("Curtain rail",face,new Vector3(opening.xMin-.08f,opening.yMax+.08f,.29f),new Vector3(opening.xMax+.08f,opening.yMax+.08f,.29f),.014f,metal);
   for(int side=0;side<2;side++) {
    const int columns=16,rows=12;var vertices=new Vector3[(columns+1)*(rows+1)];var uv=new Vector2[vertices.Length];var indices=new int[columns*rows*6];float panel=opening.width*.28f;
    for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++) {
     float u=col/(float)columns,v=row/(float)rows,sway=.015f*Mathf.Sin(v*Mathf.PI)*Mathf.Sin(identity*1.37f+side),px=(side==0?opening.xMin+.015f:opening.xMax-panel-.015f)+u*panel+sway;
     int i=row*(columns+1)+col;vertices[i]=new Vector3(px,Mathf.Lerp(opening.yMin+.04f,opening.yMax+.04f,v),.29f+.035f*Mathf.Cos(u*Mathf.PI*8)+.018f*Mathf.Sin(v*Mathf.PI));uv[i]=new Vector2(u*panel,v*opening.height);
     if(row<rows&&col<columns){int a=(row*columns+col)*6;indices[a]=i;indices[a+1]=i+columns+1;indices[a+2]=i+1;indices[a+3]=i+1;indices[a+4]=i+columns+1;indices[a+5]=i+columns+2;}
    }
    art.Mesh("Folded linen curtain",face,vertices,indices,curtain,uv);
   }
  }
  void Balcony(Transform face,float x,float floor,float width) {
   float left=x-width*.5f-.20f,right=x+width*.5f+.20f;
   Frame("Stone balcony slab",face,new Vector3(x,floor-.10f,-.40f),new Vector3(width+.45f,.16f,.88f),stone);
   art.Tube("Balcony handrail",face,new Vector3(left,floor+.79f,-.78f),new Vector3(right,floor+.79f,-.78f),.026f,metal);
   for(float px=left;px<=right+.01f;px+=.18f)art.Tube("Balcony baluster",face,new Vector3(px,floor,-.78f),new Vector3(px,floor+.79f,-.78f),.012f,metal);
   foreach(float px in new[]{left,right}) {
    art.Tube("Balcony return rail",face,new Vector3(px,floor+.79f,-.78f),new Vector3(px,floor+.79f,0),.026f,metal);
    art.Tube("Balcony bracket",face,new Vector3(px,floor-.62f,0),new Vector3(px,floor-.19f,-.73f),.027f,metal);
   }
  }
  void BuildRadio(Transform radio) {
   Material cabinet=art.Material(new Color(.17f,.19f,.18f),.38f),grille=art.Material(new Color(.08f,.09f,.08f),.23f);
   Frame("Rounded radio cabinet",radio,new Vector3(0,.91f,.32f),new Vector3(2.60f,1.55f,.64f),cabinet,.075f);
   Frame("Speaker recess",radio,new Vector3(-.61f,.95f,-.012f),new Vector3(1.08f,1.13f,.035f),grille,.035f);
   for(int i=0;i<17;i++)Frame("Speaker grille rib",radio,new Vector3(-.61f,.43f+i*.063f,-.041f),new Vector3(1.02f,.016f,.019f),metal,.004f);
   for(int i=0;i<21;i++)Frame("Speaker grille wire",radio,new Vector3(-1.10f+i*.049f,.94f,-.037f),new Vector3(.008f,1.01f,.012f),metal,.003f);
   Frame("Recessed tuning glass",radio,new Vector3(.65f,1.2f,-.018f),new Vector3(.88f,.48f,.035f),glass,.015f);
   for(int i=0;i<13;i++)Frame("Frequency graduation",radio,new Vector3(.29f+i*.058f,1.19f,-.044f),new Vector3(.009f,i%3==0?.18f:.09f,.012f),plaster,.002f);
   Frame("Tuning cursor",radio,new Vector3(.61f,1.18f,-.050f),new Vector3(.014f,.27f,.014f),wood,.003f);
   art.Tube("Tuning knob",radio,new Vector3(.65f,.56f,-.01f),new Vector3(.65f,.56f,-.13f),.135f,metal);
   art.Tube("Knob inset",radio,new Vector3(.65f,.56f,-.13f),new Vector3(.65f,.56f,-.14f),.098f,cabinet);
   art.Tube("Telescopic aerial",radio,new Vector3(-.70f,1.71f,.19f),new Vector3(-.30f,3.65f,.19f),.019f,metal);
   art.Tube("Aerial tip",radio,new Vector3(-.30f,3.60f,.19f),new Vector3(-.30f,3.67f,.19f),.028f,metal);
   Frame("Folded carrying handle",radio,new Vector3(.34f,1.80f,.36f),new Vector3(1.11f,.10f,.11f),cabinet,.04f);
   foreach(float x in new[]{-.17f,.85f})Frame("Handle mounting",radio,new Vector3(x,1.72f,.36f),new Vector3(.10f,.18f,.11f),metal,.025f);
   foreach(float x in new[]{-.94f,.94f})Frame("Radio rubber foot",radio,new Vector3(x,.105f,.34f),new Vector3(.19f,.09f,.40f),grille,.018f);
  }
  // Keep individual authored parts and seams, but render each material as one owned mesh per object.
  void BatchConstruction(Transform root) {
   var batches=new Dictionary<Material,List<CombineInstance>>();var originals=root.GetComponentsInChildren<MeshRenderer>();
   foreach(var renderer in originals) {
    var filter=renderer.GetComponent<MeshFilter>();Material material=renderer.sharedMaterial;
    if(!batches.TryGetValue(material,out var batch)){batch=new List<CombineInstance>();batches.Add(material,batch);}
    batch.Add(new CombineInstance{mesh=filter.sharedMesh,transform=root.worldToLocalMatrix*filter.transform.localToWorldMatrix});
   }
   foreach(var batch in batches) {
    var combined=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};combined.CombineMeshes(batch.Value.ToArray(),true,true);
    var visual=art.Mesh("Joined construction "+batch.Key.name,root,combined.vertices,combined.triangles,batch.Key,combined.uv);
    Mesh owned=visual.GetComponent<MeshFilter>().sharedMesh;owned.normals=combined.normals;owned.RecalculateTangents();
    if(batch.Key==clearGlass)visual.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    UnityEngine.Object.DestroyImmediate(combined);
   }
   foreach(var renderer in originals)renderer.enabled=false;
  }
  // Apertures remove wall cells, leaving real reveal depth instead of painting windows on a box.
  void Wall(string name,Transform parent,float width,float h,List<Rect> apertures,Material facade) {
   var xs=new SortedSet<float>{0,width};var ys=new SortedSet<float>{0,h};
   foreach(var r in apertures){xs.Add(r.xMin);xs.Add(r.xMax);ys.Add(r.yMin);ys.Add(r.yMax);}
   var xx=new List<float>(xs);var yy=new List<float>(ys);var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
   for(int x=0;x<xx.Count-1;x++)for(int y=0;y<yy.Count-1;y++) {
    var centre=new Vector2((xx[x]+xx[x+1])*.5f,(yy[y]+yy[y+1])*.5f);bool hole=false;
    foreach(var r in apertures)if(r.Contains(centre)){hole=true;break;}if(hole)continue;
    Box(v,uv,t,new Vector3(xx[x],yy[y],0),new Vector3(xx[x+1],yy[y+1],.22f));
   }
   var wall=art.Mesh(name,parent,v.ToArray(),t.ToArray(),facade,uv.ToArray());art.MetricUV(wall,2.2f);
  }
  static void Quad(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d) {
   int k=v.Count;v.AddRange(new[]{a,b,c,d});
   bool vertical=Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<.5f;
   foreach(var point in new[]{a,b,c,d})uv.Add(vertical?new Vector2(point.x+point.z,point.y)*.45f:new Vector2(point.x,point.z)*.45f);
   t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
  }
  static void Box(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 a,Vector3 b) {
   Quad(v,uv,t,a,new Vector3(a.x,b.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(b.x,a.y,a.z));
   Quad(v,uv,t,new Vector3(b.x,a.y,b.z),b,new Vector3(a.x,b.y,b.z),new Vector3(a.x,a.y,b.z));
   Quad(v,uv,t,new Vector3(a.x,a.y,b.z),new Vector3(a.x,b.y,b.z),new Vector3(a.x,b.y,a.z),a);
   Quad(v,uv,t,new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),b,new Vector3(b.x,a.y,b.z));
   Quad(v,uv,t,new Vector3(a.x,b.y,a.z),new Vector3(a.x,b.y,b.z),b,new Vector3(b.x,b.y,a.z));
   Quad(v,uv,t,new Vector3(a.x,a.y,b.z),a,new Vector3(b.x,a.y,a.z),new Vector3(b.x,a.y,b.z));
  }
  void Gable(Transform parent,float width,float h,float ridge,float rise,float z,Material facade) {
   var v=new[]{new Vector3(0,h,z),new Vector3(ridge,h+rise,z),new Vector3(width,h,z),new Vector3(0,h,z+.22f),new Vector3(ridge,h+rise,z+.22f),new Vector3(width,h,z+.22f)};
   var gable=art.Mesh("Solid gable",parent,v,new[]{0,1,2,3,5,4,0,3,4,0,4,1,1,4,5,1,5,2,2,5,3,2,3,0},facade);art.MetricUV(gable,2.2f);
  }
  void TileRoof(Transform parent,float width,float h,float ridge,float rise) {
   var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
   for(int side=0;side<2;side++) {
    Vector3 start=new Vector3(side==0?0:width,h,0),slope=new Vector3(ridge-start.x,rise,0);
    float length=slope.magnitude;Vector3 along=slope.normalized,normal=new Vector3(-along.y,along.x,0);if(normal.y<0)normal=-normal;
    int rows=Mathf.CeilToInt((length+.28f)/.34f),columns=Mathf.CeilToInt((Depth+.56f)/.34f);
    for(int row=0;row<rows;row++)for(int col=0;col<columns;col++) {
     float lower=-.22f+row*(length+.25f)/rows,upper=Mathf.Min(length+.045f,lower+.42f);
     float z0=-.28f+col*(Depth+.56f)/columns,z1=z0+(Depth+.56f)/columns;
     for(int arc=0;arc<6;arc++) {
      float a=arc/6f,b=(arc+1)/6f;
      Vector3 za=Vector3.forward*Mathf.Lerp(z0,z1,a)+normal*(.031f+Mathf.Sin(a*Mathf.PI)*.044f+row*.0007f);
      Vector3 zb=Vector3.forward*Mathf.Lerp(z0,z1,b)+normal*(.031f+Mathf.Sin(b*Mathf.PI)*.044f+row*.0007f);
      Vector3 p=start+along*lower+za,q=start+along*upper+za,r=start+along*upper+zb,s=start+along*lower+zb;
      if(Vector3.Dot(Vector3.Cross(q-p,r-p),normal)>0)Quad(v,uv,t,p,q,r,s);else Quad(v,uv,t,s,r,q,p);
     }
    }
   }
   var tiles=art.Mesh("Overlapping curved clay roof tiles",parent,v.ToArray(),t.ToArray(),roof,uv.ToArray());art.MetricUV(tiles,2);
   art.Tube("Rounded ridge tiles",parent,new Vector3(ridge,h+rise+.06f,-.3f),new Vector3(ridge,h+rise+.06f,Depth+.3f),.095f,roof);
  }
 }
}
