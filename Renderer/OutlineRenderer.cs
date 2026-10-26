using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class OutlineRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private int _shader;

    private int _locView;
    private int _locProjection;
    private int _locOffset;
    private int _locColor;

    private bool _disposed;

    public OutlineRenderer()
    {
        string vert = @"
#version 330 core
layout(location = 0) in vec3 aPos;
uniform mat4 view;
uniform mat4 projection;
uniform vec3 offset;
void main() {
    gl_Position = projection * view * vec4(aPos + offset, 1.0);
}";

        string frag = @"
#version 330 core
out vec4 FragColor;
uniform vec4 color;
void main() {
    FragColor = color;
}";

        int vs = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vs, vert);
        GL.CompileShader(vs);
        CheckCompile(vs, "Outline.VERTEX");

        int fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, frag);
        GL.CompileShader(fs);
        CheckCompile(fs, "Outline.FRAGMENT");

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
            throw new Exception($"Ошибка линковки шейдера обводки: {log}");
        }

        GL.DetachShader(_shader, vs);
        GL.DetachShader(_shader, fs);
        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        _locView = GL.GetUniformLocation(_shader, "view");
        _locProjection = GL.GetUniformLocation(_shader, "projection");
        _locOffset = GL.GetUniformLocation(_shader, "offset");
        _locColor = GL.GetUniformLocation(_shader, "color");

        // Отступ от граней блока
        float e = 0.002f;

        float[] vertices = {
            -e, -e, -e,
             1+e, -e, -e,
             1+e, 1+e, -e,
            -e, 1+e, -e,
            -e, -e, 1+e,
             1+e, -e, 1+e,
             1+e, 1+e, 1+e,
            -e, 1+e, 1+e
        };

        uint[] indices = {
            0, 1,  1, 5,  5, 4,  4, 0,
            3, 2,  2, 6,  6, 7,  7, 3,
            0, 3,  1, 2,  5, 6,  4, 7
        };

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        _ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);
    }

    public void Draw(Vector3 blockPos, Matrix4 view, Matrix4 projection)
    {
        GL.UseProgram(_shader);

        GL.UniformMatrix4(_locView, false, ref view);
        GL.UniformMatrix4(_locProjection, false, ref projection);
        GL.Uniform3(_locOffset, blockPos);
        GL.Uniform4(_locColor, 0f, 0f, 0f, 0.5f);

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Lines, 24, DrawElementsType.UnsignedInt, 0);
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