using System.Collections.Generic;
using UnityEngine;

public class WorldRenderer : MonoBehaviour
{
    public WorldSimulation world;
    public Material soilMaterial;
    public RenderTexture soilRT;
    public Material cellMaterial;
    public RenderTexture cellRT;
    public Material worldMaterial;
    public RenderTexture worldRT;

    public enum RenderMode { Full, Energy, Organics };
    public RenderMode renderMode = RenderMode.Full;

    Mesh quadMesh;
    ComputeBuffer readBuffer;

    void Start()
    {
        if (world == null) world = FindAnyObjectByType<WorldSimulation>();
        // CreateQuadMesh();
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

        if (worldRT)
        {
            var blitMat = renderMode switch
            {
                RenderMode.Full => worldMaterial,
                RenderMode.Energy => soilMaterial,
                RenderMode.Organics => soilMaterial,
                _ => throw new System.NotImplementedException(),
            };
            blitMat.SetTexture("_SoilTex", world.SoilRTSource);
            blitMat.SetBuffer("_Cells", world.CellsBuffer);
            blitMat.SetFloat("_Blend", renderMode == RenderMode.Energy ? 1f : 0f);

            Graphics.Blit(null, worldRT, blitMat);
        }
    }

    void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        Vector3[] v = new Vector3[4] { new Vector3(-0.5f,-0.5f,0), new Vector3(0.5f,-0.5f,0), new Vector3(-0.5f,0.5f,0), new Vector3(0.5f,0.5f,0) };
        Vector2[] uv = new Vector2[4] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
        int[] idx = new int[6] { 0,1,2, 2,1,3 };
        quadMesh.vertices = v; quadMesh.uv = uv; quadMesh.triangles = idx; quadMesh.RecalculateBounds();
    }
}
