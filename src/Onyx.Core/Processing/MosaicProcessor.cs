using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>
/// Applies a heavy mosaic (pixelation) effect by downscaling then upscaling with
/// nearest-neighbour interpolation. Works in place on OpenCV <see cref="Mat"/> frames.
/// </summary>
/// <summary>How a face region is covered. On-brand Ghost Cam options.</summary>
public enum CoverStyle { Mosaic, Black, Ghost, Censored }

public sealed class MosaicProcessor
{
    private int _blockSize = 16;

    /// <summary>Pixel block size. Larger = stronger/blockier pixelation. Clamped to >= 2.</summary>
    public int BlockSize
    {
        get => _blockSize;
        set => _blockSize = Math.Max(2, value);
    }

    /// <summary>How covered regions are rendered.</summary>
    public CoverStyle Style { get; set; } = CoverStyle.Mosaic;

    /// <summary>Cover the entire frame in place.</summary>
    public void ApplyFullFrame(Mat frame)
    {
        if (frame.Empty()) { return; }
        Cover(frame);
    }

    /// <summary>Cover only the given regions (e.g. detected faces) in place.</summary>
    public void ApplyRegions(Mat frame, IReadOnlyList<Rect> regions)
    {
        if (frame.Empty()) { return; }
        var bounds = new Rect(0, 0, frame.Width, frame.Height);
        foreach (var r in regions)
        {
            var roi = r.Intersect(bounds);
            if (roi.Width < 2 || roi.Height < 2) { continue; }
            using var region = new Mat(frame, roi);
            Cover(region);
        }
    }

    private void Cover(Mat r)
    {
        switch (Style)
        {
            case CoverStyle.Black: r.SetTo(Scalar.Black); break;
            case CoverStyle.Ghost: DrawGhost(r); break;
            case CoverStyle.Censored: DrawCensored(r); break;
            default: Pixelate(r); break;
        }
    }

    // A little white ghost over a dark backing (the face is fully covered first).
    private static void DrawGhost(Mat r)
    {
        int w = r.Width, h = r.Height;
        r.SetTo(new Scalar(12, 12, 12));
        var white = new Scalar(238, 238, 238);
        var dark = new Scalar(14, 14, 14);

        // Body: an ellipse head merged with a rounded rectangle torso.
        Cv2.Ellipse(r, new Point(w / 2, (int)(h * 0.42)),
            new Size((int)(w * 0.34), (int)(h * 0.32)), 0, 180, 360, white, -1);
        Cv2.Rectangle(r, new Rect((int)(w * 0.16), (int)(h * 0.42), (int)(w * 0.68), (int)(h * 0.40)),
            white, -1);
        // Wavy bottom.
        int feet = 4, fw = (int)(w * 0.68 / feet);
        for (int i = 0; i < feet; i++)
        {
            Cv2.Circle(r, new Point((int)(w * 0.16) + fw / 2 + i * fw, (int)(h * 0.82)),
                fw / 2, white, -1);
        }
        // Eyes.
        int eye = Math.Max(2, w / 12);
        Cv2.Circle(r, new Point((int)(w * 0.40), (int)(h * 0.44)), eye, dark, -1);
        Cv2.Circle(r, new Point((int)(w * 0.60), (int)(h * 0.44)), eye, dark, -1);
    }

    // Tabloid black bar with "CENSORED".
    private static void DrawCensored(Mat r)
    {
        int w = r.Width, h = r.Height;
        r.SetTo(Scalar.Black);
        Cv2.Rectangle(r, new Rect(0, 0, w - 1, h - 1), new Scalar(30, 30, 200),
            Math.Max(2, w / 40));
        double scale = Math.Max(0.4, w / 240.0);
        int thick = Math.Max(1, (int)(scale * 2));
        var size = Cv2.GetTextSize("CENSORED", HersheyFonts.HersheyDuplex, scale, thick, out int baseline);
        var org = new Point((w - size.Width) / 2, (h + size.Height) / 2);
        Cv2.PutText(r, "CENSORED", org, HersheyFonts.HersheyDuplex, scale,
            new Scalar(235, 235, 235), thick, LineTypes.AntiAlias);
    }

    private void Pixelate(Mat target)
    {
        int w = Math.Max(1, target.Width / _blockSize);
        int h = Math.Max(1, target.Height / _blockSize);

        using var small = new Mat();
        Cv2.Resize(target, small, new Size(w, h), 0, 0, InterpolationFlags.Linear);
        Cv2.Resize(small, target, new Size(target.Width, target.Height), 0, 0, InterpolationFlags.Nearest);
    }
}
