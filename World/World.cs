using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class World
{
    private readonly Dictionary<Vector3i, Chunk> _chunks = new();
    private readonly Renderer.TextureAtlas _atlas;

    public World(Renderer.TextureAtlas atlas) => _atlas = atlas;

    public void Generate(int chunksX, int chunksZ)
    {
        var noise = new Utils.SimplexNoise(1337);

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
                int height = (int)(n * 8 + 20); // базовая высота ~20

                for (int y = 0; y < Chunk.SizeY; y++)
                {
                    BlockType block = BlockType.Air;
                    if (y == height) block = BlockType.Grass;
                    else if (y < height && y > height - 4) block = BlockType.Dirt;
                    else if (y <= height - 4) block = BlockType.Stone;

                    if (block != BlockType.Air)
                        chunk.SetBlock(x, y, z, block);
                }
            }

            chunk.RebuildMesh();
        }
    }

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        var chunkPos = new Vector3i(
            Mathf.Floor(worldX / (float)Chunk.SizeX),
            0,
            Mathf.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return BlockType.Air;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Z * Chunk.SizeZ;
        return chunk.GetBlock(localX, worldY, localZ);
    }

    public void SetBlock(int worldX, int worldY, int worldZ, BlockType type)
    {
        var chunkPos = new Vector3i(
            Mathf.Floor(worldX / (float)Chunk.SizeX),
            0,
            Mathf.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Z * Chunk.SizeZ;
        chunk.SetBlock(localX, worldY, localZ, type);
    }

    public IEnumerable<Chunk> GetVisibleChunks(Vector3 cameraPos)
    {
        // Упрощённая видимость: все чанки в радиусе 32 блоков
        foreach (var chunk in _chunks.Values)
        {
            var center = new Vector3(
                chunk.Position.X * Chunk.SizeX + Chunk.SizeX / 2f,
                0,
                chunk.Position.Z * Chunk.SizeZ + Chunk.SizeZ / 2f);

            if ((center - cameraPos).Length < 48f)
                yield return chunk;
        }
    }
}