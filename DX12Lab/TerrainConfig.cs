using System;

namespace DX12Lab;

public class TerrainConfig
{
    public float TerrainSize { get; set; } = 512.0f;
    public float HeightScale { get; set; } = 80.0f;

    public float MinX => -TerrainSize * 0.5f;
    public float MaxX => TerrainSize * 0.5f;
    public float MinZ => -TerrainSize * 0.5f;
    public float MaxZ => TerrainSize * 0.5f;

    public int MaxDepth { get; set; } = 3;
    public int PatchGridSize { get; set; } = 33;
    public float SkirtDepth { get; set; } = 8.0f;
    public float[] LODDistances { get; set; } = new float[] { 340.0f, 180.0f, 90.0f, 0.0f };
}
