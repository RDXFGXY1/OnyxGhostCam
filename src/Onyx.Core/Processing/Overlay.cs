using OpenCvSharp;

namespace Onyx.Core.Processing;

public enum OverlayKind { Text, Image }

/// <summary>A user-added element (text or image) freely positioned on the output.</summary>
public sealed class Overlay : IDisposable
{
    public OverlayKind Kind { get; init; }
    public string Text { get; set; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>Top-left position, normalized 0..1 of frame width/height.</summary>
    public double Nx { get; set; } = 0.05;
    public double Ny { get; set; } = 0.05;

    /// <summary>Image: fraction of frame width. Text: font scale.</summary>
    public double Scale { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;

    /// <summary>Text colour (BGR).</summary>
    public Scalar Color { get; set; } = new(240, 240, 240);

    public Mat? Image { get; private set; }

    public string DisplayName => Kind == OverlayKind.Text
        ? (Text.Length > 18 ? Text[..18] + "…" : Text)
        : System.IO.Path.GetFileName(ImagePath);

    public static Overlay CreateText(string text)
        => new() { Kind = OverlayKind.Text, Text = text, Scale = 1.2 };

    public static Overlay CreateImage(string path)
    {
        var o = new Overlay { Kind = OverlayKind.Image, ImagePath = path, Scale = 0.25 };
        var img = Cv2.ImRead(path, ImreadModes.Unchanged);
        o.Image = img.Empty() ? null : img;
        return o;
    }

    public void Dispose() { Image?.Dispose(); Image = null; }
}

public static class OverlayCompositor
{
    private const HersheyFonts Font = HersheyFonts.HersheyDuplex;

    public static void DrawAll(Mat frame, IEnumerable<Overlay> overlays)
    {
        foreach (var o in overlays) { Draw(frame, o); }
    }

    private static void Draw(Mat frame, Overlay o)
    {
        double op = Math.Clamp(o.Opacity, 0, 1);
        if (op >= 0.99) { Render(frame, o); return; }
        if (op <= 0.01) { return; }

        var r = MeasureRect(o, frame.Width, frame.Height);
        if (r.Width < 1 || r.Height < 1) { return; }
        using var layer = frame.Clone();
        Render(layer, o);
        using var lr = new Mat(layer, r);
        using var fr = new Mat(frame, r);
        Cv2.AddWeighted(lr, op, fr, 1 - op, 0, fr);
    }

    private static void Render(Mat f, Overlay o)
    {
        if (o.Kind == OverlayKind.Text) { DrawText(f, o); }
        else { DrawImage(f, o); }
    }

    public static void Watermark(Mat frame, string name)
    {
        string t = $"GHOSTCAM // {name}".ToUpperInvariant();
        double sc = Math.Max(0.4, frame.Width / 1500.0);
        int th = Math.Max(1, (int)(sc * 2));
        var sz = Cv2.GetTextSize(t, Font, sc, th, out int baseline);

        int padX = 12, padY = 8;
        int bw = sz.Width + padX * 2, bh = sz.Height + baseline + padY * 2;
        int x = frame.Width - bw - 14, y = frame.Height - bh - 14;
        if (x < 0 || y < 0) { return; }
        var box = new Rect(x, y, bw, bh);

        // Translucent dark tag + red bracket border (brutalist HUD tag).
        using (var roi = new Mat(frame, box))
        using (var dark = new Mat(roi.Size(), roi.Type(), new Scalar(8, 8, 8)))
        {
            Cv2.AddWeighted(dark, 0.55, roi, 0.45, 0, roi);
        }
        Cv2.Rectangle(frame, box, new Scalar(30, 30, 205), 2, LineTypes.AntiAlias);
        // corner ticks
        int tk = 8;
        Cv2.Line(frame, new Point(x, y), new Point(x + tk, y), new Scalar(60, 60, 235), 3);
        Cv2.Line(frame, new Point(x + bw, y + bh), new Point(x + bw - tk, y + bh), new Scalar(60, 60, 235), 3);

        var org = new Point(x + padX, y + padY + sz.Height);
        Cv2.PutText(frame, t, org, Font, sc, new Scalar(235, 235, 235), th, LineTypes.AntiAlias);
    }

    /// <summary>Pixel bounding rect of an overlay, clamped to the frame.</summary>
    public static Rect MeasureRect(Overlay o, int fw, int fh)
    {
        int x = (int)(o.Nx * fw), y = (int)(o.Ny * fh);
        int w, h;
        if (o.Kind == OverlayKind.Text)
        {
            double sc = Math.Max(0.4, o.Scale);
            int th = Math.Max(1, (int)(sc * 2));
            var sz = Cv2.GetTextSize(o.Text.Length == 0 ? " " : o.Text, Font, sc, th, out int baseline);
            w = sz.Width; h = sz.Height + baseline;
        }
        else if (o.Image is { } img && !img.Empty())
        {
            w = Math.Clamp((int)(fw * o.Scale), 8, fw);
            h = Math.Clamp(w * img.Height / img.Width, 8, fh);
        }
        else { return new Rect(0, 0, 0, 0); }

        x = Math.Clamp(x, 0, Math.Max(0, fw - w));
        y = Math.Clamp(y, 0, Math.Max(0, fh - h));
        return new Rect(x, y, Math.Min(w, fw - x), Math.Min(h, fh - y));
    }

    private static void DrawText(Mat f, Overlay o)
    {
        if (string.IsNullOrEmpty(o.Text)) { return; }
        double sc = Math.Max(0.4, o.Scale);
        int th = Math.Max(1, (int)(sc * 2));
        var sz = Cv2.GetTextSize(o.Text, Font, sc, th, out int baseline);
        int x = Math.Clamp((int)(o.Nx * f.Width), 0, Math.Max(0, f.Width - sz.Width));
        int y = Math.Clamp((int)(o.Ny * f.Height), 0, Math.Max(0, f.Height - sz.Height - baseline));
        var org = new Point(x, y + sz.Height);
        Cv2.PutText(f, o.Text, new Point(org.X + 2, org.Y + 2), Font, sc, Scalar.Black, th + 1, LineTypes.AntiAlias);
        Cv2.PutText(f, o.Text, org, Font, sc, o.Color, th, LineTypes.AntiAlias);
    }

    private static void DrawImage(Mat f, Overlay o)
    {
        if (o.Image is null || o.Image.Empty()) { return; }
        int w = Math.Clamp((int)(f.Width * o.Scale), 8, f.Width - 2);
        int h = Math.Clamp(w * o.Image.Height / o.Image.Width, 8, f.Height - 2);
        int x = Math.Clamp((int)(o.Nx * f.Width), 0, f.Width - w);
        int y = Math.Clamp((int)(o.Ny * f.Height), 0, f.Height - h);

        using var resized = o.Image.Resize(new Size(w, h));
        using var region = new Mat(f, new Rect(x, y, w, h));

        if (resized.Channels() == 4)
        {
            Cv2.Split(resized, out Mat[] ch);
            try
            {
                using var fg = new Mat();
                Cv2.Merge(new[] { ch[0], ch[1], ch[2] }, fg);
                using var alpha3 = new Mat();
                Cv2.CvtColor(ch[3], alpha3, ColorConversionCodes.GRAY2BGR);
                using var aF = new Mat(); alpha3.ConvertTo(aF, MatType.CV_32FC3, 1.0 / 255);
                using var invA = new Mat(); Cv2.Subtract(new Scalar(1, 1, 1), aF, invA);
                using var fgF = new Mat(); fg.ConvertTo(fgF, MatType.CV_32FC3);
                using var bgF = new Mat(); region.ConvertTo(bgF, MatType.CV_32FC3);
                using var t1 = new Mat(); Cv2.Multiply(fgF, aF, t1);
                using var t2 = new Mat(); Cv2.Multiply(bgF, invA, t2);
                using var outF = new Mat(); Cv2.Add(t1, t2, outF);
                outF.ConvertTo(region, MatType.CV_8UC3);
            }
            finally { foreach (var c in ch) { c.Dispose(); } }
        }
        else { resized.CopyTo(region); }
    }
}
