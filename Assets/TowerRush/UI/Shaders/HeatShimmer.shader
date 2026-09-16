Shader "HeatRise/UI/HeatShimmer"
{
    // Horizontally scrolling 4-stop gradient, screen-blended over the menu background.
    // Mirrors the mock's `shimmerMove` keyframe (background-position 0% -> 200%, 6s linear infinite).
    Properties
    {
        _ColorA ("Color A", Color) = (1, 0.54, 0.10, 0.2)
        _ColorB ("Color B", Color) = (1, 0.69, 0.30, 0.33)
        _ColorC ("Color C", Color) = (1, 0.42, 0.10, 0.2)
        _ColorD ("Color D", Color) = (1, 0.82, 0.48, 0.27)
        _ScrollSpeed ("Scroll Speed (cycles/sec)", Float) = 0.1667
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

            fixed4 _ColorA, _ColorB, _ColorC, _ColorD;
            float _ScrollSpeed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = frac(i.uv.x - _Time.y * _ScrollSpeed) * 4.0;
                fixed4 c0 = x < 1 ? lerp(_ColorD, _ColorA, x) : x < 2 ? lerp(_ColorA, _ColorB, x - 1) :
                            x < 3 ? lerp(_ColorB, _ColorC, x - 2) : lerp(_ColorC, _ColorD, x - 3);
                return c0;
            }
            ENDCG
        }
    }
}
