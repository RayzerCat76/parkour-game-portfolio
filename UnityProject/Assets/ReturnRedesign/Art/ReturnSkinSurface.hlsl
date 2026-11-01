#ifndef RETURN_SKIN_SURFACE_INCLUDED
#define RETURN_SKIN_SURFACE_INCLUDED
// Analytic material detail attached to the existing MakeHuman UV atlas.
// This is an artist-controlled approximation, not scanned skin or thickness.
float ReturnSkinHash(float2 p) {
 p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);
 return frac((p.x+p.y)*p.x);
}
float3 ReturnSkinNoiseGradient(float2 uv) {
 float2 cell=floor(uv),f=frac(uv),u=f*f*(3-2*f),du=6*f*(1-f);
 float a=ReturnSkinHash(cell),b=ReturnSkinHash(cell+float2(1,0));
 float c=ReturnSkinHash(cell+float2(0,1)),d=ReturnSkinHash(cell+1);
 return float3(lerp(lerp(a,b,u.x),lerp(c,d,u.x),u.y),
               lerp(b-a,d-c,u.y)*du.x,lerp(c-a,d-b,u.x)*du.y);
}
float ReturnSkinEllipse(float2 uv,float2 centre,float2 radius) {
 float2 at=(uv-centre)/radius;return exp(-dot(at,at)*2);
}
void ReturnSkinSurface(float2 uv,out float2 slope,out float roughnessOffset) {
 float footprint=max(length(ddx(uv)),length(ddy(uv)));
 float broad=1-smoothstep(.45,1.25,footprint*92);
 float medium=1-smoothstep(.45,1.25,footprint*530);
 float fine=1-smoothstep(.45,1.25,footprint*1700);
 float3 n0=ReturnSkinNoiseGradient(uv*92+float2(7,19));
 float3 n1=ReturnSkinNoiseGradient(uv*530+float2(31,43));
 float3 n2=ReturnSkinNoiseGradient(uv*1700+float2(61,89));
 // Pits affect tangent-space normals only while the pattern is resolvable.
 slope=-(n0.yz*.014*broad+n1.yz*.04*medium+n2.yz*(2*n2.x)*.10*fine)*_SkinDetail;
 float patch=ReturnSkinNoiseGradient(uv*42+float2(17,31)).x-.5;
 // Forehead/nose regions follow the shared rotated head patch of this atlas.
 float oil=max(ReturnSkinEllipse(uv,float2(.805,.49),float2(.035,.070)),
               ReturnSkinEllipse(uv,float2(.893,.49),float2(.024,.035)));
 roughnessOffset=patch*.10-oil*.22*_SkinOiliness+(1-fine)*_SkinDetail*.025;
}
#endif
