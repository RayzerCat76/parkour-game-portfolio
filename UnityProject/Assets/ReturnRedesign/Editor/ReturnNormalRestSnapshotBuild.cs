using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnNormalRestSnapshotBuild
    {
        const string AssetPath="Assets/ReturnRedesign/Art/Resources/ReturnRealism/Normal-Rest-Snapshot-v95.asset";
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static Vector3 Unit(Vector3 v){double length=Math.Sqrt((double)v.x*v.x+(double)v.y*v.y+(double)v.z*v.z);if(double.IsNaN(length)||double.IsInfinity(length)||length<=0)throw new InvalidOperationException("Invalid rest snapshot normal");return new Vector3((float)(v.x/length),(float)(v.y/length),(float)(v.z/length));}
        static Vector3 Surface(Vector3[] positions,int[] indices,int triangle,out bool degenerate){int at=triangle*3;Vector3 cross=Vector3.Cross(positions[indices[at+1]]-positions[indices[at]],positions[indices[at+2]]-positions[indices[at]]);degenerate=cross.x==0&&cross.y==0&&cross.z==0;return degenerate?Vector3.zero:Unit(cross);}
        static ReturnNormalRestSnapshot.ExpressionFrame[] CaptureExpressions(Mesh mesh,List<ReturnNormalRestSnapshot.Triangle> triangles,ReturnNormalRestSnapshot snapshot)
        {
            var selected=new SortedSet<int>();foreach(var triangle in triangles)foreach(var vertex in triangle.vertices)selected.Add(vertex.vertex);
            var positions=new Vector3[mesh.vertexCount];var normals=new Vector3[mesh.vertexCount];var tangents=new Vector3[mesh.vertexCount];var frames=new List<ReturnNormalRestSnapshot.ExpressionFrame>();
            foreach(string name in new[]{"Blink","SoftConcern","SoftRelief"}){
                int shape=mesh.GetBlendShapeIndex(name);if(shape<0||mesh.GetBlendShapeFrameCount(shape)!=1)throw new InvalidOperationException("Expression trace requires the current single frame: "+name);
                float weight=mesh.GetBlendShapeFrameWeight(shape,0);if(!Finite(weight)||weight<=0)throw new InvalidOperationException("Invalid expression trace frame weight");mesh.GetBlendShapeFrameVertices(shape,0,positions,normals,tangents);
                var deltas=new List<ReturnNormalRestSnapshot.ExpressionDelta>();foreach(int vertex in selected){Vector3 p=positions[vertex],n=normals[vertex];if(!Finite(p.x)||!Finite(p.y)||!Finite(p.z)||!Finite(n.x)||!Finite(n.y)||!Finite(n.z))throw new InvalidOperationException("Invalid expression trace delta");deltas.Add(new ReturnNormalRestSnapshot.ExpressionDelta{vertex=vertex,position=p,normal=n});snapshot.expressionDeltaSamples++;}
                frames.Add(new ReturnNormalRestSnapshot.ExpressionFrame{name=name,shape=shape,frame=0,weight=weight,deltas=deltas.ToArray()});snapshot.expressionFrames++;
            }
            return frames.ToArray();
        }
        public static void Capture()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Rest snapshot requires editor outside play loop");
            var existing=AssetDatabase.LoadAssetAtPath<ReturnNormalRestSnapshot>(AssetPath);
            var snapshot=ScriptableObject.CreateInstance<ReturnNormalRestSnapshot>();snapshot.finite=true;var sources=new List<ReturnNormalRestSnapshot.SourceMesh>();
            foreach(string person in new[]{"Mother","Nurse"}){
                string modelPath="Assets/ReturnRedesign/Art/Resources/ReturnRealism/"+person+".fbx";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);if(prefab==null)throw new InvalidOperationException("Rest snapshot imported model missing: "+modelPath);
                if(person=="Nurse"&&Environment.GetEnvironmentVariable("RETURN_CAPTURE_CLOTH_REST")=="1"){
                    string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");if(string.IsNullOrEmpty(evidence))throw new InvalidOperationException("Cloth rest evidence directory missing");
                    ReturnClothSurfaceReview.ExportRestGeometry(prefab,System.IO.Path.Combine(evidence,"cloth-rest-geometry.json"));
                }
                SkinnedMeshRenderer renderer=null;foreach(var candidate in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(candidate.sharedMesh!=null&&candidate.sharedMesh.name=="Face and body"){if(renderer!=null)throw new InvalidOperationException("Duplicate rest snapshot skin: "+person);renderer=candidate;}
                if(renderer==null)throw new InvalidOperationException("Rest snapshot Face and body missing: "+person);Mesh mesh=renderer.sharedMesh;
                Vector3[] positions=mesh.vertices,normals=mesh.normals;int[] indices=mesh.triangles;Transform[] bones=renderer.bones;Matrix4x4[] binds=mesh.bindposes;
                if(positions.Length!=mesh.vertexCount||normals.Length!=positions.Length||indices.Length%3!=0||bones.Length!=binds.Length)throw new InvalidOperationException("Rest snapshot mesh data mismatch");
                var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();snapshot.weightStreams++;
                try{
                    if(counts.Length!=positions.Length)throw new InvalidOperationException("Rest snapshot bone counts missing");var offsets=new int[counts.Length+1];for(int i=0;i<counts.Length;i++)offsets[i+1]=offsets[i]+counts[i];if(offsets[counts.Length]!=weights.Length)throw new InvalidOperationException("Rest snapshot weight stream mismatch");
                    var triangles=new List<ReturnNormalRestSnapshot.Triangle>();foreach(int triangle in person=="Nurse"?new[]{2140,15986}:new[]{4616}){
                        bool zero;Vector3 surface=Surface(positions,indices,triangle,out zero);if(zero)throw new InvalidOperationException("Degenerate rest snapshot target");var vertices=new List<ReturnNormalRestSnapshot.Vertex>();
                        for(int corner=0;corner<3;corner++){
                            int vertex=indices[triangle*3+corner];Vector3 normal=Unit(normals[vertex]);var influences=new List<ReturnNormalRestSnapshot.Influence>();float sum=0;
                            for(int at=offsets[vertex];at<offsets[vertex+1];at++){
                                var weight=weights[at];int bone=weight.boneIndex;if(bone<0||bone>=bones.Length||bones[bone]==null||!Finite(weight.weight)||weight.weight<0)throw new InvalidOperationException("Invalid rest snapshot influence");float parity=binds[bone].determinant;if(!Finite(parity))throw new InvalidOperationException("Invalid rest snapshot bind pose");sum+=weight.weight;
                                influences.Add(new ReturnNormalRestSnapshot.Influence{bone=bone,boneName=bones[bone].name,weight=weight.weight,bindParity=parity,bindPose=binds[bone]});
                            }
                            if(influences.Count==0||Mathf.Abs(sum-1)>.001f)throw new InvalidOperationException("Invalid rest snapshot weight sum");
                            var adjacent=new List<ReturnNormalRestSnapshot.Adjacent>();for(int at=0;at<indices.Length;at+=3)if(indices[at]==vertex||indices[at+1]==vertex||indices[at+2]==vertex){bool degenerate;Vector3 face=Surface(positions,indices,at/3,out degenerate);adjacent.Add(new ReturnNormalRestSnapshot.Adjacent{triangle=at/3,indices=new[]{indices[at],indices[at+1],indices[at+2]},degenerate=degenerate,restSurface=face});}
                            Vector3 p=positions[vertex];snapshot.finite&=Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(normal.x)&&Finite(normal.y)&&Finite(normal.z);vertices.Add(new ReturnNormalRestSnapshot.Vertex{vertex=vertex,position=p,normal=normals[vertex],influences=influences.ToArray(),adjacent=adjacent.ToArray()});snapshot.vertexSamples++;
                        }
                        triangles.Add(new ReturnNormalRestSnapshot.Triangle{triangle=triangle,indices=new[]{indices[triangle*3],indices[triangle*3+1],indices[triangle*3+2]},restSurface=surface,vertices=vertices.ToArray()});snapshot.triangleCount++;
                    }
                    var boneNames=new string[bones.Length];for(int i=0;i<bones.Length;i++){if(bones[i]==null)throw new InvalidOperationException("Missing rest snapshot bone");boneNames[i]=bones[i].name;}
                    sources.Add(new ReturnNormalRestSnapshot.SourceMesh{person=person,mesh=mesh,vertexCount=positions.Length,indexCount=indices.Length,importedReadable=mesh.isReadable,boneNames=boneNames,triangles=triangles.ToArray(),frames=CaptureExpressions(mesh,triangles,snapshot)});
                }finally{counts=default;weights=default;snapshot.clearedViewCount+=2;}
            }
            snapshot.meshes=sources.ToArray();snapshot.borrowedViewsCleared=snapshot.clearedViewCount==4;snapshot.captured=snapshot.finite&&sources.Count==2&&snapshot.triangleCount==3&&snapshot.vertexSamples==9&&snapshot.weightStreams==2&&snapshot.borrowedViewsCleared&&snapshot.expressionFrames==6&&snapshot.expressionDeltaSamples==27;
            if(!snapshot.captured)throw new InvalidOperationException("Rest snapshot incomplete");if(existing==null)AssetDatabase.CreateAsset(snapshot,AssetPath);else{EditorUtility.CopySerialized(snapshot,existing);UnityEngine.Object.DestroyImmediate(snapshot);}AssetDatabase.SaveAssets();
            Debug.Log("[RETURN] Rest normal snapshot PASS: 2 exact mesh references, 3 triangles, 9 vertices; import settings retained.");
        }
    }
}
