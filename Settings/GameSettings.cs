using System;
using System.IO;
using System.Text.Json;

namespace MinecraftClone.Settings;

public class GameSettings
{
    public int RenderDistance { get; set; } = 5;
    public bool VSync { get; set; } = true;
    public float Fov { get; set; } = 75f;
    public float MouseSensitivity { get; set; } = 0.15f;
    public int FpsLimit { get; set; } = 60;

    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MinecraftClone");

    private static readonly string FilePath = Path.Combine(FolderPath, "settings.json");

    public static GameSettings Load()
    {
        if (!File.Exists(FilePath)) return new GameSettings();
        try
        {
            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            return new GameSettings();
        }
    }

    public void Reset()
    {
        RenderDistance = 5;
        VSync = true;
        Fov = 75f;
        MouseSensitivity = 0.15f;
        FpsLimit = 60;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
        }
    }
}