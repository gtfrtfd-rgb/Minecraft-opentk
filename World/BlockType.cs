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
    Water = 7
}

public static class BlockData
{
    // d = ось (0-X, 1-Y, 2-Z), face = направление (0-положительное, 1-отрицательное)
    public static Vector2 GetTileCoords(BlockType type, int d, int face)
    {
        float tileX = type switch
        {
            // Верх (Y+, face=0) и низ (Y-, face=1) для травы
            BlockType.Grass when d == 1 && face == 0 => 0, 
            BlockType.Grass when d == 1 && face == 1 => 2, 
            BlockType.Grass => 3, // Бока травы

            BlockType.Dirt => 2,
            BlockType.Stone => 1,
            
            // Верх и низ дерева (спил), бока - кора
            BlockType.Wood when d == 1 => 4, 
            BlockType.Wood => 5,             
            
            BlockType.Leaves => 6,
            BlockType.Sand => 7,
            BlockType.Water => 8,
            _ => 0
        };
        float tileY = 15; // Все наши текстуры в 15-й строке
        return new Vector2(tileX, tileY);
    }

    public static bool IsTransparent(BlockType type) => type == BlockType.Water || type == BlockType.Leaves;
    public static bool IsSolid(BlockType type) => type != BlockType.Air && type != BlockType.Water;
    public static bool IsOpaque(BlockType type) => type != BlockType.Air && !IsTransparent(type);
}