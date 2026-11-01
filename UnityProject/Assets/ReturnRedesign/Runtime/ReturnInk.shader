Shader "Return/Ink" {
 Properties { _Color ("Tint", Color) = (1,1,1,1) _RockAtlasRect ("Rock atlas interior", Vector) = (0,0,1,1) _AuthoredUV ("Authored texture coordinates", Float) = 0 _Soft ("Soft light", Float) = 0 _MainTex ("Painted layer", 2D) = "white" {} _Blur ("Focus softness", Float) = 0 _ClipTop ("Foreground limit", Float) = 1 _SmokeEdge ("Soft volcanic front", Float) = 0 _Remembered ("Recovered stage", Float) = 0 _RecallOpacity ("Remembered atmosphere", Float) = 1 _RecallBlueReveal ("Reveal recovered world", Float) = 0 _StageReveal ("First-stage environment", Float) = 0 _FootingReveal ("Local floor visibility", Float) = 0 _RuptureReveal ("Chase colour return", Float) = 0 _SkyWash ("Continuous sky wash", Float) = 0 _SkyBottom ("Sky horizon", Color) = (1,1,1,1) _SkyTop ("Sky zenith", Color) = (1,1,1,1) _WashMap ("Wash coordinates", Vector) = (1,0,0,0) _RibbonSoft ("Feathered ash ribbon", Float) = 0 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Blend SrcAlpha OneMinusSrcAlpha
  ZWrite Off Cull Off
  Pass {
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
    float4 _RockAtlasRect,_Color,_ReturnRecallColour,_ReturnRecallGradient,_SkyBottom,_SkyTop,_WashMap; float _AuthoredUV,_Soft,_Blur,_ClipTop,_SmokeEdge,_Remembered,_RecallOpacity,_RecallBlueReveal,_SkyWash,_RibbonSoft; sampler2D _MainTex; float4 _MainTex_TexelSize;
    float4 _ReturnGreyWave,_ReturnGreyOrigin,_ReturnFooting,_ReturnFootingAxis; float _StageReveal,_RuptureReveal,_ReturnRuptureColour,_FootingReveal;
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; float2 inkPosition:TEXCOORD0; float worldX:TEXCOORD1; float2 authoredUV:TEXCOORD2; float3 worldPosition:TEXCOORD3; };
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.inkPosition=v.vertex.xy; o.authoredUV=v.uv; o.worldPosition=mul(unity_ObjectToWorld,v.vertex).xyz; o.worldX=o.worldPosition.x; return o; }
   fixed4 rockPatch(float2 samplePosition,float2 cell) {
    float angle=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453)*6.2831853;
    float s,c; sincos(angle,s,c); float2 d=samplePosition-cell;
    float2 patch=.5+float2(c*d.x-s*d.y,s*d.x+c*d.y)*.353553;
    return tex2D(_MainTex,_RockAtlasRect.xy+patch*_RockAtlasRect.zw);
   }
   fixed4 rockSurface(float2 samplePosition) {
    float2 cell=floor(samplePosition),blend=frac(samplePosition); blend=blend*blend*(3-2*blend);
    // Neighbouring patches overlap continuously; no sample reflects its texture.
    return lerp(lerp(rockPatch(samplePosition,cell),rockPatch(samplePosition,cell+float2(1,0)),blend.x),
                lerp(rockPatch(samplePosition,cell+float2(0,1)),rockPatch(samplePosition,cell+float2(1,1)),blend.x),blend.y);
   }
   fixed4 frag(v2f i):SV_Target {
    float2 uv=i.inkPosition+.5; if(_ClipTop<.999)clip(_ClipTop-uv.y);
    fixed4 paint; if(_AuthoredUV>.5)paint=rockSurface(i.authoredUV); else paint=tex2D(_MainTex,uv);
    if(_Blur>.01) {
     float2 stepSize=_MainTex_TexelSize.xy*_Blur;
     paint=paint*.2+(tex2D(_MainTex,uv+stepSize)+tex2D(_MainTex,uv-stepSize)+tex2D(_MainTex,uv+float2(stepSize.x,-stepSize.y))+tex2D(_MainTex,uv+float2(-stepSize.x,stepSize.y)))*.2;
    }
    fixed4 c=_Color*paint;
     if(_SkyWash>.5){float wash=smoothstep(0,1,uv.y*_WashMap.x+_WashMap.y);c.rgb=lerp(_SkyBottom.rgb,_SkyTop.rgb,wash);c.a*=lerp(_SkyBottom.a,_SkyTop.a,wash);}
     if(_RibbonSoft>.5) {
      float drift=.025*sin(i.authoredUV.y*18+_Time.y*.35)+.014*sin(i.authoredUV.y*37-_Time.y*.22);
      float edge=abs(i.authoredUV.x-.5+drift);
      c.a*=(1-smoothstep(.30,.50,edge))*(1-smoothstep(.70,1,i.authoredUV.y));
     }
    if(_Remembered>.5) {
     float travel=saturate(_ReturnRecallGradient.y);
     float feather=4*travel*(1-travel);
     float progress=saturate(travel+(_ReturnRecallGradient.x-i.worldX)/max(1,_ReturnRecallGradient.z)*feather);
     float3 recall=_ReturnRecallColour.rgb;
     float3 spatial=float3(.35+.65*smoothstep(0,.4,progress),smoothstep(.10,.70,progress),smoothstep(.48,1,progress));
     recall=lerp(recall,spatial,saturate(_ReturnRecallGradient.w));
     float grey=dot(c.rgb,float3(.299,.587,.114));
     float3 recovered=lerp(grey.xxx,grey*float3(1.16,1.07,.72),recall.x);
     recovered=lerp(recovered,float3(max(c.r,grey),grey*.7,grey*.58),recall.y);
     c.rgb=lerp(recovered,c.rgb,recall.z);
    }
    if(_Remembered<.5) {
     float grey=dot(c.rgb,float3(.299,.587,.114));
     if(_StageReveal>.5 && _ReturnGreyWave.w>.5) {
      float distance=length(i.worldPosition-_ReturnGreyOrigin.xyz);
      float reveal=smoothstep(distance-1.5,distance+1.5,_ReturnGreyWave.z);
      float light=lerp(_ReturnGreyWave.x,_ReturnGreyWave.y,reveal);
      c.rgb=(.08+.92*grey)*light;
       if(_FootingReveal>.5&&_ReturnFooting.w>.5) {
        float3 offset=i.worldPosition-_ReturnFooting.xyz;
        float horizontal=abs(dot(offset,_ReturnFootingAxis.xyz));
        float footLight=(1-smoothstep(.12,_ReturnFootingAxis.w,horizontal))*(1-smoothstep(.04,.65,abs(offset.y)));
        c.rgb=max(c.rgb,.075*footLight);
       }
     }
     if(_RuptureReveal>.5)c.rgb=lerp(grey.xxx,c.rgb,saturate(_ReturnRuptureColour));
    }
    if(_SmokeEdge>.5) {
     float billow=.035*sin(uv.y*37+_Time.y*.9)+.018*sin(uv.y*83-_Time.y*1.6);
     c.a*=1-smoothstep(.72+billow,1,uv.x);
    }
    c.a*=_RecallOpacity*(1-_RecallBlueReveal*saturate(_ReturnRecallColour.z)*.96)*lerp(1,pow(saturate(1-length(i.inkPosition)*2),2.7),_Soft); return c;
   }
   ENDHLSL
  }
 }
}
