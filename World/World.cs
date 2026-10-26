using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Mathematics;
using MinecraftClone.Utils;

namespace MinecraftClone.World;

public class World : IDisposable
{
    private readonly ConcurrentDictionary<Vector2i, Chunk> _chunks = new();
    private readonly SimplexNoise _noise;

    private readonly ConcurrentQueue<Vector2i> _chunksToGenerate = new();
    private readonly ConcurrentQueue<Chunk> _chunksToUpload = new();
    private readonly ConcurrentDictionary<Vector2i, byte> _pendingChunks = new();

    private readonly Queue<Vector2i> _dirtyQueue = new();
    private readonly HashSet<Vector2i> _dirtySet = new();

    private readonly List<Thread> _workers = new();
    private volatile bool _isRunning = true;
    private bool _disposed;

    private const int MaxDirtyRebuildsPerFrame = 4;
    private const int MaxUploadsPerFrame = 10;

    public World()
    {
        _noise = new SimplexNoise(1337);

        int threadCount = Math.Max(1, Environment.ProcessorCount - 1);
        for (int i = 0; i < threadCount; i++)
        {
            var thread = new Thread(GenerationLoop) { IsBackground = true, Name = $"WorldGen-{i}" };
            thread.Start();
            _workers.Add(thread);
        }
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
        MarkChunkDirty(chunkPos);

        if (localX == 0) MarkChunkDirty(chunkPos + new Vector2i(-1, 0));
        if (localX == Chunk.SizeX - 1) MarkChunkDirty(chunkPos + new Vector2i(1, 0));
        if (localZ == 0) MarkChunkDirty(chunkPos + new Vector2i(0, -1));
        if (localZ == Chunk.SizeZ - 1) MarkChunkDirty(chunkPos + new Vector2i(0, 1));
    }

    public bool IsChunkReady(float worldX, float worldZ)
    {
        var chunkPos = new Vector2i(
            (int)MathF.Floor(worldX / Chunk.SizeX),
            (int)MathF.Floor(worldZ / Chunk.SizeZ));
        if (_chunks.TryGetValue(chunkPos, out var chunk))
        {
            return chunk.IsUploaded;
        }
        return false;
    }

    private void MarkChunkDirty(Vector2i pos)
    {
        if (!_chunks.ContainsKey(pos)) return;
        if (_dirtySet.Add(pos)) _dirtyQueue.Enqueue(pos);
    }

    public void UpdateChunks(Vector3 playerPos, int renderDistance = 8)
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

        missingChunks.Sort((a, b) =>
        {
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

        int uploaded = 0;
        while (_chunksToUpload.TryDequeue(out var chunk) && uploaded < MaxUploadsPerFrame)
        {
            chunk.UploadMesh();
            uploaded++;
            
            MarkChunkDirty(chunk.Position);
            MarkChunkDirty(chunk.Position + new Vector2i(1, 0));
            MarkChunkDirty(chunk.Position + new Vector2i(-1, 0));
            MarkChunkDirty(chunk.Position + new Vector2i(0, 1));
            MarkChunkDirty(chunk.Position + new Vector2i(0, -1));
        }

        int rebuilt = 0;
        while (_dirtyQueue.Count > 0 && rebuilt < MaxDirtyRebuildsPerFrame)
        {
            var pos = _dirtyQueue.Dequeue();
            _dirtySet.Remove(pos);
            if (_chunks.TryGetValue(pos, out var chunk) && chunk.IsDirty)
            {
                chunk.RebuildMeshData(this);
                chunk.UploadMesh();
                rebuilt++;
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
            _dirtySet.Remove(pos);
        }
    }

    private void GenerationLoop()
    {
        while (_isRunning)
        {
            if (_chunksToGenerate.TryDequeue(out var pos))
            {
                if (!_isRunning) break;
                var chunk = new Chunk(pos);
                GenerateChunkData(chunk);
                chunk.RebuildMeshData(this);
                _chunks[pos] = chunk; 
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
                    int hash = Math.Abs(worldX * 71 + worldZ * 37);
                    if (hash % 150 == 0)
                    {
                        GenerateTree(chunk, x, height + 1, z);
                    }
                }
            }
        }
    }

    private void GenerateTree(Chunk chunk, int x, int y, int z)
    {
        var rnd = new Random(Math.Abs(x * 31 + z * 17));
        int height = 4 + rnd.Next(2);

        for (int i = 0; i < height; i++)
            chunk.SetBlockDirect(x, y + i, z, BlockType.Wood);

        int topY = y + height - 1;

        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (Math.Abs(dx) == 1 && Math.Abs(dz) == 1) continue;
            if (rnd.NextDouble() < 0.2) continue;
            SetLeaf(chunk, x + dx, topY - 2, z + dz);
        }

        for (int dy = -1; dy <= 0; dy++)
        {
            for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                if (Math.Abs(dx) == 2 && Math.Abs(dz) == 2) continue;
                if (rnd.NextDouble() < 0.2) continue;
                SetLeaf(chunk, x + dx, topY + dy, z + dz);
            }
        }

        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (Math.Abs(dx) == 1 && Math.Abs(dz) == 1) continue;
            if (rnd.NextDouble() < 0.2) continue;
            SetLeaf(chunk, x + dx, topY + 1, z + dz);
        }

        if (rnd.NextDouble() > 0.2) SetLeaf(chunk, x, topY + 2, z);
        if (rnd.NextDouble() > 0.2) SetLeaf(chunk, x + 1, topY + 1, z);
        if (rnd.NextDouble() > 0.2) SetLeaf(chunk, x - 1, topY + 1, z);
        if (rnd.NextDouble() > 0.2) SetLeaf(chunk, x, topY + 1, z + 1);
        if (rnd.NextDouble() > 0.2) SetLeaf(chunk, x, topY + 1, z - 1);
    }

    private void SetLeaf(Chunk chunk, int x, int y, int z)
    {
        if (x >= 0 && x < Chunk.SizeX && y >= 0 && y < Chunk.SizeY && z >= 0 && z < Chunk.SizeZ)
        {
            if (chunk.GetBlock(x, y, z) == BlockType.Air)
                chunk.SetBlockDirect(x, y, z, BlockType.Leaves);
        }
    }

    public void GetVisibleChunks(Vector3 cameraPos, Frustum frustum, List<Chunk> result)
    {
        result.Clear();
        foreach (var chunk in _chunks.Values)
        {
            var center = new Vector3(chunk.Position.X * Chunk.SizeX + Chunk.SizeX / 2f, 0, chunk.Position.Y * Chunk.SizeZ + Chunk.SizeZ / 2f);
            if ((center - cameraPos).Length > 150f) continue;

            var min = new Vector3(chunk.Position.X * Chunk.SizeX, 0, chunk.Position.Y * Chunk.SizeZ);
            var max = new Vector3(min.X + Chunk.SizeX, Chunk.SizeY, min.Z + Chunk.SizeZ);

            if (frustum.IsBoxVisible(min, max))
            {
                result.Add(chunk);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _isRunning = false;

        foreach (var thread in _workers)
        {
            if (thread.IsAlive) thread.Join(2000);
        }
        _workers.Clear();

        _chunksToGenerate.Clear();
        while (_chunksToUpload.TryDequeue(out var pendingChunk))
        {
            pendingChunk.Dispose();
        }

        foreach (var chunk in _chunks.Values)
        {
            chunk.Dispose();
        }
        _chunks.Clear();

        _dirtyQueue.Clear();
        _dirtySet.Clear();
        _pendingChunks.Clear();
    }
}