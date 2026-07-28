# 5. Milestone 4 — settings persistence + packaging

Date: 2026-07-28
Status: In progress

## Goal

Make the app configurable and persistent, and produce a distributable build.

## What landed

- **`OnyxSettings`** (`Onyx.Core/Settings`): POCO persisted to
  `%AppData%\Onyx\settings.json` (local only, no telemetry). Loaded on startup,
  saved on close. Covers camera index, input resolution (720p/1080p), mosaic
  strength, detection rate, GPU toggle, and shield-on-startup.
- **Settings panel** additions: camera index, 1080p toggle, GPU (DirectML)
  toggle, shield-on-startup toggle — all styled in the brutalist theme.
- **`publish.ps1`**: self-contained Release build to `.\publish\`, bundling the
  ONNX model. `-SingleFile` for a one-exe extract build.

## Deferred

- Full installer (MSIX or Inno Setup) with Start-menu entry and uninstall — its
  own ADR when we get there.
- "Detection model choice (fast/accurate)" and "output frame-rate target" from
  the original spec: the settings model has room; wire real options once we bundle
  a second model / add a frame limiter.
