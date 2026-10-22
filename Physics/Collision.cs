using OpenTK.Mathematics;
using MinecraftClone.World;

namespace MinecraftClone.Physics;

public static class Collision
{
    public static bool IsColliding(World.World world, Vector3 min, Vector3 max)
    {
        for (int x = (int)MathF.Floor(min.X); x <= (int)MathF.Floor(max.X); x++)
        for (int y = (int)MathF.Floor(min.Y); y <= (int)MathF.Floor(max.Y); y++)
        for (int z = (int)MathF.Floor(min.Z); z <= (int)MathF.Floor(max.Z); z++)
        {
            if (BlockData.IsSolid(world.GetBlock(x, y, z)))
                return true;
        }
        return false;
    }

    public static Vector3 ResolveMovement(World.World world, Vector3 position, Vector3 velocity, float dt)
    {
        const float playerWidth = 0.6f;
        const float playerHeight = 1.8f;

        var min = position - new Vector3(playerWidth / 2, 0, playerWidth / 2);
        var max = position + new Vector3(playerWidth / 2, playerHeight, playerWidth / 2);

        // Пошаговое движение по каждой оси отдельно — простейший способ избежать «застревания» в углах
        var newPos = position;

        newPos.X += velocity.X * dt;
        if (IsColliding(world, newPos - new Vector3(playerWidth / 2, 0, playerWidth / 2),
            newPos + new Vector3(playerWidth / 2, playerHeight, playerWidth / 2)))
            newPos.X = position.X;

        newPos.Z += velocity.Z * dt;
        if (IsColliding(world, newPos - new Vector3(playerWidth / 2, 0, playerWidth / 2),
            newPos + new Vector3(playerWidth / 2, playerHeight, playerWidth / 2)))
            newPos.Z = position.Z;

        newPos.Y += velocity.Y * dt;
        if (IsColliding(world, newPos - new Vector3(playerWidth / 2, 0, playerWidth / 2),
            newPos + new Vector3(playerWidth / 2, playerHeight, playerWidth / 2)))
            newPos.Y = position.Y;

        return newPos;
    }
}