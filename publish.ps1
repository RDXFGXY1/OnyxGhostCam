<#
.SYNOPSIS
    Publishes a self-contained Release build of the Onyx app to .\publish\.

    Produces a folder that runs on any Windows 11 x64 machine without a .NET
    install. Bundles the ONNX model if present. (A full MSIX/Inno installer is a
    later step - see docs/decisions.)
#>
param(
    [switch]$SingleFile
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out  = Join-Path $root 'publish'

$props = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    "-p:PublishSingleFile=$($SingleFile.IsPresent.ToString().ToLower())"
)
if ($SingleFile) { $props += '-p:IncludeNativeLibrariesForSelfExtract=true' }

Write-Host "==> publishing Onyx.App (Release, self-contained, win-x64)" -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\Onyx.App\Onyx.App.csproj') @props -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# Bundle the face model next to the app if it has been downloaded.
$model = Join-Path $root 'models\version-RFB-320.onnx'
if (Test-Path $model) {
    $modelsOut = Join-Path $out 'models'
    New-Item -ItemType Directory -Force -Path $modelsOut | Out-Null
    Copy-Item $model $modelsOut -Force
    Write-Host "bundled model -> $modelsOut" -ForegroundColor Green
} else {
    Write-Host "note: models\version-RFB-320.onnx not found (run get-model.ps1 to include it)" -ForegroundColor Yellow
}

Write-Host "==> done. Run: $out\Onyx.exe" -ForegroundColor Green
