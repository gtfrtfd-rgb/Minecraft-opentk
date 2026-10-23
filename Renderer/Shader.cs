using System;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class Shader
{
    public int Handle { get; }

    public Shader()
    {
        string vertexSource = @"
#version 330 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in vec3 aNormal;
layout(location = 3) in vec2 aTileCoords;

uniform mat4 view;
uniform mat4 projection;

out vec2 TexCoord;
out vec3 Normal;
out vec2 TileCoords;

void main()
{
    TexCoord = aTexCoord;
    Normal = aNormal;
    TileCoords = aTileCoords;
    gl_Position = projection * view * vec4(aPosition, 1.0);
}";

        string fragmentSource = @"
#version 330 core

in vec2 TexCoord;
in vec3 Normal;
in vec2 TileCoords;
out vec4 FragColor;

uniform sampler2D textureAtlas;

void main()
{
    vec2 localUV = fract(TexCoord);
    vec2 finalUV = (TileCoords + localUV) / 16.0;

    vec4 texColor = texture(textureAtlas, finalUV);
    if (texColor.a < 0.1) discard;

    float light = 1.0;
    if (Normal.y > 0.5) light = 1.0;
    else if (Normal.y < -0.5) light = 0.5;
    else if (Normal.z > 0.5) light = 0.8;
    else if (Normal.z < -0.5) light = 0.7;
    else if (Normal.x > 0.5) light = 0.6;
    else if (Normal.x < -0.5) light = 0.6;

    FragColor = vec4(texColor.rgb * light, texColor.a);
}";

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

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public void Use() => GL.UseProgram(Handle);
    public void SetMatrix4(string name, Matrix4 value) => GL.UniformMatrix4(GL.GetUniformLocation(Handle, name), false, ref value);
    public void SetInt(string name, int value) => GL.Uniform1(GL.GetUniformLocation(Handle, name), value);

    private static void CheckCompile(int shader, string type)
    {
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0) throw new Exception($"Ошибка компиляции {type} шейдера: {GL.GetShaderInfoLog(shader)}");
    }

    public void Dispose() => GL.DeleteProgram(Handle);
}