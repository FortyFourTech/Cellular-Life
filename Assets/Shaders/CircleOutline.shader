Shader "UI/CircleOutline"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                fixed4 color    : COLOR;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uvDelta = float2(ddx(i.uv.x), ddy(i.uv.y));
                // float2 texturePixelsPerScreenPixel = abs(uvDelta) * _MainTex_TexelSize.zw;
                float sizeX = _ScreenParams.x * abs(uvDelta.x);
                float sizeY = _ScreenParams.y * abs(uvDelta.y);

                // Transform UV to center around (0.5, 0.5)
                float2 centered = i.uv - 0.5;

                // Calculate distance from center
                float dist = length(centered);

                float outlineWidth = _OutlineWidth * min(sizeX, sizeY);
                // Define inner and outer radius for the outline
                float innerRadius = 0.5 - outlineWidth;
                float outerRadius = 0.5;

                float alpha = abs(dist - (outerRadius + innerRadius) * 0.5) <= (outlineWidth * 0.5);

                fixed4 col = _OutlineColor;
                col *= i.color;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}