Shader "Simulation/Soil"
{
    Properties{
        _SoilTex("Soil Texture", 2D) = "white" {}
        _OrganicsColor("Organics Color", Color) = (0.0,1.0,0.0,1)
        _EnergyColor("Energy Color", Color) = (0.0,0.5,1.0,1)
        _Blend("Blend", Range(0,1)) = 0.5 // 0 - organics, 1 - energy
        _Threshold("Highlight Threshold", Float) = 1.0
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

            sampler2D _SoilTex;
            fixed4 _OrganicsColor;
            fixed4 _EnergyColor;
            float _Blend;
            float _Threshold;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v) {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 val = tex2D(_SoilTex, i.uv).rg; // x organics, y energy
                fixed3 colOrg = val.x > _Threshold ? _OrganicsColor.xyz : (val.x / _Threshold).xxx;
                fixed3 colEn = val.y > _Threshold ? _EnergyColor.xyz : (val.y / _Threshold).xxx;
                fixed3 colComb = fixed3(colOrg.x, colEn.y, 0);
                fixed3 col = lerp(colOrg, colComb, _Blend * 2);
                if (_Blend > 0.5)
                {
                    col = lerp(colComb, colEn, (_Blend-0.5) * 2);
                }
                // highlight if organics exceed threshold
                // return float4(val.xy, 0, 1);
                // return float4(1,0,0,1);
                return fixed4(col.xyz, 1);
                // return float4(1,1,1,1);
            }
            ENDCG
        }
    }
}
