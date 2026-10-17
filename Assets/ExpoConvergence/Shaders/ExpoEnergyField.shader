// Two-sided additive energy field: scrolling stripes, hex shimmer, bright UV edges and a
// world-space ripple. Used for thermal turnstiles, district seals and energy barriers.
Shader "Expo/EnergyField"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 0.7, 0.2, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        _StripeDensity ("Stripe Density", Float) = 18
        _Speed ("Speed", Float) = 0.8
        _EdgeWidth ("Edge Width", Range(0.001, 0.5)) = 0.06
        _Open ("Open (0 closed, 1 open)", Range(0, 1)) = 0
        _RipplePos ("Ripple Position (world)", Vector) = (0, -1000, 0, 0)
        _RippleTime ("Ripple Time", Float) = -10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "EnergyField"
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
                half _StripeDensity;
                half _Speed;
                half _EdgeWidth;
                half _Open;
                float4 _RipplePos;
                float _RippleTime;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv;
                float t = _Time.y * _Speed;

                // Scrolling diagonal stripes plus a slower counter-scroll for depth.
                half stripes = 0.5h + 0.5h * sin((uv.y * _StripeDensity + uv.x * 3.0) * 6.2831 - t * 6.2831);
                half stripes2 = 0.5h + 0.5h * sin((uv.y * _StripeDensity * 0.37 - uv.x * 5.0) * 6.2831 + t * 2.6);
                half body = 0.12h + 0.35h * smoothstep(0.55h, 1.0h, stripes) + 0.2h * stripes2;

                // Bright frame along the UV borders.
                float2 e = min(uv, 1.0 - uv);
                half edge = 1.0h - smoothstep(0.0h, _EdgeWidth, min(e.x, e.y));

                // Ripple ring spreading from a world position (e.g. where the cloaked courier passes).
                float age = _Time.y - _RippleTime;
                float dist = distance(input.positionWS, _RipplePos.xyz);
                half ring = (age > 0.0 && age < 1.4) ? exp(-pow((dist - age * 3.5) * 3.0, 2.0)) * (1.0 - age / 1.4) : 0.0h;

                // Opening splits the field from the middle outwards.
                half openMask = smoothstep(_Open * 0.5h - 0.02h, _Open * 0.5h + 0.02h, abs(uv.x - 0.5h));
                half alpha = saturate(body + edge * 0.9h + ring * 1.2h) * _Alpha * lerp(1.0h, openMask, step(0.001h, _Open));
                half3 color = _Color.rgb * (0.7h + edge * 1.6h + ring * 2.0h + stripes * 0.35h);
                color = MixFogColor(color, half3(0, 0, 0), input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
