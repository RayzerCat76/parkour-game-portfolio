using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Return.Redesign.Editor {
 public static class ReturnFruitPresentationReview {
  [Serializable] class PoseFile {public Pose[] samples;}
  [Serializable] class Pose {public string label;public Vector3 rootPosition;public Quaternion rootRotation;}
  [Serializable] class Report {
   public bool passed,originalActive,bittenActive,fleshActive,skinOccludesFleshCentre;
   public float actionTime,cutNormalToCamera,skinDistance,fleshDistance;
   public string skinMesh,fleshMesh;public Vector3 camera,fleshCentre,cutNormal;
  }
  public static void Inspect() {
   string folder=Environment.GetEnvironmentVariable("RETURN_FRUIT_PROBE_DIR"),posePath=Environment.GetEnvironmentVariable("RETURN_FRUIT_POSE_FILE");
   if(string.IsNullOrEmpty(folder)||string.IsNullOrEmpty(posePath))throw new InvalidOperationException("Fruit probe paths required");
   Pose sample=Array.Find(JsonUtility.FromJson<PoseFile>(File.ReadAllText(posePath)).samples,p=>p.label=="Fruit-Pick");if(sample==null)throw new InvalidOperationException("Native fruit pose missing");
   var view=new GameObject("Fruit presentation diagnosis");var camera=view.AddComponent<Camera>();ReturnJourney3D journey=null;
   try {
    journey=new ReturnJourney3D(camera);journey.Begin();Set(journey,"Position",sample.rootPosition);Set(journey,"Yaw",sample.rootRotation.eulerAngles.y);
    for(int i=0;i<40;i++){journey.Step(1f/60,default);journey.UpdateCamera(1f/60,false);}
    if(!journey.CanFruit)throw new InvalidOperationException("Recorded native stance cannot reach the fruit");
    journey.Step(1f/60,new ReturnInput{interact=true});journey.UpdateCamera(1f/60,false);
    if(journey.FruitActionTime<0)throw new InvalidOperationException("Recorded stance did not start tasting");
    for(int frame=0;frame<180&&journey.FruitActionTime<2.35f;frame++){journey.Step(1f/60,default);journey.UpdateCamera(1f/60,false);}
    if(journey.FruitActionTime<2.35f)throw new InvalidOperationException("Tasting simulation did not advance");
    Transform original=Field<Transform>(journey,"fruit"),bitten=Field<Transform>(journey,"bittenFruit");Transform flesh=null;MeshFilter skin=null;float volume=-1;
    foreach(var f in bitten.GetComponentsInChildren<MeshFilter>(true)){if(f.name=="The exposed bite"){flesh=f.transform;continue;}Vector3 size=f.GetComponent<Renderer>().bounds.size;float v=size.x*size.y*size.z;if(v>volume){skin=f;volume=v;}}
    if(flesh==null||skin==null)throw new InvalidOperationException("Actual bitten fruit skin or flesh renderer missing");
    Mesh cap=flesh.GetComponent<MeshFilter>().sharedMesh;Vector3 centre=flesh.TransformPoint(cap.vertices[0]),normal=flesh.localToWorldMatrix.inverse.transpose.MultiplyVector(cap.normals[0]).normalized;
    Vector3 origin=camera.transform.position,ray=(centre-origin).normalized;float distance=Vector3.Distance(origin,centre),nearest=float.PositiveInfinity;var vertices=skin.sharedMesh.vertices;var indices=skin.sharedMesh.triangles;
    for(int i=0;i<indices.Length;i+=3){float hit=RayTriangle(origin,ray,skin.transform.TransformPoint(vertices[indices[i]]),skin.transform.TransformPoint(vertices[indices[i+1]]),skin.transform.TransformPoint(vertices[indices[i+2]]));if(hit>0)nearest=Mathf.Min(nearest,hit);}
    var report=new Report{passed=true,actionTime=journey.FruitActionTime,originalActive=original.gameObject.activeInHierarchy,bittenActive=bitten.gameObject.activeInHierarchy,fleshActive=flesh.gameObject.activeInHierarchy&&flesh.GetComponent<Renderer>().enabled,cutNormalToCamera=Vector3.Dot(normal,(origin-centre).normalized),skinOccludesFleshCentre=nearest<distance-.0002f,skinDistance=float.IsInfinity(nearest)?-1:nearest,fleshDistance=distance,skinMesh=AssetDatabase.GetAssetPath(skin.sharedMesh),fleshMesh=AssetDatabase.GetAssetPath(cap),camera=origin,fleshCentre=centre,cutNormal=normal};
    report.passed=report.bittenActive&&report.fleshActive&&!report.originalActive;
    Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"fruit-presentation-probe.json"),JsonUtility.ToJson(report,true));if(!report.passed)throw new InvalidOperationException("Tasting state did not activate the authored bitten fruit");
    Debug.Log("[RETURN] Fruit presentation probe PASS: normal toward camera="+report.cutNormalToCamera+", skin occludes flesh="+report.skinOccludesFleshCentre);
   }finally{journey?.Dispose();Object.DestroyImmediate(view);}
  }
  static T Field<T>(ReturnJourney3D j,string name)=>(T)typeof(ReturnJourney3D).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(j);
  static void Set(ReturnJourney3D j,string name,object value)=>typeof(ReturnJourney3D).GetProperty(name,BindingFlags.Instance|BindingFlags.Public).SetValue(j,value);
  static float RayTriangle(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c){Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(direction,e2);float determinant=Vector3.Dot(e1,p);if(Mathf.Abs(determinant)<1e-10f)return -1;float inverse=1/determinant;Vector3 t=origin-a;float u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return -1;Vector3 q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)return -1;return Vector3.Dot(e2,q)*inverse;}
 }
}
