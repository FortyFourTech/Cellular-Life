using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Simple IMGUI-based simulation controller. Provides controls and stubs
// for features to be implemented later. Does not modify other classes.
public class SimulationUI : MonoBehaviour
{
    public WorldSimulation world;
    public WorldRenderer wRenderer;
    public UIPanZoom panZoom;

    // Simulation controls
    public bool isPaused = false;
    public float simulationSpeed = 1.0f; // steps per frame

    // World parameters
    public float sunlight = 1.0f;
    public float diffusionRate = 0.2f;
    public float cellEnergyCost = 0.005f;

    // Brush settings
    public enum BrushMode { None, AddOrganics, AddEnergy, KillCell, SetCell }
    public BrushMode activeBrush = BrushMode.None;
    public float brushRadius = 4.0f;
    public float brushCellType = 1;
    public int brushMouseButton = 0;
    // Quick mutator amounts
    public float brushDelta = 1.0f;

    // Visualization toggles (placeholders)
    public WorldRenderer.RenderMode activeRenderMode = WorldRenderer.RenderMode.CellsFull;
    public bool showEnergyFlow = false;

    // Mutation / genetics
    public float mutationRate = 0.01f;
    public int mutationChanges = 1;
    public bool mutationIncremental = true;

    // Genome input (simple text field)
    public string genomeInput = "";

    // Internal
    Vector2 scroll;
    float lastUpdateTime;
    // Cell inspector
    bool inspectorEnabled = true;
    bool haveInspectedCell = false;
    CellData inspectedCell;
    GenomeData inspectedGenome;
    string genomeReadError = "";
    // optional pan/zoom helper
    RawImage panRawImage;

    void Reset()
    {
        // try to auto-assign world
        if (world == null) world = FindAnyObjectByType<WorldSimulation>();
        if (!wRenderer) wRenderer = FindAnyObjectByType<WorldRenderer>();
        // try to find UIPanZoom (if a UI RawImage displays the world)
        if (!panZoom) panZoom = GetComponentInChildren<UIPanZoom>();
    }

    private void Start() {
        if (panZoom != null) panRawImage = panZoom.GetComponent<RawImage>();
    }

    void Update()
    {
        // keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isPaused) {
                if (Input.GetKey(KeyCode.LeftControl))
                    isPaused = !isPaused;
                else
                    StepOnce();
            }
            else
                isPaused = !isPaused;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            activeRenderMode = WorldRenderer.RenderMode.CellsFull;
        } else if (Input.GetKeyDown(KeyCode.Alpha2)) {
            activeRenderMode = WorldRenderer.RenderMode.CellsEnergy;
        } else if (Input.GetKeyDown(KeyCode.Alpha3)) {
            activeRenderMode = WorldRenderer.RenderMode.SoilOrganics;
        } else if (Input.GetKeyDown(KeyCode.Alpha4)) {
            activeRenderMode = WorldRenderer.RenderMode.SoilEnergy;
        }

        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R)) { Generate(); Populate(); }

        // Ctrl + mouse wheel changes simulation speed (approx)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            float delta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(delta) > 0.01f) simulationSpeed = Mathf.Clamp(simulationSpeed + delta * 0.1f, 0.01f, 64f);
        }

        // run simulation steps when not paused
        if (!isPaused && world != null)
        {
            float timeSinceLastUpdate = Time.time - lastUpdateTime;
            if (simulationSpeed > 1f) {
                int steps = Mathf.Max(1, Mathf.FloorToInt(simulationSpeed));
                for (int i = 0; i < steps; ++i) world.Step();
                lastUpdateTime = Time.time;
            } else {
                if (timeSinceLastUpdate > Time.fixedDeltaTime / simulationSpeed)
                {
                    world.Step();
                    lastUpdateTime = Time.time;
                }
            }

            ApplySimParameters();
        }

        // brush input: apply while holding the brush mouse button
        if (activeBrush != BrushMode.None && Input.GetMouseButtonDown(brushMouseButton) && world != null)
        {
            // ignore clicks over the UI panel (left side)
            const float pad = 8f;
            float uiWidth = 320f;
            if (Input.mousePosition.x > (pad + uiWidth))
            {
                ApplyBrushAtScreenPosition(Input.mousePosition);
            }
        }
    }

    void OnGUI()
    {
        const float pad = 8f;
        float w = 320f;
        GUILayout.BeginArea(new Rect(pad, pad, w, Screen.height - pad*2), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Simulation Controls", GUI.skin.label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(isPaused ? "Resume" : "Pause")) { isPaused = !isPaused; }
        if (isPaused)
            if (GUILayout.Button("Step")) { StepOnce(); }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate")) { Generate(); }
        if (GUILayout.Button("Populate")) { Populate(); }
        if (GUILayout.Button("Restart")) { Generate(); Populate(); }
        GUILayout.EndHorizontal();
        GUILayout.Label($"Speed: {simulationSpeed:F2}");
        simulationSpeed = GUILayout.HorizontalSlider(simulationSpeed, 0.01f, 1f);

        GUILayout.Space(6);
        GUILayout.Label("World Parameters", GUI.skin.label);
        GUILayout.Label($"Sunlight: {sunlight:F2}");
        sunlight = GUILayout.HorizontalSlider(sunlight, 0f, 10f);
        GUILayout.Label($"Diffusion Rate: {diffusionRate:F2}");
        diffusionRate = GUILayout.HorizontalSlider(diffusionRate, 0f, 10f);
        // GUILayout.Label($"Cell energy cost/tick: {cellEnergyCost:F4}");
        // cellEnergyCost = GUILayout.HorizontalSlider(cellEnergyCost, 0f, 0.1f);
        // if (GUILayout.Button("Apply World Params")) ApplySimParameters();

        GUILayout.Space(6);
        GUILayout.Label("Brush / Tools", GUI.skin.label);
        activeBrush = (BrushMode)GUILayout.SelectionGrid((int)activeBrush, Enum.GetNames(typeof(BrushMode)), 2);
        GUILayout.Label($"Brush radius: {brushRadius:F1}");
        brushRadius = GUILayout.HorizontalSlider(brushRadius, 1f, 64f);
        GUILayout.Label($"Brush delta: {brushDelta:F1}");
        brushDelta = GUILayout.HorizontalSlider(brushDelta, -1f, 1f);
        GUILayout.Label($"Brush cell type: {brushCellType:F1}");
        brushCellType = GUILayout.HorizontalSlider(brushCellType, 1f, 6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Use Brush (Hold)")) { /* placeholder: handled via mouse events */ }
        if (GUILayout.Button("Clear Brush")) { /* stub */ }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Visualization", GUI.skin.label);
        activeRenderMode = (WorldRenderer.RenderMode)GUILayout.SelectionGrid((int)activeRenderMode, Enum.GetNames(typeof(WorldRenderer.RenderMode)), 2);
        // showEnergyFlow = GUILayout.Toggle(showEnergyFlow, "Show Energy Flow");
        wRenderer.renderMode = activeRenderMode;

        // GUILayout.Space(6);
        // GUILayout.Label("Genetics / Mutation", GUI.skin.label);
        // GUILayout.Label($"Mutation rate: {mutationRate:F3}");
        // mutationRate = GUILayout.HorizontalSlider(mutationRate, 0f, 1f);
        // GUILayout.Label($"Num changes: {mutationChanges}");
        // mutationChanges = (int)GUILayout.HorizontalSlider(mutationChanges, 0, 10);
        // mutationIncremental = GUILayout.Toggle(mutationIncremental, "Incremental changes");

        // GUILayout.Label("Genome input (32 values 0..20 separated by commas)");
        // genomeInput = GUILayout.TextArea(genomeInput, GUILayout.Height(60));
        // if (GUILayout.Button("Create Seed with Genome")) { CreateSeedFromGenome(); }

        GUILayout.Space(6);
        GUILayout.Label("Debug / Quick Stats", GUI.skin.label);
        if (world != null)
        {
            var total = CountCells();
            var leaves = CountCellsByType(1);
            GUILayout.Label($"Total cells: {total}");
            GUILayout.Label($"Leaves: {leaves}");
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawCellInspectorPanel();
    }

    void DrawCellInspectorPanel()
    {
        const float pad = 8f;
        float inspectorW = 350f;
        Rect area = new Rect(Screen.width - pad - inspectorW, pad, inspectorW, Screen.height - pad*2);
        GUILayout.BeginArea(area, GUI.skin.box);
        // GUILayout.Label("Cell Inspector", GUI.skin.label);

        Vector2 mouse = Input.mousePosition;
        int gx, gy;
        if (world == null)
        {
            GUILayout.Label("No world assigned");
            GUILayout.EndArea();
            return;
        }

        bool inside = ScreenToGrid(mouse, out gx, out gy);
        if (!inside)
        {
            GUILayout.Label("Cursor outside world area");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"Grid: {gx}, {gy}");

        // read cell from GPU
        ReadCellFromGpu(gx, gy);

        if (!haveInspectedCell)
        {
            GUILayout.Label("Failed to read cell data");
            GUILayout.EndArea();
            return;
        }

        // display all fields of CellData
        GUILayout.Label($"cellType: {inspectedCell.cellType.Symbol()}({inspectedCell.cellType})");
        GUILayout.Label($"energy: {inspectedCell.energy:F4}");
        GUILayout.Label($"energyFlow: {inspectedCell.energyFlow}");
        GUILayout.Label($"parentDir: {inspectedCell.parentDir}");
        // GUILayout.Label($"genomeId: {inspectedCell.genomeId}");
        GUILayout.Label($"direction: {inspectedCell.direction}");
        // GUILayout.Label($"activeGene: {inspectedCell.activeGene}");
        if (inspectedCell.cellType == CellType.Seed)
        {
            GUILayout.Label($"seed timer: {GetByte(inspectedCell.seedProps, 0)}");
            GUILayout.Label($"seed speed: {GetByte(inspectedCell.seedProps, 2)}");
        }

        // GUILayout.Label("-----------------------------------");
        // GUILayout.Space(6);
        GUILayout.Label($"Genome#{inspectedCell.genomeId} (cells: {inspectedGenome.cellNum})");
        genomeReadError = "";
        if (inspectedGenome.cellNum == 0u)
        {
            GUILayout.Label("<no genome>");
        }
        else
        {
            // ReadGenomeFromGpu(inspectedCell.genomeId);

            // var inspectedGene = inspectedGenome.GetGene(inspectedCell.activeGene);

            // GUILayout.Label($"                              {inspectedGene.GetGrowCellType(0).Symbol()}"); // bites with types in relative direction: 0 - forward, 1 - right, 2 - back, 3 - left
            // GUILayout.Label($"growDirections: {inspectedGene.GetGrowCellTypeString(3)}  +  {inspectedGene.GetGrowCellTypeString(1)}"); // bites with types in relative direction: 0 - forward, 1 - right, 2 - back, 3 - left
            // GUILayout.Label($"                              {inspectedGene.GetGrowCellTypeString(2)}"); // bites with types in relative direction: 0 - forward, 1 - right, 2 - back, 3 - left

            // var cond1 = GetByte(inspectedGene.conditions, 0) % 26;
            // var cond1String = cond1 >= 13 ? "-" : $"{cond1}";
            // GUILayout.Label($"condition 1: {cond1String} ({inspectedGene.condParam1}) (raw: {GetByte(inspectedGene.conditions, 0)})");
            // var cond2 = GetByte(inspectedGene.conditions, 1) % 26;
            // var cond2String = cond2 >= 13 ? "-" : $"{cond2}";
            // GUILayout.Label($"condition 2: {cond2String} ({inspectedGene.condParam2}) (raw: {GetByte(inspectedGene.conditions, 1)})");

            // GUILayout.Label($"condResult: com1[{GetByte(inspectedGene.condResult,0)%26}] com2[{GetByte(inspectedGene.condResult, 1)%26}] gene1[{GetByte(inspectedGene.condResult, 2)%32}] gene2[{GetByte(inspectedGene.condResult, 3)%32}]"); // two commands in two first bites: 0 - command for success, 1 - command for fail, 2 - gene for success, 3 - gene for fail
            // GUILayout.Label($"comGenes: com1Success[{GetByte(inspectedGene.comGenes,0)%32}] com1fail[{GetByte(inspectedGene.comGenes, 1)%32}] com2Success[{GetByte(inspectedGene.comGenes, 2)%32}] com2fail[{GetByte(inspectedGene.comGenes, 3)%32}]"); // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail
            // GUILayout.Label($"aloneCommands: com1[{GetByte(inspectedGene.aloneCommands,0)%20}] com2[{GetByte(inspectedGene.aloneCommands, 1)%20}]"); // two commands in two first bites: 0 - for success, 1 - for fail
            // GUILayout.Label($"aloneComGenes: com1Success[{GetByte(inspectedGene.aloneComGenes,0)%32}] com1fail[{GetByte(inspectedGene.aloneComGenes,1)%32}] com2success[{GetByte(inspectedGene.aloneComGenes,2)%32}] com2fail[{GetByte(inspectedGene.aloneComGenes,3)%32}]"); // gene indicies: 0 - for first command success, 1 - for first command fail, 2 - for second command success, 3 - for second command fail

            StringBuilder sb = new StringBuilder();
            for (uint i = 0; i < 32u; ++i)
            {
                Gene g = inspectedGenome.GetGene(i);

                string grow0 = g.GetGrowCellTypeString(0);
                string grow1 = g.GetGrowCellTypeString(1);
                string grow2 = g.GetGrowCellTypeString(2);
                string grow3 = g.GetGrowCellTypeString(3);

                uint cond1Raw = GetByte(g.conditions, 0) % 26u;
                var cond1String = cond1Raw >= 13 ? "- " : $"{cond1Raw}";
                uint cond2Raw = GetByte(g.conditions, 1) % 26u;
                var cond2String = cond2Raw >= 13 ? "- " : $"{cond2Raw}";

                uint cr0 = GetByte(g.condResult, 0) % 26u;
                uint cr1 = GetByte(g.condResult, 1) % 26u;
                uint gr0 = GetByte(g.condResult, 2) % 32u;
                uint gr1 = GetByte(g.condResult, 3) % 32u;

                string activeMark = (i == inspectedCell.activeGene) ? "<color=green>" : "<color=white>";

                // Compact one-line representation per gene
                sb.AppendFormat("{0}{1:00}: {2}{3}{4}{5} | {6}({7:F1}), {8}({9:F1}) | c:{10}, {11} g:{12}, {13}\n",
                    activeMark, i, grow0, grow1, grow2, grow3,
                    cond1String, g.condParam1, cond2String, g.condParam2,
                    cr0, cr1, gr0, gr1);
            }

            // GUILayout.Label($"Genes:");
            GUILayout.Label(sb.ToString());

            // unsafe
            // {
            // }
            // var genesArr = (fixed Gene[32])inspectedGenome.genes;

            // display genome fields via reflection to avoid access issues
            // var gType = typeof(GenomeData);
            // var fields = gType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            // foreach (var f in fields)
            // {
            //     object val = f.GetValue(inspectedGenome);
            //     if (val == null)
            //     {
            //         GUILayout.Label($"{f.Name}: null");
            //         continue;
            //     }

            //     // if it's an array, show length and some preview
            //     var arr = val as System.Array;
            //     if (arr != null)
            //     {
            //         GUILayout.Label($"{f.Name}.Length: {arr.Length}");
            //         int toShow = Math.Min(8, arr.Length);
            //         for (int i = 0; i < toShow; ++i)
            //         {
            //             var el = arr.GetValue(i);
            //             if (el == null) break;
            //             // show element fields
            //             var et = el.GetType();
            //             var efields = et.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            //             System.Text.StringBuilder sb = new System.Text.StringBuilder();
            //             sb.Append($"[{i}]: ");
            //             foreach (var ef in efields)
            //             {
            //                 var v = ef.GetValue(el);
            //                 sb.Append($"{ef.Name}={v} ");
            //             }
            //             GUILayout.Label(sb.ToString());
            //         }
            //     }
            //     else
            //     {
            //         GUILayout.Label($"{f.Name}: {val}");
            //     }
            // }
        }

        GUILayout.EndArea();
    }

    bool ScreenToGrid(Vector2 screenPos, out int gx, out int gy)
    {
        gx = gy = 0;
        if (world == null) return false;

        // If a UIPanZoom + RawImage is present, use its rect and uvRect to map screen->uv
        if (panZoom != null && panRawImage != null)
        {
            RectTransform rt = panRawImage.rectTransform;
            Vector2 localPoint;
            Camera cam = null; // assume Screen Space - Overlay
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, cam, out localPoint))
            {
                return false;
            }

            Vector2 size = rt.rect.size;
            if (size.x <= 0 || size.y <= 0) return false;

            Vector2 normalized = new Vector2((localPoint.x + size.x * 0.5f) / size.x, (localPoint.y + size.y * 0.5f) / size.y);

            // get uvRect from RawImage
            var uv = panRawImage.uvRect;
            // uv origin is bottom-left; normalized is 0..1 within rect
            float u = uv.x + normalized.x * uv.width;
            float v = uv.y + normalized.y * uv.height;

            // wrap UVs so panning repeats texture instead of clamping to edges
            u = Mathf.Repeat(u, 1f);
            v = Mathf.Repeat(v, 1f);

            gx = Mathf.Clamp((int)(u * world.SimParams._Width), 0, world.SimParams._Width - 1);
            gy = Mathf.Clamp((int)(v * world.SimParams._Height), 0, world.SimParams._Height - 1);
            return true;
        }

        // fallback: centered square preserving proportions (original behavior)
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

    void ReadCellFromGpu(int gx, int gy)
    {
        // haveInspectedCell = false;
        genomeReadError = "";
        if (world == null || world.CellsBuffer == null) return;
        int idx = gy * world.SimParams._Width + gx;

        world.RequestCellAndGenome(gx, gy, (cell, genome) =>
        {
            inspectedCell = cell;
            inspectedGenome = genome;
            haveInspectedCell = inspectedCell.cellType > 0;
            if (haveInspectedCell)
                return;
        });
        // inspectedCell = world.CellsData[idx];
        return;

        try
        {
            var carr = new CellData[1];
            world.CellsBuffer.GetData(carr, 0, idx, 1);
            inspectedCell = carr[0];
            haveInspectedCell = true;
        }
        catch (System.Exception ex)
        {
            haveInspectedCell = false;
            genomeReadError = "Error reading cell: " + ex.Message;
        }
    }

    void ReadGenomeFromGpu(int genomeId)
    {
        if (world == null || world.GenomesBuffer == null) return;

        inspectedGenome = world.GetGenome(genomeId);
        return;

        // attempt to read genome buffer
        if (world.GenomesBuffer == null)
        {
            GUILayout.Label("Genome buffer not available");
        }
        else
        {
            try
            {
                var gid = (int)inspectedCell.genomeId;
                var garr = new GenomeData[1];
                world.GenomesBuffer.GetData(garr, 0, gid, 1);
                inspectedGenome = garr[0];
            }
            catch (System.Exception ex)
            {
                genomeReadError = "Error reading genome: " + ex.Message;
                GUILayout.Label(genomeReadError);
            }
        }
    }

    void Generate()
    {
        world?.InitWorld();
    }

    void Populate()
    {
        world?.PopulateWorld();
    }

    void StepOnce()
    {
        world?.Step();
    }

    void ApplySimParameters()
    {
        if (world == null) return;

        world.SimParams._Sunlight = sunlight;
        world.SimParams._DiffusionRate = diffusionRate;
    }

    void CreateSeedFromGenome()
    {
        // stub: parse genomeInput and enqueue creation of a seed cell with this genome
        Debug.Log("CreateSeedFromGenome: stub - parsed input: " + genomeInput);
    }

    void ApplyBrushAtScreenPosition(Vector2 screenPos)
    {
        if (world == null) return;
        int gx, gy;
        if (!ScreenToGrid(screenPos, out gx, out gy)) return;

        int r = Mathf.Max(1, Mathf.CeilToInt(brushRadius));
        float r2 = brushRadius * brushRadius;

        // iterate over integer grid within radius and schedule changes
        for (int oy = -r; oy <= r; ++oy)
        {
            int y = gy + oy;
            if (y < 0 || y >= world.SimParams._Height) continue;
            for (int ox = -r; ox <= r; ++ox)
            {
                int x = gx + ox;
                if (x < 0 || x >= world.SimParams._Width) continue;
                if ((ox * ox + oy * oy) > r2) continue;

                switch (activeBrush)
                {
                    case BrushMode.AddOrganics:
                        world.ScheduleOrganicsChange(x, y, brushDelta);
                        break;
                    case BrushMode.AddEnergy:
                        world.ScheduleEnergyChange(x, y, brushDelta);
                        break;
                    case BrushMode.KillCell:
                        world.ScheduleKillAt(x, y);
                        break;
                    case BrushMode.SetCell:
                        world.ScheduleSetCell(x, y, Mathf.FloorToInt(brushCellType));
                        break;
                }
            }
        }

        // dispatch the corresponding mutator kernels immediately
        try {
            if (activeBrush == BrushMode.AddOrganics) world.RunOrganicsNow();
            if (activeBrush == BrushMode.AddEnergy) world.RunEnergyNow();
            if (activeBrush == BrushMode.KillCell) world.RunKillNow();
            if (activeBrush == BrushMode.SetCell) world.RunSetCellNow();
        } catch { }
    }

    uint CountCells()
    {
        if (world == null) return 0;
        return world.CellsNum;
    }

    uint CountCellsByType(uint type)
    {
        if (world == null) return 0;
        return type switch
        {
            1 => world.LeavesNum,
            _ => 0,
        };
    }

    uint GetByte(uint container, int byteIdx)
    {
        return (container >> (byteIdx * 8)) & 0xffu;
    }
}
