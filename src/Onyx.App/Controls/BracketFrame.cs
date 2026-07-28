using System.Windows;
using System.Windows.Controls;

namespace Onyx.App.Controls;

/// <summary>
/// A brutalist HUD panel: hosts arbitrary content framed by L-shaped corner
/// brackets, an optional scanline overlay, and a small uppercase label.
/// Styled implicitly by Themes/Controls.xaml.
/// </summary>
public class BracketFrame : ContentControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(BracketFrame),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowScanlinesProperty =
        DependencyProperty.Register(nameof(ShowScanlines), typeof(bool), typeof(BracketFrame),
            new PropertyMetadata(true));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool ShowScanlines
    {
        get => (bool)GetValue(ShowScanlinesProperty);
        set => SetValue(ShowScanlinesProperty, value);
    }
}
