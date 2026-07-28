<#
.SYNOPSIS
    Builds the whole Onyx solution (native C++ + managed C#).

.DESCRIPTION
    Onyx mixes a C++ project (Onyx.Native) with C# projects. No single build
    tool handles both cleanly:
      * `dotnet`  builds C#  but not C++ (.vcxproj)
      * VS MSBuild builds C++ but not the SDK-style C# projects on this machine
    So this script uses the correct tool for each and reports one combined result.

.EXAMPLE
    .\build.ps1                 # Debug build of everything
    .\build.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Find-MSBuild {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { throw "vswhere.exe not found - is Visual Studio installed?" }
    $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
    if (-not $msbuild) { throw "MSBuild not found - install the 'Desktop development with C++' workload." }
    return $msbuild
}

Write-Host "==> [1/2] Building native C++ (Onyx.Native) [$Configuration|x64]" -ForegroundColor Cyan
$msbuild = Find-MSBuild
& $msbuild "$root\src\Onyx.Native\Onyx.Native.vcxproj" /p:Configuration=$Configuration /p:Platform=x64 /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Native build failed." }

Write-Host "`n==> [2/2] Building managed C# (Core, App, Tests) [$Configuration]" -ForegroundColor Cyan
& dotnet build "$root\Onyx.CSharp.slnf" -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "Managed build failed." }

Write-Host "`n==> Build succeeded: native + managed [$Configuration]" -ForegroundColor Green
