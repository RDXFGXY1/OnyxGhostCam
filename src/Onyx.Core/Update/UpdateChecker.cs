using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Onyx.Core.Update;

/// <summary>Release metadata published alongside the app (update.json).</summary>
public sealed class UpdateInfo
{
    [JsonPropertyName("version")] public string Version { get; set; } = "0.0.0";
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("published")] public string Published { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("sizeMb")] public double SizeMb { get; set; }
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("added")] public List<string> Added { get; set; } = new();
    [JsonPropertyName("fixed")] public List<string> Fixed { get; set; } = new();
    [JsonPropertyName("changed")] public List<string> Changed { get; set; } = new();
}

/// <summary>
/// Checks a small JSON manifest on GitHub for a newer release. This is the only
/// network call GhostCam ever makes, it sends no data about the user, and it can
/// be switched off in CONFIG.
/// </summary>
public sealed class UpdateChecker
{
    public const string DefaultManifestUrl =
        "https://raw.githubusercontent.com/RDXFGXY1/OnyxGhostCam/main/update.json";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("GhostCam-Updater");
        return c;
    }

    public string ManifestUrl { get; init; } = DefaultManifestUrl;

    /// <summary>Returns release info if the manifest advertises a newer version, else null.</summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            // Cache-bust so users don't get a stale CDN copy.
            var url = $"{ManifestUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json);
            if (info is null || string.IsNullOrWhiteSpace(info.Version)) { return null; }
            return IsNewer(info.Version, currentVersion) ? info : null;
        }
        catch
        {
            // Offline, blocked, rate-limited or malformed: silently skip.
            return null;
        }
    }

    /// <summary>Downloads the installer to a temp file, reporting 0..1 progress.</summary>
    public async Task<string?> DownloadAsync(UpdateInfo info, IProgress<double>? progress,
                                             CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.Url)) { return null; }
        var dest = Path.Combine(Path.GetTempPath(), $"GhostCam-Setup-{info.Version}.exe");

        using var resp = await Http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead, ct)
                                   .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(dest);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total is > 0) { progress?.Report((double)read / total.Value); }
        }
        return dest;
    }

    /// <summary>True if <paramref name="candidate"/> is a higher version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string candidate, string current)
    {
        static Version Parse(string s)
        {
            s = s.TrimStart('v', 'V').Trim();
            return Version.TryParse(s, out var v) ? v : new Version(0, 0, 0);
        }
        return Parse(candidate) > Parse(current);
    }
}
