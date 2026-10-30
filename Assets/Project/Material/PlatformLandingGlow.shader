Shader "Project/PlatformLandingGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture",2D)="white" {}
        _Color ("Glow Color",Color)=(.1,.8,1,.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes { COMMON_2D_INPUTS half4 color:COLOR; };
            struct Varyings { COMMON_2D_OUTPUTS_SHARED half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS=UnityFlipSprite(input.positionOS,unity_SpriteProps.xy);
                Varyings output=(Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv; output.color=input.color*_Color*unity_SpriteColor;
                return output;
            }
            half4 Fragment(Varyings input):SV_Target
            {
                half fade=smoothstep(0,.08,input.uv.x)*smoothstep(0,.08,1-input.uv.x);
                half light=exp2(-16*abs(input.uv.y-.5));
                return half4(input.color.rgb,input.color.a*light*fade);
            }
            ENDHLSL
        }
    }
}
