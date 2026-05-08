using System;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldSimulation : MonoBehaviour
{
    public ComputeShader initShader;
    public ComputeShader simulationShader;
    public ComputeShader behaviorShader;
    public ComputeShader mutationShader;
    public int width = 128;
    public int height = 128;
    public int genomeCapacity = 1024;

    // public bool isPaused = true;

    RenderTexture soilRT0;
    RenderTexture soilRT1;
    bool ping = false;
    bool isStarted = false;

    ConstantBuffer<SimParams> simParamsBuffer;
    ComputeBuffer cellsBuffer;
    // ComputeBuffer cellsBuffer1;
    ComputeBuffer genomesBuffer;
    ComputeBuffer commandBuffer;
    ComputeBuffer statsBuffer;

    int kernelEnergyIdx;
    int kernelLeafIdx;
    int kernelRootIdx;
    int kernelAntennaIdx;
    int kernelRerouteIdx;
    int kernelTransportIdx;
    int kernelAbsorbIdx;
    int kernelDeathIdx;
    // int kernelBehaviorIdx;
    int kernelDecisionIdx;
    int[] kernelCmdIdx = new int[20];
    int kernelStatsIdx;
    int kernelMutOrganicsIdx = -1;
    int kernelMutEnergyIdx = -1;
    int kernelMutKillIdx = -1;
    int kernelSetCellIdx = -1;
    int kernelCopyCellIdx = -1;
    int kernelCopyGenomeIdx = -1;

    uint[] stats;

    // mutator buffers and pending lists
    ComputeBuffer organicsDeltaBuffer;
    ComputeBuffer energyDeltaBuffer;
    ComputeBuffer killBuffer;
    ComputeBuffer setCellBuffer;
    // small single-item buffers for readback
    ComputeBuffer singleCellBuffer;
    ComputeBuffer singleGenomeBuffer;

    bool organicsScheduled = false;
    bool energyScheduled = false;
    bool killScheduled = false;
    bool setScheduled = false;

    struct ResourceDelta { public uint x; public uint y; public float delta; public uint pad; }
    struct KillCoord { public uint x; public uint y; public uint pad0; public uint pad1; }
    struct SetCell { public uint x; public uint y; public uint type; public uint pad0; }
    System.Collections.Generic.List<ResourceDelta> organicsPending = new System.Collections.Generic.List<ResourceDelta>();
    System.Collections.Generic.List<ResourceDelta> energyPending = new System.Collections.Generic.List<ResourceDelta>();
    System.Collections.Generic.List<KillCoord> killPending = new System.Collections.Generic.List<KillCoord>();
    System.Collections.Generic.List<SetCell> setPending = new System.Collections.Generic.List<SetCell>();

    public RenderTexture SoilRTSource => ping ? soilRT1 : soilRT0;
    public RenderTexture SoilRTTarget => ping ? soilRT0 : soilRT1;

    public ComputeBuffer CellsBuffer => cellsBuffer;
    public ComputeBuffer GenomesBuffer => genomesBuffer;

    public CellData[] CellsData;
    public GenomeData[] GenomesData;
    public SimParams SimParams;

    public GenomeData GetGenome(int id) => GenomesData[id]; // GenomeData[CellsData[idx].genomeIdx

    public uint CellsNum => stats[0];
    public uint LeavesNum => stats[1];

    void Start()
    {
        InitGpuResources();
        InitWorld();
        PopulateWorld();
        // CreateTestCells();
    }

    public void InitGpuResources()
    {
        if (simulationShader == null) { Debug.LogError("Assign Simulation.compute to simulationShader"); return; }

        // kernels
        kernelEnergyIdx = simulationShader.FindKernel("DiffuseEnergyKernel");
        kernelLeafIdx = simulationShader.FindKernel("LeafKernel");
        kernelRootIdx = simulationShader.FindKernel("RootKernel");
        kernelAntennaIdx = simulationShader.FindKernel("AntennaKernel");
        kernelRerouteIdx = simulationShader.FindKernel("RerouteKernel");
        kernelTransportIdx = simulationShader.FindKernel("TransportKernel");
        kernelAbsorbIdx = simulationShader.FindKernel("AbsorptionKernel");
        kernelDeathIdx = simulationShader.FindKernel("DeathKernel");
        // kernelBehaviorIdx = simulationShader.FindKernel("BehaviorKernel");
        kernelStatsIdx = simulationShader.FindKernel("StatsKernel");

        try { kernelDecisionIdx = behaviorShader.FindKernel("DecisionKernel"); } catch { kernelDecisionIdx = -1; }
        try { kernelCmdIdx[0] = behaviorShader.FindKernel("Cmd_0_Skip"); } catch { kernelCmdIdx[0] = -1; }
        try { kernelCmdIdx[1] = behaviorShader.FindKernel("Cmd_1_Grow"); } catch { kernelCmdIdx[1] = -1; }
        try { kernelCmdIdx[2] = behaviorShader.FindKernel("Cmd_2_Move"); } catch { kernelCmdIdx[2] = -1; }
        try { kernelCmdIdx[3] = behaviorShader.FindKernel("Cmd_3_RotR"); } catch { kernelCmdIdx[3] = -1; }
        try { kernelCmdIdx[4] = behaviorShader.FindKernel("Cmd_4_RotL"); } catch { kernelCmdIdx[4] = -1; }
        try { kernelCmdIdx[5] = behaviorShader.FindKernel("Cmd_5_BecomeSeed"); } catch { kernelCmdIdx[5] = -1; }
        try { kernelCmdIdx[6] = behaviorShader.FindKernel("Cmd_6_Eat"); } catch { kernelCmdIdx[6] = -1; }
        try { kernelCmdIdx[7] = behaviorShader.FindKernel("Cmd_7_Attach"); } catch { kernelCmdIdx[7] = -1; }
        try { kernelCmdIdx[8] = behaviorShader.FindKernel("Cmd_8_ExtractOrg"); } catch { kernelCmdIdx[8] = -1; }
        try { kernelCmdIdx[9] = behaviorShader.FindKernel("Cmd_9_ExtractNrg"); } catch { kernelCmdIdx[9] = -1; }
        try { kernelCmdIdx[10] = behaviorShader.FindKernel("Cmd_10_Separate"); } catch { kernelCmdIdx[10] = -1; }
        try { kernelCmdIdx[11] = behaviorShader.FindKernel("Cmd_11_MoveOrgR"); } catch { kernelCmdIdx[11] = -1; }
        try { kernelCmdIdx[12] = behaviorShader.FindKernel("Cmd_12_MoveOrgF"); } catch { kernelCmdIdx[12] = -1; }
        try { kernelCmdIdx[13] = behaviorShader.FindKernel("Cmd_13_MoveOrgL"); } catch { kernelCmdIdx[13] = -1; }
        try { kernelCmdIdx[14] = behaviorShader.FindKernel("Cmd_14_MoveNrgR"); } catch { kernelCmdIdx[14] = -1; }
        try { kernelCmdIdx[15] = behaviorShader.FindKernel("Cmd_15_MoveNrgF"); } catch { kernelCmdIdx[15] = -1; }
        try { kernelCmdIdx[16] = behaviorShader.FindKernel("Cmd_16_MoveNrgL"); } catch { kernelCmdIdx[16] = -1; }
        try { kernelCmdIdx[17] = behaviorShader.FindKernel("Cmd_17_Die"); } catch { kernelCmdIdx[17] = -1; }
        try { kernelCmdIdx[18] = behaviorShader.FindKernel("Cmd_18_SendSeed"); } catch { kernelCmdIdx[18] = -1; }
        try { kernelCmdIdx[19] = behaviorShader.FindKernel("Cmd_19_SpitEnergy"); } catch { kernelCmdIdx[19] = -1; }

        // rendertextures
        soilRT0 = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat);
        soilRT0.enableRandomWrite = true;
        soilRT0.Create();
        soilRT1 = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat);
        soilRT1.enableRandomWrite = true;
        soilRT1.Create();

        // buffers
        cellsBuffer = new ComputeBuffer(width*height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellData)));
        Shader.SetGlobalBuffer("_Cells",cellsBuffer);

        var emptyGenomes = new GenomeData[genomeCapacity];
        for (int i = 0; i < genomeCapacity; i++)
        {
            emptyGenomes[i] = new GenomeData(); // Default struct constructor initializes fields (like cellNum) to 0.
        }
        genomesBuffer = new ComputeBuffer(Mathf.Max(1, genomeCapacity), System.Runtime.InteropServices.Marshal.SizeOf(typeof(GenomeData)));
        genomesBuffer.SetData(emptyGenomes);
        Shader.SetGlobalBuffer("_Genomes", genomesBuffer);

        statsBuffer = new ComputeBuffer(4, sizeof(uint));
        // zero stats
        stats = new uint[4];
        statsBuffer.SetData(stats);
        Shader.SetGlobalBuffer("_Stats", statsBuffer);

        // set constants
        SimParams = new()
        {
            _Width = width,
            _Height = height,
            _Timestep = 0.1f,
            _GenomeCapacity = genomeCapacity
        };
        simParamsBuffer = new ConstantBuffer<SimParams>();
        UpdateTimestamp();
        simParamsBuffer.SetGlobal(Shader.PropertyToID("_SimParams"));

        simulationShader.SetFloat("_Sunlight", 1.0f);
        simulationShader.SetFloat("_DiffusionRate", 0.2f);
        simulationShader.SetFloat("_CriticalOrg", 1.0f);
        simulationShader.SetFloat("_CriticalNrg", 1.0f);

        // allocate command buffer (4 uints per entry)
        int cmdElemSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CommandEntry));
        commandBuffer = new ComputeBuffer(Mathf.Max(1, width * height), cmdElemSize);
        // zero init
        var zeros = new CommandEntry[width * height];
        commandBuffer.SetData(zeros);
        Shader.SetGlobalBuffer("_CommandBuffer", commandBuffer);

        // create mutator buffers (capacity = width*height)
        int maxDeltas = width * height;
        organicsDeltaBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        energyDeltaBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        killBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(KillCoord)));
        setCellBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SetCell)));

        // find mutator kernels and bind buffers if present
        try {
            kernelMutOrganicsIdx = mutationShader.FindKernel("ApplyOrganicsKernel");
            mutationShader.SetBuffer(kernelMutOrganicsIdx, "_OrganicsDeltas", organicsDeltaBuffer);
        } catch { kernelMutOrganicsIdx = -1; }
        try {
            kernelMutEnergyIdx = mutationShader.FindKernel("ApplyEnergyKernel");
            mutationShader.SetBuffer(kernelMutEnergyIdx, "_EnergyDeltas", energyDeltaBuffer);
        } catch { kernelMutEnergyIdx = -1; }
        try {
            kernelMutKillIdx = mutationShader.FindKernel("KillCellsKernel");
            mutationShader.SetBuffer(kernelMutKillIdx, "_KillList", killBuffer);
        } catch { kernelMutKillIdx = -1; }
        try {
            kernelSetCellIdx = mutationShader.FindKernel("SetCellsKernel");
            mutationShader.SetBuffer(kernelSetCellIdx, "_SetList", setCellBuffer);
        } catch { kernelSetCellIdx = -1; }

        // create single-element readback buffers and bind copy kernels
        singleCellBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellData)));
        singleGenomeBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GenomeData)));
        try {
            kernelCopyCellIdx = mutationShader.FindKernel("CopyCellKernel");
            mutationShader.SetBuffer(kernelCopyCellIdx, "_SingleCellOut", singleCellBuffer);
        } catch { kernelCopyCellIdx = -1; }
        try {
            kernelCopyGenomeIdx = mutationShader.FindKernel("CopyGenomeKernel");
            mutationShader.SetBuffer(kernelCopyGenomeIdx, "_SingleGenomeOut", singleGenomeBuffer);
        } catch { kernelCopyGenomeIdx = -1; }

        // initialize soil as zeros (MVP)
        Graphics.SetRenderTarget(soilRT0);
        GL.Clear(false, true, Color.black);
        Graphics.SetRenderTarget(soilRT1);
        GL.Clear(false, true, Color.black);
    }

    public void PopulateWorld()
    {
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);
        UpdateTimestamp();

        // init starting cells
        int cellsKernelIdx = initShader.FindKernel("InitCells");
        initShader.SetInt("_CellRowCount", cx);
        initShader.Dispatch(cellsKernelIdx, cx, cy, 1);

        isStarted = true;

        // get data (stats only). Avoid full-buffer readbacks here because they are
        // expensive. Use `RequestCellAndGenome(x,y, callback)` to read a single cell
        // and its genome on demand instead of reading the whole buffers.
        AsyncGPUReadback.Request(statsBuffer, request =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<uint>();

            // Debug.Log(Data[63].value);
            // submit updated date
            stats = data.ToArray();
        });
    }

    public void InitWorld()
    {
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);

        // init starting organics
        int orgKernelIdx = initShader.FindKernel("InitOrganicsKernel");
        UpdateTimestamp();
        initShader.SetTexture(orgKernelIdx, "_SoilTexRead", SoilRTSource);
        initShader.SetTexture(orgKernelIdx, "_SoilTexWrite", SoilRTTarget);
        initShader.SetFloat("_StartOrganic", 0.5f);
        initShader.Dispatch(orgKernelIdx, cx, cy, 1);
        FlipSoilTex();

        // init starting energy
        int nrgKernelIdx = initShader.FindKernel("InitEnergyKernel");
        UpdateTimestamp();
        initShader.SetTexture(nrgKernelIdx, "_SoilTexRead", SoilRTSource);
        initShader.SetTexture(nrgKernelIdx, "_SoilTexWrite", SoilRTTarget);
        initShader.SetFloat("_MeanEnergy", 0.5f);
        initShader.Dispatch(nrgKernelIdx, cx, cy, 1);
        FlipSoilTex();

        // clean the field from cells
        int cellsKernelIdx = initShader.FindKernel("CleanCells");
        UpdateTimestamp();
        initShader.Dispatch(cellsKernelIdx, cx, cy, 1);
    }

    public void Step()
    {
        if (!isStarted)
        {
            CreateTestCells();
            isStarted = true;
        }

        // apply scheduled mutator operations between steps
        if (organicsScheduled && kernelMutOrganicsIdx >= 0)
        {
            RunOrganicsNow();
            organicsScheduled = false;
        }
        if (energyScheduled && kernelMutEnergyIdx >= 0)
        {
            RunEnergyNow();
            energyScheduled = false;
        }
        if (killScheduled && kernelMutKillIdx >= 0)
        {
            RunKillNow();
            killScheduled = false;
        }
        if (setScheduled && kernelMutKillIdx >= 0)
        {
            RunSetCellNow();
            setScheduled = false;
        }

        // Build a single command buffer to execute soil, decision and per-command kernels as a pipeline
        var cb = new CommandBuffer();
        cb.name = "SimulationPipeline";

        // simParamsBuffer.SetGlobal(cb, Shader.PropertyToID("_SimParams"));
        UpdateTimestamp();
        cb.SetGlobalTexture("_SoilTexRead", SoilRTSource);
        cb.SetGlobalTexture("_SoilTexWrite", SoilRTTarget);

        int tx = Mathf.CeilToInt(width / 8f);
        int ty = Mathf.CeilToInt(height / 8f);

        // soil kernels
        cb.DispatchCompute(simulationShader, kernelEnergyIdx, tx, ty, 1);

        // cell kernels
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);

        // Leafs
        cb.DispatchCompute(simulationShader, kernelLeafIdx, cx, cy, 1);

        // Roots
        cb.DispatchCompute(simulationShader, kernelRootIdx, cx, cy, 1);

        // Antennas
        cb.DispatchCompute(simulationShader, kernelAntennaIdx, cx, cy, 1);

        // Transport
        cb.DispatchCompute(simulationShader, kernelRerouteIdx, cx, cy, 1);
        cb.DispatchCompute(simulationShader, kernelTransportIdx, cx, cy, 1);

        // Absorption
        cb.DispatchCompute(simulationShader, kernelAbsorbIdx, cx, cy, 1);

        // Death
        cb.DispatchCompute(simulationShader, kernelDeathIdx, cx, cy, 1);

        // Behavior
        // if (kernelBehaviorIdx >= 0) {
        //     cb.SetComputeTextureParam(simulationShader, kernelBehaviorIdx, "_SoilTexRead", SoilRTSource);
        //     cb.SetComputeTextureParam(simulationShader, kernelBehaviorIdx, "_SoilTexWrite", SoilRTTarget);
        //     cb.SetComputeFloatParam(simulationShader, "_Rand", UnityEngine.Random.value);
        // }
        // Decision kernel: choose command per cell
        if (kernelDecisionIdx >= 0) cb.DispatchCompute(behaviorShader, kernelDecisionIdx, cx, cy, 1);

        // Per-command kernels
        for (int i = 0; i < kernelCmdIdx.Length; ++i)
        {
            cb.DispatchCompute(behaviorShader, kernelCmdIdx[i], cx, cy, 1);
        }

        // apply set-active-only kernel if present
        // try {
        //     int kset = behaviorShader.FindKernel("Cmd_SetActiveOnly");
        //     if (kset >= 0) cb.DispatchCompute(behaviorShader, kset, cx, cy, 1);
        // } catch {}

        // Stats kernel
        stats = new uint[4];
        statsBuffer.SetData(stats);
        if (kernelStatsIdx >= 0) cb.DispatchCompute(simulationShader, kernelStatsIdx, 1, 1, 1);

        // Execute the assembled command buffer once on GPU
        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();

        // read back stats
        AsyncGPUReadback.Request(statsBuffer, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<uint>();
            stats = data.ToArray();
        });

        FlipSoilTex();
    }

    void CreateTestCells()
    {
        int cellCapacity = width * height;
        var arr = new CellData[cellCapacity];
        for (int i = 0; i < cellCapacity; ++i) { arr[i].cellType = 0; arr[i].energy = 0; }
        // add a Leaf in center
        int midx = width/2, midy = height/2;
        arr[midy * height + midx] = new CellData() { cellType = CellType.Leaf, energy = 1.0f };
        arr[midy * height + midx + 1] = new CellData() { cellType = CellType.Wood, energy = 0.5f };
        cellsBuffer.SetData(arr);
    }

    private void OnDestroy()
    {
        simParamsBuffer?.Release();
        cellsBuffer?.Release();
        genomesBuffer?.Release();
        statsBuffer?.Release();
        organicsDeltaBuffer?.Release();
        energyDeltaBuffer?.Release();
        killBuffer?.Release();
        singleCellBuffer?.Release();
        singleGenomeBuffer?.Release();
        commandBuffer?.Release();
        if (soilRT0 != null) soilRT0.Release();
        if (soilRT1 != null) soilRT1.Release();
    }

    private void FlipSoilTex()
    {
        Graphics.Blit(SoilRTTarget, SoilRTSource);
        ping = !ping;
        // Graphics.CopyBuffer(cellsBuffer, killBuffer);
    }

    private void UpdateTimestamp() {
        SimParams._Timestamp = Time.time;
        SimParams._Rand = UnityEngine.Random.value;

        simParamsBuffer.UpdateData(SimParams);
    }

    // void Update()
    // {
    //     if (!isPaused) Step();
    // }

    // ---- Mutator API ----
    public void ScheduleOrganicsChange(int x, int y, float delta)
    {
        organicsPending.Add(new ResourceDelta() { x = (uint)x, y = (uint)y, delta = delta, pad = 0 });
        organicsScheduled = true;
    }

    public void ScheduleEnergyChange(int x, int y, float delta)
    {
        energyPending.Add(new ResourceDelta() { x = (uint)x, y = (uint)y, delta = delta, pad = 0 });
        energyScheduled = true;
    }

    public void ScheduleKillAt(int x, int y)
    {
        killPending.Add(new KillCoord() { x = (uint)x, y = (uint)y, pad0 = 0, pad1 = 0 });
        killScheduled = true;
    }

    public void ScheduleSetCell(int x, int y, int inType)
    {
        setPending.Add(new SetCell() { x = (uint)x, y = (uint)y, type = (uint)inType, pad0 = 0 });
        setScheduled = true;
    }

    public void RunOrganicsNow()
    {
        if (kernelMutOrganicsIdx < 0) return;
        int count = organicsPending.Count;
        if (count == 0) return;
        organicsDeltaBuffer.SetData(organicsPending);
        mutationShader.SetInt("_OrganicsCount", count);
        mutationShader.SetTexture(kernelMutOrganicsIdx, "_SoilTexRead", SoilRTSource);
        mutationShader.SetTexture(kernelMutOrganicsIdx, "_SoilTexWrite", SoilRTTarget);
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelMutOrganicsIdx, groups, 1, 1);
        organicsPending.Clear();
        FlipSoilTex();
    }

    public void RunEnergyNow()
    {
        if (kernelMutEnergyIdx < 0) return;
        int count = energyPending.Count;
        if (count == 0) return;
        energyDeltaBuffer.SetData(energyPending);
        mutationShader.SetInt("_EnergyCount", count);
        mutationShader.SetTexture(kernelMutEnergyIdx, "_SoilTexRead", SoilRTSource);
        mutationShader.SetTexture(kernelMutEnergyIdx, "_SoilTexWrite", SoilRTTarget);
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelMutEnergyIdx, groups, 1, 1);
        energyPending.Clear();
        FlipSoilTex();
    }

    public void RunKillNow()
    {
        if (kernelMutKillIdx < 0) return;
        int count = killPending.Count;
        if (count == 0) return;
        killBuffer.SetData(killPending);
        mutationShader.SetInt("_KillCount", count);
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelMutKillIdx, groups, 1, 1);
        killPending.Clear();
    }

    public void RunSetCellNow()
    {
        if (kernelSetCellIdx < 0) return;
        int count = setPending.Count;
        if (count == 0) return;
        setCellBuffer.SetData(setPending);
        mutationShader.SetInt("_SetCount", count);
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelSetCellIdx, groups, 1, 1);
        setPending.Clear();
    }

    // Read a single cell at (x,y) and its genome asynchronously.
    // onComplete is called with the CellData and GenomeData (GenomeData may be default if absent).
    public void RequestCellAndGenome(int x, int y, Action<CellData, GenomeData> onComplete)
    {
        int idx = y * width + x;
        int total = width * height;
        if (idx < 0 || idx >= total) {
            Debug.LogWarning($"RequestCellAndGenome: coords out of range ({x},{y})");
            onComplete?.Invoke(default, default);
            return;
        }
        // Use small one-element buffers and copy kernels to avoid allocating/reading entire buffers
        if (kernelCopyCellIdx < 0) {
            Debug.LogError("CopyCellKernel not available in mutationShader");
            onComplete?.Invoke(default, default);
            return;
        }

        // set requested index and dispatch copy kernel
        mutationShader.SetInt("_RequestedCellIdx", idx);
        mutationShader.Dispatch(kernelCopyCellIdx, 1, 1, 1);

        // read back single cell buffer
        AsyncGPUReadback.Request(singleCellBuffer, (AsyncGPUReadbackRequest req) =>
        {
            if (req.hasError) {
                Debug.LogError("Ошибка чтения клетки с GPU");
                onComplete?.Invoke(default, default);
                return;
            }

            var cellArr = req.GetData<CellData>();
            var cell = cellArr[0];

            int gidx = (int)cell.genomeId;
            if (cell.cellType == CellType.Empty || kernelCopyGenomeIdx < 0 || genomesBuffer == null || gidx < 0 || gidx >= genomesBuffer.count)
            {
                onComplete?.Invoke(cell, default);
                return;
            }

            // dispatch genome copy
            mutationShader.SetInt("_RequestedGenomeIdx", gidx);
            mutationShader.Dispatch(kernelCopyGenomeIdx, 1, 1, 1);

            AsyncGPUReadback.Request(singleGenomeBuffer, (AsyncGPUReadbackRequest req2) =>
            {
                if (req2.hasError) {
                    Debug.LogError("Ошибка чтения генома с GPU");
                    onComplete?.Invoke(cell, default);
                    return;
                }

                var gArr = req2.GetData<GenomeData>();
                var genome = gArr[0];
                onComplete?.Invoke(cell, genome);
            });
        });
    }
}
