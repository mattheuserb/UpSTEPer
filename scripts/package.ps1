<#
.SYNOPSIS
    Builds the release files of one platform into artifacts/release.

.DESCRIPTION
    win-x64     UpSTEPer-<version>-win-x64-setup.exe (installer) and UpSTEPer-<version>-win-x64.zip
    linux-x64   UpSTEPer-<version>-linux-x64.tar.gz (app and CLI)
    osx-arm64   UpSTEPer-<version>-osx-arm64-cli.tar.gz (CLI only: the app's 3D view is not
                supported on macOS yet)

    Each archive holds one folder UpSTEPer-<version>. Runs scripts/publish.ps1 first and, on
    Windows, scripts/build-installer.ps1 (needs Inno Setup 6). Used by CI and the release
    workflow; see docs/packaging.md.

.PARAMETER Rid
    win-x64, linux-x64 or osx-arm64.

.PARAMETER Version
    SemVer version, e.g. 0.1.0-alpha (default: Directory.Build.props).

.EXAMPLE
    ./scripts/package.ps1 -Rid win-x64 -Version 0.1.0-alpha
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('win-x64', 'linux-x64', 'osx-arm64')] [string] $Rid,
    [string] $Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Join-Path $root 'artifacts/release'
$publish = Join-Path $root "artifacts/publish/$Rid"

if (-not $Version) {
    [xml] $props = Get-Content (Join-Path $root 'Directory.Build.props')
    $group = $props.Project.PropertyGroup | Where-Object { $_.VersionPrefix } | Select-Object -First 1
    $Version = if ($group.VersionSuffix) { "$($group.VersionPrefix)-$($group.VersionSuffix)" } else { $group.VersionPrefix }
}

New-Item -ItemType Directory -Force $release | Out-Null
$cliOnly = $Rid -like 'osx-*'
& (Join-Path $PSScriptRoot 'publish.ps1') -Rid $Rid -Version $Version -CliOnly:$cliOnly

# Archives contain one top-level folder, so unpacking never scatters files.
$name = "UpSTEPer-$Version"
$staging = Join-Path ([System.IO.Path]::GetTempPath()) "upsteper-package-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $staging | Out-Null
try {
    Copy-Item $publish (Join-Path $staging $name) -Recurse
    if ($Rid -like 'win-*') {
        $zip = Join-Path $release "$name-$Rid.zip"
        Remove-Item $zip -ErrorAction SilentlyContinue
        Compress-Archive -Path (Join-Path $staging $name) -DestinationPath $zip
        & (Join-Path $PSScriptRoot 'build-installer.ps1') -Version $Version -SkipPublish
        Copy-Item (Join-Path $root "artifacts/installer/$name-$Rid-setup.exe") $release
    }
    else {
        $suffix = if ($cliOnly) { "$Rid-cli" } else { $Rid }
        # tar keeps the executable bits of upsteper and UpSTEPer.Desktop.
        tar -czf (Join-Path $release "$name-$suffix.tar.gz") -C $staging $name
        if ($LASTEXITCODE -ne 0) { throw "tar failed with exit code $LASTEXITCODE." }
    }
}
finally {
    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
}

Get-ChildItem $release | ForEach-Object { Write-Host ("{0} ({1:N0} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor Green }
