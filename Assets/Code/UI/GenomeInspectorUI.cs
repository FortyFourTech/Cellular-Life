using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GenomeInspectorUI : MonoBehaviour
{
    public TMP_Text headerText;
    public TMP_Text tableText; // multiline compact table

    GenomeData currentGenome;
    bool haveGenome = false;
    uint currentActiveGene = 0u;

    public void SetGenome(GenomeData genome, uint activeGene)
    {
        currentGenome = genome;
        haveGenome = currentGenome.cellNum != 0u;
        currentActiveGene = activeGene;
        Render();
    }

    public void Clear()
    {
        haveGenome = false;
        headerText.text = "<no genome>";
        tableText.text = "";
    }

    void Render()
    {
        if (!haveGenome)
        {
            Clear();
            return;
        }

        headerText.text = $"Genome (cells: {currentGenome.cellNum})";

        StringBuilder sb = new StringBuilder();
        for (uint i = 0; i < 32u; ++i)
        {
            Gene g = currentGenome.GetGene(i);

            string grow0 = g.GetGrowCellTypeString(0);
            string grow1 = g.GetGrowCellTypeString(1);
            string grow2 = g.GetGrowCellTypeString(2);
            string grow3 = g.GetGrowCellTypeString(3);

            uint cond1Raw = GetByte(g.conditions, 0) % 256u;
            uint cond2Raw = GetByte(g.conditions, 1) % 256u;

            uint cr0 = GetByte(g.condResult, 0) % 26u;
            uint cr1 = GetByte(g.condResult, 1) % 26u;
            uint gr0 = GetByte(g.condResult, 2) % 32u;
            uint gr1 = GetByte(g.condResult, 3) % 32u;

            string activeMark = (i == currentActiveGene) ? "<color=green>>" : "<color=white> ";

            // Compact one-line representation per gene
            sb.AppendFormat("{0}{1:00}: {2}{3}{4}{5} | c:{6}/{7:F1},{8:F1} | r:{9},{10} g:{11},{12}<\\color>\n",
                activeMark, i, grow0, grow1, grow2, grow3,
                cond1Raw, g.condParam1, g.condParam2,
                cr0, cr1, gr0, gr1);
        }

        tableText.text = sb.ToString();
    }

    static uint GetByte(uint container, int byteIdx) => (container >> (byteIdx * 8)) & 0xffu;
}
