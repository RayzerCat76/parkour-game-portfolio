using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor
{
    public static class ReturnKneeSkinBuild
    {
        const string Source="Assets/ReturnRedesign/Art/Resources/ReturnRealism/Dreamer.fbx";
        const string Output="Assets/ReturnRedesign/Art/Resources/ReturnRealism/Dreamer_KneeSkin.asset";
        [Serializable] sealed class Preparation {public string source,output;public int vertices,reweightedVertices,ankleReweightedVertices;public float halfBand,ankleHalfBand,maximumWeightSumError;}

        public static void PrepareAndBuild()
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if(model==null)throw new InvalidOperationException("Dreamer source model missing");
            SkinnedMeshRenderer trousers=null;
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())if(renderer.name=="male_casualsuit02")trousers=renderer;
            if(trousers==null||trousers.sharedMesh==null)throw new InvalidOperationException("Dreamer trouser mesh missing");
            var mesh=UnityEngine.Object.Instantiate(trousers.sharedMesh);mesh.name="Dreamer_KneeSkin";
            var vertices=mesh.vertices;var originalCounts=mesh.GetBonesPerVertex().ToArray();var originalWeights=mesh.GetAllBoneWeights().ToArray();
            int[] thighs={-1,-1},calves={-1,-1},feet={-1,-1};Vector3[] knees=new Vector3[2],ankles=new Vector3[2];
            for(int i=0;i<trousers.bones.Length;i++)for(int leg=0;leg<2;leg++){
                string suffix=leg==0?"_l":"_r";
                if(trousers.bones[i].name=="thigh"+suffix)thighs[leg]=i;
                if(trousers.bones[i].name=="calf"+suffix){calves[leg]=i;knees[leg]=model.transform.InverseTransformPoint(trousers.bones[i].position);}
                if(trousers.bones[i].name=="foot"+suffix){feet[leg]=i;ankles[leg]=model.transform.InverseTransformPoint(trousers.bones[i].position);}
            }
            if(thighs[0]<0||thighs[1]<0||calves[0]<0||calves[1]<0||feet[0]<0||feet[1]<0)throw new InvalidOperationException("Dreamer garment leg bones missing");
            const float halfBand=.10f,ankleHalfBand=.10f;int offset=0,reweighted=0,ankleReweighted=0;float maximumSumError=0;
            var counts=new byte[vertices.Length];var weights=new List<BoneWeight1>(originalWeights.Length);
            for(int vertex=0;vertex<vertices.Length;vertex++){
                var influences=new List<BoneWeight1>();for(int i=0;i<originalCounts[vertex];i++)influences.Add(originalWeights[offset++]);
                Vector3 point=model.transform.InverseTransformPoint(trousers.transform.TransformPoint(vertices[vertex]));
                for(int leg=0;leg<2;leg++){
                    float pair=0;foreach(var influence in influences)if(influence.boneIndex==thighs[leg]||influence.boneIndex==calves[leg])pair+=influence.weight;
                    if(pair<.98f||Mathf.Abs(point.y-knees[leg].y)>=halfBand)continue;
                    // Spread the bend around the skeleton's knee, preserving this leg's total influence.
                    float thigh=Mathf.SmoothStep(0,1,Mathf.InverseLerp(knees[leg].y-halfBand,knees[leg].y+halfBand,point.y))*pair;
                    influences.RemoveAll(w=>w.boneIndex==thighs[leg]||w.boneIndex==calves[leg]);
                    if(thigh>0)influences.Add(new BoneWeight1{boneIndex=thighs[leg],weight=thigh});
                    if(pair-thigh>0)influences.Add(new BoneWeight1{boneIndex=calves[leg],weight=pair-thigh});
                    influences.Sort((a,b)=>b.weight.CompareTo(a.weight));reweighted++;break;
                }
                for(int leg=0;leg<2;leg++){
                    float pair=0;foreach(var influence in influences)if(influence.boneIndex==calves[leg]||influence.boneIndex==feet[leg])pair+=influence.weight;
                    if(pair<.98f||Mathf.Abs(point.y-ankles[leg].y)>=ankleHalfBand)continue;
                    // Keep the cuff transition centred on the ankle instead of attaching the lower row abruptly to the foot.
                    float calf=Mathf.SmoothStep(0,1,Mathf.InverseLerp(ankles[leg].y-ankleHalfBand,ankles[leg].y+ankleHalfBand,point.y))*pair;
                    influences.RemoveAll(w=>w.boneIndex==calves[leg]||w.boneIndex==feet[leg]);
                    if(calf>0)influences.Add(new BoneWeight1{boneIndex=calves[leg],weight=calf});
                    if(pair-calf>0)influences.Add(new BoneWeight1{boneIndex=feet[leg],weight=pair-calf});
                    influences.Sort((a,b)=>b.weight.CompareTo(a.weight));ankleReweighted++;break;
                }
                float sum=0;foreach(var influence in influences)sum+=influence.weight;maximumSumError=Mathf.Max(maximumSumError,Mathf.Abs(1-sum));
                counts[vertex]=(byte)influences.Count;weights.AddRange(influences);
            }
            if(offset!=originalWeights.Length||reweighted==0||ankleReweighted==0)throw new InvalidOperationException("Garment skin authoring did not cover source weights");
            using(var nativeCounts=new NativeArray<byte>(counts,Allocator.Temp))
            using(var nativeWeights=new NativeArray<BoneWeight1>(weights.ToArray(),Allocator.Temp))mesh.SetBoneWeights(nativeCounts,nativeWeights);
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Output);
            if(existing==null)AssetDatabase.CreateAsset(mesh,Output);
            else {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
            AssetDatabase.SaveAssets();
            string evidence=Environment.GetEnvironmentVariable("RETURN_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(evidence))throw new InvalidOperationException("Knee skin evidence directory missing");
            Directory.CreateDirectory(evidence);File.WriteAllText(Path.Combine(evidence,"knee-skin-authoring.json"),JsonUtility.ToJson(new Preparation{source=Source,output=Output,vertices=vertices.Length,reweightedVertices=reweighted,ankleReweightedVertices=ankleReweighted,halfBand=halfBand,ankleHalfBand=ankleHalfBand,maximumWeightSumError=maximumSumError},true));
            Debug.Log("[RETURN garment skin] Authored "+reweighted+" knee and "+ankleReweighted+" ankle vertices; original model preserved");
            ReturnBuild.MacAudioProbe();
        }
    }
}
