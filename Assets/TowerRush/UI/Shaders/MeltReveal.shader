Shader "HeatRise/UI/MeltReveal"
{
    // Full-screen scene transition: a lava-colored circle grows from a UV point until it covers
    // the screen. Mirrors the mock's `clip-path: circle(0% -> 150% at 50% 100%)` melt wipe.
    Properties
    {
        [PerRendererData] _MainTex ("Lava Texture", 2D) = "white" {}
        _Center ("Center (UV)", Vector) = (0.5, 0, 0, 0)
        _Radius ("Radius (0-1, fraction of farthest-corner distance)", Range(0, 1.6)) = 0
        _Aspect ("Aspect (screenW/screenH)", Float) = 1.78
        _Tiling ("Tiling", Vector) = (1.4, 1.4, 0, 0)
        _ScrollSpeed ("Scroll Speed (UV/sec)", Vector) = (0.02, 0.05, 0, 0)
        _EdgeDarken ("Edge Darken", Range(0, 1)) = 0.5
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
            float2 _Tiling;
            float2 _ScrollSpeed;
            float _EdgeDarken;

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

                float2 uv = i.uv * _Tiling + _Time.y * _ScrollSpeed;
                fixed3 lava = tex2D(_MainTex, uv).rgb;
                lava *= lerp(1.15, 1.0 - _EdgeDarken, t); // brighter near the growing center, darker toward the rim
                float inside = step(dist, _Radius * maxDist);
                return fixed4(lava, inside);
            }
            ENDCG
        }
    }
}
