using System;
using OpenTK.Graphics.OpenGL4;

namespace MinecraftClone.Renderer;

public class TextureAtlas : IDisposable
{
    public int Handle { get; }
    private bool _disposed;

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
        int y = tileY * 16 + (15 - py);
        int index = (y * atlasWidth + x) * 4;
        pixels[index] = r;
        pixels[index + 1] = g;
        pixels[index + 2] = b;
        pixels[index + 3] = a;
    }

    private static void GenerateLeaves(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(500 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            if (rnd.NextDouble() < 0.10) { SetPixel(p, w, tx, ty, x, y, 0, 0, 0, 0); continue; }
            double cluster = Math.Sin(x * 0.8) * Math.Cos(y * 0.8) + (rnd.NextDouble() * 0.4);
            byte r, g, b;
            if (cluster > 0.2) { g = (byte)rnd.Next(110, 150); r = (byte)(g * 0.35f); b = (byte)(g * 0.15f); }
            else { g = (byte)rnd.Next(60, 95); r = (byte)(g * 0.3f); b = (byte)(g * 0.15f); }
            int noise = rnd.Next(-5, 5);
            r = (byte)Math.Clamp(r + noise, 0, 255); g = (byte)Math.Clamp(g + noise, 0, 255); b = (byte)Math.Clamp(b + noise, 0, 255);
            SetPixel(p, w, tx, ty, x, y, r, g, b);
        }
    }

    private static void GenerateWoodTop(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(400 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            float dx = x - 7.5f; float dy = y - 7.5f;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float noise = (float)(rnd.NextDouble() * 0.8 - 0.4);
            float noisyDist = dist + noise;
            byte r, g, b;
            if (dist > 6.5f) { r = 85; g = 60; b = 40; int n = rnd.Next(-10, 10); r = (byte)Math.Clamp(r + n, 0, 255); g = (byte)Math.Clamp(g + n, 0, 255); b = (byte)Math.Clamp(b + n, 0, 255); }
            else { int ring = (int)(noisyDist * 1.2f); if (ring % 2 == 0) { r = 175; g = 135; b = 85; } else { r = 140; g = 100; b = 60; } int n = rnd.Next(-5, 5); r = (byte)Math.Clamp(r + n, 0, 255); g = (byte)Math.Clamp(g + n, 0, 255); b = (byte)Math.Clamp(b + n, 0, 255); }
            SetPixel(p, w, tx, ty, x, y, r, g, b);
        }
    }

    private static void GenerateWoodSide(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(400 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        {
            byte baseV = (byte)rnd.Next(90, 120);
            bool isDarkStripe = rnd.NextDouble() < 0.3;
            for (int y = 0; y < 16; y++)
            {
                int noise = rnd.Next(-15, 15);
                byte r = (byte)Math.Clamp(baseV + noise, 60, 150);
                byte g = (byte)(r * 0.7f); byte b = (byte)(r * 0.4f);
                if (isDarkStripe && rnd.NextDouble() < 0.8) { r = (byte)(r * 0.6f); g = (byte)(g * 0.6f); b = (byte)(b * 0.6f); }
                SetPixel(p, w, tx, ty, x, y, r, g, b);
            }
        }
    }

    private static void GenerateSand(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(600 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(215, 245); byte g = (byte)(v - rnd.Next(5, 15)); byte b = (byte)(v - rnd.Next(30, 50));
            if (rnd.NextDouble() < 0.1) { v = 255; g = 245; b = 210; }
            SetPixel(p, w, tx, ty, x, y, v, g, b);
        }
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
        for (int i = 0; i < 12; i++) { int px = rnd.Next(16), py = rnd.Next(16); SetPixel(p, w, tx, ty, px, py, 80, 60, 40); }
    }

    private static void GenerateStone(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(300 + tx * 31 + ty);
        byte baseColor = 128;
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            int noise = rnd.Next(-10, 10);
            byte v = (byte)Math.Clamp(baseColor + noise, 100, 150);
            if (rnd.NextDouble() < 0.15) v = (byte)(v - rnd.Next(15, 30));
            if (rnd.NextDouble() < 0.1) v = (byte)(v + rnd.Next(15, 25));
            if ((x * 3 + y * 7) % 11 == 0) v = (byte)(v - 20);
            SetPixel(p, w, tx, ty, x, y, v, v, v);
        }
    }

    private static void GenerateWater(byte[] p, int w, int tx, int ty)
    {
        var rnd = new Random(700 + tx * 31 + ty);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        {
            byte v = (byte)rnd.Next(50, 100);
            SetPixel(p, w, tx, ty, x, y, 40, 100, (byte)(150 + v), 160);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GL.DeleteTexture(Handle);
    }
}