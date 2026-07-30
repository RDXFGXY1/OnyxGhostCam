<div align="center">

# 👻 GhostCam

**Keep your face private on camera.**

*by KYROS · Null Studio*

</div>

---

## What is GhostCam?

GhostCam is a privacy shield for your webcam.

It sits quietly between your camera and every app you use — Discord, Zoom, Teams,
your browser — and **covers your face in real time**. The people on the call see
you moving, talking, gesturing… but your face stays hidden behind a mosaic, a
black box, a little ghost, or a **CENSORED** bar.

You get to show up. You just don't have to show your face.

---

## Why you might want it

- Joining a call with people you don't know
- Streaming or recording without revealing yourself
- Sharing your screen while your camera is on
- Anywhere you want to be present but not identifiable

---

## How it works (the short version)

```
   your webcam  ──►  GhostCam covers your face  ──►  Discord / Zoom / any app
```

GhostCam finds your face automatically and follows it as you move. Whatever your
webcam sees, the other side only ever gets the protected version.

**Your video never leaves your computer.** GhostCam never uploads video and
never saves a single frame. The only thing it ever contacts is GitHub, to check
for a new version — you can switch that off in CONFIG.

---

## Getting started

**1. Install** — run the setup file and pick where you'd like it installed.
You can also let it create a desktop shortcut and add itself to the Start menu.

**2. Open GhostCam** — you'll see a control panel that looks like a cockpit.
That's on purpose. You arm it in three steps, in order:

| Step | Switch | What it does |
|:---:|---|---|
| **1** | **SENSOR** | Turns your camera on, then asks you to confirm you can see yourself |
| **2** | **CLOAK** | Covers your face — pick your style, test it, then engage |
| **3** | **UPLINK** | Sends the protected picture out to your other apps |

Each step unlocks the next, so you can't accidentally go live before your face is
covered.

**3. In Discord (or Zoom, Teams, anything)** — open the camera settings and
choose **"OBS Virtual Camera"** from the camera list. That's GhostCam's feed.

**4. Keep an eye on it** — press **POP-OUT MONITOR** for a small window that
floats on top and shows exactly what everyone else is seeing. Peace of mind at a
glance.

---

## What you can do with it

**Choose how you're hidden**

| Style | Look |
|---|---|
| **Mosaic** | Classic chunky pixelation |
| **Black** | A solid black box |
| **Ghost** | A little cartoon ghost sits over your face |
| **Censored** | Tabloid-style black bar |
| **Image** | **Any picture you upload** — a mask, sticker, logo, your own art. It follows your face as you move (transparent PNGs work best) |
| **Text** | **Hide behind a word** — type anything and it's stamped across your face, auto-sized to fit |

**Make it yours**

- **Overlay editor** — add your own text and images to the picture and drag them
  wherever you like. Resize them, fade them, recolour your text.
- **Watermark** — stamp your name on the corner of everything you send.
- **Filters** — give the whole picture a retro scanline look or a glitchy feel.
- **Tactical display** — turn on a heads-up display with a targeting reticle that
  locks onto your face. Purely for style. Completely worth it.

**Stay in control**

- **Paranoid mode** — if GhostCam ever loses track of your face for a moment, it
  hides the *whole* picture instead of risking a peek. On by default.
- **Warning alarm** — if you're ever broadcasting with your face uncovered,
  GhostCam flashes and warns you immediately.
- **Master kill** — one button shuts everything down instantly.
- **Tray mode** — tuck GhostCam into the system tray and it keeps working quietly
  in the background.
- **Built-in updater** — GhostCam tells you when there's a new version, shows what
  changed, and installs it for you. Switch it off in CONFIG if you'd rather not.

---

## What you need

- A Windows 10 or 11 computer
- A webcam
- **OBS Studio installed** — GhostCam borrows its camera connection to reach
  your other apps. You never have to open OBS; it just needs to be installed.
  *(Free, from obsproject.com)*

---

## Good to know

- Your settings are remembered, so GhostCam opens the way you left it.
- Sitting in a dark room? Your camera slows down in low light, and so will the
  picture. A lamp facing you fixes it instantly.
- GhostCam is a privacy aid, not a guarantee — keep the pop-out monitor handy
  when it really matters.

---

## Credits

**GhostCam** — by **KYROS** · **Null Studio**.

I built the app: the idea, the architecture, how the pieces fit together and what
it actually does.

**The UI/UX was built with [Claude Code](https://claude.com/claude-code)**
(Anthropic's AI coding tool) — the cockpit control panel, the tactical HUD, the
overlay editor and the whole visual design. Design is genuinely not my strength,
so I handed that part to AI and directed it. Worth being upfront about rather
than letting you assume I designed it myself.

**Also built on** — [OpenCV](https://opencv.org) for image processing,
[ONNX Runtime](https://onnxruntime.ai) for face detection, the
[UltraFace](https://github.com/onnx/models) model, and
[OBS Studio](https://obsproject.com)'s virtual camera for output.

---

<div align="center">

**GhostCam** · Video never leaves your PC · Zero telemetry · Zero data stored

Made by **KYROS**
<br>
**Null Studio**

</div>
