// Minimal unlit, vertex-coloured, transparent shader.
// Written in plain CG with no pipeline-specific includes so the same asset
// renders under the Built-in Render Pipeline and under URP. This avoids the
// magenta "shader not supported" material that appears when a package ships a
// shader tied to a single pipeline.
Shader "IsoGrid/Unlit Vertex Color"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4 // LessEqual
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull Off
        Lighting Off
        Offset -1, -1 // pushes the grid slightly toward the camera to fight z-fighting with the ground

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
