using System;
using System.IO;
using System.Numerics;
using StbImageSharp;

namespace DX12Lab;

public class TerrainHeightData
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float[,] Heights { get; private set; }

    public float MinWorldX { get; private set; }
    public float MaxWorldX { get; private set; }
    public float MinWorldZ { get; private set; }
    public float MaxWorldZ { get; private set; }
    public float HeightScale { get; private set; }

    public TerrainHeightData(float minWorldX, float maxWorldX, float minWorldZ, float maxWorldZ, float heightScale)
    {
        MinWorldX = minWorldX;
        MaxWorldX = maxWorldX;
        MinWorldZ = minWorldZ;
        MaxWorldZ = maxWorldZ;
        HeightScale = heightScale;
        Heights = new float[0, 0];
    }

    public static TerrainHeightData LoadFromTiles(
        string dir, int tilesX, int tilesY,
        float minX, float maxX, float minZ, float maxZ, float heightScale)
    {
        StbImage.stbi_set_flip_vertically_on_load(0);

        var terrain = new TerrainHeightData(minX, maxX, minZ, maxZ, heightScale);

        byte[][,] tileData = new byte[tilesX * tilesY][,];
        int tileW = 0, tileH = 0;

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                string tilePath = Path.Combine(dir, $"tile_{tx}_{ty}.png");
                if (!File.Exists(tilePath))
                    throw new FileNotFoundException($"Tile not found: {tilePath}");

                using var stream = File.OpenRead(tilePath);
                var img = ImageResult.FromStream(stream, ColorComponents.Grey);
                if (tileW == 0)
                {
                    tileW = img.Width;
                    tileH = img.Height;
                }
                else if (img.Width != tileW || img.Height != tileH)
                {
                    throw new InvalidOperationException($"Tile size mismatch in {tilePath}: {img.Width}x{img.Height} vs expected {tileW}x{tileH}");
                }

                int index = ty * tilesX + tx;
                tileData[index] = new byte[tileH, tileW];
                for (int y = 0; y < tileH; y++)
                    for (int x = 0; x < tileW; x++)
                        tileData[index][y, x] = img.Data[y * tileW + x];
            }
        }

        int stitchedW = tilesX * (tileW - 1) + 1;
        int stitchedH = tilesY * (tileH - 1) + 1;
        terrain.Width = stitchedW;
        terrain.Height = stitchedH;
        terrain.Heights = new float[stitchedH, stitchedW];

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                int index = ty * tilesX + tx;
                var t = tileData[index];
                int startX = tx * (tileW - 1);
                int startY = ty * (tileH - 1);

                for (int y = 0; y < tileH; y++)
                {
                    for (int x = 0; x < tileW; x++)
                    {
                        float h = (t[y, x] / 255.0f) * heightScale;
                        terrain.Heights[startY + y, startX + x] = h;
                    }
                }
            }
        }

        for (int ty = 0; ty < tilesY; ty++)
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                if (tx + 1 < tilesX)
                {
                    var leftTile = tileData[ty * tilesX + tx];
                    var rightTile = tileData[ty * tilesX + (tx + 1)];
                    for (int y = 0; y < tileH; y++)
                    {
                        if (leftTile[y, tileW - 1] != rightTile[y, 0])
                            throw new Exception($"Border seam mismatch at X between tile ({tx},{ty}) and ({tx+1},{ty}) at row {y}");
                    }
                }
                if (ty + 1 < tilesY)
                {
                    var topTile = tileData[ty * tilesX + tx];
                    var bottomTile = tileData[(ty + 1) * tilesX + tx];
                    for (int x = 0; x < tileW; x++)
                    {
                        if (topTile[tileH - 1, x] != bottomTile[0, x])
                            throw new Exception($"Border seam mismatch at Y between tile ({tx},{ty}) and ({tx},{ty+1}) at col {x}");
                    }
                }
            }
        }

        return terrain;
    }

    public static TerrainHeightData LoadFromFile(
        string path, float minX, float maxX, float minZ, float maxZ, float heightScale)
    {
        StbImage.stbi_set_flip_vertically_on_load(0);

        var terrain = new TerrainHeightData(minX, maxX, minZ, maxZ, heightScale);

        if (path.EndsWith(".raw", StringComparison.OrdinalIgnoreCase))
        {
            byte[] bytes = File.ReadAllBytes(path);
            int sampleCount = bytes.Length / 2;
            int side = (int)Math.Round(Math.Sqrt(sampleCount));
            if (side * side != sampleCount)
                throw new InvalidOperationException("RAW heightmap must be square 16-bit image");

            terrain.Width = side;
            terrain.Height = side;
            terrain.Heights = new float[side, side];

            int offset = 0;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    ushort val = BitConverter.ToUInt16(bytes, offset);
                    offset += 2;
                    terrain.Heights[y, x] = (val / 65535.0f) * heightScale;
                }
            }
        }
        else
        {
            using var stream = File.OpenRead(path);
            var img = ImageResult.FromStream(stream, ColorComponents.Grey);
            terrain.Width = img.Width;
            terrain.Height = img.Height;
            terrain.Heights = new float[img.Height, img.Width];

            for (int y = 0; y < img.Height; y++)
            {
                for (int x = 0; x < img.Width; x++)
                {
                    terrain.Heights[y, x] = (img.Data[y * img.Width + x] / 255.0f) * heightScale;
                }
            }
        }

        return terrain;
    }

    public float GetHeight(float worldX, float worldZ)
    {
        float tx = (worldX - MinWorldX) / (MaxWorldX - MinWorldX);
        float tz = (worldZ - MinWorldZ) / (MaxWorldZ - MinWorldZ);

        tx = Math.Clamp(tx, 0.0f, 1.0f);
        tz = Math.Clamp(tz, 0.0f, 1.0f);

        float gx = tx * (Width - 1);
        float gz = tz * (Height - 1);

        int x0 = Math.Clamp((int)gx, 0, Width - 1);
        int x1 = Math.Clamp(x0 + 1, 0, Width - 1);
        int z0 = Math.Clamp((int)gz, 0, Height - 1);
        int z1 = Math.Clamp(z0 + 1, 0, Height - 1);

        float fx = gx - x0;
        float fz = gz - z0;

        float h00 = Heights[z0, x0];
        float h10 = Heights[z0, x1];
        float h01 = Heights[z1, x0];
        float h11 = Heights[z1, x1];

        float h0 = h00 * (1.0f - fx) + h10 * fx;
        float h1 = h01 * (1.0f - fx) + h11 * fx;

        return h0 * (1.0f - fz) + h1 * fz;
    }

    public Vector3 GetNormal(float worldX, float worldZ)
    {
        float step = 0.5f;
        float hL = GetHeight(worldX - step, worldZ);
        float hR = GetHeight(worldX + step, worldZ);
        float hD = GetHeight(worldX, worldZ - step);
        float hU = GetHeight(worldX, worldZ + step);

        float dhdx = (hR - hL) / (2.0f * step);
        float dhdz = (hU - hD) / (2.0f * step);

        return Vector3.Normalize(new Vector3(-dhdx, 1.0f, -dhdz));
    }

    public void GetHeightRange(float x0, float x1, float z0, float z1, out float minY, out float maxY)
    {
        float tx0 = Math.Clamp((x0 - MinWorldX) / (MaxWorldX - MinWorldX), 0.0f, 1.0f);
        float tx1 = Math.Clamp((x1 - MinWorldX) / (MaxWorldX - MinWorldX), 0.0f, 1.0f);
        float tz0 = Math.Clamp((z0 - MinWorldZ) / (MaxWorldZ - MinWorldZ), 0.0f, 1.0f);
        float tz1 = Math.Clamp((z1 - MinWorldZ) / (MaxWorldZ - MinWorldZ), 0.0f, 1.0f);

        int minGx = Math.Clamp((int)(tx0 * (Width - 1)), 0, Width - 1);
        int maxGx = Math.Clamp((int)Math.Ceiling(tx1 * (Width - 1)), 0, Width - 1);
        int minGz = Math.Clamp((int)(tz0 * (Height - 1)), 0, Height - 1);
        int maxGz = Math.Clamp((int)Math.Ceiling(tz1 * (Height - 1)), 0, Height - 1);

        float minH = float.MaxValue;
        float maxH = float.MinValue;

        for (int z = minGz; z <= maxGz; z++)
        {
            for (int x = minGx; x <= maxGx; x++)
            {
                float h = Heights[z, x];
                if (h < minH) minH = h;
                if (h > maxH) maxH = h;
            }
        }

        minY = minH;
        maxY = maxH;
    }
}
