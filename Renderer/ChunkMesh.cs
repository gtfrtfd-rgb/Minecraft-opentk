using System;
using OpenTK.Graphics.OpenGL4;

namespace MinecraftClone.Renderer;

public class ChunkMesh : IDisposable
{
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;
    private readonly int _indexCount;

    public ChunkMesh(float[] vertices, int vertexFloatCount, uint[] indices, int indexCount)
    {
        _indexCount = indexCount;
        int vertexBytes = vertexFloatCount * sizeof(float);
        int indexBytes = indexCount * sizeof(uint);

        _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);

        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        
        // Используем unsafe-указатели, чтобы загрузить РОВНО столько байт, 
        // сколько нужно, игнорируя лишний "мусор" из ArrayPool.
        unsafe
        {
            fixed (float* v = vertices)
            {
                GL.BufferData(BufferTarget.ArrayBuffer, vertexBytes, (IntPtr)v, BufferUsageHint.StaticDraw);
            }
        }

        _ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        
        unsafe
        {
            fixed (uint* i = indices)
            {
                GL.BufferData(BufferTarget.ElementArrayBuffer, indexBytes, (IntPtr)i, BufferUsageHint.StaticDraw);
            }
        }

        int stride = 10 * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 5 * sizeof(float));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, 8 * sizeof(float));
        GL.EnableVertexAttribArray(3);

        GL.BindVertexArray(0);
    }

    public void Draw()
    {
        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, _indexCount, DrawElementsType.UnsignedInt, 0);
    }

    public void Dispose()
    {
        GL.DeleteBuffer(_vbo);
        GL.DeleteBuffer(_ebo);
        GL.DeleteVertexArray(_vao);
    }
}