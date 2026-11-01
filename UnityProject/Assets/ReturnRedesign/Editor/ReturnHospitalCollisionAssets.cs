using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
namespace Return.Redesign.Editor {
 public static class ReturnHospitalCollisionAssets {
  const string PrefabPath="Assets/ReturnRedesign/Art/Resources/ReturnRealism/HospitalBedCollision.prefab";
  [Serializable] public sealed class Part {public string asset,name,mesh;public int vertices;public float transformError;public Vector3 boundsMin,boundsMax;}
  [Serializable] public sealed class Report {public bool passed,railContact,clearContact;public int meshes,vertices;public string prefab;public Part[] parts;}
  static bool BedName(string name){name=name.ToLowerInvariant();return new[]{"bed","rail","duvet","mattress","sheet","pillow","blanket","headboard","footboard"}.Any(name.Contains);}
  public static void Prepare(){
   var sourceRoot=new GameObject("Temporary hospital source geometry");var prepared=new GameObject("Prepared hospital bed and rail collision");var parts=new List<Part>();var colliders=new List<MeshCollider>();
   try {
    foreach(string asset in new[]{"HospitalRoomLit","HospitalDetailsLit"}){
     var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReturnRedesign/Art/Resources/ReturnRealism/"+asset+".fbx");if(source==null)throw new BuildFailedException("Missing hospital source model "+asset);
     var instance=UnityEngine.Object.Instantiate(source,sourceRoot.transform,false);instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.Euler(0,180,0);
     foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true)){
      if(filter.sharedMesh==null||!BedName(filter.name))continue;var mesh=filter.sharedMesh;var child=new GameObject(filter.name);child.transform.SetParent(prepared.transform,false);child.transform.localPosition=filter.transform.position;child.transform.localRotation=filter.transform.rotation;child.transform.localScale=filter.transform.lossyScale;
      float transformError=0;var original=filter.transform.localToWorldMatrix;var copy=child.transform.localToWorldMatrix;for(int i=0;i<16;i++)transformError=Mathf.Max(transformError,Mathf.Abs(original[i]-copy[i]));if(transformError>1e-5f)throw new BuildFailedException("Hospital collision transform mismatch: "+filter.name);
      var collider=child.AddComponent<MeshCollider>();collider.convex=false;Physics.BakeMesh(mesh.GetInstanceID(),false,collider.cookingOptions);collider.sharedMesh=mesh;colliders.Add(collider);parts.Add(new Part{asset=asset,name=filter.name,mesh=mesh.name,vertices=mesh.vertexCount,transformError=transformError});
     }
    }
    if(colliders.Count!=24)throw new BuildFailedException("Expected24 prepared hospital bed meshes");Physics.SyncTransforms();for(int i=0;i<colliders.Count;i++){var bounds=colliders[i].bounds;if(bounds.size.sqrMagnitude<=1e-8f)throw new BuildFailedException("Empty prepared bed collision: "+colliders[i].name);parts[i].boundsMin=bounds.min;parts[i].boundsMax=bounds.max;}
    var probeObject=new GameObject("Temporary prepared collision control");probeObject.transform.SetParent(sourceRoot.transform,false);probeObject.transform.localPosition=Vector3.down*10000;var probe=probeObject.AddComponent<SphereCollider>();probe.radius=.005f;Vector3 rail=new Vector3(.7809224129f,1.1033232212f,.2068492472f),clear=new Vector3(.8199462891f,1.1435135603f,.2069129944f);bool railContact=false,clearContact=false;foreach(var collider in colliders){railContact|=Physics.ComputePenetration(probe,rail,Quaternion.identity,collider,collider.transform.position,collider.transform.rotation,out _,out float railDepth)&&railDepth>.00001f;clearContact|=Physics.ComputePenetration(probe,clear,Quaternion.identity,collider,collider.transform.position,collider.transform.rotation,out _,out float clearDepth)&&clearDepth>.00001f;}if(!railContact||clearContact)throw new BuildFailedException("Prepared hospital collision controls failed");
    var prefab=PrefabUtility.SaveAsPrefabAsset(prepared,PrefabPath);if(prefab==null)throw new BuildFailedException("Hospital collision prefab save failed");AssetDatabase.SaveAssets();var report=new Report{passed=true,railContact=railContact,clearContact=clearContact,meshes=parts.Count,vertices=parts.Sum(p=>p.vertices),prefab=PrefabPath,parts=parts.ToArray()};string output=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"hospital-collision-prefab.json"),JsonUtility.ToJson(report,true));Debug.Log("[RETURN hospital collision] Prepared24 exact bed/rail meshes with native collision data");
   }finally{UnityEngine.Object.DestroyImmediate(prepared);UnityEngine.Object.DestroyImmediate(sourceRoot);}
  }
 }
}
