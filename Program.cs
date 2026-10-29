using System;
using System.Runtime;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;

namespace MinecraftClone;

public static class Program
{
    // Отключает масштабирование DPI в Windows. Без этого координаты мыши
    // возвращаются в логических пикселях, а рендер идёт в физических — хитбоксы
    // кнопок "уезжают". Этот вызов должен быть ДО создания окна.
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    public static void Main()
    {
        if (OperatingSystem.IsWindows())
        {
            SetProcessDPIAware();
        }

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