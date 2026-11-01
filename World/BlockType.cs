using OpenTK.Mathematics;

namespace MinecraftClone.World;

public enum BlockType : byte
{
    Air = 0,
    Grass = 1,
    Dirt = 2,
    Stone = 3,
    Wood = 4,
    Leaves = 5,
    Sand = 6,
    Water = 7 // Теперь это базовая вода (source)
}

public static class BlockData
{
    public static Vector2 GetTileCoords(BlockType type, int d, int face)
    {
        float tileX = type switch
        {
            BlockType.Grass when d == 1 && face == 0 => 0, 
            BlockType.Grass when d == 1 && face == 1 => 2, 
            BlockType.Grass => 3, 
            BlockType.Dirt => 2,
            BlockType.Stone => 1,
            BlockType.Wood when d == 1 => 4, 
            BlockType.Wood => 5,             
            BlockType.Leaves => 6,
            BlockType.Sand => 7,
            BlockType.Water => 8,
            _ => 0
        };
        float tileY = 15; 
        return new Vector2(tileX, tileY);
    }

    public static bool IsTransparent(BlockType type) => type == BlockType.Water || type == BlockType.Leaves;
    public static bool IsSolid(BlockType type) => type != BlockType.Air && type != BlockType.Water;
    public static bool IsOpaque(BlockType type) => type != BlockType.Air && !IsTransparent(type);
    
    // НОВОЕ: Помощники для воды
    public static bool IsWater(BlockType type) => type == BlockType.Water;
}