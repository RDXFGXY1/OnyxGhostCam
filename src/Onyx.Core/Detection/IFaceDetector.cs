using OpenCvSharp;

namespace Onyx.Core.Detection;

/// <summary>
/// Detects faces in a frame. Implementations wrap an ONNX model run via ONNX
/// Runtime (DirectML GPU). Returned rectangles are in frame pixel coordinates.
/// </summary>
public interface IFaceDetector : IDisposable
{
    /// <summary>Confidence threshold, 0..1. Lower = more sensitive.</summary>
    float ScoreThreshold { get; set; }

    IReadOnlyList<Rect> Detect(Mat frame);
}
