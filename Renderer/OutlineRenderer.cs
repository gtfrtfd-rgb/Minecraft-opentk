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

        int fs = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fs, frag);
        GL.CompileShader(fs);

        _shader = GL.CreateProgram();
        GL.AttachShader(_shader, vs);
        GL.AttachShader(_shader, fs);
        GL.LinkProgram(_shader);

        GL.DeleteShader(vs);
        GL.DeleteShader(fs);

        // Отступ от граней блока
        float e = 0.002f; 
        
        // 8 вершин куба
        float[] vertices = {
            -e, -e, -e, // 0
             1+e, -e, -e, // 1
             1+e, 1+e, -e, // 2
            -e, 1+e, -e, // 3
            -e, -e, 1+e, // 4
             1+e, -e, 1+e, // 5
             1+e, 1+e, 1+e, // 6
            -e, 1+e, 1+e  // 7
        };

        // Индексы для 12 ребер (линий)
        uint[] indices = {
            // Нижняя грань
            0, 1,  1, 5,  5, 4,  4, 0,
            // Верхняя грань
            3, 2,  2, 6,  6, 7,  7, 3,
            // Вертикальные ребра
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
        
        GL.UniformMatrix4(GL.GetUniformLocation(_shader, "view"), false, ref view);
        GL.UniformMatrix4(GL.GetUniformLocation(_shader, "projection"), false, ref projection);
        GL.Uniform3(GL.GetUniformLocation(_shader, "offset"), blockPos);
        
        // Черный цвет с прозрачностью 50%
        GL.Uniform4(GL.GetUniformLocation(_shader, "color"), 0f, 0f, 0f, 0.5f);

        GL.BindVertexArray(_vao);
        
        // Рисуем линии по индексам
        GL.DrawElements(PrimitiveType.Lines, 24, DrawElementsType.UnsignedInt, 0);
        
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shader);
    }
}