<div align="center">

# 👻 GhostCam v1.0.0

**First release — keep your face private on camera.**

*by KYROS · Null Studio*

</div>

---

## 📥 Download

| File | Size | What it is |
|---|---|---|
| **GhostCam-Setup-1.0.0.exe** | ~85 MB | Full installer — everything included |

Download it, run it, follow the wizard. Nothing else to install.

> Windows may show a *"Windows protected your PC"* warning because this release
> isn't code-signed yet. Click **More info → Run anyway**.

---

## What is GhostCam?

GhostCam is a privacy shield for your webcam.

It sits between your camera and every app you use — Discord, Zoom, Teams, your
browser — and **covers your face in real time**. People on the call see you
moving, talking and gesturing, but your face stays hidden behind a mosaic, a
black box, a little ghost, or a **CENSORED** bar.

You get to show up. You just don't have to show your face.

**Your video never leaves your machine.** No uploads, nothing saved. GhostCam
contacts GitHub only to check for updates — switchable off in CONFIG.

---

## Quick start

**1.** Install and open GhostCam.

**2.** Arm the three switches in order:

| Step | Switch | What happens |
|:---:|---|---|
| **1** | **SENSOR** | Camera turns on — confirm you can see yourself |
| **2** | **CLOAK** | Your face gets covered — pick a style, test, engage |
| **3** | **UPLINK** | The protected picture goes out to your other apps |

Each step unlocks the next, so you can't go live before you're covered.

**3.** In Discord (or any app), open camera settings and pick
**"OBS Virtual Camera"**.

**4.** Press **POP-OUT MONITOR** — a small always-on-top window showing exactly
what everyone else sees.

---

## What's in this release

**Privacy**
- Real-time face detection that follows you as you move
- Four cover styles: **mosaic**, **black**, **ghost**, **censored**
- **Paranoid mode** — hides the whole picture if your face is ever lost (on by default)
- **⚠ EXPOSED alarm** — flashes and warns if you're ever broadcasting uncovered
- **MASTER KILL** — one button, everything off

**Make it yours**
- **Overlay editor** — add your own text and images, drag them anywhere, resize,
  fade, recolour
- **Watermark** — stamp `GHOSTCAM // your name` on the corner
- **Filters** — retro scanlines or a glitch look
- **Tactical HUD** — targeting reticle that locks onto your face

**Built for actual use**
- Cockpit-style controls with step-by-step arming and interlocks
- Docked feed monitor + floating pop-out monitor
- Runs on your dedicated GPU for speed
- System-tray mode — tuck it away and it keeps working
- Every setting is remembered between sessions

---

## Requirements

- **Windows 10 or 11** (64-bit)
- A **webcam**
- **[OBS Studio](https://obsproject.com)** installed — GhostCam borrows its
  camera connection to reach your other apps. **You never have to open OBS**,
  it just needs to be installed.

---

## Good to know

- **Dark room?** Cameras slow down in low light, and the picture will too. A lamp
  facing you fixes it instantly.
- **Your own preview may look mirrored** in Discord — that's Discord mirroring
  your self-view, not GhostCam. Trust the pop-out monitor; that's the real
  output. There are **MIRROR** switches in CONFIG if you want to flip it.
- GhostCam is a privacy aid, not a guarantee. Keep the pop-out monitor open when
  it really matters.

---

## Known limitations

- Requires OBS Studio installed for the virtual camera (a standalone driver needs
  code-signing — planned for a future release)
- Not code-signed yet, so Windows shows a SmartScreen warning on first run
- Detects and covers faces; it doesn't yet recognise *whose* face it is

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

## Feedback

Found a bug or have an idea? Open an issue — this is the first release and I'd
genuinely like to hear what breaks and what you'd want next.

---

<div align="center">

**GhostCam v1.0.0** · Video never leaves your PC · Zero telemetry · Zero data stored

Made by **KYROS**
<br>
**Null Studio**

</div>
