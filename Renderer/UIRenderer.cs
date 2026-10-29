using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class UIRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _shader;

    private int _locProjection;
    private int _locTexture;

    private Matrix4 _projection = Matrix4.Identity;
    private int _cachedWidth = -1;
    private int _cachedHeight = -1;

    private const int MaxQuads = 8192;
    private const int MaxVertices = MaxQuads * 4;
    private const int MaxIndices = MaxQuads * 6;
    private const int VertexStrideFloats = 9;

    private readonly float[] _vertices = new float[MaxVertices * VertexStrideFloats];
    private readonly uint[] _indices = new uint[MaxIndices];
    private int _quadCount = 0;

    private static readonly Dictionary<char, int[]> Font = new()
    {
        {'A', new[]{0b01110, 0b10001, 0b11111, 0b10001, 0b10001}},
        {'B', new[]{0b11110, 0b10001, 0b11110, 0b10001, 0b11110}},
        {'C', new[]{0b01111, 0b10000, 0b10000, 0b10000, 0b01111}},
        {'D', new[]{0b11110, 0b10001, 0b10001, 0b10001, 0b11110}},
        {'E', new[]{0b11111, 0b10000, 0b11110, 0b10000, 0b11111}},
        {'F', new[]{0b11111, 0b10000, 0b11110, 0b10000, 0b10000}},
        {'G', new[]{0b01111, 0b10000, 0b10111, 0b10001, 0b01111}},
        {'H', new[]{0b10001, 0b10001, 0b11111, 0b10001, 0b10001}},
        {'I', new[]{0b11111, 0b00100, 0b00100, 0b00100, 0b11111}},
        {'J', new[]{0b00111, 0b00010, 0b00010, 0b10010, 0b01100}},
        {'K', new[]{0b10001, 0b10010, 0b11100, 0b10010, 0b10001}},
        {'L', new[]{0b10000, 0b10000, 0b10000, 0b10000, 0b11111}},
        {'M', new[]{0b10001, 0b11011, 0b10101, 0b10001, 0b10001}},
        {'N', new[]{0b10001, 0b11001, 0b10101, 0b10011, 0b10001}},
        {'O', new[]{0b01110, 0b10001, 0b10001, 0b10001, 0b01110}},
        {'P', new[]{0b11110, 0b10001, 0b11110, 0b10000, 0b10000}},
        {'Q', new[]{0b01110, 0b10001, 0b10101, 0b10010, 0b01101}},
        {'R', new[]{0b11110, 0b10001, 0b11110, 0b10010, 0b10001}},
        {'S', new[]{0b01111, 0b10000, 0b01110, 0b00001, 0b11110}},
        {'T', new[]{0b11111, 0b00100, 0b00100, 0b00100, 0b00100}},
        {'U', new[]{0b10001, 0b10001, 0b10001, 0b10001, 0b01110}},
        {'V', new[]{0b10001, 0b10001, 0b10001, 0b01010, 0b00100}},
        {'W', new[]{0b10001, 0b10001, 0b10101, 0b11011, 0b10001}},
        {'X', new[]{0b10001, 0b01010, 0b00100, 0b01010, 0b10001}},
        {'Y', new[]{0b10001, 0b01010, 0b00100, 0b00100, 0b00100}},
        {'Z', new[]{0b11111, 0b00010, 0b00100, 0b01000, 0b11111}},
        {'0', new[]{0b01110, 0b10011, 0b10101, 0b11001, 0b01110}},
        {'1', new[]{0b00100, 0b01100, 0b00100, 0b00100, 0b01110}},
        {'2', new[]{0b01110, 0b10001, 0b00110, 0b01000, 0b11111}},
        {'3', new[]{0b11110, 0b00001, 0b00110, 0b00001, 0b11110}},
        {'4', new[]{0b00010, 0b00110, 0b01010, 0b11111, 0b00010}},
        {'5', new[]{0b11111, 0b10000, 0b11110, 0b00001, 0b11110}},
        {'6', new[]{0b01110, 0b10000, 0b11110, 0b10001, 0b01110}},
        {'7', new[]{0b11111, 0b00001, 0b00010, 0b00100, 0b01000}},
        {'8', new[]{0b01110, 0b10001, 0b01110, 0b10001, 0b01110}},
        {'9', new[]{0b01110, 0b10001, 0b01111, 0b00001, 0b01110}},
        {':', new[]{0b00000, 0b00100, 0b00000, 0b00100, 0b00000}},
        {'-', new[]{0b00000, 0b00000, 0b11111, 0b00000, 0b00000}},
        {'.', new[]{0b00000, 0b00000, 0b00000, 0b00000, 0b00100}},
        {' ', new[]{0b00000, 0b00000, 0b00000, 0b00000, 0b00000}}
    };

    private bool _disposed;

    public UIRenderer()
    {
        string vert = @"
#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in vec4 aColor;
layout(location = 3) in float aUseTexture;
out vec2 TexCoord;
out vec4 Color;
out float UseTexture;
uniform mat4 projection;
void main() {
    gl_Position = projection * vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
    Color = aColor;
    UseTexture = aUseTexture;
}";

        string frag = @"
#version 330 core
in vec2 TexCoord;
in vec4 Color;
in float UseTexture;
out vec4 FragColor;
uniform sampler2D texture0;
void main() {
    if (UseTexture > 0.5) {
        FragColor = texture(texture0, TexCoord) * Color;
    } else {
        FragColor = Color;
    }
}";

        int vs = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vs, vert);
        GL.CompileShader(vs);
        CheckCompile(vs, "UI.VERTEX");

        int fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, frag);
        GL.CompileShader(fs);
        CheckCompile(fs, "UI.FRAGMENT");

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, vs);
        GL.AttachShader(_shader, fs);
        GL.LinkProgram(_shader);

        GL.GetProgram(_shader, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(_shader);
            GL.DeleteShader(vs);
            GL.DeleteShader(fs);
            GL.DeleteProgram(_shader);
            throw new Exception($"Ошибка линковки UI-шейдера: {log}");
        }

        GL.DetachShader(_shader, vs);
        GL.DetachShader(_shader, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        _locProjection = GL.GetUniformLocation(_shader, "projection");
        _locTexture = GL.GetUniformLocation(_shader, "texture0");

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, MaxVertices * VertexStrideFloats * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);

        _ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, MaxIndices * sizeof(uint), IntPtr.Zero, BufferUsageHint.StreamDraw);

        int stride = VertexStrideFloats * sizeof(float);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, 4 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
        GL.EnableVertexAttribArray(3);

        GL.BindVertexArray(0);
    }

    public void SetScreenSize(int width, int height)
    {
        if (width == _cachedWidth && height == _cachedHeight) return;
        _cachedWidth = width;
        _cachedHeight = height;
        _projection = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
    }

    public void Begin() => _quadCount = 0;

    public void DrawQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color)
        => AddQuad(x, y, width, height, uvRect, color, true);

    public void DrawSolidQuad(float x, float y, float width, float height, Vector4 color)
        => AddQuad(x, y, width, height, Vector4.Zero, color, false);

    public void DrawText(string text, float x, float y, float scale, Vector4 color)
    {
        float cursorX = x;
        foreach (char c in text)
        {
            if (Font.TryGetValue(c, out var mask))
            {
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        if ((mask[row] & (1 << (4 - col))) != 0)
                        {
                            AddQuad(cursorX + col * scale, y + row * scale, scale, scale, Vector4.Zero, color, false);
                        }
                    }
                }
            }
            cursorX += 6 * scale;
        }
    }

    public void DrawButton(float x, float y, float w, float h, string text, bool isHovered)
    {
        Vector4 bgColor = isHovered ? new Vector4(0.3f, 0.3f, 0.3f, 0.9f) : new Vector4(0.2f, 0.2f, 0.2f, 0.8f);
        DrawSolidQuad(x, y, w, h, bgColor);

        // Базовый масштаб 4f. Если текст длинный — уменьшаем его, чтобы он влез в кнопку.
        float scale = 4f;
        float textWidth = text.Length * 6 * scale;
        float maxWidth = w - 10f; // 5 пикселей отступа с каждой стороны

        if (textWidth > maxWidth)
        {
            scale = maxWidth / (text.Length * 6f);
            textWidth = text.Length * 6 * scale;
        }

        float textHeight = 5 * scale;
        float textX = x + (w - textWidth) / 2f;
        float textY = y + (h - textHeight) / 2f;

        DrawText(text, textX, textY, scale, Vector4.One);
    }

    public void DrawSlider(float x, float y, float w, float h, float value, float min, float max, bool isHovered)
    {
        DrawSolidQuad(x, y, w, h, new Vector4(0.2f, 0.2f, 0.2f, 0.8f));

        float pct = (value - min) / (max - min);
        DrawSolidQuad(x, y, w * pct, h, new Vector4(0.4f, 0.6f, 0.4f, 1f));

        float handleWidth = 8f;
        float handleX = x + w * pct - handleWidth / 2f;
        Vector4 handleColor = isHovered ? new Vector4(1f, 1f, 1f, 1f) : new Vector4(0.8f, 0.8f, 0.8f, 1f);
        DrawSolidQuad(handleX, y - 4, handleWidth, h + 8, handleColor);
    }

    public void DrawCheckbox(float x, float y, float size, string text, bool isChecked, bool isHovered)
    {
        Vector4 bgColor = isHovered ? new Vector4(0.3f, 0.3f, 0.3f, 0.9f) : new Vector4(0.2f, 0.2f, 0.2f, 0.8f);
        DrawSolidQuad(x, y, size, size, bgColor);

        if (isChecked)
        {
            DrawSolidQuad(x + 4, y + 4, size - 8, size - 8, new Vector4(0.4f, 0.8f, 0.4f, 1f));
        }

        DrawText(text, x + size + 10, y + (size - 20f) / 2f, 4f, Vector4.One);
    }

    private void AddQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color, bool useTexture)
    {
        if (_quadCount >= MaxQuads) return;

        float texFlag = useTexture ? 1.0f : 0.0f;
        int vOffset = _quadCount * 4 * VertexStrideFloats;
        int iOffset = _quadCount * 6;
        int baseVertex = _quadCount * 4;

        // Top-Left
        _vertices[vOffset + 0] = x; _vertices[vOffset + 1] = y;
        _vertices[vOffset + 2] = uvRect.X; _vertices[vOffset + 3] = uvRect.W;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Bottom-Left
        vOffset += VertexStrideFloats;
        _vertices[vOffset + 0] = x; _vertices[vOffset + 1] = y + height;
        _vertices[vOffset + 2] = uvRect.X; _vertices[vOffset + 3] = uvRect.Y;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Bottom-Right
        vOffset += VertexStrideFloats;
        _vertices[vOffset + 0] = x + width; _vertices[vOffset + 1] = y + height;
        _vertices[vOffset + 2] = uvRect.Z; _vertices[vOffset + 3] = uvRect.Y;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Top-Right
        vOffset += VertexStrideFloats;
        _vertices[vOffset + 0] = x + width; _vertices[vOffset + 1] = y;
        _vertices[vOffset + 2] = uvRect.Z; _vertices[vOffset + 3] = uvRect.W;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        _indices[iOffset + 0] = (uint)(baseVertex + 0);
        _indices[iOffset + 1] = (uint)(baseVertex + 1);
        _indices[iOffset + 2] = (uint)(baseVertex + 2);
        _indices[iOffset + 3] = (uint)(baseVertex + 0);
        _indices[iOffset + 4] = (uint)(baseVertex + 2);
        _indices[iOffset + 5] = (uint)(baseVertex + 3);

        _quadCount++;
    }

    public void End()
    {
        if (_quadCount == 0) return;

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _quadCount * 4 * VertexStrideFloats * sizeof(float), _vertices, BufferUsageHint.StreamDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _quadCount * 6 * sizeof(uint), _indices, BufferUsageHint.StreamDraw);

        GL.UseProgram(_shader);
        GL.UniformMatrix4(_locProjection, false, ref _projection);
        GL.Uniform1(_locTexture, 0);

        GL.DrawElements(PrimitiveType.Triangles, _quadCount * 6, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    private static void CheckCompile(int shader, string type)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new Exception($"Ошибка компиляции {type} шейдера: {log}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shader);
    }
}