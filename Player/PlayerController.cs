using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using MinecraftClone.World;

namespace MinecraftClone.Player;

public class PlayerController
{
    private readonly Camera _camera;
    private readonly World.World _world;
    private const float Reach = 5f;

    public PlayerController(Camera camera, World.World world)
    {
        _camera = camera;
        _world = world;
    }

    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        // В прототипе движение камеры уже обрабатывается в Game.cs,
        // здесь можно добавить гравитацию и коллизии с землёй.
    }

    public void BreakBlock()
    {
        if (Raycast(out var pos, out _))
            _world.SetBlock(pos.X, pos.Y, pos.Z, BlockType.Air);
    }

    public void PlaceBlock()
    {
        if (Raycast(out var pos, out var normal))
        {
            var placePos = pos + normal;
            if (_world.GetBlock(placePos.X, placePos.Y, placePos.Z) == BlockType.Air)
                _world.SetBlock(placePos.X, placePos.Y, placePos.Z, BlockType.Stone);
        }
    }

    private bool Raycast(out Vector3i blockPos, out Vector3i normal)
    {
        blockPos = Vector3i.Zero;
        normal = Vector3i.Zero;

        Vector3 origin = _camera.Position;
        Vector3 dir = _camera.GetFrontVector();

        // DDA по воксельной сетке (упрощённая версия алгоритма Amanatides & Woo)
        var pos = new Vector3i(
            (int)MathF.Floor(origin.X),
            (int)MathF.Floor(origin.Y),
            (int)MathF.Floor(origin.Z));

        var step = new Vector3i(
            dir.X > 0 ? 1 : -1,
            dir.Y > 0 ? 1 : -1,
            dir.Z > 0 ? 1 : -1);

        var tMax = new Vector3(
            ((dir.X > 0 ? pos.X + 1 - origin.X : origin.X - pos.X) / MathF.Abs(dir.X)),
            ((dir.Y > 0 ? pos.Y + 1 - origin.Y : origin.Y - pos.Y) / MathF.Abs(dir.Y)),
            ((dir.Z > 0 ? pos.Z + 1 - origin.Z : origin.Z - pos.Z) / MathF.Abs(dir.Z)));

        var tDelta = new Vector3(
            1f / MathF.Abs(dir.X),
            1f / MathF.Abs(dir.Y),
            1f / MathF.Abs(dir.Z));

        float t = 0f;

        while (t < Reach)
        {
            var block = _world.GetBlock(pos.X, pos.Y, pos.Z);
            if (block != BlockType.Air && BlockData.IsSolid(block))
            {
                blockPos = pos;
                return true;
            }

            if (tMax.X < tMax.Y && tMax.X < tMax.Z)
            {
                pos.X += step.X;
                t = tMax.X;
                tMax.X += tDelta.X;
                normal = new Vector3i(-step.X, 0, 0);
            }
            else if (tMax.Y < tMax.Z)
            {
                pos.Y += step.Y;
                t = tMax.Y;
                tMax.Y += tDelta.Y;
                normal = new Vector3i(0, -step.Y, 0);
            }
            else
            {
                pos.Z += step.Z;
                t = tMax.Z;
                tMax.Z += tDelta.Z;
                normal = new Vector3i(0, 0, -step.Z);
            }
        }

        return false;
    }
}