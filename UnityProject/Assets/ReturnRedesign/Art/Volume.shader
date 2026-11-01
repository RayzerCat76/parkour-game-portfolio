Shader "Return/Volume" {
 Properties { [MainColor] _Color("Colour",Color)=(1,1,1,1) _Glossiness("Softness",Range(0,1))=.25 _Emission("Glow",Color)=(0,0,0,1) }
 SubShader { Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" } LOD 200
 Pass { Name "Forward" Tags { "LightMode"="UniversalForwardOnly" } ZWrite On
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Color; half _Glossiness; half4 _Emission;
 CBUFFER_END
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
 struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
 Varyings vert(Attributes i) { Varyings o; VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o; }
 half4 frag(Varyings i):SV_Target {
 half3 n=normalize(i.normalWS);Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
 half diffuse=saturate(dot(n,light.direction));half3 ambient=SampleSH(n);
 half3 view=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));half spec=pow(saturate(dot(n,normalize(light.direction+view))),lerp(12,80,_Glossiness));
 half3 colour=_Color.rgb*(ambient*.8+light.color*(.08+diffuse*.85)*light.shadowAttenuation)+light.color*spec*_Glossiness*.22+_Emission.rgb;
 #if defined(_ADDITIONAL_LIGHTS)
 uint count=GetAdditionalLightsCount();for(uint index=0;index<count;index++){Light extra=GetAdditionalLight(index,i.positionWS);colour+=_Color.rgb*extra.color*saturate(dot(n,extra.direction))*extra.distanceAttenuation*.6;}
 #endif
 return half4(MixFog(colour,i.fog),1);
 }
 ENDHLSL
 }
 Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ZWrite On ZTest LEqual ColorMask 0
 HLSLPROGRAM
 #pragma vertex shadowVert
 #pragma fragment shadowFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
 float3 _LightDirection;
 struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
 float4 shadowVert(Attributes i):SV_POSITION {float3 p=TransformObjectToWorld(i.positionOS.xyz);float3 n=TransformObjectToWorldNormal(i.normalOS);float4 h=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
 #if UNITY_REVERSED_Z
 h.z=min(h.z,UNITY_NEAR_CLIP_VALUE);
 #else
 h.z=max(h.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return h;}
 half4 shadowFrag():SV_Target{return 0;}
 ENDHLSL
 }
 Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex depthVert
 #pragma fragment depthFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 struct Attributes {float4 positionOS:POSITION;};
 float4 depthVert(Attributes i):SV_POSITION{return TransformObjectToHClip(i.positionOS.xyz);}
 half4 depthFrag(float4 p:SV_POSITION):SV_Target{return p.z;}
 ENDHLSL
 }
 }
}
