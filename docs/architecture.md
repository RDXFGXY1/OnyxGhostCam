# Architecture

## Layers

1. **Onyx.Native** (C++ DLL) — uses `MFCreateVirtualCamera` to register a virtual
   camera. A backing MF **media source** supplies frames. The virtual camera can
   outlive the creating process, so frames must be delivered via IPC rather than
   a direct in-process call.

2. **Onyx.Core** (C# library) — the processing pipeline:
   - **Capture**: read frames from the real webcam (MF).
   - **Detection**: `IFaceDetector` — ONNX model (BlazeFace) via ONNX Runtime +
     DirectML on the RTX 4050. Runs every *N* frames.
   - **Processing**: `IFrameProcessor` — heavy mosaic over face boxes; box
     interpolation / light optical flow tracks between detection passes.
   - **Interop**: format convert (→ NV12/RGB32) and push frames over the IPC
     bridge to `Onyx.Native`.

3. **Onyx.App** (WPF) — brutalist HUD: side-by-side raw/blurred preview, settings.

## The critical data path

```
webcam → capture → detect (every N) → track → mosaic → convert → IPC → MF media source → virtual camera
```

The **virtual camera is the highest-risk layer** — milestone #1 is proving it
registers and shows a test pattern in a consumer app before any ML is wired in.

## IPC bridge (planned)

Shared memory ring buffer of frame slots (double/triple buffered) + a named
event/semaphore for signalling. Producer: `Onyx.Core`. Consumer: `Onyx.Native`
media source. Chosen for low latency at 60 FPS with zero copies where possible.

## Performance targets

- 60 FPS output. Detection every *N* frames + tracking interpolation between.
- GPU inference via DirectML. Configurable resolution (720p/1080p), block size,
  detection frequency, model (fast/accurate), and target frame rate.

## Privacy invariants

- No telemetry. No persistence of frames. Frames never leave the machine.
- The only network call is the optional update check (`Onyx.Core/Update`), which
  fetches `update.json` from GitHub and sends nothing about the user. It is
  switchable off in CONFIG.
- All models bundled locally; inference is 100% on-device.
