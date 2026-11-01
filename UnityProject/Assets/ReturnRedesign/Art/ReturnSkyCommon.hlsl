#ifndef RETURN_SKY_COMMON_INCLUDED
#define RETURN_SKY_COMMON_INCLUDED
TEXTURE2D(_ReturnSkyTexture);SAMPLER(sampler_ReturnSkyTexture);
float4 _ReturnSkyWhite,_ReturnSkyPole,_ReturnSkyProbeDirection;
float _ReturnSkyRecovered,_ReturnSkyEnabled;
float2 ReturnSkyUV(float3 direction) {
 direction=normalize(direction);return float2(atan2(direction.z,direction.x)*.159154943091895+.5,asin(clamp(direction.y,-1,1))*.318309886183791+.5);
}
half3 ReturnSkyColour(float3 direction) {
 float2 uv=ReturnSkyUV(direction);half3 colour=SAMPLE_TEXTURE2D_LOD(_ReturnSkyTexture,sampler_ReturnSkyTexture,uv,0).rgb;
 half3 other=SAMPLE_TEXTURE2D_LOD(_ReturnSkyTexture,sampler_ReturnSkyTexture,float2(1-uv.x,uv.y),0).rgb;
 float seam=1-smoothstep(0,.03,min(uv.x,1-uv.x));colour=lerp(colour,(colour+other)*.5,seam);colour=lerp(colour,_ReturnSkyPole.rgb,smoothstep(.94,1,uv.y));
 float yellow=saturate(_ReturnSkyRecovered*3),red=saturate(_ReturnSkyRecovered*3-1),blue=saturate(_ReturnSkyRecovered*3-2);
 half luminance=dot(colour,half3(.2126,.7152,.0722));half3 gold=luminance*half3(1.10,1.02,.60),rose=luminance*half3(1.12,.83,.73);
 return lerp(_ReturnSkyWhite.rgb,lerp(lerp(gold,rose,red),colour,blue),yellow);
}
#endif
