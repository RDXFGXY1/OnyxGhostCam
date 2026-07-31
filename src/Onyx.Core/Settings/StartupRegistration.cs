using Microsoft.Win32;

namespace Onyx.Core.Settings;

/// <summary>
/// "Start with Windows", owned by the app rather than the installer.
///
/// This deliberately does NOT live in the installer's [Registry] section. An
/// installer that writes a Run key is establishing persistence at install time,
/// which is one of the behaviours antivirus heuristics score against an unsigned
/// binary. The same key written by an already-running app, when the user flips a
/// switch, is ordinary application behaviour. Same end result, far less alarming
/// to a scanner — and honestly, more correct: it's the user's choice, so it
/// belongs to the app, and uninstalling clears it either way.
///
/// HKCU only: no elevation needed, and it never touches the machine-wide key.
/// </summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GhostCam";

    /// <summary>True if GhostCam is registered to launch at sign-in.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string s && s.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds or removes the Run entry. Returns false if the write failed.</summary>
    public static bool Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key is null) { return false; }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe)) { return false; }
                // --tray so an auto-start lands in the notification area rather than
                // throwing a window in the user's face at every sign-in.
                key.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
