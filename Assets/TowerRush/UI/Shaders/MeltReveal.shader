Shader "HeatRise/UI/MeltReveal"
{
    // Full-screen scene transition: a lava-colored circle grows from a UV point until it covers
    // the screen. Mirrors the mock's `clip-path: circle(0% -> 150% at 50% 100%)` melt wipe.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Center ("Center (UV)", Vector) = (0.5, 0, 0, 0)
        _Radius ("Radius (0-1, fraction of farthest-corner distance)", Range(0, 1.6)) = 0
        _Aspect ("Aspect (screenW/screenH)", Float) = 1.78
        _ColorCenter ("Color Center", Color) = (1, 0.75, 0.30, 1)
        _ColorMid ("Color Mid", Color) = (1, 0.42, 0.10, 1)
        _ColorEdge ("Color Edge", Color) = (0.23, 0.06, 0.02, 1)
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
        Blend SrcAlpha OneMinusSrcAlpha
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
            float2 _Center;
            float _Radius;
            float _Aspect;
            fixed4 _ColorCenter;
            fixed4 _ColorMid;
            fixed4 _ColorEdge;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = i.uv - _Center;
                d.x *= _Aspect;
                float dist = length(d);

                float2 corner = float2(max(_Center.x, 1 - _Center.x) * _Aspect, max(_Center.y, 1 - _Center.y));
                float maxDist = max(length(corner), 0.0001);
                float t = saturate(dist / maxDist);

                fixed4 col = t < 0.45 ? lerp(_ColorCenter, _ColorMid, t / 0.45) : lerp(_ColorMid, _ColorEdge, (t - 0.45) / 0.55);
                float inside = step(dist, _Radius * maxDist);
                col.a *= inside;
                return col;
            }
            ENDCG
        }
    }
}
