using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class Chunk
{
    public const int SizeX = 16;
    public const int SizeY = 64; // упрощённая высота, для прототипа достаточно
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

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= SizeX || y < 0 || y >= SizeY || z < 0 || z >= SizeZ) return;
        _blocks[x, y, z] = type;
        RebuildMesh();
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

            // Проверяем каждого соседа; грань добавляется, если сосед прозрачный
            AddFaceIfVisible(vertices, indices, x, y, z, 0, 1, 0, block, 0); // top
            AddFaceIfVisible(vertices, indices, x, y, z, 0, -1, 0, block, 1); // bottom
            AddFaceIfVisible(vertices, indices, x, y, z, 0, 0, 1, block, 2); // front
            AddFaceIfVisible(vertices, indices, x, y, z, 0, 0, -1, block, 3); // back
            AddFaceIfVisible(vertices, indices, x, y, z, -1, 0, 0, block, 4); // left
            AddFaceIfVisible(vertices, indices, x, y, z, 1, 0, 0, block, 5); // right
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

        // Выход за пределы чанка считаем воздухом (на стыках чанков нужна отдельная логика)
        if (nx < 0 || nx >= SizeX || ny < 0 || ny >= SizeY || nz < 0 || nz >= SizeZ)
            return;

        if (BlockData.IsSolid(_blocks[nx, ny, nz])) return;

        var uv = BlockData.GetUV(block, faceIndex);
        uint start = (uint)(verts.Count / 8);

        // 6 вершин на грань (2 треугольника) с позицией, UV и нормалью
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
        v.Add(face == 0 ? uv.X : face == 1 ? uv.X : uv.X);
        v.Add(face == 0 ? uv.Y : face == 1 ? uv.W : uv.Y);
        // нормали упрощены; для прототипа можно передавать 0
        v.Add(0); v.Add(0); v.Add(0);
    }

    public void Dispose() => Mesh?.Dispose();
}