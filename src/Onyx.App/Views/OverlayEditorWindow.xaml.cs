using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Onyx.Core.Processing;
using Scalar = OpenCvSharp.Scalar;

namespace Onyx.App.Views;

/// <summary>
/// Free-form overlay editor: the live feed is the stage, overlays are draggable
/// proxies. Edits mutate the shared overlay list (under a lock) so the main
/// compositor reflects them live.
/// </summary>
public partial class OverlayEditorWindow : Window
{
    private const double CW = 640, CH = 360;

    private readonly List<Overlay> _overlays;
    private readonly object _lock;
    private readonly Dictionary<Overlay, Border> _proxies = new();

    private static readonly (string name, Scalar bgr)[] Palette =
    {
        ("WHITE", new Scalar(240, 240, 240)), ("RED", new Scalar(40, 40, 225)),
        ("GREEN", new Scalar(60, 200, 60)), ("CYAN", new Scalar(230, 220, 40)),
        ("AMBER", new Scalar(40, 200, 245)), ("BLACK", new Scalar(20, 20, 20)),
    };

    private Overlay? _selected;
    private Overlay? _dragging;
    private System.Windows.Point _dragStart;
    private double _startNx, _startNy;

    public OverlayEditorWindow(List<Overlay> overlays, object overlayLock)
    {
        InitializeComponent();
        _overlays = overlays;
        _lock = overlayLock;
        Loaded += (_, _) => RebuildProxies();
    }

    public void ShowBitmap(WriteableBitmap? bmp)
    {
        if (!ReferenceEquals(FeedImage.Source, bmp)) { FeedImage.Source = bmp; }
    }

    private Brush Red => (Brush)FindResource("AccentRed");
    private Brush Dim => (Brush)FindResource("AccentRedDim");

    private void RebuildProxies()
    {
        foreach (var p in _proxies.Values) { EditCanvas.Children.Remove(p); }
        _proxies.Clear();
        List<Overlay> snapshot;
        lock (_lock) { snapshot = new List<Overlay>(_overlays); }
        foreach (var o in snapshot) { AddProxy(o); }
    }

    private void AddProxy(Overlay o)
    {
        var b = new Border
        {
            BorderBrush = Dim, BorderThickness = new Thickness(2), Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll, Tag = o,
        };
        b.Child = new TextBlock
        {
            Text = o.Kind == OverlayKind.Text ? "T" : "IMG",
            Foreground = Red, FontSize = 11, Margin = new Thickness(2, 0, 0, 0),
        };
        b.MouseLeftButtonDown += ProxyDown;
        b.MouseMove += ProxyMove;
        b.MouseLeftButtonUp += ProxyUp;
        _proxies[o] = b;
        EditCanvas.Children.Add(b);
        UpdateProxy(o);
    }

    private void UpdateProxy(Overlay o)
    {
        if (!_proxies.TryGetValue(o, out var b)) { return; }
        var r = OverlayCompositor.MeasureRect(o, (int)CW, (int)CH);
        b.Width = Math.Max(28, r.Width);
        b.Height = Math.Max(18, r.Height);
        Canvas.SetLeft(b, o.Nx * CW);
        Canvas.SetTop(b, o.Ny * CH);
    }

    private void ProxyDown(object sender, MouseButtonEventArgs e)
    {
        var b = (Border)sender;
        _dragging = (Overlay)b.Tag;
        _dragStart = e.GetPosition(EditCanvas);
        _startNx = _dragging.Nx; _startNy = _dragging.Ny;
        b.CaptureMouse();
        Select(_dragging);
        e.Handled = true;
    }

    private void ProxyMove(object sender, MouseEventArgs e)
    {
        if (_dragging is null || e.LeftButton != MouseButtonState.Pressed) { return; }
        var p = e.GetPosition(EditCanvas);
        double nx = Math.Clamp(_startNx + (p.X - _dragStart.X) / CW, 0, 1);
        double ny = Math.Clamp(_startNy + (p.Y - _dragStart.Y) / CH, 0, 1);
        lock (_lock) { _dragging.Nx = nx; _dragging.Ny = ny; }
        Canvas.SetLeft((Border)sender, nx * CW);
        Canvas.SetTop((Border)sender, ny * CH);
    }

    private void ProxyUp(object sender, MouseButtonEventArgs e)
    {
        ((Border)sender).ReleaseMouseCapture();
        _dragging = null;
    }

    private void Select(Overlay? o)
    {
        _selected = o;
        foreach (var (ov, b) in _proxies)
        {
            b.BorderBrush = ReferenceEquals(ov, o) ? Red : Dim;
            b.Background = ReferenceEquals(ov, o) ? new SolidColorBrush(Color.FromArgb(40, 200, 24, 24)) : Brushes.Transparent;
        }
        if (o is null) { Props.Visibility = Visibility.Collapsed; NoSel.Visibility = Visibility.Visible; return; }

        Props.Visibility = Visibility.Visible; NoSel.Visibility = Visibility.Collapsed;
        SelName.Text = (o.Kind == OverlayKind.Text ? "TEXT · " : "IMAGE · ") + o.DisplayName;
        TextRow.Visibility = o.Kind == OverlayKind.Text ? Visibility.Visible : Visibility.Collapsed;
        if (o.Kind == OverlayKind.Text) { SelText.Text = o.Text; ColorBtn.Content = "COLOR: " + ColorName(o.Color); }
        RefreshValues(o);
    }

    private void RefreshValues(Overlay o)
    {
        SizeVal.Text = o.Kind == OverlayKind.Text ? $"{o.Scale:0.0}x" : $"{o.Scale * 100:0}%";
        OpacityVal.Text = $"{o.Opacity * 100:0}%";
    }

    private static string ColorName(Scalar c)
    {
        foreach (var (n, bgr) in Palette)
        {
            if (Math.Abs(bgr.Val0 - c.Val0) < 2 && Math.Abs(bgr.Val1 - c.Val1) < 2 && Math.Abs(bgr.Val2 - c.Val2) < 2) { return n; }
        }
        return "CUSTOM";
    }

    // ===== toolbar =====
    private void OnAddText(object sender, RoutedEventArgs e)
    {
        var t = NewText.Text.Trim();
        if (t.Length == 0) { return; }
        var o = Overlay.CreateText(t);
        lock (_lock) { _overlays.Add(o); }
        NewText.Clear();
        AddProxy(o); Select(o);
    }

    private void OnAddImage(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add image overlay",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
        };
        if (dlg.ShowDialog() != true) { return; }
        var o = Overlay.CreateImage(dlg.FileName);
        if (o.Image is null) { o.Dispose(); return; }
        lock (_lock) { _overlays.Add(o); }
        AddProxy(o); Select(o);
    }

    private void OnSelTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_selected is null || _selected.Kind != OverlayKind.Text) { return; }
        lock (_lock) { _selected.Text = SelText.Text; }
        SelName.Text = "TEXT · " + _selected.DisplayName;
        UpdateProxy(_selected);
    }

    private void OnCycleColor(object sender, RoutedEventArgs e)
    {
        if (_selected is null) { return; }
        int i = 0;
        for (int k = 0; k < Palette.Length; k++) { if (ColorName(_selected.Color) == Palette[k].name) { i = k; break; } }
        var next = Palette[(i + 1) % Palette.Length];
        lock (_lock) { _selected.Color = next.bgr; }
        ColorBtn.Content = "COLOR: " + next.name;
    }

    private void OnSizeDown(object sender, RoutedEventArgs e) => Size(false);
    private void OnSizeUp(object sender, RoutedEventArgs e) => Size(true);
    private void Size(bool up)
    {
        if (_selected is null) { return; }
        lock (_lock)
        {
            if (_selected.Kind == OverlayKind.Text) { _selected.Scale = Math.Clamp(_selected.Scale + (up ? 0.2 : -0.2), 0.5, 4.0); }
            else { _selected.Scale = Math.Clamp(_selected.Scale + (up ? 0.05 : -0.05), 0.05, 1.0); }
        }
        RefreshValues(_selected); UpdateProxy(_selected);
    }

    private void OnOpacityDown(object sender, RoutedEventArgs e) => StepOpacity(false);
    private void OnOpacityUp(object sender, RoutedEventArgs e) => StepOpacity(true);
    private void StepOpacity(bool up)
    {
        if (_selected is null) { return; }
        lock (_lock) { _selected.Opacity = Math.Clamp(_selected.Opacity + (up ? 0.1 : -0.1), 0.1, 1.0); }
        RefreshValues(_selected);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_selected is null) { return; }
        var o = _selected;
        lock (_lock) { _overlays.Remove(o); }
        if (_proxies.TryGetValue(o, out var b)) { EditCanvas.Children.Remove(b); _proxies.Remove(o); }
        o.Dispose();
        Select(null);
    }
}
