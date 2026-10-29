Shader "Project/BiomeEnvironment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Contrast ("Surface Contrast", Range(0.5, 1.5)) = 1.08
        _EmissionStrength ("Window And Lantern Light", Range(0, 2)) = 0.65
        _GlowStrength ("Light Spill On Surface", Range(0, 1)) = 0.25
        _GlowRadius ("Light Spill Radius (Source Pixels)", Range(1, 64)) = 24
        _WarmColor ("Warm Light", Color) = (1, 0.7, 0.15, 1)
        _CoolColor ("Cool Light", Color) = (0.05, 0.85, 1, 1)
        _RimColor ("Walkable Edge Color", Color) = (0.08, 0.7, 1, 1)
        _RimStrength ("Walkable Edge Strength", Range(0, 1)) = 0.65
        _EdgeGlowStrength ("Platform Edge Glow", Range(0, 1)) = 0
        _RippleStrength ("Reflection Shimmer", Range(0, 0.5)) = 0.18
        _RippleSpeed ("Reflection Speed", Range(0, 2)) = 0.45
        [Toggle(_SURFACE_RIM)] _SurfaceRim ("Walkable Edge", Float) = 0
        [Toggle(_SURFACE_REFLECTION)] _SurfaceReflection ("Street Reflection", Float) = 0
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 0
        [HideInInspector] _PreviewTime ("Preview Time Override", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex EnvironmentVertex
            #pragma fragment EnvironmentFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #pragma shader_feature_local_fragment _SURFACE_RIM
            #pragma shader_feature_local_fragment _SURFACE_REFLECTION
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };
            struct Varyings
            {
                COMMON_2D_OUTPUTS_SHARED
                half4 color : COLOR;
                float2 worldXY : TEXCOORD1;
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _WarmColor, _CoolColor, _RimColor;
                half _Contrast, _EmissionStrength, _GlowStrength, _GlowRadius;
                half _RimStrength, _RippleStrength, _RippleSpeed, _EdgeGlowStrength;
                float _PreviewTime;
            CBUFFER_END

            Varyings EnvironmentVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.worldXY = TransformObjectToWorld(input.positionOS).xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half3 LightMask(half4 source)
            {
                // Select painted yellow/cyan lights, not red roofs or blue walls.
                half warm = smoothstep(0.28h, 0.7h, min(source.r, source.g))
                    * smoothstep(0.09h, 0.3h, min(source.r, source.g) - source.b);
                half cool = smoothstep(0.36h, 0.8h, min(source.g, source.b))
                    * smoothstep(0.16h, 0.45h, min(source.g, source.b) - source.r);
                return (warm * _WarmColor.rgb + cool * _CoolColor.rgb) * source.a;
            }

            half3 Spill(float2 uv, float2 offset)
            {
                return LightMask(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset));
            }

            half4 EnvironmentFragment(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (source.a <= 0 || input.color.a <= 0) return 0;
                half3 color = saturate((source.rgb * input.color.rgb - 0.12h) * _Contrast + 0.12h);
                #if defined(_SURFACE_RIM)
                    half detail = max(source.r, max(source.g, source.b));
                    color = lerp(color, _RimColor.rgb * (0.35h + detail * 0.65h), _RimStrength);
                    float seconds = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                    color += _RimColor.rgb * _EdgeGlowStrength * (1 + 0.04h * sin(seconds * 2));
                #elif defined(_SURFACE_REFLECTION)
                    // Brightness-only ripples keep the supplied pixel shapes, alpha and tile seams intact.
                    float2 cell = floor(input.worldXY * float2(12, 24));
                    float seconds = _PreviewTime >= 0 ? _PreviewTime : _Time.y;
                    half wave = sin(cell.y * 1.7 + sin(cell.x * 0.11) + seconds * _RippleSpeed * 3);
                    half lit = saturate(max(source.r, max(source.g, source.b)) * 2 - 0.45h);
                    color *= 1 + wave * _RippleStrength * lit;
                    color += LightMask(source) * _EmissionStrength * (0.85h + wave * 0.15h);
                #else
                    half3 light = LightMask(source);
                    color += light * _EmissionStrength;
                    UNITY_BRANCH
                    if (_GlowStrength > 0)
                    {
                        float2 radius = _MainTex_TexelSize.xy * _GlowRadius;
                        half3 spill = Spill(input.uv, radius * float2(1, 0)) + Spill(input.uv, radius * float2(-1, 0))
                            + Spill(input.uv, radius * float2(0, 1)) + Spill(input.uv, radius * float2(0, -1));
                        spill += Spill(input.uv, radius * float2(0.5, 0.5)) + Spill(input.uv, radius * float2(-0.5, 0.5))
                            + Spill(input.uv, radius * float2(0.5, -0.5)) + Spill(input.uv, radius * float2(-0.5, -0.5));
                        color += spill * (_GlowStrength / 8);
                    }
                #endif
                // Emission never fills transparent padding or escapes a SpriteMask.
                return half4(saturate(color), source.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
