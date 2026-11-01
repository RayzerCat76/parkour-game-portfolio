Shader "Return/RecoveredSky" {
 SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"} Cull Off ZWrite Off
  Pass {HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "ReturnSkyCommon.hlsl"
   struct A {float4 positionOS:POSITION;};struct V {float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;};
   V vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.direction=a.positionOS.xyz;return o;}
   float _ReturnStreetSky;
   half3 CarvedStreetSky(float3 direction) {
    float3 ray=normalize(direction);float height=smoothstep(-.02,.72,ray.y);
    half3 horizon=pow(half3(.93,.91,.83),2.2),zenith=pow(half3(.51,.73,.78),2.2);
    half3 colour=lerp(horizon,zenith,height);
    float wash=pow(saturate(dot(ray,normalize(float3(-.38,.38,.84)))),24);
    colour+=pow(half3(1,.79,.48),2.2)*wash*.075;
    float yellow=saturate(_ReturnSkyRecovered*3),red=saturate(_ReturnSkyRecovered*3-1),blue=saturate(_ReturnSkyRecovered*3-2);
    half luminance=dot(colour,half3(.2126,.7152,.0722));
    return lerp(_ReturnSkyWhite.rgb,lerp(lerp(luminance*half3(1.10,1.02,.60),luminance*half3(1.12,.83,.73),red),colour,blue),yellow);
   }
   half4 frag(V i):SV_Target{return half4(_ReturnStreetSky>.5?CarvedStreetSky(i.direction):ReturnSkyColour(i.direction),1);}
  ENDHLSL}
 }
}
