using System;
using UnityEngine;

// Simple IMGUI-based simulation controller. Provides controls and stubs
// for features to be implemented later. Does not modify other classes.
public class SimulationUI : MonoBehaviour
{
    public WorldSimulation world;
    public WorldRenderer wRenderer;

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
    public WorldRenderer.RenderMode activeRenderMode = WorldRenderer.RenderMode.Full;
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

    void Reset()
    {
        // try to auto-assign world
        if (world == null) world = FindAnyObjectByType<WorldSimulation>();
        if (!wRenderer) wRenderer = FindAnyObjectByType<WorldRenderer>();
    }

    void Update()
    {
        // keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isPaused) StepOnce();
            else isPaused = !isPaused;
        }

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
        }

        // brush input: apply while holding the brush mouse button
        if (activeBrush != BrushMode.None && Input.GetMouseButton(brushMouseButton) && world != null)
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

        // GUILayout.Space(6);
        // GUILayout.Label("World Parameters", GUI.skin.label);
        // GUILayout.Label($"Sunlight: {sunlight:F2}");
        // sunlight = GUILayout.HorizontalSlider(sunlight, 0f, 8f);
        // GUILayout.Label($"Diffusion Rate: {diffusionRate:F2}");
        // diffusionRate = GUILayout.HorizontalSlider(diffusionRate, 0f, 1f);
        // GUILayout.Label($"Cell energy cost/tick: {cellEnergyCost:F4}");
        // cellEnergyCost = GUILayout.HorizontalSlider(cellEnergyCost, 0f, 0.1f);

        // if (GUILayout.Button("Apply World Params")) ApplyWorldParameters();

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
        activeRenderMode = (WorldRenderer.RenderMode)GUILayout.SelectionGrid((int)activeRenderMode, Enum.GetNames(typeof(WorldRenderer.RenderMode)), 3);
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

    void ApplyWorldParameters()
    {
        if (world == null) return;
        // Apply to compute shader if available
        if (world.simulationShader != null)
        {
            try { world.simulationShader.SetFloat("_Sunlight", sunlight); } catch {}
            try { world.simulationShader.SetFloat("_DiffusionRate", diffusionRate); } catch {}
            try { world.simulationShader.SetFloat("_Timestep", 1.0f); } catch {}
        }
    }

    void CreateSeedFromGenome()
    {
        // stub: parse genomeInput and enqueue creation of a seed cell with this genome
        Debug.Log("CreateSeedFromGenome: stub - parsed input: " + genomeInput);
    }

    void ApplyBrushAtScreenPosition(Vector2 screenPos)
    {
        if (world == null) return;
        // normalize screen to [0..1]
        float nx = Mathf.Clamp01(screenPos.x / Screen.width);
        float ny = Mathf.Clamp01(screenPos.y / Screen.height);

        int gx = Mathf.Clamp((int)(nx * world.width), 0, world.width - 1);
        int gy = Mathf.Clamp((int)(ny * world.height), 0, world.height - 1);

        int r = Mathf.Max(1, Mathf.CeilToInt(brushRadius));
        float r2 = brushRadius * brushRadius;

        // iterate over integer grid within radius and schedule changes
        for (int oy = -r; oy <= r; ++oy)
        {
            int y = gy + oy;
            if (y < 0 || y >= world.height) continue;
            for (int ox = -r; ox <= r; ++ox)
            {
                int x = gx + ox;
                if (x < 0 || x >= world.width) continue;
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
}
