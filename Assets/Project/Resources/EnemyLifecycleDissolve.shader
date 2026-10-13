Shader "Project/EnemyLifecycleDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeColor ("Energy Color", Color) = (0.2,1,0.75,1)
        _EdgeWidth ("Energy Edge", Range(0.01,0.2)) = 0.07
        _NoiseScale ("Mist Scale", Float) = 5
        _SpritePresentation ("Sprite Rendering", Float) = 1
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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
                float2 fragmentPosition : TEXCOORD1;
            };
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EdgeColor;
                float _Dissolve;
                float _EdgeWidth;
                float _NoiseScale;
                float _SpritePresentation;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                float2 localPosition = input.positionOS.xy;
                if (_SpritePresentation > 0.5)
                    input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color;
                if (_SpritePresentation > 0.5) output.color *= unity_SpriteColor;
                output.fragmentPosition = localPosition;
                return output;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            float MistNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 t = frac(p);
                t = t * t * (3 - 2 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + float2(1,1)), t.x), t.y);
            }
            float Mist(float2 p)
            {
                return (MistNoise(p) * 0.5 + MistNoise(p * 2.03) * 0.25
                    + MistNoise(p * 4.07) * 0.125) / 0.875;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);
                if (_SpritePresentation < 0.5)
                {
                    float2 p = input.uv * 2 - 1;
                    float2 drift = float2(_Time.y * 0.12, -_Time.y * 0.22);
                    float turbulence = Mist(input.uv * 4 + drift);
                    float radial = length(p * float2(1, 0.85));
                    float cloud = pow(saturate(1 - radial + (turbulence - 0.5) * 0.3), 1.4);
                    color.a *= cloud * lerp(0.4, 1, turbulence);
                    color.rgb *= lerp(0.65, 1, turbulence);
                    return color;
                }
                float noise = Mist(input.fragmentPosition * max(1, _NoiseScale) + float2(0, -_Time.y * 0.2));
                float threshold = lerp(-0.2, 1.2, saturate(_Dissolve));
                float softness = max(0.08, _EdgeWidth);
                color.a *= smoothstep(threshold - softness, threshold + softness, noise);
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, sin(saturate(_Dissolve) * 3.14159) * 0.45);
                return color;
            }
            ENDHLSL
        }
    }
}
