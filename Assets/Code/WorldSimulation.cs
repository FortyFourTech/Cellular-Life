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

    RenderTexture soilRT0;
    RenderTexture soilRT1;

    ConstantBuffer<SimParams> simParamsBuffer;
    ComputeBuffer cellsBuffer;
    // ComputeBuffer cellsBuffer1;
    ComputeBuffer genomesBuffer;
    ComputeBuffer commandBuffer;
    ComputeBuffer statsBuffer;

    int kernelApplySoilIdx;
    int kernelEnergyIdx;
    int kernelLeafIdx;
    int kernelRootIdx;
    int kernelAntennaIdx;
    int kernelRerouteIdx;
    int kernelTransportIdx;
    int kernelAbsorbIdx;
    int kernelDeathIdx;
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

    uint[] stats;

    // mutator buffers and pending lists
    ComputeBuffer organicsDeltaBuffer;
    ComputeBuffer energyDeltaBuffer;
    ComputeBuffer killBuffer;
    ComputeBuffer setCellBuffer;
    // small single-item buffers for readback
    ComputeBuffer singleCellBuffer;
    ComputeBuffer singleGenomeBuffer;
    ComputeBuffer singleCommandbuffer;

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

    public RenderTexture SoilTexture => soilRT0;

    public ComputeBuffer CellsBuffer => cellsBuffer;
    public ComputeBuffer GenomesBuffer => genomesBuffer;

    public uint SubstepIdx => (uint)_substepIdx;

    public uint CellsNum => stats[0];
    public uint LeavesNum => stats[1];

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
        kernelApplySoilIdx = simulationShader.FindKernel("ApplySoilChange");
        kernelEnergyIdx = simulationShader.FindKernel("DiffuseEnergyKernel");
        kernelLeafIdx = simulationShader.FindKernel("LeafKernel");
        kernelRootIdx = simulationShader.FindKernel("RootKernel");
        kernelAntennaIdx = simulationShader.FindKernel("AntennaKernel");
        kernelRerouteIdx = simulationShader.FindKernel("RerouteKernel");
        kernelTransportIdx = simulationShader.FindKernel("TransportKernel");
        kernelAbsorbIdx = simulationShader.FindKernel("AbsorptionKernel");
        kernelDeathIdx = simulationShader.FindKernel("DeathKernel");
        kernelSeedIdx = simulationShader.FindKernel("SeedKernel");
        // kernelBehaviorIdx = simulationShader.FindKernel("BehaviorKernel");
        kernelStatsIdx = simulationShader.FindKernel("StatsKernel");

        try { kernelDecisionIdx = behaviorShader.FindKernel("DecisionKernel"); } catch { kernelDecisionIdx = -1; }
        for (int i = 0; i < kernelCmdIdx.Length; i++) {
            try { kernelCmdIdx[i] = behaviorShader.FindKernel($"Cmd_{i+1}"); } catch { kernelCmdIdx[i] = -1; }
        }

        // rendertextures
        soilRT0 = new RenderTexture(SimParams._Width, SimParams._Height, 0, RenderTextureFormat.RGFloat);
        soilRT0.enableRandomWrite = true;
        soilRT0.Create();
        soilRT1 = new RenderTexture(SimParams._Width, SimParams._Height, 0, RenderTextureFormat.RGFloat);
        soilRT1.enableRandomWrite = true;
        soilRT1.Create();
        // initialize soil as zeros (MVP)
        Graphics.SetRenderTarget(soilRT0);
        GL.Clear(false, true, Color.black);
        Graphics.SetRenderTarget(soilRT1);
        GL.Clear(false, true, Color.black);

        Shader.SetGlobalTexture("_SoilTexRead", soilRT0);
        Shader.SetGlobalTexture("_SoilTexWrite", soilRT1);

        // buffers
        int cellsCapacity = SimParams._Width * SimParams._Height;
        cellsBuffer = new ComputeBuffer(cellsCapacity, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellData)));
        Shader.SetGlobalBuffer("_Cells",cellsBuffer);

        int genomeCapacity = cellsCapacity;
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
        simParamsBuffer = new ConstantBuffer<SimParams>();
        UpdateTimestamp();
        simParamsBuffer.SetGlobal(Shader.PropertyToID("_SimParams"));

        // allocate command buffer (4 uints per entry)
        int cmdElemSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(CommandEntry));
        commandBuffer = new ComputeBuffer(Mathf.Max(1, cellsCapacity), cmdElemSize);
        // zero init
        var zeros = new CommandEntry[cellsCapacity];
        commandBuffer.SetData(zeros);
        Shader.SetGlobalBuffer("_CommandBuffer", commandBuffer);

        // create mutator buffers (capacity = width*height)
        int maxDeltas = cellsCapacity;
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
        singleCommandbuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CommandEntry)));
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
        cb.SetComputeFloatParam(initShader, "_StartOrganic", 0.5f);
        int orgKernelIdx = initShader.FindKernel("InitOrganicsKernel");
        cb.DispatchCompute(initShader, orgKernelIdx, cx, cy, 1);
        ApplySoilChange(cb);

        // init starting energy
        int nrgKernelIdx = initShader.FindKernel("InitEnergyKernel");
        cb.SetComputeFloatParam(initShader, "_MeanEnergy", 0.5f);
        cb.DispatchCompute(initShader, nrgKernelIdx, cx, cy, 1);
        ApplySoilChange(cb);

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
        UpdateTimestamp();

        // init starting cells
        int cellsKernelIdx = initShader.FindKernel("InitCells");
        initShader.SetInt("_CellRowCount", cx);
        initShader.Dispatch(cellsKernelIdx, cx, cy, 1);

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

    public void Step()
    {
        RunPendingMutatorOperations();

        var cb = new CommandBuffer();
        cb.name = "SimulationPipeline";

        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);

        UpdateTimestamp();

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

    private void ApplySoilChange(CommandBuffer cb = null)
    {
        int cx = Mathf.CeilToInt(SimParams._Width / 8f);
        int cy = Mathf.CeilToInt(SimParams._Height / 8f);

        if (cb != null) {
            cb.DispatchCompute(simulationShader, kernelApplySoilIdx, cx, cy, 1);
        } else {
            simulationShader.Dispatch(kernelApplySoilIdx, cx, cy, 1);
        }
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
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelMutOrganicsIdx, groups, 1, 1);
        organicsPending.Clear();
        ApplySoilChange();
    }

    public void RunEnergyNow()
    {
        if (kernelMutEnergyIdx < 0) return;
        int count = energyPending.Count;
        if (count == 0) return;
        energyDeltaBuffer.SetData(energyPending);
        mutationShader.SetInt("_EnergyCount", count);
        int groups = Mathf.CeilToInt((float)count / 64f);
        mutationShader.Dispatch(kernelMutEnergyIdx, groups, 1, 1);
        energyPending.Clear();
        ApplySoilChange();
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
            case 0:
                cb.DispatchCompute(simulationShader, kernelEnergyIdx, cx, cy, 1);
                ApplySoilChange(cb);
                break;
            case 1:
                cb.DispatchCompute(simulationShader, kernelLeafIdx, cx, cy, 1);
                break;
            case 2:
                cb.DispatchCompute(simulationShader, kernelRootIdx, cx, cy, 1);
                ApplySoilChange(cb);
                break;
            case 3:
                cb.DispatchCompute(simulationShader, kernelAntennaIdx, cx, cy, 1);
                ApplySoilChange(cb);
                break;
            case 4:
                cb.DispatchCompute(simulationShader, kernelRerouteIdx, cx, cy, 1);
                break;
            case 5:
                cb.DispatchCompute(simulationShader, kernelTransportIdx, cx, cy, 1);
                break;
            case 6:
                cb.DispatchCompute(simulationShader, kernelAbsorbIdx, cx, cy, 1);
                break;
            case 7:
                cb.DispatchCompute(simulationShader, kernelDeathIdx, cx, cy, 1);
                ApplySoilChange(cb);
                break;
            case 8:
                cb.DispatchCompute(simulationShader, kernelSeedIdx, cx, cy, 1);
                ApplySoilChange(cb);
                break;
            case 9:
                cb.DispatchCompute(behaviorShader, kernelDecisionIdx, cx, cy, 1);
                break;
            case 10:
                for (int i = 0; i < kernelCmdIdx.Length; ++i)
                {
                    cb.DispatchCompute(behaviorShader, kernelCmdIdx[i], cx, cy, 1);
                }
                break;
            case 11:
                stats = new uint[4];
                statsBuffer.SetData(stats);
                cb.DispatchCompute(simulationShader, kernelStatsIdx, 1, 1, 1);
                cb.RequestAsyncReadback(statsBuffer, request =>
                {
                    if (request.hasError) {
                        Debug.LogError("Ошибка чтения с GPU");
                        return;
                    }

                    var data = request.GetData<uint>();
                    stats = data.ToArray();
                });
                break;
        }
    }
}
