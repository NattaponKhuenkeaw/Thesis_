Shader "Project/LightningPlungeEnergy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,5)) = 1.5
        _Softness ("Soft Edge", Range(0.02,1)) = 0.6
        _Profile ("Profile: ribbon 0 / radial 1", Float) = 0
        _ActionFrame ("Action Frame", Float) = 0
        _NoiseStrength ("Filament Variation", Range(0,1)) = 0.12
        _Dissolve ("Exit Erosion", Range(0,1)) = 0
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Emission, _Softness, _Profile, _ActionFrame, _NoiseStrength, _Dissolve;
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
                // Animation comes from the combat clock, so pause/hitstop cannot shimmer independently.
                float filament = sin(input.world.y * 17 + sin(input.world.x * 9) + floor(_ActionFrame) * 0.9);
                float variation = lerp(1, 0.8 + 0.2 * filament, _NoiseStrength);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                color.rgb *= _Emission * variation;
                color.a *= shape;
                // Break the retreating geometry into small charged cells, with a bright erosion front.
                float2 cell = floor(input.world * 22);
                float grain = frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
                float threshold = _Dissolve * 1.35 - 0.3 + input.uv.y * 0.2;
                float erosion = smoothstep(threshold, threshold + 0.08, grain);
                float rim = (1 - smoothstep(0.0, 0.12, abs(grain - threshold))) * step(0.001, _Dissolve);
                color.rgb += half3(0.7, 0.25, 1.25) * rim * _Dissolve;
                color.a *= erosion;
                return color;
            }
            ENDHLSL
        }
    }
}
