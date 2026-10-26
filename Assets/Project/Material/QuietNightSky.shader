Shader "Project/QuietNightSky"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HorizonColor ("Horizon", Color) = (0.075,0.105,0.17,1)
        _ZenithColor ("Upper Sky", Color) = (0.025,0.04,0.075,1)
        _HorizonY ("Horizon World Height", Float) = 0
        _FadeHeight ("Sky Fade Height", Float) = 22
        _CloudColor ("Cloud Tint", Color) = (0.13,0.16,0.23,1)
        _CloudStrength ("Cloud Strength", Range(0,1)) = 0.38
        _StarStrength ("Star Brightness", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXY : TEXCOORD0;
                half alpha : TEXCOORD1;
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _HorizonColor;
                half4 _ZenithColor;
                float _HorizonY;
                float _FadeHeight;
                half4 _CloudColor;
                float _CloudStrength;
                float _StarStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.worldXY = position.positionWS.xy;
                output.alpha = input.color.a;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), t.x), t.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // A shared world-height gradient keeps vertically tiled sky pieces seamless.
                // The old halftone texture is deliberately not sampled.
                float height = saturate((input.worldXY.y - _HorizonY) / max(1.0, _FadeHeight));
                height = height * height * (3.0 - 2.0 * height);
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, height);

                // Broad, soft cloud bands; no fine grain or halftone pattern.
                float2 p = input.worldXY * float2(0.065, 0.2) + float2(_Time.y * 0.0015, 0);
                float cloud = Noise(p) * 0.65 + Noise(p * 2.03 + 19.7) * 0.25
                    + Noise(p * 4.07 + 7.1) * 0.1;
                cloud = smoothstep(0.38, 0.78, cloud);
                color = lerp(color, _CloudColor.rgb, cloud * _CloudStrength);

                // Uneven city glow near the horizon adds depth behind the silhouettes.
                float haze = exp(-abs(input.worldXY.y - _HorizonY - 3.0) * 0.14);
                float glow = Noise(float2(input.worldXY.x * 0.075, 13.4));
                color += half3(0.016, 0.023, 0.035) * haze * (0.45 + glow * 0.55);

                // A few small stars, jittered within cells so they never form a grid.
                float2 starGrid = input.worldXY / 1.4;
                float2 starCell = floor(starGrid);
                float2 starCenter = 0.15 + 0.7 * float2(Hash(starCell + 4.3), Hash(starCell + 17.8));
                float distanceToStar = length(frac(starGrid) - starCenter);
                float radius = lerp(0.008, 0.018, Hash(starCell + 31.2));
                float aa = max(fwidth(distanceToStar), 0.001);
                float star = (1.0 - smoothstep(radius, radius + aa, distanceToStar))
                    * step(0.9, Hash(starCell + 82.5));
                star *= smoothstep(0.12, 0.65, height) * (1.0 - cloud * 0.85);
                color += half3(0.65, 0.75, 0.9) * star * _StarStrength;
                return half4(color, input.alpha);
            }
            ENDHLSL
        }
    }
}
