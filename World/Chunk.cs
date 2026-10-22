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
    public Renderer.ChunkMesh? Mesh { get; private set; }

    public Chunk(Vector3i position) => Position = position;

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ)
            return BlockType.Air;
        return _blocks[x, y, z];
    }

    // Обычный SetBlock (с перестройкой меша)
    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        RebuildMesh();
    }

    // Быстрый SetBlock без перестройки меша (используется при генерации мира)
    public void SetBlockDirect(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
    }

    public void RebuildMesh()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int x = 0; x < SizeX; x++)
        for (int y = 0; y < SizeY; y++)
        for (int z = 0; z < SizeZ; z++)
        {
            var block = _blocks[x, y, z];
            if (block == BlockType.Air) continue;

            AddFaceIfVisible(vertices, indices, x, y, z, 0, 1, 0, block, 0);
            AddFaceIfVisible(vertices, indices, x, y, z, 0, -1, 0, block, 1);
            AddFaceIfVisible(vertices, indices, x, y, z, 0, 0, 1, block, 2);
            AddFaceIfVisible(vertices, indices, x, y, z, 0, 0, -1, block, 3);
            AddFaceIfVisible(vertices, indices, x, y, z, -1, 0, 0, block, 4);
            AddFaceIfVisible(vertices, indices, x, y, z, 1, 0, 0, block, 5);
        }

        if (vertices.Count == 0)
        {
            Mesh?.Dispose();
            Mesh = null;
            return;
        }

        Mesh?.Dispose();
        Mesh = new Renderer.ChunkMesh(vertices.ToArray(), indices.ToArray());
    }

    private void AddFaceIfVisible(List<float> verts, List<uint> inds,
        int x, int y, int z, int dx, int dy, int dz, BlockType block, int faceIndex)
    {
        int nx = x + dx, ny = y + dy, nz = z + dz;

        if (nx < 0 || nx >= SizeX || ny < 0 || ny >= SizeY || nz < 0 || nz >= SizeZ)
            return;

        if (BlockData.IsSolid(_blocks[nx, ny, nz])) return;

        var uv = BlockData.GetUV(block, faceIndex);
        uint start = (uint)(verts.Count / 8);

        AddVertex(verts, x, y, z, uv, faceIndex);
        AddVertex(verts, x + 1, y, z, uv, faceIndex);
        AddVertex(verts, x + 1, y, z + 1, uv, faceIndex);
        AddVertex(verts, x, y, z, uv, faceIndex);
        AddVertex(verts, x + 1, y, z + 1, uv, faceIndex);
        AddVertex(verts, x, y, z + 1, uv, faceIndex);

        inds.AddRange(new uint[] { start, start + 1, start + 2, start + 3, start + 4, start + 5 });
    }

    private static void AddVertex(List<float> v, float x, float y, float z, Vector4 uv, int face)
    {
        v.Add(x); v.Add(y); v.Add(z);
        v.Add(uv.X);
        v.Add(face == 1 ? uv.W : uv.Y);
        v.Add(0); v.Add(0); v.Add(0);
    }

    public void Dispose() => Mesh?.Dispose();
}