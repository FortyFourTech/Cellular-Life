#pragma once

#include "Defines.hlsl"

cbuffer _SimParams
{
    int _Width;
    int _Height;
    float _Timestep;
    float _Timestamp;
    float _Rand;
    int _GenomeCapacity;
    float _Sunlight;
    float _DiffusionRate;
}

RWTexture2D<float2> _SoilTexRead;// x=organics, y=soilEnergy
RWTexture2D<float2> _SoilTexWrite; // x=organics, y=soilEnergy
RWStructuredBuffer<Cell> _Cells;
RWStructuredBuffer<Genome> _Genomes; // packed genomes
