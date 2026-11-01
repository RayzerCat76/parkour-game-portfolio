Shader "Return/Physical" {
 Properties {
  [MainTexture] _BaseMap("Albedo",2D)="white"{} [MainColor] _BaseColor("Colour",Color)=(1,1,1,1)
  _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Float)=1
  _OcclusionMap("Occlusion",2D)="white"{} _Smoothness("Smoothness",Range(0,1))=.3
  _RoughnessMap("Roughness",2D)="white"{} _UseRoughness("Use roughness map",Float)=0
  _Metallic("Metallic",Range(0,1))=0 _Cutoff("Cutout",Range(0,1))=.001
  _AlphaMap("Opacity mask",2D)="white"{}
  [HideInInspector] _FaceStrands("Soft facial strands",Float)=0
  [HideInInspector] _Glazing("Clear architectural glazing",Float)=0
  [HideInInspector] _ClothReview("Explicit cloth diagnostic",Float)=0
  [HideInInspector] _SrcBlend("Source blend",Float)=1
  [HideInInspector] _DstBlend("Destination blend",Float)=0
  [HideInInspector] _ZWrite("Depth writes",Float)=1
  _EmissionColor("Emission",Color)=(0,0,0,1) _RecoveryChannel("Recovery colour",Float)=0
  _Skin("Human skin diffusion",Float)=0
  [HideInInspector] _SkinDiffuseReview("Temporary native skin review",Float)=0
  [HideInInspector] _SkinReflectionReview("Temporary native reflection isolation",Float)=0
  [HideInInspector] _SkinDFGEnabled("Integrated hospital skin environment",Float)=0
  [HideInInspector] _SkinReflectionVisibility("Local skin reflection visibility",Float)=0
  [HideInInspector] _SkinDFG("Integrated GGX environment factors",2D)="black"{}
  _SkinDetail("Skin surface detail",Range(0,1))=.5
  _SkinOiliness("Skin oil variation",Range(0,1))=.5
  _Hair("Hospital hair fibres",Float)=0
  _SurfaceDetail("Surface detail",Float)=0
  [HideInInspector] _CeilingTiles("Filtered acoustic ceiling tiles",Float)=0
  [HideInInspector] _CeilingOrigin("Ceiling tile origin",Vector)=(0,0,0,0)
  [HideInInspector] _CeilingIrradiance("Ceiling diffuse bounce light",Vector)=(0,0,0,0)
  _ThinSurface("Thin surface transmission",Range(0,1))=0 _AlbedoRelief("Albedo relief",Range(0,2))=0
  _BackColour("Underside colour and blend",Color)=(1,1,1,0)
  _ShoreSoil("Wet shoreline soil",Range(0,1))=0
  [HideInInspector] _StreetSurface("Dry asphalt surface",Float)=0
  [HideInInspector] _StreetPaint("Worn road paint",Float)=0
  [HideInInspector] _StreetPaintAxis("Road paint axis",Float)=0
  [HideInInspector] _CityFacade("Weathered city plaster",Float)=0
  [HideInInspector] _CityFacadeHeight("City facade height",Float)=0
  [HideInInspector] _CityFacadeSeed("Building weather seed",Float)=0
    _Wind("Leaf motion",Float)=0 _WindMode("Leaf motion pin",Float)=0
    _TreeSway("Root-anchored tree sway",Range(0,1))=0
   _FoliageNormalUp("Grass canopy normal bias",Range(0,1))=0
  _IndirectMap("Baked indirect irradiance",2D)="black"{} _UseIndirect("Use baked indirect light",Float)=0 _IndirectStrength("Indirect light strength",Float)=1
  _IndirectFilter("Indirect light filtering",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off
  HLSLINCLUDE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
    TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
    TEXTURE2D(_SkinDFG); SAMPLER(sampler_SkinDFG);
   CBUFFER_START(UnityPerMaterial)
    float4 _CeilingOrigin;half _CeilingTiles;
    half4 _CeilingIrradiance;
    float4 _BaseMap_ST,_BaseMap_TexelSize,_IndirectMap_TexelSize; half4 _BaseColor,_EmissionColor,_BackColour; half _Smoothness,_Metallic,_BumpScale,_Cutoff,_RecoveryChannel,_SurfaceDetail,_ThinSurface,_AlbedoRelief,_Wind,_WindMode,_UseIndirect,_IndirectStrength,_IndirectFilter,_UseRoughness,_ShoreSoil,_StreetSurface,_StreetPaint,_StreetPaintAxis,_CityFacade,_CityFacadeHeight,_FoliageNormalUp,_Skin,_SkinDetail,_SkinOiliness,_FaceStrands,_ClothReview,_Hair,_SkinDiffuseReview,_SkinReflectionReview,_SkinDFGEnabled,_SkinReflectionVisibility,_Glazing,_TreeSway;
   float _CityFacadeSeed;
   CBUFFER_END
   float4 _ReturnTreeAnchor,_ReturnTreeBreeze;
   float3 ReturnTreeSlope(float3 positionWS){float h=max(0,(positionWS.y-_ReturnTreeAnchor.y)/max(.01,_ReturnTreeAnchor.w));return _ReturnTreeBreeze.xyz*(_TreeSway*2*h/max(.01,_ReturnTreeAnchor.w));}
   float3 ReturnTreeNormal(float3 normalWS,float3 positionWS){if(_TreeSway<.5)return normalWS;float3 slope=ReturnTreeSlope(positionWS);return normalize(float3(normalWS.x,normalWS.y-dot(slope.xz,normalWS.xz),normalWS.z));}
   float3 ReturnTreeTangent(float3 tangentWS,float3 positionWS){if(_TreeSway<.5)return tangentWS;return normalize(tangentWS+ReturnTreeSlope(positionWS)*tangentWS.y);}
   float3 ReturnWind(float3 positionOS,float2 uv,half anchor) {
    float pin=_WindMode>1.5?anchor:_WindMode>.5?pow(abs(uv.x*2-1),2):saturate(uv.y);
    float phase=_Time.y*1.15+positionOS.x*2+positionOS.z*.9;
    positionOS.y+=sin(phase)*_Wind*pin;positionOS.x+=cos(phase*.8)*_Wind*pin*.22;
    if(_TreeSway>.5){float3 positionWS=TransformObjectToWorld(positionOS);float h=max(0,(positionWS.y-_ReturnTreeAnchor.y)/max(.01,_ReturnTreeAnchor.w));positionWS+=_ReturnTreeBreeze.xyz*(h*h*_TreeSway);positionOS=TransformWorldToObject(positionWS);}return positionOS;
   }
   void ReturnCutout(float2 uv,half alpha) {clip(alpha*SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,uv).r-_Cutoff);}
   struct ReturnDepthInput {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 colour:COLOR;};
   struct ReturnDepthOutput {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   half4 ReturnDepthFragment(ReturnDepthOutput i):SV_Target {clip(.5-_FaceStrands);ReturnCutout(i.uv,SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a);return 0;}
  ENDHLSL
  Pass {
   Name "Physical" Tags {"LightMode"="UniversalForward"}
   Blend [_SrcBlend] [_DstBlend]
   ZWrite [_ZWrite]
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   #pragma shader_feature_local_fragment _ _SPECULAR_SETUP
    #include "ReturnPhysicalCommon.hlsl"
     #include "ReturnHairLighting.hlsl"
    half3 ReturnSkinEnvironmentFactor(BRDFData b,half NoV) {
     if(_SkinDFGEnabled>.5&&_SkinDiffuseReview<.5){
      half2 uv=half2(saturate(NoV),saturate((b.perceptualRoughness-.25h)/.60h));
      half2 ab=SAMPLE_TEXTURE2D_LOD(_SkinDFG,sampler_SkinDFG,uv,0).rg;
      return b.specular*ab.x+ab.y;
     }
     return EnvironmentBRDFSpecular(b,Pow4(1-NoV));
    }
    half3 ReturnSkinEnvironmentCorrection(InputData d,SurfaceData s) {
     BRDFData b;InitializeBRDFData(s,b);
     AmbientOcclusionFactor ao=CreateAmbientOcclusionFactor(d,s);
     half NoV=saturate(dot(d.normalWS,d.viewDirectionWS));
     half3 irradiance=GlossyEnvironmentReflection(reflect(-d.viewDirectionWS,d.normalWS),d.positionWS,b.perceptualRoughness,1,d.normalizedScreenSpaceUV);
     return irradiance*(ReturnSkinEnvironmentFactor(b,NoV)-EnvironmentBRDFSpecular(b,Pow4(1-NoV)))*ao.indirectAmbientOcclusion;
    }
    half3 ReturnReviewLightSpecular(BRDFData b,Light light,InputData d) {
     const BRDFData noCoat=(BRDFData)0;
     return LightingPhysicallyBased(b,noCoat,light,d.normalWS,d.viewDirectionWS,0,false)
           -LightingPhysicallyBased(b,noCoat,light,d.normalWS,d.viewDirectionWS,0,true);
    }
    half3 ReturnReviewReflection(InputData d,SurfaceData s) {
     // Explicit diagnostic clone only. Match UniversalFragmentPBR's BRDF/AO/lights.
     BRDFData b;InitializeBRDFData(s,b);
     AmbientOcclusionFactor ao=CreateAmbientOcclusionFactor(d,s);
     if(_SkinReflectionReview<1.5){
      half NoV=saturate(dot(d.normalWS,d.viewDirectionWS));
      half3 irradiance=GlossyEnvironmentReflection(reflect(-d.viewDirectionWS,d.normalWS),d.positionWS,b.perceptualRoughness,1,d.normalizedScreenSpaceUV);
      return irradiance*ReturnSkinEnvironmentFactor(b,NoV)*ao.indirectAmbientOcclusion;
     }
     half4 shadowMask=CalculateShadowMask(d);
     uint layers=GetMeshRenderingLayer();
     Light light=GetMainLight(d,shadowMask,ao);half3 direct=0;
     #ifdef _LIGHT_LAYERS
     if(IsMatchingLightLayer(light.layerMask,layers))
     #endif
     direct+=ReturnReviewLightSpecular(b,light,d);
     #if defined(_ADDITIONAL_LIGHTS)
     uint pixelLightCount=GetAdditionalLightsCount();
     #if USE_CLUSTER_LIGHT_LOOP
     [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++){
      CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
      Light additional=GetAdditionalLight(lightIndex,d,shadowMask,ao);
      #ifdef _LIGHT_LAYERS
      if(IsMatchingLightLayer(additional.layerMask,layers))
      #endif
      direct+=ReturnReviewLightSpecular(b,additional,d);
     }
     #endif
     LIGHT_LOOP_BEGIN(pixelLightCount)
      Light additional=GetAdditionalLight(lightIndex,d,shadowMask,ao);
      #ifdef _LIGHT_LAYERS
      if(IsMatchingLightLayer(additional.layerMask,layers))
      #endif
      direct+=ReturnReviewLightSpecular(b,additional,d);
     LIGHT_LOOP_END
     #endif
     return direct;
    }
   half4 Frag(V i,bool facing:SV_IsFrontFace):SV_Target {
    SurfaceData s;InputData d;ReturnSurface(i,facing,s,d);
    if(_ClothReview>1.5h)return half4(d.normalWS*.5h+.5h,1);
    if(_ClothReview>.5h)return half4(s.albedo,1);
    if(_Glazing>.5h){s.albedo=0;s.emission=0;}
     half4 result=UniversalFragmentPBR(d,s);Light main=GetMainLight(d.shadowCoord);
      if(_Skin>.5&&_SkinDiffuseReview>.5){
      // Diagnostic clone only: identical diffuse energy to SkinDiffuse below.
      s.albedo*=.972h;s.specular=0;s.smoothness=0;s.emission=0;
       result=UniversalFragmentPBR(d,s);
      }
      if(_Skin>.5&&_SkinDFGEnabled>.5&&_SkinDiffuseReview<.5)result.rgb+=ReturnSkinEnvironmentCorrection(d,s);
      if(_Skin>.5&&_SkinReflectionReview>.5)result.rgb-=ReturnReviewReflection(d,s);
     if(_Hair>.5)result.rgb=ReturnHairLighting(i,d,s,main);
    half through=saturate(dot(-d.normalWS,main.direction))*.32*_ThinSurface;
    result.rgb+=s.albedo*main.color*through*lerp(.55,1,main.shadowAttenuation);
    if(_Glazing>.5h){
     half fresnel=.04h+.96h*pow(1-saturate(abs(dot(d.normalWS,d.viewDirectionWS))),5);
     result.a=1-(1-_BaseColor.a)*(1-fresnel);
     result.rgb=MixFog(result.rgb/max(.001h,result.a),i.fog)*result.a;
    }else result.rgb=MixFog(result.rgb,i.fog);
    if(_FaceStrands>.5)result.a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a*SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,i.uv).r;
    return result;
   }
   ENDHLSL
  }
  Pass {
   Name "SkinDiffuse" Tags {"LightMode"="ReturnSkinDiffuse"}
   ZWrite Off ZTest LEqual
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment ReturnSkinFragment
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   #define _SPECULAR_SETUP 1
   #include "ReturnPhysicalCommon.hlsl"
    struct SkinDiffuseOutput {half4 diffuse:SV_Target0;half4 albedoRoot:SV_Target1;};
    SkinDiffuseOutput ReturnSkinFragment(V i,bool facing:SV_IsFrontFace) {
    clip(_Skin-.5);SurfaceData s;InputData d;ReturnSurface(i,facing,s,d);
    // Match the forward skin diffuse energy; zero specular/grazing reflection.
    s.albedo*=.972h;s.specular=0;s.smoothness=0;s.emission=0;
    half3 diffuse=UniversalFragmentPBR(d,s).rgb;
    diffuse=MixFog(diffuse,i.fog)-MixFog(half3(0,0,0),i.fog);
     SkinDiffuseOutput o;o.diffuse=half4(diffuse,1);
     o.albedoRoot=half4(_SkinDFGEnabled>.5?sqrt(max(s.albedo,.0001h)):half3(1,1,1),1);return o;
   }
   ENDHLSL
  }
   Pass {
    Name "SkinVisibility" Tags {"LightMode"="ReturnSkinVisibility"}
    ZWrite Off ZTest LEqual
    HLSLPROGRAM
    #pragma target 3.5
    #pragma vertex Vert
    #pragma fragment ReturnSkinVisibilityFragment
    #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
    #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
    #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
    #pragma multi_compile_fog
    #define _SPECULAR_SETUP 1
    #include "ReturnPhysicalCommon.hlsl"
    struct SkinVisibilityOutput {half4 environment:SV_Target0;half4 normal:SV_Target1;float4 position:SV_Target2;};
    SkinVisibilityOutput ReturnSkinVisibilityFragment(V i,bool facing:SV_IsFrontFace) {
     clip(_Skin-.5);clip(_SkinReflectionVisibility-.5);
     // Diagnostic diffuse/no-environment modes already remove this energy.
     clip(.5-_SkinDiffuseReview);clip(.5-_SkinReflectionReview);
     SurfaceData s;InputData d;ReturnSurface(i,facing,s,d);
     BRDFData b;InitializeBRDFData(s,b);AmbientOcclusionFactor ao=CreateAmbientOcclusionFactor(d,s);
     half NoV=saturate(dot(d.normalWS,d.viewDirectionWS));
     half3 factor=EnvironmentBRDFSpecular(b,Pow4(1-NoV));
     if(_SkinDFGEnabled>.5){
      half2 uv=half2(NoV,saturate((b.perceptualRoughness-.25h)/.60h));
      half2 ab=SAMPLE_TEXTURE2D_LOD(_SkinDFG,sampler_SkinDFG,uv,0).rg;
      factor=b.specular*ab.x+ab.y;
     }
     half3 environment=GlossyEnvironmentReflection(reflect(-d.viewDirectionWS,d.normalWS),d.positionWS,b.perceptualRoughness,1,d.normalizedScreenSpaceUV)*factor*ao.indirectAmbientOcclusion;
     SkinVisibilityOutput o;
     o.environment=half4(MixFog(environment,i.fog)-MixFog(half3(0,0,0),i.fog),1);
     o.normal=half4(d.normalWS*.5h+.5h,b.perceptualRoughness);o.position=float4(d.positionWS,1);return o;
    }
    ENDHLSL
   }
   Pass {
    Name "SkinVisibilityPosition" Tags {"LightMode"="ReturnSkinVisibilityPosition"}
    ZWrite Off ZTest LEqual
    HLSLPROGRAM
    #pragma target 3.5
    #pragma vertex Vert
    #pragma fragment ReturnSkinVisibilityPosition
    #include "ReturnPhysicalCommon.hlsl"
    struct VisibilityPositionOutput {float4 position:SV_Target0;float4 normal:SV_Target1;};
    VisibilityPositionOutput ReturnSkinVisibilityPosition(V i,bool facing:SV_IsFrontFace) {
     clip(_Skin-.5);clip(_SkinReflectionVisibility-.5);
     clip(.5-_SkinDiffuseReview);clip(.5-_SkinReflectionReview);
     SurfaceData s;InputData d;ReturnSurface(i,facing,s,d);
     VisibilityPositionOutput o;o.position=float4(i.positionWS,1);o.normal=float4(d.normalWS,1-s.smoothness);return o;
    }
    ENDHLSL
   }
   Pass {
    Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"}
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ReturnShadowVertex
   #pragma fragment ReturnDepthFragment
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
   float3 _LightDirection; float3 _LightPosition;
   ReturnDepthOutput ReturnShadowVertex(ReturnDepthInput a) {
    ReturnDepthOutput o;float3 positionWS=TransformObjectToWorld(ReturnWind(a.positionOS.xyz,a.uv,a.colour.a));
    float3 normalWS=ReturnTreeNormal(TransformObjectToWorldNormal(a.normalOS),positionWS);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
     float3 lightDirectionWS=normalize(_LightPosition-positionWS);
    #else
     float3 lightDirectionWS=_LightDirection;
    #endif
    o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,lightDirectionWS)));
    o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;
   }
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex ReturnDepthVertex
   #pragma fragment ReturnDepthFragment
   ReturnDepthOutput ReturnDepthVertex(ReturnDepthInput a) {
    ReturnDepthOutput o;o.positionCS=TransformObjectToHClip(ReturnWind(a.positionOS.xyz,a.uv,a.colour.a));o.uv=TRANSFORM_TEX(a.uv,_BaseMap);return o;
   }
   ENDHLSL
  }
 }
}
