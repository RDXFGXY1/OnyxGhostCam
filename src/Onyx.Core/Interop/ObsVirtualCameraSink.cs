using System.IO.MemoryMappedFiles;
using OpenCvSharp;

namespace Onyx.Core.Interop;

/// <summary>
/// Pushes frames into the OBS Virtual Camera via its shared-memory protocol
/// (the same one OBS Studio and pyvirtualcam use). OBS Studio must be installed
/// (for its signed virtual-camera device), but does not need to be running.
///
/// Layout (from OBS shared-memory-queue.c): a queue_header followed by three
/// NV12 frame slots, each prefixed by a 32-byte frame header holding the
/// timestamp. We are the writer; the app selecting "OBS Virtual Camera" reads.
/// </summary>
public sealed class ObsVirtualCameraSink : IDisposable
{
    private const string VideoName = "OBSVirtualCamVideo";

    // queue_header field offsets (MSVC packing) and the 32-aligned header size.
    private const int HeaderSizeAligned = 96;
    private const int FrameHeaderSize = 32;
    private const int OffWriteIdx = 0, OffReadIdx = 4, OffState = 8;
    private const int OffOffsets = 12;                 // uint32[3]
    private const int OffType = 24, OffCx = 28, OffCy = 32, OffInterval = 40;

    private const int StateStarting = 1, StateReady = 2, StateStopping = 3;

    private readonly int _cx, _cy, _frameSize;
    private readonly long _interval;                   // frame interval, 100ns units
    private readonly int[] _frameOffset = new int[3];

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private readonly byte[] _nv12;
    private readonly Mat _i420 = new();
    private readonly Mat _resized = new();
    private long _writeIdx;

    public bool IsOpen => _view is not null;

    public ObsVirtualCameraSink(int width, int height, int fps = 30)
    {
        _cx = width;
        _cy = height;
        _frameSize = _cx * _cy * 3 / 2;
        _interval = 10_000_000L / Math.Max(1, fps);
        _nv12 = new byte[_frameSize];
    }

    private static int Align32(int v) => (v + 31) & ~31;

    /// <summary>Creates the shared queue. Fails (returns false) if another writer (e.g. OBS) owns it.</summary>
    public bool TryStart()
    {
        try
        {
            using var existing = MemoryMappedFile.OpenExisting(VideoName);
            return false; // already in use
        }
        catch (FileNotFoundException) { /* free to create */ }

        int o0 = HeaderSizeAligned;
        int o1 = Align32(o0 + _frameSize + FrameHeaderSize);
        int o2 = Align32(o1 + _frameSize + FrameHeaderSize);
        int total = Align32(o2 + _frameSize + FrameHeaderSize);
        _frameOffset[0] = o0; _frameOffset[1] = o1; _frameOffset[2] = o2;

        _mmf = MemoryMappedFile.CreateNew(VideoName, total);
        _view = _mmf.CreateViewAccessor(0, total);

        _view.Write(OffWriteIdx, 0);
        _view.Write(OffReadIdx, 0);
        _view.Write(OffState, StateStarting);
        _view.Write(OffOffsets + 0, o0);
        _view.Write(OffOffsets + 4, o1);
        _view.Write(OffOffsets + 8, o2);
        _view.Write(OffType, 0);
        _view.Write(OffCx, _cx);
        _view.Write(OffCy, _cy);
        _view.Write(OffInterval, _interval);
        return true;
    }

    /// <summary>Converts a BGR frame to NV12 and publishes it to the queue.</summary>
    public void WriteFrame(Mat bgr)
    {
        var view = _view;
        if (view is null || bgr.Empty()) { return; }

        Mat src = bgr;
        if (bgr.Width != _cx || bgr.Height != _cy)
        {
            Cv2.Resize(bgr, _resized, new Size(_cx, _cy));
            src = _resized;
        }

        // BGR -> I420 (planar Y,U,V), then interleave U/V into NV12.
        Cv2.CvtColor(src, _i420, ColorConversionCodes.BGR2YUV_I420);
        _i420.GetArray(out byte[] i420);

        int ySize = _cx * _cy;
        int chroma = ySize / 4;
        Array.Copy(i420, 0, _nv12, 0, ySize);
        int uOff = ySize, vOff = ySize + chroma, uv = ySize;
        for (int i = 0; i < chroma; i++)
        {
            _nv12[uv++] = i420[uOff + i];
            _nv12[uv++] = i420[vOff + i];
        }

        long inc = ++_writeIdx;
        int idx = (int)((uint)inc % 3);
        long timestamp = inc * _interval;

        view.Write(OffWriteIdx, (int)inc);                    // claim slot
        view.Write(_frameOffset[idx], timestamp);             // frame header: timestamp
        view.WriteArray(_frameOffset[idx] + FrameHeaderSize, _nv12, 0, _frameSize);
        view.Write(OffReadIdx, (int)inc);                     // publish
        view.Write(OffState, StateReady);
    }

    public void Dispose()
    {
        if (_view is not null)
        {
            try { _view.Write(OffState, StateStopping); } catch { /* ignore */ }
            _view.Dispose();
            _view = null;
        }
        _mmf?.Dispose();
        _mmf = null;
        _i420.Dispose();
        _resized.Dispose();
    }
}
