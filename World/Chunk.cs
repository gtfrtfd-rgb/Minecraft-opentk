using System.Collections.Generic;
using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class Chunk
{
    public const int SizeX = 16;
    public const int SizeY = 64;
    public const int SizeZ = 16;

    public Vector3i Position { get; }
    private readonly BlockType[,,] _blocks = new BlockType[SizeX, SizeY, SizeZ];
    
    public Renderer.ChunkMesh? OpaqueMesh { get; private set; }
    public Renderer.ChunkMesh? TransparentMesh { get; private set; }

    public Chunk(Vector3i position) => Position = position;

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return BlockType.Air;
        return _blocks[x, y, z];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        RebuildMesh();
    }

    public void SetBlockDirect(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
    }

    public void RebuildMesh()
    {
        var opaqueVerts = new List<float>();
        var opaqueInds = new List<uint>();
        
        var transVerts = new List<float>();
        var transInds = new List<uint>();

        for (int x = 0; x < SizeX; x++)
        for (int y = 0; y < SizeY; y++)
        for (int z = 0; z < SizeZ; z++)
        {
            var block = _blocks[x, y, z];
            if (block == BlockType.Air) continue;

            bool isTrans = BlockData.IsTransparent(block);
            var vList = isTrans ? transVerts : opaqueVerts;
            var iList = isTrans ? transInds : opaqueInds;

            AddFaceIfVisible(vList, iList, x, y, z, 0, 1, 0, block, 0);
            AddFaceIfVisible(vList, iList, x, y, z, 0, -1, 0, block, 1);
            AddFaceIfVisible(vList, iList, x, y, z, 0, 0, 1, block, 2);
            AddFaceIfVisible(vList, iList, x, y, z, 0, 0, -1, block, 3);
            AddFaceIfVisible(vList, iList, x, y, z, -1, 0, 0, block, 4);
            AddFaceIfVisible(vList, iList, x, y, z, 1, 0, 0, block, 5);
        }

        OpaqueMesh?.Dispose();
        OpaqueMesh = opaqueVerts.Count > 0 ? new Renderer.ChunkMesh(opaqueVerts.ToArray(), opaqueInds.ToArray()) : null;

        TransparentMesh?.Dispose();
        TransparentMesh = transVerts.Count > 0 ? new Renderer.ChunkMesh(transVerts.ToArray(), transInds.ToArray()) : null;
    }

    private void AddFaceIfVisible(List<float> verts, List<uint> inds,
        int x, int y, int z, int dx, int dy, int dz, BlockType block, int faceIndex)
    {
        int nx = x + dx, ny = y + dy, nz = z + dz;

        if (nx >= 0 && nx < SizeX && ny >= 0 && ny < SizeY && nz >= 0 && nz < SizeZ)
        {
            BlockType neighbor = _blocks[nx, ny, nz];
            // Если сосед сплошной, не рисуем грань
            if (BlockData.IsOpaque(neighbor)) return;
            // Если сосед такой же прозрачный (например, вода-вода), тоже не рисуем грань
            if (neighbor == block && BlockData.IsTransparent(block)) return;
        }

        var uv = BlockData.GetUV(block, faceIndex);
        float X = Position.X * SizeX + x, Y = y, Z = Position.Z * SizeZ + z;
        float u0 = uv.X, v0 = uv.Y, u1 = uv.Z, v1 = uv.W;

        switch (faceIndex)
        {
            case 0: AddQuad(verts, inds, new Vector3(X, Y + 1, Z), new Vector3(X, Y + 1, Z + 1), new Vector3(X + 1, Y + 1, Z + 1), new Vector3(X + 1, Y + 1, Z), new Vector2(u0, v1), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector3(0, 1, 0)); break;
            case 1: AddQuad(verts, inds, new Vector3(X, Y, Z + 1), new Vector3(X, Y, Z), new Vector3(X + 1, Y, Z), new Vector3(X + 1, Y, Z + 1), new Vector2(u0, v1), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector3(0, -1, 0)); break;
            case 2: AddQuad(verts, inds, new Vector3(X, Y, Z + 1), new Vector3(X + 1, Y, Z + 1), new Vector3(X + 1, Y + 1, Z + 1), new Vector3(X, Y + 1, Z + 1), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), new Vector3(0, 0, 1)); break;
            case 3: AddQuad(verts, inds, new Vector3(X + 1, Y, Z), new Vector3(X, Y, Z), new Vector3(X, Y + 1, Z), new Vector3(X + 1, Y + 1, Z), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), new Vector3(0, 0, -1)); break;
            case 4: AddQuad(verts, inds, new Vector3(X, Y, Z), new Vector3(X, Y, Z + 1), new Vector3(X, Y + 1, Z + 1), new Vector3(X, Y + 1, Z), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), new Vector3(-1, 0, 0)); break;
            case 5: AddQuad(verts, inds, new Vector3(X + 1, Y, Z + 1), new Vector3(X + 1, Y, Z), new Vector3(X + 1, Y + 1, Z), new Vector3(X + 1, Y + 1, Z + 1), new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), new Vector3(1, 0, 0)); break;
        }
    }

    private void AddQuad(List<float> verts, List<uint> inds,
        Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Vector3 normal)
    {
        uint start = (uint)(verts.Count / 8);
        AddVertex(verts, p0, uv0, normal); AddVertex(verts, p1, uv1, normal);
        AddVertex(verts, p2, uv2, normal); AddVertex(verts, p3, uv3, normal);
        inds.AddRange(new uint[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    private static void AddVertex(List<float> v, Vector3 pos, Vector2 uv, Vector3 normal)
    {
        v.Add(pos.X); v.Add(pos.Y); v.Add(pos.Z);
        v.Add(uv.X); v.Add(uv.Y);
        v.Add(normal.X); v.Add(normal.Y); v.Add(normal.Z);
    }

    public void Dispose()
    {
        OpaqueMesh?.Dispose();
        TransparentMesh?.Dispose();
    }
}