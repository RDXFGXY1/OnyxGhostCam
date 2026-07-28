<#
.SYNOPSIS
    Self-contained virtual-camera diagnostic (run as ADMINISTRATOR).

    Enables the Windows Frame Server camera logs, launches the Onyx host, probes
    enumeration, then dumps the Frame Server's own error events so we can see WHY
    the camera does not appear even though Start() succeeded.
#>
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Continue'

$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()
    ).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
if (-not $isAdmin) { throw "Run this from an ADMINISTRATOR PowerShell." }

$root  = $PSScriptRoot
$logs  = @('Microsoft-Windows-MF-FrameServer/Camera_FrameServer',
           'Microsoft-Windows-MF-FrameServer/Camera_DeviceMFT')

Write-Host "==> enabling + clearing Frame Server logs" -ForegroundColor Cyan
foreach ($l in $logs) { wevtutil sl "$l" /e:true 2>$null; wevtutil cl "$l" 2>$null }

$hostExe  = Join-Path $root "src\Onyx.VCamHost\x64\$Configuration\Onyx.VCamHost.exe"
$probeExe = Join-Path $root "onyx-probe.exe"

Write-Host "==> launching host" -ForegroundColor Cyan
$proc = Start-Process $hostExe -PassThru -WindowStyle Minimized
Start-Sleep -Seconds 3

Write-Host "==> probing enumeration" -ForegroundColor Cyan
& $probeExe
Start-Sleep -Seconds 2

Write-Host "==> stopping host" -ForegroundColor Cyan
Stop-Process -Id $proc.Id -Force 2>$null
Start-Sleep -Seconds 2

$since = (Get-Date).AddMinutes(-2)
foreach ($l in $logs) {
    Write-Host "`n===== $l =====" -ForegroundColor Yellow
    try {
        Get-WinEvent -LogName $l -Oldest -ErrorAction Stop |
            Where-Object { $_.TimeCreated -gt $since } |
            Select-Object TimeCreated, Id, LevelDisplayName, Message |
            Format-List
    } catch { Write-Host "(no events / $($_.Exception.Message))" }
}
