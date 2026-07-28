using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp.WpfExtensions;
using Onyx.Core.Capture;
using Onyx.Core.Detection;
using Onyx.Core.Processing;
using Onyx.Core.Settings;
using Mat = OpenCvSharp.Mat;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Onyx.App.Views;

public partial class MainWindow : Window
{
    private readonly WebcamCapture _capture = new();
    private readonly MosaicProcessor _mosaic = new();
    private readonly object _frameLock = new();
    private Mat? _pendingRaw;
    private Mat? _pendingProcessed;
    private WriteableBitmap? _rawBitmap;
    private WriteableBitmap? _procBitmap;
    private volatile bool _mosaicEnabled;

    private IFaceDetector? _detector;
    private FaceTracker? _tracker;
    private volatile bool _detectorInit;
    private volatile int _lastFaceCount;

    private int _frameCount;
    private readonly System.Diagnostics.Stopwatch _fpsClock = System.Diagnostics.Stopwatch.StartNew();
    private readonly DispatcherTimer _hudTimer;
    private readonly OnyxSettings _settings = OnyxSettings.Load();

    public MainWindow()
    {
        InitializeComponent();

        _capture.FrameReady += OnFrameReady;
        _capture.Error += msg => Dispatcher.BeginInvoke(() => SetStatus($"error: {msg}"));

        CompositionTarget.Rendering += OnRendering;

        _hudTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _hudTimer.Tick += OnHudTick;
        _hudTimer.Start();

        ApplySettingsToUi();

        Closed += (_, _) => { SaveSettings(); _hudTimer.Stop(); _capture.Dispose(); _detector?.Dispose(); };
    }

    private void ApplySettingsToUi()
    {
        StrengthSlider.Value = _settings.MosaicBlockSize;
        DetectSlider.Value = _settings.DetectEveryN;
        CameraSlider.Value = _settings.CameraIndex;
        HdToggle.IsChecked = _settings.Height >= 1080;
        GpuToggle.IsChecked = _settings.UseGpu;
        ShieldOnStartToggle.IsChecked = _settings.ShieldOnStart;
        MosaicToggle.IsChecked = _settings.ShieldOnStart; // triggers OnMosaicToggle
    }

    private void SaveSettings()
    {
        _settings.MosaicBlockSize = (int)StrengthSlider.Value;
        _settings.DetectEveryN = (int)DetectSlider.Value;
        _settings.CameraIndex = (int)CameraSlider.Value;
        _settings.Width = HdToggle.IsChecked == true ? 1920 : 1280;
        _settings.Height = HdToggle.IsChecked == true ? 1080 : 720;
        _settings.UseGpu = GpuToggle.IsChecked == true;
        _settings.ShieldOnStart = ShieldOnStartToggle.IsChecked == true;
        _settings.Save();
    }

    private void OnHudTick(object? sender, EventArgs e)
    {
        int frames = Interlocked.Exchange(ref _frameCount, 0);
        double secs = _fpsClock.Elapsed.TotalSeconds;
        _fpsClock.Restart();
        double fps = secs > 0 ? frames / secs : 0;
        FpsText.Text = $"{fps:0} FPS  ·  {_lastFaceCount} FACE(S)";
    }

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        _capture.CameraIndex = (int)CameraSlider.Value;
        _capture.RequestedWidth = HdToggle.IsChecked == true ? 1920 : 1280;
        _capture.RequestedHeight = HdToggle.IsChecked == true ? 1080 : 720;

        _capture.Start();
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        SetStatus(_capture.IsRunning ? "capturing" : "failed to start");
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _capture.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        SetStatus("stopped");
    }

    private void OnMosaicToggle(object sender, RoutedEventArgs e)
    {
        _mosaicEnabled = MosaicToggle.IsChecked == true;
        if (_mosaicEnabled)
        {
            EnsureDetector();
            ShieldState.Text = "● SHIELD ACTIVE";
            ShieldState.Foreground = (Brush)FindResource("AccentRed");
        }
        else
        {
            ShieldState.Text = "● SHIELD OFF";
            ShieldState.Foreground = (Brush)FindResource("TextDim");
        }
    }

    private void OnStrengthChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        => _mosaic.BlockSize = (int)e.NewValue;

    private void OnDetectIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_tracker is not null) { _tracker.DetectEveryNFrames = (int)e.NewValue; }
    }

    private void OnCameraIndexChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CamIndexText is not null) { CamIndexText.Text = ((int)e.NewValue).ToString(); }
        // Applied on next Start (camera can't switch mid-capture).
    }

    // Load the face model once, off the UI thread (DirectML init can take a moment).
    private void EnsureDetector()
    {
        if (_detectorInit) { return; }
        _detectorInit = true;

        int detectEveryN = (int)DetectSlider.Value; // read UI values on the UI thread
        bool useGpu = GpuToggle.IsChecked == true;
        Task.Run(() =>
        {
            var path = FindModel();
            if (path is null)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SetStatus("no model - blurring whole frame (run get-model.ps1 for face-only)");
                    AccelText.Text = "N/A";
                });
                return;
            }
            try
            {
                var d = new UltraFaceDetector(path, useGpu);
                _detector = d;
                _tracker = new FaceTracker(d) { DetectEveryNFrames = detectEveryN };
                Dispatcher.BeginInvoke(() =>
                {
                    SetStatus("face detector ready");
                    AccelText.Text = d.UsingGpu ? "GPU / DIRECTML" : "CPU";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    SetStatus($"detector failed ({ex.Message}) - full-frame blur");
                    AccelText.Text = "N/A";
                });
            }
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

    // Capture thread: keep a raw copy for the left preview, process the frame in
    // place for the shielded output, then hand both to the UI.
    private void OnFrameReady(Mat frame)
    {
        Interlocked.Increment(ref _frameCount);

        Mat raw = frame.Clone();

        if (_mosaicEnabled)
        {
            var tracker = _tracker;
            if (tracker is not null)
            {
                var faces = tracker.Update(frame);
                _lastFaceCount = faces.Count;
                if (faces.Count > 0)
                {
                    _mosaic.ApplyRegions(frame, PadFaces(faces, frame.Size()));
                }
            }
            else
            {
                _mosaic.ApplyFullFrame(frame);
            }
        }
        else
        {
            _lastFaceCount = 0;
        }

        lock (_frameLock)
        {
            _pendingRaw?.Dispose();
            _pendingRaw = raw;
            _pendingProcessed?.Dispose();
            _pendingProcessed = frame;
        }
    }

    // Expand each face box ~20% so the mosaic fully covers the face + a margin.
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

    // UI thread: display whatever frames are pending.
    private void OnRendering(object? sender, EventArgs e)
    {
        Mat? raw, processed;
        lock (_frameLock)
        {
            raw = _pendingRaw; _pendingRaw = null;
            processed = _pendingProcessed; _pendingProcessed = null;
        }

        if (raw is not null)
        {
            using (raw) { UpdateBitmap(ref _rawBitmap, RawImage, raw); }
        }
        if (processed is not null)
        {
            using (processed) { UpdateBitmap(ref _procBitmap, PreviewImage, processed); }
        }
    }

    private static void UpdateBitmap(ref WriteableBitmap? bmp, Image target, Mat frame)
    {
        if (bmp is null || bmp.PixelWidth != frame.Width || bmp.PixelHeight != frame.Height)
        {
            bmp = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
            target.Source = bmp;
        }
        WriteableBitmapConverter.ToWriteableBitmap(frame, bmp);
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
