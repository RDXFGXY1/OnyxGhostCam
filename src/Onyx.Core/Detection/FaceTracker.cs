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
    private int _hold;

    /// <summary>Run real detection once every N frames (1 = every frame). Clamped to >= 1.</summary>
    public int DetectEveryNFrames { get; set; } = 3;

    /// <summary>Position smoothing factor for matched boxes, 0..1 (higher = snappier).</summary>
    public double Smoothing { get; set; } = 0.5;

    /// <summary>
    /// Cover latch: how many frames to keep covering the last known boxes after the
    /// detector stops finding a face. A single missed detection (blink, head turn,
    /// motion blur) would otherwise expose the face for that frame. 0 disables.
    /// </summary>
    public int LatchFrames { get; set; } = 15;

    /// <summary>True while the boxes are being held open by the latch, not detected.</summary>
    public bool IsLatched { get; private set; }

    public FaceTracker(IFaceDetector detector) => _detector = detector;

    /// <summary>Returns the current face boxes for this frame (detected or carried over).</summary>
    public IReadOnlyList<Rect> Update(Mat frame)
    {
        int n = Math.Max(1, DetectEveryNFrames);
        if (_frame++ % n == 0)
        {
            var detected = _detector.Detect(frame);
            if (detected.Count > 0)
            {
                _last = Smooth(_last, detected);
                _hold = Math.Max(0, LatchFrames);
                IsLatched = false;
            }
            else if (_hold > 0 && _last.Count > 0)
            {
                // Lost them — hold the last known position rather than exposing.
                // Grow the box a little on the way in: we most likely lost the face
                // because it moved, so cover a wider area while we wait.
                if (!IsLatched) { _last = Grow(_last, 0.12, frame.Size()); }
                IsLatched = true;
            }
            else
            {
                _last = Array.Empty<Rect>();
                IsLatched = false;
            }
        }

        // Burn the latch down per frame, not per detection pass, so LatchFrames
        // means the same thing at any scan rate.
        if (IsLatched && --_hold <= 0)
        {
            _last = Array.Empty<Rect>();
            IsLatched = false;
        }
        return _last;
    }

    /// <summary>Drops all tracked state (use when the cloak is disengaged).</summary>
    public void Reset()
    {
        _last = Array.Empty<Rect>();
        _hold = 0;
        IsLatched = false;
    }

    private static List<Rect> Grow(IReadOnlyList<Rect> boxes, double by, Size bounds)
    {
        var outp = new List<Rect>(boxes.Count);
        var limit = new Rect(0, 0, bounds.Width, bounds.Height);
        foreach (var r in boxes)
        {
            int dx = (int)(r.Width * by), dy = (int)(r.Height * by);
            outp.Add(new Rect(r.X - dx, r.Y - dy, r.Width + 2 * dx, r.Height + 2 * dy).Intersect(limit));
        }
        return outp;
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
