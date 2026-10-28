Shader "Project/DimensionSlashRibbon"
{
    Properties
    {
        _ActionFrame ("Action Frame", Float) = 0
        _EndFrame ("End Frame", Float) = 90
        [HDR] _Purple ("Purple Glow", Color) = (0.55,0.08,1,1)
        [HDR] _Core ("Lavender Core", Color) = (1.5,1.1,2,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 timing:TEXCOORD1; float4 shape:TEXCOORD2; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 timing:TEXCOORD1; float4 shape:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
                float _ActionFrame, _EndFrame;
                half4 _Purple, _Core;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=input.uv; output.timing=input.timing; output.shape=input.shape;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float age=_ActionFrame-input.timing.x;
                clip(age);
                float reveal=saturate(age/max(1,input.timing.y));
                float decay=saturate((age-input.timing.y-input.timing.z)/max(1,input.timing.w));
                float fieldFade=saturate((_EndFrame-_ActionFrame)/8);
                float opacity=(1-smoothstep(0.55,1,decay))*fieldFade;
                clip(opacity-0.001);
                // Spatial quantization gives stepped pixel edges while time remains continuous.
                float2 uv=(floor(input.uv*input.shape.xy)+0.5)/input.shape.xy;
                float shrink=lerp(1,0.62,decay*decay);
                float u=(uv.x-0.5)/shrink+0.5;
                if(input.shape.z<0)u=1-u;
                float y=abs(uv.y-0.5)*8;
                float taper=pow(saturate(sin(saturate(u)*3.14159265)),0.45);
                float thickness=lerp(1,0.012,pow(decay,0.7));
                float edge=smoothstep(-0.035,0.035,reveal-u);
                float longitudinal=smoothstep(0,0.03,u)*(1-smoothstep(0.97,1,u));
                float distance=y/max(0.01,thickness*(0.2+0.8*taper));
                float core=1-smoothstep(0.13,0.25,distance);
                float rim=1-smoothstep(0.25,0.8,distance);
                float glow=exp2(-distance*distance*1.6)*0.4;
                // Keep the traveling glint compact in pixels even on screen-spanning cuts.
                float headRadius=clamp(input.shape.y*0.08,2,10);
                float2 headDistance=float2((u-reveal)*input.shape.x,(uv.y-0.5)*input.shape.y)/headRadius;
                float head=exp2(-dot(headDistance,headDistance)*2)*(1-step(0.999,reveal));
                half3 color=_Purple.rgb*(rim*0.65+glow)+_Core.rgb*(core+head*1.3);
                float alpha=saturate((core+rim*0.5+glow+head)*opacity*edge*longitudinal*input.shape.w);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
