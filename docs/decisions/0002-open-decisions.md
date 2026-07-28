# 2. Tracking the open decisions

Date: 2026-07-28
Status: Open

The following are deliberately deferred. Each becomes its own ADR when decided.

| # | Decision | Options | Notes |
|---|----------|---------|-------|
| A | Final app name | "Onyx", "Ghost Cam", other | Working name is **Onyx** |
| B | Bundled ONNX face model | BlazeFace (fast) + TBD accurate | Must run well on DirectML/RTX 4050 |
| C | Multiple simultaneous faces | Yes / No | Affects tracker + mosaic loop |
| D | Installer / packaging | MSIX, Inno Setup, WiX | Must register the virtual camera |

## Build-first milestone order (agreed)

1. Native MF virtual camera emitting a **test pattern** (proves hardest layer).
2. C# capture + ONNX/DirectML detect + mosaic on a preview window.
3. IPC bridge wiring (2) → (1).
4. WPF brutalist HUD shell.
