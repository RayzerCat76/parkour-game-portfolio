Shader "Hidden/Return/PreviewUI"
{
    Properties { _MainTex ("Texture",2D)="white"{} _Tint ("Tint",Color)=(1,1,1,1) _Font ("Font alpha",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _Tint; float _Font;
            struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct Output {float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            Output vert(Input i){Output o;o.position=UnityObjectToClipPos(i.vertex);o.uv=i.uv;return o;}
            half4 frag(Output i):SV_Target {half4 t=tex2D(_MainTex,i.uv);return half4(lerp(t.rgb,half3(1,1,1),_Font)*_Tint.rgb,t.a*_Tint.a);}
            ENDCG
        }
    }
}
