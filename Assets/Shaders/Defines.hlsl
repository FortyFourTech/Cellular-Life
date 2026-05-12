#pragma once

#define CELLTYPE_LEAF       1
#define CELLTYPE_ROOT       2
#define CELLTYPE_ANTENNA    3
#define CELLTYPE_WOOD       4
#define CELLTYPE_SPROUT     5
#define CELLTYPE_SEED       6

#define ENERGY_GROW         0.5

#define ABSORB_SOIL_ORGANICS 0.05
#define ABSORB_SOIL_ENERGY  0.05
#define ABSORB_LIFE_ENERGY  0.05
#define ABSORB_LIFE_ENERGY_SEED  0.005

#define TRANSPORT_SPEED 2.0

#define GENES_NUM 32

#define DIR_F   0u
#define DIR_R   1u
#define DIR_B   2u
#define DIR_L   3u

#define SPEED_NO 0u
#define SPEED_SLOW 1u
#define SPEED_FAST 2u

#define CMD_SKIP 1u
#define CMD_GROW 2u
#define CMD_MOVE 3u

struct Cell {
    uint cellType; // [0,6] 3 bits
    float energy;
    uint energyFlow; // absolute // [0,15] 4 bits
    uint parentDir; // [0,3] 2 bits
    uint genomeId;
    uint direction; // [0,3] 2 bits
    uint activeGene; // [0,31] 5 bits
    uint seedProps; // bytes 0,1 - speed; 2,3 - timer
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
    uint pad0;
};
