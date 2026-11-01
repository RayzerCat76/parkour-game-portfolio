#ifndef RETURN_RECOVERY_COMMON_INCLUDED
#define RETURN_RECOVERY_COMMON_INCLUDED
float4 _ReturnRecoveryOrigin;
float _ReturnYellowRadius,_ReturnRedRadius,_ReturnBlueRadius;
float ReturnRecoveryDistance(float2 position) {
 float2 offset=position-_ReturnRecoveryOrigin.xz;float distance=length(offset),angle=atan2(offset.y,offset.x);
 float variation=sin(angle*7+distance*.11)*1.05+sin(angle*13-distance*.19)*.45+sin(dot(position,float2(.55,.37)))*.3;
 return max(0,distance+variation*saturate(distance/4));
}
float ReturnRecoveryAmount(float distance,float radius) {
 float width=max(2.4,fwidth(distance)*2);
 return radius<0?0:smoothstep(distance-width,distance+width,radius);
}
#endif
