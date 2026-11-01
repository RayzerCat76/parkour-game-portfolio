Shader "Return/DriftVisibilityControl"
{
    Properties { [Enum(UnityEngine.Rendering.CompareFunction)] _DepthTest("Depth test",Float)=4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        ZTest [_DepthTest]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        struct ControlAttributes { float4 positionOS : POSITION; };
        struct ControlVaryings { float4 positionCS : SV_POSITION; };
        ControlVaryings ControlVertex(ControlAttributes input)
        {
            ControlVaryings output;
            output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
            return output;
        }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex ControlVertex
            #pragma fragment ControlFragment
            half4 ControlFragment(ControlVaryings input) : SV_Target { return half4(1,0,1,1); }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ControlVertex
            #pragma fragment ControlDepth
            half4 ControlDepth(ControlVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DriftForcedLeaf"
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex ForcedVertex
            #pragma fragment ForcedFragment
            float4x4 _DriftLocalToWorld, _DriftWorldToClip;
            ControlVaryings ForcedVertex(ControlAttributes input)
            {
                ControlVaryings output;
                output.positionCS=mul(_DriftWorldToClip,mul(_DriftLocalToWorld,float4(input.positionOS.xyz,1)));
                return output;
            }
            half4 ForcedFragment(ControlVaryings input) : SV_Target { return half4(1,0,1,1); }
            ENDHLSL
        }
        Pass
        {
            Name "DriftMarker"
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MarkerVertex
            #pragma fragment MarkerFragment
            ControlVaryings MarkerVertex(uint id : SV_VertexID)
            {
                ControlVaryings output;
                float2 markerPosition=id==0?float2(.88,-.02):id==1?float2(.94,-.02):float2(.88,.05);
                output.positionCS=float4(markerPosition,0,1);
                return output;
            }
            half4 MarkerFragment(ControlVaryings input) : SV_Target { return half4(0,1,1,1); }
            ENDHLSL
        }
        Pass
        {
            Name "DriftBuiltinLeaf"
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex ControlVertex
            #pragma fragment BuiltinFragment
            half4 BuiltinFragment(ControlVaryings input) : SV_Target { return half4(1,0,1,1); }
            ENDHLSL
        }
    }
}
