Shader "Simulation/Cells"
{
    Properties{
        _LeafColor("Leaf Color", Color) = (0.0,1.0,0.0,1)
        _RootColor("Root Color", Color) = (1.0,0.0,0.0,1)
        _AntennaColor("Antenna Color", Color) = (0.0,0.0,1.0,1)
        _WoodColor("Wood Color", Color) = (0.5,0.5,0.5,1)
        _SproutColor("Sprout Color", Color) = (1.0,1.0,1.0,1)
        _SeedColor("Seed Color", Color) = (1.0,1.0,0.0,1)
        _Width("Width", Float) = 1.0
        _Height("Height", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
// Upgrade NOTE: excluded shader from DX11, OpenGL ES 2.0 because it uses unsized arrays
// #pragma exclude_renderers d3d11 gles
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Defines.hlsl"

            fixed4 _LeafColor;
            fixed4 _RootColor;
            fixed4 _AntennaColor;
            fixed4 _WoodColor;
            fixed4 _SproutColor;
            fixed4 _SeedColor;
            float _Width;
            float _Height;

            StructuredBuffer<Cell> _Cells;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v) {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                Cell c = _Cells[i.uv.y * _Height * _Width + i.uv.x * _Width];
                uint cellType = c.cellType;

                fixed4 col = float4(0,0,0,1);

                if (cellType == 1) col = _LeafColor; // leaf green
                if (cellType == 2) col = _RootColor; // root red
                if (cellType == 3) col = _AntennaColor; // antenna blue
                if (cellType == 4) col = _WoodColor; // wood grey
                if (cellType == 5) col = _SproutColor; // sprout
                if (cellType == 6) col = _SeedColor; // seed

                // return float4(1,1,1,1);
                return col;
            }
            ENDCG
        }
    }
}
