using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class Chunk : IDisposable
{
    public const int SizeX = 16;
    public const int SizeY = 128;
    public const int SizeZ = 16;

    public Vector2i Position { get; }
    private readonly BlockType[,,] _blocks = new BlockType[SizeX, SizeY, SizeZ];

    public Renderer.ChunkMesh? OpaqueMesh { get; private set; }
    public Renderer.ChunkMesh? LeafMesh { get; private set; }
    public Renderer.ChunkMesh? WaterMesh { get; private set; }
    public bool IsDirty { get; private set; } = true;
    public bool IsUploaded { get; private set; } = false; 

    public Chunk(Vector2i position) => Position = position;

    public void SetDirty() => IsDirty = true;

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return BlockType.Air;
        return _blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        IsDirty = true;
    }

    public void SetBlockDirect(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
    }

    private float[] _opaqueVerts = Array.Empty<float>();
    private uint[] _opaqueInds = Array.Empty<uint>();
    private float[] _leafVerts = Array.Empty<float>();
    private uint[] _leafInds = Array.Empty<uint>();
    private float[] _waterVerts = Array.Empty<float>();
    private uint[] _waterInds = Array.Empty<uint>();

    public void RebuildMeshData(World world)
    {
        var opaqueVerts = new List<float>(); var opaqueInds = new List<uint>();
        var leafVerts = new List<float>(); var leafInds = new List<uint>();
        var waterVerts = new List<float>(); var waterInds = new List<uint>();

        GreedyMesh(world, opaqueVerts, opaqueInds, 0);
        GreedyMesh(world, leafVerts, leafInds, 1);
        GreedyMesh(world, waterVerts, waterInds, 2);

        _opaqueVerts = opaqueVerts.ToArray(); _opaqueInds = opaqueInds.ToArray();
        _leafVerts = leafVerts.ToArray(); _leafInds = leafInds.ToArray();
        _waterVerts = waterVerts.ToArray(); _waterInds = waterInds.ToArray();
    }

    private bool Hides(BlockType target, BlockType neighbor)
    {
        if (neighbor == BlockType.Air) return false;
        if (neighbor == BlockType.Water) return target == BlockType.Water;
        if (neighbor == BlockType.Leaves) return target == BlockType.Leaves;
        return true;
    }

    private void GreedyMesh(World world, List<float> verts, List<uint> inds, int pass)
    {
        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;
            int[] x = { 0, 0, 0 };
            int[] q = { 0, 0, 0 };
            q[d] = 1;

            int maxD = d == 0 ? SizeX : (d == 1 ? SizeY : SizeZ);
            int maxU = u == 0 ? SizeX : (u == 1 ? SizeY : SizeZ);
            int maxV = v == 0 ? SizeX : (v == 1 ? SizeY : SizeZ);

            int[] mask = new int[maxU * maxV];

            for (x[d] = -1; x[d] < maxD; x[d]++)
            {
                int n = 0;
                for (x[v] = 0; x[v] < maxV; x[v]++)
                {
                    for (x[u] = 0; x[u] < maxU; x[u]++)
                    {
                        BlockType a = (x[d] >= 0) ? GetBlock(x[0], x[1], x[2]) : world.GetBlock(Position.X * SizeX + x[0], x[1], Position.Y * SizeZ + x[2]);
                        BlockType b = (x[d] < maxD - 1) ? GetBlock(x[0] + q[0], x[1] + q[1], x[2] + q[2]) : world.GetBlock(Position.X * SizeX + x[0] + q[0], x[1] + q[1], Position.Y * SizeZ + x[2] + q[2]);

                        bool aMatch = pass == 0 ? BlockData.IsOpaque(a) : pass == 1 ? a == BlockType.Leaves : a == BlockType.Water;
                        bool bMatch = pass == 0 ? BlockData.IsOpaque(b) : pass == 1 ? b == BlockType.Leaves : b == BlockType.Water;

                        if (a != BlockType.Air && aMatch && !Hides(a, b))
                            mask[n++] = (int)a * 2 + 1;
                        else if (b != BlockType.Air && bMatch && !Hides(b, a))
                            mask[n++] = (int)b * 2 + 2;
                        else
                            mask[n++] = 0;
                    }
                }

                n = 0;
                for (int j = 0; j < maxV; j++)
                {
                    for (int i = 0; i < maxU; )
                    {
                        int c = mask[n];
                        if (c == 0) { i++; n++; continue; }

                        int w = 1;
                        while (i + w < maxU && mask[n + w] == c) w++;

                        int h = 1;
                        bool done = false;
                        while (j + h < maxV)
                        {
                            for (int k = 0; k < w; k++)
                            {
                                if (mask[n + k + h * maxU] != c) { done = true; break; }
                            }
                            if (done) break;
                            h++;
                        }

                        x[u] = i;
                        x[v] = j;
                        int[] du = { 0, 0, 0 }; int[] dv = { 0, 0, 0 };
                        du[u] = w; dv[v] = h;

                        BlockType block = (BlockType)((c - 1) / 2);
                        int face = (c - 1) % 2;

                        Vector3 normal = new Vector3(q[0], q[1], q[2]) * (face == 0 ? 1 : -1);
                        Vector2 tileCoords = BlockData.GetTileCoords(block, d, face);

                        Vector3 basePos = new Vector3(x[0], x[1], x[2]) + new Vector3(Position.X * SizeX, 0, Position.Y * SizeZ);

                        // ИСПРАВЛЕНИЕ: Плоскость всегда находится на x + q
                        Vector3 p0 = basePos + new Vector3(q[0], q[1], q[2]);
                        Vector3 p1 = p0 + new Vector3(du[0], du[1], du[2]);
                        Vector3 p2 = p0 + new Vector3(du[0] + dv[0], du[1] + dv[1], du[2] + dv[2]);
                        Vector3 p3 = p0 + new Vector3(dv[0], dv[1], dv[2]);

                        float eps = 0.001f;
                        Vector2 uv0, uv1, uv2, uv3;
                        
                        if (d == 1) 
                        {
                            uv0 = new Vector2(0, 0); uv1 = new Vector2(w - eps, 0); uv2 = new Vector2(w - eps, h - eps); uv3 = new Vector2(0, h - eps);
                        }
                        else if (d == 0) 
                        {
                            uv0 = new Vector2(0, 0); uv1 = new Vector2(0, w - eps); uv2 = new Vector2(h - eps, w - eps); uv3 = new Vector2(h - eps, 0);
                        }
                        else 
                        {
                            uv0 = new Vector2(0, 0); uv1 = new Vector2(w - eps, 0); uv2 = new Vector2(w - eps, h - eps); uv3 = new Vector2(0, h - eps);
                        }

                        AddQuad(verts, inds, p0, p1, p2, p3, uv0, uv1, uv2, uv3, normal, tileCoords);

                        for (int l = 0; l < h; l++)
                            for (int k = 0; k < w; k++)
                                mask[n + k + l * maxU] = 0;

                        i += w; n += w;
                    }
                }
            }
        }
    }

    private void AddQuad(List<float> verts, List<uint> inds,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector3 normal, Vector2 tileCoords)
    {
        uint start = (uint)(verts.Count / 10);
        
        Vector3 edge1 = p1 - p0;
        Vector3 edge2 = p2 - p0;
        Vector3 faceNormal = Vector3.Cross(edge1, edge2);

        if (Vector3.Dot(faceNormal, normal) < 0)
        {
            AddVertex(verts, p0, uv0, normal, tileCoords); AddVertex(verts, p3, uv3, normal, tileCoords);
            AddVertex(verts, p2, uv2, normal, tileCoords); AddVertex(verts, p1, uv1, normal, tileCoords);
            inds.AddRange(new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        else
        {
            AddVertex(verts, p0, uv0, normal, tileCoords); AddVertex(verts, p1, uv1, normal, tileCoords);
            AddVertex(verts, p2, uv2, normal, tileCoords); AddVertex(verts, p3, uv3, normal, tileCoords);
            inds.AddRange(new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }

    private static void AddVertex(List<float> v, Vector3 pos, Vector2 uv, Vector3 normal, Vector2 tileCoords)
    {
        v.Add(pos.X); v.Add(pos.Y); v.Add(pos.Z);
        v.Add(uv.X); v.Add(uv.Y);
        v.Add(normal.X); v.Add(normal.Y); v.Add(normal.Z);
        v.Add(tileCoords.X); v.Add(tileCoords.Y);
    }

    public void UploadMesh()
    {
        OpaqueMesh?.Dispose();
        OpaqueMesh = _opaqueVerts.Length > 0 ? new Renderer.ChunkMesh(_opaqueVerts, _opaqueInds) : null;

        LeafMesh?.Dispose();
        LeafMesh = _leafVerts.Length > 0 ? new Renderer.ChunkMesh(_leafVerts, _leafInds) : null;

        WaterMesh?.Dispose();
        WaterMesh = _waterVerts.Length > 0 ? new Renderer.ChunkMesh(_waterVerts, _waterInds) : null;

        IsDirty = false;
        IsUploaded = true; 
    }

    public void Dispose()
    {
        OpaqueMesh?.Dispose();
        LeafMesh?.Dispose();
        WaterMesh?.Dispose();
        OpaqueMesh = null;
        LeafMesh = null;
        WaterMesh = null;
    }
}