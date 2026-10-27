Shader "Project/LobbyStreetBackdrop"
{
    Properties { [PerRendererData] _MainTex ("Street Image", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
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
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv * float2(1, 2) - float2(0, 0.5);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(input.uv));
                float upper = smoothstep(1.0, 1.4, input.uv.y);
                float lower = 1.0 - smoothstep(-0.4, 0.0, input.uv.y);
                color.rgb = lerp(color.rgb, half3(0.035, 0.08, 0.25), upper);
                color.rgb = lerp(color.rgb, half3(0.025, 0.045, 0.12), lower);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }
    }
}
