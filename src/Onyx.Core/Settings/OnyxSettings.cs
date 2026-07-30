using System.Text.Json;

namespace Onyx.Core.Settings;

/// <summary>
/// User-configurable settings, persisted to %AppData%\Onyx\settings.json.
/// Stored locally only — no telemetry, nothing uploaded.
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

    /// <summary>Blur the whole frame when no face is detected (fail-safe privacy).</summary>
    public bool ParanoidMode { get; set; } = true;

    /// <summary>Cover style index: 0 Mosaic, 1 Black, 2 Ghost, 3 Censored.</summary>
    public int CoverStyle { get; set; }

    /// <summary>Output filter index: 0 None, 1 Scanlines, 2 Glitch.</summary>
    public int OutputEffect { get; set; }

    /// <summary>Tactical HUD overlay on the output.</summary>
    public bool Hud { get; set; }

    /// <summary>Cockpit sound effects.</summary>
    public bool Sound { get; set; }

    /// <summary>Horizontally flip the final composed output (video + text).</summary>
    public bool Mirror { get; set; }

    /// <summary>Mirror only the drawn elements (text/overlays/HUD), leaving the video as-is.</summary>
    public bool MirrorText { get; set; }

    /// <summary>
    /// Check GitHub for new releases. This is the only network call GhostCam makes;
    /// turn it off to keep the app completely offline.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Release version the user chose to skip reminders for.</summary>
    public string SkippedVersion { get; set; } = string.Empty;

    /// <summary>Show the GHOSTCAM · name watermark.</summary>
    public bool Watermark { get; set; }
    public string WatermarkName { get; set; } = "KYROS";

    /// <summary>Persisted custom overlays (text + image).</summary>
    public List<OverlayState> Overlays { get; set; } = new();

    /// <summary>Face-detection confidence threshold (0.3 = sensitive, 0.9 = strict).</summary>
    public double ScoreThreshold { get; set; } = 0.6;

    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GhostCam");
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

    public sealed class OverlayState
    {
        public int Kind { get; set; }
        public string Text { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public double Nx { get; set; } = 0.05;
        public double Ny { get; set; } = 0.05;
        public double Scale { get; set; } = 1.0;
        public double Opacity { get; set; } = 1.0;
        public int ColorB { get; set; } = 240;
        public int ColorG { get; set; } = 240;
        public int ColorR { get; set; } = 240;
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
