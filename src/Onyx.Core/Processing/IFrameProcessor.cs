namespace Onyx.Core.Processing;

/// <summary>
/// Applies privacy processing (heavy mosaic pixelation over face regions)
/// to a frame in place, producing the output that is pushed to the virtual camera.
/// </summary>
public interface IFrameProcessor
{
    /// <summary>Mosaic block size in pixels — larger means stronger pixelation.</summary>
    int MosaicBlockSize { get; set; }

    /// <summary>Pixelate the given face regions on the frame buffer in place.</summary>
    void Apply(Span<byte> frame, int width, int height,
               IReadOnlyList<Detection.FaceBox> faces);
}
