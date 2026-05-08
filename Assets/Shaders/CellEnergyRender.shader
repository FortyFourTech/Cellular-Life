Shader "Simulation/CellEnergy"
{
    Properties{
        _Width("Width", Integer) = 1024
        _Height("Height", Integer) = 1024
        _UpperBound("Upper bound", Float) = 1.0
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

            int _Width;
            int _Height;
            float _UpperBound;

            StructuredBuffer<Cell> _Cells;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v) {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pixelPos = floor(i.uv * float2(_Width, _Height));
                uint cellIdx = (uint)pixelPos.y * (uint)_Width + (uint)pixelPos.x;
                // uint cellIdx = floor(i.uv.y * _Height * _Width) + floor(i.uv.x * _Width);
                Cell c = _Cells[cellIdx];
                float energyVal = c.cellType > 0 ? c.energy : 0;
                energyVal /= _UpperBound;

                // highlight if organics exceed threshold
                // return float4(i.uv.xy,0,1);
                // return float4(val.xy, 0, 1);
                // return float4(1,0,0,1);
                return fixed4(energyVal.xxx, 1);
                // return float4(1,1,1,1);
            }
            ENDCG
        }
    }
}
