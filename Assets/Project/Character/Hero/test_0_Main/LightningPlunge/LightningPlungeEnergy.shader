Shader "Project/LightningPlungeEnergy"
{
    Properties
    {
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,5)) = 1.5
        _Softness ("Soft Edge", Range(0.02,1)) = 0.6
        _Profile ("Profile: ribbon 0 / radial 1", Float) = 0
        _ActionFrame ("Action Frame", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha [_DstBlend]
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; float2 world:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Emission, _Softness, _Profile, _ActionFrame;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.world = TransformObjectToWorld(input.positionOS.xyz).xy;
                output.uv = input.uv; output.color = input.color * _Color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float distance = lerp(abs(p.x), length(p), saturate(_Profile));
                float edge = 1 - smoothstep(1 - _Softness, 1, distance);
                float gaussian = exp2(-4.5 * distance * distance);
                float shape = lerp(edge, gaussian * edge, saturate(_Profile + _Softness * 0.45));
                // Meshes and particle meshes supply their own color/UVs. Sampling the sprite
                // renderer's per-renderer texture can pick up an unrelated atlas binding.
                half4 color = input.color;
                color.rgb = max(half3(0, 0, 0), color.rgb) * max(0, _Emission);
                color.a = saturate(color.a * shape);
                return color;
            }
            ENDHLSL
        }
    }
}
