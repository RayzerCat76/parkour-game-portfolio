using System.Collections.Generic;
using UnityEngine;
namespace Return.Redesign {
 // Sheet 4: the four yellow blocks, red world and blue fruit unite into the way home.
 public sealed class ReturnDoorReunion {
  public Transform Root {get;}
  public Transform Hinge {get;}
  public bool Ready {get;private set;}
  readonly Transform tokens;
  readonly Transform[] yellow=new Transform[4],red=new Transform[3];
  readonly Transform blue,bluePanel;
  readonly Vector3[] yellowStart=new Vector3[4],yellowEnd=new Vector3[4];
  readonly Vector3[] redEnd=new Vector3[3];
  readonly Vector3 door,fruitOrigin;
 Vector3 radioOrigin,redOrigin,cityExit;bool seeded;
  public ReturnDoorReunion(ReturnVolumeArt art,Transform parent,Vector3 doorway,Vector3 fruit) {
   door=doorway;fruitOrigin=fruit;
   Root=new GameObject("A doorway assembled from the recovered colours").transform;Root.SetParent(parent,false);Root.localPosition=door;
   tokens=new GameObject("Yellow, red and blue return together").transform;tokens.SetParent(parent,false);tokens.gameObject.SetActive(false);
    Material wood=ReturnConstructionSurface.Material(art,"Timber",new Color(.93f,.85f,.69f));
   Material paint=art.Material(new Color(.88f,.90f,.82f),.34f),gold=art.Material(new Color(.96f,.72f,.15f),.38f,.04f);
    Material fired=ReturnConstructionSurface.Material(art,"Clay",new Color(.74f,.12f,.065f));
   Material bluePaint=art.Material(new Color(.095f,.29f,.65f),.56f,.015f);bluePaint.SetFloat("_RecoveryChannel",3);
   gold.SetFloat("_RecoveryChannel",1);fired.SetFloat("_RecoveryChannel",2);
   Bevel(art,"Rounded left casing",Root,new Vector3(-1.13f,1.62f,0),new Vector3(.25f,3.25f,.28f),.027f,wood);
   Bevel(art,"Rounded right casing",Root,new Vector3(1.13f,1.62f,0),new Vector3(.25f,3.25f,.28f),.027f,wood);
   Bevel(art,"Rounded lintel",Root,new Vector3(0,3.22f,0),new Vector3(2.51f,.25f,.28f),.027f,wood);
   Hinge=new GameObject("The recovered door opens").transform;Hinge.SetParent(Root,false);Hinge.localPosition=new Vector3(-1.01f,0,0);
   Bevel(art,"Painted door with softened edges",Hinge,new Vector3(1.01f,1.55f,0),new Vector3(2.02f,3.08f,.11f),.02f,paint);
   // A recessed panel and real handle leave readable light/shadow edges at eye level.
   bluePanel=Bevel(art,"The fruit's blue spreads through the doorway",Hinge,new Vector3(1.01f,1.68f,-.071f),new Vector3(1.71f,2.52f,.035f),.013f,bluePaint);
   bluePanel.gameObject.SetActive(false);
   art.Tube("Rounded brass lever",Hinge,new Vector3(1.57f,1.25f,-.145f),new Vector3(1.91f,1.25f,-.145f),.022f,gold);
   for(int i=0;i<4;i++) {
    yellowEnd[i]=door+new Vector3(i%2==0?-1.13f:1.13f,i<2?.15f:3.15f,-.18f);
    yellow[i]=Bevel(art,"Recovered yellow block "+i,tokens,Vector3.zero,new Vector3(.25f,.25f,.25f),.021f,gold);
   }
   for(int i=0;i<3;i++) {
    redEnd[i]=door+(i<2?new Vector3(i==0?-1.13f:1.13f,1.62f,-.18f):new Vector3(0,3.22f,-.18f));
    red[i]=Bevel(art,"The eruption's red becomes a sheltering surround "+i,tokens,Vector3.zero,i<2?new Vector3(.23f,3.08f,.16f):new Vector3(2.48f,.23f,.16f),.02f,fired);
   }
    blue=art.Model("BlueFruit",tokens,fruitOrigin);blue.localScale=Vector3.one*.28f;
    // The final casing and travelling red inserts use the same real material scale as the recalled city.
    foreach(var renderer in Root.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial==wood)art.MetricUV(renderer.transform,.6f,true);
    foreach(var insert in red)art.MetricUV(insert,2);
   Root.gameObject.SetActive(false);
  }
  public Vector3 BundleCentre(float t) {
   if(!seeded)return door+Vector3.up*1.6f;
   if(t<26)return Vector3.Lerp(radioOrigin,radioOrigin+Vector3.up*1.3f,Mathf.SmoothStep(0,1,(t-24)/2));
   if(t<29)return Vector3.Lerp(radioOrigin+Vector3.up*1.3f,redOrigin+Vector3.up*2.5f,Mathf.SmoothStep(0,1,(t-26)/3));
  if(t<ReturnReverseJourney.DoorAt)return Vector3.Lerp(redOrigin+Vector3.up*2.5f,cityExit+Vector3.up*1.6f,Mathf.SmoothStep(0,1,(t-29)/3));
  // Rise clear of the city, carry the recovered pieces home, then settle on the original island.
  if(t<34.9f)return Vector3.Lerp(cityExit+Vector3.up*1.6f,cityExit+Vector3.up*18,Mathf.SmoothStep(0,1,(t-ReturnReverseJourney.DoorAt)/.9f));
  if(t<37.1f)return Vector3.Lerp(cityExit+Vector3.up*18,door+Vector3.up*18,Mathf.SmoothStep(0,1,(t-34.9f)/2.2f));
  return Vector3.Lerp(door+Vector3.up*18,door+Vector3.up*1.6f,Mathf.SmoothStep(0,1,(t-37.1f)/.9f));
  }
  public void Step(float t,ReturnReverseJourney memory) {
   if(t<24)return;
  if(!seeded){for(int i=0;i<4;i++){yellowStart[i]=memory.BlockOrigin(i);radioOrigin+=yellowStart[i]/4;}redOrigin=memory.RedOrigin;cityExit=memory.CityExit;seeded=true;}
   if(Ready)return;tokens.gameObject.SetActive(true);
  Vector3 centre=BundleCentre(t);float release=Mathf.SmoothStep(0,1,(t-24)/1.3f),assemble=Mathf.SmoothStep(0,1,(t-ReturnReverseJourney.IslandReturnAt)/2.2f);
   for(int i=0;i<4;i++) {
    float angle=i*Mathf.PI*.5f+(t-24)*1.25f;Vector3 orbit=centre+new Vector3(Mathf.Cos(angle)*.42f,Mathf.Sin(angle)*.42f,0);
    yellow[i].position=Vector3.Lerp(Vector3.Lerp(yellowStart[i],orbit,release),yellowEnd[i],assemble);
    yellow[i].rotation=Quaternion.Slerp(Quaternion.Euler(22,(t-24)*60+i*45,12),Quaternion.identity,assemble);
   }
   for(int i=0;i<3;i++) {
    float join=Mathf.SmoothStep(0,1,(t-27)/2),angle=i*Mathf.PI*2/3+(t-27)*.8f;
    Vector3 orbit=centre+new Vector3(Mathf.Cos(angle)*.7f,Mathf.Sin(angle)*.5f,.12f);
    red[i].position=Vector3.Lerp(Vector3.Lerp(redOrigin+new Vector3(i*.3f,.1f,0),orbit,join),redEnd[i],assemble);
    red[i].localScale=Vector3.one*Mathf.Lerp(.22f,1,assemble);red[i].rotation=Quaternion.Slerp(Quaternion.Euler(0,0,Mathf.Sin(angle)*25),Quaternion.identity,assemble);
    red[i].gameObject.SetActive(t>=27);
   }
  float blueJoin=Mathf.SmoothStep(0,1,(t-37.1f)/.9f);
   Vector3 blueEnd=door+new Vector3(0,1.68f,-.15f);
   blue.position=Vector3.Lerp(Vector3.Lerp(fruitOrigin,centre+new Vector3(0,.15f,-.4f),blueJoin),blueEnd,assemble);blue.gameObject.SetActive(t>=29);
   float blueSpread=Mathf.SmoothStep(0,1,(assemble-.12f)/.70f);
   blue.localScale=Vector3.one*(.28f*(1-blueSpread));
   bluePanel.gameObject.SetActive(blueSpread>0);bluePanel.localScale=new Vector3(1,Mathf.Max(.001f,blueSpread),1);
  float body=assemble;Root.gameObject.SetActive(body>0);Root.localScale=new Vector3(1,Mathf.Max(.001f,body),1);
  if(t>=ReturnReverseJourney.IslandReturnAt+2.2f){Root.localScale=Vector3.one;for(int i=0;i<4;i++){yellow[i].position=yellowEnd[i];yellow[i].rotation=Quaternion.identity;}for(int i=0;i<3;i++){red[i].position=redEnd[i];red[i].rotation=Quaternion.identity;red[i].localScale=Vector3.one;}blue.gameObject.SetActive(false);bluePanel.localScale=Vector3.one;Ready=true;}
  }
   internal static Transform Bevel(ReturnVolumeArt art,string name,Transform parent,Vector3 position,Vector3 size,float bevel,Material material) {
   float hx=size.x*.5f,hy=size.y*.5f,hz=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(hx,Mathf.Min(hy,hz))*.8f);
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   for(int ring=0;ring<4;ring++) {
    bool face=ring==0||ring==3;float x=hx-(face?b:0),y=hy-(face?b:0),cut=face?b*.25f:b,z=ring==0?-hz:ring==1?-hz+b:ring==2?hz-b:hz;
    var contour=new[]{new Vector2(-x+cut,-y),new Vector2(x-cut,-y),new Vector2(x,-y+cut),new Vector2(x,y-cut),new Vector2(x-cut,y),new Vector2(-x+cut,y),new Vector2(-x,y-cut),new Vector2(-x,-y+cut)};
    foreach(var p in contour){vertices.Add(new Vector3(p.x,p.y,z));uv.Add(new Vector2(p.x/size.x+.5f,p.y/size.y+.5f));}
   }
   for(int ring=0;ring<3;ring++)for(int side=0;side<8;side++){int a=ring*8+side,b0=ring*8+(side+1)%8,c=a+8,d=b0+8;triangles.AddRange(new[]{a,b0,c,b0,d,c});}
   for(int face=0;face<2;face++){int centre=vertices.Count;vertices.Add(new Vector3(0,0,face==0?-hz:hz));uv.Add(new Vector2(.5f,.5f));for(int side=0;side<8;side++){int a=face==0?side:24+side,n=face==0?(side+1)%8:24+(side+1)%8;triangles.AddRange(face==0?new[]{centre,n,a}:new[]{centre,a,n});}}
   Transform model=art.Mesh(name,parent,vertices.ToArray(),triangles.ToArray(),material,uv.ToArray());model.localPosition=position;return model;
  }
 }
}
