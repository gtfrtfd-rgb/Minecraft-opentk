using OpenTK.Mathematics;
using MinecraftClone.Utils;

namespace MinecraftClone.World;

public class World
{
    private readonly Dictionary<Vector3i, Chunk> _chunks = new();
    private readonly Renderer.TextureAtlas _atlas;

    public World(Renderer.TextureAtlas atlas) => _atlas = atlas;

    public void Generate(int chunksX, int chunksZ)
    {
        var noise = new SimplexNoise(1337);

        for (int cx = 0; cx < chunksX; cx++)
        for (int cz = 0; cz < chunksZ; cz++)
        {
            var chunk = new Chunk(new Vector3i(cx, 0, cz));
            _chunks[chunk.Position] = chunk;

            for (int x = 0; x < Chunk.SizeX; x++)
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = cx * Chunk.SizeX + x;
                int worldZ = cz * Chunk.SizeZ + z;

                float n = noise.Noise(worldX * 0.05f, worldZ * 0.05f);
                int height = (int)(n * 8 + 20);

                for (int y = 0; y < Chunk.SizeY; y++)
                {
                    BlockType block = BlockType.Air;
                    if (y == height) block = BlockType.Grass;
                    else if (y < height && y > height - 4) block = BlockType.Dirt;
                    else if (y <= height - 4) block = BlockType.Stone;

                    if (block != BlockType.Air)
                        chunk.SetBlockDirect(x, y, z, block);
                }
            }
            chunk.RebuildMesh();
        }
    }

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        var chunkPos = new Vector3i(
            (int)MathF.Floor(worldX / (float)Chunk.SizeX),
            0,
            (int)MathF.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return BlockType.Air;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Z * Chunk.SizeZ;
        return chunk.GetBlock(localX, worldY, localZ);
    }

    public void SetBlock(int worldX, int worldY, int worldZ, BlockType type)
    {
        var chunkPos = new Vector3i(
            (int)MathF.Floor(worldX / (float)Chunk.SizeX),
            0,
            (int)MathF.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Z * Chunk.SizeZ;
        chunk.SetBlock(localX, worldY, localZ, type);
    }

    // ОБНОВЛЕНО: Теперь принимает Frustum для отсечения
    public IEnumerable<Chunk> GetVisibleChunks(Vector3 cameraPos, Frustum frustum)
    {
        foreach (var chunk in _chunks.Values)
        {
            // Быстрая проверка по дистанции (даже если в фрустуме, далеко не рисуем)
            var center = new Vector3(
                chunk.Position.X * Chunk.SizeX + Chunk.SizeX / 2f,
                0,
                chunk.Position.Z * Chunk.SizeZ + Chunk.SizeZ / 2f);

            if ((center - cameraPos).Length > 80f) 
                continue;

            // Проверка: находится ли чанк в поле зрения камеры
            var min = new Vector3(chunk.Position.X * Chunk.SizeX, 0, chunk.Position.Z * Chunk.SizeZ);
            var max = new Vector3(min.X + Chunk.SizeX, Chunk.SizeY, min.Z + Chunk.SizeZ);

            if (frustum.IsBoxVisible(min, max))
            {
                yield return chunk;
            }
        }
    }
}