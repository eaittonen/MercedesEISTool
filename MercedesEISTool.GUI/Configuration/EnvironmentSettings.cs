using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MercedesEISTool.GUI.Configuration;

public sealed class EnvironmentSettings
{
    private const string FileName = "user-settings.json";
    private readonly string _filePath;

    public EnvironmentSettings()
    {
        var appDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MercedesEISTool");
        Directory.CreateDirectory(appDataDirectory);
        _filePath = Path.Combine(appDataDirectory, FileName);
    }

    public string SelectedEnvironment { get; set; } = "Production";

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public static EnvironmentSettings Load()
    {
        var settings = new EnvironmentSettings();
        if (!File.Exists(settings._filePath))
        {
            return settings;
        }

        try
        {
            var json = File.ReadAllText(settings._filePath);
            var loaded = JsonSerializer.Deserialize<EnvironmentSettings>(json);
            if (loaded is not null)
            {
                settings.SelectedEnvironment = loaded.SelectedEnvironment;
            }
        }
        catch
        {
            // Ignore invalid settings and fall back to defaults.
        }

        return settings;
    }
}
