using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Mathematics;
using MinecraftClone.Utils;

namespace MinecraftClone.World;

public class World : System.IDisposable
{
    private readonly ConcurrentDictionary<Vector2i, Chunk> _chunks = new();
    private readonly Renderer.TextureAtlas _atlas;
    private readonly SimplexNoise _noise;

    private readonly ConcurrentQueue<Vector2i> _chunksToGenerate = new();
    private readonly ConcurrentQueue<Chunk> _chunksToUpload = new();
    
    // Используем словарь для потокобезопасности
    private readonly ConcurrentDictionary<Vector2i, byte> _pendingChunks = new();
    
    private readonly List<Thread> _workers = new();
    private volatile bool _isRunning = true;

    public World(Renderer.TextureAtlas atlas)
    {
        _atlas = atlas;
        _noise = new SimplexNoise(1337);

        // Запускаем столько потоков, сколько ядер у процессора (минус 1 для главного)
        int threadCount = System.Math.Max(1, System.Environment.ProcessorCount - 1);
        
        for (int i = 0; i < threadCount; i++)
        {
            var thread = new Thread(GenerationLoop);
            thread.IsBackground = true;
            thread.Start();
            _workers.Add(thread);
        }
    }

    public bool IsChunkLoaded(int worldX, int worldZ)
    {
        var chunkPos = new Vector2i(
            (int)MathF.Floor(worldX / (float)Chunk.SizeX),
            (int)MathF.Floor(worldZ / (float)Chunk.SizeZ));
        return _chunks.ContainsKey(chunkPos);
    }

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        var chunkPos = new Vector2i(
            (int)MathF.Floor(worldX / (float)Chunk.SizeX),
            (int)MathF.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return BlockType.Air;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Y * Chunk.SizeZ;
        return chunk.GetBlock(localX, worldY, localZ);
    }

    public void SetBlock(int worldX, int worldY, int worldZ, BlockType type)
    {
        var chunkPos = new Vector2i(
            (int)MathF.Floor(worldX / (float)Chunk.SizeX),
            (int)MathF.Floor(worldZ / (float)Chunk.SizeZ));

        if (!_chunks.TryGetValue(chunkPos, out var chunk)) return;

        int localX = worldX - chunkPos.X * Chunk.SizeX;
        int localZ = worldZ - chunkPos.Y * Chunk.SizeZ;
        chunk.SetBlock(localX, worldY, localZ, type);
        
        if (localX == 0) _chunks.GetValueOrDefault(chunkPos + new Vector2i(-1, 0))?.SetDirty();
        if (localX == Chunk.SizeX - 1) _chunks.GetValueOrDefault(chunkPos + new Vector2i(1, 0))?.SetDirty();
        if (localZ == 0) _chunks.GetValueOrDefault(chunkPos + new Vector2i(0, -1))?.SetDirty();
        if (localZ == Chunk.SizeZ - 1) _chunks.GetValueOrDefault(chunkPos + new Vector2i(0, 1))?.SetDirty();
    }

    public void UpdateChunks(Vector3 playerPos, int renderDistance = 10)
    {
        Vector2i currentChunk = new Vector2i(
            (int)MathF.Floor(playerPos.X / Chunk.SizeX),
            (int)MathF.Floor(playerPos.Z / Chunk.SizeZ));

        var missingChunks = new List<Vector2i>();

        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                Vector2i pos = currentChunk + new Vector2i(x, z);
                if (!_chunks.ContainsKey(pos) && !_pendingChunks.ContainsKey(pos))
                {
                    missingChunks.Add(pos);
                }
            }
        }

        // СОРТИРОВКА: Чанки под ногами генерируются первыми
        missingChunks.Sort((a, b) => {
            int distA = (a.X - currentChunk.X) * (a.X - currentChunk.X) + (a.Y - currentChunk.Y) * (a.Y - currentChunk.Y);
            int distB = (b.X - currentChunk.X) * (b.X - currentChunk.X) + (b.Y - currentChunk.Y) * (b.Y - currentChunk.Y);
            return distA.CompareTo(distB);
        });

        foreach (var pos in missingChunks)
        {
            if (_pendingChunks.TryAdd(pos, 0))
            {
                _chunksToGenerate.Enqueue(pos);
            }
        }

        // УВЕЛИЧЕН ЛИМИТ: Грузим на видеокарту до 10 чанков за кадр
        int uploaded = 0;
        while (_chunksToUpload.TryDequeue(out var chunk) && uploaded < 10)
        {
            chunk.UploadMesh();
            _chunks[chunk.Position] = chunk;
            uploaded++;
        }

        foreach (var chunk in _chunks.Values)
        {
            if (chunk.IsDirty)
            {
                chunk.RebuildMeshData(this);
                chunk.UploadMesh();
            }
        }

        int unloadDistance = renderDistance + 2;
        var chunksToRemove = new List<Vector2i>();
        foreach (var kvp in _chunks)
        {
            if (Math.Abs(kvp.Key.X - currentChunk.X) > unloadDistance || 
                Math.Abs(kvp.Key.Y - currentChunk.Y) > unloadDistance)
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        foreach (var pos in chunksToRemove)
        {
            if (_chunks.TryRemove(pos, out var chunk))
            {
                chunk.Dispose();
            }
        }
    }

    private void GenerationLoop()
    {
        while (_isRunning)
        {
            if (_chunksToGenerate.TryDequeue(out var pos))
            {
                Chunk chunk = new Chunk(pos);
                GenerateChunkData(chunk);
                chunk.RebuildMeshData(this);
                _chunksToUpload.Enqueue(chunk);
                _pendingChunks.TryRemove(pos, out _);
            }
            else
            {
                Thread.Sleep(5); 
            }
        }
    }

    private void GenerateChunkData(Chunk chunk)
    {
        int cx = chunk.Position.X * Chunk.SizeX;
        int cz = chunk.Position.Y * Chunk.SizeZ;

        for (int x = 0; x < Chunk.SizeX; x++)
        for (int z = 0; z < Chunk.SizeZ; z++)
        {
            int worldX = cx + x;
            int worldZ = cz + z;

            float n = _noise.FractalNoise(worldX * 0.01f, worldZ * 0.01f, 3, 0.5f, 2.0f);
            int height = (int)(n * 8 + 35); 

            for (int y = 0; y <= height; y++) 
            {
                BlockType block = BlockType.Air;
                float caveNoise = _noise.Noise3D(worldX * 0.05f, y * 0.05f, worldZ * 0.05f);
                bool isCave = caveNoise > 0.4f && y < height - 3;

                if (!isCave)
                {
                    if (y == height) block = BlockType.Grass;
                    else if (y < height && y > height - 4) block = BlockType.Dirt;
                    else if (y <= height - 4) block = BlockType.Stone;
                }

                if (block != BlockType.Air)
                    chunk.SetBlockDirect(x, y, z, block);
            }

            if (chunk.GetBlock(x, height, z) == BlockType.Grass)
            {
                if (x > 2 && x < Chunk.SizeX - 2 && z > 2 && z < Chunk.SizeZ - 2)
                {
                    float treeNoise = _noise.Noise(worldX * 0.1f, worldZ * 0.1f);
                    int hash = Math.Abs(worldX * 31 + worldZ * 17);
                    if (treeNoise > 0.4f && hash % 15 == 0)
                    {
                        GenerateTree(chunk, x, height + 1, z);
                    }
                }
            }
        }
    }

    private void GenerateTree(Chunk chunk, int x, int y, int z)
    {
        for (int i = 0; i < 5; i++) chunk.SetBlockDirect(x, y + i, z, BlockType.Wood);
        for (int dy = 2; dy <= 6; dy++)
        {
            int radius = dy >= 4 ? 1 : 2;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                if (Math.Abs(dx) == radius && Math.Abs(dz) == radius) continue;
                int px = x + dx, py = y + dy, pz = z + dz;
                if (px >= 0 && px < Chunk.SizeX && py >= 0 && py < Chunk.SizeY && pz >= 0 && pz < Chunk.SizeZ)
                {
                    if (chunk.GetBlock(px, py, pz) == BlockType.Air)
                        chunk.SetBlockDirect(px, py, pz, BlockType.Leaves);
                }
            }
        }
        chunk.SetBlockDirect(x, y + 6, z, BlockType.Leaves);
    }

    public IEnumerable<Chunk> GetVisibleChunks(Vector3 cameraPos, Frustum frustum)
    {
        foreach (var chunk in _chunks.Values)
        {
            var center = new Vector3(chunk.Position.X * Chunk.SizeX + Chunk.SizeX / 2f, 0, chunk.Position.Y * Chunk.SizeZ + Chunk.SizeZ / 2f);
            if ((center - cameraPos).Length > 200f) continue;

            var min = new Vector3(chunk.Position.X * Chunk.SizeX, 0, chunk.Position.Y * Chunk.SizeZ);
            var max = new Vector3(min.X + Chunk.SizeX, Chunk.SizeY, min.Z + Chunk.SizeZ);

            if (frustum.IsBoxVisible(min, max))
            {
                yield return chunk;
            }
        }
    }

    public void Dispose()
    {
        _isRunning = false;
    }
}