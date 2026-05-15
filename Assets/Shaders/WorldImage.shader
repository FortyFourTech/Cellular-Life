Shader "Simulation/WorldOut"
{
    Properties{
        _MainTex("Main Texture", 2D) = "black" {}

        _Width("Width", Integer) = 1024
        _Height("Height", Integer) = 1024

        // _RenderIndividualCells("Render individual cells", Range(0,1)) = 0.0
        _IndividualCellsRenderSize("Minimal cell size to render individual cells", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Defines.hlsl"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            int _Width;
            int _Height;

            // float _RenderIndividualCells;
            float _IndividualCellsRenderSize;

            StructuredBuffer<Cell> _CellsRO;

            #include "CommonLib.hlsl"
            #include "RenderLib.hlsl"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v) {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texVal = tex2D(_MainTex, i.uv);
                fixed4 col = texVal;

                float2 uvDelta = float2(ddx(i.uv.x), ddy(i.uv.y));

                float2 texturePixelsPerScreenPixel = abs(uvDelta) * _MainTex_TexelSize.zw;

                float cellSizeX = 1 / texturePixelsPerScreenPixel.x;
                float cellSizeY = 1 / texturePixelsPerScreenPixel.y;
                float cellSize = min(cellSizeX, cellSizeY);

                bool renderIndividualCells = cellSize >= _IndividualCellsRenderSize;

                if (renderIndividualCells) {
                    float2 pixelPos = floor(i.uv * float2(_Width, _Height)) % float2(_Width,_Height);
                    uint cellIdx = (uint)pixelPos.y * (uint)_Width + (uint)pixelPos.x;
                    Cell cellData = _CellsRO[cellIdx];

                    // return float4(1,0,0,1);
                    // float2 cellBL = float2(
                    //     floor(i.uv.x * _Width) / _Width,
                    //     floor(i.uv.y * _Height) / _Height
                    // );
                    // float2 cellTR = float2(
                    //     ceil(i.uv.x * _Width) / _Width,
                    //     ceil(i.uv.y * _Height) / _Height
                    // );
                    float2 cellUV = float2(
                        frac(i.uv.x * (float)_Width),
                        frac(i.uv.y * (float)_Height)
                    );

                    fixed4 individualVal = fixed4(0,0,0,1);
                    for (int dir = 0; dir < 4; ++dir) {
                        bool outFlow = GetIntBit(cellData.energyFlow, dir);
                        READ_NEIGHBOR_CELL(pixelPos, dir);

                        uint neighborFlow = neighborCell.energyFlow;
                        bool inFlow = GetIntBit(neighborFlow, RotateDir(dir, DIR_B));

                        if (outFlow || inFlow) {
                            individualVal = RenderLine(cellUV, individualVal, fixed4(0.25,0.25,0.25,1.0), 0.35, dir);
                        }
                    }
                    fixed4 shape = RenderCell(cellUV, individualVal, texVal, cellData.cellType, cellData.direction);
                    col = lerp(texVal, shape, saturate(cellData.cellType));
                }
                // return float4(cellUV.xy,0,1);
                // return float4(cellCenter,0,1);
                // return val * float4(1,0,0,1);
                // return lerp(texVal, shape, saturate(cellData.cellType) * _RenderIndividualCells);
                // return float4(1,1,1,1);
                return col;
            }
            ENDCG
        }
    }
}
