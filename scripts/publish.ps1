<#
.SYNOPSIS
    Builds the self-contained distribution of the app and the CLI for one platform.

.DESCRIPTION
    Publishes UpSTEPer.App (UpSTEPer.Desktop.exe) and UpSTEPer.Cli (upsteper.exe) self-contained
    into one folder, together with the native library, OCCT and its dependencies. On Windows the
    MSVC runtime DLLs the native code needs are copied next to them (app-local deployment, no
    VC++ Redistributable or admin rights needed). The licence texts of the native libraries
    (installed by CMake to artifacts/native/<Rid>/licenses) go to licenses/.

    The result is the input of the installer (packaging/windows) and of the archives of a release.
    Needs the native library (scripts/build-native.ps1) for the same platform.

.PARAMETER Rid
    Runtime identifier and native preset: win-x64 (default on Windows), linux-x64, osx-arm64.

.PARAMETER Version
    Version to stamp into the assemblies, e.g. 0.1.0-alpha (default: Directory.Build.props).

.PARAMETER Output
    Target folder. Default: artifacts/publish/<Rid>. It is emptied first.

.PARAMETER CliOnly
    Publish the CLI only (macOS releases: the app's 3D view is not supported there yet).

.EXAMPLE
    ./scripts/publish.ps1
.EXAMPLE
    ./scripts/publish.ps1 -Version 0.1.0-alpha.2
#>
[CmdletBinding()]
param(
    [string] $Rid = 'win-x64',
    [string] $Version,
    [string] $Output,
    [switch] $CliOnly
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$native = Join-Path $root "artifacts/native/$Rid"
if (-not $Output) { $Output = Join-Path $root "artifacts/publish/$Rid" }

if (-not (Test-Path (Join-Path $native '*upsteper_native*'))) {
    throw "Native library for $Rid not found in $native. Build it first (scripts/build-native.ps1, docs/development.md)."
}

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    # dotnet writes warnings to stderr; Windows PowerShell 5.1 would turn them into terminating errors.
    $ErrorActionPreference = 'Continue'
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

if (Test-Path $Output) { Remove-Item -Recurse -Force $Output }
New-Item -ItemType Directory -Force $Output | Out-Null

# Only the UI languages of the app (Localization/Strings.*.resx); packages bring many more.
$properties = @("-p:UpsNativeRid=$Rid", '-p:SatelliteResourceLanguages=en%3Bde')
if ($Version) { $properties += "-p:Version=$Version" }

$projects = if ($CliOnly) { @('src/UpSTEPer.Cli') } else { @('src/UpSTEPer.App', 'src/UpSTEPer.Cli') }
foreach ($project in $projects) {
    Invoke-Step "Publish $project" {
        dotnet publish (Join-Path $root $project) -c Release -r $Rid --self-contained -o $Output --nologo @properties
    }
}

# Debug symbols of native NuGet libraries (SkiaSharp, HarfBuzzSharp: about 100 MB) are of no use
# to users; the small portable PDBs of UpSTEPer's own assemblies stay for stack traces in the log.
Get-ChildItem $Output -Filter 'lib*.pdb' | Remove-Item

if ($Rid -like 'win-*') {
    Write-Host '==> MSVC runtime (app-local)' -ForegroundColor Cyan
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    if (-not $vsPath) { throw 'No Visual Studio installation with the C++ workload found.' }
    $arch = $Rid.Substring(4)
    # The newest redist folder that has a CRT for this architecture (14.x versions sort as text).
    $crt = Get-ChildItem (Join-Path $vsPath 'VC\Redist\MSVC') -Directory |
        Sort-Object Name -Descending |
        ForEach-Object { Get-ChildItem (Join-Path $_.FullName $arch) -Directory -Filter 'Microsoft.VC*.CRT' -ErrorAction SilentlyContinue } |
        Select-Object -First 1
    if (-not $crt) { throw "MSVC runtime DLLs for $arch not found below $vsPath\VC\Redist\MSVC." }
    # The DLLs upsteper_native and OCCT import (see docs/packaging.md).
    foreach ($dll in 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll') {
        Copy-Item (Join-Path $crt.FullName $dll) $Output
    }
    Write-Host "    from $($crt.FullName)"
}

Write-Host '==> Licences' -ForegroundColor Cyan
$licenses = Join-Path $Output 'licenses'
New-Item -ItemType Directory -Force $licenses | Out-Null
$nativeLicenses = Join-Path $native 'licenses'
if (-not (Test-Path $nativeLicenses)) {
    throw "Licence texts not found in $nativeLicenses; rebuild the native library (cmake --install installs them)."
}
foreach ($port in Get-ChildItem $nativeLicenses -Directory) {
    $copyright = Join-Path $port.FullName 'copyright'
    if (Test-Path $copyright) { Copy-Item $copyright (Join-Path $licenses "$($port.Name).txt") }
}
if (-not $CliOnly) {
    Copy-Item (Join-Path $root 'src/UpSTEPer.App/Themes/Lucide-LICENSE.txt') (Join-Path $licenses 'lucide.txt')
}
# The app build copies these too; the CLI alone does not.
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $Output -Force

$size = (Get-ChildItem $Output -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Published to {0} ({1:N0} MB)" -f $Output, $size) -ForegroundColor Green
