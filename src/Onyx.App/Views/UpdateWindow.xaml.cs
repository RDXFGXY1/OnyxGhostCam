using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Onyx.Core.Update;

namespace Onyx.App.Views;

/// <summary>Shows what's new in a release and installs it on request.</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private readonly UpdateChecker _checker = new();
    private CancellationTokenSource? _cts;
    private bool _busy;

    public UpdateWindow(UpdateInfo info, string currentVersion)
    {
        InitializeComponent();
        _info = info;

        VersionText.Text = $"v{info.Version.TrimStart('v', 'V')}";
        CurrentText.Text = $"  (you have v{currentVersion})";

        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.Published)) { meta.Add(info.Published); }
        if (info.SizeMb > 0) { meta.Add($"{info.SizeMb:0} MB DOWNLOAD"); }
        MetaText.Text = string.Join("   ·   ", meta);

        AddSection("ADDED", info.Added);
        AddSection("FIXED", info.Fixed);
        AddSection("CHANGED", info.Changed);
        if (NotesPanel.Children.Count == 0)
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = "No details provided for this release.",
                Style = (Style)FindResource("HudLabel"),
            });
        }

        if (info.Mandatory) { LaterButton.IsEnabled = false; }
    }

    private void AddSection(string title, List<string> items)
    {
        if (items.Count == 0) { return; }

        NotesPanel.Children.Add(new TextBlock
        {
            Text = "◤ " + title,
            FontFamily = (FontFamily)FindResource("HeaderFont"),
            FontSize = 14,
            Foreground = (Brush)FindResource("AccentRed"),
            Margin = new Thickness(0, NotesPanel.Children.Count == 0 ? 0 : 14, 0, 6),
        });

        foreach (var item in items)
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = "  ›  " + item,
                Style = (Style)FindResource("HudLabel"),
                Foreground = (Brush)FindResource("Text"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }
    }

    private void OnDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) { DragMove(); }
    }

    private void OnLater(object sender, RoutedEventArgs e)
    {
        if (_busy) { _cts?.Cancel(); }
        DialogResult = false;
        Close();
    }

    private async void OnUpdate(object sender, RoutedEventArgs e)
    {
        if (_busy) { return; }
        _busy = true;
        UpdateButton.IsEnabled = false;
        LaterButton.Content = "CANCEL";
        ProgressTrack.Visibility = Visibility.Visible;
        StatusText.Text = "downloading update…";

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(p =>
        {
            ProgressFill.Width = p * ProgressTrack.ActualWidth;
            StatusText.Text = $"downloading update…  {p * 100:0}%";
        });

        try
        {
            var file = await _checker.DownloadAsync(_info, progress, _cts.Token);
            if (file is null)
            {
                StatusText.Text = "download failed - no installer link in the release";
                Reset();
                return;
            }

            StatusText.Text = "launching installer - GhostCam will close…";
            await Task.Delay(600);

            Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
            // The installer needs our files free, so shut down.
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "cancelled";
            Reset();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"update failed: {ex.Message}";
            Reset();
        }
    }

    private void Reset()
    {
        _busy = false;
        UpdateButton.IsEnabled = true;
        LaterButton.Content = "LATER";
        ProgressTrack.Visibility = Visibility.Collapsed;
        ProgressFill.Width = 0;
    }
}
