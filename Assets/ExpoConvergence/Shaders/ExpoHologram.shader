// Additive hologram: fresnel rim, world-space scanlines and a light flicker.
// Used for holographic signage, the Mirage Cloak body shell, shockwaves and afterimages.
Shader "Expo/Hologram"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.3, 0.9, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _CoreAlpha ("Core Alpha", Range(0, 1)) = 0.12
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5
        _ScanDensity ("Scan Density", Float) = 38
        _ScanSpeed ("Scan Speed", Float) = 0.6
        _ScanStrength ("Scan Strength", Range(0, 1)) = 0.35
        _Flicker ("Flicker", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Alpha;
                half _CoreAlpha;
                half _FresnelPower;
                half _ScanDensity;
                half _ScanSpeed;
                half _ScanStrength;
                half _Flicker;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 n = normalize(input.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(input.positionWS));
                half fresnel = pow(1.0h - saturate(abs(dot(n, v))), _FresnelPower);
                half scan = 0.5h + 0.5h * sin(input.positionWS.y * _ScanDensity - _Time.y * _ScanSpeed * 6.2831h);
                scan = smoothstep(0.35h, 1.0h, scan) * _ScanStrength;
                half noise = frac(sin(floor(_Time.y * 22.0) * 12.9898) * 43758.5453);
                half flicker = 1.0h - _Flicker * step(0.93h, noise);
                half alpha = saturate(_CoreAlpha + fresnel * 0.9h + scan) * _Alpha * flicker;
                half3 color = _Color.rgb * (0.55h + fresnel * 1.5h + scan * 1.2h);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
