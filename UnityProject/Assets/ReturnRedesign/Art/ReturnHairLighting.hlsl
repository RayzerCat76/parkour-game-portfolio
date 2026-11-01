#ifndef RETURN_HAIR_LIGHTING_INCLUDED
#define RETURN_HAIR_LIGHTING_INCLUDED
// A card-based Kajiya-Kay approximation with separate surface/internal lobes.
// Inspired by Marschner et al., 2003; this is not their full scattering model.
float3 ReturnHairDirection(V i) {
 // The imported card's authored UV V axis supplies a stable, continuous flow.
 // Colour variation is strand pigmentation, not a direction field.
 float3 u=normalize(i.tangentWS.xyz);
 return normalize(cross(normalize(i.normalWS),u))*i.tangentWS.w;
}
half ReturnHairLobe(float3 tangent,float3 normal,float3 halfDirection,float shift,float power) {
 float3 shifted=normalize(tangent+normal*shift);
 float along=dot(shifted,halfDirection);
 return pow(sqrt(saturate(1-along*along)),power);
}
half3 ReturnHairLight(Light light,InputData d,SurfaceData s,float3 tangent,float3 cardNormal) {
 float3 halfDirection=SafeNormalize(light.direction+d.viewDirectionWS);
 half normalLight=saturate(dot(d.normalWS,light.direction));
 half cylinder=sqrt(saturate(1-pow(dot(tangent,light.direction),2)));
 // Resolve the broad sheen on the smooth card surface, independently of the
 // fine albedo-derived relief normal used for diffuse strand definition.
 half primary=ReturnHairLobe(tangent,cardNormal,halfDirection,-.12,72);
 half secondary=ReturnHairLobe(tangent,cardNormal,halfDirection,.22,20);
 half3 tint=sqrt(max(s.albedo,half3(.001,.001,.001)));
 half3 diffuse=s.albedo*cylinder*(.18+.64*normalLight);
 half3 sheen=(primary*.045+secondary*tint*.024)*saturate(.25+.75*normalLight);
 return (diffuse+sheen)*light.color*(light.distanceAttenuation*light.shadowAttenuation);
}
half3 ReturnHairEnvironment(InputData d,SurfaceData s,float3 tangent,float3 cardNormal,out half reflectance) {
 // Approximate a fibre-oriented environment response with the existing room
 // probe. This is a filtered card approximation, not full fibre scattering.
 float3 projected=d.viewDirectionWS-tangent*dot(d.viewDirectionWS,tangent);
 float3 fibreNormal=dot(projected,projected)>1e-5?normalize(projected):cardNormal;
 float3 normal=SafeNormalize(lerp(cardNormal,fibreNormal,.7));
 float3 reflection=reflect(-d.viewDirectionWS,normal);
 half NoV=saturate(dot(fibreNormal,d.viewDirectionWS));
 half grazing=1-NoV;
 reflectance=.0465h+.9535h*grazing*grazing*grazing*grazing*grazing;
 half roughness=clamp(1-s.smoothness,.35h,.65h);
 // Broaden the reflection along the authored strand flow so window/ceiling
 // light reads as soft sheen rather than a smooth plastic card highlight.
 half3 radiance=GlossyEnvironmentReflection(reflection,d.positionWS,roughness,1,d.normalizedScreenSpaceUV)*.5h;
 radiance+=GlossyEnvironmentReflection(SafeNormalize(reflection+tangent*.35),d.positionWS,roughness,1,d.normalizedScreenSpaceUV)*.25h;
 radiance+=GlossyEnvironmentReflection(SafeNormalize(reflection-tangent*.35),d.positionWS,roughness,1,d.normalizedScreenSpaceUV)*.25h;
 return radiance;
}
half3 ReturnHairLighting(V i,InputData d,SurfaceData s,Light main) {
 float3 tangent=ReturnHairDirection(i);
 float3 cardNormal=normalize(i.normalWS);
 if(dot(cardNormal,d.normalWS)<0)cardNormal=-cardNormal;
 half reflectance;half3 environment=ReturnHairEnvironment(d,s,tangent,cardNormal,reflectance);
 AmbientOcclusionFactor ao=CreateAmbientOcclusionFactor(d,s);
 half3 result=(s.albedo*d.bakedGI*(1-reflectance)+environment*reflectance)*ao.indirectAmbientOcclusion+ReturnHairLight(main,d,s,tangent,cardNormal)+s.emission;
 #ifdef _ADDITIONAL_LIGHTS
 uint count=GetAdditionalLightsCount();
 LIGHT_LOOP_BEGIN(count)
  Light light=GetAdditionalLight(lightIndex,d.positionWS,d.shadowMask);
  result+=ReturnHairLight(light,d,s,tangent,cardNormal);
 LIGHT_LOOP_END
 #endif
 return result;
}
#endif
