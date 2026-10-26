using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class UIRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _shader;

    private int _locProjection;
    private int _locColor;
    private int _locTexture;
    private int _locUseTexture;

    private Matrix4 _projection = Matrix4.Identity;
    private int _cachedWidth = -1;
    private int _cachedHeight = -1;

    private readonly float[] _quadBuffer = new float[24];
    private bool _disposed;

    public UIRenderer()
    {
        string vert = @"
#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 TexCoord;
uniform mat4 projection;
void main() {
    gl_Position = projection * vec4(aPos, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

        string frag = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;
uniform sampler2D texture0;
uniform vec4 colorMultiplier;
uniform bool useTexture;
void main() {
    if (useTexture) {
        FragColor = texture(texture0, TexCoord) * colorMultiplier;
    } else {
        FragColor = colorMultiplier;
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
        _locColor = GL.GetUniformLocation(_shader, "colorMultiplier");
        _locTexture = GL.GetUniformLocation(_shader, "texture0");
        _locUseTexture = GL.GetUniformLocation(_shader, "useTexture");

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        GL.BindVertexArray(0);
    }

    /// <summary>
    /// Обновляет ортопроекцию под текущий размер окна. Дешёвый no-op при совпадении размеров.
    /// </summary>
    public void SetScreenSize(int width, int height)
    {
        if (width == _cachedWidth && height == _cachedHeight) return;
        _cachedWidth = width;
        _cachedHeight = height;
        _projection = Matrix4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
    }

    public void DrawQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color)
        => RenderQuad(x, y, width, height, uvRect, color, true);

    public void DrawSolidQuad(float x, float y, float width, float height, Vector4 color)
        => RenderQuad(x, y, width, height, Vector4.Zero, color, false);

    private void RenderQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color, bool useTexture)
    {
        int i = 0;
        _quadBuffer[i++] = x;                 _quadBuffer[i++] = y;                 _quadBuffer[i++] = uvRect.X; _quadBuffer[i++] = uvRect.W;
        _quadBuffer[i++] = x;                 _quadBuffer[i++] = y + height;        _quadBuffer[i++] = uvRect.X; _quadBuffer[i++] = uvRect.Y;
        _quadBuffer[i++] = x + width;         _quadBuffer[i++] = y + height;        _quadBuffer[i++] = uvRect.Z; _quadBuffer[i++] = uvRect.Y;
        _quadBuffer[i++] = x;                 _quadBuffer[i++] = y;                 _quadBuffer[i++] = uvRect.X; _quadBuffer[i++] = uvRect.W;
        _quadBuffer[i++] = x + width;         _quadBuffer[i++] = y + height;        _quadBuffer[i++] = uvRect.Z; _quadBuffer[i++] = uvRect.Y;
        _quadBuffer[i++] = x + width;         _quadBuffer[i++] = y;                 _quadBuffer[i++] = uvRect.Z; _quadBuffer[i++] = uvRect.W;

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _quadBuffer.Length * sizeof(float), _quadBuffer, BufferUsageHint.StreamDraw);

        GL.UseProgram(_shader);
        GL.UniformMatrix4(_locProjection, false, ref _projection);
        GL.Uniform4(_locColor, color);
        GL.Uniform1(_locTexture, 0);
        GL.Uniform1(_locUseTexture, useTexture ? 1 : 0);

        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shader);
    }
}