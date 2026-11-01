Shader "Return/SkinDiffusion" {
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest Always
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
  TEXTURE2D_X(_ReturnSkinOriginal);TEXTURE2D_X(_ReturnSkinBlurred);TEXTURE2D_X_FLOAT(_ReturnSkinDepth);TEXTURE2D_X(_ReturnSkinComposite);
  TEXTURE2D_X(_ReturnSkinAlbedoRoot);
  TEXTURE2D_X(_ReturnSkinEnvironment);TEXTURE2D_X(_ReturnSkinNormals);
  TEXTURE2D_X(_ReturnSkinVisibilityPositions);
  TEXTURE2D_X(_ReturnSkinVisibilityRawNormals);
  TEXTURE2D_X(_ReturnSkinReflectionBefore);
  TEXTURE2D_X_FLOAT(_ReturnSkinSurfacePositions);
  float4 _ReturnSkinProjection,_ReturnSkinZParams;
  float _ReturnSkinCompositeReviewBypass;
  float Eye(float2 uv){return LinearEyeDepth(SAMPLE_TEXTURE2D_X(_ReturnSkinDepth,sampler_PointClamp,uv).r,_ReturnSkinZParams);}
  half3 SkinAlbedoRoot(float2 uv){return max(SAMPLE_TEXTURE2D_X(_ReturnSkinAlbedoRoot,sampler_LinearClamp,uv).rgb,.01h);}
  half4 Diffuse(Varyings i,float2 axis,bool firstPass) {
   float2 uv=i.texcoord;half4 centre=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
   half mask=SAMPLE_TEXTURE2D_X(_ReturnSkinOriginal,sampler_LinearClamp,uv).a;if(mask<.99)return centre;
   float eye=Eye(uv);float2 stepUV=axis*_ReturnSkinProjection.xy*.006/max(2*eye,.01);
   // Broad red diffusion and much narrower green/blue. Three taps on either
   // side cover a 6 mm red support, not an unrestricted portrait blur.
   // Split hospital pigmentation between light entry and exit; keep reflection separate.
   half3 total=centre.rgb/(firstPass?SkinAlbedoRoot(uv):half3(1,1,1)),weight=1;
   [unroll]for(int t=1;t<=3;t++) {
    float offset=t/3.0;half3 kernel=exp(-.5*pow(offset/float3(.5,.2167,.1333),2));
    [unroll]for(int side=-1;side<=1;side+=2) {
     float2 at=uv+stepUV*offset*side;half neighbourMask=SAMPLE_TEXTURE2D_X(_ReturnSkinOriginal,sampler_LinearClamp,at).a;
     half valid=step(.99,neighbourMask)*exp(-pow((Eye(at)-eye)/.012,2));half3 w=kernel*valid;
     half3 neighbour=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,at).rgb;
     if(firstPass)neighbour/=SkinAlbedoRoot(at);
     total+=neighbour*w;weight+=w;
    }
   }
   return half4((total/weight)*(firstPass?half3(1,1,1):SkinAlbedoRoot(uv)),centre.a);
  }
  half4 Horizontal(Varyings i):SV_Target{return Diffuse(i,float2(1,0),true);}
  half4 Vertical(Varyings i):SV_Target{return Diffuse(i,float2(0,1),false);}
  float4 ApplySkin(Varyings i):SV_Target {
   float2 uv=i.texcoord;float4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);float4 skin=SAMPLE_TEXTURE2D_X(_ReturnSkinOriginal,sampler_LinearClamp,uv);float3 blurred=SAMPLE_TEXTURE2D_X(_ReturnSkinBlurred,sampler_LinearClamp,uv).rgb;
   if(_ReturnSkinCompositeReviewBypass>.5)return scene;
   // The scene's surface reflections, eyes, hair and room remain outside the filter.
   scene.rgb+=(blurred-skin.rgb)*step(.99,skin.a);return scene;
  }
  float4 Evidence(Varyings i):SV_Target {
   float2 uv=i.texcoord;float3 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv).rgb;float4 skin=SAMPLE_TEXTURE2D_X(_ReturnSkinOriginal,sampler_PointClamp,uv);float3 blurred=SAMPLE_TEXTURE2D_X(_ReturnSkinBlurred,sampler_PointClamp,uv).rgb;float3 result=SAMPLE_TEXTURE2D_X(_ReturnSkinComposite,sampler_PointClamp,uv).rgb;float mask=step(.99,skin.a);
   return float4(length(result-scene),mask,length((result-scene)-(blurred-skin.rgb)*mask),dot(skin.rgb,float3(.3333,.3333,.3333)));
  }
  float LocalVisibility(float3 origin,float3 direction,float2 originUV) {
   float obstruction=0;
   [loop] for(int step=0;step<16;step++) {
    float3 at=origin+direction*(.006+step*.0075);
    float4 clip=TransformWorldToHClip(at);if(clip.w<=0)break;
    float2 uv=clip.xy/clip.w*.5+.5;
    #if UNITY_UV_STARTS_AT_TOP
    uv.y=1-uv.y;
    #endif
    if(any(uv<=0)||any(uv>=1))break;
    float movement=length((uv-originUV)*_BlitTexture_TexelSize.zw);
    if(movement<1.5)continue;
    float pointDepth=-TransformWorldToView(at).z;
    float behind=pointDepth-Eye(uv);
    // A short metric thickness interval excludes unrelated distant surfaces.
    float hit=smoothstep(.001,.004,behind)*(1-smoothstep(.012,.018,behind));
    obstruction=max(obstruction,hit*smoothstep(1.5,3,movement));
   }
   return 1-obstruction;
  }
  float EvaluateLocalVisibility(float2 uv,float4 normal,out float3 position) {
   float3 n=normalize(normal.xyz*2-1);
   float4 surface=SAMPLE_TEXTURE2D_X(_ReturnSkinSurfacePositions,sampler_PointClamp,uv);position=surface.xyz;
   // Unknown surface coverage keeps the existing room reflection.
   if(surface.a<.99)return 1;
   float3 view=normalize(GetCameraPositionWS()-position),ray=reflect(-view,n);
   float3 tangent=normalize(cross(ray,abs(ray.y)<.9?float3(0,1,0):float3(1,0,0))),bitangent=cross(ray,tangent);
   float cone=normal.a*normal.a*.45;float3 origin=position+n*.002;
   float visibility=LocalVisibility(origin,ray,uv);
   visibility+=LocalVisibility(origin,normalize(ray+tangent*cone),uv);
   visibility+=LocalVisibility(origin,normalize(ray-tangent*cone),uv);
   visibility+=LocalVisibility(origin,normalize(ray+bitangent*cone),uv);
   visibility+=LocalVisibility(origin,normalize(ray-bitangent*cone),uv);
   return saturate(visibility*.2);
  }
  float4 ApplyVisibility(Varyings i):SV_Target {
   float2 uv=i.texcoord;float4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
   float4 environment=SAMPLE_TEXTURE2D_X(_ReturnSkinEnvironment,sampler_PointClamp,uv);
   if(environment.a<.99)return scene;
   float4 normal=SAMPLE_TEXTURE2D_X(_ReturnSkinNormals,sampler_PointClamp,uv);
   float3 position;float visibility=EvaluateLocalVisibility(uv,normal,position);
   // Remove only the enabled skin's blocked probe reflection; diffuse/direct
   // light and all other surfaces remain in the scene source.
   scene.rgb-=environment.rgb*(1-visibility);return scene;
  }
  struct VisibilityEvidenceOutput {float4 visibility:SV_Target0;float4 before:SV_Target1;float4 after:SV_Target2;float4 environmentVisibility:SV_Target3;};
  VisibilityEvidenceOutput VisibilityEvidence(Varyings i) {
   float2 uv=i.texcoord;
   VisibilityEvidenceOutput o;o.before=SAMPLE_TEXTURE2D_X(_ReturnSkinReflectionBefore,sampler_PointClamp,uv);o.after=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);o.environmentVisibility=float4(0,0,0,1);
   float4 environment=SAMPLE_TEXTURE2D_X(_ReturnSkinEnvironment,sampler_PointClamp,uv);
   float4 actual=SAMPLE_TEXTURE2D_X(_ReturnSkinVisibilityPositions,sampler_PointClamp,uv);
   if(environment.a<.99){o.visibility=float4(0,0,0,actual.a>.99?-2:0);return o;}
   float4 normal=SAMPLE_TEXTURE2D_X(_ReturnSkinNormals,sampler_PointClamp,uv);
   float3 position;float visibility=EvaluateLocalVisibility(uv,normal,position);
   float covered=SAMPLE_TEXTURE2D_X(_ReturnSkinSurfacePositions,sampler_PointClamp,uv).a;
   o.visibility=float4(1,saturate(1-visibility),dot(environment.rgb,float3(.3333,.3333,.3333)),covered<.99?-3:actual.a>.99?length(position-actual.xyz)*1000:-1);o.environmentVisibility=float4(environment.rgb,visibility);return o;
  }
  struct NormalTransportOutput {float4 stored:SV_Target0;float4 producer:SV_Target1;};
  NormalTransportOutput NormalTransportEvidence(Varyings i) {
   NormalTransportOutput o;o.stored=SAMPLE_TEXTURE2D_X(_ReturnSkinNormals,sampler_PointClamp,i.texcoord);
   o.producer=SAMPLE_TEXTURE2D_X(_ReturnSkinVisibilityRawNormals,sampler_PointClamp,i.texcoord);return o;
  }
  ENDHLSL
  Pass {Name "Horizontal" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Horizontal
   ENDHLSL
  }
  Pass {Name "Vertical" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Vertical
   ENDHLSL
  }
  Pass {Name "Composite" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment ApplySkin
   ENDHLSL
  }
  Pass {Name "Evidence" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Evidence
   ENDHLSL
  }
  Pass {Name "LocalReflectionVisibility" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment ApplyVisibility
   ENDHLSL
  }
  Pass {Name "VisibilityEvidence" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment VisibilityEvidence
   ENDHLSL
  }
  Pass {Name "NormalTransportEvidence" HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment NormalTransportEvidence
   ENDHLSL
  }
 }
}
