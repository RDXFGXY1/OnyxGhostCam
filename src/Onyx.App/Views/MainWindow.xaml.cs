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
    private readonly BackgroundProcessor _background = new();
    private readonly object _frameLock = new();
    private Mat? _pendingProcessed;
    private WriteableBitmap? _procBitmap;
    private volatile bool _mosaicEnabled;

    private IFaceDetector? _detector;
    private FaceTracker? _tracker;
    private volatile bool _detectorInit;
    private volatile int _lastFaceCount;
    private volatile bool _latched;

    private ObsVirtualCameraSink? _vcamSink;
    private readonly object _sinkLock = new();

    private OutputPreviewWindow? _popup;
    private OverlayEditorWindow? _editor;
    private volatile bool _paranoid = true;
    private volatile OutputEffect _outputEffect = OutputEffect.None;
    private volatile bool _hudEnabled;
    private volatile int _currentFps;

    private int _strength = 16, _sensitivity = 60, _detRate = 3, _camIndex;
    private int _latch = 15, _bgStrength = 12, _bgTight = 100, _watchdogMs = 1200;
    private bool _hd, _useGpu = true;

    // Watchdog: the capture thread stamps this on every delivered frame; a timer
    // thread blacks out the uplink if it goes stale.
    private long _lastFrameTicks;
    private volatile int _frameW = 1280, _frameH = 720;
    private System.Threading.Timer? _watchdog;
    private volatile bool _stalled;

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

        // Deliberately a plain threading timer, not a DispatcherTimer: it has to keep
        // running even if the UI thread is busy or hung.
        _watchdog = new System.Threading.Timer(OnWatchdogTick, null, 250, 250);

        ApplySettingsToUi();
        Refresh();

        // Launched by the "start with Windows" Run entry: go straight to the tray
        // instead of throwing a window up at every sign-in.
        if (Environment.GetCommandLineArgs().Any(a =>
                a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))
        {
            Loaded += (_, _) => { _tray.Show("GhostCam is running — double-click to open."); Hide(); };
        }

        _tray.RestoreRequested += RestoreFromTray;
        _tray.ExitRequested += Close;
        _tray.UpdateRequested += () => Dispatcher.BeginInvoke(ShowUpdateWindow);

        // Runs in the background; must not hold up the window opening.
        if (_settings.CheckForUpdates) { RunUpdateCheckInBackground(); }

        Closed += (_, _) =>
        {
            _tray.Dispose();
            SaveSettings(); _hudTimer.Stop(); _warnTimer.Stop(); _holdTimer.Stop();
            _watchdog?.Dispose(); _watchdog = null;
            _capture.Dispose(); _detector?.Dispose(); _background.Dispose();
            _popup?.Close(); _editor?.Close();
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
        _mosaic.Style = (CoverStyle)Math.Clamp(_settings.CoverStyle, 0, 5);
        _mosaic.BlockSize = _strength;
        _mosaic.CoverText = string.IsNullOrWhiteSpace(_settings.CoverText) ? "NOPE" : _settings.CoverText;
        if (!string.IsNullOrWhiteSpace(_settings.CoverImagePath) && File.Exists(_settings.CoverImagePath))
        {
            _mosaic.LoadCoverImage(_settings.CoverImagePath);
        }
        _outputEffect = (OutputEffect)Math.Clamp(_settings.OutputEffect, 0, 2);
        _hudEnabled = _settings.Hud;

        _latch = Math.Clamp(_settings.LatchFrames, 0, 120);
        _watchdogMs = _settings.WatchdogMs <= 0 ? 0 : Math.Clamp(_settings.WatchdogMs, 300, 5000);
        _bgStrength = Math.Clamp(_settings.BackgroundStrength, 1, 30);
        _bgTight = Math.Clamp(_settings.BackgroundTightness, 50, 160);
        _background.Mode = (BackgroundMode)Math.Clamp(_settings.BackgroundMode, 0, 3);
        _background.Strength = _bgStrength;
        _background.Tightness = _bgTight;
        if (!string.IsNullOrWhiteSpace(_settings.BackgroundImagePath) && File.Exists(_settings.BackgroundImagePath))
        {
            _background.LoadReplacement(_settings.BackgroundImagePath);
        }
        BgRadio(_background.Mode).IsChecked = true;
        LatchVal.Text = _latch.ToString();
        DogVal.Text = _watchdogMs == 0 ? "OFF" : _watchdogMs.ToString();
        BgStrengthVal.Text = _bgStrength.ToString();
        BgTightVal.Text = _bgTight.ToString();
        BgNameText.Text = _background.ReplacementPath.Length > 0
            ? Path.GetFileName(_background.ReplacementPath) : "no backdrop chosen";
        UpdateBgRows();
        RefreshProfileNames();

        StrengthVal.Text = _strength.ToString(); SensVal.Text = _sensitivity.ToString();
        RateVal.Text = _detRate.ToString(); CamVal.Text = _camIndex.ToString();
        _mirror = _settings.Mirror; MirrorSwitch.IsChecked = _mirror;
        _mirrorText = _settings.MirrorText; MirrorTextSwitch.IsChecked = _mirrorText;
        GpuSwitch.IsChecked = _useGpu; ParanoidSwitch.IsChecked = _paranoid;
        BootSwitch.IsChecked = _settings.ShieldOnStart; HudSwitch.IsChecked = _hudEnabled;
        // Read the live registry state rather than a stored bool: the user may have
        // cleared it from Task Manager's Startup tab behind our back.
        _arming = true;
        StartupSwitch.IsChecked = StartupRegistration.IsEnabled();
        _arming = false;
        Sfx.Enabled = _settings.Sound; SoundSwitch.IsChecked = _settings.Sound;
        UpdateSwitch.IsChecked = _settings.CheckForUpdates;
        (_hd ? Res1080 : Res720).IsChecked = true;
        CoverRadio(_mosaic.Style).IsChecked = true;
        FilterRadio(_outputEffect).IsChecked = true;
        CoverTextBox.Text = _mosaic.CoverText;
        MaskNameText.Text = _mosaic.CoverImage is not null
            ? Path.GetFileName(_mosaic.CoverImagePath) : "no mask loaded";
        UpdateCoverRows();

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
        _settings.CoverImagePath = _mosaic.CoverImagePath; _settings.CoverText = _mosaic.CoverText;
        _settings.OutputEffect = (int)_outputEffect; _settings.Hud = _hudEnabled;
        _settings.Sound = Sfx.Enabled;
        _settings.LatchFrames = _latch; _settings.WatchdogMs = _watchdogMs;
        _settings.BackgroundMode = (int)_background.Mode;
        _settings.BackgroundStrength = _bgStrength;
        _settings.BackgroundTightness = _bgTight;
        _settings.BackgroundImagePath = _background.ReplacementPath;
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
    {
        CoverStyle.Black => CoverBlack,
        CoverStyle.Ghost => CoverGhost,
        CoverStyle.Censored => CoverCensor,
        CoverStyle.Image => CoverImage,
        CoverStyle.Text => CoverTextMode,
        _ => CoverMosaic,
    };
    private RadioButton FilterRadio(OutputEffect e) => e switch
    { OutputEffect.Scanlines => FilterScan, OutputEffect.Glitch => FilterGlitch, _ => FilterNone };

    // ===== control-rail tabs =====
    // Only one page of the rail is visible at a time; the tab strip drives it.
    private void OnTab(object sender, RoutedEventArgs e)
    {
        // Fires once during InitializeComponent, before the panels exist.
        if (CloakContent is null) { return; }
        Sfx.Click();

        var tag = (sender as RadioButton)?.Tag as string ?? "cover";
        Show(CloakContent, tag == "cover");
        Show(OverlaysContent, tag == "overlay");
        Show(UplinkContent, tag == "output");
        Show(SensorContent, tag == "setup");
    }

    private static void Show(UIElement el, bool on)
        => el.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    // The big primary button: run the whole chain (sensor → cloak → uplink) or stop it.
    private void OnGoLive(object sender, RoutedEventArgs e)
    {
        Sfx.Click();
        if (_uplinkLive) { OnUplinkAbort(sender, e); return; }

        if (_sensor != SensorSt.Online)
        {
            TabSetup.IsChecked = true;
            SetStatus("start with SETUP — power the sensor, then ACQUIRE");
            return;
        }
        if (_cloak != CloakSt.Engaged)
        {
            TabCover.IsChecked = true;
            SetStatus("enable and engage the cloak before going live");
            return;
        }
        TabOutput.IsChecked = true;
        SetStatus("lift the arm guard, then hold BROADCAST");
    }

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
        TabCover.IsChecked = true; // walk them to the next step
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
        Sfx.Arm(); _cloak = CloakSt.Engaged; TabOutput.IsChecked = true; SetStatus("cloak engaged - finish on OUTPUT"); Refresh();
    }

    private void OnCloakAbort(object s, RoutedEventArgs e) { Sfx.Click(); CloakKill(); Refresh(); }

    private void CloakKill()
    {
        _cloak = CloakSt.Safe; ForceOff(CloakEnableSw); UplinkKill();
        _tracker?.Reset(); _latched = false;
    }

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
            case "latch": _latch = Math.Clamp(_latch + (up ? 5 : -5), 0, 120); LatchVal.Text = _latch.ToString(); if (_tracker is not null) { _tracker.LatchFrames = _latch; } break;
            case "bg": _bgStrength = Math.Clamp(_bgStrength + (up ? 2 : -2), 1, 30); BgStrengthVal.Text = _bgStrength.ToString(); _background.Strength = _bgStrength; break;
            case "tight": _bgTight = Math.Clamp(_bgTight + (up ? 5 : -5), 50, 160); BgTightVal.Text = _bgTight.ToString(); _background.Tightness = _bgTight; break;
            case "dog":
                // Step down past the minimum to reach 0 = disabled.
                _watchdogMs = up
                    ? Math.Clamp(_watchdogMs == 0 ? 300 : _watchdogMs + 200, 300, 5000)
                    : (_watchdogMs <= 300 ? 0 : _watchdogMs - 200);
                DogVal.Text = _watchdogMs == 0 ? "OFF" : _watchdogMs.ToString();
                break;
        }
    }

    // ===== background =====
    private RadioButton BgRadio(BackgroundMode m) => m switch
    {
        BackgroundMode.Blur => BgBlur,
        BackgroundMode.Image => BgImage,
        BackgroundMode.Color => BgColor,
        _ => BgOff,
    };

    private void OnBgSel(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        _background.Mode = (BackgroundMode)int.Parse((string)((RadioButton)s).Tag);
        UpdateBgRows();
        if (_background.Mode == BackgroundMode.Image && _background.ReplacementPath.Length == 0)
        {
            SetStatus("pick a backdrop image, or the background stays as-is");
        }
    }

    private void UpdateBgRows()
    {
        if (BgStrengthRow is null) { return; }
        var m = _background.Mode;
        bool on = m != BackgroundMode.Off;
        BgStrengthRow.Visibility = m == BackgroundMode.Blur ? Visibility.Visible : Visibility.Collapsed;
        BgImageRow.Visibility = m == BackgroundMode.Image ? Visibility.Visible : Visibility.Collapsed;
        BgTightRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        BgHint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUploadBackdrop(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a background image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
        };
        if (dlg.ShowDialog() != true) { return; }

        if (_background.LoadReplacement(dlg.FileName))
        {
            BgNameText.Text = Path.GetFileName(dlg.FileName);
            SetStatus("backdrop loaded");
        }
        else { SetStatus("could not read that image"); }
    }

    // ===== profiles =====
    private void RefreshProfileNames()
    {
        var p = _settings.Profiles;
        if (p.Count != 3) { return; }
        Prof0Name.Text = p[0].Name; Prof1Name.Text = p[1].Name; Prof2Name.Text = p[2].Name;
    }

    private void OnProfileLoad(object s, RoutedEventArgs e)
    {
        int i = int.Parse((string)((Button)s).Tag);
        if (i >= _settings.Profiles.Count) { return; }
        Sfx.Arm();
        ApplyProfile(_settings.Profiles[i]);
        SetStatus($"loaded profile: {_settings.Profiles[i].Name}");
    }

    private void OnProfileSave(object s, RoutedEventArgs e)
    {
        int i = int.Parse((string)((Button)s).Tag);
        if (i >= _settings.Profiles.Count) { return; }
        Sfx.Beep();
        var name = _settings.Profiles[i].Name;   // slot keeps its name
        _settings.Profiles[i] = CaptureProfile(name);
        _settings.Save();
        SetStatus($"saved current settings to: {name}");
    }

    private void OnProfilesReset(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        _settings.Profiles = OnyxSettings.DefaultProfiles();
        _settings.Save();
        RefreshProfileNames();
        SetStatus("profiles reset to defaults");
    }

    private OnyxSettings.Profile CaptureProfile(string name) => new()
    {
        Name = name,
        CoverStyle = (int)_mosaic.Style,
        MosaicBlockSize = _strength,
        DetectEveryN = _detRate,
        ScoreThreshold = _sensitivity / 100.0,
        ParanoidMode = _paranoid,
        LatchFrames = _latch,
        BackgroundMode = (int)_background.Mode,
        BackgroundStrength = _bgStrength,
        BackgroundTightness = _bgTight,
        BackgroundImagePath = _background.ReplacementPath,
        OutputEffect = (int)_outputEffect,
        Hud = _hudEnabled,
        Watermark = _watermark,
        CoverText = _mosaic.CoverText,
        CoverImagePath = _mosaic.CoverImagePath,
    };

    // Pushes a saved profile into both the live pipeline and the controls. Never
    // touches the sensor/cloak/uplink state machines — loading a profile must not
    // be able to put you on air.
    private void ApplyProfile(OnyxSettings.Profile p)
    {
        _strength = Math.Clamp(p.MosaicBlockSize, 4, 60);
        _detRate = Math.Clamp(p.DetectEveryN, 1, 8);
        _sensitivity = Math.Clamp((int)Math.Round(p.ScoreThreshold * 100), 30, 90);
        _latch = Math.Clamp(p.LatchFrames, 0, 120);
        _bgStrength = Math.Clamp(p.BackgroundStrength, 1, 30);
        _bgTight = Math.Clamp(p.BackgroundTightness, 50, 160);
        _paranoid = p.ParanoidMode;
        _hudEnabled = p.Hud;
        _watermark = p.Watermark;
        _outputEffect = (OutputEffect)Math.Clamp(p.OutputEffect, 0, 2);

        _mosaic.Style = (CoverStyle)Math.Clamp(p.CoverStyle, 0, 5);
        _mosaic.BlockSize = _strength;
        if (!string.IsNullOrWhiteSpace(p.CoverText)) { _mosaic.CoverText = p.CoverText; }
        if (!string.IsNullOrWhiteSpace(p.CoverImagePath) && File.Exists(p.CoverImagePath))
        {
            _mosaic.LoadCoverImage(p.CoverImagePath);
        }

        _background.Mode = (BackgroundMode)Math.Clamp(p.BackgroundMode, 0, 3);
        _background.Strength = _bgStrength;
        _background.Tightness = _bgTight;
        if (!string.IsNullOrWhiteSpace(p.BackgroundImagePath) && File.Exists(p.BackgroundImagePath))
        {
            _background.LoadReplacement(p.BackgroundImagePath);
        }

        if (_detector is not null) { _detector.ScoreThreshold = _sensitivity / 100f; }
        if (_tracker is not null) { _tracker.DetectEveryNFrames = _detRate; _tracker.LatchFrames = _latch; }

        // Mirror it all back into the controls.
        _arming = true;
        StrengthVal.Text = _strength.ToString();
        SensVal.Text = _sensitivity.ToString();
        RateVal.Text = _detRate.ToString();
        LatchVal.Text = _latch.ToString();
        BgStrengthVal.Text = _bgStrength.ToString();
        BgTightVal.Text = _bgTight.ToString();
        ParanoidSwitch.IsChecked = _paranoid;
        HudSwitch.IsChecked = _hudEnabled;
        WatermarkSwitch.IsChecked = _watermark;
        CoverRadio(_mosaic.Style).IsChecked = true;
        FilterRadio(_outputEffect).IsChecked = true;
        BgRadio(_background.Mode).IsChecked = true;
        CoverTextBox.Text = _mosaic.CoverText;
        MaskNameText.Text = _mosaic.CoverImage is not null
            ? Path.GetFileName(_mosaic.CoverImagePath) : "no mask loaded";
        BgNameText.Text = _background.ReplacementPath.Length > 0
            ? Path.GetFileName(_background.ReplacementPath) : "no backdrop chosen";
        _arming = false;

        UpdateCoverRows();
        UpdateBgRows();
        Refresh();
    }

    private void OnCoverSel(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        _mosaic.Style = (CoverStyle)int.Parse((string)((RadioButton)s).Tag);
        UpdateCoverRows();
    }

    // Show only the controls that belong to the selected cover mode.
    private void UpdateCoverRows()
    {
        if (MaskRow is null || CoverTextRow is null) { return; }
        MaskRow.Visibility = _mosaic.Style == CoverStyle.Image ? Visibility.Visible : Visibility.Collapsed;
        CoverTextRow.Visibility = _mosaic.Style == CoverStyle.Text ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUploadMask(object s, RoutedEventArgs e)
    {
        Sfx.Click();
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an image to cover your face",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
        };
        if (dlg.ShowDialog() != true) { return; }

        if (_mosaic.LoadCoverImage(dlg.FileName))
        {
            MaskNameText.Text = Path.GetFileName(dlg.FileName);
            SetStatus("mask loaded - transparent PNGs work best");
        }
        else
        {
            SetStatus("could not read that image");
        }
    }

    private void OnCoverTextChanged(object s, TextChangedEventArgs e)
    {
        if (CoverTextBox is null) { return; }
        _mosaic.CoverText = string.IsNullOrWhiteSpace(CoverTextBox.Text) ? "NOPE" : CoverTextBox.Text.Trim();
    }
    private void OnFilterSel(object s, RoutedEventArgs e) { Sfx.Click(); _outputEffect = (OutputEffect)int.Parse((string)((RadioButton)s).Tag); }
    private void OnResSel(object s, RoutedEventArgs e) { Sfx.Click(); _hd = (string)((RadioButton)s).Tag == "1080"; }
    private void OnParanoidSw(object s, RoutedEventArgs e) { Sfx.Click(); _paranoid = ParanoidSwitch.IsChecked == true; }
    private void OnGpuSw(object s, RoutedEventArgs e) { Sfx.Click(); _useGpu = GpuSwitch.IsChecked == true; }
    private void OnMirrorSw(object s, RoutedEventArgs e) { Sfx.Click(); _mirror = MirrorSwitch.IsChecked == true; }
    private void OnMirrorTextSw(object s, RoutedEventArgs e) { Sfx.Click(); _mirrorText = MirrorTextSwitch.IsChecked == true; }
    private void OnHudSw(object s, RoutedEventArgs e) { Sfx.Click(); _hudEnabled = HudSwitch.IsChecked == true; }
    private void OnSoundSw(object s, RoutedEventArgs e) { Sfx.Enabled = SoundSwitch.IsChecked == true; Sfx.Click(); }

    private void OnStartupSw(object s, RoutedEventArgs e)
    {
        if (_arming) { return; }
        Sfx.Click();
        bool want = StartupSwitch.IsChecked == true;
        if (StartupRegistration.Set(want))
        {
            SetStatus(want ? "GhostCam will start with Windows (to tray)" : "GhostCam will no longer start with Windows");
        }
        else
        {
            _arming = true; StartupSwitch.IsChecked = !want; _arming = false;
            SetStatus("could not change the startup setting");
        }
    }

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
        GoLiveText.Text = _uplinkLive ? "■ STOP BROADCAST" : "◤ GO LIVE";
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

    // ===== pipeline watchdog =====
    // If the capture thread dies, blocks on a wedged driver, or the camera is yanked
    // out mid-call, the OBS shared buffer would otherwise keep serving whatever
    // frame was last written — a still image of you, uncovered if the cloak hadn't
    // engaged yet. Fail closed: push black.
    private void OnWatchdogTick(object? _)
    {
        int limit = _watchdogMs;
        if (limit <= 0 || !_uplinkLive) { return; }

        long since = Environment.TickCount64 - Interlocked.Read(ref _lastFrameTicks);
        if (since < limit) { return; }

        try
        {
            lock (_sinkLock)
            {
                if (_vcamSink is null) { return; }
                using var black = new Mat(new Size(_frameW, _frameH), OpenCvSharp.MatType.CV_8UC3, OpenCvSharp.Scalar.Black);
                OpenCvSharp.Cv2.PutText(black, "SIGNAL LOST", new OpenCvSharp.Point(40, _frameH / 2),
                    OpenCvSharp.HersheyFonts.HersheyDuplex, 1.2, new OpenCvSharp.Scalar(40, 40, 200), 2,
                    OpenCvSharp.LineTypes.AntiAlias);
                _vcamSink.WriteFrame(black);
            }
        }
        catch
        {
            // The watchdog must never be the thing that takes the app down.
        }

        if (_stalled) { return; }
        _stalled = true;
        Dispatcher.BeginInvoke(() => SetStatus($"⚠ FEED STALLED ({since} ms) - output blacked out"));
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
                _detector = d;
                _tracker = new FaceTracker(d) { DetectEveryNFrames = n, LatchFrames = _latch };
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
        FpsText.Text = _latched
            ? $"{fps:0} FPS  ·  LATCHED"
            : $"{fps:0} FPS  ·  {_lastFaceCount} FACE(S)";
    }

    private void OnFrameReady(Mat frame)
    {
        Interlocked.Increment(ref _frameCount);
        IReadOnlyList<Rect> hudFaces = System.Array.Empty<Rect>();

        _frameW = frame.Width; _frameH = frame.Height;

        if (_mosaicEnabled)
        {
            var tracker = _tracker;
            if (tracker is not null)
            {
                var faces = tracker.Update(frame);
                _lastFaceCount = faces.Count;
                _latched = tracker.IsLatched;
                var padded = faces.Count > 0 ? PadFaces(faces, frame.Size()) : faces;

                // Background first: the cover is drawn on top so the face is never
                // blended with whatever is behind it.
                //
                // Note this gets the RAW boxes, not the padded ones. Padding exists to
                // make the *cover* generous; feeding it to the cutout as well stacks
                // two expansions and drags a ring of wall into the sharp region.
                _background.Apply(frame, faces);

                if (padded.Count > 0) { _mosaic.ApplyRegions(frame, padded); hudFaces = padded; }
                else if (_paranoid) { _mosaic.ApplyFullFrame(frame); }
            }
            else { _mosaic.ApplyFullFrame(frame); }
        }
        else
        {
            _lastFaceCount = 0; _latched = false;
            _background.Apply(frame, System.Array.Empty<Rect>());
        }

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

        // Stamped last, so it only counts once the frame has actually gone out.
        Interlocked.Exchange(ref _lastFrameTicks, Environment.TickCount64);
        _stalled = false;
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
