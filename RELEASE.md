<div align="center">

# 👻 GhostCam v1.1.0

**Hide behind your own image — or a word.**

*by KYROS · Null Studio*

</div>

---

## 📥 Download

| File | Size | What it is |
|---|---|---|
| **GhostCam-Setup-1.1.0.exe** | ~85 MB | Full installer — everything included |

Already on v1.0.0? **You don't need this page** — open GhostCam and it'll offer the
update itself.

> Windows may show a *"Windows protected your PC"* warning, and some antivirus
> may flag it. GhostCam isn't code-signed yet (certificates are expensive), so
> brand-new unknown installers get flagged by default. Click
> **More info → Run anyway**. Source is right here if you'd rather build it yourself.

---

## ✨ What's new

### 🖼️ Hide behind your own image

Upload **any picture** and it covers your face — a mask, a sticker, a logo, your
own artwork, a cursed meme. It follows your face as you move.

- Transparent PNGs work best
- Fills the whole covered area
- Transparent parts sit on a pixelated backing, so nothing leaks through

**CLOAK → COVER MODE → IMAGE → UPLOAD…**

### 🔤 Hide behind a word

Type anything — `NOPE`, `PRIVATE`, your handle — and it's stamped across your
face. The font resizes itself so it always fits, whether you're close to the
camera or far away.

**CLOAK → COVER MODE → TEXT**

### 🔄 Built-in updater

GhostCam now tells you when there's a new version, shows exactly what changed,
and installs it for you. No more checking GitHub.

- A reminder arrives as a Windows notification if you dismiss it
- Switch it off any time in **CONFIG → CHECK FOR UPDATES**

---

## 🐛 Fixes

- Custom masks fill the entire covered region instead of leaving pixelated gaps
  around the edges
- Corrected the privacy wording — see below

## 📋 Changed

GhostCam previously claimed *"zero network calls"*. With the updater that's no
longer strictly true, so the wording is now accurate everywhere:

> **Your video never leaves your PC. Zero telemetry. Nothing stored.**
> The update check is the only network call GhostCam makes — it sends nothing
> about you, and you can turn it off.

The privacy of your camera hasn't changed at all. Only the honesty of the wording.

---

## Everything else (from v1.0.0)

Real-time face cover (**mosaic · black · ghost · censored**), overlay editor for
your own text and images, watermark, scanline and glitch filters, tactical HUD,
paranoid mode, EXPOSED alarm, master kill, pop-out monitor, system tray, and the
cockpit control panel.

New here? The [README](README.md) walks through the whole thing.

---

## Requirements

- **Windows 10 or 11** (64-bit)
- A **webcam** — or a phone-as-webcam app (DroidCam, Iriun, Camo…), just pick the
  right **CAMERA INDEX**
- **[OBS Studio](https://obsproject.com)** installed — GhostCam borrows its camera
  connection to reach your other apps. **You never have to open OBS.**

---

## Known limitations

- Requires OBS Studio installed for the virtual camera output
- Not code-signed, so Windows SmartScreen and some antivirus will warn on first run
- Covers any face it detects; it doesn't yet tell *whose* face is whose

---

## Credits

**GhostCam** — by **KYROS** · **Null Studio**.

I built the app: the idea, the architecture and what it actually does.

**The UI/UX was built with [Claude Code](https://claude.com/claude-code)**
(Anthropic's AI coding tool) — the cockpit panel, the tactical HUD, the overlay
editor and the visual design. Design isn't my strength, so I handed that part to
AI and directed it. Being upfront rather than letting you assume I designed it.

Built on [OpenCV](https://opencv.org), [ONNX Runtime](https://onnxruntime.ai),
the [UltraFace](https://github.com/onnx/models) model, and
[OBS Studio](https://obsproject.com)'s virtual camera.

---

<div align="center">

**GhostCam v1.1.0** · Video never leaves your PC · Zero telemetry · Zero data stored

Made by **KYROS**
<br>
**Null Studio**

</div>
