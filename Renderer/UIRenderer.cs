using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class UIRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _shader;

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
void main() {
    FragColor = texture(texture0, TexCoord) * colorMultiplier;
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

    public void DrawQuad(float x, float y, float width, float height, Vector4 uvRect, Vector4 color, int screenWidth, int screenHeight)
    {
        // Генерируем 6 вершин (2 треугольника) вместо индексов
        float[] vertices = {
            // Треугольник 1
            x, y, uvRect.X, uvRect.W,
            x, y + height, uvRect.X, uvRect.Y,
            x + width, y + height, uvRect.Z, uvRect.Y,
            // Треугольник 2
            x, y, uvRect.X, uvRect.W,
            x + width, y + height, uvRect.Z, uvRect.Y,
            x + width, y, uvRect.Z, uvRect.W
        };

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);

        GL.UseProgram(_shader);
        
        // Динамическая ортографическая проекция под размер окна
        var proj = Matrix4.CreateOrthographicOffCenter(0, screenWidth, screenHeight, 0, -1, 1); 
        GL.UniformMatrix4(GL.GetUniformLocation(_shader, "projection"), false, ref proj);
        
        GL.Uniform4(GL.GetUniformLocation(_shader, "colorMultiplier"), color);
        GL.Uniform1(GL.GetUniformLocation(_shader, "texture0"), 0);

        // Отрисовка без индексов
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shader);
    }
}