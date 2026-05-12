
using System;

public enum CellType : uint { Empty, Leaf, Root, Antenna, Wood, Sprout, Seed };

public struct CellData
{
    public CellType cellType;
    public float energy;
    public uint energyFlow;
    public uint parentDir;
    public uint genomeId;
    public uint direction;
    public uint activeGene;
    public uint seedProps; // bytes 0,1 - speed; 2,3 - timer
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

    public readonly CellType GetGrowCellType(int dir) {
        var typeParam = (growDirections >> (dir * 8)) & 0xFF;
        return typeParam < 64 ? CellType.Sprout :
            typeParam < 75 ? CellType.Leaf :
            typeParam < 85 ? CellType.Antenna :
            typeParam < 95 ? CellType.Root :
            0;
    }
    // public string GetGrowCellTypeString(int dir) => Enum.IsDefined(typeof(CellType), GetGrowCellType(dir)) ? GetGrowCellType(dir).ToString() : "-";
    public readonly string GetGrowCellTypeString(int dir) => GetGrowCellType(dir).Symbol();
    // Enum.IsDefined(typeof(CellType), GetGrowCellType(dir)) ? GetGrowCellType(dir).ToString() : "-";
};

[Serializable]
public unsafe struct GenomeData {
    public fixed byte genes[1024]; // 4 * 8 * 32 = 1024
    [NonSerialized] public uint cellNum;
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

public static class CellTypeExtension
{
    public static string Name(this CellType cellType) => cellType.ToString();
    public static string Symbol(this CellType cellType) => cellType switch
    {
        CellType.Empty => "[ 0]",
        CellType.Leaf => "🟢",
        CellType.Root => "🔴",
        CellType.Antenna => "🔵",
        CellType.Wood => "🟤",
        CellType.Sprout => "⚪️",
        CellType.Seed => "🟡",
        _ => "[-]",
    };
}

[Serializable]
public struct SimParams
{
    public int _Width;
    public int _Height;
    public float _Timestep;
    public float _Timestamp;
    public float _Rand;
    public float _Sunlight;
    public float _DiffusionRate;
    public float _CriticalOrg;
    public float _CriticalNrg;
    uint pad0;
    uint pad1;
    uint pad2;
}

public struct CommandEntry {
    public uint commandId;
    public uint successGene;
    public uint failGene;
    uint pad0;
};
