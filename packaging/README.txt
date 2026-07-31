================================================================
   GHOSTCAM
   Keep your face private on camera.
   by KYROS - Null Studio
================================================================


WHAT IS GHOSTCAM?
-----------------

GhostCam is a privacy shield for your webcam.

It sits quietly between your camera and every app you use --
Discord, Zoom, Teams, your browser -- and covers your face in
real time. The people on the call see you moving, talking,
gesturing... but your face stays hidden behind a mosaic, a black
box, a little ghost, or a CENSORED bar.

You get to show up. You just don't have to show your face.


WHY YOU MIGHT WANT IT
---------------------

  - Joining a call with people you don't know
  - Streaming or recording without revealing yourself
  - Sharing your screen while your camera is on
  - Anywhere you want to be present but not identifiable


HOW IT WORKS
------------

   your webcam  ->  GhostCam covers your face  ->  any app

GhostCam finds your face automatically and follows it as you
move. Whatever your webcam sees, the other side only ever gets
the protected version.

Your video never leaves your computer. GhostCam never uploads
video and never saves a single frame. It contacts GitHub only to
check for updates -- switchable off in CONFIG.


GETTING STARTED
---------------

1. Open GhostCam. You'll see a control panel that looks like a
   cockpit. That's on purpose. You arm it in three steps, in
   order:

      STEP 1 - SENSOR
        Turns your camera on, then asks you to confirm you can
        see yourself.

      STEP 2 - CLOAK
        Covers your face. Pick your style, test it, then engage.

      STEP 3 - UPLINK
        Sends the protected picture out to your other apps.

   Each step unlocks the next, so you can't accidentally go live
   before your face is covered.

2. In Discord (or Zoom, Teams, anything), open the camera
   settings and choose "OBS Virtual Camera" from the camera list.
   That is GhostCam's feed.

3. Press POP-OUT MONITOR for a small window that floats on top
   and shows exactly what everyone else is seeing.


WHAT YOU CAN DO
---------------

Choose how you're hidden:
    Mosaic     - classic chunky pixelation
    Image      - any picture you upload, tracks your face
    Text       - hide behind a word of your choice
    Black      - a solid black box
    Ghost      - a little cartoon ghost over your face
    Censored   - tabloid-style black bar

Make it yours:
    - Overlay editor: add your own text and images, drag them
      anywhere, resize, fade and recolour them
    - Watermark: stamp your name on the corner
    - Filters: retro scanlines or a glitchy look
    - Tactical display: a heads-up display with a targeting
      reticle that locks onto your face

Stay in control:
    - Paranoid mode: if GhostCam loses track of your face for a
      moment, it hides the whole picture instead of risking a
      peek (on by default)
    - Warning alarm: flashes and warns you if you are ever
      broadcasting with your face uncovered
    - Master kill: one button shuts everything down instantly
    - Tray mode: tuck GhostCam away and it keeps working quietly


WHAT YOU NEED
-------------

  - Windows 10 or 11
  - A webcam
  - OBS Studio installed. GhostCam borrows its camera connection
    to reach your other apps. You never have to open OBS; it
    just needs to be installed. Free, from obsproject.com


GOOD TO KNOW
------------

  - Your settings are remembered, so GhostCam opens the way you
    left it.
  - Sitting in a dark room? Your camera slows down in low light,
    and so will the picture. A lamp facing you fixes it.
  - GhostCam is a privacy aid, not a guarantee. Keep the pop-out
    monitor handy when it really matters.


CREDITS
-------

GhostCam -- by KYROS, Null Studio.

I built the app: the idea, the architecture and what it
actually does.

The UI/UX was built with Claude Code (Anthropic's AI coding
tool) -- the cockpit panel, the tactical HUD, the overlay editor
and the visual design. Design isn't my strength, so I handed
that part to AI and directed it. Being upfront rather than
letting you assume I designed it myself.

Built on OpenCV, ONNX Runtime, the UltraFace detection model,
and OBS Studio's virtual camera.


================================================================
   Video never leaves your PC - Zero telemetry

   Made by KYROS
   Null Studio
================================================================
