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
    uint pad0;
    uint pad1;
    uint pad2;
}

Texture2D<float2> _SoilTexRO;
RWTexture2D<float2> _SoilTexWO;
RWTexture2D<float2> _SoilTexRW; // x=organics, y=soilEnergy
StructuredBuffer<Cell> _CellsRO;
RWStructuredBuffer<Cell> _CellsWO;
RWStructuredBuffer<Cell> _CellsRW;
RWStructuredBuffer<Genome> _Genomes; // packed genomes
RWStructuredBuffer<CommandEntry> _CommandBuffer;
RWStructuredBuffer<bool> _KillCells;
