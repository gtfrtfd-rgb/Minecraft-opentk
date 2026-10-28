using System.Runtime;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace MinecraftClone;

public static class Program
{
    public static void Main()
    {
        // Interactive: короткие паузы на Gen0/Gen1, редкие длинные Gen2.
        // На слабом CPU (Ryzen 5 2500U) это даёт меньше фризов, чем SustainedLowLatency,
        // потому что последний иногда провоцирует рост хипа и OutOfMemory-подобные пики.
        GCSettings.LatencyMode = GCLatencyMode.Interactive;

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