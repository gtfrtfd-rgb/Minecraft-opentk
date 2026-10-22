using OpenTK.Mathematics;
using MinecraftClone.World;

namespace MinecraftClone.Physics;

public static class Collision
{
    // ВАЖНО: добавлено ref перед Vector3 velocity
    public static Vector3 Move(World.World world, Vector3 pos, Vector3 size, ref Vector3 velocity, float dt, out bool onGround)
    {
        onGround = false;
        Vector3 newPos = pos;

        // 1. Движение по X
        newPos.X += velocity.X * dt;
        if (CheckCollision(world, newPos, size))
        {
            newPos.X = pos.X;
            velocity.X = 0;
        }

        // 2. Движение по Z
        newPos.Z += velocity.Z * dt;
        if (CheckCollision(world, newPos, size))
        {
            newPos.Z = pos.Z;
            velocity.Z = 0;
        }

        // 3. Движение по Y (гравитация/прыжок)
        newPos.Y += velocity.Y * dt;
        if (CheckCollision(world, newPos, size))
        {
            if (velocity.Y < 0) // Если падаем вниз
            {
                onGround = true;
                newPos.Y = (float)Math.Floor(newPos.Y) + 1.0f;
            }
            else // Если бьемся головой об потолок
            {
                newPos.Y = pos.Y;
            }
            // Теперь это обнуление сохраняется в PlayerController
            velocity.Y = 0; 
        }

        return newPos;
    }

    private static bool CheckCollision(World.World world, Vector3 pos, Vector3 size)
    {
        float halfWidth = size.X / 2f;
        
        float minX = pos.X - halfWidth;
        float maxX = pos.X + halfWidth;
        float minY = pos.Y;
        float maxY = pos.Y + size.Y;
        float minZ = pos.Z - halfWidth;
        float maxZ = pos.Z + halfWidth;

        float epsilon = 0.001f;

        int startX = (int)MathF.Floor(minX + epsilon);
        int endX   = (int)MathF.Floor(maxX - epsilon);
        int startY = (int)MathF.Floor(minY + epsilon);
        int endY   = (int)MathF.Floor(maxY - epsilon);
        int startZ = (int)MathF.Floor(minZ + epsilon);
        int endZ   = (int)MathF.Floor(maxZ - epsilon);

        for (int x = startX; x <= endX; x++)
        for (int y = startY; y <= endY; y++)
        for (int z = startZ; z <= endZ; z++)
        {
            if (BlockData.IsSolid(world.GetBlock(x, y, z)))
                return true;
        }
        return false;
    }
}