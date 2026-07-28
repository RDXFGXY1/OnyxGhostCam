# Onyx (working name — "Ghost Cam")

A Windows 11 desktop app that inserts a **privacy virtual camera** between your
real webcam and any app (Zoom, Discord, OBS). It detects faces in real time and
applies heavy mosaic pixelation, then exposes the blurred feed as a normal webcam.

**Fully offline. Zero telemetry. Zero network calls. All inference is local.**

## Architecture (3 layers)

```
 real webcam ──► MF capture ──► Onyx.Core (ONNX/DirectML detect + mosaic)
                                     │
                                     ▼  format convert (NV12/RGB32) + IPC
                              Onyx.Native (MF virtual camera media source)
                                     │
                                     ▼
                          Zoom / Discord / OBS see the blurred feed
```

| Project | Tech | Role |
|---------|------|------|
| `src/Onyx.Native` | C++ / Media Foundation | Registers the virtual camera device with Windows |
| `src/Onyx.Core`   | C# / ONNX Runtime + DirectML | Capture, face detection, mosaic, tracking, IPC sender |
| `src/Onyx.App`    | C# / WPF | Brutalist dark-red HUD shell, live preview, settings |
| `tests/Onyx.Core.Tests` | xUnit | Unit tests for the core |

## Build

- **C# side:** .NET 8 SDK (installed). `dotnet build Onyx.sln`
- **Native side:** requires Visual Studio 2022 with the **Desktop development with C++**
  workload (provides MSVC + Windows 11 SDK with `mfvirtualcamera.h`).

## Status

Scaffolding stage. See [docs/architecture.md](docs/architecture.md) and the
[decision records](docs/decisions/). Milestone #1 is the native virtual camera
emitting a **test pattern** into Zoom/Discord.
