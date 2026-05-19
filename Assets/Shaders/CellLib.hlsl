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

// #region Cache
void CacheCellsAround(uint2 groupThreadID, uint2 groupID) {
    // 1. ЗАГРУЗКА ДАННЫХ В КЭШ
    // Каждому потоку (их 256) нужно загрузить чуть больше одной ячейки,
    // чтобы заполнить кэш 18x18 (324 ячейки).
    for (int i = groupThreadID.y * GROUP_SIZE + groupThreadID.x; i < CACHE_SIZE * CACHE_SIZE; i += GROUP_SIZE * GROUP_SIZE)
    {
        int localX = i % CACHE_SIZE;
        int localY = i / CACHE_SIZE;

        // Вычисляем глобальные координаты в мире с учетом смещения -1
        int globalX = groupID.x * GROUP_SIZE + localX - 1;
        int globalY = groupID.y * GROUP_SIZE + localY - 1;

        // Безопасное чтение (проверка границ мира)
        globalX = (globalX + _Width) % _Width;
        globalY = (globalY + _Height) % _Height;

        CellCache[localX][localY] = _CellsRO[globalX + globalY * _Width];
    }

    // Ждем, пока ВСЕ потоки закончат запись в кэш
    GroupMemoryBarrierWithGroupSync();
}
void CacheCellsAndCommandsAround(uint2 groupThreadID, uint2 groupID) {
    // 1. ЗАГРУЗКА ДАННЫХ В КЭШ
    // Каждому потоку (их 256) нужно загрузить чуть больше одной ячейки,
    // чтобы заполнить кэш 18x18 (324 ячейки).
    for (int i = groupThreadID.y * GROUP_SIZE + groupThreadID.x; i < CACHE_SIZE * CACHE_SIZE; i += GROUP_SIZE * GROUP_SIZE)
    {
        int localX = i % CACHE_SIZE;
        int localY = i / CACHE_SIZE;

        // Вычисляем глобальные координаты в мире с учетом смещения -1
        int globalX = groupID.x * GROUP_SIZE + localX - 1;
        int globalY = groupID.y * GROUP_SIZE + localY - 1;

        // Безопасное чтение (проверка границ мира)
        globalX = (globalX + _Width) % _Width;
        globalY = (globalY + _Height) % _Height;

        uint cellIdx = globalX + globalY * _Width;
        CellCache[localX][localY] = _CellsRO[cellIdx];
        CommandCache[localX][localY] = _CommandBuffer[cellIdx];
    }

    // Ждем, пока ВСЕ потоки закончат запись в кэш
    GroupMemoryBarrierWithGroupSync();
}

uint2 ShiftCacheCoord(int2 inPos, uint dir) {
    static const int2 offsets[4] = {
        int2( 0,  1), // 0
        int2( 1,  0), // 1
        int2( 0, -1), // 2
        int2(-1,  0), // 3
    };

    int2 mapSize = int2(CACHE_SIZE,CACHE_SIZE);
    return (inPos + offsets[dir] + mapSize) % mapSize;
}

uint2 MoveCacheCoord(uint2 inPos, int2 offset) {
    int2 mapSize = int2(CACHE_SIZE,CACHE_SIZE);
    return (inPos + offset + mapSize) % mapSize;
}

uint CachePosToIdx(uint2 pos) {
    return pos.y * CACHE_SIZE + pos.x;
}

uint2 CacheIdxToPos(uint idx) {
    return uint2(idx % CACHE_SIZE, idx / CACHE_SIZE);
}

uint2 CachePos(uint2 groupThreadID) {
    return uint2(groupThreadID.x + 1, groupThreadID.y + 1);
}

#define READ_CACHED_NEIGHBOR_CELL(pos, dir) \
uint2 neighborPos = ShiftCacheCoord(pos, dir); \
Cell neighborCell = CellCache[neighborPos.x][neighborPos.y];

#define READ_CACHED_COMMAND(pos) \
CommandEntry neighborCmd = CommandCache[pos.x][pos.y];
// #endregion // Cache
