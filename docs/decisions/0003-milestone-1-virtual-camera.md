# 3. Milestone #1 — virtual camera emitting a test pattern

Date: 2026-07-28
Status: Paused (steps 1a-1c done; 1d blocked)

## Outcome / why paused

The camera **registers and enumerates** ("Onyx Virtual Camera" appears to apps),
Windows loads our DLL and drives it through its full negotiation, and media-type
negotiation succeeds. But the Frame Server abandons our source during its probe
phase and never starts streaming (`RPC_S_CALL_FAILED` / watchdog
`MF_E_...0xC00D4E24`). We addressed every requirement the diagnostics pointed at
(base-interface QI via `ChainInterfaces`, video-capture stream attributes,
allocator usage, free-threaded-marshaler agility, `KSPROPERTY_CAMERACONTROL_PRIVACY`)
and it still stops at the same point without reporting the failing requirement.

A fully-correct from-scratch MF software camera has more undocumented Frame
Server requirements than is practical to reverse-engineer blind. Revisit later by
either porting Microsoft's SimpleMediaSource sample wholesale, or routing output
through **OBS Virtual Camera**. All the native code, host, deploy, and diagnostic
scripts remain in the repo for that.

## Goal

Prove the hardest layer: a virtual camera registered with Windows via
`MFCreateVirtualCamera` that shows a **test pattern** in a consumer app
(Zoom / Discord / OBS), with **no** ML or real webcam involved yet.

## Why a test pattern first

If Windows won't accept and publish our virtual camera device, nothing else in
the project matters. The test pattern removes every other variable (capture,
ONNX, IPC) so we validate the OS integration in isolation.

## Sub-steps

| Step | Deliverable | How it's verified |
|------|-------------|-------------------|
| 1a | `TestPattern` frame generator (pure C++) | ✅ Done — verified via rendered frame |
| 1b | `OnyxMediaSource` COM object serving frames | ✅ Done — compiles (source + stream) |
| 1c | Registration + `MFCreateVirtualCamera` + console host | ✅ Done — DLL exports COM entry points; host builds |
| 1d | Consumer sees "Onyx Virtual Camera" | Manual test in Zoom/OBS (user-run) |

## Running it (1d)

```
regsvr32 /s <out>\Onyx.Native.dll        # register source (per-user, no admin)
<out>\Onyx.VCamHost.exe                   # create + start the virtual camera
```
Then open OBS / Windows Camera and select "Onyx Virtual Camera".
Unregister with `regsvr32 /u /s Onyx.Native.dll`.

## Constraints / notes

- Requires **Windows 11** (met: 26100 SDK).
- Frame format: start with **RGB32 (BGRA)**; convert to NV12 later if a
  consumer requires it.
- The Zoom/OBS visibility check (1d) can only be run on the user's machine.
- Expect 2–3 build/test iterations for 1b–1d.

## Frame format decision

Test pattern is generated as **32-bit BGRA (RGB32)** top-down, which Media
Foundation accepts directly for a software source and is trivial to reason about
pixel-by-pixel. NV12 conversion is deferred until a real consumer forces it.
