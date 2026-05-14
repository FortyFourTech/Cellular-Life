using System;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldSimulation : MonoBehaviour
{
    public ComputeShader initShader;
    public ComputeShader simulationShader;
    public ComputeShader behaviorShader;
    public ComputeShader mutationShader;
    [SerializeField] private GenomeStorage _genomeStorage;
    public SimParams SimParams;

    // public bool isPaused = true;

    PingPongTexture _soilTex;
    // PingPongTexture _cellEnergyTex;
    PingPongBuffer _cellsBuffer;

    ConstantBuffer<SimParams> simParamsBuffer;
    GraphicsBuffer genomesBuffer;
    GraphicsBuffer killBuffer;
    GraphicsBuffer commandBuffer;
    GraphicsBuffer statsBuffer;
    GraphicsBuffer debugBuffer;

    int kernelEnergyIdx;
    int kernelLeafIdx;
    int kernelRootIdx;
    int kernelAntennaIdx;
    int kernelRerouteIdx;
    int kernelTransportIdx;
    int kernelAbsorbIdx;
    int kernelDeathIdx;
    int kernelKillIdx;
    int kernelSeedIdx;
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
    int kernelCopyCommandIdx = -1;

    SimStats stats;

    // mutator buffers and pending lists
    GraphicsBuffer organicsDeltaBuffer;
    GraphicsBuffer energyDeltaBuffer;
    GraphicsBuffer killCellsBuffer;
    GraphicsBuffer setCellBuffer;
    // small single-item buffers for readback
    GraphicsBuffer singleCellBuffer;
    GraphicsBuffer singleGenomeBuffer;
    GraphicsBuffer singleCommandbuffer;

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

    public RenderTexture SoilTexture => _soilTex.ReadResource;

    public GraphicsBuffer CellsBuffer => _cellsBuffer.ReadResource;
    public GraphicsBuffer GenomesBuffer => genomesBuffer;

    public uint SubstepIdx => (uint)_substepIdx;

    public SimStats Stats => stats;

    private int _substepIdx = 0;
    private const int _substeps = 12;

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
        kernelKillIdx = simulationShader.FindKernel("KillKernel");
        kernelSeedIdx = simulationShader.FindKernel("SeedKernel");
        // kernelBehaviorIdx = simulationShader.FindKernel("BehaviorKernel");
        kernelStatsIdx = simulationShader.FindKernel("StatsKernel");

        try { kernelDecisionIdx = behaviorShader.FindKernel("DecisionKernel"); } catch { kernelDecisionIdx = -1; }
        for (int i = 0; i < kernelCmdIdx.Length; i++) {
            try { kernelCmdIdx[i] = behaviorShader.FindKernel($"Cmd_{i+1}"); } catch { kernelCmdIdx[i] = -1; }
        }

        // rendertextures
        _soilTex = new PingPongTexture(SimParams._Width, SimParams._Height, RenderTextureFormat.RGFloat, "_SoilTex");
        // _cellEnergyTex = new PingPongTexture(SimParams._Width, SimParams._Height, RenderTextureFormat.RFloat, "_CellNrgTex");

        // buffers
        int cellsCapacity = SimParams._Width * SimParams._Height;
        _cellsBuffer = new PingPongBuffer(cellsCapacity, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellData)), "_Cells");

        int genomeCapacity = cellsCapacity;
        var emptyGenomes = new GenomeData[genomeCapacity];
        for (int i = 0; i < genomeCapacity; i++)
        {
            emptyGenomes[i] = new GenomeData(); // Default struct constructor initializes fields (like cellNum) to 0.
        }
        genomesBuffer = new (GraphicsBuffer.Target.Structured, Mathf.Max(1, genomeCapacity), System.Runtime.InteropServices.Marshal.SizeOf(typeof(GenomeData)));
        genomesBuffer.SetData(emptyGenomes);
        Shader.SetGlobalBuffer("_Genomes", genomesBuffer);

        killBuffer = new (GraphicsBuffer.Target.Structured, cellsCapacity, 4);
        Shader.SetGlobalBuffer("_KillCells", killBuffer);

        statsBuffer = new (GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SimStats)));
        // zero stats
        stats = new SimStats();
        statsBuffer.SetData(new SimStats[]{stats});
        Shader.SetGlobalBuffer("_Stats", statsBuffer);

        debugBuffer = new (GraphicsBuffer.Target.Structured, cellsCapacity, 8);
        Shader.SetGlobalBuffer("_Debug", debugBuffer);

        // set constants
        simParamsBuffer = new ConstantBuffer<SimParams>();
        UpdateTimestamp();
        simParamsBuffer.SetGlobal(Shader.PropertyToID("_SimParams"));

        // allocate command buffer (4 uints per entry)
        int cmdElemSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CommandEntry));
        commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.Max(1, cellsCapacity), cmdElemSize);
        // zero init
        var zeros = new CommandEntry[cellsCapacity];
        commandBuffer.SetData(zeros);
        Shader.SetGlobalBuffer("_CommandBuffer", commandBuffer);

        // create mutator buffers (capacity = width*height)
        int maxDeltas = cellsCapacity;
        organicsDeltaBuffer = new (GraphicsBuffer.Target.Structured, maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        energyDeltaBuffer = new (GraphicsBuffer.Target.Structured, maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        killCellsBuffer = new (GraphicsBuffer.Target.Structured, maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(KillCoord)));
        setCellBuffer = new (GraphicsBuffer.Target.Structured, maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SetCell)));

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
            mutationShader.SetBuffer(kernelMutKillIdx, "_KillList", killCellsBuffer);
        } catch { kernelMutKillIdx = -1; }
        try {
            kernelSetCellIdx = mutationShader.FindKernel("SetCellsKernel");
            mutationShader.SetBuffer(kernelSetCellIdx, "_SetList", setCellBuffer);
        } catch { kernelSetCellIdx = -1; }

        // create single-element readback buffers and bind copy kernels
        singleCellBuffer = new (GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellInfo)));
        singleGenomeBuffer = new (GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GenomeData)));
        singleCommandbuffer = new (GraphicsBuffer.Target.Structured, 1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CommandEntry)));
        try {
            kernelCopyCellIdx = mutationShader.FindKernel("CopyCellKernel");
            mutationShader.SetBuffer(kernelCopyCellIdx, "_SingleCellOut", singleCellBuffer);
        } catch { kernelCopyCellIdx = -1; }
        try {
            kernelCopyGenomeIdx = mutationShader.FindKernel("CopyGenomeKernel");
            mutationShader.SetBuffer(kernelCopyGenomeIdx, "_SingleGenomeOut", singleGenomeBuffer);
        } catch { kernelCopyGenomeIdx = -1; }
        try {
            kernelCopyCommandIdx = mutationShader.FindKernel("CopyCommandKernel");
            mutationShader.SetBuffer(kernelCopyCommandIdx, "_SingleCommandOut", singleCommandbuffer);
        } catch { kernelCopyCommandIdx = -1; }
    }

    public void InitWorld()
    {
        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);

        var cb = new CommandBuffer();
        cb.name = "InitPipeline";

        // init starting organics
        UpdateTimestamp();
        _soilTex.SetResources(cb);
        _cellsBuffer.SetResources(cb);

        cb.SetComputeFloatParam(initShader, "_StartOrganic", 0.5f);
        int orgKernelIdx = initShader.FindKernel("InitOrganicsKernel");
        cb.DispatchCompute(initShader, orgKernelIdx, cx, cy, 1);

        // init starting energy
        int nrgKernelIdx = initShader.FindKernel("InitEnergyKernel");
        cb.SetComputeFloatParam(initShader, "_MeanEnergy", 0.5f);
        cb.DispatchCompute(initShader, nrgKernelIdx, cx, cy, 1);

        // clean the field from cells
        int cellsKernelIdx = initShader.FindKernel("CleanCells");
        cb.DispatchCompute(initShader, cellsKernelIdx, cx, cy, 1);

        // clean the field from cells
        int genomesKernelIdx = initShader.FindKernel("CleanGenomes");
        cb.DispatchCompute(initShader, genomesKernelIdx, cx * cy, 1, 1);

        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
    }

    public void PopulateWorld()
    {
        int cx = Mathf.CeilToInt(SimParams._Width / 32);
        int cy = Mathf.CeilToInt(SimParams._Height / 32);


        var cb = new CommandBuffer();
        cb.name = "PopulatePipeline";

        UpdateTimestamp();
        _soilTex.SetResources(cb);
        _cellsBuffer.SetResources(cb);

        // init starting cells
        int cellsKernelIdx = initShader.FindKernel("InitCells");
        cb.SetKeyword(initShader, new LocalKeyword(initShader, "RANDOMIZE_GENOME"), _genomeStorage?.genomes.Count > 0);
        cb.SetComputeIntParam(initShader, "_CellRowCount", cx);
        cb.DispatchCompute(initShader, cellsKernelIdx, cx, cy, 1);

        // If a GenomeStorage asset is assigned, copy its genomes into the GPU buffer
        // up to the buffer capacity. Remaining entries are left as default (generated
        // by existing initialization logic).
        try {
            if (_genomeStorage != null && _genomeStorage.genomes != null && _genomeStorage.genomes.Count > 0)
            {
                int genomeCapacity = SimParams._Width * SimParams._Height;
                var arr = new GenomeData[genomeCapacity];

                int useCount = Mathf.Min(_genomeStorage.genomes.Count, genomeCapacity);
                for (int i = 0; i < useCount; ++i)
                {
                    arr[i] = _genomeStorage.genomes[i];
                }
                // remaining entries stay as default (new GenomeData())

                genomesBuffer.SetData(arr);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to apply genomes from GenomeStorage: {ex.Message}");
        }

        cb.DispatchCompute(simulationShader, kernelStatsIdx, 1, 1, 1);

        // get data (stats only). Avoid full-buffer readbacks here because they are
        // expensive. Use `RequestCellAndGenome(x,y, callback)` to read a single cell
        // and its genome on demand instead of reading the whole buffers.
        cb.RequestAsyncReadback(statsBuffer, request =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<SimStats>();

            // Debug.Log(Data[63].value);
            // submit updated date
            stats = data[0];
        });

        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
    }

    public void Step()
    {
        RunPendingMutatorOperations();

        var cb = new CommandBuffer();
        cb.name = "SimulationPipeline";

        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);

        UpdateTimestamp();
        _soilTex.SetResources(cb);
        _cellsBuffer.SetResources(cb);

        for (int i = _substepIdx; i < _substeps; i++)
        {
            _AddSubstep(cb, cx, cy, i);
            // _substepIdx = (_substepIdx + 1) % _substeps;
        }

        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();

        _substepIdx = 0;
    }

    private void RunPendingMutatorOperations()
    {
        var cb = new CommandBuffer() { name = "MutatorOperations" };

        // apply scheduled mutator operations between steps
        if (organicsScheduled && kernelMutOrganicsIdx >= 0)
        {
            RunOrganicsNow(cb);
            organicsScheduled = false;
        }
        if (energyScheduled && kernelMutEnergyIdx >= 0)
        {
            RunEnergyNow(cb);
            energyScheduled = false;
        }
        if (killScheduled && kernelMutKillIdx >= 0)
        {
            RunKillNow(cb);
            killScheduled = false;
        }
        if (setScheduled && kernelMutKillIdx >= 0)
        {
            RunSetCellNow(cb);
            setScheduled = false;
        }

        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();
    }

    private void OnDestroy()
    {
        simParamsBuffer?.Release();
        _cellsBuffer.Dispose();
        genomesBuffer?.Release();
        killBuffer?.Release();
        statsBuffer?.Release();
        debugBuffer?.Release();
        organicsDeltaBuffer?.Release();
        energyDeltaBuffer?.Release();
        setCellBuffer?.Release();
        killCellsBuffer?.Release();
        singleCellBuffer?.Release();
        singleGenomeBuffer?.Release();
        singleCommandbuffer?.Release();
        commandBuffer?.Release();

        _soilTex.Dispose();
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

    public void RunOrganicsNow(CommandBuffer cb = null)
    {
        if (organicsPending.Count == 0 || kernelMutOrganicsIdx < 0) return;

        bool runImmediately = false;
        if (cb == null) {
            cb = new CommandBuffer() { name = "OrganicsModification" };
            runImmediately = true;
        }

        int count = organicsPending.Count;
        cb.SetBufferData(organicsDeltaBuffer, organicsPending);
        organicsPending.Clear();
        cb.SetComputeIntParam(mutationShader, "_OrganicsCount", count);
        _soilTex.SetResources(cb);
        int groups = Mathf.CeilToInt((float)count / 64f);
        cb.DispatchCompute(mutationShader, kernelMutOrganicsIdx, groups, 1, 1);

        if (runImmediately) {
            Graphics.ExecuteCommandBuffer(cb);
            cb.Release();
        }
    }

    public void RunEnergyNow(CommandBuffer cb = null)
    {
        if (energyPending.Count == 0 || kernelMutEnergyIdx < 0) return;

        bool runImmediately = false;
        if (cb == null) {
            cb = new CommandBuffer() { name = "EnergyModification" };
            runImmediately = true;
        }

        int count = energyPending.Count;
        cb.SetBufferData(energyDeltaBuffer, energyPending);
        energyPending.Clear();
        cb.SetComputeIntParam(mutationShader, "_EnergyCount", count);
        _soilTex.SetResources();
        int groups = Mathf.CeilToInt((float)count / 64f);
        cb.DispatchCompute(mutationShader, kernelMutEnergyIdx, groups, 1, 1);

        if (runImmediately) {
            Graphics.ExecuteCommandBuffer(cb);
            cb.Release();
        }
    }

    public void RunKillNow(CommandBuffer cb = null)
    {
        if (killPending.Count == 0 || kernelMutKillIdx < 0) return;

        bool runImmediately = false;
        if (cb == null) {
            cb = new CommandBuffer() { name = "CellsElimination" };
            runImmediately = true;
        }

        int count = killPending.Count;
        cb.SetBufferData(killCellsBuffer, killPending);
        killPending.Clear();
        cb.SetComputeIntParam(mutationShader, "_KillCount", count);
        int groups = Mathf.CeilToInt((float)count / 64f);
        cb.DispatchCompute(mutationShader, kernelMutKillIdx, groups, 1, 1);

        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);
        cb.DispatchCompute(simulationShader, kernelKillIdx, cx, cy, 1);

        if (runImmediately) {
            Graphics.ExecuteCommandBuffer(cb);
            cb.Release();
        }
    }

    public void RunSetCellNow(CommandBuffer cb = null)
    {
        if (setPending.Count == 0 || kernelSetCellIdx < 0) return;

        bool runImmediately = false;
        if (cb == null) {
            cb = new CommandBuffer() { name = "CellsModification" };
            runImmediately = true;
        }

        int count = setPending.Count;
        cb.SetBufferData(setCellBuffer, setPending);
        setPending.Clear();
        cb.SetComputeIntParam(mutationShader, "_SetCount", count);
        int groups = Mathf.CeilToInt((float)count / 64f);
        cb.DispatchCompute(mutationShader, kernelSetCellIdx, groups, 1, 1);

        if (runImmediately) {
            Graphics.ExecuteCommandBuffer(cb);
            cb.Release();
        }
    }

    // Read a single cell at (x,y) and its genome asynchronously.
    // onComplete is called with the CellData and GenomeData (GenomeData may be default if absent).
    public void RequestCellAndGenome(int x, int y, Action<CellInfo, GenomeData> onComplete)
    {
        int idx = y * SimParams._Width + x;
        int total = SimParams._Width * SimParams._Height;
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

            var cellArr = req.GetData<CellInfo>();
            var cell = cellArr[0];

            int gidx = (int)cell.cell.genomeId;
            if (cell.cell.cellType == CellType.Empty || kernelCopyGenomeIdx < 0 || genomesBuffer == null || gidx < 0 || gidx >= genomesBuffer.count)
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

    public void RequestCommand(int x, int y, Action<CommandEntry> onComplete)
    {
        int idx = y * SimParams._Width + x;
        int total = SimParams._Width * SimParams._Height;
        if (idx < 0 || idx >= total) {
            Debug.LogWarning($"RequestCommand: coords out of range ({x},{y})");
            onComplete?.Invoke(default);
            return;
        }
        // Use small one-element buffers and copy kernels to avoid allocating/reading entire buffers
        if (kernelCopyCommandIdx < 0) {
            Debug.LogError("CopyCommandKernel not available in mutationShader");
            onComplete?.Invoke(default);
            return;
        }

        // set requested index and dispatch copy kernel
        mutationShader.SetInt("_RequestedCommandIdx", idx);
        mutationShader.Dispatch(kernelCopyCommandIdx, 1, 1, 1);

        // read back single cell buffer
        AsyncGPUReadback.Request(singleCommandbuffer, (AsyncGPUReadbackRequest req) =>
        {
            if (req.hasError) {
                Debug.LogError("Ошибка чтения команды с GPU");
                onComplete?.Invoke(default);
                return;
            }

            var cellArr = req.GetData<CommandEntry>();
            var cell = cellArr[0];

            onComplete?.Invoke(cell);
        });
    }

    public void StepSubstep()
    {
        RunPendingMutatorOperations();

        var cb = new CommandBuffer();
        cb.name = "SimulationSubstepPipeline";

        UpdateTimestamp();

        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);

        _AddSubstep(cb, cx, cy, _substepIdx);

        Graphics.ExecuteCommandBuffer(cb);
        cb.Release();

        _substepIdx = (_substepIdx + 1) % _substeps;
    }

    private void _AddSubstep(CommandBuffer cb, int cx, int cy, int substepIdx)
    {
        switch (substepIdx)
        {
            case 0: // soil energy
                cb.DispatchCompute(simulationShader, kernelEnergyIdx, cx, cy, 1);
                // _soilTex.SwapResource(cb);
                break;
            case 1: // leafs
                cb.DispatchCompute(simulationShader, kernelLeafIdx, cx, cy, 1);
                break;
            case 2: // roots
                cb.DispatchCompute(simulationShader, kernelRootIdx, cx, cy, 1);
                _soilTex.SwapResource(cb);
                break;
            case 3: // antennas
                cb.DispatchCompute(simulationShader, kernelAntennaIdx, cx, cy, 1);
                _soilTex.SwapResource(cb);
                break;
            case 4: // reroute
                cb.DispatchCompute(simulationShader, kernelRerouteIdx, cx, cy, 1);
                _cellsBuffer.SwapResource(cb);
                break;
            case 5: // transport
                cb.DispatchCompute(simulationShader, kernelTransportIdx, cx, cy, 1);
                _cellsBuffer.SwapResource(cb);
                break;
            case 6: // energy absorption
                cb.DispatchCompute(simulationShader, kernelAbsorbIdx, cx, cy, 1);
                break;
            case 7: // death
                cb.DispatchCompute(simulationShader, kernelDeathIdx, cx, cy, 1);
                cb.DispatchCompute(simulationShader, kernelKillIdx, cx, cy, 1);
                // _soilTex.SwapResource(cb);
                // _cellsBuffer.SwapResource(cb);
                break;
            case 8: // seeds
                cb.DispatchCompute(simulationShader, kernelSeedIdx, cx, cy, 1);
                cb.DispatchCompute(simulationShader, kernelKillIdx, cx, cy, 1);
                // _soilTex.SwapResource(cb);
                // _cellsBuffer.SwapResource(cb);
                break;
            case 9: // sprout decision
                cb.DispatchCompute(behaviorShader, kernelDecisionIdx, cx, cy, 1);
                break;
            case 10: // sprout commands
                for (int i = 0; i < kernelCmdIdx.Length; ++i)
                {
                    cb.DispatchCompute(behaviorShader, kernelCmdIdx[i], cx, cy, 1);
                }
                cb.DispatchCompute(simulationShader, kernelKillIdx, cx, cy, 1);
                _soilTex.SwapResource(cb);
                _soilTex.Sync(cb);
                _cellsBuffer.SwapResource(cb);
                _cellsBuffer.Sync(cb);
                break;
            case 11: // stats
                cb.DispatchCompute(simulationShader, kernelStatsIdx, 1, 1, 1);
                cb.RequestAsyncReadback(statsBuffer, request =>
                {
                    if (request.hasError) {
                        Debug.LogError("Ошибка чтения с GPU");
                        return;
                    }

                    var data = request.GetData<SimStats>();
                    stats = data[0];
                });
                break;
        }
    }
}
