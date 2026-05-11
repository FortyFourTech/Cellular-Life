using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CellInspectorUI : MonoBehaviour
{
    public SimulationUI simUI;
    public WorldSimulation world;
    public RawImage panRawImage;
    public UIPanZoom panZoom;

    [Header("UI Elements")]
    public TMP_Text gridLabel;
    public TMP_Text cellTypeLabel;
    public TMP_Text energyLabel;
    public TMP_Text energyFlowLabel;
    public TMP_Text parentDirLabel;
    public TMP_Text genomeIdLabel;
    public TMP_Text directionLabel;
    public TMP_Text activeGeneLabel;
    public TMP_Text seedPropsLabel;

    public GenomeInspectorUI genomeInspector;

    void Reset()
    {
        if (simUI == null) simUI = FindAnyObjectByType<SimulationUI>();
        if (world == null) world = FindAnyObjectByType<WorldSimulation>();
        if (panZoom == null) panZoom = FindAnyObjectByType<UIPanZoom>();
        if (panRawImage == null && panZoom != null) panRawImage = panZoom.GetComponent<RawImage>();
    }

    void Update()
    {
        if (world == null) { SetInactive(); return; }

        Vector2 mouse = Input.mousePosition;
        if (!ScreenToGrid(mouse, out int gx, out int gy)) { SetInactive(); return; }

        gridLabel.text = $"Grid: {gx}, {gy}";

        // request cell+genome asynchronously from the world (same helper used in SimulationUI)
        world.RequestCellAndGenome(gx, gy, (cell, genome) =>
        {
            bool have = cell.cellType > 0;
            if (!have)
            {
                SetInactive();
                return;
            }

            cellTypeLabel.text = $"cellType: {cell.cellType.Symbol()} ({cell.cellType})";
            energyLabel.text = $"energy: {cell.energy:F4}";
            energyFlowLabel.text = $"energyFlow: {cell.energyFlow}";
            parentDirLabel.text = $"parentDir: {cell.parentDir}";
            genomeIdLabel.text = $"genomeId: {cell.genomeId}";
            directionLabel.text = $"direction: {cell.direction}";
            activeGeneLabel.text = $"activeGene: {cell.activeGene}";
            if (cell.cellType == CellType.Seed)
            {
                seedPropsLabel.text = $"seed timer: {GetByte(cell.seedProps,0)}  speed: {GetByte(cell.seedProps,2)}";
            }

            if (genomeInspector != null)
            {
                genomeInspector.SetGenome(genome, cell.activeGene);
            }
        });
    }

    void SetInactive()
    {
        if (gridLabel != null) gridLabel.text = "";
        if (cellTypeLabel != null) cellTypeLabel.text = "<no cell>";
        if (energyLabel != null) energyLabel.text = "";
        if (energyFlowLabel != null) energyFlowLabel.text = "";
        if (parentDirLabel != null) parentDirLabel.text = "";
        if (genomeIdLabel != null) genomeIdLabel.text = "";
        if (directionLabel != null) directionLabel.text = "";
        if (activeGeneLabel != null) activeGeneLabel.text = "";
        if (seedPropsLabel != null) seedPropsLabel.text = "";
        if (genomeInspector != null) genomeInspector.Clear();
    }

    bool ScreenToGrid(Vector2 screenPos, out int gx, out int gy)
    {
        gx = gy = 0;
        if (world == null) return false;

        if (panZoom != null && panRawImage != null)
        {
            RectTransform rt = panRawImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out Vector2 localPoint))
                return false;
            Vector2 size = rt.rect.size;
            if (size.x <= 0 || size.y <= 0) return false;
            Vector2 normalized = new Vector2((localPoint.x + size.x * 0.5f) / size.x, (localPoint.y + size.y * 0.5f) / size.y);
            var uv = panRawImage.uvRect;
            float u = uv.x + normalized.x * uv.width;
            float v = uv.y + normalized.y * uv.height;
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);
            gx = Mathf.Clamp((int)(u * world.SimParams._Width), 0, world.SimParams._Width - 1);
            gy = Mathf.Clamp((int)(v * world.SimParams._Height), 0, world.SimParams._Height - 1);
            return true;
        }

        float size2 = Mathf.Min(Screen.width, Screen.height);
        float left = (Screen.width - size2) * 0.5f;
        float top = (Screen.height - size2) * 0.5f;
        float mx2 = screenPos.x;
        float my2 = screenPos.y;
        if (mx2 < left || mx2 > left + size2 || my2 < top || my2 > top + size2) return false;
        float nx = (mx2 - left) / size2;
        float ny = (my2 - top) / size2;
        gx = Mathf.Clamp((int)(nx * world.SimParams._Width), 0, world.SimParams._Width - 1);
        gy = Mathf.Clamp((int)(ny * world.SimParams._Height), 0, world.SimParams._Height - 1);
        return true;
    }

    uint GetByte(uint container, int byteIdx) => (container >> (byteIdx * 8)) & 0xffu;
}
