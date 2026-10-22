using OpenTK.Graphics.OpenGL4;

namespace MinecraftClone.Renderer;

public class TextureAtlas
{
    public int Handle { get; }

    public TextureAtlas(int tilesX, int tilesY)
    {
        const int tileSize = 16;
        int width = tilesX * tileSize;
        int height = tilesY * tileSize;
        var pixels = new byte[width * height * 4];

        GenerateGrassTop(pixels, width, 0, 15);
        GenerateDirt(pixels, width, 2, 15);
        GenerateStone(pixels, width, 1, 15);
        GenerateWoodTop(pixels, width, 4, 15);
        GenerateWoodSide(pixels, width, 5, 15);
        GenerateLeaves(pixels, width, 6, 15);
        GenerateSand(pixels, width, 7, 15);
        GenerateWater(pixels, width, 8, 15);
        GenerateGrassSide(pixels, width, 3, 15);

        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
            width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static void SetPixel(byte[] pixels, int atlasWidth, int tileX, int tileY, int px, int py, byte r, byte g, byte b, byte a = 255)
    {
        int x = tileX * 16 + px;
        int y = tileY * 16 + (15 - py); // Инверсия Y для OpenGL
        int index = (y * atlasWidth + x) * 4;
        pixels[index] = r;
        pixels[index + 1] = g;
        pixels[index + 2] = b;
        pixels[index + 3] = a;
    }

    private static void GenerateGrassTop(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(100 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(90, 140);
            SetPixel(p, w, tx, ty, x, y, (byte)(v * 0.6f), v, (byte)(v * 0.3f));
        }
    }

    private static void GenerateDirt(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(200 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(100, 150);
            SetPixel(p, w, tx, ty, x, y, v, (byte)(v * 0.75f), (byte)(v * 0.5f));
        }
        for (int i = 0; i < 12; i++)
        {
            int px = rnd.Next(16), py = rnd.Next(16);
            SetPixel(p, w, tx, ty, px, py, 80, 60, 40);
        }
    }

    private static void GenerateStone(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(300 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(120, 170);
            SetPixel(p, w, tx, ty, x, y, v, v, v);
        }
    }

    private static void GenerateWoodTop(byte[] p, int w, int tx, int ty)
    {
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            int dist = Math.Abs(x - 8) + Math.Abs(y - 8);
            byte v = (byte)(dist < 3 ? 90 : dist < 6 ? 110 : 130);
            SetPixel(p, w, tx, ty, x, y, v, (byte)(v * 0.8f), (byte)(v * 0.5f));
        }
    }

    private static void GenerateWoodSide(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(400 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)(rnd.Next(90, 130) + (x % 4 == 0 ? 15 : 0));
            SetPixel(p, w, tx, ty, x, y, v, (byte)(v * 0.75f), (byte)(v * 0.4f));
        }
    }

    private static void GenerateLeaves(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(500 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            bool transparent = rnd.NextDouble() < 0.15;
            if (transparent) { SetPixel(p, w, tx, ty, x, y, 0, 0, 0, 0); continue; }
            byte v = (byte)rnd.Next(40, 90);
            SetPixel(p, w, tx, ty, x, y, (byte)(v * 0.3f), v, (byte)(v * 0.2f));
        }
    }

    private static void GenerateSand(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(600 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(190, 230);
            SetPixel(p, w, tx, ty, x, y, v, (byte)(v * 0.9f), (byte)(v * 0.6f));
        }
    }

    private static void GenerateWater(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(700 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(50, 100);
            SetPixel(p, w, tx, ty, x, y, 40, 100, (byte)(150 + v));
        }
    }

    private static void GenerateGrassSide(byte[] p, int w, int tx, int ty)
    {
        GenerateDirt(p, w, tx, ty);
        var rnd = new Random(800 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        {
            int greenHeight = 3 + rnd.Next(2);
            for (int y = 0; y < greenHeight; y++)
            {
                byte v = (byte)rnd.Next(80, 130);
                SetPixel(p, w, tx, ty, x, y, (byte)(v * 0.5f), v, (byte)(v * 0.25f));
            }
        }
    }

    public void Bind(TextureUnit unit)
    {
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }
}