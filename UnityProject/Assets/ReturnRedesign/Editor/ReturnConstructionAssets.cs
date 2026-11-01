using System;
using UnityEditor;
using UnityEngine;

namespace Return.Redesign.Editor {
 public static class ReturnConstructionAssets {
  const string Folder="Assets/ReturnRedesign/Art/Resources/ReturnConstruction/";
  public static void Prepare() {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var shader=Shader.Find("Return/Physical");if(shader==null)throw new InvalidOperationException("Construction physical shader missing");
   Material(shader,"Plaster","plastered_wall_02",1,.38f);
   Material(shader,"Timber","wood_table_worn",1,.32f);
   Material(shader,"Clay","clay_plaster",2,.36f);
   AssetDatabase.SaveAssets();
   Debug.Log("[RETURN] Construction materials imported: 9 repeat maps, sRGB albedo, linear GL normals/roughness.");
  }
  static void Material(Shader shader,string name,string asset,int channel,float bump) {
   string path=Folder+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
   material.shader=shader;material.SetColor("_BaseColor",Color.white);material.SetFloat("_Metallic",0);
   material.SetFloat("_BumpScale",bump);material.SetFloat("_UseRoughness",1);material.SetFloat("_SurfaceDetail",0);
   material.SetFloat("_AlbedoRelief",0);material.SetFloat("_RecoveryChannel",channel);
   material.SetTexture("_BaseMap",Map(asset+"_diff_1k.jpg",false,true));
   material.SetTexture("_BumpMap",Map(asset+"_nor_gl_1k.exr",true,false));
   material.SetTexture("_RoughnessMap",Map(asset+"_rough_1k.exr",false,false));
   EditorUtility.SetDirty(material);
  }
  static Texture2D Map(string filename,bool normal,bool colour) {
   string path=Folder+filename;var importer=AssetImporter.GetAtPath(path) as TextureImporter;
   if(importer==null)throw new InvalidOperationException("Construction texture missing: "+path);
   importer.maxTextureSize=1024;importer.npotScale=TextureImporterNPOTScale.None;
   importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
   importer.sRGBTexture=colour;importer.alphaSource=TextureImporterAlphaSource.None;
   importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
   importer.mipmapEnabled=true;importer.isReadable=false;importer.flipGreenChannel=false;
   importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
   if(texture==null)throw new InvalidOperationException("Construction texture import failed: "+path);
   return texture;
  }
 }
}
