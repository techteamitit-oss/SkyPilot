using System.Text.Json;

namespace SkyPilot.Utils;

/// <summary>
/// JSON-based application settings.
/// </summary>
public class AppSettings
{
    public string LastSerialPort { get; set; } = "";
    public int LastBaudRate { get; set; } = 115200;
    public string LastUdpHost { get; set; } = "127.0.0.1";
    public int LastUdpPort { get; set; } = 14550;
    public string LastLogFile { get; set; } = "";
    public int MapProvider { get; set; } = 1;

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SkyPilot", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
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
            var dir = Path.GetDirectoryName(SettingsPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
