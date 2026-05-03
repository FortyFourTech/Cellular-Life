using UnityEngine;
using UnityEngine.Rendering;

public class WorldSimulation : MonoBehaviour
{
    public ComputeShader initShader;
    public ComputeShader simulationShader;
    public ComputeShader mutationShader;
    public int width = 128;
    public int height = 128;
    public int genomeCapacity = 1024;

    // public bool isPaused = true;

    RenderTexture soilRT0;
    RenderTexture soilRT1;
    bool ping = false;
    bool isStarted = false;

    ComputeBuffer cellsBuffer;
    // ComputeBuffer cellsBuffer1;
    ComputeBuffer genomesBuffer;
    ComputeBuffer statsBuffer;

    int kernelEnergyIdx;
    int kernelLeafIdx;
    int kernelRootIdx;
    int kernelAntennaIdx;
    int kernelRerouteIdx;
    int kernelTransportIdx;
    int kernelAbsorbIdx;
    int kernelDeathIdx;
    int kernelBehaviorIdx;
    int kernelStatsIdx;
    int kernelMutOrganicsIdx = -1;
    int kernelMutEnergyIdx = -1;
    int kernelMutKillIdx = -1;
    int kernelSetCellIdx = -1;

    uint[] stats;

    // mutator buffers and pending lists
    ComputeBuffer organicsDeltaBuffer;
    ComputeBuffer energyDeltaBuffer;
    ComputeBuffer killBuffer;
    ComputeBuffer setCellBuffer;

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
        kernelBehaviorIdx = simulationShader.FindKernel("BehaviorKernel");
        kernelStatsIdx = simulationShader.FindKernel("StatsKernel");

        // rendertextures
        soilRT0 = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat);
        soilRT0.enableRandomWrite = true;
        soilRT0.Create();
        soilRT1 = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat);
        soilRT1.enableRandomWrite = true;
        soilRT1.Create();

        // buffers
        cellsBuffer = new ComputeBuffer(width*height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(CellData)));
        genomesBuffer = new ComputeBuffer(Mathf.Max(1, genomeCapacity), System.Runtime.InteropServices.Marshal.SizeOf(typeof(GenomeData)));
        statsBuffer = new ComputeBuffer(4, sizeof(uint));

        // zero stats
        stats = new uint[4];
        statsBuffer.SetData(stats);

        // set constants
        simulationShader.SetInt("_Width", width);
        simulationShader.SetInt("_Height", height);
        simulationShader.SetFloat("_Sunlight", 1.0f);
        simulationShader.SetFloat("_DiffusionRate", 0.2f);
        simulationShader.SetFloat("_CriticalOrg", 1.0f);
        simulationShader.SetFloat("_CriticalNrg", 1.0f);
        simulationShader.SetFloat("_Timestep", 0.1f);
        // genome capacity uniform for shaders
        simulationShader.SetInt("_GenomeCapacity", genomeCapacity);

        // bind textures and buffers for kernels that will use them
        // simulationShader.SetTexture(kernelEnergyIdx, "_SoilTexRead", soilRT0);
        // simulationShader.SetTexture(kernelEnergyIdx, "_SoilTexWrite", soilRT1);

        // simulationShader.SetTexture(kernelDeathIdx, "_SoilTexRead", soilRT0);
        // simulationShader.SetTexture(kernelDeathIdx, "_SoilTexWrite", soilRT1);

        // assign soil textures to other relevant kernels
        // simulationShader.SetTexture(kernelLeafIdx, "_SoilTexRead", soilRT0);
        // simulationShader.SetTexture(kernelRootIdx, "_SoilTexRead", soilRT0);
        // simulationShader.SetTexture(kernelAntennaIdx, "_SoilTexRead", soilRT0);
        // simulationShader.SetTexture(kernelAbsorbIdx, "_SoilTexRead", soilRT0);

        simulationShader.SetBuffer(kernelLeafIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelRootIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelAntennaIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelRerouteIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelTransportIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelAbsorbIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelDeathIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelBehaviorIdx, "_Cells", cellsBuffer);
        simulationShader.SetBuffer(kernelStatsIdx, "_Cells", cellsBuffer);

        simulationShader.SetBuffer(kernelDeathIdx, "_Genomes", genomesBuffer);
        simulationShader.SetBuffer(kernelBehaviorIdx, "_Genomes", genomesBuffer);

        simulationShader.SetBuffer(kernelAbsorbIdx, "_Stats", statsBuffer);
        simulationShader.SetBuffer(kernelLeafIdx, "_Stats", statsBuffer);
        simulationShader.SetBuffer(kernelStatsIdx, "_Stats", statsBuffer);

        // create mutator buffers (capacity = width*height)
        int maxDeltas = width * height;
        organicsDeltaBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        energyDeltaBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(ResourceDelta)));
        killBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(KillCoord)));
        setCellBuffer = new ComputeBuffer(maxDeltas, System.Runtime.InteropServices.Marshal.SizeOf(typeof(SetCell)));

        mutationShader.SetInt("_Width", width);
        mutationShader.SetInt("_Height", height);
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
            mutationShader.SetBuffer(kernelMutKillIdx, "_Cells", cellsBuffer);
        } catch { kernelMutKillIdx = -1; }
        try {
            kernelSetCellIdx = mutationShader.FindKernel("SetCellsKernel");
            mutationShader.SetBuffer(kernelSetCellIdx, "_SetList", setCellBuffer);
            mutationShader.SetBuffer(kernelSetCellIdx, "_Cells", cellsBuffer);
        } catch { kernelSetCellIdx = -1; }


        // initialize soil as zeros (MVP)
        Graphics.SetRenderTarget(soilRT0);
        GL.Clear(false, true, Color.black);
        Graphics.SetRenderTarget(soilRT1);
        GL.Clear(false, true, Color.black);
    }

    public void PopulateWorld()
    {
        initShader.SetInt("_Width", width);
        initShader.SetInt("_Height", height);
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);

        // init starting cells
        int cellsKernelIdx = initShader.FindKernel("InitCells");
        initShader.SetInt("_CellRowCount", cx);
        initShader.SetFloat("_Timestamp", Time.time);
        initShader.SetFloat("_Rand", Random.value);
        initShader.SetBuffer(cellsKernelIdx, "_Cells", cellsBuffer);
        initShader.SetBuffer(cellsKernelIdx, "_Genomes", genomesBuffer);
        initShader.Dispatch(cellsKernelIdx, cx, cy, 1);

        isStarted = true;

        // get data
        AsyncGPUReadback.Request(statsBuffer, (AsyncGPUReadbackRequest request) =>
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

        AsyncGPUReadback.Request(cellsBuffer, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<CellData>();

            // Debug.Log(Data[63].value);
            // submit updated date
            CellsData = data.ToArray();
        });

        AsyncGPUReadback.Request(genomesBuffer, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<GenomeData>();

            // Debug.Log(Data[63].value);
            // submit updated date
            GenomesData = data.ToArray();
        });
    }

    public void InitWorld()
    {
        initShader.SetInt("_Width", width);
        initShader.SetInt("_Height", height);
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);

        // init starting organics
        int orgKernelIdx = initShader.FindKernel("InitOrganicsKernel");
        initShader.SetTexture(orgKernelIdx, "_SoilTexRead", SoilRTSource);
        initShader.SetTexture(orgKernelIdx, "_SoilTexWrite", SoilRTTarget);
        initShader.SetFloat("_StartOrganic", 0.5f);
        initShader.Dispatch(orgKernelIdx, cx, cy, 1);
        ReversePing();

        // init starting energy
        int nrgKernelIdx = initShader.FindKernel("InitEnergyKernel");
        initShader.SetTexture(nrgKernelIdx, "_SoilTexRead", SoilRTSource);
        initShader.SetTexture(nrgKernelIdx, "_SoilTexWrite", SoilRTTarget);
        initShader.SetFloat("_MeanEnergy", 0.5f);
        initShader.Dispatch(nrgKernelIdx, cx, cy, 1);
        ReversePing();

        // init starting cells
        int cellsKernelIdx = initShader.FindKernel("CleanCells");
        initShader.SetBuffer(cellsKernelIdx, "_Cells", cellsBuffer);
        initShader.SetBuffer(cellsKernelIdx, "_Genomes", genomesBuffer);
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

        // soil kernels
        int tx = Mathf.CeilToInt(width / 8f);
        int ty = Mathf.CeilToInt(height / 8f);
        simulationShader.SetTexture(kernelEnergyIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelEnergyIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.Dispatch(kernelEnergyIdx, tx, ty, 1);

        // cell kernels
        int cx = Mathf.CeilToInt(width / 32f);
        int cy = Mathf.CeilToInt(height / 32f);

        // Leafs
        simulationShader.SetTexture(kernelLeafIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelLeafIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.Dispatch(kernelLeafIdx, cx, cy, 1);

        // Roots
        simulationShader.SetTexture(kernelRootIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelRootIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.Dispatch(kernelRootIdx, cx, cy, 1);

        // Antennas
        simulationShader.SetTexture(kernelAntennaIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelAntennaIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.Dispatch(kernelAntennaIdx, cx, cy, 1);

        // Transport
        simulationShader.Dispatch(kernelRerouteIdx, cx, cy, 1);
        simulationShader.Dispatch(kernelTransportIdx, cx, cy, 1);

        // Absorption
        simulationShader.Dispatch(kernelAbsorbIdx, cx, cy, 1);

        // Death - use soil write target opposite of current
        simulationShader.SetTexture(kernelDeathIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelDeathIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.Dispatch(kernelDeathIdx, cx, cy, 1);

        // Behavior
        simulationShader.SetTexture(kernelBehaviorIdx, "_SoilTexRead", SoilRTSource);
        simulationShader.SetTexture(kernelBehaviorIdx, "_SoilTexWrite", SoilRTTarget);
        simulationShader.SetFloat("_Rand", Random.value);
        simulationShader.Dispatch(kernelBehaviorIdx, cx, threadGroupsY: cy, 1);

        // Stats
        stats = new uint[4];
        statsBuffer.SetData(stats);
        simulationShader.Dispatch(kernelStatsIdx, 1, 1, 1);

        // get data
        AsyncGPUReadback.Request(statsBuffer, (AsyncGPUReadbackRequest request) =>
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

        AsyncGPUReadback.Request(cellsBuffer, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<CellData>();

            // Debug.Log(Data[63].value);
            // submit updated date
            CellsData = data.ToArray();
        });

        AsyncGPUReadback.Request(genomesBuffer, (AsyncGPUReadbackRequest request) =>
        {
            if (request.hasError) {
                Debug.LogError("Ошибка чтения с GPU");
                return;
            }

            var data = request.GetData<GenomeData>();

            // Debug.Log(Data[63].value);
            // submit updated date
            GenomesData = data.ToArray();
        });

        ReversePing();
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
        cellsBuffer?.Release();
        genomesBuffer?.Release();
        statsBuffer?.Release();
        organicsDeltaBuffer?.Release();
        energyDeltaBuffer?.Release();
        killBuffer?.Release();
        if (soilRT0 != null) soilRT0.Release();
        if (soilRT1 != null) soilRT1.Release();
    }

    private void ReversePing()
    {
        Graphics.Blit(SoilRTTarget, SoilRTSource);
        ping = !ping;
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
        ReversePing();
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
        ReversePing();
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
}
