using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace Return.Redesign.Editor {
 public static class ReturnRealismAssets {
  const string PathRoot="Assets/ReturnRedesign/Art/Resources/ReturnRealism/";
  [Serializable] class Entry {public string name,diffuse,normal,ao,indirect,roughness,opacity;public float[] colour,emission;public float smoothness,metallic,channel,transmission,relief,wind,windMode,indirectStrength,detail,foliageNormalUp,skinDetail,skinOiliness;public bool alpha,hasDetail;}
  [Serializable] class Manifest {public Entry[] materials;}
  public static void Prepare() {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   Shader shader=Shader.Find("Return/Physical");if(shader==null)throw new InvalidOperationException("Physical shader missing");
   const string basis="Assets/ReturnRedesign/Art/Resources/ReturnPhysical.mat";
   if(AssetDatabase.LoadAssetAtPath<Material>(basis)==null)AssetDatabase.CreateAsset(new Material(shader),basis);
   var entries=JsonUtility.FromJson<Manifest>(File.ReadAllText(PathRoot+"materials.json")).materials;
   Texture2D skinDfg=SkinDFG();
   foreach(var e in entries) {
    bool faceStrands=e.name.EndsWith("_Brows")||e.name.EndsWith("_Lashes");
    string path=PathRoot+e.name+".mat";Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;
    m.SetColor("_BaseColor",new Color(e.colour[0],e.colour[1],e.colour[2],e.colour[3]));m.SetFloat("_Smoothness",e.smoothness);m.SetFloat("_Metallic",e.metallic);m.SetFloat("_RecoveryChannel",e.channel);m.SetFloat("_SurfaceDetail",e.hasDetail?e.detail:e.metallic>.5f?.06f:.35f);m.SetFloat("_Cutoff",e.alpha&&!faceStrands?.35f:.001f);m.SetFloat("_ThinSurface",e.transmission);m.SetFloat("_AlbedoRelief",e.relief);m.SetFloat("_Wind",e.wind);m.SetFloat("_WindMode",e.windMode);
    m.SetFloat("_FaceStrands",faceStrands?1:0);m.SetFloat("_SrcBlend",faceStrands?5:1);m.SetFloat("_DstBlend",faceStrands?10:0);m.SetFloat("_ZWrite",faceStrands?0:1);
     Texture(e.diffuse,"_BaseMap",m,false,e.name=="Island_Grass"||e.name.EndsWith("_Hair"),faceStrands);Texture(e.normal,"_BumpMap",m,true);Texture(e.ao,"_OcclusionMap",m,false);
     m.SetFloat("_FoliageNormalUp",e.foliageNormalUp);
     bool skin=e.name=="Dreamer_Skin"||e.name=="Mother_Skin"||e.name=="Nurse_Skin";m.SetFloat("_Skin",skin?1:0);if(skin)m.EnableKeyword("_SPECULAR_SETUP");else m.DisableKeyword("_SPECULAR_SETUP");
      m.SetFloat("_SkinDetail",e.skinDetail);m.SetFloat("_SkinOiliness",e.skinOiliness);
      bool hospitalSkin=e.name=="Mother_Skin"||e.name=="Nurse_Skin";
       m.SetFloat("_SkinDFGEnabled",hospitalSkin?1:0);m.SetTexture("_SkinDFG",hospitalSkin?skinDfg:null);
       m.SetFloat("_SkinReflectionVisibility",hospitalSkin?1:0);
      if(hospitalSkin&&string.IsNullOrEmpty(e.ao))m.SetTexture("_OcclusionMap",null);
     m.SetFloat("_Hair",e.name=="Mother_Hair"||e.name=="Nurse_Hair"?1:0);
    if(string.IsNullOrEmpty(e.opacity))m.SetTexture("_AlphaMap",Texture2D.whiteTexture);else Texture(e.opacity,"_AlphaMap",m,false);
    Texture(e.roughness,"_RoughnessMap",m,false);m.SetFloat("_UseRoughness",string.IsNullOrEmpty(e.roughness)?0:1);
    m.SetColor("_EmissionColor",e.emission!=null&&e.emission.Length>=3?new Color(e.emission[0],e.emission[1],e.emission[2]):Color.black);
    Texture(e.indirect,"_IndirectMap",m,false);m.SetFloat("_UseIndirect",string.IsNullOrEmpty(e.indirect)?0:1);m.SetFloat("_IndirectStrength",e.indirectStrength);
    if(faceStrands){m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");m.DisableKeyword("_ALPHATEST_ON");}
    else if(e.alpha){m.renderQueue=2450;m.SetOverrideTag("RenderType","TransparentCutout");m.EnableKeyword("_ALPHATEST_ON");}EditorUtility.SetDirty(m);
   }
   foreach(string name in new[]{"Dreamer","Mother","Nurse","FruitTree","HospitalDetails","BlueFruit","HospitalRoom","HospitalRoomLit","HospitalDetailsLit","ShoreRock","IslandGrass"}) {
    string fbx=PathRoot+name+".fbx";var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
    importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=false;importer.meshCompression=ModelImporterMeshCompression.Off;importer.isReadable=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
    foreach(var e in entries)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),e.name),AssetDatabase.LoadAssetAtPath<Material>(PathRoot+e.name+".mat"));
    importer.SaveAndReimport();
   }
   const string skyTexturePath="Assets/ReturnRedesign/Art/Resources/ReturnSkyPanorama-v26.png";
   var skyImporter=(TextureImporter)AssetImporter.GetAtPath(skyTexturePath);if(skyImporter==null)throw new InvalidOperationException("Recovered sky panorama missing");skyImporter.maxTextureSize=2048;skyImporter.npotScale=TextureImporterNPOTScale.None;skyImporter.textureType=TextureImporterType.Default;skyImporter.sRGBTexture=true;skyImporter.textureCompression=TextureImporterCompression.Uncompressed;skyImporter.isReadable=true;skyImporter.mipmapEnabled=true;skyImporter.wrapModeU=TextureWrapMode.Repeat;skyImporter.wrapModeV=TextureWrapMode.Clamp;skyImporter.filterMode=FilterMode.Trilinear;skyImporter.alphaSource=TextureImporterAlphaSource.None;skyImporter.SaveAndReimport();
   var skyShader=Shader.Find("Return/RecoveredSky");if(skyShader==null)throw new InvalidOperationException("Recovered sky shader missing");const string skyMaterialPath="Assets/ReturnRedesign/Art/Resources/ReturnDreamSky.mat";if(AssetDatabase.LoadAssetAtPath<Material>(skyMaterialPath)==null)AssetDatabase.CreateAsset(new Material(skyShader),skyMaterialPath);
   AssetDatabase.SaveAssets();Debug.Log("[RETURN] Anatomical skinned models, detailed tree/hospital and physical materials imported.");
  }
   [Serializable] sealed class DfgImportReport {public bool passed,linear,noMipmaps,clamp,bilinear;public int width,height;public string format;public float maximumHalfError;}
   static Texture2D SkinDFG(){
    byte[] bytes=File.ReadAllBytes(PathRoot+"SkinDFG-v70.bytes");
    const string path=PathRoot+"SkinDFG-v70.asset";
    using(var reader=new BinaryReader(new MemoryStream(bytes))){
     if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8))!="RSDFG70\0")throw new InvalidOperationException("Skin DFG header mismatch");
     int width=reader.ReadInt32(),height=reader.ReadInt32();
     if(width!=128||height!=128||bytes.Length!=16+width*height*8)throw new InvalidOperationException("Skin DFG dimensions mismatch");
     var colours=new Color[width*height];for(int i=0;i<colours.Length;i++)colours[i]=new Color(reader.ReadSingle(),reader.ReadSingle(),0,1);
     var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
     if(texture==null){texture=new Texture2D(width,height,TextureFormat.RGHalf,false,true){name="RETURN integrated hospital skin environment"};AssetDatabase.CreateAsset(texture,path);}
     texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;texture.anisoLevel=1;texture.SetPixels(colours);texture.Apply(false,false);EditorUtility.SetDirty(texture);
     var actual=texture.GetPixels();float error=0;for(int i=0;i<actual.Length;i++)error=Mathf.Max(error,Mathf.Abs(actual[i].r-colours[i].r),Mathf.Abs(actual[i].g-colours[i].g));
     var report=new DfgImportReport{width=texture.width,height=texture.height,format=texture.format.ToString(),linear=texture.graphicsFormat.ToString()=="R16G16_SFloat",noMipmaps=texture.mipmapCount==1,clamp=texture.wrapMode==TextureWrapMode.Clamp,bilinear=texture.filterMode==FilterMode.Bilinear,maximumHalfError=error};
     report.passed=report.width==128&&report.height==128&&report.format=="RGHalf"&&report.linear&&report.noMipmaps&&report.clamp&&report.bilinear&&error<=.0005f;
     string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Redesign/Evidence/v70"));Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"dfg-import.json"),JsonUtility.ToJson(report,true));
     if(!report.passed)throw new InvalidOperationException("Skin DFG import/precision check failed");
     return texture;
    }
   }
   static void Texture(string file,string property,Material m,bool normal,bool bladeCutout=false,bool softAlpha=false){if(string.IsNullOrEmpty(file))return;string path=PathRoot+file;var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(importer==null)throw new InvalidOperationException("Texture missing: "+path);importer.maxTextureSize=2048;importer.wrapMode=file.StartsWith("Island_Ground")||file.StartsWith("Tree_Bark")?TextureWrapMode.Repeat:TextureWrapMode.Clamp;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.mipmapEnabled=true;importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&property=="_BaseMap";if(bladeCutout||softAlpha){importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.mipMapsPreserveCoverage=!softAlpha;importer.alphaTestReferenceValue=.35f;}if(softAlpha)importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();m.SetTexture(property,AssetDatabase.LoadAssetAtPath<Texture2D>(path));}
 }
}
