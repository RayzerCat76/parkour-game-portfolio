using System;
using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign {
 public sealed class ReturnQuietLandscape {
  public readonly Transform Root,Tree;
  public ReturnQuietLandscape(ReturnVolumeArt art,Transform parent,Vector3 treeBase,Material bark,Material leaves) {
   Root=new GameObject("Still Water · a carved garden").transform;Root.SetParent(parent,false);
   Tree=CreateTree(art,Root,treeBase,bark,leaves);
   Material stone=Matte(art,new Color(.79f,.83f,.78f));UseAuthoredRecovery(stone,"Shore_Rock");
   Vector2[] shore={new Vector2(-3.05f,-.65f),new Vector2(-3.14f,.20f),new Vector2(-2.65f,1.9f),new Vector2(2.78f,1.35f),new Vector2(3.05f,-.28f),new Vector2(-1.45f,-2.90f)};
   for(int i=0;i<shore.Length;i++) {
    Vector2 p=shore[i];float h=.17f+(i%3)*.035f;
    Form(art,"A smooth shore stone",Root,new Vector3(p.x,ReturnVolumeArt.IslandHeight(p.x,p.y)+h*.24f,p.y+26),new Vector3(.43f+(i%2)*.11f,h,.34f),stone,8,24);
   }
   Vector2[] beds={new Vector2(-2.1f,-.8f),new Vector2(2.35f,.7f),new Vector2(-.8f,2.55f)};
   Color[] colours={new Color(.94f,.70f,.24f),new Color(.78f,.27f,.23f),new Color(.30f,.52f,.70f)};
   Material stalk=Matte(art,new Color(.39f,.54f,.45f));stalk.SetFloat("_RecoveryChannel",3);
   for(int i=0;i<beds.Length;i++) {
    Vector2 p=beds[i];Vector3 foot=new Vector3(p.x,ReturnVolumeArt.IslandHeight(p.x,p.y),p.y+26);
    Stem(art,"A quiet flower stem",Root,foot,foot+new Vector3(0,.12f,0),foot+new Vector3(.025f,.23f,0),foot+new Vector3(.02f,.30f,0),.014f,.010f,stalk);
    Material bud=Matte(art,colours[i]);bud.SetFloat("_RecoveryChannel",i+1);
    Form(art,"A single colour bud",Root,foot+new Vector3(.02f,.34f,0),new Vector3(.16f,.20f,.16f),bud,8,16);
   }
  }
  public static Transform CreateTree(ReturnVolumeArt art,Transform parent,Vector3 position,Material bark,Material leaves,float scale=1) {
   var tree=new GameObject("A sculpted fruit tree").transform;tree.SetParent(parent,false);tree.localPosition=position;tree.localScale=Vector3.one*scale;
   Stem(art,"The curved trunk",tree,Vector3.zero,new Vector3(-.12f,.85f,0),new Vector3(.08f,2.10f,.06f),new Vector3(-.04f,3.30f,-.06f),.20f,.025f,bark);
   Vector3[] tips={new Vector3(-1.05f,2.88f,.10f),new Vector3(-.72f,3.38f,-.28f),new Vector3(-.10f,3.78f,-.18f),new Vector3(.55f,3.48f,.20f),new Vector3(1.03f,3.02f,.36f),new Vector3(.54f,3.03f,-.72f),new Vector3(-.56f,2.97f,-.65f),new Vector3(-.10f,3.31f,.72f)};
   var canopyVertices=new List<Vector3>[3];var canopyTriangles=new List<int>[3];
   Material[] tones={leaves,art.Copy(leaves),art.Copy(leaves)};tones[1].color=leaves.color*1.08f;tones[2].color=leaves.color*.86f;
   for(int i=0;i<3;i++){canopyVertices[i]=new List<Vector3>();canopyTriangles[i]=new List<int>();}
   for(int i=0;i<tips.Length;i++) {
    Vector3 tip=tips[i],start=new Vector3(.02f,1.75f+(i%3)*.25f,.02f);
    Stem(art,"A tapering crown branch",tree,start,start+new Vector3(tip.x*.24f,.40f,tip.z*.24f),tip-new Vector3(tip.x*.22f,.32f,tip.z*.22f),tip,.078f,.012f,bark);
    Vector3 outward=new Vector3(tip.x,0,tip.z).normalized;
    Stem(art,"A leaf bearing twig",tree,tip-outward*.18f,tip+outward*.05f+Vector3.up*.04f,tip+outward*.27f+Vector3.up*.10f,tip+outward*.43f+Vector3.up*.17f,.017f,.004f,bark);
    for(int pair=0;pair<3;pair++)for(int side=-1;side<=1;side+=2) {
     Vector3 foot=tip+outward*(pair*.15f-.08f)+Vector3.up*(pair*.05f);
     Vector3 direction=Quaternion.AngleAxis(side*(62-pair*7),Vector3.up)*outward;
     float length=.65f+(pair==1?.10f:0)+(i%2)*.04f;int tone=(i+pair+(side==1?1:0))%3;
     Blade(canopyVertices[tone],canopyTriangles[tone],foot,foot+direction*length+Vector3.up*(.15f+pair*.025f),.36f+pair*.025f,side*12);
    }
    for(int j=-1;j<=1;j++) {
     Vector3 foot=tip+outward*.35f+Vector3.up*.13f;
     Vector3 direction=Quaternion.AngleAxis(j*27,Vector3.up)*outward;int tone=(i+j+4)%3;
     Blade(canopyVertices[tone],canopyTriangles[tone],foot,foot+direction*(j==0?.64f:.53f)+Vector3.up*(j==0?.22f:.12f),j==0?.40f:.33f,j*15);
    }
   }
   for(int i=0;i<3;i++)art.Mesh("Layered carved foliage "+i,tree,canopyVertices[i].ToArray(),canopyTriangles[i].ToArray(),tones[i]);
   return tree;
  }
  // Closed, gently folded blades give the canopy an articulated silhouette without alpha cards.
  static void Blade(List<Vector3> vertices,List<int> triangles,Vector3 foot,Vector3 tip,float width,float roll) {
   const int rows=8,sides=6,columns=sides+1,layerSize=(rows+1)*columns;
   int first=vertices.Count;Vector3 along=(tip-foot).normalized,right=Vector3.Cross(Vector3.up,along).normalized;
   right=Quaternion.AngleAxis(roll,along)*right;Vector3 normal=Vector3.Cross(along,right).normalized;
   float length=Vector3.Distance(foot,tip);
   for(int layer=0;layer<2;layer++)for(int row=0;row<=rows;row++) {
    float u=row/(float)rows,arch=Mathf.Sin(u*Mathf.PI),span=width*.5f*Mathf.Pow(Mathf.Max(0,arch),.78f);
    Vector3 centre=Vector3.Lerp(foot,tip,u)+normal*(arch*length*.07f);
    for(int side=0;side<=sides;side++) {
     float v=side/(float)sides*2-1;
     vertices.Add(centre+right*(v*span)+normal*(arch*(1-Mathf.Abs(v))*.035f+(layer==0?.007f:-.007f)));
     if(row<rows&&side<sides) {
      int k=first+layer*layerSize+row*columns+side;
      if(layer==0)triangles.AddRange(new[]{k,k+columns,k+1,k+1,k+columns,k+columns+1});
      else triangles.AddRange(new[]{k,k+1,k+columns,k+1,k+columns+1,k+columns});
     }
    }
   }
   for(int row=0;row<rows;row++)for(int side=0;side<=sides;side+=sides) {
    int a=first+row*columns+side,b=a+columns;
    if(side==0)triangles.AddRange(new[]{a,a+layerSize,b,b,a+layerSize,b+layerSize});
    else triangles.AddRange(new[]{a,b,a+layerSize,b,b+layerSize,a+layerSize});
   }
   for(int side=0;side<sides;side++)for(int row=0;row<=rows;row+=rows) {
    int a=first+row*columns+side,b=a+1;
    if(row==0)triangles.AddRange(new[]{a,b,a+layerSize,b,b+layerSize,a+layerSize});
    else triangles.AddRange(new[]{a,a+layerSize,b,b,a+layerSize,b+layerSize});
   }
  }
  public static void Sprig(ReturnVolumeArt art,Transform parent,Vector3 position,Material leaves,float scale=1,float yaw=0) {
   var sprig=new GameObject("A carved leaf spray").transform;sprig.SetParent(parent,false);sprig.localPosition=position;sprig.localRotation=Quaternion.Euler(0,yaw,0);sprig.localScale=Vector3.one*scale;
   Stem(art,"A fine curved stalk",sprig,Vector3.zero,new Vector3(.025f,.12f,0),new Vector3(-.025f,.28f,.02f),new Vector3(.025f,.42f,.03f),.012f,.004f,leaves);
   for(int i=0;i<5;i++) {
    var leaf=new GameObject("A shaped lance leaf").transform;leaf.SetParent(sprig,false);leaf.localPosition=new Vector3(0,.065f+i*.061f,0);leaf.localRotation=Quaternion.Euler(14+i*7,i*31,(i%2==0?-1:1)*(48-i*4));
    Form(art,"A rounded leaf blade",leaf,new Vector3(0,.115f,0),new Vector3(.085f,.26f,.035f),leaves,7,12);
   }
  }
  static Material Matte(ReturnVolumeArt art,Color colour){Material m=art.Material(colour,.08f);m.SetFloat("_SurfaceDetail",0);return m;}
  public static void UseAuthoredRecovery(Material target,string name) {
   Material source=Resources.Load<Material>("ReturnRealism/"+name);
   if(source==null||!source.HasProperty("_RecoveryChannel"))throw new InvalidOperationException("Authored colour recovery missing: "+name);
   target.SetFloat("_RecoveryChannel",source.GetFloat("_RecoveryChannel"));
  }
  static Vector3 Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float u){float v=1-u;return v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;}
  static void Stem(ReturnVolumeArt art,string name,Transform parent,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float start,float end,Material material) {
   Surface(art,name,parent,u=>Curve(a,b,c,d,u),u=>Vector2.one*Mathf.Lerp(start,end,u),16,12,material);
  }
  static void Form(ReturnVolumeArt art,string name,Transform parent,Vector3 centre,Vector3 size,Material material,int rows,int sides) {
   Surface(art,name,parent,u=>centre+Vector3.up*((u-.5f)*size.y),u=>{
    float radius=.012f+.988f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.7f);
    return new Vector2(size.x,size.z)*(.5f*radius);
   },rows,sides,material);
  }
  static void Surface(ReturnVolumeArt art,string name,Transform parent,Func<float,Vector3> centre,Func<float,Vector2> radius,int rows,int sides,Material material) {
   int columns=sides+1,cap=(rows+1)*columns;var vertices=new Vector3[cap+2];var triangles=new List<int>();
   for(int row=0;row<=rows;row++) {
    float u=row/(float)rows;Vector3 p=centre(u);Vector2 r=radius(u);
    for(int side=0;side<=sides;side++) {
     float angle=side*Mathf.PI*2/sides;int k=row*columns+side;
     vertices[k]=p+new Vector3(Mathf.Cos(angle)*r.x,0,Mathf.Sin(angle)*r.y);
     if(row<rows&&side<sides)triangles.AddRange(new[]{k,k+columns,k+1,k+1,k+columns,k+columns+1});
    }
   }
   vertices[cap]=centre(0);vertices[cap+1]=centre(1);
   for(int side=0;side<sides;side++)triangles.AddRange(new[]{cap,side,side+1,cap+1,rows*columns+side+1,rows*columns+side});
   art.Mesh(name,parent,vertices,triangles.ToArray(),material);
  }
  public int MeshCount=>Root.GetComponentsInChildren<MeshFilter>(true).Length;
  public int VertexCount {get{int count=0;foreach(var mesh in Root.GetComponentsInChildren<MeshFilter>(true))count+=mesh.sharedMesh.vertexCount;return count;}}
  public bool HasVolume {get{var renderers=Tree.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return false;Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b.size.x>2&&b.size.y>3&&b.size.z>.8f;}}
  public bool MatteSurfaces {get{foreach(var r in Root.GetComponentsInChildren<Renderer>(true))foreach(Material m in r.sharedMaterials)if(m.GetFloat("_SurfaceDetail")>.001f)return false;return MeshCount>0;}}
  public bool RecoveryBound {get{foreach(var r in Root.GetComponentsInChildren<Renderer>(true))foreach(Material m in r.sharedMaterials)if(m.GetFloat("_RecoveryChannel")<1||m.GetFloat("_RecoveryChannel")>3)return false;return MeshCount>0;}}
 }
}
