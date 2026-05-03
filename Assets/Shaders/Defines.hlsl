#pragma once

#define CELLTYPE_LEAF       1
#define CELLTYPE_ROOT       2
#define CELLTYPE_ANTENNA    3
#define CELLTYPE_WOOD       4
#define CELLTYPE_SPROUT     5
#define CELLTYPE_SEED       6

#define ENERGY_GROW         0.1

#define ABSORB_SOIL_ORGANICS 0.05
#define ABSORB_SOIL_ENERGY  0.05
#define ABSORB_LIFE_ENERGY  0.05

struct Cell {
    uint cellType;    // 0=empty,1=Leaf,2=Root,3=Antenna,4=Wood,5=Sprout,6=Seed
    float energy;
    uint energyFlow; // absolute
    uint parentDir;
    uint genomeId;
    uint direction;
    uint activeGene;
    uint pad0;
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
    Gene genes[32];
    uint cellNum;
};
