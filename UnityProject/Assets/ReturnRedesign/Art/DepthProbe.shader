Shader "Return/DepthProbe" {
 SubShader {Tags {"RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off ZTest Always
  Pass {HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   float4 _ReturnDepthZParams;
   float4 frag(Varyings i):SV_Target{int sampleIndex=clamp((int)i.positionCS.x,0,2);float2 uv=float2(.5,.25+sampleIndex*.3);float raw=SAMPLE_TEXTURE2D_X_LOD(_BlitTexture,sampler_PointClamp,uv,0).r;return float4(raw,LinearEyeDepth(raw,_ReturnDepthZParams),0,1);}
  ENDHLSL}
 }
}
