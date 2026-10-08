<#
.SYNOPSIS
    Builds the upsteper_native bridge (and OCCT via vcpkg) on Windows.

.DESCRIPTION
    Locates Visual Studio / Build Tools with the C++ workload via vswhere, enters its
    developer environment (MSVC, CMake, Ninja), then configures, builds and installs the
    native library into artifacts/native/<preset>. The first run builds OCCT from source
    and takes 30-90 minutes; later runs use the vcpkg binary cache.

.PARAMETER Preset
    CMake preset from native/CMakePresets.json. Default: win-x64.

.PARAMETER VcpkgRoot
    vcpkg checkout. Defaults to $env:VCPKG_ROOT. Use a short path without spaces (e.g. C:\vcpkg).

.EXAMPLE
    ./scripts/build-native.ps1 -VcpkgRoot C:\vcpkg
#>
[CmdletBinding()]
param(
    [string] $Preset = 'win-x64',
    [string] $VcpkgRoot = $env:VCPKG_ROOT
)

$ErrorActionPreference = 'Stop'

if (-not $VcpkgRoot -or -not (Test-Path (Join-Path $VcpkgRoot 'vcpkg.exe'))) {
    throw "vcpkg not found. Clone and bootstrap it (see docs/development.md) and pass -VcpkgRoot or set VCPKG_ROOT."
}
$vcpkgRootPath = (Resolve-Path $VcpkgRoot).Path

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw "No Visual Studio installation with the C++ workload found ('Desktop development with C++')."
}

# The VS developer scripts call vswhere.exe without a path.
$env:PATH = "$(Split-Path $vswhere);$env:PATH"
Import-Module (Join-Path $vsPath 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null

# Must come after Enter-VsDevShell: it points VCPKG_ROOT at the VS-bundled vcpkg, which has
# no pinned baseline and a long, space-containing path.
$env:VCPKG_ROOT = $vcpkgRootPath

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

Push-Location (Join-Path $PSScriptRoot '..\native')
try {
    Invoke-Step 'Configure' { cmake --preset $Preset }
    Invoke-Step 'Build' { cmake --build --preset $Preset }
    Invoke-Step 'Install' { cmake --install "build/$Preset" }
}
finally {
    Pop-Location
}

Write-Host "Native library installed to artifacts/native/$Preset" -ForegroundColor Green
