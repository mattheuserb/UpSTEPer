<#
.SYNOPSIS
    Runs the same quality checks as CI: formatting, analyzers, tests and coverage.

.DESCRIPTION
    1. Restores local .NET tools (ReportGenerator).
    2. Verifies formatting with `dotnet format --verify-no-changes` (or applies it with -Fix).
    3. Builds the solution; analyzer and compiler warnings are errors.
    4. Runs all tests with code coverage (Cobertura) into artifacts/coverage.
    5. Writes an HTML report to artifacts/coverage/report and prints a summary.

    Tests that need the native library are skipped when it has not been built
    (see docs/development.md); set UPS_REQUIRE_NATIVE=1 to make them mandatory, as CI does.
    Works with Windows PowerShell 5.1 and PowerShell 7 on Windows, Linux and macOS.

.PARAMETER Fix
    Apply formatting and code-style fixes instead of only verifying them.

.PARAMETER Configuration
    Build configuration. Default: Release (same as CI).

.PARAMETER NoCoverage
    Run tests without collecting coverage (faster).

.EXAMPLE
    ./scripts/check.ps1
.EXAMPLE
    ./scripts/check.ps1 -Fix
#>
[CmdletBinding()]
param(
    [switch] $Fix,
    [string] $Configuration = 'Release',
    [switch] $NoCoverage
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $root 'UpSTEPer.slnx'
$coverageDir = Join-Path $root 'artifacts/coverage'

function Invoke-Step([string] $Name, [scriptblock] $Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Name" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Push-Location $root
try {
    Invoke-Step 'Restore tools' { dotnet tool restore }

    if ($Fix) {
        Invoke-Step 'Format (apply)' { dotnet format $solution }
    }
    else {
        Invoke-Step 'Format (verify)' { dotnet format $solution --verify-no-changes }
    }

    Invoke-Step 'Build' { dotnet build $solution -c $Configuration }

    if ($NoCoverage) {
        Invoke-Step 'Test' { dotnet test --solution $solution -c $Configuration --no-build }
    }
    else {
        if (Test-Path $coverageDir) { Remove-Item $coverageDir -Recurse -Force }
        Invoke-Step 'Test with coverage' {
            dotnet test --solution $solution -c $Configuration --no-build `
                --coverage --coverage-output-format cobertura --results-directory $coverageDir
        }
        Invoke-Step 'Coverage report' {
            dotnet reportgenerator "-reports:$coverageDir/*.cobertura.xml" "-targetdir:$coverageDir/report" `
                '-reporttypes:Html;MarkdownSummaryGithub;TextSummary' '-assemblyfilters:-*.Tests' -verbosity:Warning
        }
        Get-Content (Join-Path $coverageDir 'report/Summary.txt') | Select-Object -Skip 1 -First 16
        Write-Host "HTML report: $(Join-Path $coverageDir 'report/index.html')"
    }

    Write-Host 'All checks passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
