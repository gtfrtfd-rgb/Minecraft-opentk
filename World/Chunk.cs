using System;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class Chunk
{
    public const int SizeX = 16;
    public const int SizeY = 128;
    public const int SizeZ = 16;

    public Vector2i Position { get; }
    private readonly BlockType[,,] _blocks = new BlockType[SizeX, SizeY, SizeZ];

    public Renderer.ChunkMesh? OpaqueMesh { get; private set; }
    public Renderer.ChunkMesh? LeafMesh { get; private set; }
    public Renderer.ChunkMesh? WaterMesh { get; private set; }
    public bool IsUploaded { get; private set; } = false;

    private int _isDirty = 1;
    public bool IsDirty => Volatile.Read(ref _isDirty) == 1;

    private ChunkMeshData? _pendingMeshData;

    public sealed class ChunkMeshData
    {
        public float[] OpaqueVerts = Array.Empty<float>();
        public uint[] OpaqueInds = Array.Empty<uint>();
        public float[] LeafVerts = Array.Empty<float>();
        public uint[] LeafInds = Array.Empty<uint>();
        public float[] WaterVerts = Array.Empty<float>();
        public uint[] WaterInds = Array.Empty<uint>();
    }

    public Chunk(Vector2i position) => Position = position;

    public void SetDirty() => Interlocked.Exchange(ref _isDirty, 1);
    public bool TryClearDirty() => Interlocked.Exchange(ref _isDirty, 0) == 1;

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return BlockType.Air;
        return _blocks[x, y, z];
    }

    public BlockType GetBlockNoCheck(int x, int y, int z) => _blocks[x, y, z];

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        Interlocked.Exchange(ref _isDirty, 1);
    }

    public void SetBlockDirect(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
    }

    public void RebuildMeshData(World world)
    {
        var left  = world.GetChunk(Position.X - 1, Position.Y);
        var right = world.GetChunk(Position.X + 1, Position.Y);
        var front = world.GetChunk(Position.X,     Position.Y - 1);
        var back  = world.GetChunk(Position.X,     Position.Y + 1);

        var opaqueVerts = new List<float>(); var opaqueInds = new List<uint>();
        var leafVerts = new List<float>();   var leafInds = new List<uint>();
        var waterVerts = new List<float>();  var waterInds = new List<uint>();

        GreedyMesh(left, right, front, back, opaqueVerts, opaqueInds, 0);
        GreedyMesh(left, right, front, back, leafVerts, leafInds, 1);
        GreedyMesh(left, right, front, back, waterVerts, waterInds, 2);

        var data = new ChunkMeshData
        {
            OpaqueVerts = opaqueVerts.ToArray(),
            OpaqueInds = opaqueInds.ToArray(),
            LeafVerts = leafVerts.ToArray(),
            LeafInds = leafInds.ToArray(),
            WaterVerts = waterVerts.ToArray(),
            WaterInds = waterInds.ToArray()
        };

        Volatile.Write(ref _pendingMeshData, data);
    }

    private bool Hides(BlockType target, BlockType neighbor)
    {
        if (neighbor == BlockType.Air) return false;
        if (neighbor == BlockType.Water) return target == BlockType.Water;
        if (neighbor == BlockType.Leaves) return target == BlockType.Leaves;
        return true;
    }

    private BlockType GetBlockWithNeighbors(int x, int y, int z, Chunk? left, Chunk? right, Chunk? front, Chunk? back)
    {
        if ((uint)y >= (uint)SizeY) return BlockType.Air;
        if ((uint)x < (uint)SizeX && (uint)z < (uint)SizeZ) return _blocks[x, y, z];
        if (x < 0)  return left  != null ? left.GetBlockNoCheck(SizeX - 1, y, z) : BlockType.Air;
        if (x >= SizeX) return right != null ? right.GetBlockNoCheck(0, y, z) : BlockType.Air;
        if (z < 0)  return front != null ? front.GetBlockNoCheck(x, y, SizeZ - 1) : BlockType.Air;
        if (z >= SizeZ) return back != null ? back.GetBlockNoCheck(x, y, 0) : BlockType.Air;
        return BlockType.Air;
    }

    private void GreedyMesh(Chunk? left, Chunk? right, Chunk? front, Chunk? back, List<float> verts, List<uint> inds, int pass)
    {
        Span<int> x = stackalloc int[3];
        Span<int> q = stackalloc int[3];
        Span<int> du = stackalloc int[3];
        Span<int> dv = stackalloc int[3];
        Span<int> mask = stackalloc int[SizeX * SizeY];

        for (int d = 0; d < 3; d++)
        {
            int u = (d + 1) % 3;
            int v = (d + 2) % 3;
            x[0] = x[1] = x[2] = 0;
            q[0] = q[1] = q[2] = 0;
            q[d] = 1;

            int maxD = d == 0 ? SizeX : (d == 1 ? SizeY : SizeZ);
            int maxU = u == 0 ? SizeX : (u == 1 ? SizeY : SizeZ);
            int maxV = v == 0 ? SizeX : (v == 1 ? SizeY : SizeZ);

            Span<int> axisMask = mask.Slice(0, maxU * maxV);
            axisMask.Clear();

            for (x[d] = -1; x[d] < maxD; x[d]++)
            {
                int n = 0;
                for (x[v] = 0; x[v] < maxV; x[v]++)
                {
                    for (x[u] = 0; x[u] < maxU; x[u]++)
                    {
                        BlockType a = GetBlockWithNeighbors(x[0], x[1], x[2], left, right, front, back);
                        BlockType b = GetBlockWithNeighbors(x[0] + q[0], x[1] + q[1], x[2] + q[2], left, right, front, back);

                        bool aMatch = pass == 0 ? BlockData.IsOpaque(a) : pass == 1 ? a == BlockType.Leaves : a == BlockType.Water;
                        bool bMatch = pass == 0 ? BlockData.IsOpaque(b) : pass == 1 ? b == BlockType.Leaves : b == BlockType.Water;

                        if (a != BlockType.Air && aMatch && !Hides(a, b))
                            axisMask[n++] = (int)a * 2 + 1;
                        else if (b != BlockType.Air && bMatch && !Hides(b, a))
                            axisMask[n++] = (int)b * 2 + 2;
                        else
                            axisMask[n++] = 0;
                    }
                }

                n = 0;
                for (int j = 0; j < maxV; j++)
                {
                    for (int i = 0; i < maxU;)
                    {
                        int c = axisMask[n];
                        if (c == 0) { i++; n++; continue; }

                        int w = 1;
                        while (i + w < maxU && axisMask[n + w] == c) w++;

                        int h = 1;
                        bool done = false;
                        while (j + h < maxV)
                        {
                            for (int k = 0; k < w; k++)
                            {
                                if (axisMask[n + k + h * maxU] != c) { done = true; break; }
                            }
                            if (done) break;
                            h++;
                        }

                        x[u] = i;
                        x[v] = j;
                        du[0] = du[1] = du[2] = 0;
                        dv[0] = dv[1] = dv[2] = 0;
                        du[u] = w;
                        dv[v] = h;

                        BlockType block = (BlockType)((c - 1) / 2);
                        int face = (c - 1) % 2;

                        Vector3 normal = new Vector3(q[0], q[1], q[2]) * (face == 0 ? 1 : -1);
                        Vector2 tileCoords = BlockData.GetTileCoords(block, d, face);

                        Vector3 basePos = new Vector3(x[0], x[1], x[2]) + new Vector3(Position.X * SizeX, 0, Position.Y * SizeZ);

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
                                axisMask[n + k + l * maxU] = 0;

                        i += w; n += w;
                    }
                }
            }
        }
    }

    private static void AddQuad(List<float> verts, List<uint> inds,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector3 normal, Vector2 tileCoords)
    {
        uint start = (uint)(verts.Count / 10);
        Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);

        if (Vector3.Dot(faceNormal, normal) < 0)
        {
            AddVertex(verts, p0, uv0, normal, tileCoords);
            AddVertex(verts, p3, uv3, normal, tileCoords);
            AddVertex(verts, p2, uv2, normal, tileCoords);
            AddVertex(verts, p1, uv1, normal, tileCoords);
        }
        else
        {
            AddVertex(verts, p0, uv0, normal, tileCoords);
            AddVertex(verts, p1, uv1, normal, tileCoords);
            AddVertex(verts, p2, uv2, normal, tileCoords);
            AddVertex(verts, p3, uv3, normal, tileCoords);
        }

        inds.Add(start); inds.Add(start + 1); inds.Add(start + 2);
        inds.Add(start); inds.Add(start + 2); inds.Add(start + 3);
    }

    private static void AddVertex(List<float> v, Vector3 pos, Vector2 uv, Vector3 normal, Vector2 tileCoords)
    {
        v.Add(pos.X); v.Add(pos.Y); v.Add(pos.Z);
        v.Add(uv.X); v.Add(uv.Y);
        v.Add(normal.X); v.Add(normal.Y); v.Add(normal.Z);
        v.Add(tileCoords.X); v.Add(tileCoords.Y);
    }

    public Renderer.ChunkMesh?[] UploadMesh()
    {
        var data = Interlocked.Exchange(ref _pendingMeshData, null);
        if (data == null) return Array.Empty<Renderer.ChunkMesh?>();

        var old = new Renderer.ChunkMesh?[] { OpaqueMesh, LeafMesh, WaterMesh };

        OpaqueMesh = data.OpaqueVerts.Length > 0 ? new Renderer.ChunkMesh(data.OpaqueVerts, data.OpaqueInds) : null;
        LeafMesh   = data.LeafVerts.Length   > 0 ? new Renderer.ChunkMesh(data.LeafVerts,   data.LeafInds)   : null;
        WaterMesh  = data.WaterVerts.Length  > 0 ? new Renderer.ChunkMesh(data.WaterVerts,  data.WaterInds)  : null;

        IsUploaded = true;
        return old;
    }

    public Renderer.ChunkMesh?[] ReleaseMeshes()
    {
        var old = new Renderer.ChunkMesh?[] { OpaqueMesh, LeafMesh, WaterMesh };
        OpaqueMesh = null;
        LeafMesh = null;
        WaterMesh = null;
        return old;
    }
}