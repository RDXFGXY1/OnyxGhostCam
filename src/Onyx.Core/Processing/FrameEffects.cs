using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>Full-frame output filters that match the brutalist HUD aesthetic.</summary>
public enum OutputEffect { None, Scanlines, Glitch }

public static class FrameEffects
{
    private static readonly Random _rng = new();

    public static void Apply(Mat frame, OutputEffect effect)
    {
        if (frame.Empty()) { return; }
        switch (effect)
        {
            case OutputEffect.Scanlines: Scanlines(frame); break;
            case OutputEffect.Glitch: Glitch(frame); break;
        }
    }

    // Darken every other row for a CRT/scanline look.
    private static void Scanlines(Mat f)
    {
        for (int y = 0; y < f.Height; y += 2)
        {
            using var row = f.Row(y);
            row.ConvertTo(row, -1, 0.55, 0); // pixel = pixel * 0.55
        }
    }

    // Random horizontal band displacement + chromatic aberration.
    private static void Glitch(Mat f)
    {
        int w = f.Width, h = f.Height;

        int bands = _rng.Next(2, 5);
        for (int i = 0; i < bands; i++)
        {
            int y = _rng.Next(0, Math.Max(1, h - 8));
            int bh = Math.Min(_rng.Next(4, 22), h - y);
            int dx = _rng.Next(-w / 18, w / 18);
            using var band = new Mat(f, new Rect(0, y, w, bh));
            ShiftHorizontal(band, dx);
        }

        // Offset the red channel a few pixels.
        Cv2.Split(f, out Mat[] ch);
        try
        {
            ShiftHorizontal(ch[2], _rng.Next(3, 9));
            Cv2.Merge(ch, f);
        }
        finally
        {
            foreach (var c in ch) { c.Dispose(); }
        }
    }

    private static void ShiftHorizontal(Mat m, int dx)
    {
        if (dx == 0) { return; }
        using var mat = new Mat(2, 3, MatType.CV_32F);
        mat.Set(0, 0, 1f); mat.Set(0, 1, 0f); mat.Set(0, 2, dx);
        mat.Set(1, 0, 0f); mat.Set(1, 1, 1f); mat.Set(1, 2, 0f);
        Cv2.WarpAffine(m, m, mat, m.Size(), InterpolationFlags.Nearest,
            BorderTypes.Replicate);
    }
}
