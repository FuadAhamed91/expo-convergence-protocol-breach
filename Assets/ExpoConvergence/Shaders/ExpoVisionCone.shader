// Additive security vision cone. UV.y runs from the sensor (0) to the cone's far end (1);
// alpha falls off with distance and towards silhouette edges so cones read as light, not solids.
Shader "Expo/VisionCone"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.22
        _EdgeSoftness ("Edge Softness", Range(0.1, 4)) = 1.2
        _ScanSpeed ("Scan Speed", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "VisionCone"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _EdgeSoftness;
                half _ScanSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = pow(saturate(abs(dot(n, v))), _EdgeSoftness);
                half lengthFade = (1.0h - input.uv.y) * smoothstep(0.0h, 0.08h, input.uv.y);
                half sweep = 0.5h + 0.5h * sin(input.uv.y * 18.0h - _Time.y * _ScanSpeed * 6.2831h);
                half alpha = _Alpha * facing * lengthFade * (0.75h + 0.25h * sweep);
                half3 color = _Color.rgb * (1.0h + 0.4h * sweep);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
