Shader "HeatRise/UI/HeatShimmer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Lava Texture", 2D) = "white" {}
        _Tiling ("Tiling", Vector) = (1.6, 1.6, 0, 0)
        _ScrollSpeed ("Scroll Speed (UV/sec)", Vector) = (0.03, 0.015, 0, 0)
        _Intensity ("Intensity", Range(0, 1)) = 0.4
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend OneMinusDstColor One
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float2 _Tiling;
            float2 _ScrollSpeed;
            float _Intensity;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv * _Tiling + _Time.y * _ScrollSpeed;
                fixed3 lava = tex2D(_MainTex, uv).rgb;
                return fixed4(lava * _Intensity, 1);
            }
            ENDCG
        }
    }
}
