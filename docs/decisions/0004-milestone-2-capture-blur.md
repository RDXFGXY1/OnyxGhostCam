# 4. Milestone 2 — capture + face-blur pipeline

Date: 2026-07-28
Status: In progress

## Goal

The app's real privacy value, visible in a window: capture the webcam, detect
faces, and mosaic-blur them in real time, shown in the WPF HUD. No virtual
camera involved (that output is deferred, see ADR 0003).

## Stack decision

- **Capture + image ops**: OpenCvSharp4 (`VideoCapture`, `Mat`, resize for
  mosaic, drawing). Simple, well-supported, gives us everything for capture and
  pixelation without hand-rolling Media Foundation capture.
- **Face detection**: ONNX Runtime + DirectML (already referenced) running a
  lightweight detector (BlazeFace / UltraFace). Deferred to step 2c.
- **Display**: convert `Mat` -> `WriteableBitmap` (OpenCvSharp WpfExtensions),
  bound to an `Image` in the WPF window.

## Sub-steps

| Step | Deliverable | Verified by |
|------|-------------|-------------|
| 2a | Webcam capture -> raw feed shown in the window | ✅ Done |
| 2b | Full-frame mosaic toggle | ✅ Done |
| 2c | ONNX face detection -> mosaic only face regions | ✅ Done (UltraFace + DirectML) |
| 2d | Detection every N frames + box tracking between | ✅ Done (FaceTracker + FPS HUD) |

Start at 2a (prove capture + display), then layer on processing.
