#pragma once

#define CELLTYPE_EMPTY      0
#define CELLTYPE_LEAF       1
#define CELLTYPE_ROOT       2
#define CELLTYPE_ANTENNA    3
#define CELLTYPE_WOOD       4
#define CELLTYPE_SPROUT     5
#define CELLTYPE_SEED       6

#define CELLTYPE_EMPTY_MASK     (1 << CELLTYPE_EMPTY)
#define CELLTYPE_LEAF_MASK      (1 << CELLTYPE_LEAF)
#define CELLTYPE_ROOT_MASK      (1 << CELLTYPE_ROOT)
#define CELLTYPE_ANTENNA_MASK   (1 << CELLTYPE_ANTENNA)
#define CELLTYPE_WOOD_MASK      (1 << CELLTYPE_WOOD)
#define CELLTYPE_SPROUT_MASK    (1 << CELLTYPE_SPROUT)
#define CELLTYPE_SEED_MASK      (1 << CELLTYPE_SEED)
#define CELLTYPE_ALL_MASK       0xFFFFFFFFu
#define CELLTYPE_VALID_MASK     (CELLTYPE_ALL_MASK & !CELLTYPE_EMPTY_MASK)

#define GENES_NUM 32

#define DIR_F   0u
#define DIR_R   1u
#define DIR_B   2u
#define DIR_L   3u

#define SPEED_NO 0u
#define SPEED_SLOW 1u
#define SPEED_FAST 2u

#define CMD_SET_FAIL_GENE 1u
#define CMD_SKIP 2u
#define CMD_GROW 3u
#define CMD_MOVE 4u

#define PARENT_NO 0xFFFFFFFFu

struct Cell {
    uint cellType; // [0,6] 3 bits
    float energy;
    uint energyFlow; // absolute // out flow [0,15] first 4 bits; in flow [0,15] second 4 bits
    uint parentDir; // [0,3] 2 bits
    uint genomeId;
    uint direction; // [0,3] 2 bits
    uint activeGene; // [0,31] 5 bits
    uint seedProps; // bytes 0 - speed; 2 - timer
};

struct CellInfo {
    Cell cell;
    float2 soil;
    uint2 debug;
};

struct Gene {
    uint growDirections; // bites with types in relative direction: 0 - forward, 1 - right, 2 - back, 3 - left
    uint conditions; // two conditions in two first bites
    float condParam1;
    float condParam2;
    uint condResult; // two commands in two first bites: 0 - command for success, 1 - command for fail, 2 - gene for success, 3 - gene for fail
    uint comGenes; // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail
    uint aloneCommands; // two commands in two first bites: 0 - for success, 1 - for fail
    uint aloneComGenes; // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail
    // uint pad0;
};

struct Genome {
    Gene genes[GENES_NUM];
    uint cellNum;
    uint pad0;
    uint pad1;
    uint pad2;
};

struct CommandEntry {
    uint commandId;
    uint successGene;
    uint failGene;
    uint executionFlags;
};

struct SimStats
{
    uint cellsCount;
    uint leafsCount;
    uint rootsCount;
    uint antennasCount;
    uint woodCount;
    uint sproutsCount;
    uint seedsCount;
    float cellEnergy;
    float soilOrganics;
    float soilEnergy;
    uint2 pad01;
};

// Размер группы
#define GROUP_SIZE 8
// Размер кэша с учетом соседей со всех сторон (+1 слева, +1 справа...)
#define CACHE_SIZE (GROUP_SIZE + 2)
