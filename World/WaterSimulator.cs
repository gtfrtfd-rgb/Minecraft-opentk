using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenTK.Mathematics;

namespace MinecraftClone.World;

public class WaterSimulator : IDisposable
{
    private readonly World _world;
    private readonly ConcurrentQueue<Vector3i> _dirtyWater = new();
    
    private Thread? _worker;
    private volatile bool _isRunning = true;
    private bool _disposed;

    public WaterSimulator(World world)
    {
        _world = world;
        _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "WaterSim", Priority = ThreadPriority.BelowNormal };
        _worker.Start();
    }

    public void AddWaterSource(int x, int y, int z) => _dirtyWater.Enqueue(new Vector3i(x, y, z));
    public void AddDirtyBlock(int x, int y, int z) => _dirtyWater.Enqueue(new Vector3i(x, y, z));

    private void WorkerLoop()
    {
        while (_isRunning)
        {
            int processedCount = 0;
            
            // Обрабатываем не более 200 блоков за один тик, чтобы не вешать CPU
            while (processedCount < 200 && _dirtyWater.TryDequeue(out var pos))
            {
                if (!_isRunning) break;
                ProcessWaterBlock(pos);
                processedCount++;
            }

            // Делаем паузу между тиками симуляции (20 тиков в секунду)
            Thread.Sleep(50); 
        }
    }

    private void ProcessWaterBlock(Vector3i pos)
    {
        var block = _world.GetBlock(pos.X, pos.Y, pos.Z);
        var level = _world.GetWaterLevel(pos.X, pos.Y, pos.Z);
        
        if (block != BlockType.Water) return;

        var belowBlock = _world.GetBlock(pos.X, pos.Y - 1, pos.Z);
        
        // Если под водой воздух — она падает вниз
        if (belowBlock == BlockType.Air)
        {
            _world.SetBlockDirectForSim(pos.X, pos.Y - 1, pos.Z, BlockType.Water, 0);
            _dirtyWater.Enqueue(new Vector3i(pos.X, pos.Y - 1, pos.Z));
        }
        else if (BlockData.IsSolid(belowBlock))
        {
            // Вода растекается в стороны, если под ней твердый блок
            if (level < 7)
            {
                sbyte newLevel = (sbyte)(level + 1);
                TrySpread(pos.X + 1, pos.Y, pos.Z, newLevel);
                TrySpread(pos.X - 1, pos.Y, pos.Z, newLevel);
                TrySpread(pos.X, pos.Y, pos.Z + 1, newLevel);
                TrySpread(pos.X, pos.Y, pos.Z - 1, newLevel);
            }
        }
    }

    private void TrySpread(int x, int y, int z, sbyte newLevel)
    {
        if (newLevel > 7) return;

        var targetBlock = _world.GetBlock(x, y, z);
        var targetLevel = _world.GetWaterLevel(x, y, z);

        if (targetBlock == BlockType.Air)
        {
            _world.SetBlockDirectForSim(x, y, z, BlockType.Water, newLevel);
            _dirtyWater.Enqueue(new Vector3i(x, y, z));
        }
        else if (targetBlock == BlockType.Water && targetLevel > newLevel)
        {
            _world.SetBlockDirectForSim(x, y, z, BlockType.Water, newLevel);
            _dirtyWater.Enqueue(new Vector3i(x, y, z));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _isRunning = false;
        _worker?.Join(2000);
    }
}