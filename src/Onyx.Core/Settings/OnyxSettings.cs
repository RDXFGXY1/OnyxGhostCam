using System.Text.Json;

namespace Onyx.Core.Settings;

/// <summary>
/// User-configurable settings, persisted to %AppData%\Onyx\settings.json.
/// Everything stays local — no telemetry, no cloud.
/// </summary>
public sealed class OnyxSettings
{
    public int CameraIndex { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
    public int MosaicBlockSize { get; set; } = 16;
    public int DetectEveryN { get; set; } = 3;
    public bool UseGpu { get; set; } = true;
    public bool ShieldOnStart { get; set; }

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Onyx");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static OnyxSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<OnyxSettings>(json) ?? new OnyxSettings();
            }
        }
        catch
        {
            // Corrupt/unreadable settings fall back to defaults.
        }
        return new OnyxSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
            // Best-effort; never crash the app over a settings write.
        }
    }
}
