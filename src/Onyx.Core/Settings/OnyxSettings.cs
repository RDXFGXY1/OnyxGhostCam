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

    /// <summary>Cover style index: 0 Mosaic, 1 Black, 2 Ghost, 3 Censored, 4 Image, 5 Text.</summary>
    public int CoverStyle { get; set; }

    /// <summary>User-uploaded mask drawn over the face (CoverStyle.Image).</summary>
    public string CoverImagePath { get; set; } = string.Empty;

    /// <summary>Word stamped over the face (CoverStyle.Text).</summary>
    public string CoverText { get; set; } = "NOPE";

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

    /// <summary>Frames to keep covering the last known face after detection drops out.</summary>
    public int LatchFrames { get; set; } = 15;

    /// <summary>Background mode: 0 Off, 1 Blur, 2 Image, 3 Colour.</summary>
    public int BackgroundMode { get; set; }

    /// <summary>Background blur strength (1..30).</summary>
    public int BackgroundStrength { get; set; } = 12;

    /// <summary>Backdrop image for BackgroundMode.Image.</summary>
    public string BackgroundImagePath { get; set; } = string.Empty;

    /// <summary>Width of the person cutout as a percentage (50..160).</summary>
    public int BackgroundTightness { get; set; } = 100;

    /// <summary>Stall threshold in ms before the watchdog blacks out the output. 0 disables.</summary>
    public int WatchdogMs { get; set; } = 1200;

    /// <summary>Three one-click presets. Always kept at exactly three slots.</summary>
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>
    /// A saved snapshot of the cloak/output settings, loaded in one click. Only the
    /// look-and-feel knobs are captured — never the arm state, so loading a profile
    /// can't put you live by accident.
    /// </summary>
    public sealed class Profile
    {
        public string Name { get; set; } = "PROFILE";
        public int CoverStyle { get; set; }
        public int MosaicBlockSize { get; set; } = 16;
        public int DetectEveryN { get; set; } = 3;
        public double ScoreThreshold { get; set; } = 0.6;
        public bool ParanoidMode { get; set; } = true;
        public int LatchFrames { get; set; } = 15;
        public int BackgroundMode { get; set; }
        public int BackgroundStrength { get; set; } = 12;
        public string BackgroundImagePath { get; set; } = string.Empty;
        public int BackgroundTightness { get; set; } = 100;
        public int OutputEffect { get; set; }
        public bool Hud { get; set; }
        public bool Watermark { get; set; }
        public string CoverText { get; set; } = "NOPE";
        public string CoverImagePath { get; set; } = string.Empty;
    }

    /// <summary>The three stock presets, used on first run and by RESET.</summary>
    public static List<Profile> DefaultProfiles() => new()
    {
        // Conservative: recognisable-but-softened, nothing distracting.
        new Profile
        {
            Name = "WORK CALL", CoverStyle = 0, MosaicBlockSize = 12, DetectEveryN = 3,
            ScoreThreshold = 0.55, ParanoidMode = true, LatchFrames = 15,
            BackgroundMode = 1, BackgroundStrength = 14, OutputEffect = 0, Hud = false,
            Watermark = false,
        },
        // On-brand: ghost cover, watermark, tactical HUD.
        new Profile
        {
            Name = "STREAM", CoverStyle = 2, MosaicBlockSize = 16, DetectEveryN = 2,
            ScoreThreshold = 0.5, ParanoidMode = true, LatchFrames = 20,
            BackgroundMode = 1, BackgroundStrength = 18, OutputEffect = 1, Hud = true,
            Watermark = true,
        },
        // Maximum paranoia: hard black cover, long latch, room fully replaced.
        new Profile
        {
            Name = "FULL ANON", CoverStyle = 1, MosaicBlockSize = 30, DetectEveryN = 1,
            ScoreThreshold = 0.4, ParanoidMode = true, LatchFrames = 45,
            BackgroundMode = 3, BackgroundStrength = 30, OutputEffect = 0, Hud = false,
            Watermark = false,
        },
    };

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
                var loaded = JsonSerializer.Deserialize<OnyxSettings>(json) ?? new OnyxSettings();
                // Settings written before profiles existed, or hand-edited badly.
                if (loaded.Profiles.Count != 3) { loaded.Profiles = DefaultProfiles(); }
                return loaded;
            }
        }
        catch
        {
            // Corrupt/unreadable settings fall back to defaults.
        }
        return new OnyxSettings { Profiles = DefaultProfiles() };
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
