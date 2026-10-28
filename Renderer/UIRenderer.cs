using System;
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

    private const int MaxQuads = 4096;
    private const int MaxVertices = MaxQuads * 4;
    private const int MaxIndices = MaxQuads * 6;
    private const int VertexStrideFloats = 9;

    private readonly float[] _vertices = new float[MaxVertices * VertexStrideFloats];
    private readonly uint[] _indices = new uint[MaxIndices];
    private int _quadCount = 0;

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

    private void AddQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color, bool useTexture)
    {
        if (_quadCount >= MaxQuads) return;

        float texFlag = useTexture ? 1.0f : 0.0f;
        int vOffset = _quadCount * 4 * VertexStrideFloats;
        int iOffset = _quadCount * 6;
        int baseVertex = _quadCount * 4;

        // Bottom-Left
        _vertices[vOffset + 0] = x; _vertices[vOffset + 1] = y;
        _vertices[vOffset + 2] = uvRect.X; _vertices[vOffset + 3] = uvRect.W;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Top-Left
        vOffset += VertexStrideFloats;
        _vertices[vOffset + 0] = x; _vertices[vOffset + 1] = y + height;
        _vertices[vOffset + 2] = uvRect.X; _vertices[vOffset + 3] = uvRect.Y;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Top-Right
        vOffset += VertexStrideFloats;
        _vertices[vOffset + 0] = x + width; _vertices[vOffset + 1] = y + height;
        _vertices[vOffset + 2] = uvRect.Z; _vertices[vOffset + 3] = uvRect.Y;
        _vertices[vOffset + 4] = color.X; _vertices[vOffset + 5] = color.Y; _vertices[vOffset + 6] = color.Z; _vertices[vOffset + 7] = color.W;
        _vertices[vOffset + 8] = texFlag;

        // Bottom-Right
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
        // ORPHANING: сообщаем драйверу, что старое содержимое буфера нам больше не нужно.
        // Драйвер может выделить новый блок памяти, пока GPU дорисовывает кадр из старого.
        // Без этого SubData заставляет драйвер ждать окончания работы GPU -> фризы.
        GL.BufferData(BufferTarget.ArrayBuffer, MaxVertices * VertexStrideFloats * sizeof(float), IntPtr.Zero, BufferUsageHint.StreamDraw);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, _quadCount * 4 * VertexStrideFloats * sizeof(float), _vertices);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, MaxIndices * sizeof(uint), IntPtr.Zero, BufferUsageHint.StreamDraw);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero, _quadCount * 6 * sizeof(uint), _indices);

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