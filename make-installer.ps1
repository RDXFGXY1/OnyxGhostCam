<#
.SYNOPSIS
    Builds the Onyx // Ghost Cam Windows installer.

    Publishes a self-contained Release build, bundles the ONNX model, then
    compiles packaging\Onyx.iss with Inno Setup into dist\.

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

Write-Host "==> [1/4] refreshing app icon" -ForegroundColor Cyan
& (Join-Path $root 'tools\make-icon.ps1')

Write-Host "==> [2/4] publishing self-contained Release build" -ForegroundColor Cyan
$out = Join-Path $root 'publish'
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $root 'src\Onyx.App\Onyx.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE) { throw "publish failed" }

Write-Host "==> [3/4] bundling face model" -ForegroundColor Cyan
$model = Join-Path $root 'models\version-RFB-320.onnx'
if (Test-Path $model) {
    $md = Join-Path $out 'models'
    New-Item -ItemType Directory -Force -Path $md | Out-Null
    Copy-Item $model $md -Force
    Write-Host "    bundled $(Split-Path $model -Leaf)" -ForegroundColor Green
} else {
    Write-Host "    WARNING: models\version-RFB-320.onnx not found - run .\get-model.ps1 first" -ForegroundColor Yellow
}

Write-Host "==> [4/4] compiling installer (Inno Setup)" -ForegroundColor Cyan
$iscc = Find-ISCC
if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup 6 not found. Install it, then re-run this script:" -ForegroundColor Yellow
    Write-Host "    winget install -e --id JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "(The published build is ready in .\publish - only the installer step is missing.)"
    return
}

& $iscc (Join-Path $root 'packaging\Onyx.iss')
if ($LASTEXITCODE) { throw "Inno Setup compile failed" }

$setup = Get-ChildItem (Join-Path $root 'dist') -Filter 'GhostCam-Setup-*.exe' |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "==> installer ready:" -ForegroundColor Green
Write-Host "    $($setup.FullName)  ($([math]::Round($setup.Length/1MB,1)) MB)" -ForegroundColor Green
