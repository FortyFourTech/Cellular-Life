
struct CellData
{
    public uint cellType;
    public float energy;
    public uint parentId;
    public uint genomeId;
    public uint direction;
    public uint flags;
    public uint activeGene;
    public uint pad0;
}

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

struct GenomeData {
    Gene[] genes;
    uint cellNum;
};

