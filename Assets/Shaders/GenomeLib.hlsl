#pragma once

#include "Defines.hlsl"
#include "CommonLib.hlsl"

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

uint AllocateGenome(uint cellIdx)
{
    InterlockedAdd(_Genomes[cellIdx].cellNum, 1);
    return cellIdx;
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
