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
    public Image brushRenderer;
    [SerializeField] private GenomeStorage _genomeStorage;

    // Simulation controls
    public bool isPaused = false;
    public float simulationSpeed = 1.0f; // steps per frame
    public bool executeSubstep = false;

    // World parameters
    public float cellEnergyCost = 0.005f;

    // Brush settings
    public enum BrushMode { None, AddOrganics, AddEnergy, KillCell }
    public BrushMode activeBrush = BrushMode.None;
    public float brushRadius = 50.0f;
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

    // Internal
    Vector2 scroll;
    float lastUpdateTime;
    // Cell inspector
    bool inspectorEnabled = true;
    bool showInvalidCell = false;
    bool haveInspectedCell = false;
    bool showWorldParameters = false;
    bool showCellConstants = false;
    bool showTools = false;
    bool showStatistics = false;
    CellData inspectedCell;
    Vector2 inspectedSoil;
    Vector2Int inspectedDebug;
    GenomeData inspectedGenome;
    CommandEntry inspectedCommand;
    string genomeReadError = "";
    // optional pan/zoom helper
    RawImage panRawImage;

    SimParams _simParams;
    CellConstants _cellConstants;
    private static readonly uint[] commandLookupSingle = {1,2,3,4,5,6,7,8,9,10};
    private static readonly uint[] commandLookupCommon = {1,2,6,11,12,13,14,15,16,17,18,19,20};

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

        if (world)
        {
            _simParams = world.SimParams;
            _cellConstants = world.CellConstants;
        }
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

        if (Input.GetKeyDown(KeyCode.R)) { Generate(); Populate(); }

        // Ctrl + mouse wheel changes simulation speed (approx)
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            float delta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(delta) > 0.01f) simulationSpeed = Mathf.Clamp(simulationSpeed + delta * 0.1f, 0.01f, 64f);
        }

        // Shift + mouse wheel changes brush size
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            float delta = Input.mouseScrollDelta.y;
            if (Mathf.Abs(delta) > 0.01f) brushRadius = Mathf.Clamp(brushRadius + delta, 10f, 100f);
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

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(1) && inspectedCell.cellType != CellType.Empty)
        {
            _genomeStorage.genomes.Insert(0, inspectedGenome);
            UnityEditor.EditorUtility.SetDirty(_genomeStorage);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(_genomeStorage);
        }
#endif
    }

    private void _Tooltip(string text) {
        Rect labelRect = GUILayoutUtility.GetLastRect();
        Vector2 mousePosGUI = Event.current.mousePosition;
        if (labelRect.Contains(mousePosGUI))
            _currentTooltip = text;
    }

    private string _currentTooltip = "";
    void OnGUI()
    {
        _currentTooltip = "";
        const float pad = 8f;
        float w = 320f;
        GUILayout.BeginArea(new Rect(pad, pad, w, Screen.height - pad*2), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Simulation Controls", GUI.skin.label);
        GUILayout.BeginHorizontal();
            if (GUILayout.Button(isPaused ? "Resume" : "Pause")) { isPaused = !isPaused; }
            _Tooltip(isPaused ? "[Ctrl]+[Space]" : "[Space]");

            if (isPaused){
                if (GUILayout.Button("Step")) { StepOnce(); }
                _Tooltip("[Space]");
            }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate")) { Generate(); }
            if (GUILayout.Button("Populate")) { Populate(); }
            if (GUILayout.Button("Restart")) { Generate(); Populate(); }
            _Tooltip("[R]");
        GUILayout.EndHorizontal();

        if (isPaused)
            executeSubstep = GUILayout.Toggle(executeSubstep, $"Execute Sim Substep ({world.SubstepIdx})");

        GUILayout.Label($"Speed: {simulationSpeed:F2}");
        _Tooltip("[Ctrl] + MouseWheel");
        simulationSpeed = GUILayout.HorizontalSlider(simulationSpeed, 0.01f, 1f);

        GUILayout.Space(6);
        showWorldParameters = GUILayout.Toggle(showWorldParameters, " World Parameters");
        if (showWorldParameters) {
            GUILayout.Label($"Sunlight: {_simParams._Sunlight:F2}");
            _simParams._Sunlight = GUILayout.HorizontalSlider(_simParams._Sunlight, 0f, 10f);
            GUILayout.Label($"Diffusion Rate: {_simParams._DiffusionRate:F2}");
            _simParams._DiffusionRate = GUILayout.HorizontalSlider(_simParams._DiffusionRate, 0f, 1f);
        }

        GUILayout.Space(6);
        showCellConstants = GUILayout.Toggle(showCellConstants, " Cell Constants");
        if (showCellConstants) {
            GUILayout.Label($"Soil absorb speed: {_cellConstants.OrgAbsorbSpeed:F2}");
            _cellConstants.OrgAbsorbSpeed = GUILayout.HorizontalSlider(_cellConstants.OrgAbsorbSpeed, 0.05f, 1f);
            _cellConstants.NrgAbsorbSpeed = _cellConstants.OrgAbsorbSpeed;

            GUILayout.Label($"Grow energy spend: {_cellConstants.GrowNrg:F2}");
            _cellConstants.GrowNrg = GUILayout.HorizontalSlider(_cellConstants.GrowNrg, 0.01f, 2f);

            GUILayout.Label($"Life spend: {_cellConstants.LifeNrgSpend:F2}");
            _cellConstants.LifeNrgSpend = GUILayout.HorizontalSlider(_cellConstants.LifeNrgSpend, 0.01f, 1f);

            GUILayout.Label($"Transport speed: {_cellConstants.NrgTransportSpeed:F2}");
            _cellConstants.NrgTransportSpeed = GUILayout.HorizontalSlider(_cellConstants.NrgTransportSpeed, 1f, 10f);

            GUILayout.Label($"Min energy to transport: {_cellConstants.NrgTransportMin:F2}");
            _cellConstants.NrgTransportMin = GUILayout.HorizontalSlider(_cellConstants.NrgTransportMin, 0.01f, 2f);
        }

        GUILayout.Space(6);
        showTools = GUILayout.Toggle(showTools, " Brush / Tools");
        if (showTools) {
            activeBrush = (BrushMode)GUILayout.SelectionGrid((int)activeBrush, Enum.GetNames(typeof(BrushMode)), 2);
            GUILayout.Label($"Brush radius: {brushRadius:F1}");
            _Tooltip("[Shift] + MouseWheel");
            brushRadius = GUILayout.HorizontalSlider(brushRadius, 10f, 100f);
            GUILayout.Label($"Brush delta: {brushDelta:F1}");
            brushDelta = GUILayout.HorizontalSlider(brushDelta, -1f, 1f);
            GUILayout.Label($"Brush cell type: {brushCellType:F1}");
            brushCellType = GUILayout.HorizontalSlider(brushCellType, 1f, 6f);
        }

        GUILayout.Space(6);
        GUILayout.Label("Visualization", GUI.skin.label);
        Action<WorldRenderer.RenderMode,string> renderModeButton = (WorldRenderer.RenderMode rm, string hotkeyTooltip) => {
            if (GUILayout.Toggle(activeRenderMode == rm, Enum.GetName(typeof(WorldRenderer.RenderMode), rm), GUI.skin.button)) { activeRenderMode = rm; }
            _Tooltip(hotkeyTooltip);
        };
        GUILayout.BeginHorizontal();
        renderModeButton(WorldRenderer.RenderMode.CellsFull,    "[1]");
        renderModeButton(WorldRenderer.RenderMode.CellsEnergy,  "[2]");
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        renderModeButton(WorldRenderer.RenderMode.SoilOrganics, "[3]");
        renderModeButton(WorldRenderer.RenderMode.SoilEnergy,   "[4]");
        GUILayout.EndHorizontal();
        // showEnergyFlow = GUILayout.Toggle(showEnergyFlow, "Show Energy Flow");
        wRenderer.SetRenderMode(activeRenderMode);

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
        showStatistics = GUILayout.Toggle(showStatistics, " Debug / Quick Stats");
        if (showStatistics) {
            GUILayout.Space(6);
            if (world != null)
            {
                GUILayout.Label($"Total cells: {world.Stats.cellsCount}");
                GUILayout.Label($"{CellType.Leaf.Symbol()}: {world.Stats.leafsCount}");
                GUILayout.Label($"{CellType.Root.Symbol()}: {world.Stats.rootsCount}");
                GUILayout.Label($"{CellType.Antenna.Symbol()}: {world.Stats.antennasCount}");
                GUILayout.Label($"{CellType.Wood.Symbol()}: {world.Stats.woodCount}");
                GUILayout.Label($"{CellType.Sprout.Symbol()}: {world.Stats.sproutsCount}");
                GUILayout.Label($"{CellType.Seed.Symbol()}: {world.Stats.seedsCount}");
                GUILayout.Label($"Cell energy: {world.Stats.cellEnergy}");
                GUILayout.Label($"Soil organics: {world.Stats.soilOrganics}");
                GUILayout.Label($"Soil energy: {world.Stats.soilEnergy}");
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        DrawCellInspectorPanel();

        if (!string.IsNullOrEmpty(_currentTooltip)) {
            Vector2 mousePos = Input.mousePosition; // Event.current.mousePosition;
            GUI.Box(new Rect(mousePos.x + 16f, Screen.height - mousePos.y - 16f, 150f, 25f), _currentTooltip);
        }

        if (activeBrush != BrushMode.None && world != null)
        {
            Vector2 mouse = Input.mousePosition;
            int gx, gy;
            bool insideWorldArea = ScreenToGrid(mouse, out gx, out gy);

            if (insideWorldArea)
            {
                // Convert mouse position to GUI space (y-inverted)
                // Vector2 guiMousePos = new Vector2(mouse.x, Screen.height - mouse.y);

                // Draw the circle using the GL class, which operates in pixel coordinates
                UpdateBrush(mouse, brushRadius, activeBrush switch
                {
                    BrushMode.AddOrganics => Color.orange,
                    BrushMode.AddEnergy => Color.blue,
                    BrushMode.KillCell => Color.red,
                    // BrushMode.SetCell => Color.white,
                    _ => Color.clear
                });
            }
        }
        else
        {
            UpdateBrush(Vector2.zero, 0, Color.clear);
        }
    }

    void DrawCellInspectorPanel()
    {
        const float pad = 8f;
        float inspectorW = 400f;
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
        GUILayout.Label($"Soil [{inspectedSoil.x};{inspectedSoil.y}]");

#if DEVELOPMENT_BUILD
        GUILayout.Label($"Debug [{inspectedDebug.x};{inspectedDebug.y}]");

        ReadCommandFromGpu(gx, gy);
        GUILayout.Label($"Last cell command: [{inspectedCommand.commandId}]({inspectedCommand.successGene},{inspectedCommand.failGene})");
#endif // DEVELOPMENT_BUILD

        // read cell from GPU
        ReadCellFromGpu(gx, gy);

#if DEVELOPMENT_BUILD
        showInvalidCell = GUILayout.Toggle(showInvalidCell, "Show invalid cell");
#endif // DEVELOPMENT_BUILD

        GUILayout.Space(6);
        if (!haveInspectedCell && !showInvalidCell)
        {
            GUILayout.Label("Failed to read cell data");
            GUILayout.EndArea();
            return;
        }

        // display all fields of CellData
        bool isSingle = inspectedCell.parentDir > 3 && inspectedCell.energyFlow == 0;
        GUILayout.Label($"cellType: {inspectedCell.cellType.Symbol()}({inspectedCell.cellType})" + (isSingle ? " SINGLE" : ""));
        GUILayout.Label($"energy: {inspectedCell.energy:F4}");
        GUILayout.Label($"energyFlow: {((DirectionFlags)inspectedCell.energyFlow).Symbol()} ({inspectedCell.energyFlow})");
        GUILayout.Label($"parentDir: {((Direction)inspectedCell.parentDir).Symbol()} ({inspectedCell.parentDir})");
        // GUILayout.Label($"genomeId: {inspectedCell.genomeId}");
        GUILayout.Label($"direction: {((Direction)inspectedCell.direction).Symbol()} ({inspectedCell.direction})");
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
            // var cond1String1 = cond1 >= 13 ? "-" : $"{cond1}";
            // GUILayout.Label($"condition 1: {cond1String1} ({inspectedGene.condParam1}) (raw: {GetByte(inspectedGene.conditions, 0)})");
            // var cond2 = GetByte(inspectedGene.conditions, 1) % 26;
            // var cond2String1 = cond2 >= 13 ? "-" : $"{cond2}";
            // GUILayout.Label($"condition 2: {cond2String1} ({inspectedGene.condParam2}) (raw: {GetByte(inspectedGene.conditions, 1)})");

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

                uint commandNum = isSingle ? 10u : 13u;
                uint cr0 = GetByte(isSingle ? g.aloneCommands : g.condResult, 0) % (commandNum * 2);
                string cr0String = cr0 >= commandNum ? "- " : (isSingle ? $"{commandLookupSingle[cr0]}" : $"{commandLookupCommon[cr0]}");
                uint cr1 = GetByte(isSingle ? g.aloneCommands : g.condResult, 1) % (commandNum * 2);
                string cr1String = cr1 >= commandNum ? "- " : (isSingle ? $"{commandLookupSingle[cr1]}" : $"{commandLookupCommon[cr1]}");
                uint gr0 = GetByte(g.condResult, 2) % 32u;
                uint gr1 = GetByte(g.condResult, 3) % 32u;

                uint cg1 = GetByte(isSingle ? g.aloneComGenes : g.comGenes, 0) % 32u;
                uint cg2 = GetByte(isSingle ? g.aloneComGenes : g.comGenes, 1) % 32u;
                uint cg3 = GetByte(isSingle ? g.aloneComGenes : g.comGenes, 2) % 32u;
                uint cg4 = GetByte(isSingle ? g.aloneComGenes : g.comGenes, 3) % 32u;

                string activeMark = (i == inspectedCell.activeGene) ? "<color=green>" : "<color=white>";

                // Compact one-line representation per gene
                sb.AppendFormat("{0}{1:00}: [{2}{3}{4}{5}] | {6}({7:F1}), {8}({9:F1}) | [{10}|{14},{15}], [{11}|{16},{17}] | {12}, {13}</color>\n",
                    activeMark, i, grow0, grow1, grow2, grow3,
                    cond1String, g.condParam1, cond2String, g.condParam2,
                    cr0String, cr1String, gr0, gr1,
                    cg1, cg2, cg3, cg4
                );
            }

            // GUILayout.Label($"Genes:");
            GUILayout.Label(sb.ToString());
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
            inspectedCell = cell.cell;
            inspectedSoil.x = cell.soilOrg;
            inspectedSoil.y = cell.soilNrg;
            inspectedDebug.x = (int)cell.debug0;
            inspectedDebug.y = (int)cell.debug1;
            inspectedGenome = genome;
            haveInspectedCell = inspectedCell.cellType > 0;
            if (haveInspectedCell)
                return;
        });
        // inspectedCell = world.CellsData[idx];
        return;
    }

    void ReadCommandFromGpu(int gx, int gy)
    {
        if (world == null) return;
        int idx = gy * world.SimParams._Width + gx;

        world.RequestCommand(gx, gy, (command) =>
        {
            inspectedCommand = command;
        });
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
        if (executeSubstep) world?.StepSubstep();
        else world?.Step();
    }

    void ApplySimParameters()
    {
        if (world == null) return;

        world.SimParams = _simParams;
        world.CellConstants = _cellConstants;
    }

    void ApplyBrushAtScreenPosition(Vector2 screenPos)
    {
        if (world == null) return;
        int gx, gy;
        if (!ScreenToGrid(screenPos, out gx, out gy)) return;

        float worldBrushRadius = brushRadius; // This is now screen pixels
        if (panZoom != null && panRawImage != null)
        {
            RectTransform rt = panRawImage.rectTransform;
            Vector2 size = rt.rect.size; // Screen size of the RawImage
            var uv = panRawImage.uvRect;

            // 1 screen pixel covers (world.SimParams._Width * uv.width) / size.x grid cells.
            worldBrushRadius = brushRadius * (world.SimParams._Width * uv.width) / size.x;
        }

        int r = Mathf.Max(1, Mathf.CeilToInt(worldBrushRadius));
        float r2 = worldBrushRadius * worldBrushRadius;

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
                    // case BrushMode.SetCell:
                    //     world.ScheduleSetCell(x, y, Mathf.FloorToInt(brushCellType));
                    //     break;
                }
            }
        }

        // dispatch the corresponding mutator kernels immediately
        try {
            if (activeBrush == BrushMode.AddOrganics) world.RunOrganicsNow();
            if (activeBrush == BrushMode.AddEnergy) world.RunEnergyNow();
            if (activeBrush == BrushMode.KillCell) world.RunKillNow();
            // if (activeBrush == BrushMode.SetCell) world.RunSetCellNow();
        } catch { }
    }

    uint GetByte(uint container, int byteIdx)
    {
        return (container >> (byteIdx * 8)) & 0xffu;
    }

    void UpdateBrush(Vector2 center, float radius, Color color)
    {
        brushRenderer.color = color;

        brushRenderer.rectTransform.anchoredPosition = center;
        brushRenderer.rectTransform.sizeDelta = new Vector2(radius * 2, radius * 2);
    }
}
