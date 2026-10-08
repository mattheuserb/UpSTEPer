<#
.SYNOPSIS
    Builds the coverage report from Cobertura files and enforces the minimum coverage.

.DESCRIPTION
    Used by scripts/check.ps1 and by CI, so thresholds live in exactly one place.
    Reads artifacts/coverage/*.cobertura.xml (one per test project), writes an HTML report,
    a Markdown summary and a JSON summary to <CoverageDir>/report, prints the totals and
    exits with 1 when line or branch coverage is below the minimum.

    Policy (CONTRIBUTING.md, "Code quality"): thresholds only go up. Lowering one needs a
    reason in the commit message.

.PARAMETER CoverageDir
    Folder containing the Cobertura files. Default: artifacts/coverage.

.PARAMETER MinLineCoverage
    Minimum line coverage in percent.

.PARAMETER MinBranchCoverage
    Minimum branch coverage in percent.

.PARAMETER GitHubSummary
    Append the Markdown summary to $env:GITHUB_STEP_SUMMARY (CI job page).

.PARAMETER ReportOnly
    Write the report but do not enforce the minimum (used locally when the native library has
    not been built and the kernel tests were skipped).
#>
[CmdletBinding()]
param(
    [string] $CoverageDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/coverage'),
    [double] $MinLineCoverage = 90,
    [double] $MinBranchCoverage = 70,
    [switch] $GitHubSummary,
    [switch] $ReportOnly
)

$ErrorActionPreference = 'Stop'
$reportDir = Join-Path $CoverageDir 'report'

dotnet reportgenerator "-reports:$CoverageDir/*.cobertura.xml" "-targetdir:$reportDir" `
    '-reporttypes:Html;MarkdownSummaryGithub;TextSummary;JsonSummary' `
    '-assemblyfilters:-*.Tests' '-filefilters:-*.g.cs' -verbosity:Warning
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$summary = (Get-Content (Join-Path $reportDir 'Summary.json') -Raw | ConvertFrom-Json).summary
$line = [double] $summary.linecoverage
$branch = [double] $summary.branchcoverage

if ($GitHubSummary -and $env:GITHUB_STEP_SUMMARY) {
    Get-Content (Join-Path $reportDir 'SummaryGithub.md') | Add-Content $env:GITHUB_STEP_SUMMARY
}

$inv = [Globalization.CultureInfo]::InvariantCulture
Write-Host ('Line coverage:   {0}% (minimum {1}%)' -f $line.ToString($inv), $MinLineCoverage.ToString($inv))
Write-Host ('Branch coverage: {0}% (minimum {1}%)' -f $branch.ToString($inv), $MinBranchCoverage.ToString($inv))
Write-Host "HTML report: $(Join-Path $reportDir 'index.html')"

if ($ReportOnly) {
    Write-Host 'Minimum coverage not enforced (-ReportOnly).' -ForegroundColor Yellow
    exit 0
}

$failed = $false
if ($line -lt $MinLineCoverage) {
    Write-Host "Line coverage $line% is below the minimum of $MinLineCoverage%." -ForegroundColor Red
    $failed = $true
}
if ($branch -lt $MinBranchCoverage) {
    Write-Host "Branch coverage $branch% is below the minimum of $MinBranchCoverage%." -ForegroundColor Red
    $failed = $true
}
if ($failed) { exit 1 }
