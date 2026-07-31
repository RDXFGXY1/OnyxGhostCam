using Microsoft.Win32;
using Onyx.Core.Settings;
using Xunit;

namespace Onyx.Core.Tests;

/// <summary>
/// Touches the real HKCU Run key — it's the only way to know this works, and the
/// alternative (an abstraction layer over the registry) would be testing a mock
/// instead of the thing that ships. Every test captures and restores whatever was
/// there first, so a developer's own setting survives a test run.
/// </summary>
public class StartupRegistrationTests : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GhostCam";

    private readonly string? _original;

    public StartupRegistrationTests()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        _original = key?.GetValue(ValueName) as string;
    }

    public void Dispose()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) { return; }
        if (_original is null) { key.DeleteValue(ValueName, throwOnMissingValue: false); }
        else { key.SetValue(ValueName, _original, RegistryValueKind.String); }
    }

    private static string? RawValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) as string;
    }

    [Fact]
    public void EnableThenDisableRoundTrips()
    {
        Assert.True(StartupRegistration.Set(true));
        Assert.True(StartupRegistration.IsEnabled());

        Assert.True(StartupRegistration.Set(false));
        Assert.False(StartupRegistration.IsEnabled());
        Assert.Null(RawValue());
    }

    [Fact]
    public void EnabledValueIsQuotedAndAsksForTray()
    {
        StartupRegistration.Set(true);
        var v = RawValue();

        Assert.NotNull(v);
        // Unquoted paths with spaces are a classic Windows launch bug.
        Assert.StartsWith("\"", v);
        Assert.Contains("\" --tray", v);
    }

    [Fact]
    public void DisablingWhenAlreadyOffIsHarmless()
    {
        StartupRegistration.Set(false);
        Assert.True(StartupRegistration.Set(false));
        Assert.False(StartupRegistration.IsEnabled());
    }

    [Fact]
    public void EnablingTwiceDoesNotDuplicateOrCorrupt()
    {
        StartupRegistration.Set(true);
        var first = RawValue();
        StartupRegistration.Set(true);

        Assert.Equal(first, RawValue());
        Assert.True(StartupRegistration.IsEnabled());
    }

    [Fact]
    public void IsEnabledIgnoresAnEmptyValue()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
        {
            key!.SetValue(ValueName, string.Empty, RegistryValueKind.String);
        }
        Assert.False(StartupRegistration.IsEnabled());
    }
}
