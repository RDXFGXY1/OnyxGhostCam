using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp.WpfExtensions;
using Onyx.App.Audio;
using Onyx.Core.Capture;
using Onyx.Core.Detection;
using Onyx.Core.Interop;
using Onyx.Core.Processing;
using Onyx.Core.Settings;
using Onyx.Core.Update;
using Mat = OpenCvSharp.Mat;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Onyx.App.Views;

public partial class MainWindow : Window
{
    private enum SensorSt { Safe, Powered, Acquiring, Online }
    private enum CloakSt { Safe, Enabled, Tested, Engaged }

    private SensorSt _sensor = SensorSt.Safe;
    private CloakSt _cloak = CloakSt.Safe;
    private bool _uplinkLive;
    private bool _guardLifted;

    private readonly WebcamCapture _capture = new();
    private readonly MosaicProcessor _mosaic = new();
    private readonly object _frameLock = new();
    private Mat? _pendingProcessed;
    private WriteableBitmap? _procBitmap;
    private volatile bool _mosaicEnabled;

    private IFaceDetector? _detector;
    private FaceTracker? _tracker;
    private volatile bool _detectorInit;
    private volatile int _lastFaceCount;

    private ObsVirtualCameraSink? _vcamSink;
    private readonly object _sinkLock = new();

    private OutputPreviewWindow? _popup;
    private OverlayEditorWindow? _editor;
    private volatile bool _paranoid = true;
    private volatile OutputEffect _outputEffect = OutputEffect.None;
    private volatile bool _hudEnabled;
    private volatile int _currentFps;

    private int _strength = 16, _sensitivity = 60, _detRate = 3, _camIndex;
    private bool _hd, _useGpu = true;

    private readonly List<Overlay> _overlays = new();
    private readonly object _overlayLock = new();
    private volatile bool _watermark;
    private string _watermarkName = "KYROS";
    private volatile bool _mirror;
    private volatile bool _mirrorText;

    private int _frameCount;
    private readonly System.Diagnostics.Stopwatch _fpsClock = System.Diagnostics.Stopwatch.StartNew();
    private readonly DispatcherTimer _hudTimer;
    private readonly DispatcherTimer _warnTimer;
    private readonly DispatcherTimer _holdTimer;
    private readonly System.Diagnostics.Stopwatch _holdClock = new();
    private readonly OnyxSettings _settings = OnyxSettings.Load();
    private bool _blink, _warnActive;
    private bool _arming;
    private readonly TrayIcon _tray = new();

    private static readonly string AppVersion =
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}" : "1.0.0";
    private UpdateInfo? _pendingUpdate;
    private DispatcherTimer? _updateNagTimer;
    private readonly Random _rng = new();

    public MainWindow()
    {
        InitializeComponent();
        _capture.FrameReady += OnFrameReady;
        _capture.Error += msg => Dispatcher.BeginInvoke(() => SetStatus($"error: {msg}"));
        CompositionTarget.Rendering += OnRendering;

        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hudTimer.Tick += OnHudTick; _hudTimer.Start();
        _warnTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _warnTimer.Tick += OnWarnTick; _warnTimer.Start();
        _holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _holdTimer.Tick += OnHoldTick;

        ApplySettingsToUi();
        Refresh();

        _tray.RestoreRequested += RestoreFromTray;
        _tray.ExitRequested += Close;
        _tray.UpdateRequested += () => Dispatcher.BeginInvoke(ShowUpdateWindow);

        // Runs in the background; must not hold up the window opening.
        if (_settings.CheckForUpdates) { RunUpdateCheckInBackground(); }

        Closed += (_, _) =>
        {
            _tray.Dispose();
            SaveSettings(); _hudTimer.Stop(); _warnTimer.Stop(); _holdTimer.Stop();
            _capture.Dispose(); _detector?.Dispose(); _popup?.Close(); _editor?.Close();
            lock (_sinkLock) { _vcamSink?.Dispose(); _vcamSink = null; }
            lock (_overlayLock) { foreach (var o in _overlays) { o.Dispose(); } _overlays.Clear(); }
        };
    }

    // ===== custom title bar =====
    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed) { DragMove(); }
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        Sfx.Click();
        WindowState = WindowState.Minimized;
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        Sfx.Click();
        Close();
    }

    // Hide the window entirely and live in the system tray (the pipeline keeps
    // running, so the uplink stays live while Onyx is out of the way).
    private void OnHideToTray(object sender, RoutedEventArgs e)
    {
        Sfx.Click();
        _tray.Show("Running in the tray — double-click to restore.");
        Hide();
    }

    private void RestoreFromTray()
    {
        _tray.Hide();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ===== updates =====

    private async void RunUpdateCheckInBackground()
    {
        try { await CheckForUpdatesAsync(); }
        catch { /* never let an update check break startup */ }
    }

    // Checks GitHub once at launch. If a newer release exists, show the window
    // right away, then keep reminding with a Windows notification every 20-60 min.
    private async Task CheckForUpdatesAsync()
    {
        var info = await new UpdateChecker().CheckAsync(AppVersion);
        if (info is null) { return; }

        _pendingUpdate = info;
        _ = Dispatcher.BeginInvoke(() =>
        {
            SetStatus($"update available: v{info.Version.TrimStart('v', 'V')}");
            if (!string.Equals(_settings.SkippedVersion, info.Version, StringComparison.OrdinalIgnoreCase))
            {
                ShowUpdateWindow();
            }
            StartUpdateNag();
        });
    }

    private void StartUpdateNag()
    {
        _updateNagTimer?.Stop();
        _updateNagTimer = new DispatcherTimer { Interval = NextNagDelay() };
        _updateNagTimer.Tick += (_, _) =>
        {
            if (_pendingUpdate is null) { _updateNagTimer?.Stop(); return; }
            _tray.NotifyUpdate($"v{_pendingUpdate.Version.TrimStart('v', 'V')}");
            // Re-roll the interval so the reminder isn't clockwork.
            if (_updateNagTimer is not null) { _updateNagTimer.Interval = NextNagDelay(); }
        };
        _updateNagTimer.Start();
    }

    private TimeSpan NextNagDelay() => TimeSpan.FromMinutes(_rng.Next(20, 61));

    private void ShowUpdateWindow()
    {
        if (_pendingUpdate is null) { return; }
        if (!IsVisible) { RestoreFromTray(); }

        var dlg = new UpdateWindow(_pendingUpdate, AppVersion) { Owner = this };
        var result = dlg.ShowDialog();

        // "Later" on a non-mandatory release: stop nagging for this version.
        if (result != true && !_pendingUpdate.Mandatory)
        {
            _settings.SkippedVersion = _pendingUpdate.Version;
        }
    }

    // ===== brushes =====
    private Brush Red => (Brush)FindResource("AccentRed");
    private Brush Dim => (Brush)FindResource("TextDim");
    private Brush Txt => (Brush)FindResource("Text");

    // ===== settings =====
    private void ApplySettingsToUi()
    {
        _strength = _settings.MosaicBlockSize; _detRate = _settings.DetectEveryN;
        _camIndex = _settings.CameraIndex; _hd = _settings.Height >= 1080;
        _useGpu = _settings.UseGpu; _paranoid = _settings.ParanoidMode;
        _sensitivity = (int)Math.Round(_settings.ScoreThreshold * 100.0);
        _mosaic.Style = (CoverStyle)Math.Clamp(_settings.CoverStyle, 0, 3);
        _mosaic.BlockSize = _strength;
        _outputEffect = (OutputEffect)Math.Clamp(_settings.OutputEffect, 0, 2);
        _hudEnabled = _settings.Hud;

        StrengthVal.Text = _strength.ToString(); SensVal.Text = _sensitivity.ToString();
        RateVal.Text = _detRate.ToString(); CamVal.Text = _camIndex.ToString();
        _mirror = _settings.Mirror; MirrorSwitch.IsChecked = _mirror;
        _mirrorText = _settings.MirrorText; MirrorTextSwitch.IsChecked = _mirrorText;
        GpuSwitch.IsChecked = _useGpu; ParanoidSwitch.IsChecked = _paranoid;
        BootSwitch.IsChecked = _settings.ShieldOnStart; HudSwitch.IsChecked = _hudEnabled;
        Sfx.Enabled = _settings.Sound; SoundSwitch.IsChecked = _settings.Sound;
        UpdateSwitch.IsChecked = _settings.CheckForUpdates;
        (_hd ? Res1080 : Res720).IsChecked = true;
        CoverRadio(_mosaic.Style).IsChecked = true;
        FilterRadio(_outputEffect).IsChecked = true;

        _watermark = _settings.Watermark;
        WatermarkSwitch.IsChecked = _watermark;
        _watermarkName = string.IsNullOrWhiteSpace(_settings.WatermarkName) ? "KYROS" : _settings.WatermarkName;
        WatermarkNameBox.Text = _watermarkName;
        foreach (var st in _settings.Overlays)
        {
            var o = st.Kind == (int)OverlayKind.Image ? Overlay.CreateImage(st.ImagePath) : Overlay.CreateText(st.Text);
            if (o.Kind == OverlayKind.Image && o.Image is null) { o.Dispose(); continue; }
            o.Nx = st.Nx; o.Ny = st.Ny; o.Scale = st.Scale; o.Opacity = st.Opacity;
            o.Color = new OpenCvSharp.Scalar(st.ColorB, st.ColorG, st.ColorR);
            lock (_overlayLock) { _overlays.Add(o); }
        }
    }

    private void SaveSettings()
    {
        _settings.MosaicBlockSize = _strength; _settings.DetectEveryN = _detRate;
        _settings.CameraIndex = _camIndex; _settings.Width = _hd ? 1920 : 1280;
        _settings.Height = _hd ? 1080 : 720; _settings.UseGpu = _useGpu;
        _settings.ShieldOnStart = BootSwitch.IsChecked == true; _settings.ParanoidMode = _paranoid;
        _settings.ScoreThreshold = _sensitivity / 100.0; _settings.CoverStyle = (int)_mosaic.Style;
        _settings.OutputEffect = (int)_outputEffect; _settings.Hud = _hudEnabled;
        _settings.Sound = Sfx.Enabled;
        _settings.Watermark = _watermark; _settings.WatermarkName = _watermarkName;
        _settings.Mirror = _mirror; _settings.MirrorText = _mirrorText;
        lock (_overlayLock)
        {
            _settings.Overlays = _overlays.Select(o => new OnyxSettings.OverlayState
            {
                Kind = (int)o.Kind, Text = o.Text, ImagePath = o.ImagePath,
                Nx = o.Nx, Ny = o.Ny, Scale = o.Scale, Opacity = o.Opacity,
                ColorB = (int)o.Color.Val0, ColorG = (int)o.Color.Val1, ColorR = (int)o.Color.Val2,
            }).ToList();
        }
        _settings.Save();
    }

    private RadioButton CoverRadio(CoverStyle s) => s switch
    { CoverStyle.Black => CoverBlack, CoverStyle.Ghost => CoverGhost, CoverStyle.Censored => CoverCensor, _ => CoverMosaic };
    private RadioButton FilterRadio(OutputEffect e) => e switch
    { OutputEffect.Scanlines => FilterScan, OutputEffect.Glitch => FilterGlitch, _ => FilterNone };

    // ===== panel expand/collapse =====
    private static void Toggle(UIElement el)
        => el.Visibility = el.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void OnToggleSensor(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(SensorContent); }
    private void OnToggleCloak(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(CloakContent); }
    private void OnToggleUplink(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(UplinkContent); }
    private void OnToggleConfig(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(ConfigContent); }
    private void OnToggleDiag(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(DiagContent); }

    // ===== SENSOR procedure =====
    private void OnSensorPower(object s, RoutedEventArgs e)
    {
        if (_arming) { return; }
        Sfx.Click();
        if (SensorPowerSw.IsChecked == true) { if (_sensor == SensorSt.Safe) { _sensor = SensorSt.Powered; } }
        else { SensorKill(); }
        Refresh();
    }

    private void OnAcquire(object s, RoutedEventArgs e)
    {
        if (_sensor != SensorSt.Powered) { return; }
        Sfx.Beep();
        _capture.CameraIndex = _camIndex;
        _capture.RequestedWidth = _hd ? 1920 : 1280;
        _capture.RequestedHeight = _hd ? 1080 : 720;
        _capture.Start();
        if (_capture.IsRunning) { _sensor = SensorSt.Acquiring; SetStatus("feed acquired - verify then confirm"); }
        else { SetStatus("acquire failed: no camera at that index"); }
        Refresh();
    }

    private void OnConfirmFeed(object s, RoutedEventArgs e)
    {
        if (_sensor != SensorSt.Acquiring) { return; }
        Sfx.Arm(); _sensor = SensorSt.Online;
        CloakContent.Visibility = Visibility.Visible; // auto-open next
        SetStatus("sensor online");
        Refresh();
    }

    private void OnSensorAbort(object s, RoutedEventArgs e)
    {
        Sfx.Click(); _capture.Stop(); _sensor = SensorSt.Powered; CloakKill(); Refresh();
    }

    private void SensorKill()
    {
        _capture.Stop(); _sensor = SensorSt.Safe; ForceOff(SensorPowerSw); CloakKill();
    }

    // ===== CLOAK procedure =====
    private void OnCloakEnable(object s, RoutedEventArgs e)
    {
        if (_arming) { return; }
        Sfx.Click();
        if (CloakEnableSw.IsChecked == true)
        {
            if (_sensor != SensorSt.Online) { ForceOff(CloakEnableSw); SetStatus("⚠ SENSOR must be ONLINE first"); Refresh(); return; }
            _cloak = CloakSt.Enabled;
        }
        else { _cloak = CloakSt.Safe; UplinkKill(); }
        Refresh();
    }

    private void OnTestCloak(object s, RoutedEventArgs e)
    {
        if (_cloak < CloakSt.Enabled) { return; }
        Sfx.Beep(); EnsureDetector(); _cloak = CloakSt.Tested; SetStatus("cloak testing - watch the monitor"); Refresh();
    }

    private void OnConfirmCloak(object s, RoutedEventArgs e)
    {
        if (_cloak != CloakSt.Tested) { return; }
        Sfx.Arm(); _cloak = CloakSt.Engaged; UplinkContent.Visibility = Visibility.Visible; SetStatus("cloak engaged"); Refresh();
    }

    private void OnCloakAbort(object s, RoutedEventArgs e) { Sfx.Click(); CloakKill(); Refresh(); }

    private void CloakKill() { _cloak = CloakSt.Safe; ForceOff(CloakEnableSw); UplinkKill(); }

    // ===== UPLINK procedure =====
    private bool UplinkReady => _sensor == SensorSt.Online && _cloak == CloakSt.Engaged;

    private void OnLiftGuard(object s, RoutedEventArgs e)
    {
        if (!UplinkReady || _uplinkLive) { return; }
        Sfx.Arm(); _guardLifted = true; SetStatus("guard lifted - hold to broadcast"); Refresh();
    }

    private void OnBroadcastDown(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!BroadcastBtn.IsEnabled || !_guardLifted || _uplinkLive) { return; }
        _holdClock.Restart(); _holdTimer.Start();
    }

    private void OnBroadcastUp(object s, System.Windows.Input.MouseEventArgs e)
    {
        if (_uplinkLive) { return; }
        _holdTimer.Stop(); _holdClock.Reset(); BroadcastFill.Width = 0;
    }

    private void OnHoldTick(object? s, EventArgs e)
    {
        double frac = Math.Min(1.0, _holdClock.Elapsed.TotalMilliseconds / 1500.0);
        BroadcastFill.Width = frac * BroadcastBtn.ActualWidth;
        if (frac >= 1.0)
        {
            _holdTimer.Stop(); _holdClock.Reset();
            lock (_sinkLock)
            {
                var sink = new ObsVirtualCameraSink(1280, 720, 30);
                if (sink.TryStart()) { _vcamSink = sink; _uplinkLive = true; Sfx.Arm(); SetStatus("UPLINK LIVE - select 'OBS Virtual Camera'"); }
                else { sink.Dispose(); BroadcastFill.Width = 0; SetStatus("uplink failed: OBS busy or not installed"); }
            }
            Refresh();
        }
    }

    private void OnUplinkAbort(object s, RoutedEventArgs e) { Sfx.Click(); UplinkKill(); Refresh(); }

    private void UplinkKill()
    {
        _uplinkLive = false; _guardLifted = false; BroadcastFill.Width = 0;
        lock (_sinkLock) { _vcamSink?.Dispose(); _vcamSink = null; }
    }

    private void OnMasterKill(object s, RoutedEventArgs e)
    {
        Sfx.Click(); SensorKill(); Refresh(); SetStatus("MASTER KILL - all systems safe");
    }

    // ===== shared control handlers =====
    private void ForceOff(ToggleButton sw) { _arming = true; sw.IsChecked = false; _arming = false; }

    private void OnStep(object sender, RoutedEventArgs e)
    {
        Sfx.Beep();
        var tag = (string)((Button)sender).Tag;
        bool up = tag.EndsWith('+'); string key = tag.TrimEnd('+', '-');
        switch (key)
        {
            case "strength": _strength = Math.Clamp(_strength + (up ? 2 : -2), 4, 60); StrengthVal.Text = _strength.ToString(); _mosaic.BlockSize = _strength; break;
            case "sens": _sensitivity = Math.Clamp(_sensitivity + (up ? 5 : -5), 30, 90); SensVal.Text = _sensitivity.ToString(); if (_detector is not null) { _detector.ScoreThreshold = _sensitivity / 100f; } break;
            case "rate": _detRate = Math.Clamp(_detRate + (up ? 1 : -1), 1, 8); RateVal.Text = _detRate.ToString(); if (_tracker is not null) { _tracker.DetectEveryNFrames = _detRate; } break;
            case "cam": _camIndex = Math.Clamp(_camIndex + (up ? 1 : -1), 0, 4); CamVal.Text = _camIndex.ToString(); break;
        }
    }

    private void OnCoverSel(object s, RoutedEventArgs e) { Sfx.Click(); _mosaic.Style = (CoverStyle)int.Parse((string)((RadioButton)s).Tag); }
    private void OnFilterSel(object s, RoutedEventArgs e) { Sfx.Click(); _outputEffect = (OutputEffect)int.Parse((string)((RadioButton)s).Tag); }
    private void OnResSel(object s, RoutedEventArgs e) { Sfx.Click(); _hd = (string)((RadioButton)s).Tag == "1080"; }
    private void OnParanoidSw(object s, RoutedEventArgs e) { Sfx.Click(); _paranoid = ParanoidSwitch.IsChecked == true; }
    private void OnGpuSw(object s, RoutedEventArgs e) { Sfx.Click(); _useGpu = GpuSwitch.IsChecked == true; }
    private void OnMirrorSw(object s, RoutedEventArgs e) { Sfx.Click(); _mirror = MirrorSwitch.IsChecked == true; }
    private void OnMirrorTextSw(object s, RoutedEventArgs e) { Sfx.Click(); _mirrorText = MirrorTextSwitch.IsChecked == true; }
    private void OnHudSw(object s, RoutedEventArgs e) { Sfx.Click(); _hudEnabled = HudSwitch.IsChecked == true; }
    private void OnSoundSw(object s, RoutedEventArgs e) { Sfx.Enabled = SoundSwitch.IsChecked == true; Sfx.Click(); }

    private void OnUpdateSw(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        _settings.CheckForUpdates = UpdateSwitch.IsChecked == true;
        if (!_settings.CheckForUpdates) { _updateNagTimer?.Stop(); }
    }

    private async void OnCheckUpdateNow(object s, RoutedEventArgs e)
    {
        Sfx.Beep();
        CheckUpdateButton.IsEnabled = false;
        SetStatus("checking for updates…");

        _settings.SkippedVersion = string.Empty; // manual check ignores "later"
        var info = await new UpdateChecker().CheckAsync(AppVersion);
        if (info is null)
        {
            SetStatus($"you're up to date (v{AppVersion})");
        }
        else
        {
            _pendingUpdate = info;
            ShowUpdateWindow();
            StartUpdateNag();
        }
        CheckUpdateButton.IsEnabled = true;
    }

    private void OnPopOutClick(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        if (_popup is null) { _popup = new OutputPreviewWindow { Owner = this }; _popup.Closed += (_, _) => _popup = null; _popup.Show(); }
        else { _popup.Activate(); }
    }

    // ===== OVERLAYS =====
    private void OnToggleOverlays(object s, RoutedEventArgs e) { Sfx.Click(); Toggle(OverlaysContent); }

    private void OnWatermarkSw(object s, RoutedEventArgs e) { Sfx.Click(); _watermark = WatermarkSwitch.IsChecked == true; }

    private void OnWatermarkName(object s, TextChangedEventArgs e)
    {
        if (WatermarkNameBox is null) { return; }
        _watermarkName = string.IsNullOrWhiteSpace(WatermarkNameBox.Text) ? "KYROS" : WatermarkNameBox.Text.Trim();
    }

    private void OnOpenEditor(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        if (_editor is null)
        {
            _editor = new OverlayEditorWindow(_overlays, _overlayLock) { Owner = this };
            _editor.Closed += (_, _) => _editor = null;
            _editor.Show();
        }
        else { _editor.Activate(); }
    }

    // ===== interlock + visual refresh =====
    private void Refresh()
    {
        _mosaicEnabled = _cloak is CloakSt.Tested or CloakSt.Engaged;

        // Sensor
        Led(SLed1, _sensor >= SensorSt.Powered ? 2 : 1);
        Led(SLed2, _sensor >= SensorSt.Powered ? 2 : 0);
        Led(SLed3, _sensor >= SensorSt.Acquiring ? 2 : (_sensor == SensorSt.Powered ? 1 : 0));
        Led(SLed4, _sensor == SensorSt.Online ? 2 : (_sensor == SensorSt.Acquiring ? 1 : 0));
        SensorState.Text = _sensor.ToString().ToUpperInvariant();
        SensorState.Foreground = _sensor == SensorSt.Online ? Red : Dim;
        CamDown.IsEnabled = CamUp.IsEnabled = AcquireBtn.IsEnabled = _sensor == SensorSt.Powered;
        ConfirmFeed.IsEnabled = SensorAbort.IsEnabled = _sensor == SensorSt.Acquiring;

        // Cloak
        bool cloakUnlocked = _sensor == SensorSt.Online;
        CloakState.Text = cloakUnlocked ? _cloak.ToString().ToUpperInvariant() : "LOCKED";
        CloakState.Foreground = _cloak == CloakSt.Engaged ? Red : Dim;
        CloakEnableSw.IsEnabled = cloakUnlocked;
        bool cfg = _cloak >= CloakSt.Enabled;
        CoverMosaic.IsEnabled = CoverBlack.IsEnabled = CoverGhost.IsEnabled = CoverCensor.IsEnabled = cfg;
        TestBtn.IsEnabled = _cloak >= CloakSt.Enabled;
        ConfirmCloak.IsEnabled = CloakAbort.IsEnabled = _cloak == CloakSt.Tested;
        Led(CLed1, _cloak >= CloakSt.Enabled ? 2 : (cloakUnlocked ? 1 : 0));
        Led(CLed2, cfg ? 2 : 0); Led(CLed3, cfg ? 2 : 0);
        Led(CLed4, _cloak >= CloakSt.Tested ? 2 : (_cloak == CloakSt.Enabled ? 1 : 0));
        Led(CLed5, _cloak == CloakSt.Engaged ? 2 : (_cloak == CloakSt.Tested ? 1 : 0));

        // Uplink
        UplinkState.Text = _uplinkLive ? "LIVE" : (UplinkReady ? "READY" : "LOCKED");
        UplinkState.Foreground = _uplinkLive ? Red : Dim;
        Check(ChkSensor, "SENSOR", _sensor == SensorSt.Online);
        Check(ChkCloak, "CLOAK", _cloak == CloakSt.Engaged);
        Check(ChkObs, "OBS DRIVER", true);
        FilterNone.IsEnabled = FilterScan.IsEnabled = FilterGlitch.IsEnabled = HudSwitch.IsEnabled = UplinkReady;
        ArmGuardBtn.IsEnabled = UplinkReady && !_uplinkLive;
        ArmGuardBtn.Visibility = _guardLifted ? Visibility.Collapsed : Visibility.Visible;
        BroadcastBtn.IsEnabled = _guardLifted && !_uplinkLive;
        BroadcastBtn.Opacity = BroadcastBtn.IsEnabled || _uplinkLive ? 1.0 : 0.4;
        UplinkAbort.IsEnabled = _uplinkLive;
        Led(ULed1, UplinkReady ? 2 : 0); Led(ULed2, UplinkReady ? 2 : 0);
        Led(ULed3, _guardLifted ? 2 : (UplinkReady ? 1 : 0));
        Led(ULed4, _uplinkLive ? 2 : (_guardLifted ? 1 : 0));

        PreviewHint.Visibility = _capture.IsRunning ? Visibility.Collapsed : Visibility.Visible;
        if (!_warnActive) { UpdateMaster(); }
    }

    private void UpdateMaster()
    {
        string s; Brush led, fg;
        if (_uplinkLive) { s = "● GHOST LIVE"; led = Red; fg = Red; }
        else if (_cloak == CloakSt.Engaged) { s = "CLOAKED"; led = Red; fg = Txt; }
        else if (_cloak == CloakSt.Tested) { s = "CLOAK TEST"; led = Txt; fg = Txt; }
        else if (_sensor == SensorSt.Online) { s = "SENSOR ONLINE"; led = Txt; fg = Txt; }
        else if (_sensor >= SensorSt.Acquiring) { s = "ACQUIRING"; led = Txt; fg = Txt; }
        else if (_sensor == SensorSt.Powered) { s = "SENSOR STANDBY"; led = Dim; fg = Txt; }
        else { s = "SYSTEM SAFE"; led = Dim; fg = Dim; }
        MasterStatus.Text = s; MasterStatus.Foreground = fg; MasterLed.Fill = led;
    }

    private void Led(TextBlock led, int state)
        => led.Foreground = state == 2 ? Red : (state == 1 ? Txt : Dim);

    private void Check(TextBlock t, string label, bool ok)
    {
        t.Text = (ok ? "● " : "○ ") + label;
        t.Foreground = ok ? Red : Dim;
    }

    // ===== warning watchdog =====
    private void OnWarnTick(object? sender, EventArgs e)
    {
        bool exposed = _uplinkLive && (!_mosaicEnabled || (_lastFaceCount == 0 && !_paranoid));
        if (exposed)
        {
            _warnActive = true; _blink = !_blink;
            MasterLed.Fill = _blink ? Red : Dim;
            MasterStatus.Text = "⚠ EXPOSED"; MasterStatus.Foreground = Red;
            if (_blink) { Sfx.Alarm(); }
        }
        else if (_warnActive) { _warnActive = false; UpdateMaster(); }
    }

    // ===== detector =====
    private void EnsureDetector()
    {
        if (_detectorInit) { return; }
        _detectorInit = true;
        int n = _detRate; bool gpu = _useGpu; float th = _sensitivity / 100f;
        Task.Run(() =>
        {
            var path = FindModel();
            if (path is null) { Dispatcher.BeginInvoke(() => { SetStatus("no model - covering whole frame (run get-model.ps1)"); AccelText.Text = "N/A"; }); return; }
            try
            {
                var d = new UltraFaceDetector(path, gpu) { ScoreThreshold = th };
                _detector = d; _tracker = new FaceTracker(d) { DetectEveryNFrames = n };
                Dispatcher.BeginInvoke(() => AccelText.Text = d.UsingGpu ? ShortGpu(d.GpuName) : "CPU");
            }
            catch (Exception ex) { Dispatcher.BeginInvoke(() => { SetStatus($"detector failed ({ex.Message})"); AccelText.Text = "N/A"; }); }
        });
    }

    private static string? FindModel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var p = Path.Combine(dir.FullName, "models", "version-RFB-320.onnx");
            if (File.Exists(p)) { return p; }
            dir = dir.Parent;
        }
        return null;
    }

    // ===== pipeline =====
    private void OnHudTick(object? sender, EventArgs e)
    {
        int frames = Interlocked.Exchange(ref _frameCount, 0);
        double secs = _fpsClock.Elapsed.TotalSeconds; _fpsClock.Restart();
        double fps = secs > 0 ? frames / secs : 0; _currentFps = (int)Math.Round(fps);
        FpsText.Text = $"{fps:0} FPS  ·  {_lastFaceCount} FACE(S)";
    }

    private void OnFrameReady(Mat frame)
    {
        Interlocked.Increment(ref _frameCount);
        IReadOnlyList<Rect> hudFaces = System.Array.Empty<Rect>();

        if (_mosaicEnabled)
        {
            var tracker = _tracker;
            if (tracker is not null)
            {
                var faces = tracker.Update(frame);
                _lastFaceCount = faces.Count;
                if (faces.Count > 0) { var p = PadFaces(faces, frame.Size()); _mosaic.ApplyRegions(frame, p); hudFaces = p; }
                else if (_paranoid) { _mosaic.ApplyFullFrame(frame); }
            }
            else { _mosaic.ApplyFullFrame(frame); }
        }
        else { _lastFaceCount = 0; }

        if (_outputEffect != OutputEffect.None) { FrameEffects.Apply(frame, _outputEffect); }

        // MIRROR TEXT ONLY: flip -> draw -> flip back. The video ends up in its
        // original orientation while the drawn elements come out pre-mirrored, so
        // they read correctly on a display that mirrors the picture.
        if (_mirrorText)
        {
            OpenCvSharp.Cv2.Flip(frame, frame, OpenCvSharp.FlipMode.Y);
            DrawElements(frame, MirrorRects(hudFaces, frame.Width));
            OpenCvSharp.Cv2.Flip(frame, frame, OpenCvSharp.FlipMode.Y);
        }
        else
        {
            DrawElements(frame, hudFaces);
        }

        // MIRROR flips the whole composed picture (video + text).
        if (_mirror) { OpenCvSharp.Cv2.Flip(frame, frame, OpenCvSharp.FlipMode.Y); }

        lock (_sinkLock) { _vcamSink?.WriteFrame(frame); }
        lock (_frameLock) { _pendingProcessed?.Dispose(); _pendingProcessed = frame; }
    }

    // Draw every overlay element (HUD, custom overlays, watermark) onto a frame.
    private void DrawElements(Mat frame, IReadOnlyList<Rect> hudFaces)
    {
        if (_hudEnabled)
        {
            HudOverlay.Draw(frame, hudFaces, _mosaicEnabled, _uplinkLive, _currentFps, _lastFaceCount);
        }
        lock (_overlayLock)
        {
            if (_overlays.Count > 0) { OverlayCompositor.DrawAll(frame, _overlays); }
            if (_watermark) { OverlayCompositor.Watermark(frame, _watermarkName); }
        }
    }

    // Mirror face rects horizontally so HUD lock brackets land on the face in the
    // flipped drawing pass.
    private static IReadOnlyList<Rect> MirrorRects(IReadOnlyList<Rect> src, int width)
    {
        if (src.Count == 0) { return src; }
        var outp = new List<Rect>(src.Count);
        foreach (var r in src) { outp.Add(new Rect(width - r.X - r.Width, r.Y, r.Width, r.Height)); }
        return outp;
    }

    private static IReadOnlyList<Rect> PadFaces(IReadOnlyList<Rect> faces, Size bounds)
    {
        var padded = new List<Rect>(faces.Count);
        foreach (var f in faces)
        {
            int px = f.Width / 5, py = f.Height / 5;
            var r = new Rect(f.X - px, f.Y - py, f.Width + 2 * px, f.Height + 2 * py);
            padded.Add(r.Intersect(new Rect(0, 0, bounds.Width, bounds.Height)));
        }
        return padded;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        Mat? processed;
        lock (_frameLock) { processed = _pendingProcessed; _pendingProcessed = null; }
        if (processed is null) { return; }
        using (processed)
        {
            if (_procBitmap is null || _procBitmap.PixelWidth != processed.Width || _procBitmap.PixelHeight != processed.Height)
            {
                _procBitmap = new WriteableBitmap(processed.Width, processed.Height, 96, 96, PixelFormats.Bgr24, null);
                DockPreview.Source = _procBitmap;
            }
            WriteableBitmapConverter.ToWriteableBitmap(processed, _procBitmap);
            _popup?.ShowBitmap(_procBitmap);
            _editor?.ShowBitmap(_procBitmap);
        }
    }

    private static string ShortGpu(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { return "GPU / DIRECTML"; }
        int rtx = name.IndexOf("RTX", StringComparison.OrdinalIgnoreCase);
        if (rtx >= 0) { var t = name[rtx..].Split(' '); return t.Length >= 2 ? $"{t[0]} {t[1]}" : name[rtx..]; }
        return name.Length > 22 ? name[..22] : name;
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
