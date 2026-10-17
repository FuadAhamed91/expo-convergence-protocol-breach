// Additive beam for LineRenderers and thin quads: a hot core across UV.y with a soft halo,
// plus travelling energy pulses along UV.x. Used for lasers, prism bolts and landmark light beams.
Shader "Expo/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.15, 0.2, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
        _CoreWidth ("Core Width", Range(1, 40)) = 9
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseDensity ("Pulse Density", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Beam"
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
                half _CoreWidth;
                half _PulseSpeed;
                half _PulseDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
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
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half across = abs(input.uv.y - 0.5h) * 2.0h;
                half core = exp(-across * across * _CoreWidth);
                half halo = (1.0h - across) * 0.35h;
                half pulse = 0.75h + 0.25h * sin(input.uv.x * _PulseDensity * 6.2831h - _Time.y * _PulseSpeed * 6.2831h);
                half alpha = saturate(core + halo) * _Alpha * input.color.a * pulse;
                half3 color = _Color.rgb * input.color.rgb * (0.6h + core * 1.8h);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
