using OpenCvSharp;

namespace Onyx.Core.Processing;

/// <summary>
/// Applies a heavy mosaic (pixelation) effect by downscaling then upscaling with
/// nearest-neighbour interpolation. Works in place on OpenCV <see cref="Mat"/> frames.
/// </summary>
public enum BlurStyle { Mosaic, Black }

public sealed class MosaicProcessor
{
    private int _blockSize = 16;

    /// <summary>Pixel block size. Larger = stronger/blockier pixelation. Clamped to >= 2.</summary>
    public int BlockSize
    {
        get => _blockSize;
        set => _blockSize = Math.Max(2, value);
    }

    /// <summary>Mosaic pixelation or a solid black box.</summary>
    public BlurStyle Style { get; set; } = BlurStyle.Mosaic;

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

    private void Cover(Mat target)
    {
        if (Style == BlurStyle.Black) { target.SetTo(Scalar.Black); }
        else { Pixelate(target); }
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
