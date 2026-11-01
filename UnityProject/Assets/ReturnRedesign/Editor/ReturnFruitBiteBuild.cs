using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Return.Redesign.Editor {
 public static class ReturnFruitBiteBuild {
  const string Folder="Assets/ReturnRedesign/Art/Resources/ReturnRealism/";
  struct Corner {
   public Vector3 p,n;public Vector2 uv;
   public static Corner Between(Corner a,Corner b,float t)=>new Corner{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
  }
  public static void Mac(){Bake();ReturnBuild.Mac();}
  public static void SculptAndMac(){Bake(true);ReturnBuild.Mac();}
  static T Save<T>(T candidate,string file) where T:Object {
   var existing=AssetDatabase.LoadAssetAtPath<T>(Folder+file);
   if(existing==null){AssetDatabase.CreateAsset(candidate,Folder+file);return candidate;}
   EditorUtility.CopySerialized(candidate,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(candidate);return existing;
  }
  static void Bake(bool replace=false) {
   if(!replace&&AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"BlueFruitBitten.prefab")!=null)throw new InvalidOperationException("Bitten fruit already exists; inspect the previous authoring attempt.");
   var source=Resources.Load<GameObject>("ReturnRealism/BlueFruit");if(source==null)throw new InvalidOperationException("Original blue fruit missing");
   var model=Object.Instantiate(source);model.name="BlueFruitBitten";
   try {
    MeshFilter body=null;float volume=-1;
    foreach(var filter in model.GetComponentsInChildren<MeshFilter>()) {var r=filter.GetComponent<MeshRenderer>();if(r==null)continue;Vector3 size=r.bounds.size;float v=size.x*size.y*size.z;if(v>volume){body=filter;volume=v;}}
    if(body==null)throw new InvalidOperationException("Original fruit has no surface mesh");
    Mesh original=body.sharedMesh;Bounds bounds=body.GetComponent<Renderer>().bounds;float radius=Mathf.Max(bounds.extents.x,Mathf.Max(bounds.extents.y,bounds.extents.z));
    Vector3 cutNormal=model.transform.TransformDirection(new Vector3(0,.3f,-1).normalized);
    Vector3 axisWorld=Vector3.Cross(cutNormal,Vector3.up);if(axisWorld.sqrMagnitude<.001f)axisWorld=Vector3.Cross(cutNormal,Vector3.right);axisWorld.Normalize();Vector3 acrossWorld=Vector3.Cross(cutNormal,axisWorld);
    float BiteHeight(float u,float v){
     float radial=Mathf.Sqrt(u*u+v*v)/radius,angle=Mathf.Atan2(v,u);
     float rim=.07f*radius*radial*radial*(.65f*Mathf.Cos(angle*5+.4f)+.35f*Mathf.Sin(angle*9-.6f));
     return radius*(.65f-.38f*(1-radial*radial))+rim;
    }
    float Distance(Vector3 local){Vector3 q=body.transform.TransformPoint(local)-bounds.center;return Vector3.Dot(q,cutNormal)-BiteHeight(Vector3.Dot(q,axisWorld),Vector3.Dot(q,acrossWorld));}
    Corner Intersection(Corner a,Corner b,float da){
     float lo=0,hi=1;for(int step=0;step<16;step++){float mid=(lo+hi)*.5f;if((Distance(Vector3.Lerp(a.p,b.p,mid))<=0)==(da<=0))lo=mid;else hi=mid;}
     return Corner.Between(a,b,(lo+hi)*.5f);
    }
    var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var rings=new List<Vector3>();var submeshes=new List<int[]>();
    using(var snapshots=MeshUtility.AcquireReadOnlyMeshData(original)) {
     var data=snapshots[0];using(var positions=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var ns=new NativeArray<Vector3>(data.vertexCount,Allocator.Temp))using(var ts=new NativeArray<Vector2>(data.vertexCount,Allocator.Temp)) {
      data.GetVertices(positions);data.GetNormals(ns);data.GetUVs(0,ts);
      for(int sub=0;sub<data.subMeshCount;sub++)using(var indices=new NativeArray<int>(data.GetSubMesh(sub).indexCount,Allocator.Temp)) {
       data.GetIndices(indices,sub);var output=new List<int>();
       for(int i=0;i<indices.Length;i+=3) {
        var polygon=new List<Corner>();for(int k=0;k<3;k++){int index=indices[i+k];polygon.Add(new Corner{p=positions[index],n=ns[index],uv=ts[index]});}
        var clipped=new List<Corner>();
        for(int k=0;k<polygon.Count;k++) {
         Corner a=polygon[k],b=polygon[(k+1)%polygon.Count];float da=Distance(a.p),db=Distance(b.p);
         if(da<=0)clipped.Add(a);
         if((da<=0)!=(db<=0)){Corner edge=Intersection(a,b,da);clipped.Add(edge);bool unique=true;foreach(var p in rings)if(Vector3.Distance(body.transform.TransformPoint(p),body.transform.TransformPoint(edge.p))<radius*.0001f){unique=false;break;}if(unique)rings.Add(edge.p);}
        }
        if(clipped.Count<3)continue;int first=vertices.Count;foreach(var c in clipped){vertices.Add(c.p);normals.Add(c.n);uv.Add(c.uv);}for(int k=1;k<clipped.Count-1;k++){output.Add(first);output.Add(first+k);output.Add(first+k+1);}
       }
       submeshes.Add(output.ToArray());
      }
     }
    }
    if(rings.Count<8)throw new InvalidOperationException("Fruit bite did not intersect a continuous skin surface");
    var skin=new Mesh{name="Blue fruit with a bite",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};skin.SetVertices(vertices);skin.SetNormals(normals);skin.SetUVs(0,uv);skin.subMeshCount=submeshes.Count;for(int i=0;i<submeshes.Count;i++)skin.SetTriangles(submeshes[i],i);skin.RecalculateTangents();skin.RecalculateBounds();body.sharedMesh=Save(skin,"BlueFruitBittenSkin.asset");
    Vector3 normal=body.transform.localToWorldMatrix.transpose.MultiplyVector(cutNormal).normalized;
    rings.Sort((a,b)=>{Vector3 qa=body.transform.TransformPoint(a)-bounds.center,qb=body.transform.TransformPoint(b)-bounds.center;return Mathf.Atan2(Vector3.Dot(qa,acrossWorld),Vector3.Dot(qa,axisWorld)).CompareTo(Mathf.Atan2(Vector3.Dot(qb,acrossWorld),Vector3.Dot(qb,axisWorld)));});
    const int layers=8;int count=rings.Count;
    var fleshPoints=new List<Vector3>{body.transform.InverseTransformPoint(bounds.center+cutNormal*BiteHeight(0,0))};var fleshUV=new List<Vector2>{Vector2.one*.5f};
    for(int layer=1;layer<=layers;layer++)foreach(var p in rings){
     float fraction=layer/(float)layers;Vector3 q=body.transform.TransformPoint(p)-bounds.center;float u=Vector3.Dot(q,axisWorld)*fraction,v=Vector3.Dot(q,acrossWorld)*fraction;
     float grain=.006f*radius*Mathf.Sin(u/radius*78)*Mathf.Sin(v/radius*63)*fraction*(1-fraction);
     Vector3 world=bounds.center+axisWorld*u+acrossWorld*v+cutNormal*(BiteHeight(u,v)+grain);
     fleshPoints.Add(layer==layers?p:body.transform.InverseTransformPoint(world));fleshUV.Add(new Vector2(.5f+u/radius*.5f,.5f+v/radius*.5f));
    }
    var fleshIndices=new List<int>();
    for(int i=0;i<count;i++)Triangle(0,1+i,1+(i+1)%count);
    for(int layer=1;layer<layers;layer++)for(int i=0;i<count;i++){int a=1+(layer-1)*count+i,b=1+(layer-1)*count+(i+1)%count,c=1+layer*count+i,d=1+layer*count+(i+1)%count;Triangle(a,c,d);Triangle(a,d,b);}
    void Triangle(int a,int b,int c){if(Vector3.Dot(Vector3.Cross(fleshPoints[b]-fleshPoints[a],fleshPoints[c]-fleshPoints[a]),normal)<0){int swap=b;b=c;c=swap;}fleshIndices.Add(a);fleshIndices.Add(b);fleshIndices.Add(c);}
    var fleshMesh=new Mesh{name="Exposed fruit flesh"};fleshMesh.SetVertices(fleshPoints);fleshMesh.SetUVs(0,fleshUV);fleshMesh.SetTriangles(fleshIndices,0);fleshMesh.RecalculateNormals();fleshMesh.RecalculateTangents();fleshMesh.RecalculateBounds();fleshMesh=Save(fleshMesh,"BlueFruitBittenFlesh.asset");
    var fleshMaterial=new Material(Resources.Load<Material>("ReturnPhysical")){name="Fresh blue fruit flesh",color=new Color(.75f,.74f,.57f)};fleshMaterial.SetFloat("_Smoothness",.46f);fleshMaterial.SetFloat("_SurfaceDetail",.32f);fleshMaterial=Save(fleshMaterial,"BlueFruitBittenFlesh.mat");
    var flesh=new GameObject("The exposed bite");flesh.transform.SetParent(body.transform,false);flesh.AddComponent<MeshFilter>().sharedMesh=fleshMesh;flesh.AddComponent<MeshRenderer>().sharedMaterial=fleshMaterial;
    PrefabUtility.SaveAsPrefabAsset(model,Folder+"BlueFruitBitten.prefab");AssetDatabase.SaveAssets();
    Debug.Log("[RETURN] Authored bitten fruit from original skin: "+original.vertexCount+" source vertices, "+rings.Count+" cut boundary points; original asset and importer unchanged.");
   }finally{Object.DestroyImmediate(model);}
  }
 }
}
