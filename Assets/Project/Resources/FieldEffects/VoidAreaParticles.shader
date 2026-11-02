Shader "Project/VoidAreaParticles"
{
    Properties
    {
        _RootColor ("Root", Color) = (0.55,0.015,0.95,1)
        _TipColor ("Smoke", Color) = (0.018,0.004,0.03,1)
        _Opacity ("Opacity", Range(0,1)) = 0.7
        _Smoke ("Smoke Shape", Range(0,1)) = 0
        _Halo ("Shadow Halo", Range(0,1)) = 0
        _Fragment ("Flame Fragment", Range(0,1)) = 0
        [HDR] _BaseColor ("Hot Root Color", Color) = (0.62,0.48,1.8,1)
        _BaseGlow ("Root Glow", Range(0,4)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
        [HideInInspector] _FieldHeight ("Field Height", Float) = 2
        [HideInInspector] _GroundY ("Ground", Float) = -10000
        [HideInInspector] _PreviewTime ("Preview Time", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha [_DstBlend]
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float2 seed : TEXCOORD1; float worldY : TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                half4 _RootColor;
                half4 _TipColor;
                float _Opacity;
                float _Smoke;
                float _Halo;
                float _Fragment;
                float _GroundY;
                float _PreviewTime;
                half4 _BaseColor;
                float _BaseGlow;
                float _FieldHeight;
            CBUFFER_END
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1,0)), f.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + 1), f.x), f.y);
            }
            float Turbulence(float2 p)
            {
                return Noise(p) * 0.68 + Noise(p * 2.03 + 13.1) * 0.25 + Noise(p * 4.07 + 7.3) * 0.07;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.color = input.color;
                output.seed = float2(world.x * 0.17 + input.color.r * 31.13, input.color.g * 17.31);
                output.worldY = world.y;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _PreviewTime >= 0.0 ? _PreviewTime : _Time.y;
                float ground = smoothstep(_GroundY - 0.03, _GroundY + 0.09, input.worldY);
                if (_Halo > 0.5)
                {
                    float2 p = uv * 2.0 - 1.0;
                    float radius = length(p);
                    float soft = exp(-radius * radius * 3.8) * (1.0 - smoothstep(0.65, 1.0, radius));
                    float fringe = exp(-pow((radius - 0.62) * 5.0, 2.0));
                    half3 tint = lerp(_RootColor.rgb, _TipColor.rgb, fringe * 0.6);
                    return half4(tint * input.color.rgb, soft * _Opacity * input.color.a * ground);
                }
                if (_Fragment > 0.5)
                {
                    float2 p = uv * 2.0 - 1.0;
                    float radius = length(p * float2(1.0, 0.8));
                    float body = exp(-radius * radius * 5.0) * (1.0 - smoothstep(0.4, 1.0, radius));
                    half3 tint = lerp(_RootColor.rgb, _TipColor.rgb, exp(-radius * radius * 12.0));
                    return half4(tint * input.color.rgb, body * _Opacity * input.color.a * ground);
                }
                float2 p = uv * 2.0 - 1.0;
                float2 flow = p * float2(1.6, 1.5) + input.seed + float2(0, -t * 0.6);
                float warp = Turbulence(flow * 0.85);
                float2 curl = float2(sin(flow.y * 2.0 + warp * 4.0), cos(flow.x * 2.0 - warp * 4.0)) * 0.45;
                float density = Turbulence(flow * 0.95 + curl * 1.5);
                float radius = length(p + float2((warp - 0.5) * 0.45, 0));
                float envelope = 1.0 - smoothstep(0.35, 1.05, radius);
                float billows = smoothstep(0.28, 0.61, density);
                float shade = Turbulence(flow * 0.95 + curl * 1.5 + float2(0.23, 0.15));
                float light = saturate((density - shade) * 4.0 + 0.45);
                float heat = pow(saturate(density * 1.15), 2.0) * lerp(0.3, 1.0, light);
                half3 color = lerp(_RootColor.rgb, _TipColor.rgb, heat);
                float rootHeat = 1.0 - smoothstep(0.0, _FieldHeight * 0.55,
                    input.worldY - _GroundY);
                rootHeat = pow(rootHeat, 1.4) * smoothstep(0.3, 0.65, density);
                color += _BaseColor.rgb * rootHeat * _BaseGlow;
                float alpha = envelope * billows * _Opacity * input.color.a * ground;
                return half4(color * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
