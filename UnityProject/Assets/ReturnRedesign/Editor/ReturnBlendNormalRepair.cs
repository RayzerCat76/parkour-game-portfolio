using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public sealed class ReturnBlendNormalRepair : AssetPostprocessor
    {
        const string Folder="Assets/ReturnRedesign/Art/Resources/ReturnRealism/";
        [Serializable] sealed class RepairReport {public string person,mesh,firstMismatchChannel,firstExpected,firstActual;public int firstMismatchIndex=-1;public bool passed,baseDataRetained,framesRetained,scratchReleased,originalReplayCompleted,originalReplayCloneReleased,canonicalProbeCloneReleased,cleanupBounded;public int vertices,triangles,shapes,frames,clearedBorrowedViews;public TangentDifference[] originalReplayTangents,repairedTangents;public TangentCleanup[] staticTangentChanges;public FrameCheck[] fullFrameChecks;}
        [Serializable] sealed class TangentDifference {public string name,firstExpected,firstActual;public int shape,frame,vertices,differingVertices,differingComponents,firstMismatchIndex=-1;public float maxAbsoluteComponentDelta,maxX,maxY,maxZ;public bool finite=true,metadataMatched;}
        [Serializable] sealed class TangentCleanup {public string name,original,intended;public int shape,frame,index;public float maxComponent;public bool finite,positionZero,normalZero;}
        [Serializable] sealed class FrameCheck {public string name;public int frame;public bool positionsRetained,normalsRetained,tangentsRetained,finite;}
        sealed class Frame {public string name;public int shape,frame;public float weight;public Vector3[] positions,normals,tangents;}
        public override uint GetVersion()=>103;
        static string Describe<T>(T value){if(value is Vector3 v)return v.x.ToString("R")+","+v.y.ToString("R")+","+v.z.ToString("R");if(value is Vector4 q)return q.x.ToString("R")+","+q.y.ToString("R")+","+q.z.ToString("R")+","+q.w.ToString("R");if(value is BoneWeight1 w)return w.boneIndex+":"+w.weight.ToString("R");return value==null?"null":value.ToString();}
        static void Mismatch(RepairReport report,string channel,int index,string expected,string actual){if(!string.IsNullOrEmpty(report.firstMismatchChannel))return;report.firstMismatchChannel=channel;report.firstMismatchIndex=index;report.firstExpected=expected;report.firstActual=actual;}
        static bool Same<T>(T[] a,T[] b,string channel,RepairReport report){if(a.Length!=b.Length){Mismatch(report,channel+" length",-1,a.Length.ToString(),b.Length.ToString());return false;}for(int i=0;i<a.Length;i++)if(!EqualityComparer<T>.Default.Equals(a[i],b[i])){Mismatch(report,channel,i,Describe(a[i]),Describe(b[i]));return false;}return true;}
        static Vector4[][] UVs(Mesh mesh){var channels=new Vector4[8][];for(int i=0;i<8;i++){var values=new List<Vector4>();mesh.GetUVs(i,values);channels[i]=values.ToArray();}return channels;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static float MaxComponent(Vector3 value)=>Mathf.Max(Mathf.Abs(value.x),Mathf.Max(Mathf.Abs(value.y),Mathf.Abs(value.z)));
        static TangentDifference[] CompareTangents(Mesh mesh,List<Frame> frames,int shapes)
        {
            var results=new List<TangentDifference>();foreach(var expected in frames){
                var result=new TangentDifference{name=expected.name,shape=expected.shape,frame=expected.frame,vertices=expected.tangents.Length};results.Add(result);
                result.metadataMatched=mesh.blendShapeCount==shapes&&expected.shape<mesh.blendShapeCount&&expected.frame<mesh.GetBlendShapeFrameCount(expected.shape)&&mesh.GetBlendShapeName(expected.shape)==expected.name&&mesh.GetBlendShapeFrameWeight(expected.shape,expected.frame)==expected.weight;
                if(!result.metadataMatched)continue;
                var positions=new Vector3[result.vertices];var normals=new Vector3[result.vertices];var actual=new Vector3[result.vertices];mesh.GetBlendShapeFrameVertices(expected.shape,expected.frame,positions,normals,actual);
                for(int i=0;i<actual.Length;i++){
                    Vector3 a=expected.tangents[i],b=actual[i];float x=Mathf.Abs(a.x-b.x),y=Mathf.Abs(a.y-b.y),z=Mathf.Abs(a.z-b.z);
                    bool finite=Finite(a.x)&&Finite(a.y)&&Finite(a.z)&&Finite(b.x)&&Finite(b.y)&&Finite(b.z)&&Finite(x)&&Finite(y)&&Finite(z);result.finite&=finite;
                    if(finite){result.maxX=Mathf.Max(result.maxX,x);result.maxY=Mathf.Max(result.maxY,y);result.maxZ=Mathf.Max(result.maxZ,z);result.maxAbsoluteComponentDelta=Mathf.Max(result.maxAbsoluteComponentDelta,Mathf.Max(x,Mathf.Max(y,z)));}
                    bool sameX=EqualityComparer<float>.Default.Equals(a.x,b.x),sameY=EqualityComparer<float>.Default.Equals(a.y,b.y),sameZ=EqualityComparer<float>.Default.Equals(a.z,b.z);
                    result.differingComponents+=(sameX?0:1)+(sameY?0:1)+(sameZ?0:1);if(!sameX||!sameY||!sameZ){result.differingVertices++;if(result.firstMismatchIndex<0){result.firstMismatchIndex=i;result.firstExpected=Describe(a);result.firstActual=Describe(b);}}
                }
            }return results.ToArray();
        }
        static void CanonicalizeStaticTangents(Mesh mesh,List<Frame> frames,RepairReport report)
        {
            var changes=new List<TangentCleanup>();report.cleanupBounded=true;Mesh clone=UnityEngine.Object.Instantiate(mesh);clone.name="RETURN corrected expression omission check";
            try{clone.ClearBlendShapes();foreach(var frame in frames)clone.AddBlendShapeFrame(frame.name,frame.weight,frame.positions,frame.normals,frame.tangents);
                foreach(var frame in frames){var p=new Vector3[mesh.vertexCount];var n=new Vector3[mesh.vertexCount];var actual=new Vector3[mesh.vertexCount];clone.GetBlendShapeFrameVertices(frame.shape,frame.frame,p,n,actual);
                    for(int i=0;i<actual.Length;i++)if(!frame.tangents[i].Equals(actual[i])){
                        Vector3 original=frame.tangents[i];bool positionZero=frame.positions[i].Equals(Vector3.zero),normalZero=frame.normals[i].Equals(Vector3.zero),finite=Finite(original)&&Finite(actual[i]);float maximum=MaxComponent(original);
                        if(!positionZero||!normalZero||!finite||maximum>1e-5f||!actual[i].Equals(Vector3.zero)){report.cleanupBounded=false;Mismatch(report,"unbounded tangent omission "+frame.name,i,Describe(original),Describe(actual[i]));continue;}
                        changes.Add(new TangentCleanup{name=frame.name,shape=frame.shape,frame=frame.frame,index=i,original=Describe(original),intended=Describe(Vector3.zero),maxComponent=maximum,positionZero=positionZero,normalZero=normalZero,finite=finite});frame.tangents[i]=Vector3.zero;
                    }
                }report.staticTangentChanges=changes.ToArray();
            }finally{UnityEngine.Object.DestroyImmediate(clone);report.canonicalProbeCloneReleased=clone==null;}
        }
        public static void EnsureImported(){foreach(string person in new[]{"Mother","Nurse"})AssetDatabase.ImportAsset(Folder+person+".fbx",ImportAssetOptions.ForceUpdate);}
        void OnPostprocessModel(GameObject root)
        {
            string person=assetPath==Folder+"Mother.fbx"?"Mother":assetPath==Folder+"Nurse.fbx"?"Nurse":null;if(person==null)return;
            SkinnedMeshRenderer skin=null;foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(renderer.sharedMesh!=null&&renderer.sharedMesh.name=="Face and body"){if(skin!=null)throw new InvalidOperationException("Duplicate geometry normal repair skin");skin=renderer;}
            if(skin==null)throw new InvalidOperationException("Geometry normal repair skin missing: "+person);Mesh mesh=skin.sharedMesh;
            Vector3[] vertices=mesh.vertices,normals=mesh.normals;Vector4[] tangents=mesh.tangents;int[] indices=mesh.triangles;Matrix4x4[] binds=mesh.bindposes;Vector4[][] uv=UVs(mesh);
            var countsView=mesh.GetBonesPerVertex();var weightsView=mesh.GetAllBoneWeights();var counts=countsView.ToArray();var weights=weightsView.ToArray();countsView=default;weightsView=default;
            var report=new RepairReport{person=person,mesh=mesh.name,vertices=vertices.Length,triangles=indices.Length/3,shapes=mesh.blendShapeCount,clearedBorrowedViews=2};
            var frames=new List<Frame>();for(int shape=0;shape<mesh.blendShapeCount;shape++)for(int frame=0;frame<mesh.GetBlendShapeFrameCount(shape);frame++){var p=new Vector3[vertices.Length];var n=new Vector3[vertices.Length];var t=new Vector3[vertices.Length];mesh.GetBlendShapeFrameVertices(shape,frame,p,n,t);frames.Add(new Frame{name=mesh.GetBlendShapeName(shape),shape=shape,frame=frame,weight=mesh.GetBlendShapeFrameWeight(shape,frame),positions=p,normals=n,tangents=t});}report.frames=frames.Count;
            Mesh originalReplay=UnityEngine.Object.Instantiate(mesh);originalReplay.name="RETURN original expression replay diagnostic";
            try{originalReplay.ClearBlendShapes();foreach(var frame in frames)originalReplay.AddBlendShapeFrame(frame.name,frame.weight,frame.positions,frame.normals,frame.tangents);report.originalReplayTangents=CompareTangents(originalReplay,frames,report.shapes);report.originalReplayCompleted=true;}
            finally{UnityEngine.Object.DestroyImmediate(originalReplay);report.originalReplayCloneReleased=originalReplay==null;}
            var scratch=new Mesh{name="RETURN expression geometry normal scratch",indexFormat=mesh.indexFormat};
            try{
                scratch.vertices=vertices;scratch.triangles=indices;scratch.RecalculateNormals();Vector3[] baseline=scratch.normals;var posed=new Vector3[vertices.Length];
                foreach(var frame in frames){for(int i=0;i<vertices.Length;i++)posed[i]=vertices[i]+frame.positions[i];scratch.vertices=posed;scratch.RecalculateNormals();Vector3[] calculated=scratch.normals;for(int i=0;i<vertices.Length;i++)frame.normals[i]=calculated[i]-baseline[i];}
                CanonicalizeStaticTangents(mesh,frames,report);
                mesh.ClearBlendShapes();foreach(var frame in frames)mesh.AddBlendShapeFrame(frame.name,frame.weight,frame.positions,frame.normals,frame.tangents);
                report.repairedTangents=CompareTangents(mesh,frames,report.shapes);
                report.baseDataRetained=Same(vertices,mesh.vertices,"base positions",report)&&Same(normals,mesh.normals,"base normals",report)&&Same(tangents,mesh.tangents,"base tangents",report)&&Same(indices,mesh.triangles,"indices",report)&&Same(binds,mesh.bindposes,"bind poses",report);var currentUV=UVs(mesh);for(int i=0;i<8;i++)report.baseDataRetained&=Same(uv[i],currentUV[i],"UV"+i,report);
                countsView=mesh.GetBonesPerVertex();weightsView=mesh.GetAllBoneWeights();report.baseDataRetained&=Same(counts,countsView.ToArray(),"bone counts",report)&&Same(weights,weightsView.ToArray(),"bone weights",report);countsView=default;weightsView=default;report.clearedBorrowedViews+=2;
                var checks=new List<FrameCheck>();report.framesRetained=mesh.blendShapeCount==report.shapes;if(!report.framesRetained)Mismatch(report,"shape count",-1,report.shapes.ToString(),mesh.blendShapeCount.ToString());int at=0;for(int shape=0;shape<mesh.blendShapeCount;shape++)for(int frame=0;frame<mesh.GetBlendShapeFrameCount(shape);frame++){var p=new Vector3[vertices.Length];var n=new Vector3[vertices.Length];var t=new Vector3[vertices.Length];mesh.GetBlendShapeFrameVertices(shape,frame,p,n,t);var old=frames[at++];bool name=mesh.GetBlendShapeName(shape)==old.name,weight=mesh.GetBlendShapeFrameWeight(shape,frame)==old.weight;if(!name)Mismatch(report,"frame name",at-1,old.name,mesh.GetBlendShapeName(shape));if(!weight)Mismatch(report,"frame weight "+old.name,at-1,old.weight.ToString("R"),mesh.GetBlendShapeFrameWeight(shape,frame).ToString("R"));bool positions=Same(old.positions,p,"frame positions "+old.name,report),tangentsRetained=Same(old.tangents,t,"frame tangents "+old.name,report),normalsRetained=Same(old.normals,n,"corrected frame normals "+old.name,report),finite=true;for(int i=0;i<p.Length;i++)finite&=Finite(p[i])&&Finite(n[i])&&Finite(t[i])&&Finite(old.positions[i])&&Finite(old.normals[i])&&Finite(old.tangents[i]);checks.Add(new FrameCheck{name=old.name,frame=old.frame,positionsRetained=positions,normalsRetained=normalsRetained,tangentsRetained=tangentsRetained,finite=finite});report.framesRetained&=name&&weight&&positions&&tangentsRetained&&normalsRetained&&finite;}report.fullFrameChecks=checks.ToArray();report.framesRetained&=at==frames.Count;if(at!=frames.Count)Mismatch(report,"frame count",-1,frames.Count.ToString(),at.ToString());
            }finally{UnityEngine.Object.DestroyImmediate(scratch);report.scratchReleased=scratch==null;}
            if(!report.scratchReleased)Mismatch(report,"scratch release",-1,"released","retained");if(report.clearedBorrowedViews!=4)Mismatch(report,"borrowed views cleared",-1,"4",report.clearedBorrowedViews.ToString());report.passed=report.baseDataRetained&&report.framesRetained&&report.scratchReleased&&report.clearedBorrowedViews==4&&report.cleanupBounded&&report.canonicalProbeCloneReleased;
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Redesign/Evidence/v103"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,person+"-normal-repair.json"),JsonUtility.ToJson(report,true));if(!report.passed)throw new InvalidOperationException("Expression geometry normal repair preservation failed: "+report.firstMismatchChannel+" at "+report.firstMismatchIndex);Debug.Log("[RETURN] Expression geometry normal repair PASS: "+person+", "+report.frames+" frames; other mesh and animation data retained.");
        }
    }
}
