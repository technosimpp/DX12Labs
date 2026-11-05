using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace DX12Lab;

[StructLayout(LayoutKind.Sequential)]
public struct TerrainVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TexCoord;
}

public class TerrainNode : IDisposable
{
    public int NodeId { get; }
    public int Depth { get; }
    public float MinX { get; }
    public float MaxX { get; }
    public float MinZ { get; }
    public float MaxZ { get; }
    public float MinY { get; private set; }
    public float MaxY { get; private set; }

    public BoundingBox Bounds { get; private set; }
    public Vector3 Center => (Bounds.Min + Bounds.Max) * 0.5f;

    public TerrainNode[]? Children { get; set; }
    public bool IsLeaf => Children == null;

    public ID3D12Resource? VertexBuffer { get; private set; }
    public ID3D12Resource? IndexBuffer { get; private set; }
    public VertexBufferView VertexBufferView { get; private set; }
    public IndexBufferView IndexBufferView { get; private set; }

    public int GridIndexCount { get; private set; }
    public int SkirtIndexCount { get; private set; }
    public int TotalIndexCount => GridIndexCount + SkirtIndexCount;

    public TerrainNode(int id, int depth, float minX, float maxX, float minZ, float maxZ)
    {
        NodeId = id;
        Depth = depth;
        MinX = minX;
        MaxX = maxX;
        MinZ = minZ;
        MaxZ = maxZ;
    }

    public float DistanceTo(Vector3 point)
    {
        float dx = MathF.Max(0.0f, MathF.Max(Bounds.Min.X - point.X, point.X - Bounds.Max.X));
        float dy = MathF.Max(0.0f, MathF.Max(Bounds.Min.Y - point.Y, point.Y - Bounds.Max.Y));
        float dz = MathF.Max(0.0f, MathF.Max(Bounds.Min.Z - point.Z, point.Z - Bounds.Max.Z));
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public void BuildGeometry(ID3D12Device device, TerrainHeightData heightData, TerrainConfig config)
    {
        int M = config.PatchGridSize;
        heightData.GetHeightRange(MinX, MaxX, MinZ, MaxZ, out float minY, out float maxY);
        MinY = minY;
        MaxY = maxY;

        Bounds = new BoundingBox(
            new Vector3(MinX, MinY - config.SkirtDepth, MinZ),
            new Vector3(MaxX, MaxY, MaxZ));

        var vertices = new List<TerrainVertex>(M * M + 4 * M);
        float width = MaxX - MinX;
        float height = MaxZ - MinZ;

        for (int z = 0; z < M; z++)
        {
            float fz = (float)z / (M - 1);
            float worldZ = MinZ + fz * height;

            for (int x = 0; x < M; x++)
            {
                float fx = (float)x / (M - 1);
                float worldX = MinX + fx * width;

                float worldY = heightData.GetHeight(worldX, worldZ);
                var normal = heightData.GetNormal(worldX, worldZ);

                float u = (worldX - config.MinX) / config.TerrainSize;
                float v = (worldZ - config.MinZ) / config.TerrainSize;

                vertices.Add(new TerrainVertex
                {
                    Position = new Vector3(worldX, worldY, worldZ),
                    Normal = normal,
                    TexCoord = new Vector2(u, v)
                });
            }
        }

        var indices = new List<uint>((M - 1) * (M - 1) * 6 + 4 * (M - 1) * 6);
        for (int z = 0; z < M - 1; z++)
        {
            for (int x = 0; x < M - 1; x++)
            {
                uint v00 = (uint)(z * M + x);
                uint v10 = (uint)(z * M + (x + 1));
                uint v01 = (uint)((z + 1) * M + x);
                uint v11 = (uint)((z + 1) * M + (x + 1));

                indices.Add(v00);
                indices.Add(v10);
                indices.Add(v01);

                indices.Add(v10);
                indices.Add(v11);
                indices.Add(v01);
            }
        }
        GridIndexCount = indices.Count;

        float skirtDepth = config.SkirtDepth;

        void AddSkirtEdge(int[] edgeIndices)
        {
            int edgeLen = edgeIndices.Length;
            int skirtStart = vertices.Count;

            for (int i = 0; i < edgeLen; i++)
            {
                var topVert = vertices[edgeIndices[i]];
                var skirtVert = topVert;
                skirtVert.Position.Y -= skirtDepth;
                vertices.Add(skirtVert);
            }

            for (int i = 0; i < edgeLen - 1; i++)
            {
                uint top0 = (uint)edgeIndices[i];
                uint top1 = (uint)edgeIndices[i + 1];
                uint bot0 = (uint)(skirtStart + i);
                uint bot1 = (uint)(skirtStart + i + 1);

                indices.Add(top0);
                indices.Add(bot0);
                indices.Add(top1);

                indices.Add(top1);
                indices.Add(bot0);
                indices.Add(bot1);
            }
        }

        int[] southEdge = new int[M];
        for (int x = 0; x < M; x++) southEdge[x] = x;
        AddSkirtEdge(southEdge);

        int[] northEdge = new int[M];
        for (int x = 0; x < M; x++) northEdge[x] = (M - 1) * M + (M - 1 - x);
        AddSkirtEdge(northEdge);

        int[] westEdge = new int[M];
        for (int z = 0; z < M; z++) westEdge[z] = (M - 1 - z) * M;
        AddSkirtEdge(westEdge);

        int[] eastEdge = new int[M];
        for (int z = 0; z < M; z++) eastEdge[z] = z * M + (M - 1);
        AddSkirtEdge(eastEdge);

        SkirtIndexCount = indices.Count - GridIndexCount;

        var vertArray = vertices.ToArray();
        int vbSize = Marshal.SizeOf<TerrainVertex>() * vertArray.Length;
        VertexBuffer = device.CreateCommittedResource(
            new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)vbSize), ResourceStates.GenericRead);

        unsafe
        {
            void* ptr = null;
            VertexBuffer.Map(0, null, &ptr);
            fixed (TerrainVertex* src = vertArray)
                Buffer.MemoryCopy(src, ptr, vbSize, vbSize);
            VertexBuffer.Unmap(0, null);
        }
        VertexBufferView = new VertexBufferView(
            VertexBuffer.GPUVirtualAddress, (uint)vbSize, (uint)Marshal.SizeOf<TerrainVertex>());

        var idxArray = indices.ToArray();
        int ibSize = sizeof(uint) * idxArray.Length;
        IndexBuffer = device.CreateCommittedResource(
            new HeapProperties(HeapType.Upload), HeapFlags.None,
            ResourceDescription.Buffer((ulong)ibSize), ResourceStates.GenericRead);

        unsafe
        {
            void* ptr = null;
            IndexBuffer.Map(0, null, &ptr);
            fixed (uint* src = idxArray)
                Buffer.MemoryCopy(src, ptr, ibSize, ibSize);
            IndexBuffer.Unmap(0, null);
        }
        IndexBufferView = new IndexBufferView(
            IndexBuffer.GPUVirtualAddress, (uint)ibSize, Format.R32_UInt);
    }

    public void Dispose()
    {
        VertexBuffer?.Dispose();
        IndexBuffer?.Dispose();
        VertexBuffer = null;
        IndexBuffer = null;

        if (Children != null)
        {
            foreach (var child in Children)
                child.Dispose();
        }
    }
}
