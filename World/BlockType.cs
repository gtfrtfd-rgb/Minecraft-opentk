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
    public static Vector4 GetUV(BlockType type, int face)
    {
        return type switch
        {
            BlockType.Grass when face == 0 => AtlasTile(0, 15),
            BlockType.Grass when face == 1 => AtlasTile(2, 15),
            BlockType.Grass => AtlasTile(3, 15),
            BlockType.Dirt => AtlasTile(2, 15),
            BlockType.Stone => AtlasTile(1, 15),
            BlockType.Wood when face <= 1 => AtlasTile(4, 15),
            BlockType.Wood => AtlasTile(5, 15),
            BlockType.Leaves => AtlasTile(6, 15),
            BlockType.Sand => AtlasTile(7, 15),
            BlockType.Water => AtlasTile(8, 15),
            _ => AtlasTile(0, 15)
        };
    }

    private static Vector4 AtlasTile(int tileX, int tileY)
    {
        const int atlasSize = 16;
        float u0 = (float)tileX / atlasSize;
        float u1 = (float)(tileX + 1) / atlasSize;
        float v0 = (float)tileY / atlasSize;
        float v1 = (float)(tileY + 1) / atlasSize;
        return new Vector4(u0, v0, u1, v1);
    }

    // Является ли блок прозрачным (для разделения рендера)
    public static bool IsTransparent(BlockType type) => type == BlockType.Water || type == BlockType.Leaves;

    // Физика: сталкивается ли игрок
    public static bool IsSolid(BlockType type) => type != BlockType.Air && type != BlockType.Water;
    
    // Графика: скрывает ли блок грани соседей (Вода и Листва - не скрывают)
    public static bool IsOpaque(BlockType type) => type != BlockType.Air && !IsTransparent(type);
}