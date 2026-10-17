// Midnight-navy skybox: vertical gradient, warm city glow on the horizon, twinkling stars,
// a faint Milky Way lane and a soft moon.
Shader "Expo/NightSky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.008, 0.014, 0.04, 1)
        _HorizonColor ("Horizon Color", Color) = (0.04, 0.07, 0.15, 1)
        _BottomColor ("Bottom Color", Color) = (0.01, 0.015, 0.03, 1)
        [HDR] _GlowColor ("City Glow", Color) = (0.35, 0.22, 0.12, 1)
        _GlowHeight ("City Glow Falloff", Range(1, 40)) = 9
        _StarDensity ("Star Density", Range(0.9, 0.999)) = 0.985
        _StarBrightness ("Star Brightness", Range(0, 6)) = 2.5
        [HDR] _BandColor ("Band Color", Color) = (0.05, 0.12, 0.25, 1)
        _MoonDirection ("Moon Direction", Vector) = (-0.4, 0.45, 0.8, 0)
        [HDR] _MoonColor ("Moon Color", Color) = (1.6, 1.7, 2.0, 1)
        _MoonSize ("Moon Size", Range(0.00005, 0.01)) = 0.00028
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _GlowColor;
                half _GlowHeight;
                half _StarDensity;
                half _StarBrightness;
                half4 _BandColor;
                float4 _MoonDirection;
                half4 _MoonColor;
                half _MoonSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float h = d.y;
                half3 sky = (h >= 0.0)
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(h), 0.45))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-h * 6.0));

                // Warm light pollution from the city sitting on the horizon.
                sky += _GlowColor.rgb * exp(-abs(h) * _GlowHeight);

                // A faint Milky Way: a clumpy great-circle lane with a darker dust rift down its middle.
                float3 laneN = normalize(float3(0.42, 0.3, 0.86));
                float g = dot(d, laneN);
                float lane = exp(-g * g * 22.0);
                float clump = 0.55 + 0.45 * sin(d.x * 7.0 + d.y * 3.0 + 1.3) * sin(d.z * 9.0 - d.y * 5.0 + 0.7);
                float grain = 0.8 + 0.2 * sin(d.x * 61.0 + d.y * 23.0) * sin(d.z * 57.0 - d.y * 31.0);
                float rift = 1.0 - 0.55 * exp(-g * g * 260.0);
                sky += _BandColor.rgb * 0.55 * lane * clump * grain * rift * smoothstep(0.08, 0.45, h);

                // Stars: one candidate per cell on a fine 3D grid, denser along the lane, hidden near the horizon haze.
                float3 p = d * 260.0;
                float3 cell = floor(p);
                float r = Hash13(cell);
                float3 f = frac(p) - 0.5;
                float star = step(_StarDensity - lane * 0.006, r) * smoothstep(0.28, 0.0, length(f));
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.5 + r * 4.0) + r * 60.0);
                sky += star * twinkle * _StarBrightness * smoothstep(0.05, 0.35, h);

                // Moon disc with a soft halo.
                float3 moonDir = normalize(_MoonDirection.xyz);
                float m = dot(d, moonDir);
                float disc = smoothstep(1.0 - _MoonSize, 1.0 - _MoonSize * 0.7, m);
                float halo = pow(saturate(m), 2400.0) * 0.4 + pow(saturate(m), 160.0) * 0.06;
                sky += _MoonColor.rgb * (disc + halo);

                // Per-pixel dither of about one 8-bit step, so the dark gradients never posterize into contours.
                sky += (Hash13(float3(input.positionCS.xy, 17.0)) - 0.5) * 0.0016;

                return half4(max(sky, 0.0), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
