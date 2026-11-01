#ifndef RETURN_WATER_FIELD_INCLUDED
#define RETURN_WATER_FIELD_INCLUDED
CBUFFER_START(UnityPerMaterial)
half4 _Color,_ReflectionSky;
float4x4 _ReflectionViewProjection;float4 _ReflectionUVScaleBias;float _ReflectionPlaneY,_PlanarReflectionValid;
float4 _ReturnWave0,_ReturnWave1,_ReturnWave2,_ReturnWave3,_ReturnWaveProbePoint;
CBUFFER_END
float _ReturnWaterTime;
float4 ReturnWave(int index){if(index==0)return _ReturnWave0;if(index==1)return _ReturnWave1;if(index==2)return _ReturnWave2;return _ReturnWave3;}
float ReturnWaveHeight(float2 p,float t) {
 float bend=.35*sin(dot(p,float2(.037,.051))-t*.07),height=0;
 [unroll]for(int k=0;k<4;k++){float4 wave=ReturnWave(k);float sign=(k==0||k==2)?1:-1;float phase=dot(p,wave.xy)-t*wave.z+.4+k*1.7+sign*bend;height+=wave.w*sin(phase);}
 return height;
}
float2 ReturnWaveGradient(float2 p,float t,float2 pixelDx,float2 pixelDy) {
 float bendPhase=dot(p,float2(.037,.051))-t*.07,bend=.35*sin(bendPhase);
 float2 bendGradient=.35*cos(bendPhase)*float2(.037,.051),gradient=0;
 [unroll]for(int k=0;k<4;k++) {
  float4 wave=ReturnWave(k);float sign=(k==0||k==2)?1:-1;float2 direction=wave.xy+sign*bendGradient;
  float phase=dot(p,wave.xy)-t*wave.z+.4+k*1.7+sign*bend;
  float footprint=abs(dot(direction,pixelDx))+abs(dot(direction,pixelDy));
  gradient+=wave.w*cos(phase)*exp(-.5*footprint*footprint)*direction;
 }
 return gradient;
}
#endif
