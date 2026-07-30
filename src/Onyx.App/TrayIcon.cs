using System.Drawing;
using WinForms = System.Windows.Forms;

namespace Onyx.App;

/// <summary>
/// System-tray presence for Onyx. The icon is drawn at runtime (a red ghost mark)
/// so no asset file needs shipping. Double-click or the menu restores the window.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private Icon? _generated;

    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    /// <summary>Raised when the user clicks an update notification balloon.</summary>
    public event Action? UpdateRequested;

    private bool _balloonIsUpdate;

    public TrayIcon()
    {
        _generated = LoadAppIcon() ?? BuildIcon();

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show GhostCam", null, (_, _) => RestoreRequested?.Invoke());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new WinForms.NotifyIcon
        {
            Icon = _generated,
            Text = "GhostCam · Null Studio",
            Visible = false,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => RestoreRequested?.Invoke();
        _icon.BalloonTipClicked += (_, _) =>
        {
            if (_balloonIsUpdate) { UpdateRequested?.Invoke(); }
            else { RestoreRequested?.Invoke(); }
        };
    }

    /// <summary>
    /// Windows notification telling the user an update is ready. Clicking it opens
    /// the update window ("Update now").
    /// </summary>
    public void NotifyUpdate(string version)
    {
        bool wasHidden = !_icon.Visible;
        _icon.Visible = true;
        _balloonIsUpdate = true;
        _icon.BalloonTipTitle = $"GhostCam {version} is available";
        _icon.BalloonTipText = "Click here to update now.";
        _icon.BalloonTipIcon = WinForms.ToolTipIcon.Info;
        _icon.ShowBalloonTip(10000);
        if (wasHidden)
        {
            // Keep the icon around briefly so the toast stays clickable.
            var t = new WinForms.Timer { Interval = 12000 };
            t.Tick += (_, _) => { t.Stop(); t.Dispose(); if (!_keepVisible) { _icon.Visible = false; } };
            t.Start();
        }
    }

    private bool _keepVisible;

    public void Show(string? balloon = null)
    {
        _icon.Visible = true;
        _keepVisible = true;
        if (balloon is not null)
        {
            _balloonIsUpdate = false;
            _icon.BalloonTipTitle = "GhostCam";
            _icon.BalloonTipText = balloon;
            _icon.ShowBalloonTip(1500);
        }
    }

    public void Hide() { _keepVisible = false; _icon.Visible = false; }

    /// <summary>Prefers the real app icon extracted from the running executable.</summary>
    private static Icon? LoadAppIcon()
    {
        try
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            return exe is null ? null : Icon.ExtractAssociatedIcon(exe);
        }
        catch { return null; }
    }

    /// <summary>Fallback: draws a simple red-on-black ghost glyph.</summary>
    private static Icon BuildIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(10, 10, 10));
            using var red = new SolidBrush(Color.FromArgb(200, 24, 24));
            using var dark = new SolidBrush(Color.FromArgb(10, 10, 10));
            g.FillPie(red, 6, 4, 20, 22, 180, 180);          // head dome
            g.FillRectangle(red, 6, 15, 20, 11);              // body
            for (int i = 0; i < 3; i++) { g.FillEllipse(red, 6 + i * 7, 22, 7, 8); }  // wavy hem
            g.FillEllipse(dark, 11, 13, 4, 5);               // eyes
            g.FillEllipse(dark, 18, 13, 4, 5);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _generated?.Dispose();
        _generated = null;
    }
}
