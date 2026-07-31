<div align="center">

# 👻 GhostCam v1.2.0

**Now it hides your room too — and never blinks.**

*by KYROS · Null Studio*

</div>

---

## 📥 Download

| File | Size | What it is |
|---|---|---|
| **GhostCam-Setup-1.2.0.exe** | ~85 MB | **Start here.** Normal installer — everything included |
| **GhostCam-Portable-1.2.0.zip** | ~85 MB | No install needed. Use this if your antivirus blocks the installer |
| SHA256SUMS-1.2.0.txt | 1 KB | Optional — lets you verify your download wasn't tampered with |

Already on v1.1.0? **You don't need this page** — open GhostCam and it'll offer the
update itself.

**You also need [OBS Studio](https://obsproject.com/download)** installed — GhostCam
uses its virtual-camera driver to reach Discord, Zoom and Teams. OBS doesn't need to
be running, just installed.

### Portable version

Blocked by antivirus? Download the ZIP instead, right-click → **Extract All**, and run
`GhostCam.exe` from the extracted folder. Nothing is installed, nothing is written to
the registry, no admin rights needed. Delete the folder to remove it.

> **About the antivirus warning:** GhostCam isn't code-signed yet (certificates are a
> recurring cost), so Windows and some scanners flag *any* brand-new unknown program by
> default. It's not a detection of anything in the code. Click **More info → Run
> anyway**. The full source is in this repo if you'd rather build it yourself.

<details>
<summary>Verifying your download (optional)</summary>

Open PowerShell in your Downloads folder and run:

```powershell
Get-FileHash .\GhostCam-Setup-1.2.0.exe -Algorithm SHA256
```

The hash it prints should match the line in `SHA256SUMS-1.2.0.txt`. If it doesn't,
you didn't get the file from here — delete it.

</details>

---

## ✨ What's new

### 🔒 Cover latch — no more single-frame slips

Face detection isn't perfect. Blink, turn your head, move too fast, and for one frame
the detector loses you — and for that frame, your face was on camera.

GhostCam now **keeps covering the last place it saw you** for a moment after losing
track, and widens the cover slightly while it waits (if it lost you, you were probably
moving). The monitor shows `LATCHED` while it's holding.

**CLOAK → COVER LATCH** — in frames. Higher is safer, lower is more responsive.

### 🌫️ Background blur & replace

Your face isn't the only thing that identifies you. The posters on your wall, the mail
on your desk, the view out your window, whoever else is in the room — all of it leaks.

Four modes:

- **BLUR** — softens the room behind you
- **IMAGE** — swap in any picture as a backdrop
- **VOID** — black it out entirely
- **OFF** — leave it alone

**CLOAK → BACKGROUND**

> **Be realistic about this one.** The cutout is traced from your face, not from a
> full body-segmentation model, so it reads like a shallow depth-of-field blur rather
> than a hard green-screen key. Arms held away from your body won't be included. If
> wall stays sharp beside you, turn **CUTOUT WIDTH** down; if your shoulders get
> blurred, turn it up. Try 85–95% first.

### 🎛️ Profiles — one click, everything set

Three preset slots that load your whole cloak configuration at once:

| Profile | What it's for |
|---|---|
| **WORK CALL** | Softened mosaic, blurred room, nothing distracting |
| **STREAM** | Ghost cover, watermark, tactical HUD, scanlines |
| **FULL ANON** | Hard black cover, long latch, room replaced, scanning every frame |

**SAVE** overwrites a slot with your current settings. Arm state is never stored — a
profile can't put you on air by accident.

**SETUP → PROFILES**

### 🐕 Pipeline watchdog — fail closed

If your camera stalls, the driver wedges, or someone yanks the USB cable while you're
live, the virtual camera used to keep serving whatever frame was written last — a
frozen still of you, possibly from before the cloak engaged.

Now it pushes **black** instead. Silent, immediate, no way to be left exposed by a
crash.

**SETUP → WATCHDOG → STALL CUTOFF** — or set it to OFF if you'd rather not have it.

### 🖥️ Rebuilt around the preview

The window is now a large live monitor with a tabbed control rail beside it —
**CLOAK · OVERLAY · OUTPUT · SETUP** — instead of stacked collapsing panels. Every
control, switch and gated procedure is still there, just no longer buried. New
**GO LIVE** button walks you to whichever step is blocking you.

### 🪟 Start with Windows

GhostCam can now launch straight to the system tray when you sign in.

**SETUP → START WITH WINDOWS**

---

## 📋 Changed

- **"Start with Windows" moved out of the installer** into the app itself. Same
  feature — but an installer that writes a startup registry key looks like malware
  persistence to antivirus heuristics, while an app doing it because you asked does
  not. One less false-positive trigger.
- **Portable ZIP** is now published every release, for anyone whose antivirus eats
  the installer.
- **SHA256 checksums** are published so you can verify what you downloaded.

## 🐛 Fixes

- Face tracking no longer jitters when the detector briefly misses you
- The background cutout no longer drags a ring of sharp wall around your head

---

## Everything else (from v1.0.0 – v1.1.0)

Real-time face cover (**mosaic · black · ghost · censored · your image · your text**),
overlay editor for custom text and images, watermark, scanline and glitch filters,
tactical HUD, paranoid mode, EXPOSED alarm, master kill, pop-out monitor, system tray,
in-app updater, and the cockpit control panel.

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
- Background replacement is a geometric cutout, not true segmentation — see the note
  above

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

**GhostCam v1.2.0** · Video never leaves your PC · Zero telemetry · Zero data stored

Made by **KYROS**
<br>
**Null Studio**

</div>
