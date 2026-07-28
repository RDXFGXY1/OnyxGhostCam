using OpenCvSharp;

namespace Onyx.Core.Detection;

/// <summary>
/// Runs the (relatively expensive) face detector only every N frames and reuses
/// the last boxes in between, smoothing box positions across detection passes to
/// avoid jitter. This is what lets us keep a high frame rate: detection cost is
/// amortised while the mosaic still tracks the face every frame.
/// </summary>
public sealed class FaceTracker
{
    private readonly IFaceDetector _detector;
    private long _frame;
    private IReadOnlyList<Rect> _last = Array.Empty<Rect>();

    /// <summary>Run real detection once every N frames (1 = every frame). Clamped to >= 1.</summary>
    public int DetectEveryNFrames { get; set; } = 3;

    /// <summary>Position smoothing factor for matched boxes, 0..1 (higher = snappier).</summary>
    public double Smoothing { get; set; } = 0.5;

    public FaceTracker(IFaceDetector detector) => _detector = detector;

    /// <summary>Returns the current face boxes for this frame (detected or carried over).</summary>
    public IReadOnlyList<Rect> Update(Mat frame)
    {
        int n = Math.Max(1, DetectEveryNFrames);
        if (_frame++ % n == 0)
        {
            var detected = _detector.Detect(frame);
            _last = Smooth(_last, detected);
        }
        return _last;
    }

    // Blend each newly-detected box toward its best-matching previous box so the
    // mosaic doesn't "pop" between detection passes.
    private List<Rect> Smooth(IReadOnlyList<Rect> previous, IReadOnlyList<Rect> current)
    {
        var result = new List<Rect>(current.Count);
        foreach (var c in current)
        {
            Rect? best = null;
            double bestIoU = 0.2; // require a minimum overlap to treat as the same face
            foreach (var p in previous)
            {
                double iou = IoU(p, c);
                if (iou > bestIoU) { bestIoU = iou; best = p; }
            }

            result.Add(best is { } prev ? Blend(prev, c, Smoothing) : c);
        }
        return result;
    }

    private static Rect Blend(Rect a, Rect b, double t)
    {
        int L(int x, int y) => (int)Math.Round(x + (y - x) * t);
        return new Rect(L(a.X, b.X), L(a.Y, b.Y), L(a.Width, b.Width), L(a.Height, b.Height));
    }

    private static double IoU(Rect a, Rect b)
    {
        var inter = a.Intersect(b);
        double interArea = (double)inter.Width * inter.Height;
        if (interArea <= 0) { return 0; }
        double union = (double)a.Width * a.Height + (double)b.Width * b.Height - interArea;
        return union <= 0 ? 0 : interArea / union;
    }
}
