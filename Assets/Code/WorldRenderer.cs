using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class WorldRenderer : MonoBehaviour, IMaterialModifier
{
    public WorldSimulation world;
    public Material soilMaterial;
    public Material energyMaterial;
    public Material worldMaterial;

    public enum RenderMode { CellsFull, CellsEnergy, SoilOrganics, SoilEnergy };

    RenderMode _renderMode = RenderMode.CellsFull;
    RenderTexture _RT;
    RawImage _imageRenderer;

    void Start()
    {
        if (world == null) world = FindAnyObjectByType<WorldSimulation>();
        // CreateQuadMesh();
        _RT = new RenderTexture(world.SimParams._Width, world.SimParams._Height, 0, RenderTextureFormat.ARGBFloat)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        // _RT.enableRandomWrite = true;
        _imageRenderer = GetComponent<RawImage>();
        _imageRenderer.texture = _RT;
    }

    void Update()
    {
        // if (soilRT)
        // {
        //     heatmapMaterial.SetTexture("_SoilTex", world.SoilRTSource);
        //     Graphics.Blit(null, soilRT, heatmapMaterial);
        // }

        // if (cellRT)
        // {
        //     cellMaterial.SetBuffer("_Cells", world.CellsBuffer);
        //     Graphics.Blit(null, cellRT, cellMaterial);
        // }

        if (_RT)
        {
            var blitMat = _renderMode switch
            {
                RenderMode.CellsFull => worldMaterial,
                RenderMode.CellsEnergy => energyMaterial,
                RenderMode.SoilOrganics => soilMaterial,
                RenderMode.SoilEnergy => soilMaterial,
                _ => throw new System.NotImplementedException(),
            };
            blitMat.SetTexture("_SoilTex", world.SoilTexture);
            blitMat.SetBuffer("_Cells", world.CellsBuffer);
            blitMat.SetFloat("_Blend", _renderMode == RenderMode.SoilEnergy ? 1f : 0f);
            blitMat.SetInteger("_Width", world.SimParams._Width);
            blitMat.SetInteger("_Height", world.SimParams._Height);
            blitMat.SetFloat("_OrgThreshold", world.SimParams._CriticalOrg);
            blitMat.SetFloat("_NrgThreshold", world.SimParams._CriticalNrg);
            blitMat.SetFloat("_Threshold", _renderMode == RenderMode.SoilEnergy ? world.SimParams._CriticalNrg : world.SimParams._CriticalOrg);

            Graphics.Blit(null, _RT, blitMat);
        }
    }

    public void SetRenderMode(RenderMode mode) {
        var prevMode = _renderMode;
        _renderMode = mode;
        if (prevMode != _renderMode)
            _imageRenderer.SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        baseMaterial.SetFloat("_RenderIndividualCells", _renderMode == RenderMode.CellsFull || _renderMode == RenderMode.CellsEnergy ? 1f : 0f);
        baseMaterial.SetFloat("_RenderFlowAsCell", _renderMode == RenderMode.CellsEnergy ? 1f : 0f);

        return baseMaterial;
    }
}
