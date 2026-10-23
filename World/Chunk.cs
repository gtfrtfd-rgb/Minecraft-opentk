using System.Collections.Generic;
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
    public Renderer.ChunkMesh? TransparentMesh { get; private set; }
    public bool IsDirty { get; private set; } = true; 

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

    private float[] _opaqueVerts = System.Array.Empty<float>();
    private uint[] _opaqueInds = System.Array.Empty<uint>();
    private float[] _transVerts = System.Array.Empty<float>();
    private uint[] _transInds = System.Array.Empty<uint>();

    public void RebuildMeshData(World world)
    {
        var opaqueVerts = new List<float>();
        var opaqueInds = new List<uint>();
        var transVerts = new List<float>();
        var transInds = new List<uint>();

        GreedyMesh(world, opaqueVerts, opaqueInds, false);
        GreedyMesh(world, transVerts, transInds, true);

        _opaqueVerts = opaqueVerts.ToArray();
        _opaqueInds = opaqueInds.ToArray();
        _transVerts = transVerts.ToArray();
        _transInds = transInds.ToArray();
    }

    private void GreedyMesh(World world, List<float> verts, List<uint> inds, bool isTransparentPass)
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
                        BlockType a = (x[d] >= 0) ? GetBlock(x[0], x[1], x[2]) : BlockType.Air;
                        BlockType b = (x[d] < maxD - 1) ? GetBlock(x[0] + q[0], x[1] + q[1], x[2] + q[2]) : BlockType.Air;

                        bool aTrans = BlockData.IsTransparent(a);
                        bool bTrans = BlockData.IsTransparent(b);

                        if (isTransparentPass != aTrans && a != BlockType.Air) { mask[n++] = 0; continue; }
                        if (isTransparentPass != bTrans && b != BlockType.Air) { mask[n++] = 0; continue; }

                        if (a == BlockType.Air && b == BlockType.Air) { mask[n++] = 0; continue; }
                        
                        if (a != BlockType.Air && b != BlockType.Air)
                        {
                            if (BlockData.IsOpaque(a) && BlockData.IsOpaque(b)) { mask[n++] = 0; continue; }
                            if (a == b) { mask[n++] = 0; continue; }
                        }

                        if (a != BlockType.Air && (b == BlockType.Air || !BlockData.IsOpaque(b)))
                        {
                            mask[n++] = (int)a * 2 + 1; 
                        }
                        else if (b != BlockType.Air && (a == BlockType.Air || !BlockData.IsOpaque(a)))
                        {
                            mask[n++] = (int)b * 2 + 2;
                        }
                        else
                        {
                            mask[n++] = 0;
                        }
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
                        int[] du = { 0, 0, 0 };
                        int[] dv = { 0, 0, 0 };
                        du[u] = w;
                        dv[v] = h;

                        BlockType block = (BlockType)((c - 1) / 2);
                        int face = (c - 1) % 2;

                        Vector3 normal = new Vector3(q[0], q[1], q[2]) * (face == 0 ? 1 : -1);
                        
                        // ИСПРАВЛЕНО: Передаем d и face для правильного выбора текстуры
                        Vector2 tileCoords = BlockData.GetTileCoords(block, d, face);

                        // ИСПРАВЛЕНО: Добавлен q[0], q[1], q[2] для смещения грани на нужную сторону
                        Vector3 p0 = new Vector3(x[0], x[1], x[2]) + new Vector3(q[0], q[1], q[2]) + new Vector3(Position.X * SizeX, 0, Position.Y * SizeZ);
                        Vector3 p1 = p0 + new Vector3(du[0], du[1], du[2]);
                        Vector3 p2 = p0 + new Vector3(du[0] + dv[0], du[1] + dv[1], du[2] + dv[2]);
                        Vector3 p3 = p0 + new Vector3(dv[0], dv[1], dv[2]);

                        if (face == 0)
                            AddQuad(verts, inds, p0, p1, p2, p3, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h), normal, tileCoords);
                        else
                            AddQuad(verts, inds, p0, p3, p2, p1, new Vector2(0, 0), new Vector2(0, h), new Vector2(w, h), new Vector2(w, 0), normal, tileCoords);

                        for (int l = 0; l < h; l++)
                            for (int k = 0; k < w; k++)
                                mask[n + k + l * maxU] = 0;

                        i += w;
                        n += w;
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
        AddVertex(verts, p0, uv0, normal, tileCoords); AddVertex(verts, p1, uv1, normal, tileCoords);
        AddVertex(verts, p2, uv2, normal, tileCoords); AddVertex(verts, p3, uv3, normal, tileCoords);
        inds.AddRange(new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 });
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

        TransparentMesh?.Dispose();
        TransparentMesh = _transVerts.Length > 0 ? new Renderer.ChunkMesh(_transVerts, _transInds) : null;
        
        IsDirty = false;
    }

    public void Dispose()
    {
        OpaqueMesh?.Dispose();
        TransparentMesh?.Dispose();
    }
}