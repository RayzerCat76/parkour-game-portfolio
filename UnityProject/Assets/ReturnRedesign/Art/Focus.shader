Shader "Return/Focus" {
 Properties { _MainTex("Image",2D)="white" {} _Blur("Focus",Float)=0 _Warmth("Warmth",Float)=0 }
 SubShader { Cull Off ZWrite Off ZTest Always Pass {
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize; float _Blur,_Warmth;
 fixed4 frag(v2f_img i):SV_Target {
 float2 s=_MainTex_TexelSize.xy*_Blur;
 fixed4 c=tex2D(_MainTex,i.uv)*.28;
 c+=(tex2D(_MainTex,i.uv+s)+tex2D(_MainTex,i.uv-s)+tex2D(_MainTex,i.uv+float2(s.x,-s.y))+tex2D(_MainTex,i.uv+float2(-s.x,s.y)))*.12;
 c+=(tex2D(_MainTex,i.uv+float2(s.x*2,0))+tex2D(_MainTex,i.uv-float2(s.x*2,0))+tex2D(_MainTex,i.uv+float2(0,s.y*2))+tex2D(_MainTex,i.uv-float2(0,s.y*2)))*.06;
 float vignette=smoothstep(.72,.15,length((i.uv-.5)*float2(1,.85)));
 c.rgb*=lerp(.88,1,vignette); c.rgb+=float3(.018,.011,0)*_Warmth; return c;
 }
 ENDCG
 } }
}
