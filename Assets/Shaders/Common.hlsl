#pragma once

#include "Defines.hlsl"

// Globals
// _Cells;
// _Genomes;
// _SoilTexRead;
// _SoilTexWrite;
// _Width;
// _Height;

float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float randFloat(uint2 cellPos, float salt)
{
    return
        _Rand * (float(asint(salt) >> 0 & 1u) * 2.0 - 1.0)
        + asfloat(cellPos.x) * (float(asint(salt) >> 1 & 1u) * 2.0 - 1.0)
        + asfloat(cellPos.y) * (float(asint(salt) >> 2 & 1u) * 2.0 - 1.0)
        + salt * (float(asint(salt) >> 3 & 1u) * 2.0 - 1.0);
    // float2 seed = cellPos + float2(salt, 0.0);
    // float random_val = hash12(seed);
    // return random_val;
}

uint randInt(uint2 cellPos, float salt)
{
    return asuint(randFloat(cellPos, salt));
}

uint PosToIdx(uint2 pos) {
    return pos.y * _Width + pos.x;
}

uint2 IdxToPos(uint idx) {
    return uint2(idx % _Width, idx / _Width);
}

// #region Packing
// uint UnpackFloat1(float data) {
//     return (asuint(data) >> 0) & 0xFF;
// }
// uint UnpackFloat2(float data) {
//     return (asuint(data) >> 8) & 0xFF;
// }
// uint UnpackFloat3(float data) {
//     return (asuint(data) >> 16) & 0xFF;
// }
// uint UnpackFloat4(float data) {
//     return (asuint(data) >> 24) & 0xFF;
// }

uint GetIntByte(uint data, uint byteIdx) {
    return (data >> byteIdx * 8) & 0xFF;
}

uint GetIntBit(uint data, uint bitIdx) {
    return (data >> bitIdx) & 1u;
}

uint UnpackInt1(uint data) {
    return GetIntByte(data, 0);
}
uint UnpackInt2(uint data) {
    return GetIntByte(data, 1);
}
uint UnpackInt3(uint data) {
    return GetIntByte(data, 2);
}
uint UnpackInt4(uint data) {
    return GetIntByte(data, 3);
}

// void PackToFloat1(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 0);
//     container = asfloat(bits);
// }
// void PackToFloat2(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 8);
//     container = asfloat(bits);
// }
// void PackToFloat3(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 16);
//     container = asfloat(bits);
// }
// void PackToFloat4(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 24);
//     container = asfloat(bits);
// }

void SetIntByte(inout uint container, uint data, uint byteIdx) {
    container |= (data & 0xff) << (byteIdx * 8);
}

void SetIntBit(inout uint container, uint data, uint bitIdx) {
    container |= (data & 1u) << (bitIdx);
}

void PackToInt1(inout uint container, uint data) {
    SetIntByte(container, data, 0);
}
void PackToInt2(inout uint container, uint data) {
    SetIntByte(container, data, 1);
}
void PackToInt3(inout uint container, uint data) {
    SetIntByte(container, data, 2);
}
void PackToInt4(inout uint container, uint data) {
    SetIntByte(container, data, 3);
}

uint MergeByMask(uint a, uint b, uint mask) {
    return (a & ~mask) | (b & mask);
}

uint GetBit(uint value, uint bitIdx) {
    return (value >> bitIdx) & 1u;
}
// #endregion // Packing

// #region Direction
// 0 10 ( 0, y) forward up
// 1 11 ( x, 0) right   right
// 2 00 ( 0,-y) back    down
// 3 01 (-x, 0) left    left
uint RotateDir(in uint baseDir, in uint relDir) {
    return (baseDir + relDir + 4) % 4;
}

uint2 ShiftCoord(in uint2 inPos, in uint dir) {
    static const int2 offsets[4] = {
        int2( 0,  1), // 0
        int2( 1,  0), // 1
        int2( 0, -1), // 2
        int2(-1,  0), // 3
    };

    uint2 mapSize = uint2(_Width,_Height);
    return (inPos + (uint2)offsets[dir] + mapSize) % mapSize;
}
// #endregion // Direction

// #region Genome funcs
Gene GenerateRandomGene(in uint2 cellPos, uint geneIdx)
{
    Gene gene = (Gene)0;
    gene.growDirections = randInt(cellPos, _Timestamp + geneIdx);
    gene.conditions = randInt(cellPos, _Timestamp * 2 + geneIdx);
    gene.condParam1 = randFloat(cellPos, _Timestamp * 3 + geneIdx);
    gene.condParam2 = randFloat(cellPos, _Timestamp * 4 + geneIdx);
    gene.condResult = randInt(cellPos, _Timestamp * 5 + geneIdx);
    gene.comGenes = randInt(cellPos, _Timestamp * 6 + geneIdx);
    gene.aloneCommands = randInt(cellPos, _Timestamp * 7 + geneIdx);
    gene.aloneComGenes = randInt(cellPos, _Timestamp * 8 + geneIdx);
    // TODO: optimize randomization to make less randInt() calls

    return gene;
}

Genome GenerateRandomGenome(in uint2 cellPos)
{
    Genome genome = (Genome)0;
    for (int gIdx = 0; gIdx < 32; gIdx++)
    {
        genome.genes[gIdx] = GenerateRandomGene(cellPos, gIdx);
    }
    genome.cellNum = 0;

    return genome;
}

Gene MutateGene(in uint2 cellPos, in Gene gene)
{
    uint randIdx = randInt(cellPos, _Timestamp) % 22;
    uint randVal = randInt(cellPos, _Timestamp*2);
    // select what to mutate
    // change value
    gene.growDirections = randIdx == 1 ? MergeByMask(gene.growDirections, randVal, 0x000000ff) : gene.growDirections;
    gene.growDirections = randIdx == 2 ? MergeByMask(gene.growDirections, randVal, 0x0000ff00) : gene.growDirections;
    gene.growDirections = randIdx == 3 ? MergeByMask(gene.growDirections, randVal, 0x00ff0000) : gene.growDirections;
    gene.growDirections = randIdx == 4 ? MergeByMask(gene.growDirections, randVal, 0xff000000) : gene.growDirections;
    gene.conditions = randIdx == 5 ? MergeByMask(gene.conditions, randVal, 0x000000ff) : gene.conditions;
    gene.conditions = randIdx == 6 ? MergeByMask(gene.conditions, randVal, 0x0000ff00) : gene.conditions;
    gene.condParam1 = randIdx == 7 ? randVal : gene.condParam1;
    gene.condParam2 = randIdx == 8 ? randVal : gene.condParam2;
    gene.condResult = randIdx == 9 ? MergeByMask(gene.condResult, randVal, 0x000000ff) : gene.condResult;
    gene.condResult = randIdx == 10 ? MergeByMask(gene.condResult, randVal, 0x0000ff00) : gene.condResult;
    gene.condResult = randIdx == 11 ? MergeByMask(gene.condResult, randVal, 0x00ff0000) : gene.condResult;
    gene.condResult = randIdx == 12 ? MergeByMask(gene.condResult, randVal, 0xff000000) : gene.condResult;
    gene.comGenes = randIdx == 13 ? MergeByMask(gene.comGenes, randVal, 0x000000ff) : gene.comGenes;
    gene.comGenes = randIdx == 14 ? MergeByMask(gene.comGenes, randVal, 0x0000ff00) : gene.comGenes;
    gene.comGenes = randIdx == 15 ? MergeByMask(gene.comGenes, randVal, 0x00ff0000) : gene.comGenes;
    gene.comGenes = randIdx == 16 ? MergeByMask(gene.comGenes, randVal, 0xff000000) : gene.comGenes;
    gene.aloneCommands = randIdx == 17 ? MergeByMask(gene.aloneCommands, randVal, 0x000000ff) : gene.aloneCommands;
    gene.aloneCommands = randIdx == 18 ? MergeByMask(gene.aloneCommands, randVal, 0x0000ff00) : gene.aloneCommands;
    gene.aloneComGenes = randIdx == 19 ? MergeByMask(gene.aloneComGenes, randVal, 0x000000ff) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 20 ? MergeByMask(gene.aloneComGenes, randVal, 0x0000ff00) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 21 ? MergeByMask(gene.aloneComGenes, randVal, 0x00ff0000) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 22 ? MergeByMask(gene.aloneComGenes, randVal, 0xff000000) : gene.aloneComGenes;
    return gene;
}

// Allocate a new genome slot in _Genomes. Tries to find a slot with zero refs
// and falls back to circular overwrite if none free.
uint AllocateGenomeSlot(in Genome genome)
{
    // uint prev;
    // uint start = prev % (uint)_GenomeCapacity;

    uint idx = 0;
    for (uint i = 0; i < (uint)_GenomeCapacity; ++i) {
        if (_Genomes[idx].cellNum == 0u) {
            _Genomes[idx] = genome;
            idx = i;
        }
    }

    // fallback overwrite
    // idx = start;
    _Genomes[idx] = genome;
    return idx;
}

// Mutate a genome and allocate it into the genomes buffer. Returns new genome id.
uint MutateGenome(in uint2 cellPos, in Genome inGenome)
{
    // Genome newGenome = inGenome;
    // select random gene
    uint randIdx = randInt(cellPos, _Timestamp) % 32;
    Gene gene = inGenome.genes[randIdx];
    gene = MutateGene(cellPos, gene);
    Gene genomeGenes[] = inGenome.genes;
    genomeGenes[randIdx] = gene;
    inGenome.genes = genomeGenes;

    uint newId = AllocateGenomeSlot(inGenome);
    return newId;
}
// #endregion // Genome funcs

// #region Cell mutation
void CreateCell(in uint2 targetPos, uint type, uint direction, uint parentDir)
{
    uint2 parentPos = ShiftCoord(targetPos, parentDir);
    uint parentIdx = parentPos.y * _Width + parentPos.x;
    uint targetIdx = targetPos.y * _Width + targetPos.x;

    Cell newCell = (Cell)0;
    newCell.cellType = type;

    // by default inherit parent's genome id
    newCell.genomeId = _Cells[parentIdx].genomeId;
    newCell.parentDir = parentDir;
    newCell.energy = 0.5;
    SetIntBit(newCell.energyFlow, type != CELLTYPE_SPROUT && type != CELLTYPE_SEED, parentDir);

    // TODO: try to mutate
    // only on Sprout or Seed cells
    if ((type == CELLTYPE_SPROUT || type == CELLTYPE_SEED)
        && (randInt(targetPos, _Timestamp) & 0xff == 0)) { // 25% to mutate
        uint parentGid = _Cells[parentIdx].genomeId;
        Genome parentGenome = _Genomes[parentGid];
        uint newGid = MutateGenome(targetPos, parentGenome);
        newCell.genomeId = newGid;
        // increment ref for new genome
        InterlockedAdd(_Genomes[newGid].cellNum, 1);
    } else {
        // increment ref for inherited genome
        InterlockedAdd(_Genomes[newCell.genomeId].cellNum, 1);
    }

    _Cells[targetIdx] = newCell;
}

void KillCell(in uint2 cellPos)
{
    uint cellIdx = cellPos.y * _Width + cellPos.x;
    // Cell cell = _Cells[cellIdx];

    // set parent dir to -1 on neighbors if it was their parent
    for (int dir = 0; dir < 4; ++dir) {
        int toNeighbor = dir;
        int fromNeighbor = RotateDir(toNeighbor, 2);
        uint2 neighborPos = ShiftCoord(cellPos, toNeighbor);
        uint neighborIdx = neighborPos.y * _Width + neighborPos.x;

        if (_Cells[neighborIdx].cellType == 0) continue;

        _Cells[neighborIdx].parentDir = _Cells[neighborIdx].parentDir == fromNeighbor ? 0xFFFFFFFF : _Cells[neighborIdx].parentDir;
        if (GetIntBit(_Cells[neighborIdx].energyFlow, fromNeighbor))
            SetIntBit(_Cells[neighborIdx].energyFlow, 0, fromNeighbor);
    }
    // spit energy from the cell to soil
    // spit organics to soil

    float2 cur = _SoilTexRead[cellPos];
    cur.x += 0.1; // organics
    cur.y += _Cells[cellIdx].energy; // energy
    _SoilTexWrite[cellPos] = cur; // write to write-target; commit swap on CPU

    // decrement genome refcount
    uint oldGid = _Cells[cellIdx].genomeId;
    InterlockedAdd(_Genomes[oldGid].cellNum, (uint)-1);

    // clear cell
    _Cells[cellIdx] = (Cell)0;
}
// #endregion // Cell mutation
