Shader "Return/Water" {
 Properties { [MainColor] _Color("Water",Color)=(.98,.98,.97,1) _ReflectionTex("Planar reflection",2D)="white"{} [HideInInspector] _PlanarReflectionValid("Current perspective reflection",Float)=0 [HideInInspector] _ReflectionUVScaleBias("Reflection UV",Vector)=(.5,.5,.5,.5) [HideInInspector] _ReflectionPlaneY("Reflection plane",Float)=-.11 [HideInInspector] _ReflectionSky("Reflection sky",Vector)=(.98,.98,.97,1)
 [HideInInspector] _ReturnWave0("Wave 0",Vector)=(0,0,0,0) [HideInInspector] _ReturnWave1("Wave 1",Vector)=(0,0,0,0) [HideInInspector] _ReturnWave2("Wave 2",Vector)=(0,0,0,0) [HideInInspector] _ReturnWave3("Wave 3",Vector)=(0,0,0,0) [HideInInspector] _ReturnWaveProbePoint("Wave probe",Vector)=(0,0,0,0) }
 SubShader { Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
 Pass { Tags { "LightMode"="UniversalForwardOnly" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 #include "WaterField.hlsl"
  #include "ReturnSkyCommon.hlsl"
  #include "ReturnRecoveryCommon.hlsl"
 TEXTURE2D(_ReflectionTex); SAMPLER(sampler_ReflectionTex);
  float4 _ReturnLeafPosition,_ReturnBodyPosition;
  float4 _ReturnWaterBodyFocus,_ReturnWaterBodyMotion,_ReturnWaterLeafMotion;
  float4 _ReturnWaterHandLeft,_ReturnWaterHandRight;
  float4 _ReturnWaterPelvis,_ReturnWaterKneeLeft,_ReturnWaterKneeRight,_ReturnWaterFootLeft,_ReturnWaterFootRight;
 struct Attributes {float4 positionOS:POSITION;};
 struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half fog:TEXCOORD2;};
 Varyings vert(Attributes i) {Varyings o;
 float2 worldXZ=TransformObjectToWorld(i.positionOS.xyz).xz;i.positionOS.y+=ReturnWaveHeight(worldXZ,_ReturnWaterTime);
 VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
 // Suppress wave frequencies that a pixel's footprint cannot resolve.
 float FilteredWave(float phase) {float footprint=fwidth(phase);return sin(phase)*exp(-.5*footprint*footprint);}
 float3 HandDisturbance(float2 p,float4 hand,float time) {
  float2 offset=p-hand.xy;float distance=length(offset);
  float envelope=exp(-distance*3.4)*hand.z*hand.w;
  float pulse=FilteredWave(distance*18-time*3.2);
  return float3(offset/max(distance,.01)*pulse*envelope*.055,abs(pulse)*envelope*.09);
 }
  // Approximate the shorter refracted optical path through shallow liquid.
  // The unrefracted camera ray over-attenuates limbs at low viewing angles.
  float SubmergedTransmission(Varyings i,float cosineAir,float fresnel) {
   float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
   float depth=SampleSceneDepth(uv),validDepth;
   #if UNITY_REVERSED_Z
    validDepth=step(.000001,depth);
   #else
    validDepth=1-step(.999999,depth);
    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
   #endif
   float3 submerged=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
   float verticalDepth=max(0,i.positionWS.y-submerged.y);
   float cosineWater=sqrt(max(.001,1-(1-cosineAir*cosineAir)/(1.333*1.333)));
   float opticalDepth=verticalDepth/cosineWater;
   // Surface reflection remains visible; scattering obscures deeper geometry.
   return .9*(1-fresnel)*exp(-1.8*opticalDepth)*validDepth;
  }
 float3 FootDisturbance(float3 surface,float4 foot,float time) {
  float contact=exp(-abs(surface.y-foot.y)*5)*saturate(_ReturnWaterBodyMotion.w);
  return HandDisturbance(surface.xz,float4(foot.xz,saturate(foot.w/1.8),contact),time);
 }
 half4 frag(Varyings i):SV_Target {
 float2 p=i.positionWS.xz;float t=_ReturnWaterTime;
 float leafWidth=max(.1,_ReturnWaterLeafMotion.z),leafLength=max(.1,_ReturnWaterLeafMotion.w);
 float2 bodyOffset=p-_ReturnWaterBodyFocus.xz,leafWorldOffset=p-_ReturnLeafPosition.xz;
 float2 leafScale=float2(1,leafWidth/leafLength),leafOffset=leafWorldOffset*leafScale;
 float d=length(bodyOffset),leafDistance=length(leafOffset);
 float bodySpeed=length(_ReturnWaterBodyMotion.xy),leafSpeed=length(_ReturnWaterLeafMotion.xy);
 float bodyContact=saturate(_ReturnWaterBodyMotion.w),bodyStrength=saturate(bodySpeed/2.6),leafStrength=saturate(leafSpeed/1.22);
 float2 bodyDirection=_ReturnWaterBodyMotion.xy/max(bodySpeed,.001),leafDirection=_ReturnWaterLeafMotion.xy/max(leafSpeed,.001);
 float bodyRear=max(0,-dot(bodyOffset,bodyDirection)),leafRear=max(0,-dot(leafWorldOffset,leafDirection));
 float2 bodyAcross=bodyOffset-bodyDirection*dot(bodyOffset,bodyDirection),leafAcross=leafWorldOffset-leafDirection*dot(leafWorldOffset,leafDirection);
 float bodyEnvelope=exp(-d*1.4)*bodyContact*(.28+.72*bodyStrength);
 float leafEnvelope=exp(-max(0,leafDistance-leafWidth)*2.2)*smoothstep(leafWidth*.92,leafWidth*1.06,leafDistance)*leafStrength;
 float bodyTrailEnvelope=smoothstep(.05,.3,bodyRear)*exp(-bodyRear*1.1)*exp(-abs(length(bodyAcross)-bodyRear*.34)*7)*bodyContact*bodyStrength;
 float leafTrailEnvelope=smoothstep(.15,.5,leafRear)*exp(-leafRear*.7)*exp(-abs(length(leafAcross)-leafWidth-leafRear*.22)*5)*leafStrength;
 float bodyPulse=FilteredWave(d*14-t*3.2),leafPulse=FilteredWave((leafDistance-leafWidth)*13-t*3.6);
 float bodyTrailPulse=FilteredWave((length(bodyAcross)-bodyRear*.34)*14-t*3.2),leafTrailPulse=FilteredWave((length(leafAcross)-leafWidth-leafRear*.22)*12-t*3.6);
 float wake=leafPulse*leafEnvelope*.055,ripple=bodyPulse*bodyEnvelope*.028;
 float bend=sin(dot(p,float2(.11,.073))+t*.13)*.6;
 float2 slopes=float2(.970,.243)*FilteredWave(dot(p,float2(1.7,.43))-t*.8+bend)*.026
  +float2(-.316,.949)*FilteredWave(dot(p,float2(-.7,2.1))-t*1.1-bend)*.014
  +float2(.932,.364)*FilteredWave(dot(p,float2(4.1,1.6))-t*1.6+bend)*.007
  +float2(-.412,.911)*FilteredWave(dot(p,float2(-2.4,5.3))-t*2.1-bend)*.004;
 slopes+=leafOffset*leafScale/max(leafDistance,.01)*wake+bodyOffset/max(d,.01)*ripple;
 slopes+=bodyAcross/max(length(bodyAcross),.01)*bodyTrailPulse*bodyTrailEnvelope*.04+leafAcross/max(length(leafAcross),.01)*leafTrailPulse*leafTrailEnvelope*.05;
  float3 leftHand=HandDisturbance(p,_ReturnWaterHandLeft,t),rightHand=HandDisturbance(p,_ReturnWaterHandRight,t);
 float3 leftFoot=FootDisturbance(i.positionWS,_ReturnWaterFootLeft,t),rightFoot=FootDisturbance(i.positionWS,_ReturnWaterFootRight,t);
 slopes+=leftHand.xy+rightHand.xy+leftFoot.xy+rightFoot.xy;
 slopes-=ReturnWaveGradient(p,t,ddx(p),ddy(p));
 half3 n=normalize(half3(slopes.x,1,slopes.y));
 Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
  float recoveryDistance=ReturnRecoveryDistance(p);
  float yellow=ReturnRecoveryAmount(recoveryDistance,_ReturnYellowRadius);
  float red=ReturnRecoveryAmount(recoveryDistance,_ReturnRedRadius);
  float blue=ReturnRecoveryAmount(recoveryDistance,_ReturnBlueRadius);
  // Restore neutral depth/scattering beneath the staged sky instead of
  // painting the entire liquid yellow or red.
  half3 liquid=lerp(_Color.rgb,half3(.31,.28,.20),yellow);liquid=lerp(liquid,half3(.12,.13,.12),red);liquid=lerp(liquid,half3(.03,.11,.20),blue);
 float4 reflectedPosition=mul(_ReflectionViewProjection,float4(i.positionWS.x,_ReflectionPlaneY,i.positionWS.z,1));
 float2 uv=reflectedPosition.xy/max(reflectedPosition.w,.0001)*_ReflectionUVScaleBias.xy+_ReflectionUVScaleBias.zw+n.xz*.011;
 float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
 half reflectionCoverage=smoothstep(0,.035,edge)*step(.0001,reflectedPosition.w)*_PlanarReflectionValid;
 half3 skyReflection=_ReturnSkyEnabled>.5?ReturnSkyColour(reflect(-view,n)):_ReflectionSky.rgb;
 half3 reflection=lerp(skyReflection,SAMPLE_TEXTURE2D(_ReflectionTex,sampler_ReflectionTex,saturate(uv)).rgb,reflectionCoverage);
 half fresnel=.02+.98*pow(1-saturate(dot(n,view)),5);
 float3 normalDx=ddx(n),normalDy=ddy(n);float variance=.5*(dot(normalDx,normalDx)+dot(normalDy,normalDy));
 // GGX dielectric reflection: wave variance broadens the lobe without adding energy.
 float alphaSquared=min(1,.0104167+variance);
 float noV=max(.001,saturate(dot(n,view))),noL=saturate(dot(n,light.direction));
 float3 halfVector=normalize(light.direction+view);
 float noH=saturate(dot(n,halfVector)),voH=saturate(dot(view,halfVector));
 float denominator=noH*noH*(alphaSquared-1)+1;
 float distribution=alphaSquared/max(.000001,3.14159265*denominator*denominator);
 float visibilityV=2*noV/(noV+sqrt(alphaSquared+(1-alphaSquared)*noV*noV));
 float visibilityL=2*noL/max(.001,noL+sqrt(alphaSquared+(1-alphaSquared)*noL*noL));
 float sunFresnel=.02+.98*pow(1-voH,5);
 half spec=distribution*visibilityV*visibilityL*sunFresnel/(4*noV)*light.shadowAttenuation;
 // Intersect the moving water with the authored submerged bank, rather than a circular decal.
 float2 bank=p-float2(0,26);float angle=atan2(bank.y,bank.x);
 float coast=3.66+.17*sin(3*angle+.5)+.10*sin(5*angle-.9)+.04*sin(9*angle+1.4);
 float bankDepth=saturate(max(0,length(bank)-coast)/.55);bankDepth=bankDepth*bankDepth*(3-2*bankDepth);
 float shoreHeight=-.075-.38*bankDepth,shoreline=abs(shoreHeight-i.positionWS.y);
 half foam=saturate(1-shoreline/.012)*step(coast,length(bank))*saturate(.38+.62*FilteredWave(p.x*3.7+p.y*2.9+t*.7));
 half3 c=liquid*(.82+.18*saturate(dot(n,light.direction)))*lerp(.65,1,light.shadowAttenuation);
 c=lerp(c,reflection,fresnel)+spec*light.color;c=lerp(c,half3(.96,.97,.95),foam*.55);
  // Quiet tonal contours keep the white liquid readable before blue returns.
  float swellPhase=dot(p,float2(.43,.17))+t*.22+.35*FilteredWave(dot(p,float2(.11,-.23))+t*.09);
  float swell=pow(saturate(.5+.5*FilteredWave(swellPhase)),10);
  float swellBreak=.35+.65*saturate(.5+.5*FilteredWave(dot(p,float2(.19,-.31))-t*.08));
  float bankDistance=max(0,length(bank)-coast);
  float shoreRipple=pow(saturate(.5+.5*FilteredWave(bankDistance*7-t*.85)),6)*exp(-bankDistance*1.6)*step(coast,length(bank));
  c*=1-.065*swell*swellBreak-.055*shoreRipple;
 // Local scattering makes the contact waves readable against the original white liquid.
 float contactShade=abs(bodyPulse)*bodyEnvelope*.05+abs(leafPulse)*leafEnvelope*.045+abs(bodyTrailPulse)*bodyTrailEnvelope*.05+abs(leafTrailPulse)*leafTrailEnvelope*.045;
 c*=1-min(contactShade+leftHand.z+rightHand.z+leftFoot.z+rightFoot.z,.18);
  // Soft, broken highlights mark the ordered fronts among the surface waves.
  float crestWidth=max(.8,fwidth(recoveryDistance)*2),broken=.35+.65*saturate(.5+FilteredWave(dot(p,float2(.73,.51))+t*.4)*.5);
  float3 crests=exp(-abs(recoveryDistance-float3(_ReturnYellowRadius,_ReturnRedRadius,_ReturnBlueRadius))/crestWidth)*step(0,float3(_ReturnYellowRadius,_ReturnRedRadius,_ReturnBlueRadius));
  c+=(crests.x*half3(.65,.39,.06)+crests.y*half3(.46,.09,.03)+crests.z*half3(.03,.2,.36))*(.12*broken);
 // Blend the distant surface into the same panorama ray before the far clip.
 // The liquid keeps its local waves, recovery fronts and reflections nearby.
 half3 fogged=MixFog(c,i.fog);
 if(_ReturnSkyEnabled>.5){float range=length(i.positionWS-_WorldSpaceCameraPos);float haze=smoothstep(_ProjectionParams.z*.55,_ProjectionParams.z*.9,range);fogged=lerp(fogged,ReturnSkyColour(-view),haze);}
   // Shallow foot contact remains visible while stepping from the leaf to the bank.
   if(bodyContact<.001)return half4(fogged,1);
   return half4(fogged,1-SubmergedTransmission(i,saturate(dot(n,view)),fresnel)*bodyContact);}
 ENDHLSL
 }
 Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex depthVert
 #pragma fragment depthFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "WaterField.hlsl"
 struct Attributes {float4 positionOS:POSITION;};
 float4 depthVert(Attributes i):SV_POSITION{float2 p=TransformObjectToWorld(i.positionOS.xyz).xz;i.positionOS.y+=ReturnWaveHeight(p,_ReturnWaterTime);return TransformObjectToHClip(i.positionOS.xyz);}
 half4 depthFrag(float4 p:SV_POSITION):SV_Target{return p.z;}
 ENDHLSL
 }
 // Explicitly requested by the ordinary-input renderer check; never drawn by URP.
 Pass { Name "ReflectionSkyProbe" Tags { "LightMode"="ReturnReflectionSkyProbe" } ZWrite Off ZTest Always Cull Off
 HLSLPROGRAM
 #pragma vertex probeVert
 #pragma fragment probeFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "WaterField.hlsl"
 #include "ReturnSkyCommon.hlsl"
 float4 probeVert(float4 position:POSITION):SV_POSITION{return TransformObjectToHClip(position.xyz);}
 float4 probeFrag():SV_Target{return float4(_ReturnSkyEnabled>.5?ReturnSkyColour(_ReturnSkyProbeDirection.xyz):_ReflectionSky.rgb,1);}
 ENDHLSL
 }
 Pass { Name "ReflectionCaptureProbe" Tags { "LightMode"="ReturnReflectionCaptureProbe" } ZWrite Off ZTest Always Cull Off
 HLSLPROGRAM
 #pragma vertex probeVert
 #pragma fragment probeFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_ReflectionTex);SAMPLER(sampler_ReflectionTex);float4 _ReturnReflectionProbeUV;
 float4 probeVert(float4 position:POSITION):SV_POSITION{return TransformObjectToHClip(position.xyz);}
 float4 probeFrag():SV_Target{return SAMPLE_TEXTURE2D_LOD(_ReflectionTex,sampler_ReflectionTex,_ReturnReflectionProbeUV.xy,0);}
 ENDHLSL
 }
 Pass { Name "WaveFieldProbe" Tags { "LightMode"="ReturnWaveFieldProbe" } ZWrite Off ZTest Always Cull Off
 HLSLPROGRAM
 #pragma vertex probeVert
 #pragma fragment probeFrag
 #include "UnityCG.cginc"
 #include "WaterField.hlsl"
 float4 probeVert(float4 position:POSITION):SV_POSITION{return UnityObjectToClipPos(position);}
 float4 probeFrag():SV_Target{float2 p=_ReturnWaveProbePoint.xy;float t=_ReturnWaveProbePoint.z;return float4(_ReflectionPlaneY+ReturnWaveHeight(p,t),ReturnWaveGradient(p,t,0,0),1);}
 ENDHLSL
 }
 }
}
