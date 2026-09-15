Shader "HeatRise/Gris Modelado"
{
    Properties { _Color ("Gris", Color) = (0.66, 0.66, 0.66, 1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Back ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct VertexIn { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct VertexOut { float4 vertex : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            VertexOut vert(VertexIn v)
            {
                VertexOut o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(VertexOut i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float key = saturate(dot(n, normalize(float3(-0.45, 0.85, 0.4))));
                float fill = saturate(dot(n, normalize(float3(0.8, 0.35, -0.7))));
                return fixed4(_Color.rgb * (0.38 + key * 0.53 + fill * 0.18), 1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
