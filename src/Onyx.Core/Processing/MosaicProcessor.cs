using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>
/// Applies a heavy mosaic (pixelation) effect by downscaling then upscaling with
/// nearest-neighbour interpolation. Works in place on OpenCV <see cref="Mat"/> frames.
/// </summary>
/// <summary>How a face region is covered. On-brand Ghost Cam options.</summary>
public enum CoverStyle { Mosaic, Black, Ghost, Censored, Image, Text }

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

    /// <summary>User-supplied mask drawn over the face (CoverStyle.Image).</summary>
    public Mat? CoverImage { get; private set; }
    public string CoverImagePath { get; private set; } = string.Empty;

    /// <summary>Word stamped over the face (CoverStyle.Text).</summary>
    public string CoverText { get; set; } = "NOPE";

    /// <summary>Loads a mask image (PNG transparency supported). Returns false if unreadable.</summary>
    public bool LoadCoverImage(string path)
    {
        var img = Cv2.ImRead(path, ImreadModes.Unchanged);
        if (img.Empty()) { img.Dispose(); return false; }
        CoverImage?.Dispose();
        CoverImage = img;
        CoverImagePath = path;
        return true;
    }

    public void ClearCoverImage()
    {
        CoverImage?.Dispose();
        CoverImage = null;
        CoverImagePath = string.Empty;
    }

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
            case CoverStyle.Image: DrawCoverImage(r); break;
            case CoverStyle.Text: DrawCoverText(r); break;
            default: Pixelate(r); break;
        }
    }

    // User-uploaded mask, scaled to cover the face. Transparent PNGs blend over a
    // pixelated backing so nothing shows through the see-through parts.
    private void DrawCoverImage(Mat r)
    {
        if (CoverImage is null || CoverImage.Empty()) { Pixelate(r); return; }

        Pixelate(r);   // privacy backing behind any transparency

        // Stretch the mask to fill the whole covered region, so it occupies
        // exactly the area that would otherwise be blurred.
        using var resized = CoverImage.Resize(r.Size());
        using var region = new Mat(r, new Rect(0, 0, r.Width, r.Height));

        if (resized.Channels() == 4)
        {
            Cv2.Split(resized, out Mat[] ch);
            try
            {
                using var fg = new Mat();
                Cv2.Merge(new[] { ch[0], ch[1], ch[2] }, fg);
                using var a3 = new Mat();
                Cv2.CvtColor(ch[3], a3, ColorConversionCodes.GRAY2BGR);
                using var aF = new Mat(); a3.ConvertTo(aF, MatType.CV_32FC3, 1.0 / 255);
                using var inv = new Mat(); Cv2.Subtract(new Scalar(1, 1, 1), aF, inv);
                using var fgF = new Mat(); fg.ConvertTo(fgF, MatType.CV_32FC3);
                using var bgF = new Mat(); region.ConvertTo(bgF, MatType.CV_32FC3);
                using var t1 = new Mat(); Cv2.Multiply(fgF, aF, t1);
                using var t2 = new Mat(); Cv2.Multiply(bgF, inv, t2);
                using var sum = new Mat(); Cv2.Add(t1, t2, sum);
                sum.ConvertTo(region, MatType.CV_8UC3);
            }
            finally { foreach (var c in ch) { c.Dispose(); } }
        }
        else { resized.CopyTo(region); }
    }

    // Fills the face with a solid block and stamps the user's word across it.
    private void DrawCoverText(Mat r)
    {
        r.SetTo(Scalar.Black);
        var text = string.IsNullOrWhiteSpace(CoverText) ? "NOPE" : CoverText;
        int w = r.Width, h = r.Height;

        // Grow the font until the text spans ~90% of the face width.
        double scale = 0.4;
        int thick = 1;
        for (double s = 0.4; s <= 12.0; s += 0.1)
        {
            int t = Math.Max(1, (int)(s * 1.6));
            var sz = Cv2.GetTextSize(text, HersheyFonts.HersheyDuplex, s, t, out _);
            if (sz.Width > w * 0.9 || sz.Height > h * 0.8) { break; }
            scale = s; thick = t;
        }

        var size = Cv2.GetTextSize(text, HersheyFonts.HersheyDuplex, scale, thick, out int baseline);
        var org = new Point((w - size.Width) / 2, (h + size.Height) / 2);
        Cv2.PutText(r, text, org, HersheyFonts.HersheyDuplex, scale,
            new Scalar(235, 235, 235), thick, LineTypes.AntiAlias);
        Cv2.Rectangle(r, new Rect(0, 0, w - 1, h - 1), new Scalar(30, 30, 200),
            Math.Max(2, w / 50));
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
