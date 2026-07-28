using OpenCvSharp;

namespace Onyx.Core.Detection;

/// <summary>
/// Detects faces in a frame. Implementations wrap an ONNX model run via ONNX
/// Runtime (DirectML GPU). Returned rectangles are in frame pixel coordinates.
/// </summary>
public interface IFaceDetector : IDisposable
{
    IReadOnlyList<Rect> Detect(Mat frame);
}
