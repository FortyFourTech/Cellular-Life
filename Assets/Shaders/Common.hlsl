#pragma once

#include "Defines.hlsl"

#define READ_CELL(pos) \
uint2 cellPos = pos; \
uint cellIdx = PosToIdx(cellPos); \
Cell cell = _CellsRO[cellIdx];

#define READ_SOIL(pos) \
uint2 cellPos = pos; \
float2 soil = _SoilTexRO[cellPos];

#define READ_CELL_SOIL(pos) \
uint2 cellPos = pos; \
uint cellIdx = PosToIdx(cellPos); \
Cell cell = _CellsRO[cellIdx]; \
float2 soil = _SoilTexRO[cellPos];

float hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

uint randInt(uint2 cellPos, float salt)
{
    // Создаем начальное зерно из всех доступных данных
    // _Timestamp и _Rand лучше привести к uint бинарно
    uint seed = cellPos.x + cellPos.y * 1103515245u + asuint(salt) + asuint(_Timestamp) + asuint(_Rand);

    // Алгоритм PCG (очень быстрый и качественный)
    uint state = seed * 747796405u + 2891336453u;
    uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
    uint result = (word >> 22u) ^ word;

    return result;
}

float randFloat(uint2 cellPos, float salt)
{
    return float(randInt(cellPos, salt)) / 4294967295.0;
    // float2 seed = cellPos + float2(salt + _Timestamp + _Rand, 0.0);
    // float random_val = hash12(seed);
    // return random_val;
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

uint MergeByMask(uint a, uint b, uint mask) {
    return (a & ~mask) | (b & mask);
}

uint GetIntBit(uint data, uint bitIdx) {
    return (data >> bitIdx) & 1u;
}

uint GetIntByte(uint data, uint byteIdx) {
    return (data >> (byteIdx * 8u)) & 0xffu;
}

uint UnpackInt1(uint data) {
    return GetIntByte(data, 0u);
}
uint UnpackInt2(uint data) {
    return GetIntByte(data, 1u);
}
uint UnpackInt3(uint data) {
    return GetIntByte(data, 2u);
}
uint UnpackInt4(uint data) {
    return GetIntByte(data, 3u);
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

void SetIntBit(inout uint container, uint data, uint bitIdx) {
    container = MergeByMask(container, data << bitIdx, 1u << bitIdx);
}

void SetIntByte(inout uint container, uint data, uint byteIdx) {
    container = MergeByMask(container, data << (byteIdx * 8u), 0xffu << (byteIdx * 8u));
}

void PackToInt1(inout uint container, uint data) {
    SetIntByte(container, data, 0u);
}
void PackToInt2(inout uint container, uint data) {
    SetIntByte(container, data, 1u);
}
void PackToInt3(inout uint container, uint data) {
    SetIntByte(container, data, 2u);
}
void PackToInt4(inout uint container, uint data) {
    SetIntByte(container, data, 3u);
}
// #endregion // Packing

// #region Direction
// 0 10 ( 0, y) forward up
// 1 11 ( x, 0) right   right
// 2 00 ( 0,-y) back    down
// 3 01 (-x, 0) left    left
uint RotateDir(uint baseDir, int relDir) {
    return (baseDir + relDir + 4) % 4;
}

uint2 ShiftCoord(uint2 inPos, uint dir) {
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

// #region Cell analysis
bool IsCellSingle(uint2 cellPos, in Cell cell) {
    return cell.parentDir == 0xFFFFFFFFu && cell.energyFlow == 0;
}
bool IsCellReceiveEnergy(uint2 cellPos, in Cell cell) {
    bool result = false;
    for (int dir = 0; dir < 4; ++dir) {
        uint2 neighborPos = ShiftCoord(cellPos, dir);
        uint neighborIdx = PosToIdx(neighborPos);
        uint neighborFlow = _CellsRO[neighborIdx].energyFlow;
        result = result || GetIntBit(neighborFlow,dir);
    }
    return result;
}
bool IsCellSendEnergy(uint2 cellPos, in Cell cell) {
    return cell.energyFlow > 0;
}
// #endregion // Cell analysis

// #region Genome funcs
// Allocate a new genome slot _Genomes. Tries to find a slot with zero refs
// and overrides genomeId if found one.
uint AllocateGenomeSlot(Genome genome, uint sourceGenomeId)
{
    for (uint i = 0; i < _Width*_Height; ++i) {
        uint cellNum;
        InterlockedCompareExchange(_Genomes[i].cellNum, 0, 1, cellNum);
        if (cellNum == 0u) {
            genome.cellNum = 1;
            _Genomes[i] = genome;
            return i;
        }
    }

    InterlockedAdd(_Genomes[sourceGenomeId].cellNum, 1);
    return sourceGenomeId;
}

Gene GenerateRandomGene(uint2 cellPos, uint geneIdx)
{
    Gene gene = (Gene)0;
    gene.growDirections = randInt(cellPos, geneIdx);
    gene.conditions = randInt(cellPos, 2 + geneIdx);
    gene.condParam1 = randFloat(cellPos, 3 + geneIdx) * 255;
    gene.condParam2 = randFloat(cellPos, 4 + geneIdx) * 255;
    gene.condResult = randInt(cellPos, 5 + geneIdx);
    gene.comGenes = randInt(cellPos, 6 + geneIdx);
    gene.aloneCommands = randInt(cellPos, 7 + geneIdx);
    gene.aloneComGenes = randInt(cellPos, 8 + geneIdx);
    // TODO: optimize randomization to make less randInt() calls

    return gene;
}

Genome GenerateRandomGenome(uint2 cellPos)
{
    Genome genome = (Genome)0;
    for (int gIdx = 0; gIdx < 32; gIdx++)
    {
        genome.genes[gIdx] = GenerateRandomGene(cellPos, gIdx);
    }
    genome.cellNum = 0;

    return genome;
}

uint AllocateRandomGenome(uint2 cellPos)
{
    return AllocateGenomeSlot(GenerateRandomGenome(cellPos), 0);
}


Gene MutateGene(uint2 cellPos, Gene gene)
{
    uint randIdx = randInt(cellPos, 100) % 22;
    uint randVal = randInt(cellPos, 101);
    // select what to mutate
    // change value
    gene.growDirections = randIdx == 0 ? MergeByMask(gene.growDirections, randVal, 0x000000ff) : gene.growDirections;
    gene.growDirections = randIdx == 1 ? MergeByMask(gene.growDirections, randVal, 0x0000ff00) : gene.growDirections;
    gene.growDirections = randIdx == 2 ? MergeByMask(gene.growDirections, randVal, 0x00ff0000) : gene.growDirections;
    gene.growDirections = randIdx == 3 ? MergeByMask(gene.growDirections, randVal, 0xff000000) : gene.growDirections;
    gene.conditions = randIdx == 4 ? MergeByMask(gene.conditions, randVal, 0x000000ff) : gene.conditions;
    gene.conditions = randIdx == 5 ? MergeByMask(gene.conditions, randVal, 0x0000ff00) : gene.conditions;
    gene.condParam1 = randIdx == 6 ? randVal : gene.condParam1;
    gene.condParam2 = randIdx == 7 ? randVal : gene.condParam2;
    gene.condResult = randIdx == 8 ? MergeByMask(gene.condResult, randVal, 0x000000ff) : gene.condResult;
    gene.condResult = randIdx == 9 ? MergeByMask(gene.condResult, randVal, 0x0000ff00) : gene.condResult;
    gene.condResult = randIdx == 10 ? MergeByMask(gene.condResult, randVal, 0x00ff0000) : gene.condResult;
    gene.condResult = randIdx == 11 ? MergeByMask(gene.condResult, randVal, 0xff000000) : gene.condResult;
    gene.comGenes = randIdx == 12 ? MergeByMask(gene.comGenes, randVal, 0x000000ff) : gene.comGenes;
    gene.comGenes = randIdx == 13 ? MergeByMask(gene.comGenes, randVal, 0x0000ff00) : gene.comGenes;
    gene.comGenes = randIdx == 14 ? MergeByMask(gene.comGenes, randVal, 0x00ff0000) : gene.comGenes;
    gene.comGenes = randIdx == 15 ? MergeByMask(gene.comGenes, randVal, 0xff000000) : gene.comGenes;
    gene.aloneCommands = randIdx == 16 ? MergeByMask(gene.aloneCommands, randVal, 0x000000ff) : gene.aloneCommands;
    gene.aloneCommands = randIdx == 17 ? MergeByMask(gene.aloneCommands, randVal, 0x0000ff00) : gene.aloneCommands;
    gene.aloneComGenes = randIdx == 18 ? MergeByMask(gene.aloneComGenes, randVal, 0x000000ff) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 19 ? MergeByMask(gene.aloneComGenes, randVal, 0x0000ff00) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 20 ? MergeByMask(gene.aloneComGenes, randVal, 0x00ff0000) : gene.aloneComGenes;
    gene.aloneComGenes = randIdx == 21 ? MergeByMask(gene.aloneComGenes, randVal, 0xff000000) : gene.aloneComGenes;
    return gene;
}

// Mutate a genome and allocate it into the genomes buffer. Returns new genome id.
uint MutateGenome(uint2 cellPos, uint genomeId)
{
    Genome newGenome = _Genomes[genomeId];
    newGenome.cellNum = 0;
    // select random gene
    uint randIdx = randInt(cellPos, _Timestamp) % 32;
    Gene gene = newGenome.genes[randIdx];
    gene = MutateGene(cellPos, gene);
    Gene genomeGenes[] = newGenome.genes;
    genomeGenes[randIdx] = gene;
    newGenome.genes = genomeGenes;

    uint newId = AllocateGenomeSlot(newGenome, genomeId);
    return newId;
}
// #endregion // Genome funcs

// #region Cell mutation
Cell CreateCell(uint2 targetPos, uint type, uint direction, uint parentDir, uint genomeId)
{
    Cell newCell = (Cell)0;
    newCell.cellType = type;

    // by default inherit parent's genome id
    newCell.genomeId = genomeId;
    newCell.activeGene = 0u;
    newCell.parentDir = parentDir;
    newCell.energy = ENERGY_GROW;
    newCell.direction = direction;
    newCell.seedProps = (SPEED_NO << 16u) | 32u;
    SetIntBit(newCell.energyFlow, type != CELLTYPE_SPROUT && type != CELLTYPE_SEED, parentDir);

    // mutate only on Sprout or Seed cells
    if (type >= CELLTYPE_SPROUT) {
        if ((randInt(targetPos, _Timestamp) & 3u) == 0) { // 25% to mutate
            // uint parentGid = _CellsRO[parentIdx].genomeId;
            // Genome parentGenome = _Genomes[parentGid];
            uint newGid = MutateGenome(targetPos, genomeId);
            newCell.genomeId = newGid;
            // increment ref for new genome
            // InterlockedAdd(_Genomes[newGid].cellNum, 1); // already incremented Mutate function
        } else {
            // increment ref for inherited genome
            InterlockedAdd(_Genomes[newCell.genomeId].cellNum, 1u);
        }
    }

    return newCell;
}

void RemoveCell(inout Cell cell) {
    // decrement genome refcount
    InterlockedAdd(_Genomes[cell.genomeId].cellNum, (uint)-1 * (cell.cellType >= CELLTYPE_SPROUT));

    // clear cell
    // cell = (Cell)0;
}

void KillCell(uint2 cellPos) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _CellsRO[cellIdx];
    _KillCells[cellIdx] = 1;
}

void ConvertToSeed(inout Cell cell, uint speed, uint timer) {
    InterlockedAdd(_Genomes[cell.genomeId].cellNum, cell.cellType < CELLTYPE_SPROUT ? 1 : 0);
    cell.cellType = CELLTYPE_SEED;
    cell.seedProps = (speed << 16) | timer;
}

void ConvertToSprout(inout Cell cell) {
    InterlockedAdd(_Genomes[cell.genomeId].cellNum, cell.cellType < CELLTYPE_SPROUT ? 1 : 0);
    cell.cellType = CELLTYPE_SPROUT;
    cell.activeGene = 0u;
}

void ConvertToWood(inout Cell cell) {
    InterlockedAdd(_Genomes[cell.genomeId].cellNum, cell.cellType >= CELLTYPE_SPROUT ? -1 : 0);
    cell.cellType = CELLTYPE_WOOD;
}
// #endregion // Cell mutation
