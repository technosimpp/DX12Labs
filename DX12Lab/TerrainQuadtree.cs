using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Direct3D12;

namespace DX12Lab;

public class TerrainQuadtree : IDisposable
{
    public TerrainConfig Config { get; }
    public TerrainHeightData HeightData { get; }
    public TerrainNode Root { get; }

    public List<TerrainNode> RenderList { get; } = new();

    public int TotalNodesCount { get; private set; }
    public int CulledNodesCount { get; private set; }
    public int RenderedNodesCount { get; private set; }
    public int RenderedTriangleCount { get; private set; }
    public int[] LODNodeCounts { get; }

    public bool SkirtsEnabled { get; set; } = true;
    public float LODDistanceScale { get; set; } = 1.0f;

    private int _nextNodeId = 0;

    public TerrainQuadtree(ID3D12Device device, TerrainHeightData heightData, TerrainConfig config)
    {
        Config = config;
        HeightData = heightData;
        LODNodeCounts = new int[config.MaxDepth + 1];

        Root = BuildNode(device, 0, config.MinX, config.MaxX, config.MinZ, config.MaxZ);
    }

    private TerrainNode BuildNode(
        ID3D12Device device, int depth,
        float minX, float maxX, float minZ, float maxZ)
    {
        var node = new TerrainNode(_nextNodeId++, depth, minX, maxX, minZ, maxZ);
        node.BuildGeometry(device, HeightData, Config);
        TotalNodesCount++;

        if (depth < Config.MaxDepth)
        {
            float midX = (minX + maxX) * 0.5f;
            float midZ = (minZ + maxZ) * 0.5f;

            node.Children = new TerrainNode[4]
            {
                BuildNode(device, depth + 1, minX, midX, minZ, midZ),
                BuildNode(device, depth + 1, midX, maxX, minZ, midZ),
                BuildNode(device, depth + 1, minX, midX, midZ, maxZ),
                BuildNode(device, depth + 1, midX, maxX, midZ, maxZ),
            };
        }

        return node;
    }

    public void Update(Vector3 cameraPos, Frustum frustum, bool frustumCullingEnabled)
    {
        RenderList.Clear();
        CulledNodesCount = 0;
        RenderedNodesCount = 0;
        RenderedTriangleCount = 0;
        Array.Clear(LODNodeCounts, 0, LODNodeCounts.Length);

        Traverse(Root, cameraPos, frustum, frustumCullingEnabled);
    }

    private void Traverse(TerrainNode node, Vector3 cameraPos, Frustum frustum, bool cullingEnabled)
    {
        if (cullingEnabled && !frustum.Intersects(node.Bounds))
        {
            CulledNodesCount++;
            return;
        }

        bool shouldSubdivide = false;
        if (!node.IsLeaf && node.Children != null)
        {
            float dist = node.DistanceTo(cameraPos);
            float threshold = Config.LODDistances[node.Depth] * LODDistanceScale;

            if (dist < threshold)
                shouldSubdivide = true;
        }

        if (shouldSubdivide)
        {
            foreach (var child in node.Children!)
                Traverse(child, cameraPos, frustum, cullingEnabled);
        }
        else
        {
            RenderList.Add(node);
            RenderedNodesCount++;
            int indexCount = SkirtsEnabled ? node.TotalIndexCount : node.GridIndexCount;
            RenderedTriangleCount += indexCount / 3;
            LODNodeCounts[node.Depth]++;
        }
    }

    public void Dispose()
    {
        Root?.Dispose();
    }
}
