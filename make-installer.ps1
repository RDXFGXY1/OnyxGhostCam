<#
.SYNOPSIS
    Builds the GhostCam release artifacts.

    Publishes a self-contained Release build, bundles the ONNX model, packs a
    portable ZIP, compiles packaging\Onyx.iss with Inno Setup, and writes
    SHA256SUMS - all into dist\.

    The portable ZIP exists because an unsigned Inno installer draws antivirus
    false positives; a plain archive does not. See packaging\RELEASE-CHECKLIST.md.

    Requires Inno Setup 6:  winget install -e --id JRSoftware.InnoSetup

.EXAMPLE
    .\make-installer.ps1
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Find-ISCC {
    # winget installs Inno Setup per-user under LOCALAPPDATA\Programs; the
    # classic installer puts it under Program Files.
    $c = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $c) { if (Test-Path $p) { return $p } }
    try { return (Get-Command iscc -ErrorAction Stop).Source } catch { }
    # Last resort: shallow search of the usual roots.
    foreach ($base in @("$env:LOCALAPPDATA\Programs", "$env:ProgramFiles", "${env:ProgramFiles(x86)}")) {
        if (Test-Path $base) {
            $hit = Get-ChildItem $base -Filter ISCC.exe -Recurse -Depth 3 -ErrorAction SilentlyContinue |
                   Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    return $null
}

Write-Host "==> [1/6] refreshing app icon" -ForegroundColor Cyan
& (Join-Path $root 'tools\make-icon.ps1')

Write-Host "==> [2/6] publishing self-contained Release build" -ForegroundColor Cyan
$out = Join-Path $root 'publish'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $root 'src\Onyx.App\Onyx.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE) { throw "publish failed" }

Write-Host "==> [3/6] bundling face model" -ForegroundColor Cyan
$model = Join-Path $root 'models\version-RFB-320.onnx'
if (Test-Path $model) {
    $md = Join-Path $out 'models'
    New-Item -ItemType Directory -Force -Path $md | Out-Null
    Copy-Item $model $md -Force
    Write-Host "    bundled $(Split-Path $model -Leaf)" -ForegroundColor Green
} else {
    Write-Host "    WARNING: models\version-RFB-320.onnx not found - run .\get-model.ps1 first" -ForegroundColor Yellow
}

Write-Host "==> [4/6] building portable ZIP" -ForegroundColor Cyan
# A plain ZIP trips none of the installer heuristics that flag an unsigned setup
# stub, so it's the fallback to hand anyone whose antivirus blocks the installer.
# Extract and run - no install, no registry, no admin.
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$ver = (Select-String -Path (Join-Path $root 'packaging\Onyx.iss') `
        -Pattern '^#define\s+AppVersion\s+"([^"]+)"').Matches[0].Groups[1].Value
$zip = Join-Path $dist "GhostCam-Portable-$ver.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue

# Ship the read-me next to the binaries so the ZIP stands on its own.
foreach ($f in @('README.txt', 'LICENSE.txt', 'ABOUT.txt')) {
    $src = Join-Path $root "packaging\$f"
    if (Test-Path $src) { Copy-Item $src $out -Force }
}
Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "    $(Split-Path $zip -Leaf)  ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)" -ForegroundColor Green

Write-Host "==> [5/6] compiling installer (Inno Setup)" -ForegroundColor Cyan
$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup 6 not found. Install it, then re-run this script:" -ForegroundColor Yellow
    Write-Host "    winget install -e --id JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "(The published build and portable ZIP are ready - only the installer is missing.)"
    return
}

& $iscc (Join-Path $root 'packaging\Onyx.iss')
if ($LASTEXITCODE) { throw "Inno Setup compile failed" }

Write-Host "==> [6/6] writing SHA256 checksums" -ForegroundColor Cyan
# Publish these alongside the release so people can verify they got the real file
# rather than something a mirror or a stranger handed them.
$setup = Get-ChildItem $dist -Filter 'GhostCam-Setup-*.exe' |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1

$sumFile = Join-Path $dist "SHA256SUMS-$ver.txt"
$lines = foreach ($f in @($setup, (Get-Item $zip))) {
    if ($f) { "$((Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower())  $($f.Name)" }
}
$lines | Set-Content $sumFile -Encoding utf8
$lines | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "==> release artifacts in $dist" -ForegroundColor Green
Write-Host "    $($setup.Name)  ($([math]::Round($setup.Length/1MB,1)) MB)" -ForegroundColor Green
Write-Host "    $(Split-Path $zip -Leaf)  ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)" -ForegroundColor Green
Write-Host "    $(Split-Path $sumFile -Leaf)" -ForegroundColor Green
Write-Host ""
Write-Host "    Next: packaging\RELEASE-CHECKLIST.md (VirusTotal + false-positive submissions)" -ForegroundColor Cyan
