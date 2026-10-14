<#
.SYNOPSIS
    Builds the Windows installer (Inno Setup) from a fresh publish.

.DESCRIPTION
    1. Runs scripts/publish.ps1 for win-x64 (skip with -SkipPublish to reuse artifacts/publish/win-x64).
    2. Compiles packaging/windows/upsteper.iss with Inno Setup 6 into artifacts/installer.

    Needs Inno Setup 6 (`winget install JRSoftware.InnoSetup`) and, for the publish step, the native
    library (scripts/build-native.ps1). Design and manual test steps: docs/packaging.md.

.PARAMETER Version
    SemVer version, e.g. 0.1.0-alpha (default: VersionPrefix-VersionSuffix from Directory.Build.props).

.PARAMETER SkipPublish
    Reuse the existing artifacts/publish/win-x64 folder.

.EXAMPLE
    ./scripts/build-installer.ps1
.EXAMPLE
    ./scripts/build-installer.ps1 -Version 0.1.0-alpha.2
#>
[CmdletBinding()]
param(
    [string] $Version,
    [switch] $SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts/publish/win-x64'
$outputDir = Join-Path $root 'artifacts/installer'

if (-not $Version) {
    [xml] $props = Get-Content (Join-Path $root 'Directory.Build.props')
    $group = $props.Project.PropertyGroup | Where-Object { $_.VersionPrefix } | Select-Object -First 1
    $Version = if ($group.VersionSuffix) { "$($group.VersionPrefix)-$($group.VersionSuffix)" } else { $group.VersionPrefix }
}
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') {
    throw "Version '$Version' is not a SemVer version such as 0.1.0 or 0.1.0-alpha."
}
$numericVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue | ForEach-Object Source),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup'
}

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'publish.ps1') -Rid win-x64 -Version $Version
}
if (-not (Test-Path (Join-Path $publishDir 'UpSTEPer.Desktop.exe'))) {
    throw "No published app in $publishDir; run without -SkipPublish."
}

Write-Host "==> Installer $Version" -ForegroundColor Cyan
& $iscc /Q "/DAppVersion=$Version" "/DNumericVersion=$numericVersion" "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" (Join-Path $root 'packaging/windows/upsteper.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

$setup = Join-Path $outputDir "UpSTEPer-$Version-win-x64-setup.exe"
Write-Host ("Installer: {0} ({1:N0} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)) -ForegroundColor Green
