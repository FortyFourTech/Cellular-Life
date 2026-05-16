#pragma once

#include "Defines.hlsl"

cbuffer _SimParams
{
    int _Width;
    int _Height;
    float _Timestep;
    float _Timestamp;
    float _Rand;
    float _Sunlight;
    float _DiffusionRate;
    float _CriticalOrg;
    float _CriticalNrg;
    uint3 _SimParamsPad012;
}

cbuffer _CellConstants {
    float _OrgAbsorbSpeed;      // 0.05
    float _NrgAbsorbSpeed;      // 0.05
    float _GrowNrg;             // 0.5
    float _LifeNrgSpend;        // 0.05
    float _LifeNrgSpendSeed;    // 0.005
    float _NrgTransportSpeed;   // 2.0
    float _NrgTransportMin;     // 1.0
    uint _CellConstantsPad0;
}
// #define CELL_ORG_COST       0.5

Texture2D<float2> _SoilTexRO; // x=organics, y=soilEnergy
RWTexture2D<float2> _SoilTexWO;
RWStructuredBuffer<Cell> _CellsRO;
RWStructuredBuffer<Cell> _CellsWO;
RWStructuredBuffer<Genome> _Genomes; // packed genomes
RWStructuredBuffer<CommandEntry> _CommandBuffer;
RWStructuredBuffer<bool> _KillCells;
RWStructuredBuffer<uint2> _Debug;
