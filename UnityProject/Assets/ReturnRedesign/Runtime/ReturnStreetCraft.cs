using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign {
 // Shared craft details give the recovered town an inhabited, sculpted identity.
 public sealed class ReturnStreetCraft {
  readonly ReturnVolumeArt art;
  readonly Material ivory,clay,timber,stone,green,linen,warm,cool,petal;
  public ReturnStreetCraft(ReturnVolumeArt art,Material ivory,Material clay,Material timber,Material stone) {
   this.art=art;this.ivory=ivory;this.clay=clay;this.timber=timber;this.stone=stone;
   green=Surface("Garden leaves",new Color(.32f,.54f,.46f),3);
   linen=Surface("Window linen",new Color(.94f,.90f,.81f),1);
   warm=Surface("Lamplight behind the windows",new Color(.94f,.70f,.39f),1,.16f);
   cool=Surface("Blue green window depth",new Color(.29f,.43f,.45f),3);
   petal=Surface("Recovered red blossoms",new Color(.85f,.36f,.29f),2);
  }
  Material Surface(string name,Color colour,int channel,float glow=0) {
   var material=art.Material(colour,.16f,glow);material.name="Carved city / "+name;
   material.SetFloat("_SurfaceDetail",0);material.SetFloat("_RecoveryChannel",channel);return material;
  }
  Transform Block(string name,Transform parent,Vector3 at,Vector3 size,Material material,float bevel=.025f)=>ReturnDoorReunion.Bevel(art,name,parent,at,size,bevel,material);
  public Material Window(int identity,Rect opening)=>((identity+(int)opening.yMin)%4==0)?warm:cool;

  public void Crown(Transform parent,float width,float height,float depth,float rise,int identity,Material facade,bool detailed) {
   if(identity%3==1) {
    Block("Porcelain roof terrace",parent,new Vector3(width*.5f,height+.06f,depth*.5f),new Vector3(width+.4f,.18f,depth+.4f),ivory,.045f);
    foreach(float z in new[]{-.10f,depth+.10f})Block("Terrace parapet",parent,new Vector3(width*.5f,height+.31f,z),new Vector3(width+.4f,.44f,.15f),facade,.035f);
    foreach(float x in new[]{-.10f,width+.10f})Block("Terrace return",parent,new Vector3(x,height+.31f,depth*.5f),new Vector3(.15f,.44f,depth+.4f),facade,.035f);
    if(detailed) {
     Block("Terrace stair pavilion",parent,new Vector3(width*.72f,height+.66f,depth*.70f),new Vector3(1.4f,1.15f,1.65f),facade,.06f);
     Block("Pavilion cap",parent,new Vector3(width*.72f,height+1.28f,depth*.70f),new Vector3(1.65f,.15f,1.9f),ivory,.04f);
     Block("Pavilion shadow",parent,new Vector3(width*.72f,height+.60f,depth*.70f-.84f),new Vector3(.65f,.86f,.025f),cool,.05f);
     foreach(float x in new[]{1.0f,width-1.0f}) {
      Block("Roof garden vessel",parent,new Vector3(x,height+.29f,depth*.38f),new Vector3(.66f,.42f,.66f),clay,.07f);
      ReturnQuietLandscape.CreateTree(art,parent,new Vector3(x,height+.50f,depth*.38f),timber,green,.19f);
     }
    }
   }else {
    Vector3[] corners={new Vector3(-.25f,height,-.30f),new Vector3(width+.25f,height,-.30f),new Vector3(width+.25f,height,depth+.30f),new Vector3(-.25f,height,depth+.30f)};
    Vector3 peak=new Vector3(width*.5f,height+rise,depth*.5f);var vertices=new List<Vector3>();var triangles=new List<int>();
    for(int i=0;i<4;i++){int at=vertices.Count;vertices.AddRange(new[]{corners[i],peak,corners[(i+1)%4]});triangles.AddRange(new[]{at,at+1,at+2});art.Tube("Folded roof rim",parent,corners[i],corners[(i+1)%4],.055f,clay);}
    art.Mesh("Four folded roof planes",parent,vertices.ToArray(),triangles.ToArray(),clay);
   }
  }
  public void HouseDetails(Transform parent,float width,float height,float depth,int identity,bool detailed) {
   foreach(float x in new[]{.10f,width-.10f})foreach(float z in new[]{.10f,depth-.10f})Block("Ivory corner seam",parent,new Vector3(x,height*.5f,z),new Vector3(.15f,height,.15f),ivory,.025f);
   if(!detailed)return;
   if(identity%3==0) {
    float roofY=height+1.15f;
    Block("Quiet chimney",parent,new Vector3(width*.72f,roofY,depth*.70f),new Vector3(.48f,.9f,.52f),ivory,.04f);
    Block("Chimney crown",parent,new Vector3(width*.72f,roofY+.49f,depth*.70f),new Vector3(.62f,.12f,.66f),clay,.035f);
   }
   Block("Lower storey belt",parent,new Vector3(width*.5f,1.25f,-.045f),new Vector3(width,.13f,.14f),ivory,.025f);
  }
  public void Opening(Transform face,Rect opening,bool entry,int identity,bool detailed) {
   if(!detailed)return;
   if(entry) {
    float x=opening.center.x,top=opening.yMax+.28f;
    if(identity%3==0) {
     Block("Sheltered threshold roof",face,new Vector3(x,top,-.63f),new Vector3(2.25f,.18f,1.55f),ivory,.05f);
     Block("Threshold coloured fascia",face,new Vector3(x,top-.10f,-1.36f),new Vector3(2.12f,.15f,.12f),clay,.02f);
     foreach(float side in new[]{-1f,1f}) {
      float px=x+side*.92f;art.Tube("Porch column",face,new Vector3(px,.10f,-1.16f),new Vector3(px,top-.10f,-1.16f),.10f,ivory);
      Block("Column foot",face,new Vector3(px,.07f,-1.16f),new Vector3(.34f,.14f,.34f),stone,.03f);
     }
    }
    float lanternX=opening.xMin-.35f;
    Block("Lantern bracket",face,new Vector3(lanternX,opening.yMax-.28f,-.13f),new Vector3(.10f,.10f,.32f),timber);
    Block("Warm threshold lantern",face,new Vector3(lanternX,opening.yMax-.48f,-.27f),new Vector3(.18f,.30f,.18f),warm,.035f);
    Block("Lantern cap",face,new Vector3(lanternX,opening.yMax-.29f,-.27f),new Vector3(.25f,.07f,.25f),clay,.02f);
   }else {
    Curtain(face,opening);
    if((identity+(int)opening.yMin)%3!=1)return;
    Vector3 at=new Vector3(opening.center.x,opening.yMin-.17f,-.25f);
    Block("Window flower vessel",face,at,new Vector3(opening.width+.12f,.24f,.42f),clay,.055f);
    for(int i=0;i<3;i++) {
     Vector3 foot=at+new Vector3((i-1)*opening.width*.28f,.13f,0);
     ReturnQuietLandscape.Sprig(art,face,foot,green,.50f,i*47);
     Blossom(face,foot+Vector3.up*.23f,.055f);
    }
   }
  }
  void Curtain(Transform parent,Rect opening) {
   for(int side=0;side<2;side++) {
    float x=side==0?opening.xMin+.07f:opening.xMax-opening.width*.22f-.07f;
    var vertices=new Vector3[20];var triangles=new List<int>();
    for(int y=0;y<5;y++)for(int col=0;col<4;col++) {
     int at=y*4+col;float u=col/3f,v=y/4f;
     vertices[at]=new Vector3(x+u*opening.width*.22f,Mathf.Lerp(opening.yMin+.06f,opening.yMax-.10f,v),.11f-Mathf.Sin(u*Mathf.PI*3)*.018f);
     if(y<4&&col<3)triangles.AddRange(new[]{at,at+4,at+1,at+1,at+4,at+5});
    }
    art.Mesh("Soft folded window linen",parent,vertices,triangles.ToArray(),linen);
   }
  }
  void Blossom(Transform parent,Vector3 at,float radius) {
   for(int p=0;p<5;p++){float angle=p*Mathf.PI*2/5;art.Shape("A ceramic flower petal",PrimitiveType.Sphere,parent,at+new Vector3(Mathf.Cos(angle),.1f,Mathf.Sin(angle))*radius,new Vector3(radius*1.15f,radius*.7f,radius*1.15f),petal);}
   art.Shape("A warm flower centre",PrimitiveType.Sphere,parent,at+Vector3.up*.015f,Vector3.one*radius*.72f,warm);
  }
  public void Courtyards(Transform parent) {
   foreach(float x in new[]{-17f,17f})foreach(float z in new[]{-9f,9f}) {
    var garden=new GameObject("A small garden between remembered houses").transform;garden.SetParent(parent,false);garden.localPosition=new Vector3(x,0,z);
    Block("Garden court paving",garden,new Vector3(0,-.005f,0),new Vector3(3.5f,.035f,4.0f),ivory,.015f);
    Block("Raised planted island",garden,new Vector3(.55f,.11f,.10f),new Vector3(1.8f,.24f,2.45f),clay,.10f);
    Block("Soft planted surface",garden,new Vector3(.55f,.24f,.10f),new Vector3(1.65f,.025f,2.30f),green,.025f);
    ReturnQuietLandscape.CreateTree(art,garden,new Vector3(.55f,.26f,.50f),timber,green,.43f);
    for(int i=0;i<5;i++) {
     Vector3 foot=new Vector3(.12f+(i%2)*.75f,.26f,-.8f+(i/2)*.45f);ReturnQuietLandscape.Sprig(art,garden,foot,green,.65f,i*67);Blossom(garden,foot+Vector3.up*.30f,.075f);
    }
    Block("Garden seat",garden,new Vector3(-1.0f,.42f,.25f),new Vector3(.55f,.12f,1.8f),timber,.035f);
    foreach(float end in new[]{-.4f,.9f})Block("Seat carved support",garden,new Vector3(-1.0f,.20f,end),new Vector3(.40f,.38f,.22f),stone,.03f);
    for(int i=0;i<3;i++)Block("Stepping stone",garden,new Vector3(-.65f+i*.55f,.022f,-1.55f),new Vector3(.42f,.025f,.48f),stone,.02f);
   }
  }
 }
}
