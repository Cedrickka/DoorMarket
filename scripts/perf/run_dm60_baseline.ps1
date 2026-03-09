param(
    [string]$BaseUrl = "http://localhost:5292",
    [string]$OutDir = "artifacts/perf"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command k6 -ErrorAction SilentlyContinue)) {
    throw "k6 is not installed. Install k6 then rerun this script."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path

if ([System.IO.Path]::IsPathRooted($OutDir)) {
    $targetOutDir = $OutDir
}
else {
    $targetOutDir = Join-Path $repoRoot $OutDir
}

New-Item -ItemType Directory -Force -Path $targetOutDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$jsonFile = Join-Path $targetOutDir "dm60_api_baseline_$timestamp.json"
$txtFile = Join-Path $targetOutDir "dm60_api_baseline_$timestamp.txt"

$env:BASE_URL = $BaseUrl

Write-Host "Running k6 baseline against $BaseUrl"
$k6Script = Join-Path $repoRoot "scripts/perf/dm60_api_baseline.k6.js"
$previousErrorActionPreference = $ErrorActionPreference
$k6Output = $null
$k6ExitCode = 0

try {
    # k6 emits network warnings on stderr during load; treat only non-zero exit code as failure.
    $ErrorActionPreference = "Continue"
    $k6Output = & k6 run $k6Script --summary-export $jsonFile 2>&1
    $k6ExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

$k6Output | Tee-Object -FilePath $txtFile

if ($k6ExitCode -ne 0) {
    throw "k6 baseline failed."
}

Write-Host "Baseline completed."
Write-Host "Summary JSON: $jsonFile"
Write-Host "Console output: $txtFile"
