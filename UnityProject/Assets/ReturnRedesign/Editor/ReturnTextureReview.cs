using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace Return.Redesign.Editor {
 public static class ReturnTextureReview {
  [Serializable] sealed class Probe {public bool passed,hasAtlasRect;public string texturePath,shaderPath,shaderName;public int width,height;public float authoredUV;public Vector4 atlas;public Vector2[] uv;public string[] images;}
  public static void Capture() {
   string folder=Environment.GetEnvironmentVariable("RETURN_TEXTURE_PROBE_DIR");if(string.IsNullOrEmpty(folder))throw new InvalidOperationException("Probe output not specified.");
   Directory.CreateDirectory(folder);var root=new GameObject("Temporary rock sampling probe");var art=new ReturnArt();
   try {
    Vector2[] points={new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.5f),new Vector2(-.5f,.5f)},uv={Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
    int[] triangles={0,1,2,0,2,3};var rect=new Vector4(.80f,.70f,.07f,.08f);
    var patch=art.TexturedMesh("Actual sampled patch",root.transform,points,triangles,uv,"ReturnRealism/Shore_Rock_Diffuse",rect,Color.white,0);
    var material=patch.GetComponent<MeshRenderer>().sharedMaterial;var texture=(Texture2D)material.GetTexture("_MainTex");
    bool covered=Render(patch,Path.Combine(folder,"sampled-patch.png"));
    var full=art.TexturedMesh("Actual loaded atlas",root.transform,points,triangles,uv,"ReturnRealism/Shore_Rock_Diffuse",new Vector4(0,0,1,1),Color.white,0);
    covered&=Render(full,Path.Combine(folder,"loaded-atlas.png"));
    var report=new Probe{passed=covered,hasAtlasRect=material.HasProperty("_RockAtlasRect"),texturePath=AssetDatabase.GetAssetPath(texture),shaderPath=AssetDatabase.GetAssetPath(material.shader),shaderName=material.shader.name,width=texture.width,height=texture.height,authoredUV=material.GetFloat("_AuthoredUV"),atlas=material.GetVector("_RockAtlasRect"),uv=patch.GetComponent<MeshFilter>().sharedMesh.uv,images=new[]{"loaded-atlas.png","sampled-patch.png"}};
    report.passed&=report.hasAtlasRect&&report.authoredUV==1&&report.atlas==rect;
    File.WriteAllText(Path.Combine(folder,"probe-result.json"),JsonUtility.ToJson(report,true));if(!report.passed)throw new InvalidOperationException("Actual texture probe values or draw coverage failed.");
    Debug.Log("[RETURN] Rock texture probe PASS");
   }finally{art.Dispose();UnityEngine.Object.DestroyImmediate(root);}
  }
  static bool Render(Transform visual,string path) {
   var target=new RenderTexture(512,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var readback=new Texture2D(512,512,TextureFormat.RGB24,false);var command=new CommandBuffer();var previous=RenderTexture.active;
   try {
    target.Create();command.SetRenderTarget(target);command.ClearRenderTarget(false,true,Color.magenta);
    command.SetViewProjectionMatrices(Matrix4x4.identity,GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-.5f,.5f,-.5f,.5f,-1,1),true));
    command.DrawMesh(visual.GetComponent<MeshFilter>().sharedMesh,Matrix4x4.identity,visual.GetComponent<MeshRenderer>().sharedMaterial,0,0);Graphics.ExecuteCommandBuffer(command);
    RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,512,512),0,0,false);readback.Apply();File.WriteAllBytes(path,readback.EncodeToPNG());
    int empty=0;foreach(var c in readback.GetPixels32())if(c.r>245&&c.b>245&&c.g<10)empty++;return empty<512*512*.05f;
   }finally{RenderTexture.active=previous;command.Release();target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(readback);}
  }
 }
}
