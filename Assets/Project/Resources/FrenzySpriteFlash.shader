Shader "Project/FrenzySpriteFlash"
{
    Properties
    {
        _FlashOpacity ("Flash Opacity", Range(0,1)) = 1
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _FlashOpacity;
            struct Vertex { float4 position : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Interpolator { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed alpha : COLOR; };
            Interpolator Vert(Vertex v)
            {
                Interpolator o;
                o.position = UnityObjectToClipPos(v.position);
                o.uv = v.uv;
                o.alpha = v.color.a;
                return o;
            }
            fixed4 Frag(Interpolator i) : SV_Target
            {
                return fixed4(1, 1, 1, tex2D(_MainTex, i.uv).a * _FlashOpacity * i.alpha);
            }
            ENDCG
        }
    }
}
