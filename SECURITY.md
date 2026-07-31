# Security Policy

GhostCam is a privacy tool. A bug that exposes a face that should have been
covered is a security bug here, not a cosmetic one — please report those the same
way you would report anything else on this page.

## Supported versions

GhostCam is maintained by one person, so **only the most recent release gets
fixes**. If you're on anything older, update first — the in-app updater
(**SETUP → CHECK NOW**) will take you there.

| Version | Supported |
| ------- | --------- |
| 1.2.x   | ✅ |
| 1.1.x   | ❌ |
| 1.0.x   | ❌ |
| < 1.0   | ❌ |

## Reporting a vulnerability

**Please don't open a public issue for security problems.**

Use GitHub's private reporting instead:

**[Report a vulnerability →](https://github.com/RDXFGXY1/OnyxGhostCam/security/advisories/new)**

That opens a private thread visible only to you and the maintainer.

Helpful things to include, if you have them:

- Which version you're on (shown in the title bar and in **SETUP**)
- Windows version, and your camera / capture setup
- Steps to reproduce
- What you expected to happen, and what actually happened
- A screenshot or clip **only if it doesn't expose your own face** — describe it
  instead if it would

### What to expect

This is a side project, not a company, so timelines are honest rather than
impressive:

| Stage | Timeframe |
| ----- | --------- |
| First acknowledgement | within **7 days** |
| Initial assessment (accepted / declined / need more info) | within **14 days** |
| Fix released, for accepted reports | next release, or sooner if serious |

**If it's accepted:** it gets fixed in the next release and named in the release
notes. You'll be credited by whatever name or handle you prefer — or left out
entirely, your call.

**If it's declined:** you'll get an actual explanation of why, not a form letter.
If you disagree, say so — plenty of "won't fix" calls deserve a second look.

**If it goes quiet:** ping the thread. Missing a message is far more likely than
ignoring one.

Please give a fix a reasonable window before disclosing publicly. There's no bug
bounty — there's no money behind this project — but credit is guaranteed.

## Scope

### In scope

- **Privacy bypass** — anything that causes a face to be published uncovered when
  the cloak is engaged: detection gaps that defeat the cover latch, frames
  escaping before the cover is applied, the fail-safe paths not firing
- **Virtual camera output** — issues in the OBS shared-memory sink, including
  frames leaking to other processes or stale frames being served after a stop
- **The updater** — manifest handling, the download path, anything that could
  cause GhostCam to fetch or run something it shouldn't
- **Local data** — settings, uploaded mask and backdrop images, anything written
  to disk in a way that exposes more than it should
- **The installer and portable build** — privilege issues, insecure write paths,
  unexpected persistence
- **Any unexpected network traffic.** GhostCam should make exactly one kind of
  network call: the update check. Anything else is a bug worth reporting.

### Out of scope

These are known and documented — reports about them will be closed politely:

- **The SmartScreen warning and antivirus false positives.** GhostCam isn't
  code-signed yet. See [`packaging/RELEASE-CHECKLIST.md`](packaging/RELEASE-CHECKLIST.md)
  for the full explanation and what's being done about it.
- **Background replacement not being a perfect cutout.** It's a geometric mask
  derived from the face box, not a segmentation model. This is documented in the
  release notes and tunable via **CUTOUT WIDTH**.
- **Face detection missing a face in genuinely hard conditions** (extreme angles,
  near-darkness, heavy occlusion). Paranoid mode and the cover latch exist as the
  fail-safes. If those *also* fail, that **is** in scope.
- **Vulnerabilities in OBS Studio itself** — please report those to
  [the OBS project](https://github.com/obsproject/obs-studio/security).
- Anything requiring an attacker who already has admin rights or physical access
  to the machine.

## What GhostCam does with your data

Stated plainly, so you know what you're testing against:

- **Your video never leaves your computer.** Frames go from the camera, through
  processing, into the local OBS shared-memory buffer. Nothing is uploaded,
  nothing is recorded to disk.
- **No telemetry. No analytics. No accounts.**
- **One network call:** an HTTPS `GET` for `update.json` on GitHub, to compare
  version numbers. It sends nothing about you beyond what any HTTP request
  reveals, and it can be turned off in **SETUP → CHECK FOR UPDATES**.
- **Local storage:** settings live in `%AppData%\GhostCam\settings.json`. Mask and
  backdrop images are referenced by path, not copied.

## Known limitations

Disclosed deliberately — a security policy that hides weaknesses is worse than no
policy:

- **The app is not code-signed.** Windows can't verify the publisher, so you're
  trusting the download itself. Verify it against the published SHA256 checksums,
  or build from source.
- **The updater does not verify what it downloads.** The manifest is fetched over
  HTTPS and the installer URL points at GitHub releases, so transport is
  protected — but the downloaded installer's signature and checksum are **not**
  independently checked before it runs. Anyone able to modify the published
  `update.json` could redirect that download. This is a known gap and is on the
  list to fix.
- **Detection is best-effort.** No face detector is perfect. The cover latch and
  paranoid mode exist because of that, not in spite of it.

## Building it yourself

If you'd rather not trust a prebuilt binary, everything needed to build GhostCam
is in this repo:

```bash
git clone https://github.com/RDXFGXY1/OnyxGhostCam.git
cd OnyxGhostCam
./get-model.ps1
dotnet build src/Onyx.App/Onyx.App.csproj -c Release
```

---

*GhostCam — KYROS · Null Studio*
