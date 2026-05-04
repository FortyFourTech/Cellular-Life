
using System;

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
    uint pad0;
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

    public CellType GetGrowCellType(int dir) => (CellType)(((growDirections >> (dir * 8)) & 0xFF) % 32);
    // public string GetGrowCellTypeString(int dir) => Enum.IsDefined(typeof(CellType), GetGrowCellType(dir)) ? GetGrowCellType(dir).ToString() : "-";
    public string GetGrowCellTypeString(int dir) => (((growDirections >> (dir * 8)) & 0xFF) % 32) switch
    {
        0 => "[0]",
        1 => "🟢",
        2 => "🔴",
        3 => "🔵",
        4 => "🟤",
        5 => "⚪️",
        6 => "🟡",
        _ => "[-]",
    };
    // Enum.IsDefined(typeof(CellType), GetGrowCellType(dir)) ? GetGrowCellType(dir).ToString() : "-";
};

public unsafe struct GenomeData {
    public fixed byte genes[1024]; // 4 * 8 * 32 = 1024
    public uint cellNum;
    uint pad0;
    uint pad1;
    uint pad2;

    public Gene GetGene(uint index)
    {
        // 1. Рассчитываем размер одной структуры Gene (в байтах)
        int geneSize = sizeof(Gene);

        // 2. Получаем указатель на начало массива байт
        fixed (byte* pStart = genes)
        {
            // 3. Сдвигаем указатель на (index * размер_гена)
            Gene* pGene = (Gene*)(pStart + (index * geneSize));

            // 4. Возвращаем значение (разыменовываем указатель)
            return *pGene;
        }
    }
};

