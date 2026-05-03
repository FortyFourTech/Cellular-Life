Shader "Simulation/World"
{
    Properties{
        _SoilTex("Soil Texture", 2D) = "white" {}
        _OrganicsColor("Organics Color", Color) = (0.0,1.0,0.0,1)
        _EnergyColor("Energy Color", Color) = (0.0,0.5,1.0,1)
        _LeafColor("Leaf Color", Color) = (0.0,1.0,0.0,1)
        _RootColor("Root Color", Color) = (1.0,0.0,0.0,1)
        _AntennaColor("Antenna Color", Color) = (0.0,0.0,1.0,1)
        _WoodColor("Wood Color", Color) = (0.5,0.5,0.5,1)
        _SproutColor("Sprout Color", Color) = (1.0,1.0,1.0,1)
        _SeedColor("Seed Color", Color) = (1.0,1.0,0.0,1)
        _OrgThreshold("Organics Threshold", Float) = 1.0
        _NrgThreshold("Energy Threshold", Float) = 1.0
        _Width("Width", Integer) = 1024
        _Height("Height", Integer) = 1024
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
            // #include "Common.hlsl"

            sampler2D _SoilTex;
            fixed4 _OrganicsColor;
            fixed4 _EnergyColor;
            float _OrgThreshold;
            float _NrgThreshold;
            fixed4 _LeafColor;
            fixed4 _RootColor;
            fixed4 _AntennaColor;
            fixed4 _WoodColor;
            fixed4 _SproutColor;
            fixed4 _SeedColor;
            int _Width;
            int _Height;
            float _ShowOrganics;
            float _ShowEnergy;

            StructuredBuffer<Cell> _Cells;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v) {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 val = tex2D(_SoilTex, i.uv).rg; // x organics, y energy
                float3 colOrg = val.x > _OrgThreshold
                    ? _OrganicsColor.xyz
                    : 0;// : val.x / _OrgThreshold;
                float3 colEn = val.y > _NrgThreshold
                    ? _EnergyColor.xyz
                    : 0;// : val.y / _NrgThreshold, _ShowEnergy-1;
                fixed4 col = fixed4(max(colOrg, colEn), 1);

                // highlight if organics exceed threshold
                uint cellIdx = floor(i.uv.y * _Height * _Width) + floor(i.uv.x * _Width);
                Cell c = _Cells[cellIdx];
                uint cellType = c.cellType;
                if (cellType == 1) col = _LeafColor; // leaf green
                if (cellType == 2) col = _RootColor; // root red
                if (cellType == 3) col = _AntennaColor; // antenna blue
                if (cellType == 4) col = _WoodColor; // wood grey
                if (cellType == 5) col = _SproutColor; // sprout
                if (cellType == 6) col = _SeedColor; // seed
                // return float4(1,0,0,1);
                float2 cellCenter = float2(
                    ceil(i.uv.x * _Width) / _Width,
                    ceil(i.uv.y * _Height) / _Height
                );
                // return float4(i.uv.xy,0,1);
                // return float4(cellCenter,0,1);
                return col;
                // return float4(1,1,1,1);
            }
            ENDCG
        }
    }
}
