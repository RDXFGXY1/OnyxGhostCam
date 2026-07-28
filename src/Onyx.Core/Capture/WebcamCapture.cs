using OpenCvSharp;

namespace Onyx.Core.Capture;

/// <summary>
/// Captures frames from a webcam on a background thread using OpenCV.
/// Raises <see cref="FrameReady"/> for each frame; the handler receives a fresh
/// <see cref="Mat"/> and owns it (must dispose it when done).
/// </summary>
public sealed class WebcamCapture : IDisposable
{
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    /// <summary>Raised on the capture thread for each grabbed frame (BGR). Caller disposes the Mat.</summary>
    public event Action<Mat>? FrameReady;

    /// <summary>Raised on the capture thread if the camera fails to open or read.</summary>
    public event Action<string>? Error;

    public int CameraIndex { get; set; }
    public int RequestedWidth { get; set; } = 1280;
    public int RequestedHeight { get; set; } = 720;

    public bool IsRunning => _thread is { IsAlive: true };

    public void Start()
    {
        if (IsRunning) { return; }

        _capture = new VideoCapture(CameraIndex, VideoCaptureAPIs.DSHOW);
        if (!_capture.IsOpened())
        {
            _capture.Dispose();
            _capture = null;
            Error?.Invoke($"Could not open camera index {CameraIndex}.");
            return;
        }

        _capture.Set(VideoCaptureProperties.FrameWidth, RequestedWidth);
        _capture.Set(VideoCaptureProperties.FrameHeight, RequestedHeight);

        _cts = new CancellationTokenSource();
        _thread = new Thread(() => CaptureLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "OnyxCapture",
        };
        _thread.Start();
    }

    private void CaptureLoop(CancellationToken token)
    {
        using var frame = new Mat();
        while (!token.IsCancellationRequested)
        {
            if (_capture is null || !_capture.Read(frame) || frame.Empty())
            {
                Error?.Invoke("Camera read failed / stream ended.");
                break;
            }

            // Hand the consumer its own copy so our reused buffer stays safe.
            FrameReady?.Invoke(frame.Clone());
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _thread?.Join(500);
        _thread = null;

        _capture?.Dispose();
        _capture = null;

        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();
}
