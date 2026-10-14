<#
.SYNOPSIS
    Regenerates the screenshots in docs/images used by the README.

.DESCRIPTION
    Renders the desktop app at a fixed window size (1280x800) with the English UI, using
    `--screenshot <png> --window`. Needs the native library (scripts/build-native.ps1) and a
    machine that can show windows. Run it after visible UI changes and commit the PNG files.

    - main-window.png: the cable gland example file, unmodified (its CC BY-ND licence forbids
      changed versions, so it is never coloured for a picture).
    - colouring.png: the generated test assembly, coloured per body with `upsteper paint` into a
      temporary file.

.EXAMPLE
    ./scripts/update-screenshots.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$images = Join-Path $root 'docs/images'
$temp = Join-Path ([System.IO.Path]::GetTempPath()) "upsteper-screenshots-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $images, $temp | Out-Null

function Invoke-Checked([string] $Name, [scriptblock] $Command) {
    # The app logs to stderr; Windows PowerShell 5.1 would turn that into terminating errors.
    $ErrorActionPreference = 'Continue'
    & $Command 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

function Save-Screenshot([string] $Model, [string] $Png) {
    Invoke-Checked "Screenshot $Png" {
        dotnet run --no-build --project (Join-Path $root 'src/UpSTEPer.App') -- $Model `
            --screenshot (Join-Path $images $Png) --window --language en --view iso --quality fine
    }
}

try {
    Invoke-Checked 'Build' { dotnet build (Join-Path $root 'UpSTEPer.slnx') --nologo --verbosity quiet }

    Save-Screenshot (Join-Path $root 'examples/ESKV 12 RAL 7035 10066410.stp') 'main-window.png'

    $coloured = Join-Path $temp 'test-assembly-coloured.stp'
    Invoke-Checked 'Paint' {
        dotnet run --no-build --project (Join-Path $root 'src/UpSTEPer.Cli') -- paint `
            (Join-Path $root 'examples/generated/test-assembly.stp') $coloured `
            --body 0=#2F4D6F --body 1=#C1121C --body 4=#C1121C `
            --body 2=#E5BE01 --body 3=#E5BE01 --body 5=#E5BE01 --body 6=#E5BE01
    }
    Save-Screenshot $coloured 'colouring.png'

    Write-Host "Screenshots written to $images"
}
finally {
    Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
}
