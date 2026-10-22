using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class Shader
{
    public int Handle { get; }

    public Shader(string vertexPath, string fragmentPath)
    {
        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckCompile(vertexShader, "VERTEX");

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckCompile(fragmentShader, "FRAGMENT");

        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, vertexShader);
        GL.AttachShader(Handle, fragmentShader);
        GL.LinkProgram(Handle);

        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
            throw new Exception($"Ошибка линковки шейдера: {GL.GetProgramInfoLog(Handle)}");

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public void Use() => GL.UseProgram(Handle);

    public void SetMatrix4(string name, Matrix4 value)
        => GL.UniformMatrix4(GL.GetUniformLocation(Handle, name), false, ref value);

    public void SetInt(string name, int value)
        => GL.Uniform1(GL.GetUniformLocation(Handle, name), value);

    private static void CheckCompile(int shader, string type)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
            throw new Exception($"Ошибка компиляции {type} шейдера: {GL.GetShaderInfoLog(shader)}");
    }

    public void Dispose() => GL.DeleteProgram(Handle);
}