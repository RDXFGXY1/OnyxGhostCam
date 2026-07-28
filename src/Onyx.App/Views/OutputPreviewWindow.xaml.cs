using System.Windows;
using System.Windows.Media.Imaging;

namespace Onyx.App.Views;

/// <summary>
/// Small always-on-top window mirroring the shielded output — so you always see
/// exactly what other apps see, and never forget the shield is (or isn't) on.
/// </summary>
public partial class OutputPreviewWindow : Window
{
    public OutputPreviewWindow()
    {
        InitializeComponent();
    }

    /// <summary>Point the monitor at the current output bitmap (shared with the main preview).</summary>
    public void ShowBitmap(WriteableBitmap? bmp)
    {
        if (!ReferenceEquals(PopImage.Source, bmp)) { PopImage.Source = bmp; }
    }
}
