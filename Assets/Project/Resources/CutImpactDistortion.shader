Shader "Project/CutImpactDistortion"
{
    Properties
    {
        _ImpactCenterTime ("Center / Progress / Envelope", Vector) = (0.5,0.5,0,0)
        _ImpactWave ("Unused / Width / Refraction / Chromatic", Vector) = (0,0.08,6,2)
        _ImpactDetail ("Blur / Reference Height / Aspect", Vector) = (1,270,1.77778,0)
        _ImpactCut ("Cut Start / End", Vector) = (0,0,1,1)
        _ImpactShape ("Cut Half Width / Reveal", Vector) = (0,1,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Cut Impact Refraction"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ImpactCenterTime, _ImpactWave, _ImpactDetail, _ImpactCut, _ImpactShape;
            CBUFFER_END

            half4 ReadScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, saturate(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = ReadScene(uv);
                float2 start = _ImpactCut.xy, end = _ImpactCut.zw;
                #if UNITY_UV_STARTS_AT_TOP
                    start.y = 1 - start.y;
                    end.y = 1 - end.y;
                #endif
                float aspect = max(0.01, _ImpactDetail.z);
                float2 cutVector = (end - start)*float2(aspect,1);
                float lineLength = max(0.0001,length(cutVector));
                float2 axis = cutVector/lineLength, normal = float2(-axis.y,axis.x);
                float2 relative = (uv-start)*float2(aspect,1);
                float along = dot(relative,axis), across = dot(relative,normal);
                float2 direction = normal*(across < 0 ? -1 : 1);
                float progress = saturate(_ImpactCenterTime.z);
                float envelope = saturate(_ImpactCenterTime.w);
                float width = max(0.01,_ImpactWave.y);
                float front = (abs(across)-_ImpactShape.x)/width-progress*0.65;
                float band = exp2(-front*front*3);
                float headPosition = lineLength*saturate(_ImpactShape.y);
                float segment = smoothstep(-width,0,along)*(1-smoothstep(headPosition,headPosition+width,along));
                float headDistance = (along-headPosition)/(width*1.5);
                float head = exp2(-headDistance*headDistance*2)*(1-step(0.999,_ImpactShape.y));
                // The band follows the cut's revealed segment and shrinking edges; the glint leads it.
                float weight = saturate(band*segment*envelope*(0.75+head*0.5));
                if (weight < 0.002) return original;

                float2 pixel = float2(1/aspect, 1) / max(1, _ImpactDetail.y);
                float displacement = sin(front*3.14159265-progress*4)*band;
                float2 offset = direction * pixel * _ImpactWave.z * displacement * envelope * segment;
                // Move in reference pixels and sample nearest: the world keeps crisp pixel edges.
                offset = round(offset / pixel) * pixel;
                float2 warped = uv + offset;
                float2 split = round(direction*_ImpactWave.w*weight)*pixel;
                float2 blur = round(direction*_ImpactDetail.x*weight)*pixel;
                half4 middle = ReadScene(warped);
                half3 soft = (middle.rgb*2 + ReadScene(warped+blur).rgb + ReadScene(warped-blur).rgb)*0.25;
                half red = ReadScene(warped+split).r;
                half blue = ReadScene(warped-split).b;
                half3 color = lerp(middle.rgb, soft, weight);
                color.r = lerp(color.r, red, weight);
                color.b = lerp(color.b, blue, weight);
                return half4(color, original.a);
            }
            ENDHLSL
        }
    }
}
