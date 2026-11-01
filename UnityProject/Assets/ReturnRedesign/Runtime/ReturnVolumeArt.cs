using System.Collections.Generic;
using UnityEngine;
namespace Return.Redesign {
 // Authored geometry, shared by the island and awakening. Every piece has depth and casts light/shadow.
 public sealed class ReturnVolumeArt {
  readonly List<Material> materials=new List<Material>(); readonly List<Mesh> meshes=new List<Mesh>();
  readonly Material basis;
  public ReturnVolumeArt(){basis=Resources.Load<Material>("ReturnPhysical");if(basis==null)throw new System.InvalidOperationException("3D material missing");}
  public Material Material(Color colour,float smooth=.25f,float glow=0) { var m=new Material(basis);m.color=colour;m.SetFloat("_Smoothness",smooth);m.SetColor("_EmissionColor",colour*glow);m.SetFloat("_SurfaceDetail",.35f);materials.Add(m);return m; }
  public Material Copy(Material source){var m=new Material(source);materials.Add(m);return m;}
  public Transform Shape(string name,PrimitiveType kind,Transform parent,Vector3 position,Vector3 scale,Material material) {
   var g=GameObject.CreatePrimitive(kind);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
   Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=material;return g.transform;
  }
  public Transform Mesh(string name,Transform parent,Vector3[] vertices,int[] triangles,Material material,Vector2[] authoredUV=null) {
   var g=new GameObject(name);g.transform.SetParent(parent,false);var m=new Mesh{name=name};m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;m.vertices=vertices;m.triangles=triangles;var uv=new Vector2[vertices.Length];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(vertices[i].x,vertices[i].z);m.uv=authoredUV??uv;m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();meshes.Add(m);
   g.AddComponent<MeshFilter>().sharedMesh=m;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g.transform;
  }
   public Transform Tube(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material) {
    var t=Shape(name,PrimitiveType.Cylinder,parent,(a+b)/2,new Vector3(radius,(b-a).magnitude/2,radius),material);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;
   }
  Transform Lathe(string name,Transform parent,Vector2[] profile,Material material) {
   const int sides=64;var points=new Vector3[profile.Length*sides];var uv=new Vector2[points.Length];var triangles=new List<int>();
   float distance=0;
   for(int ring=0;ring<profile.Length;ring++) {
    if(ring>0)distance+=Vector2.Distance(profile[ring-1],profile[ring]);
    for(int side=0;side<sides;side++) {
     int k=ring*sides+side,next=ring*sides+(side+1)%sides;float angle=side*Mathf.PI*2/sides;
     points[k]=new Vector3(Mathf.Cos(angle)*profile[ring].x,profile[ring].y,Mathf.Sin(angle)*profile[ring].x);uv[k]=new Vector2(side/(float)sides,distance/.2f);
     if(ring+1<profile.Length)triangles.AddRange(new[]{k,k+sides,next,next,k+sides,next+sides});
    }
   }
   return Mesh(name,parent,points,triangles.ToArray(),material,uv);
  }
  public Transform Mug(string name,Transform parent,Vector3 position,float heading,Material ceramic,Material tea=null) {
   var mug=Lathe(name,parent,new[]{new Vector2(0,.002f),new Vector2(.027f,.002f),new Vector2(.030f,0),new Vector2(.034f,.004f),new Vector2(.037f,.012f),new Vector2(.041f,.077f),new Vector2(.044f,.091f),new Vector2(.044f,.094f),new Vector2(.042f,.096f),new Vector2(.039f,.094f),new Vector2(.038f,.09f),new Vector2(.035f,.018f),new Vector2(.030f,.011f),new Vector2(0,.011f)},ceramic);
   mug.localPosition=position;mug.localRotation=Quaternion.Euler(0,heading,0);
   const int rings=32,sides=12;var points=new Vector3[(rings+1)*sides];var triangles=new List<int>();
   for(int ring=0;ring<=rings;ring++) {
    float angle=Mathf.Lerp(-120,120,ring/(float)rings)*Mathf.Deg2Rad;Vector3 radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0),centre=new Vector3(.05f+.025f*Mathf.Cos(angle),.048f+.026f*Mathf.Sin(angle),0);
    for(int side=0;side<sides;side++) {
     int k=ring*sides+side,next=ring*sides+(side+1)%sides;float around=side*Mathf.PI*2/sides;
     points[k]=centre+(radial*Mathf.Cos(around)+Vector3.forward*Mathf.Sin(around))*.0045f;
     if(ring<rings)triangles.AddRange(new[]{k,k+sides,next,next,k+sides,next+sides});
    }
   }
   Mesh("Rounded ceramic handle",mug,points,triangles.ToArray(),ceramic);
   if(tea!=null)Lathe("Tea below the open rim",mug,new[]{new Vector2(.037f,.073f),new Vector2(.035f,.072f),new Vector2(0,.072f)},tea);
   return mug;
  }
  public bool FlatSupportHeight(Transform model,Transform reference,Vector3 at,float minimum,float maximum,out float height,out string part) {
   height=minimum;part="";bool found=false;
   foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {
    var mesh=filter.sharedMesh;if(mesh==null)continue;Bounds source=mesh.bounds;var bounds=new Bounds();bool first=true;
    Matrix4x4 matrix=reference.worldToLocalMatrix*filter.transform.localToWorldMatrix;
    // Bounds remain available for the existing non-readable imported models.
    for(int corner=0;corner<8;corner++) {
     Vector3 point=matrix.MultiplyPoint3x4(new Vector3((corner&1)==0?source.min.x:source.max.x,(corner&2)==0?source.min.y:source.max.y,(corner&4)==0?source.min.z:source.max.z));
     if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);
    }
    if(at.x-.034f<bounds.min.x||at.x+.034f>bounds.max.x||at.z-.034f<bounds.min.z||at.z+.034f>bounds.max.z)continue;
    float y=bounds.max.y;if(y<minimum||y>maximum||found&&y<=height)continue;height=y;part=filter.name;found=true;
   }
   return found;
  }
   // Split UV seams without losing the authored smooth normals. Each face uses actual mesh units.
   public void MetricUV(Transform visual,float metresPerRepeat,bool grainAlongLongEdge=false) {
    if(metresPerRepeat<=0)throw new System.ArgumentOutOfRangeException(nameof(metresPerRepeat));
    var filter=visual.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;
    // Built-in cylinder geometry is shared with other scenes; only edit a locally owned instance.
    if(!meshes.Contains(mesh)){mesh=Object.Instantiate(mesh);mesh.name=visual.name+" metric UV";meshes.Add(mesh);filter.sharedMesh=mesh;}
    var source=mesh.vertices;var normals=mesh.normals;var indices=mesh.triangles;
    var vertices=new Vector3[indices.Length];var mappedNormals=new Vector3[indices.Length];var uv=new Vector2[indices.Length];var triangles=new int[indices.Length];
    Vector3 size=mesh.bounds.size;
    for(int face=0;face<indices.Length;face+=3) {
     Vector3 n=Vector3.Cross(source[indices[face+1]]-source[indices[face]],source[indices[face+2]]-source[indices[face]]).normalized;
     int axis=Mathf.Abs(n.x)>Mathf.Abs(n.y)&&Mathf.Abs(n.x)>Mathf.Abs(n.z)?0:Mathf.Abs(n.y)>Mathf.Abs(n.z)?1:2;
     bool swap=grainAlongLongEdge&&(axis==0?size.y>size.z:axis==1?size.z>size.x:size.y>size.x);
     for(int corner=0;corner<3;corner++) {
      int k=face+corner,index=indices[k];Vector3 point=source[index];
      vertices[k]=point;mappedNormals[k]=normals[index];triangles[k]=k;
      Vector2 at=axis==0?new Vector2(point.z,point.y):axis==1?new Vector2(point.x,point.z):new Vector2(point.x,point.y);
      uv[k]=(swap?new Vector2(at.y,at.x):at)/metresPerRepeat;
     }
    }
    mesh.Clear();mesh.vertices=vertices;mesh.triangles=triangles;mesh.normals=mappedNormals;mesh.uv=uv;mesh.RecalculateTangents();mesh.RecalculateBounds();
   }
  public Transform Island(Transform parent,Vector3 centre,Material material) {
   const int rings=64,sides=144;var v=new Vector3[(rings+1)*sides];var t=new List<int>();
   for(int r=0;r<=rings;r++)for(int s=0;s<sides;s++) {
    float a=s*Mathf.PI*2/sides,rad=r/(float)rings*(IslandRadius(a)+.55f);Vector3 p=new Vector3(Mathf.Cos(a)*rad,0,Mathf.Sin(a)*rad);
    p.y=IslandHeight(p.x,p.z);v[r*sides+s]=p;
    if(r<rings){int n=(s+1)%sides,k=r*sides+s;t.AddRange(new[]{k,r*sides+n,k+sides,r*sides+n,(r+1)*sides+n,k+sides});}
   }
   var root=Mesh("A small living island",parent,v,t.ToArray(),material);root.localPosition=centre;return root;
  }
  public static float IslandRadius(float angle)=>3.66f+.17f*Mathf.Sin(angle*3+.5f)+.10f*Mathf.Sin(angle*5-.9f)+.04f*Mathf.Sin(angle*9+1.4f);
  public static bool IslandContains(float x,float z)=>new Vector2(x,z).magnitude<IslandRadius(Mathf.Atan2(z,x))+.54f;
  public static float IslandTrailX(float z)=>.13f+.87f*Mathf.SmoothStep(0,1,(z+3.8f)/3.65f);
  static float IslandHash(int x,int z,uint seed){uint n=unchecked((uint)x*374761393u+(uint)z*668265263u+seed);n=unchecked((n^(n>>13))*1274126177u);n^=n>>16;return (n&0xffffffu)/16777215f;}
  public static float IslandNoise(float x,float z,uint seed){int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);float a=x-ix,b=z-iz;a=a*a*(3-2*a);b=b*b*(3-2*b);return Mathf.Lerp(Mathf.Lerp(IslandHash(ix,iz,seed),IslandHash(ix+1,iz,seed),a),Mathf.Lerp(IslandHash(ix,iz+1,seed),IslandHash(ix+1,iz+1,seed),a),b);}
  static readonly float islandStandingHeight=IslandNaturalHeight(1,-.4f);
  public static float IslandHeight(float x,float z){
   float natural=IslandNaturalHeight(x,z),distance=new Vector2(x-1,z+.4f).magnitude;
   float standing=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.9f,distance));
    return Mathf.Lerp(natural,islandStandingHeight,standing);
  }
  static float IslandNaturalHeight(float x,float z){
   float r=new Vector2(x,z).magnitude,radius=IslandRadius(Mathf.Atan2(z,x));
   if(r>radius)return -.075f-.38f*Mathf.SmoothStep(0,1,(r-radius)/.55f);
   float u=Mathf.Clamp01(1-r/radius),shore=Mathf.SmoothStep(0,1,(radius-r)/.7f);
   float ground=-.075f+1.19f*Mathf.SmoothStep(0,1,u);
   float bank=.10f*Mathf.Exp(-((x+1.6f)*(x+1.6f)+(z-.4f)*(z-.4f))/1.1f);
   float worn=z<.5f?.025f*Mathf.Exp(-Mathf.Pow((x-IslandTrailX(z))/.48f,2)):0;
   return ground+shore*((IslandNoise(x*.75f,z*.75f,714)-.5f)*.10f+(IslandNoise(x*3.8f,z*3.8f,1906)-.5f)*.025f+bank-worn);
  }
  static float LeafWidth(float z)=>Mathf.Pow(Mathf.Max(.001f,1-Mathf.Pow(z/3.15f,2)),.65f)*2.35f*(1+.008f*Mathf.Sin(z*27));
  public const float LeafPlanScale=.40f,LeafHalfLength=3.1f;
  // Keep the leaf nose on the same bank as the platform becomes smaller.
  public const float LeafDockZ=23.105f-LeafHalfLength*LeafPlanScale;
  public static float LeafHeight(float x,float z){float side=x/LeafWidth(z);return .1f+side*side*.24f+z*z*.014f;}
  public static float LeafBankLift(Transform leaf,Vector3 point) {
    if(point.z<=1.35f)return 0;return LeafBankLift(point,leaf.TransformPoint(point));
  }
  public static float LeafBankLift(Vector3 point,Vector3 world) {
    if(point.z<=1.35f||!IslandContains(world.x,world.z-26))return 0;
   return Mathf.Max(0,IslandHeight(world.x,world.z-26)+.009f-world.y)*Mathf.SmoothStep(0,1,(point.z-1.35f)/.15f);
  }
  public static void DrapeLeaf(Transform leaf,Mesh mesh,Vector3[] shape,Vector3[] posed,Color32[] pins) {
   int count=shape.Length/2;var uv=mesh.uv;
   for(int i=0;i<count;i++){float lift=LeafBankLift(leaf,shape[i]);Vector3 offset=leaf.InverseTransformVector(Vector3.up*lift);posed[i]=shape[i]+offset;posed[count+i]=shape[count+i]+offset;
    Vector3 world=leaf.TransformPoint(posed[i]);bool contact=IslandContains(world.x,world.z-26)&&world.y-IslandHeight(world.x,world.z-26)<.06f;
    byte pin=(byte)(Mathf.Pow(Mathf.Abs(uv[i].x*2-1),2)*255*(contact?0:1));pins[i]=pins[count+i]=new Color32(255,255,255,pin);
   }
   mesh.vertices=posed;mesh.colors32=pins;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
  }
  public Transform FruitBough(Transform parent,Vector3 start,Vector3 end,Material bark) {
   var points=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
   Vector3 span=end-start,side=Vector3.Cross(Vector3.up,span).normalized;
   if(side.sqrMagnitude<.1f)side=Vector3.right;
   Vector3 controlA=start+span*.28f+Vector3.up*.42f+side*.035f,controlB=end-span*.12f+Vector3.up*.36f-side*.025f;
   System.Func<float,Vector3> curve=u=>Mathf.Pow(1-u,3)*start+3*(1-u)*(1-u)*u*controlA+3*(1-u)*u*u*controlB+u*u*u*end;
   void Sweep(System.Func<float,Vector3> path,System.Func<float,float> thickness,int rings,int sides) {
    int offset=points.Count;float distance=0;Vector3 previous=path(0);
    for(int row=0;row<=rings;row++){
     float u=row/(float)rings;Vector3 centre=path(u),direction=(path(Mathf.Min(1,u+.002f))-path(Mathf.Max(0,u-.002f))).normalized;
     Vector3 across=Vector3.Cross(direction,side).normalized;if(across.sqrMagnitude<.1f)across=Vector3.Cross(direction,Vector3.up).normalized;
     Vector3 normal=Vector3.Cross(across,direction).normalized;distance+=Vector3.Distance(previous,centre);previous=centre;
     float radius=thickness(u)*(1+.055f*Mathf.Sin(u*17+.7f));
     for(int col=0;col<=sides;col++){
      float angle=col*Mathf.PI*2/sides;int k=offset+row*(sides+1)+col;
      points.Add(centre+(across*Mathf.Cos(angle)+normal*Mathf.Sin(angle)*.91f)*radius);uv.Add(new Vector2(col/(float)sides,distance/.32f));
      if(row<rings&&col<sides)indices.AddRange(new[]{k,k+sides+1,k+1,k+1,k+sides+1,k+sides+2});
     }
    }
   }
   Sweep(curve,u=>Mathf.Lerp(.026f,.0035f,Mathf.Pow(u,.65f))+.008f*Mathf.Exp(-u*32),40,20);
   var leafPoints=new List<Vector3>();var leafUV=new List<Vector2>();var leafIndices=new List<int>();
   for(int shoot=0;shoot<3;shoot++){
    float attachment=.32f+shoot*.19f,sign=shoot%2==0?1:-1;Vector3 from=curve(attachment),to=from+side*(sign*(.15f+shoot*.025f))+span.normalized*.12f+Vector3.up*(.045f-shoot*.018f);
    System.Func<float,Vector3> twig=u=>Vector3.Lerp(from,to,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.025f);
    Sweep(twig,u=>Mathf.Lerp(.005f,.0012f,u),10,10);
    for(int leaf=0;leaf<5;leaf++){
     float u=.24f+leaf*.15f,leafSign=leaf%2==0?1:-1;Vector3 node=twig(u),along=(side*(sign*.4f)+span.normalized*(leafSign*.8f)+Vector3.up*(.16f-leaf*.07f)).normalized;
     Vector3 petiole=node+along*.012f;Sweep(t=>Vector3.Lerp(node,petiole,t),t=>Mathf.Lerp(.0011f,.0006f,t),2,6);
     Vector3 across=Vector3.Cross(Vector3.up,along).normalized;across=Quaternion.AngleAxis(leafSign*(18+shoot*9),along)*across;Vector3 normal=Vector3.Cross(along,across).normalized;
     float length=.105f+.012f*((leaf+shoot)%4),width=length*.43f;const int rows=8,columns=4;int offset=leafPoints.Count;
     for(int row=0;row<=rows;row++)for(int col=0;col<=columns;col++){
      float v=row/(float)rows,w=col/(float)columns*2-1,spread=width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(v*Mathf.PI)),.72f)*(1+.025f*Mathf.Sin(v*Mathf.PI*14));
      float cup=.0045f*Mathf.Sin(v*Mathf.PI)*(1-w*w)-.012f*v*v;
      leafPoints.Add(petiole+along*(length*v)+across*(spread*w)+normal*cup);leafUV.Add(new Vector2((w+1)*.5f,v));
      if(row<rows&&col<columns){int k=offset+row*(columns+1)+col;leafIndices.AddRange(new[]{k,k+1,k+columns+2,k,k+columns+2,k+columns+1});}
     }
    }
   }
   var bough=Mesh("Slender fruit bough with attached shoots",parent,points.ToArray(),indices.ToArray(),bark,uv.ToArray());
   Mesh("Small leaves along the fruit shoots",bough,leafPoints.ToArray(),leafIndices.ToArray(),Copy(Resources.Load<Material>("ReturnRealism/Tree_Leaves")),leafUV.ToArray());
   return bough;
  }
  public Transform Leaf(Transform parent,Material material,Material vein) {
   const int rows=72,columns=48;var uv=new Vector2[(rows+1)*(columns+1)];var v=new Vector3[(rows+1)*(columns+1)];var t=new List<int>();
   for(int r=0;r<=rows;r++)for(int c=0;c<=columns;c++) {
    float z=(r/(float)rows-.5f)*LeafHalfLength*2,side=c/(float)columns*2-1;
    float width=LeafWidth(z);
    uv[r*(columns+1)+c]=new Vector2(c/(float)columns,r/(float)rows);
    v[r*(columns+1)+c]=new Vector3(side*width,LeafHeight(side*width,z),z);
    if(r<rows&&c<columns){int k=r*(columns+1)+c;t.AddRange(new[]{k,k+columns+1,k+1,k+1,k+columns+1,k+columns+2});}
   }
   int count=v.Length;var shell=new Vector3[count*2];var shellUV=new Vector2[count*2];
   for(int i=0;i<count;i++){shell[i]=v[i];shell[count+i]=v[i]-Vector3.up*.004f;shellUV[i]=shellUV[count+i]=uv[i];}
   int topIndices=t.Count;for(int i=0;i<topIndices;i+=3)t.AddRange(new[]{count+t[i],count+t[i+2],count+t[i+1]});
   for(int r=0;r<rows;r++){int left=r*(columns+1),right=left+columns;t.AddRange(new[]{left,left+columns+1,count+left,count+left,left+columns+1,count+left+columns+1,right,count+right,right+columns+1,count+right,count+right+columns+1,right+columns+1});}
   for(int c=0;c<columns;c++){int near=c,far=rows*(columns+1)+c;t.AddRange(new[]{near,count+near,near+1,count+near,count+near+1,near+1,far,far+1,count+far,count+far,far+1,count+far+1});}
   var root=Mesh("A green leaf holds the body",parent,shell,t.ToArray(),material,shellUV);root.localScale=new Vector3(LeafPlanScale,1,LeafPlanScale);
   Tube("Leaf stem",root,new Vector3(0,.1f,-2.9f),new Vector3(.2f,.16f,-4.1f),.055f,vein);return root;
  }
  public Mesh OwnedMesh(string name){var mesh=new Mesh{name=name};meshes.Add(mesh);return mesh;}
  public Transform Model(string asset,Transform parent,Vector3 p) {var source=Resources.Load<GameObject>("ReturnRealism/"+asset);if(source==null)throw new System.InvalidOperationException("Detailed model missing: "+asset);var g=Object.Instantiate(source,parent,false);g.name=asset;g.transform.localPosition=p;return g.transform;}
  public Transform Tree(Transform parent,Vector3 basePosition,Material bark,Material leaves) {
   var tree=Model("FruitTree",parent,basePosition);
   const int count=8,rings=25,sides=16;
   var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
   for(int root=0;root<count;root++) {
    float angle=root*2.399f+.21f*Mathf.Sin(root*1.7f),length=.72f+.48f*(.5f+.5f*Mathf.Sin(root*2.1f+.4f));
    var centres=new Vector3[rings];var radii=new float[rings];
    for(int ring=0;ring<rings;ring++) {
     float along=ring/(float)(rings-1),distance=.12f+length*along;
     float bend=.13f*Mathf.Sin(Mathf.PI*along)*Mathf.Sin(root*1.9f+.6f);
     float x=Mathf.Cos(angle)*distance-Mathf.Sin(angle)*bend,z=Mathf.Sin(angle)*distance+Mathf.Cos(angle)*bend;
     radii[ring]=(.088f+.018f*Mathf.Sin(root*1.4f))*Mathf.Pow(1-along,1.45f)+.003f;
     float ground=IslandHeight(basePosition.x+x,basePosition.z+z-26)-basePosition.y;
     centres[ring]=new Vector3(x,ground+radii[ring]*.25f-.035f,z);
    }
    float travelled=0;int start=vertices.Count;
    for(int ring=0;ring<rings;ring++) {
     if(ring>0)travelled+=Vector3.Distance(centres[ring-1],centres[ring]);
     Vector3 tangent=(centres[Mathf.Min(ring+1,rings-1)]-centres[Mathf.Max(0,ring-1)]).normalized;
     Vector3 side=Vector3.Cross(Vector3.up,tangent).normalized,up=Vector3.Cross(tangent,side).normalized;
     for(int segment=0;segment<sides;segment++) {
      float turn=segment*Mathf.PI*2/sides;
      vertices.Add(centres[ring]+(side*Mathf.Cos(turn)+up*Mathf.Sin(turn))*radii[ring]);
      uv.Add(new Vector2(segment/(float)sides,travelled/.4f));
      if(ring>0) {
       int at=start+ring*sides+segment,next=start+ring*sides+(segment+1)%sides;
       triangles.AddRange(new[]{at,at-sides,next,next,at-sides,next-sides});
      }
     }
    }
   }
   Mesh("Curved roots returning into the soil",parent,vertices.ToArray(),triangles.ToArray(),Copy(Resources.Load<Material>("ReturnRealism/Tree_Bark")),uv.ToArray()).localPosition=basePosition;
   return tree;
  }
  public Transform Volcano(Transform parent,Vector3 position,Material rock,Material lava) {
   const int sides=128,rings=25;float[] radius={6.5f,5.2f,2.8f,1.8f,1.22f,.55f};float[] height={0,.55f,3.35f,4.6f,4.3f,2.95f};
   var v=new Vector3[sides*rings];var t=new List<int>();
   for(int r=0;r<rings;r++)for(int side=0;side<sides;side++) {
    float angle=side*Mathf.PI*2/sides,f=r/(float)(rings-1)*(radius.Length-1);int band=Mathf.Min(radius.Length-2,Mathf.FloorToInt(f));float along=f-band;float radial=Mathf.Lerp(radius[band],radius[band+1],along),y=Mathf.Lerp(height[band],height[band+1],along);
    float ridge=Mathf.Sin(angle*17+f*.8f)*.18f+Mathf.Sin(angle*31-f*2)*.055f;float erosion=(Mathf.PerlinNoise(Mathf.Cos(angle)*3+f,Mathf.Sin(angle)*3+f*.7f)-.5f)*.24f;radial+=ridge*Mathf.Sin(r*Mathf.PI/(rings-1))+erosion;
    v[r*sides+side]=new Vector3(Mathf.Cos(angle)*radial,y+erosion,Mathf.Sin(angle)*radial);
    if(r<rings-1){int k=r*sides+side,n=r*sides+(side+1)%sides;t.AddRange(new[]{k,k+sides,n,n,k+sides,n+sides});}
   }
   var volcano=Mesh("A broken crater remembers the red world",parent,v,t.ToArray(),rock);volcano.localPosition=position;
   Shape("The fire inside the crater",PrimitiveType.Sphere,volcano,new Vector3(0,2.95f,0),new Vector3(1.18f,.08f,1.18f),lava);
   for(int i=0;i<5;i++) {
    float a=i*1.23f+.2f;Vector3 previous=new Vector3(Mathf.Cos(a)*1.7f,4.45f,Mathf.Sin(a)*1.7f);
    for(int j=1;j<6;j++){float rad=1.7f+j*.78f;Vector3 next=new Vector3(Mathf.Cos(a+j*.018f)*rad,Mathf.Lerp(4.45f,.24f,j/5f),Mathf.Sin(a+j*.018f)*rad);Tube("An ember in a fault",volcano,previous,next,.025f,lava);previous=next;}
   }
   return volcano;
  }
  public void Dispose(){foreach(var m in materials)Object.DestroyImmediate(m);foreach(var m in meshes)Object.DestroyImmediate(m);materials.Clear();meshes.Clear();}
 }
}
