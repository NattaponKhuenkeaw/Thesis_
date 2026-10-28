Shader "Project/ExecutionBreakdownMask"
{
    Properties
    {
        _ActionFrame ("Action Frame", Float) = 0
        _EndFrame ("End Frame", Float) = 30
        _FlashColor ("Bright Overlay", Color) = (0.96,0.9,1,1)
        _EdgeColor ("Tear Edge", Color) = (0.55,0.16,1,1)
        _CutAngle ("Cut Angle", Float) = 104
    }
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
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
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float _ActionFrame, _EndFrame, _CutAngle;
                half4 _FlashColor, _EdgeColor;
            CBUFFER_END
            Varyings Vert(Attributes input)
            { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.uv=input.uv; return o; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Varyings input):SV_Target
            {
                float frame=max(0,_ActionFrame), duration=max(1,_EndFrame);
                float2 p=(floor(input.uv*float2(480,270))+0.5)/float2(480,270)-0.5;
                p.x*=_ScreenParams.x/_ScreenParams.y;
                float angle=radians(_CutAngle);
                float2 axis=float2(cos(angle),sin(angle)), normal=float2(-axis.y,axis.x);
                float across=dot(p,normal), along=dot(p,axis);
                float reveal=smoothstep(0,2,frame);
                float opening=lerp(0.14,0.008,saturate(frame/20));
                float jagged=(Hash(floor(float2(along*55,0)))-0.5)*0.018;
                float slit=(1-smoothstep(opening+jagged,opening+jagged+0.006,abs(across)))*reveal;
                // Irregular fragments cut real alpha holes in the bright layer;
                // the dark shape is the scene showing through, not painted black.
                float2 shardUV=float2(across*48,along*18);
                float2 cell=floor(shardUV), local=frac(shardUV)-0.5;
                float noise=Hash(cell);
                float dissolve=smoothstep(5,duration,frame);
                float shardSize=lerp(0.2,0.72,Hash(cell+7));
                float shard=1-step(shardSize,abs(local.x)*1.2+abs(local.y)*0.75);
                float fragments=step(noise,dissolve)*shard*(1-smoothstep(0.02,0.65,abs(across)));
                float fade=1-smoothstep(8,duration,frame);
                float edge=1-smoothstep(0.003,0.024,abs(abs(across)-opening-jagged));
                half3 color=lerp(_FlashColor.rgb,_EdgeColor.rgb,edge*0.65);
                float alpha=saturate(fade*(1-slit)*(1-fragments));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
