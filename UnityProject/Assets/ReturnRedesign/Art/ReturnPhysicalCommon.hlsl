   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
   TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
   TEXTURE2D(_IndirectMap); SAMPLER(sampler_IndirectMap);
   #include "ReturnRecoveryCommon.hlsl"
   float4 _ReturnCityRecovery; // centre XZ, progress, supported-city radius (zero disables)
   #include "ReturnSkinSurface.hlsl"
   float ReturnAsphaltHash(float2 p) {p=frac(p*float2(.1031,.1030));p+=dot(p,p.yx+33.33);return frac((p.x+p.y)*p.x);}
   float ReturnAsphaltNoise(float2 p) {
    float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(ReturnAsphaltHash(cell),ReturnAsphaltHash(cell+float2(1,0)),f.x),lerp(ReturnAsphaltHash(cell+float2(0,1)),ReturnAsphaltHash(cell+1),f.x),f.y);
   }
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float2 indirectUV:TEXCOORD1;half4 colour:COLOR;};
   struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;float2 indirectUV:TEXCOORD5;};
   V Vert(A a){V o;a.positionOS.xyz=ReturnWind(a.positionOS.xyz,a.uv,a.colour.a);VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(a.normalOS,a.tangentOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=ReturnTreeNormal(n.normalWS,p.positionWS);o.tangentWS=half4(ReturnTreeTangent(n.tangentWS,p.positionWS),a.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.indirectUV=a.indirectUV;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   float2 ReturnCeilingJointIntegral(float2 position) {
    const float width=.006/.6;
    return floor(position)*width+min(frac(position),width);
   }
   void ReturnSurface(V i,bool facing,out SurfaceData s,out InputData d) {
   half4 sample=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;ReturnCutout(i.uv,sample.a);
   half underside=saturate(.5-.5*(facing?i.normalWS.y:-i.normalWS.y));sample.rgb=lerp(sample.rgb,_BackColour.rgb,underside*_BackColour.a);
     half3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
     if(_Hair>.5){
      // Derive relief from the existing unpigmented strand atlas. Fixed texel
      // offsets and mip filtering keep the tiny fibres stable as the head moves.
      float2 texel=_BaseMap_TexelSize.xy;
      half h=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb,half3(.2126,.7152,.0722));
      half hx=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(texel.x,0)).rgb,half3(.2126,.7152,.0722));
      half hy=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(0,texel.y)).rgb,half3(.2126,.7152,.0722));
      normalTS=normalize(normalTS+half3((h-hx)*1.1,(h-hy)*1.1,0));
     }
    if(_AlbedoRelief>.001){half h=dot(sample.rgb,half3(.2126,.7152,.0722));half hx=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(_BaseMap_TexelSize.x,0)).rgb*_BaseColor.rgb,half3(.2126,.7152,.0722));half hy=dot(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(0,_BaseMap_TexelSize.y)).rgb*_BaseColor.rgb,half3(.2126,.7152,.0722));normalTS=normalize(normalTS+half3((h-hx)*_AlbedoRelief*3,(h-hy)*_AlbedoRelief*3,0));}
    float skinRoughness=0;
    if(_Skin>.5){float2 slope;ReturnSkinSurface(i.uv,slope,skinRoughness);normalTS=normalize(half3(normalTS.xy+slope,normalTS.z));}
    half3 n=normalize(i.normalWS);half3 t=normalize(i.tangentWS.xyz);half3 b=cross(n,t)*i.tangentWS.w;
    if(dot(t,t)>.01)n=normalize(mul(normalTS,half3x3(t,b,n)));
     if(!facing)n=-n;
     // Small cards represent a leafy canopy; bias its lighting toward the sky
     // without changing silhouettes, opacity or the other physical materials.
     n=normalize(lerp(n,half3(0,1,0),_FoliageNormalUp));
    // Fine roughness and normal variation catches light on bark, cloth, stone and paint.
    float2 grainPhase=float2(i.positionWS.x*219+sin(i.positionWS.z*81),i.positionWS.y*157+i.positionWS.z*173);
    float2 grainFootprint=fwidth(grainPhase);float resolvedGrain=saturate(1-max(grainFootprint.x,grainFootprint.y)*.318309886);
    float grain=_Skin>.5?0:sin(grainPhase.x)*sin(grainPhase.y)*resolvedGrain*resolvedGrain;
    n=normalize(n+half3(grain,grain*.2,-grain)*_SurfaceDetail*.05);
    half3 colour=sample.rgb*(1+grain*_SurfaceDetail*.035);
    if(_CeilingTiles>.5) {
     float2 tile=(i.positionWS.xz-_CeilingOrigin.xz)/.6+.005;
     float2 radius=max(fwidth(tile)*.5,.0001);
     float2 joints=saturate((ReturnCeilingJointIntegral(tile+radius)-ReturnCeilingJointIntegral(tile-radius))/(2*radius));
     half joint=joints.x+joints.y-joints.x*joints.y;
     colour*=lerp(1,.74h,joint);
    }
     half streetSmoothness=.065h,facadeWeatherRoughness=0,paintSmoothness=.12h;
    if(_StreetSurface>.5) {
     float2 road=i.positionWS.xz;float2 footprint=fwidth(road*180);
     float resolved=1-smoothstep(.3,.9,max(footprint.x,footprint.y));
      float aggregate=ReturnAsphaltNoise(road*180)*.65+ReturnAsphaltNoise(road*77)*.35-.5;
      float wear=ReturnAsphaltNoise(road*1.6),patch=ReturnAsphaltNoise(road*.22);
      float2 mediumFootprint=fwidth(road*24);float mediumResolved=1-smoothstep(.6,1.5,max(mediumFootprint.x,mediumFootprint.y));
      float grit=ReturnAsphaltNoise(road*24)*.67+ReturnAsphaltNoise(road*11)*.33-.5;
      float repairNoise=ReturnAsphaltNoise(road*.42+float2(31.2,7.1)),repairWidth=max(fwidth(repairNoise)*1.3,.025);
      float repair=smoothstep(.70-repairWidth,.70+repairWidth,repairNoise);
      colour*=(.78+.22*patch+.10*wear)*(1+grit*.4*mediumResolved+aggregate*.26*resolved)*(1-repair*.17);
      n=normalize(n+half3(aggregate,0,ReturnAsphaltNoise(road*180+19.7)-.5)*(.035*resolved)+half3(grit,0,ReturnAsphaltNoise(road*24+7.3)-.5)*(.045*mediumResolved));
      streetSmoothness=.025h+.04h*wear+.012h*repair+aggregate*.025h*resolved;
     }
     if(_StreetPaint>.5) {
      float2 offset=i.positionWS.xz-_ReturnCityRecovery.xy;
      float2 road=lerp(offset,float2(-offset.y,offset.x),_StreetPaintAxis),footprint=fwidth(road*24);
      float resolved=1-smoothstep(.6,1.5,max(footprint.x,footprint.y));
      float crossing=1-step(6,abs(road.y)),stripeX=(frac(road.x/.85+.5)-.5)*.85;
      float sideEdge=lerp(.05-abs(road.x),.24-abs(stripeX),crossing);
      float endEdge=lerp(.7-abs((frac(road.y/4.1+.5)-.5)*4.1),.85-abs(abs(road.y)-5),crossing);
      float edgeDistance=max(0,min(sideEdge,endEdge));
      float edgeWear=1-smoothstep(.006,.026+fwidth(edgeDistance),edgeDistance);
      float chips=smoothstep(.76,.88,ReturnAsphaltNoise(road*24+13.7));
      float tyreDistance=min(abs(abs(road.x)-.95),abs(abs(road.x)-2.4));
      float tyreMask=1-smoothstep(.06,.16+max(fwidth(road.x),.025),tyreDistance);
      float abrasion=tyreMask*(.025+.055*ReturnAsphaltNoise(float2(road.x*2,road.y*.7)));
      float coverage=saturate(.985-resolved*edgeWear*chips*.55-abrasion);
      colour=lerp(half3(.17,.175,.18),colour,coverage);
      paintSmoothness=.06h+.06h*coverage;
     }
     if(_CityFacade>.5) {
     // Keep the finish on its building throughout the dimensional reunion.
     float3 wallPosition=TransformWorldToObject(i.positionWS);
     float2 wall=float2(wallPosition.x+wallPosition.z,wallPosition.y),footprint=fwidth(wall*26);
     float2 finish=wall+float2(ReturnAsphaltHash(float2(_CityFacadeSeed,17.1)),ReturnAsphaltHash(float2(_CityFacadeSeed,53.7)))*23;
      float resolved=1-smoothstep(.6,1.5,max(footprint.x,footprint.y));
     float plasterGrain=(ReturnAsphaltNoise(finish*26)*.6+ReturnAsphaltNoise(finish*43+8.3)*.4-.5)*resolved;
     float mottling=ReturnAsphaltNoise(finish*.38),trowel=ReturnAsphaltNoise(finish*2.4)-.5;
     float rain=.35+.65*ReturnAsphaltNoise(float2(finish.x*3.2,finish.y*.11));
      float ground=1-smoothstep(.05,.95,wall.y),eaves=smoothstep(max(0,_CityFacadeHeight-1.25),max(.1,_CityFacadeHeight-.15),wall.y);
      colour*=(.89+.16*mottling+.07*trowel+.07*plasterGrain)*(1-rain*(ground*.16+eaves*.07));
      half3 horizontal=half3(n.z,0,-n.x),vertical=half3(0,1,0)-n*n.y;
     n=normalize(n+horizontal*(plasterGrain*.055)+vertical*((ReturnAsphaltNoise(finish*26+9.7)-.5)*.035*resolved));
      facadeWeatherRoughness=.025h+.03h*rain+.01h*plasterGrain;
    }
    float dist=ReturnRecoveryDistance(i.positionWS.xz);
    float radius=_RecoveryChannel<1.5?_ReturnYellowRadius:_RecoveryChannel<2.5?_ReturnRedRadius:_ReturnBlueRadius;
    float restored=_RecoveryChannel<.5?1:ReturnRecoveryAmount(dist,radius);
    // A feathered spatial gradient brings the city back during the leftward camera sweep.
    float2 cityOffset=i.positionWS.xz-_ReturnCityRecovery.xy;
    float cityMask=saturate(_ReturnCityRecovery.w)*step(max(abs(cityOffset.x),abs(cityOffset.y)),max(39.5,_ReturnCityRecovery.w));
    float cityProgress=saturate(_ReturnCityRecovery.z);
    float cityFeather=4*cityProgress*(1-cityProgress);
    float cityColour=smoothstep(0,1,cityProgress+(22*(1-cityProgress)-cityOffset.x)/18*cityFeather);
    restored*=lerp(1,cityColour,cityMask);
    colour=lerp(dot(colour,half3(.2126,.7152,.0722)).xxx,colour,restored);
     half smoothness=lerp(_Smoothness,1-SAMPLE_TEXTURE2D(_RoughnessMap,sampler_RoughnessMap,i.uv).r,saturate(_UseRoughness));
      if(_Skin>.5)smoothness=1-clamp(1-smoothness+skinRoughness,.28,.78);
      // Filter unresolved normal variation in hospital skin's specular lobe.
      // The bounded kernel stays within the existing environment lookup range.
      if(_Skin>.5&&_SkinDFGEnabled>.5)smoothness=GeometricNormalFiltering(smoothness,n,.25,.35);
      if(_StreetSurface>.5)smoothness=streetSmoothness;
      if(_StreetPaint>.5)smoothness=paintSmoothness;
      if(_CityFacade>.5)smoothness=saturate(smoothness-facadeWeatherRoughness);
     half damp=_ShoreSoil*saturate((.15-i.positionWS.y)/.23);colour*=lerp(1,.63,damp);smoothness=lerp(smoothness,max(smoothness,.55),damp);
    s=(SurfaceData)0;s.albedo=colour;s.specular=.028h;s.alpha=1;s.metallic=_Metallic;s.smoothness=saturate(smoothness-grain*_SurfaceDetail*.05);s.normalTS=normalTS;s.occlusion=SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,i.uv).r;s.emission=_EmissionColor.rgb*restored;
    d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=n;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.fogCoord=i.fog;d.bakedGI=SampleSH(n);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
    half3 indirect=SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV).rgb;
    if(_IndirectFilter>.001) {
     float2 texel=_IndirectMap_TexelSize.xy*2;half3 filtered=indirect*.25h;
     filtered+=(SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV+float2(texel.x,0)).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV-float2(texel.x,0)).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV+float2(0,texel.y)).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV-float2(0,texel.y)).rgb)*.125h;
     filtered+=(SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV+texel).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV-texel).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV+float2(texel.x,-texel.y)).rgb+SAMPLE_TEXTURE2D(_IndirectMap,sampler_IndirectMap,i.indirectUV+float2(-texel.x,texel.y)).rgb)*.0625h;
     indirect=lerp(indirect,filtered,saturate(_IndirectFilter));
    }
    d.bakedGI=lerp(d.bakedGI,indirect*_IndirectStrength,saturate(_UseIndirect));
    if(_CeilingTiles>.5)d.bakedGI+=_CeilingIrradiance.rgb;
   }
