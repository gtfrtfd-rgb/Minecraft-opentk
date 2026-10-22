using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class Shader
{
    public int Handle { get; }

    public Shader(string vertexPath, string fragmentPath)
    {
        // Читаем файлы целиком
        string vertexSource = File.ReadAllText(vertexPath);
        string fragmentSource = File.ReadAllText(fragmentPath);

        // Проверка на пустой файл (частая причина ошибки EOF)
        if (string.IsNullOrWhiteSpace(fragmentSource))
        {
            throw new Exception($"Файл фрагментного шейдера '{fragmentPath}' пуст или не может быть прочитан.");
        }

        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        CheckCompile(vertexShader, "VERTEX", vertexPath);

        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        CheckCompile(fragmentShader, "FRAGMENT", fragmentPath);

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

    private static void CheckCompile(int shader, string type, string path)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            string infoLog = GL.GetShaderInfoLog(shader);
            throw new Exception($"Ошибка компиляции {type} шейдера ('{path}'): {infoLog}");
        }
    }

    public void Dispose() => GL.DeleteProgram(Handle);
}