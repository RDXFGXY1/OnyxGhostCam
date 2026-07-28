<#
.SYNOPSIS
    Downloads the UltraFace (version-RFB-320) ONNX face-detection model into models/.

    Small (~1.2 MB), fast, permissively licensed. Runs fully offline once fetched.
    Source: the official onnx/models repository.
#>
$ErrorActionPreference = 'Stop'

$dest = Join-Path $PSScriptRoot 'models\version-RFB-320.onnx'
$url  = 'https://github.com/onnx/models/raw/main/validated/vision/body_analysis/ultraface/models/version-RFB-320.onnx'

if (Test-Path $dest) {
    Write-Host "Model already present: $dest" -ForegroundColor Green
    return
}

New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
Write-Host "Downloading UltraFace model..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $dest

$size = [math]::Round((Get-Item $dest).Length / 1KB)
Write-Host "Saved -> $dest  (${size} KB)" -ForegroundColor Green
