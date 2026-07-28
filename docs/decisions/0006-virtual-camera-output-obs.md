# 6. Virtual-camera output via OBS Virtual Camera

Date: 2026-07-28
Status: Accepted — **working end-to-end**

## Context

We spent a long effort building our own Media Foundation virtual camera
(`Onyx.Native`, see ADR 0003). It registers and enumerates, and after matching
Microsoft's reference source (sensor profile, stream-descriptor attributes,
NV12 format) the Frame Server *accepts* it — but never streams it. Conclusion:
the Windows Camera Frame Server only streams **code-signed** camera sources
(a paid EV certificate / driver-signing — the NVIDIA/Snap path). Package
identity + camera consent were necessary but not sufficient.

## Decision

Output through **OBS Virtual Camera** instead of our own MF driver. OBS ships a
signed, already-trusted virtual camera. Onyx writes its blurred frames directly
into OBS's shared-memory queue (`OBSVirtualCamVideo`) using OBS's open-source
protocol — the same mechanism `pyvirtualcam` uses. **OBS does not need to run.**

- `Onyx.Core/Interop/ObsVirtualCameraSink.cs` — creates the shared memory,
  converts BGR→NV12, publishes frames (3-slot queue, matches OBS's
  `shared-memory-queue.c`).
- `Onyx.App` — "OUTPUT TO GHOST CAM" toggle feeds the shielded frame to the sink.

## Result

Onyx → captures real webcam → blurs face → pushes to OBS Virtual Camera →
Discord / Zoom / Teams / Camera app select "OBS Virtual Camera" and see the
blurred feed. Confirmed working in Discord.

## Requirements / notes

- OBS Studio must be installed (for its signed virtual-camera device); it does
  not need to be open. Onyx must be the sole writer of the shared queue (fails
  gracefully if OBS is actively running its own virtual camera).
- The from-scratch `Onyx.Native` driver remains in the repo for reference / a
  future signed build, but is not on the working path.
