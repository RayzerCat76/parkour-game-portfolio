using System.Collections.Generic;
using UnityEngine;

namespace Return.Redesign {
 public sealed class ReturnTravellerStyle {
  readonly Transform hood,coat;
  readonly Transform[] hands=new Transform[2];
  readonly List<SkinnedMeshRenderer> anatomy=new List<SkinnedMeshRenderer>();
  public bool HandsVisible=>hands[0].gameObject.activeInHierarchy||hands[1].gameObject.activeInHierarchy;
  public bool VolumeVisible=>hood.gameObject.activeInHierarchy&&coat.gameObject.activeInHierarchy&&hood.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.15f&&coat.GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.2f;
  public bool DetailsConcealed {get{foreach(var renderer in anatomy)if(renderer.enabled)return false;return anatomy.Count>0;}}
  public ReturnTravellerStyle(ReturnVolumeArt art,Transform root,Dictionary<string,Transform> bones,Dictionary<string,Quaternion> restRotation,Vector3 mouth) {
   var ivory=art.Material(new Color(.95f,.96f,.91f),.08f);ivory.name="Dreamer_Clothes_QuietIvory";ivory.SetFloat("_SurfaceDetail",0);
   var slate=art.Material(new Color(.18f,.23f,.25f),.08f);slate.name="Dreamer_Shoes_QuietSlate";slate.SetFloat("_SurfaceDetail",0);
   var shadow=art.Material(new Color(.035f,.055f,.065f),.02f);shadow.SetFloat("_SurfaceDetail",0);
   foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()) {
    bool clothing=false,shoes=false;foreach(var material in renderer.sharedMaterials)if(material!=null){clothing|=material.name.StartsWith("Dreamer_Clothes");shoes|=material.name.StartsWith("Dreamer_Shoes");}
    if(clothing||shoes){var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=shoes?slate:ivory;renderer.sharedMaterials=materials;}
    else {renderer.enabled=false;anatomy.Add(renderer);}
   }
   Vector3 head=root.InverseTransformPoint(bones["head"].position),chest=root.InverseTransformPoint(bones["spine_02"].position);
   var hoodAnchor=Anchor("Quiet traveller hood anchor",bones["head"],restRotation["head"]);
   var seam=art.Material(new Color(.76f,.81f,.79f),.08f);seam.SetFloat("_SurfaceDetail",0);
   hood=OpenHood(art,hoodAnchor,mouth-head,ivory,shadow);
   Shell(art,"Fitted traveller collar",hoodAnchor,new[]{new Vector2(.13f,-.17f),new Vector2(.14f,-.105f),new Vector2(.12f,-.065f)},.80f,ivory);
   var coatAnchor=Anchor("Quiet traveller coat anchor",bones["spine_02"],restRotation["spine_02"]);
   float shoulder=Mathf.Abs(root.InverseTransformPoint(bones["upperarm_r"].position).x)+.035f;
   float hem=Mathf.Max(.55f,root.InverseTransformPoint(bones["calf_r"].position).y+.09f);
   float collar=head.y-.16f;
   coat=Shell(art,"Traveller travel coat",coatAnchor,new[]{new Vector2(shoulder*1.12f,hem-chest.y),new Vector2(shoulder,.93f-chest.y),new Vector2(shoulder*1.04f,collar-.13f-chest.y),new Vector2(.11f,collar-chest.y)},.72f,ivory);
   Shell(art,"Travel coat stitched hem",coatAnchor,new[]{new Vector2(shoulder*1.123f,hem-chest.y+.010f),new Vector2(shoulder*1.12f,hem-chest.y+.025f)},.725f,seam);
   art.Tube("Left pocket seam",coatAnchor,new Vector3(-shoulder*.56f,-.24f,shoulder*.64f),new Vector3(-shoulder*.28f,-.30f,shoulder*.69f),.006f,seam);
   art.Tube("Right pocket seam",coatAnchor,new Vector3(shoulder*.28f,-.30f,shoulder*.69f),new Vector3(shoulder*.56f,-.24f,shoulder*.64f),.006f,seam);
   for(int side=0;side<2;side++) {
    string suffix=side==0?"_r":"_l";var hand=bones["hand"+suffix];var holder=new GameObject("Traveller key hand"+suffix).transform;holder.SetParent(hand,false);
    Vector3 knuckle=hand.InverseTransformPoint(bones["middle_02"+suffix].position);
    var mitten=art.Shape("Simple covered palm",PrimitiveType.Sphere,holder,knuckle*.5f,new Vector3(.07f,knuckle.magnitude+.035f,.05f),ivory);mitten.localRotation=Quaternion.FromToRotation(Vector3.up,knuckle.normalized);
    Vector3 thumb=hand.InverseTransformPoint(bones["thumb_02"+suffix].position);art.Shape("Quiet thumb",PrimitiveType.Sphere,holder,thumb,new Vector3(.036f,.045f,.035f),ivory);
    hands[side]=holder;holder.gameObject.SetActive(false);
   }
  }
  static Transform Anchor(string name,Transform bone,Quaternion rest) {var anchor=new GameObject(name).transform;anchor.SetParent(bone,false);anchor.localRotation=Quaternion.Inverse(rest);return anchor;}
  static Transform OpenHood(ReturnVolumeArt art,Transform parent,Vector3 mouth,Material ivory,Material shadow) {
   const int sides=64,rings=20;const float opening=.88f,rx=.15f,ry=.19f,rz=.145f;
   float centreY=Mathf.Clamp(mouth.y+.08f,.025f,.09f),centreZ=mouth.z+.010f-rz*Mathf.Cos(opening);
   Vector3 Point(float theta,float phi) {
    float around=Mathf.Sin(theta),up=Mathf.Sin(phi);
    return new Vector3(rx*around*Mathf.Cos(phi),centreY+ry*around*up+.025f*Mathf.Pow(Mathf.Max(0,up),4)*around,centreZ+rz*Mathf.Cos(theta));
   }
   var vertices=new Vector3[(rings+1)*sides];var triangles=new List<int>();
   for(int ring=0;ring<=rings;ring++)for(int side=0;side<sides;side++) {
    int at=ring*sides+side,next=ring*sides+(side+1)%sides;
    vertices[at]=Point(Mathf.Lerp(opening,Mathf.PI,ring/(float)rings),side*Mathf.PI*2/sides);
    if(ring<rings)triangles.AddRange(new[]{at,at+sides,next,next,at+sides,next+sides});
   }
   var hood=art.Mesh("Traveller open hood",parent,vertices,triangles.ToArray(),ivory);
   var rim=new Vector3[sides*2];var rimTriangles=new List<int>();
   for(int side=0;side<sides;side++) {
    int next=(side+1)%sides;Vector3 edge=Point(opening,side*Mathf.PI*2/sides);
    rim[side]=edge;rim[side+sides]=new Vector3(edge.x*.90f,centreY+(edge.y-centreY)*.90f,edge.z-.012f);
    rimTriangles.AddRange(new[]{side,next,side+sides,side+sides,next,next+sides});
   }
   art.Mesh("Turned ivory hood edge",parent,rim,rimTriangles.ToArray(),ivory);
   var face=new Vector3[sides+1];var faceTriangles=new List<int>();face[0]=new Vector3(0,centreY,mouth.z-.025f);
   for(int side=0;side<sides;side++) {
    Vector3 edge=Point(opening,side*Mathf.PI*2/sides);face[side+1]=new Vector3(edge.x*.94f,centreY+(edge.y-centreY)*.94f,mouth.z-.025f);
    faceTriangles.AddRange(new[]{0,side+1,(side+1)%sides+1});
   }
   art.Mesh("Recessed traveller face",parent,face,faceTriangles.ToArray(),shadow);return hood;
  }
  static Transform Shell(ReturnVolumeArt art,string name,Transform parent,Vector2[] profile,float depth,Material material) {
   const int sides=32;var vertices=new Vector3[profile.Length*sides];var triangles=new List<int>();
   for(int ring=0;ring<profile.Length;ring++)for(int side=0;side<sides;side++) {
    int at=ring*sides+side,next=ring*sides+(side+1)%sides;float angle=side*Mathf.PI*2/sides;
    vertices[at]=new Vector3(Mathf.Cos(angle)*profile[ring].x,profile[ring].y,Mathf.Sin(angle)*profile[ring].x*depth);
    if(ring+1<profile.Length)triangles.AddRange(new[]{at,at+sides,next,next,at+sides,next+sides});
   }
   return art.Mesh(name,parent,vertices,triangles.ToArray(),material);
  }
  public void ShowHands(bool right,bool left){hands[0].gameObject.SetActive(right);hands[1].gameObject.SetActive(left);}
 }
}
