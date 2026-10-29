using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTK.Mathematics;
using MinecraftClone.Utils;

namespace MinecraftClone.World;

public class World : IDisposable
{
    private readonly ConcurrentDictionary<Vector2i, Chunk> _chunks = new();
    private readonly SimplexNoise _noise;

    private readonly ConcurrentQueue<Vector2i> _chunksToGenerate = new();
    private readonly ConcurrentQueue<Vector2i> _chunksToRebuild = new();
    private readonly ConcurrentQueue<Chunk> _chunksToUpload = new();

    private readonly ConcurrentDictionary<Vector2i, byte> _pendingChunks = new();
    private readonly ConcurrentDictionary<Vector2i, byte> _dirtySet = new();

    private readonly ConcurrentQueue<Renderer.ChunkMesh> _meshesToDelete = new();
    private int _framesSinceLastDelete = 0;
    private const int DeleteDelayFrames = 8;
    private const int MaxDeletesPerBatch = 4;

    private readonly List<Thread> _workers = new();
    private volatile bool _isRunning = true;
    private bool _disposed;

    private static readonly long HeavyUploadThresholdTicks = Stopwatch.Frequency / 250;
    private const int CooldownAfterHeavyUpload = 3;
    private int _uploadCooldownFrames = 0;

    private readonly List<Vector2i> _missingChunksScratch = new();
    private readonly List<Vector2i> _chunksToRemoveScratch = new();
    private readonly ChunkDistanceComparer _distanceComparer = new();

    private Vector2i _lastUnloadCheckChunk = new Vector2i(int.MinValue, int.MinValue);
    private int _lastRenderDistance = -1;

    public World()
    {
        _noise = new SimplexNoise(1337);

        int threadCount = Math.Min(2, Math.Max(1, Environment.ProcessorCount - 2));
        for (int i = 0; i < threadCount; i++)
        {
            var thread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"WorldWorker-{i}",
                Priority = ThreadPriority.BelowNormal
            };
            thread.Start();
            _workers.Add(thread);
        }
    }

    public Chunk? GetChunk(int cx, int cz)
    {
        _chunks.TryGetValue(new Vector2i(cx, cz), out var chunk);
        return chunk;
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
        float chunkSizeXf = Chunk.SizeX;
        float chunkSizeZf = Chunk.SizeZ;
        var chunkPos = new Vector2i(
            (int)MathF.Floor(worldX / chunkSizeXf),
            (int)MathF.Floor(worldZ / chunkSizeZf));

        if (!_chunks.TryGetValue(chunkPos, out var center) || !center.IsUploaded)
            return false;

        float localX = worldX - chunkPos.X * chunkSizeXf;
        float localZ = worldZ - chunkPos.Y * chunkSizeZf;

        if (localX < 1f && (!_chunks.TryGetValue(chunkPos + new Vector2i(-1, 0), out var cl) || !cl.IsUploaded)) return false;
        if (localX > chunkSizeXf - 1f && (!_chunks.TryGetValue(chunkPos + new Vector2i(1, 0), out var cr) || !cr.IsUploaded)) return false;
        if (localZ < 1f && (!_chunks.TryGetValue(chunkPos + new Vector2i(0, -1), out var cf) || !cf.IsUploaded)) return false;
        if (localZ > chunkSizeZf - 1f && (!_chunks.TryGetValue(chunkPos + new Vector2i(0, 1), out var cb) || !cb.IsUploaded)) return false;

        return true;
    }

    private void MarkChunkDirty(Vector2i pos)
    {
        if (!_chunks.TryGetValue(pos, out var chunk)) return;
        chunk.SetDirty();
        if (_dirtySet.TryAdd(pos, 0))
            _chunksToRebuild.Enqueue(pos);
    }

    public void UpdateChunks(Vector3 playerPos, int renderDistance = 5)
    {
        Vector2i currentChunk = new Vector2i(
            (int)MathF.Floor(playerPos.X / Chunk.SizeX),
            (int)MathF.Floor(playerPos.Z / Chunk.SizeZ));

        bool needScan = currentChunk != _lastUnloadCheckChunk || !_chunksToGenerate.IsEmpty || renderDistance != _lastRenderDistance;

        if (needScan)
        {
            _missingChunksScratch.Clear();
            for (int x = -renderDistance; x <= renderDistance; x++)
            {
                for (int z = -renderDistance; z <= renderDistance; z++)
                {
                    Vector2i pos = currentChunk + new Vector2i(x, z);
                    if (!_chunks.ContainsKey(pos) && !_pendingChunks.ContainsKey(pos))
                    {
                        _missingChunksScratch.Add(pos);
                    }
                }
            }

            _distanceComparer.Center = currentChunk;
            _missingChunksScratch.Sort(_distanceComparer);

            foreach (var pos in _missingChunksScratch)
            {
                if (_pendingChunks.TryAdd(pos, 0))
                {
                    _chunksToGenerate.Enqueue(pos);
                }
            }
        }

        if (_uploadCooldownFrames > 0)
        {
            _uploadCooldownFrames--;
        }
        else if (_chunksToUpload.TryDequeue(out var chunk))
        {
            long start = Stopwatch.GetTimestamp();
            var oldMeshes = chunk.UploadMesh();
            long elapsed = Stopwatch.GetTimestamp() - start;

            foreach (var m in oldMeshes)
                if (m != null) _meshesToDelete.Enqueue(m);

            if (elapsed > HeavyUploadThresholdTicks)
                _uploadCooldownFrames = CooldownAfterHeavyUpload;
        }

        _framesSinceLastDelete++;
        if (_framesSinceLastDelete >= DeleteDelayFrames)
        {
            _framesSinceLastDelete = 0;
            int deleted = 0;
            while (deleted < MaxDeletesPerBatch && _meshesToDelete.TryDequeue(out var mesh))
            {
                mesh.Dispose();
                deleted++;
            }
        }

        bool needUnload = currentChunk != _lastUnloadCheckChunk || renderDistance != _lastRenderDistance;
        if (needUnload)
        {
            _lastUnloadCheckChunk = currentChunk;
            _lastRenderDistance = renderDistance;
            int unloadDistance = renderDistance + 2;
            _chunksToRemoveScratch.Clear();
            foreach (var kvp in _chunks)
            {
                if (Math.Abs(kvp.Key.X - currentChunk.X) > unloadDistance ||
                    Math.Abs(kvp.Key.Y - currentChunk.Y) > unloadDistance)
                {
                    _chunksToRemoveScratch.Add(kvp.Key);
                }
            }

            foreach (var pos in _chunksToRemoveScratch)
            {
                if (_chunks.TryRemove(pos, out var chunk))
                {
                    var meshes = chunk.ReleaseMeshes();
                    foreach (var m in meshes)
                        if (m != null) _meshesToDelete.Enqueue(m);
                }
                _dirtySet.TryRemove(pos, out _);
            }
        }
    }

    private void WorkerLoop()
    {
        while (_isRunning)
        {
            if (_chunksToRebuild.TryDequeue(out var rebuildPos))
            {
                _dirtySet.TryRemove(rebuildPos, out _);
                if (!_isRunning) break;

                if (_chunks.TryGetValue(rebuildPos, out var chunk) && chunk.TryClearDirty())
                {
                    chunk.RebuildMeshData(this);
                    _chunksToUpload.Enqueue(chunk);
                }
                continue;
            }

            if (_chunksToGenerate.TryDequeue(out var genPos))
            {
                if (!_isRunning) break;

                if (_chunks.ContainsKey(genPos))
                {
                    _pendingChunks.TryRemove(genPos, out _);
                    continue;
                }

                var newChunk = new Chunk(genPos);
                GenerateChunkData(newChunk);
                _chunks[genPos] = newChunk;

                MarkChunkDirty(genPos + new Vector2i(1, 0));
                MarkChunkDirty(genPos + new Vector2i(-1, 0));
                MarkChunkDirty(genPos + new Vector2i(0, 1));
                MarkChunkDirty(genPos + new Vector2i(0, -1));

                newChunk.RebuildMeshData(this);
                _chunksToUpload.Enqueue(newChunk);
                _pendingChunks.TryRemove(genPos, out _);
                continue;
            }

            Thread.Sleep(10);
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

    public void GetVisibleChunks(Vector3 cameraPos, Frustum frustum, List<Chunk> result, int renderDistance)
    {
        result.Clear();
        
        int camChunkX = (int)MathF.Floor(cameraPos.X / Chunk.SizeX);
        int camChunkZ = (int)MathF.Floor(cameraPos.Z / Chunk.SizeZ);

        foreach (var kvp in _chunks)
        {
            var chunk = kvp.Value;
            
            // Пропускаем чанки, которые еще не загружены в VRAM
            if (!chunk.IsUploaded) continue;

            // Квадратное расстояние (как в оригинальном Minecraft).
            // Это гарантирует, что листва, ствол и земля появляются одновременно.
            if (Math.Abs(chunk.Position.X - camChunkX) > renderDistance ||
                Math.Abs(chunk.Position.Y - camChunkZ) > renderDistance)
                continue;

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
        _chunksToRebuild.Clear();

        while (_chunksToUpload.TryDequeue(out var pending))
        {
            foreach (var m in pending.ReleaseMeshes()) m?.Dispose();
        }

        foreach (var chunk in _chunks.Values)
        {
            foreach (var m in chunk.ReleaseMeshes()) m?.Dispose();
        }
        _chunks.Clear();

        while (_meshesToDelete.TryDequeue(out var mesh))
            mesh.Dispose();

        _dirtySet.Clear();
        _pendingChunks.Clear();
    }

    private sealed class ChunkDistanceComparer : IComparer<Vector2i>
    {
        public Vector2i Center;

        public int Compare(Vector2i a, Vector2i b)
        {
            int dxA = a.X - Center.X;
            int dzA = a.Y - Center.Y;
            int dxB = b.X - Center.X;
            int dzB = b.Y - Center.Y;
            return (dxA * dxA + dzA * dzA).CompareTo(dxB * dxB + dzB * dzB);
        }
    }
}