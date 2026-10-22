using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace MinecraftClone;

public static class Program
{
    public static void Main()
    {
        var nativeSettings = new NativeWindowSettings
        {
            ClientSize = new Vector2i(1280, 720),
            Title = "Minecraft Clone — OpenTK",
            APIVersion = new Version(3, 3),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible
        };

        using var game = new Game(GameWindowSettings.Default, nativeSettings);
        game.Run();
    }
}