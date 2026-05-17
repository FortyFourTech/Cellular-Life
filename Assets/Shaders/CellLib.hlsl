#pragma once

#include "Defines.hlsl"
#include "CommonLib.hlsl"
#include "GenomeLib.hlsl"

// #region Cell mutation
Cell CreateCell(uint2 targetPos, uint type, uint direction, uint parentDir, uint genomeId)
{
    Cell newCell = (Cell)0;
    newCell.cellType = type;

    // by default inherit parent's genome id
    newCell.genomeId = genomeId;
    newCell.activeGene = 0u;
    newCell.parentDir = parentDir;
    newCell.energy = _GrowNrg;
    newCell.energyFlow = 0u;
    newCell.direction = direction;
    newCell.seedProps = (SPEED_NO << 16u) | 32u;
    SetOutFlow(newCell, parentDir, parentDir < 4 && type < CELLTYPE_SPROUT);
    SetInFlow(newCell, parentDir, parentDir < 4 && type >= CELLTYPE_SPROUT);

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
