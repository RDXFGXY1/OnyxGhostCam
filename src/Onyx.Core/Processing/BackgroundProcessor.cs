using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>What happens to everything that isn't you.</summary>
public enum BackgroundMode { Off, Blur, Image, Color }

/// <summary>
/// Hides the room behind you: your face isn't the only thing that identifies you.
/// Posters, mail on the desk, the view out the window and who else is in the room
/// all leak just as much.
///
/// The "person" region is derived geometrically from the detected face boxes — a
/// head ellipse plus a shoulders/torso wedge running to the bottom of the frame —
/// rather than from a segmentation model. That keeps the app dependency-free and
/// fast, at the cost of a soft, approximate cutout: it reads as a shallow
/// depth-of-field blur rather than a hard green-screen key. If no face is found the
/// whole frame counts as background, so this fails closed.
/// </summary>
public sealed class BackgroundProcessor
{
    private Mat? _replacement;      // user image, original size
    private Mat? _replacementFitted; // cached, scaled to the current frame size

    private Mat? _mask, _maskBlur, _small, _bg;
    private Mat? _maskF, _invF, _fgF, _bgF, _t1, _t2, _sum;

    public BackgroundMode Mode { get; set; } = BackgroundMode.Off;

    /// <summary>Blur strength for <see cref="BackgroundMode.Blur"/>, 1..30.</summary>
    public int Strength { get; set; } = 12;

    /// <summary>Fill colour for <see cref="BackgroundMode.Color"/> (BGR).</summary>
    public Scalar FillColor { get; set; } = new(18, 18, 18);

    /// <summary>
    /// How wide the person cutout is drawn, as a percentage (50..160, 100 = default).
    /// Turn it down if the wall beside you stays sharp; turn it up if your shoulders
    /// are getting blurred. There's no setting that's right for every body and
    /// framing, which is why this is a dial rather than a constant.
    /// </summary>
    public int Tightness { get; set; } = 100;

    public string ReplacementPath { get; private set; } = string.Empty;

    /// <summary>Loads a backdrop image. Returns false if it can't be read.</summary>
    public bool LoadReplacement(string path)
    {
        var img = Cv2.ImRead(path, ImreadModes.Color);
        if (img.Empty()) { img.Dispose(); return false; }
        _replacement?.Dispose();
        _replacementFitted?.Dispose();
        _replacementFitted = null;
        _replacement = img;
        ReplacementPath = path;
        return true;
    }

    public void ClearReplacement()
    {
        _replacement?.Dispose(); _replacement = null;
        _replacementFitted?.Dispose(); _replacementFitted = null;
        ReplacementPath = string.Empty;
    }

    /// <summary>
    /// Replaces the background in place. <paramref name="faces"/> are the padded face
    /// boxes for this frame; an empty list means everything gets covered.
    /// </summary>
    public void Apply(Mat frame, IReadOnlyList<Rect> faces)
    {
        if (Mode == BackgroundMode.Off || frame.Empty()) { return; }

        var bg = BuildBackground(frame);
        if (bg is null) { return; }

        if (faces.Count == 0) { bg.CopyTo(frame); return; }

        Composite(frame, bg, BuildMask(frame, faces));
    }

    // Whatever shows through behind the person: a blurred copy of the frame, a
    // user image, or a flat colour.
    private Mat? BuildBackground(Mat frame)
    {
        switch (Mode)
        {
            case BackgroundMode.Blur:
                {
                    // Blur at quarter resolution: visually identical at these radii
                    // and far cheaper than a full-size gaussian every frame.
                    _small ??= new Mat();
                    _bg ??= new Mat();
                    var q = new Size(Math.Max(1, frame.Width / 4), Math.Max(1, frame.Height / 4));
                    Cv2.Resize(frame, _small, q, 0, 0, InterpolationFlags.Area);
                    int k = Math.Clamp(Strength, 1, 30) | 1;   // gaussian needs an odd kernel
                    Cv2.GaussianBlur(_small, _small, new Size(k, k), 0);
                    Cv2.Resize(_small, _bg, frame.Size(), 0, 0, InterpolationFlags.Linear);
                    return _bg;
                }

            case BackgroundMode.Image:
                {
                    if (_replacement is null || _replacement.Empty()) { return null; }
                    if (_replacementFitted is null
                        || _replacementFitted.Width != frame.Width
                        || _replacementFitted.Height != frame.Height)
                    {
                        _replacementFitted?.Dispose();
                        _replacementFitted = _replacement.Resize(frame.Size());
                    }
                    return _replacementFitted;
                }

            default:
                _bg ??= new Mat();
                if (_bg.Width != frame.Width || _bg.Height != frame.Height || _bg.Type() != frame.Type())
                {
                    _bg.Dispose();
                    _bg = new Mat(frame.Size(), frame.Type());
                }
                _bg.SetTo(FillColor);
                return _bg;
        }
    }

    // White where the person is, black where the room is. Built from the face box:
    // an ellipse over the head, then a wedge for neck/shoulders/torso down to the
    // bottom edge. Blurred afterwards so the edge feathers instead of cutting.
    private Mat BuildMask(Mat frame, IReadOnlyList<Rect> faces)
    {
        if (_mask is null || _maskBlur is null
            || _mask.Width != frame.Width || _mask.Height != frame.Height)
        {
            _mask?.Dispose(); _maskBlur?.Dispose();
            _mask = new Mat(frame.Size(), MatType.CV_8UC1);
            _maskBlur = new Mat(frame.Size(), MatType.CV_8UC1);
        }
        var mask = _mask;
        var blurred = _maskBlur;
        mask.SetTo(Scalar.Black);

        double k = Math.Clamp(Tightness, 50, 160) / 100.0;

        foreach (var f in faces)
        {
            int cx = f.X + f.Width / 2;
            int cy = f.Y + f.Height / 2;

            // Head. Kept close to the detector's box — every pixel of slack here is
            // a ring of sharp wall around your head.
            Cv2.Ellipse(mask, new Point(cx, cy),
                new Size((int)(f.Width * 0.58 * k), (int)(f.Height * 0.72 * k)),
                0, 0, 360, Scalar.White, -1);

            // Shoulders: a trapezoid opening out from the jaw to the frame bottom.
            int neckY = f.Y + (int)(f.Height * 0.72);
            int halfNeck = (int)(f.Width * 0.34 * k);
            int halfShoulder = (int)(f.Width * 1.05 * k);
            int shoulderY = Math.Min(frame.Height, neckY + (int)(f.Height * 0.95));

            var body = new[]
            {
                new Point(cx - halfNeck, neckY),
                new Point(cx + halfNeck, neckY),
                new Point(cx + halfShoulder, shoulderY),
                new Point(cx + halfShoulder, frame.Height),
                new Point(cx - halfShoulder, frame.Height),
                new Point(cx - halfShoulder, shoulderY),
            };
            Cv2.FillConvexPoly(mask, body, Scalar.White, LineTypes.AntiAlias);
        }

        // Feather. The kernel scales with frame size so the softness looks the same
        // at 720p and 1080p.
        int fk = (Math.Max(9, frame.Width / 40)) | 1;
        Cv2.GaussianBlur(mask, blurred, new Size(fk, fk), 0);
        return blurred;
    }

    // frame = frame*mask + bg*(1-mask), in float so the feathered edge blends.
    private void Composite(Mat frame, Mat bg, Mat mask)
    {
        _maskF ??= new Mat(); _invF ??= new Mat();
        _fgF ??= new Mat(); _bgF ??= new Mat();
        _t1 ??= new Mat(); _t2 ??= new Mat(); _sum ??= new Mat();

        using var mask3 = new Mat();
        Cv2.CvtColor(mask, mask3, ColorConversionCodes.GRAY2BGR);
        mask3.ConvertTo(_maskF, MatType.CV_32FC3, 1.0 / 255);
        Cv2.Subtract(new Scalar(1, 1, 1), _maskF, _invF);

        frame.ConvertTo(_fgF, MatType.CV_32FC3);
        bg.ConvertTo(_bgF, MatType.CV_32FC3);
        Cv2.Multiply(_fgF, _maskF, _t1);
        Cv2.Multiply(_bgF, _invF, _t2);
        Cv2.Add(_t1, _t2, _sum);
        _sum.ConvertTo(frame, MatType.CV_8UC3);
    }

    public void Dispose()
    {
        _replacement?.Dispose(); _replacementFitted?.Dispose();
        _mask?.Dispose(); _maskBlur?.Dispose(); _small?.Dispose(); _bg?.Dispose();
        _maskF?.Dispose(); _invF?.Dispose(); _fgF?.Dispose(); _bgF?.Dispose();
        _t1?.Dispose(); _t2?.Dispose(); _sum?.Dispose();
    }
}
