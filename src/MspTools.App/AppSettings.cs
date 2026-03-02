using System.IO;
using System.Text.Json;

namespace MspTools.App;

/// <summary>
/// Reads and writes application settings from <c>%LOCALAPPDATA%\MspTools\settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MspTools");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public string MySqlConnectionString { get; set; } =
        "Server=localhost;Port=3306;Database=msptools;Uid=root;Pwd=;";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(SettingsFile,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
