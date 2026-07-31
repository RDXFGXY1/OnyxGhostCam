using Onyx.Core.Detection;
using OpenCvSharp;
using Xunit;

namespace Onyx.Core.Tests;

/// <summary>
/// The latch is a privacy control, not a cosmetic one: if it releases early the
/// face is exposed, and if it never releases the cover sticks to empty air. Both
/// directions are worth pinning down.
/// </summary>
public class FaceTrackerTests
{
    // Returns whatever the script says for each successive Detect call.
    private sealed class ScriptedDetector : IFaceDetector
    {
        private readonly Queue<IReadOnlyList<Rect>> _script;
        public ScriptedDetector(params IReadOnlyList<Rect>[] frames)
            => _script = new Queue<IReadOnlyList<Rect>>(frames);

        public float ScoreThreshold { get; set; } = 0.6f;
        public IReadOnlyList<Rect> Detect(Mat frame)
            => _script.Count > 0 ? _script.Dequeue() : Array.Empty<Rect>();
        public void Dispose() { }
    }

    private static readonly Rect Face = new(100, 100, 80, 80);
    private static IReadOnlyList<Rect> One => new[] { Face };
    private static IReadOnlyList<Rect> None => Array.Empty<Rect>();

    private static Mat Frame() => new(480, 640, MatType.CV_8UC3, Scalar.Black);

    [Fact]
    public void HoldsCoverThroughASingleMissedDetection()
    {
        var t = new FaceTracker(new ScriptedDetector(One, None, One))
        { DetectEveryNFrames = 1, LatchFrames = 10 };
        using var f = Frame();

        Assert.NotEmpty(t.Update(f));               // detected
        var held = t.Update(f);                     // missed - must not expose
        Assert.NotEmpty(held);
        Assert.True(t.IsLatched);
        Assert.NotEmpty(t.Update(f));               // reacquired
        Assert.False(t.IsLatched);
    }

    [Fact]
    public void ReleasesAfterLatchExpires()
    {
        var t = new FaceTracker(new ScriptedDetector(One))
        { DetectEveryNFrames = 1, LatchFrames = 3 };
        using var f = Frame();

        Assert.NotEmpty(t.Update(f));
        for (int i = 0; i < 3; i++) { t.Update(f); }

        Assert.Empty(t.Update(f));
        Assert.False(t.IsLatched);
    }

    [Fact]
    public void LatchLengthIsIndependentOfScanRate()
    {
        // The latch counts frames, so a slower scan rate must not stretch it.
        var t = new FaceTracker(new ScriptedDetector(One))
        { DetectEveryNFrames = 4, LatchFrames = 6 };
        using var f = Frame();

        t.Update(f);                                    // frame 0: detected
        for (int i = 0; i < 4; i++) { t.Update(f); }    // frame 4 misses, latch starts

        int alive = 0;
        for (int i = 0; i < 20 && t.Update(f).Count > 0; i++) { alive++; }
        Assert.InRange(alive, 1, 7);
        Assert.False(t.IsLatched);
    }

    [Fact]
    public void ZeroLatchExposesImmediately()
    {
        var t = new FaceTracker(new ScriptedDetector(One, None))
        { DetectEveryNFrames = 1, LatchFrames = 0 };
        using var f = Frame();

        Assert.NotEmpty(t.Update(f));
        Assert.Empty(t.Update(f));
    }

    [Fact]
    public void LatchedBoxIsGrownNotShrunk()
    {
        // We lost the face most likely because it moved, so the held box widens.
        var t = new FaceTracker(new ScriptedDetector(One, None))
        { DetectEveryNFrames = 1, LatchFrames = 10 };
        using var f = Frame();

        var detected = t.Update(f)[0];
        var held = t.Update(f)[0];
        Assert.True(held.Width >= detected.Width);
        Assert.True(held.Height >= detected.Height);
    }

    [Fact]
    public void ResetDropsEverything()
    {
        var t = new FaceTracker(new ScriptedDetector(One))
        { DetectEveryNFrames = 1, LatchFrames = 30 };
        using var f = Frame();

        Assert.NotEmpty(t.Update(f));
        t.Reset();
        Assert.False(t.IsLatched);

        // Nothing left to hold, so the next miss exposes rather than resurrecting.
        Assert.Empty(t.Update(f));
    }

    [Fact]
    public void LatchedBoxStaysInsideTheFrame()
    {
        var edge = new[] { new Rect(0, 0, 60, 60) };
        var t = new FaceTracker(new ScriptedDetector(edge, None))
        { DetectEveryNFrames = 1, LatchFrames = 10 };
        using var f = Frame();

        t.Update(f);
        var held = t.Update(f)[0];
        Assert.True(held.X >= 0 && held.Y >= 0);
        Assert.True(held.Right <= f.Width && held.Bottom <= f.Height);
    }
}
