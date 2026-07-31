# GhostCam release checklist

Covers the antivirus false-positive problem. Work through it every release.

## The honest position

GhostCam is flagged because it is **unsigned and has no reputation** — not because
of anything in the code. Roughly in order of weight, what a scanner sees is:

1. An unsigned executable Windows has never encountered before. This is ~90% of it.
2. A self-contained .NET publish: a large pile of unsigned native DLLs
   (ONNX Runtime, OpenCV, DirectML).
3. Webcam access.
4. LZMA2 solid compression, which makes the installer's contents harder to scan
   statically.
5. Inno Setup, which is also popular with people shipping actual malware.

**Only a code-signing certificate removes the warning.** Everything below reduces
the surface, gives users a working alternative, and speeds up delisting. None of
it is a substitute for a signature.

### What NOT to do

Do not build a small "downloader" installer that fetches and runs the real setup.
That is the defining behaviour of a trojan downloader, heuristic engines hunt for
it specifically, and it would turn one flagged file into two. Chrome and Discord
ship stub downloaders and are fine because they are signed by publishers with
years of reputation — the signature is what saves them, not the stub.

---

## Build

```powershell
.\make-installer.ps1
```

Produces in `dist\`:

| File | Purpose |
|------|---------|
| `GhostCam-Setup-<ver>.exe` | Normal installer |
| `GhostCam-Portable-<ver>.zip` | No install, no registry, no admin — the fallback when AV blocks the installer |
| `SHA256SUMS-<ver>.txt` | Publish alongside so people can verify what they downloaded |

## Pre-release checks

- [ ] `dotnet test tests\Onyx.Core.Tests\Onyx.Core.Tests.csproj` passes
- [ ] Launch the built exe and confirm the window opens (a green build is not proof)
- [ ] `AppVersion` in `packaging\Onyx.iss` matches the tag you're about to push
- [ ] `update.json` version matches, and its URL points at **this** release's asset
      (a mismatch here causes an infinite update-prompt loop)
- [ ] Install over the top of the previous version and confirm settings survive

## Scan and submit

1. **VirusTotal** — upload both artifacts to <https://www.virustotal.com>. Save the
   permalinks; put them in the release notes. Being able to say "here is the scan,
   here are the 3 engines that disagree with the other 67" is worth a lot when
   someone messages you about a warning.

2. **Microsoft** (matters most — it drives Defender and SmartScreen):
   <https://www.microsoft.com/en-us/wdsi/filesubmission>
   Submit as **Software developer**, category **Incorrectly detected as malware**.
   Include the GitHub repo URL. Turnaround is usually 1–3 days.

3. **Every other vendor that flags it** — each has its own false-positive form.
   Search "<vendor> false positive submission". Attach the VirusTotal link and the
   repo URL. Re-submit each release; delisting is per-file-hash, not per-product.

4. **Do not** reuse an old submission for a new build. Every release is a new hash
   and starts from zero.

## Release notes

Include a short, non-defensive note. Something like:

> **Antivirus warnings:** GhostCam isn't code-signed yet (certificates are a
> recurring cost), so Windows and some scanners flag it as unknown. VirusTotal scan:
> `<link>`. Source is public in this repo. If your antivirus blocks the installer,
> use the portable ZIP instead — extract it anywhere and run `GhostCam.exe`.
> SHA256 checksums are attached.

## When you're ready to sign

The real fix, cheapest first:

- **Azure Trusted Signing** — around $10/month, run by Microsoft, and individual
  developers are eligible subject to identity validation. This is by far the best
  value now and is where to look first.
- **Microsoft Store via MSIX** — $19 one-time individual dev account. Store packages
  are signed by Microsoft, so there are no warnings at all. Costs you the custom
  installer UI and adds Store review plus a webcam capability declaration. There's a
  partial MSIX layout already in `packaging\msix\`.
- **Traditional OV certificate** — ~$200–400/yr, and since 2023 the key must live on
  a hardware token or HSM. Reputation still builds gradually after signing.
- **EV certificate** — more expensive, but grants SmartScreen reputation immediately.

After signing, add the `signtool` step to `make-installer.ps1` between the publish
and Inno Setup steps, sign `GhostCam.exe` **and** the finished setup, and always
timestamp (`/tr`) so signatures stay valid after the certificate expires.
