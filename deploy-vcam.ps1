<#
.SYNOPSIS
    Deploys and registers the Onyx virtual-camera media source (admin required).

.DESCRIPTION
    The Windows Camera Frame Server loads our media source DLL inside a system
    service. That service:
      * cannot read the user's Documents folder  -> copy the DLL to ProgramData
      * cannot read the user's HKCU registration  -> register under HKLM
    Both require elevation, so run this from an *administrator* PowerShell.

.EXAMPLE
    .\deploy-vcam.ps1              # copy + register
    .\deploy-vcam.ps1 -Unregister # unregister + remove
#>
param(
    [switch]$Unregister,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

# Must be elevated (HKLM + ProgramData).
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) {
    throw "Run this from an ADMINISTRATOR PowerShell (right-click > Run as administrator)."
}

$deployDir = 'C:\ProgramData\Onyx'

if ($Unregister) {
    Get-ChildItem $deployDir 'Onyx.Native*.dll' -ErrorAction SilentlyContinue |
        ForEach-Object { regsvr32 /u /s $_.FullName }
    Write-Host "Onyx virtual camera unregistered." -ForegroundColor Yellow
    return
}

$srcDll = Join-Path $PSScriptRoot "src\Onyx.Native\x64\$Configuration\Onyx.Native.dll"
if (-not (Test-Path $srcDll)) { throw "Build first: $srcDll not found. Run .\build.ps1" }

New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

# Versioned filename: the Frame Server may still hold a previous copy locked, so
# never overwrite in place -- deploy a fresh file and re-point the CLSID to it.
$stamp     = (Get-Date).ToString('yyyyMMdd-HHmmss')
$deployDll = Join-Path $deployDir "Onyx.Native.$stamp.dll"
Copy-Item $srcDll $deployDll -Force
Write-Host "Copied -> $deployDll" -ForegroundColor Cyan

regsvr32 /s $deployDll   # DllRegisterServer points HKLM CLSID at THIS path

# Best-effort cleanup of older, now-unused copies (skip any still locked).
Get-ChildItem $deployDir 'Onyx.Native.*.dll' |
    Where-Object { $_.FullName -ne $deployDll } |
    ForEach-Object { try { Remove-Item $_.FullName -Force -ErrorAction Stop } catch {} }

Write-Host "Registered (HKLM) -> newest DLL. Now run the host:" -ForegroundColor Green
Write-Host "    .\src\Onyx.VCamHost\x64\$Configuration\Onyx.VCamHost.exe"
