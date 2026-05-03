
public enum CellType : uint { Empty, Leaf, Root, Antenna, Wood, Sprout, Seed };

public struct CellData
{
    public CellType cellType;
    public float energy;
    public uint energyFlow;
    public uint parentDir;
    public int genomeId;
    public uint direction;
    public uint activeGene;
    public uint pad0;
}

public struct Gene {
    public uint growDirections; // bites with types in relative direction: 0 - forward, 1 - right, 2 - back, 3 - left
    public uint conditions; // two conditions in two first bites
    public float condParam1;
    public float condParam2;
    public uint condResult; // two commands in two first bites: 0 - command for success, 1 - command for fail, 2 - gene for success, 3 - gene for fail
    public uint comGenes; // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail
    public uint aloneCommands; // two commands in two first bites: 0 - for success, 1 - for fail
    public uint aloneComGenes; // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail
    // public uint pad0;
};

public unsafe struct GenomeData {
    public fixed byte genes[1024]; // 4 * 8 * 32 = 1024
    public uint cellNum;
};

