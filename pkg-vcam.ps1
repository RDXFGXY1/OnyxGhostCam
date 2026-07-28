<#
.SYNOPSIS
    Packages the Onyx VCam host as an MSIX (dev-mode loose registration) to give
    it PACKAGE IDENTITY, then registers it. This lets the host obtain camera
    consent, which MFCreateVirtualCamera requires before the Windows Frame Server
    will stream our virtual camera. No code-signing certificate needed.

    Run from an ADMINISTRATOR PowerShell (dev-mode + media-source HKLM registration).

.EXAMPLE
    .\pkg-vcam.ps1              # build + deploy source + register package
    .\pkg-vcam.ps1 -Unregister  # remove the package
#>
param([switch]$Unregister)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$pkgName = 'Onyx.GhostCam'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) { throw "Run this from an ADMINISTRATOR PowerShell." }

if ($Unregister) {
    Get-AppxPackage $pkgName -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
    Write-Host "Unregistered $pkgName." -ForegroundColor Yellow
    return
}

function Find-MSBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
}
$msbuild = Find-MSBuild

Write-Host "==> [1/5] enabling Developer Mode" -ForegroundColor Cyan
$devKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
New-Item -Path $devKey -Force | Out-Null
Set-ItemProperty -Path $devKey -Name AllowDevelopmentWithoutDevLicense -Value 1 -Type DWord

Write-Host "==> [2/5] building native DLL (Debug) + host (Release)" -ForegroundColor Cyan
& $msbuild "$root\src\Onyx.Native\Onyx.Native.vcxproj"   /p:Configuration=Debug   /p:Platform=x64 /v:minimal /nologo
if ($LASTEXITCODE) { throw "native build failed" }
& $msbuild "$root\src\Onyx.VCamHost\Onyx.VCamHost.vcxproj" /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo
if ($LASTEXITCODE) { throw "host build failed" }

Write-Host "==> [3/5] registering media source (HKLM)" -ForegroundColor Cyan
& "$root\deploy-vcam.ps1"

Write-Host "==> [4/5] assembling package layout + assets" -ForegroundColor Cyan
$layout = Join-Path $root 'packaging\layout'
$assets = Join-Path $layout 'Assets'
Remove-Item $layout -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Copy-Item "$root\src\Onyx.VCamHost\x64\Release\Onyx.VCamHost.exe" $layout -Force
Copy-Item "$root\packaging\msix\AppxManifest.xml" $layout -Force

Add-Type -AssemblyName System.Drawing
function New-Logo($path, $w, $h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 200, 24, 24))
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
New-Logo (Join-Path $assets 'Square44x44Logo.png')   44  44
New-Logo (Join-Path $assets 'Square150x150Logo.png') 150 150
New-Logo (Join-Path $assets 'StoreLogo.png')         50  50

Write-Host "==> [5/5] registering package (dev-mode, no signing)" -ForegroundColor Cyan
Get-AppxPackage $pkgName -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Register (Join-Path $layout 'AppxManifest.xml')

$pkg = Get-AppxPackage $pkgName
Write-Host "`nRegistered: $($pkg.PackageFullName)" -ForegroundColor Green
Write-Host "Run the packaged host with:  OnyxVCam.exe" -ForegroundColor Green
Write-Host "First make sure Settings > Privacy & security > Camera > 'Let apps access your camera' is ON," -ForegroundColor Yellow
Write-Host "and that 'Onyx Ghost Cam' (or desktop apps) is allowed." -ForegroundColor Yellow
