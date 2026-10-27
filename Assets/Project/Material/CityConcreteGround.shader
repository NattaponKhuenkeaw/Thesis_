Shader "Project/CityConcreteGround"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FaceColor ("Concrete Face", Color) = (0.10,0.13,0.18,1)
        _EdgeColor ("Walking Edge", Color) = (0.30,0.35,0.40,1)
        _BottomColor ("Lower Shadow", Color) = (0.045,0.06,0.085,1)
        _CapDepth ("Edge Depth In World Units", Float) = 0.09
        _SlabWidth ("Slab Width In World Units", Float) = 2.4
        _PixelSize ("Detail Pixel Size", Float) = 0.035
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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXY : TEXCOORD1;
                half alpha : TEXCOORD2;
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _FaceColor;
                half4 _EdgeColor;
                half4 _BottomColor;
                float _CapDepth;
                float _SlabWidth;
                float _PixelSize;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS);
                output.positionCS = position.positionCS;
                output.worldXY = position.positionWS.xy;
                output.uv = input.uv;
                output.alpha = input.color.a;
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // World-space detail retains its density on long and short floors.
                float pixel = max(0.01, _PixelSize);
                float2 cell = floor(input.worldXY / pixel);
                float grain = Hash(cell);
                // Recover sprite height from derivatives, without changing its mesh or scale.
                float uvRate = length(float2(ddx(input.uv.y), ddy(input.uv.y)));
                float worldRate = length(float2(ddx(input.worldXY.y), ddy(input.worldXY.y)));
                float height = worldRate / max(uvRate, 0.00001);
                float depth = (1.0 - input.uv.y) * height;
                float cap = max(pixel * 2.0, _CapDepth);
                float slab = max(0.5, _SlabWidth);
                float slabX = frac((cell.x * pixel) / slab) * slab;
                float joint = 1.0 - step(pixel, min(slabX, slab - slabX));

                half3 face = lerp(_BottomColor.rgb, _FaceColor.rgb, saturate(input.uv.y * 1.4));
                face += (grain - 0.5) * 0.014;
                // Sparse recessed slab joints, strongest near the walking edge.
                face *= 1.0 - joint * 0.22 * saturate(1.0 - depth / 0.65);
                half3 edge = _EdgeColor.rgb * (0.94 + grain * 0.08);
                edge *= 1.0 - joint * 0.35;
                half3 color = depth < cap ? edge : face;
                // Small bevel above and a shadow immediately below the concrete lip.
                if (depth < pixel) color *= 1.16;
                if (depth >= cap && depth < cap + pixel) color *= 0.52;
                return half4(color, input.alpha);
            }
            ENDHLSL
        }
    }
}
