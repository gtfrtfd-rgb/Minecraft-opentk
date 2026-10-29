using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MinecraftClone.Renderer;

public class Shader : IDisposable
{
    public int Handle { get; }

    private readonly Dictionary<string, int> _uniformCache = new();
    private bool _disposed;

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
}
";

        string fragmentSource = @"
#version 330 core

in vec2 TexCoord;
in vec3 Normal;
in vec2 TileCoords;
out vec4 FragColor;

uniform sampler2D textureAtlas;
uniform bool useTransparency;

void main()
{
    vec2 localUV = fract(TexCoord);
    localUV = clamp(localUV, 0.001, 0.999);
    
    vec2 finalUV = (TileCoords + localUV) / 16.0;

    vec4 texColor = textureLod(textureAtlas, finalUV, 0.0);
    
    if (useTransparency) 
    {
        if (texColor.a < 0.1) discard;
    } 
    else 
    {
        if (texColor.a < 0.1) 
        {
            texColor = vec4(0.2, 0.5, 0.1, 1.0);
        }
    }

    float light = 1.0;
    if (Normal.y > 0.5) light = 1.0;
    else if (Normal.y < -0.5) light = 0.5;
    else if (Normal.z > 0.5) light = 0.8;
    else if (Normal.z < -0.5) light = 0.7;
    else if (Normal.x > 0.5) light = 0.6;
    else if (Normal.x < -0.5) light = 0.6;

    FragColor = vec4(texColor.rgb * light, texColor.a);
}
";

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

        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = GL.GetProgramInfoLog(Handle);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
            GL.DeleteProgram(Handle);
            throw new Exception($"Ошибка линковки программы шейдера: {log}");
        }

        GL.DetachShader(Handle, vertexShader);
        GL.DetachShader(Handle, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
    }

    public void Use() => GL.UseProgram(Handle);

    public void SetMatrix4(string name, Matrix4 value)
    {
        int loc = GetUniformLocation(name);
        GL.UniformMatrix4(loc, false, ref value);
    }

    public void SetInt(string name, int value)
    {
        int loc = GetUniformLocation(name);
        GL.Uniform1(loc, value);
    }

    private int GetUniformLocation(string name)
    {
        if (!_uniformCache.TryGetValue(name, out int loc))
        {
            loc = GL.GetUniformLocation(Handle, name);
            _uniformCache[name] = loc;
        }
        return loc;
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
        GL.DeleteProgram(Handle);
        _uniformCache.Clear();
    }
}