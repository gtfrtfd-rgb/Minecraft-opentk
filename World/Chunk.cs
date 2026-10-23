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
    
    // Оптимизация: запоминаем максимальную высоту, чтобы не перебирать пустоту
    private int _maxHeight = 0;
    
    private float[] _opaqueVerts = System.Array.Empty<float>();
    private uint[] _opaqueInds = System.Array.Empty<uint>();
    private float[] _transVerts = System.Array.Empty<float>();
    private uint[] _transInds = System.Array.Empty<uint>();

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
        if (y > _maxHeight) _maxHeight = y;
    }

    public void SetBlockDirect(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        if (type != BlockType.Air && y > _maxHeight) _maxHeight = y; // Обновляем высоту
    }

    public void RebuildMeshData(World world)
    {
        var opaqueVerts = new List<float>();
        var opaqueInds = new List<uint>();
        
        var transVerts = new List<float>();
        var transInds = new List<uint>();

        // Итерируемся только до максимальной высоты, а не до 128!
        for (int x = 0; x < SizeX; x++)
        for (int y = 0; y <= _maxHeight + 1; y++) // +1 на всякий случай
        for (int z = 0; z < SizeZ; z++)
        {
            var block = _blocks[x, y, z];
            if (block == BlockType.Air) continue;

            bool isTrans = BlockData.IsTransparent(block);
            var vList = isTrans ? transVerts : opaqueVerts;
            var iList = isTrans ? transInds : opaqueInds;

            AddFaceIfVisible(world, vList, iList, x, y, z, 0, 1, 0, block, 0);
            AddFaceIfVisible(world, vList, iList, x, y, z, 0, -1, 0, block, 1);
            AddFaceIfVisible(world, vList, iList, x, y, z, 0, 0, 1, block, 2);
            AddFaceIfVisible(world, vList, iList, x, y, z, 0, 0, -1, block, 3);
            AddFaceIfVisible(world, vList, iList, x, y, z, -1, 0, 0, block, 4);
            AddFaceIfVisible(world, vList, iList, x, y, z, 1, 0, 0, block, 5);
        }

        _opaqueVerts = opaqueVerts.ToArray();
        _opaqueInds = opaqueInds.ToArray();
        _transVerts = transVerts.ToArray();
        _transInds = transInds.ToArray();
    }

    public void UploadMesh()
    {
        OpaqueMesh?.Dispose();
        OpaqueMesh = _opaqueVerts.Length > 0 ? new Renderer.ChunkMesh(_opaqueVerts, _opaqueInds) : null;

        TransparentMesh?.Dispose();
        TransparentMesh = _transVerts.Length > 0 ? new Renderer.ChunkMesh(_transVerts, _transInds) : null;
        
        IsDirty = false;
    }

    private void AddFaceIfVisible(World world, List<float> verts, List<uint> inds,
        int x, int y, int z, int dx, int dy, int dz, BlockType block, int faceIndex)
    {
        int nx = x + dx, ny = y + dy, nz = z + dz;

        int globalX = Position.X * SizeX + nx;
        int globalZ = Position.Y * SizeZ + nz;

        BlockType neighbor = world.GetBlock(globalX, ny, globalZ);

        if (BlockData.IsOpaque(neighbor)) return;
        if (neighbor == block && BlockData.IsTransparent(block)) return;

        var uv = BlockData.GetUV(block, faceIndex);
        float X = Position.X * SizeX + x, Y = y, Z = Position.Y * SizeZ + z;
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