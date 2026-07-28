namespace Onyx.Core.Detection;

/// <summary>A detected face region, normalized to [0,1] relative to frame dimensions.</summary>
public readonly record struct FaceBox(float X, float Y, float Width, float Height, float Score);

/// <summary>
/// Runs face detection on a single frame. Implementations wrap an ONNX model
/// (e.g. BlazeFace) executed via ONNX Runtime with the DirectML provider.
/// </summary>
public interface IFaceDetector : IDisposable
{
    /// <summary>Detect faces in a raw frame. Returns boxes normalized to [0,1].</summary>
    IReadOnlyList<FaceBox> Detect(ReadOnlySpan<byte> frame, int width, int height);
}
