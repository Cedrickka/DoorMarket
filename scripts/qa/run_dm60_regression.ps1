param(
    [string]$Configuration = "Release",
    [switch]$SkipMobile
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$artifactsDir = Join-Path $repoRoot "artifacts/qa"
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

function Invoke-DotnetTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArgsLine
    )

    Write-Host "dotnet test $ArgsLine"
    Invoke-Expression "dotnet test $ArgsLine"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed for args: $ArgsLine"
    }
}

Push-Location $repoRoot
try {
    Write-Host "DM60-QA-01 - API/Web regression"
    Invoke-DotnetTest -ArgsLine "DoorMarket.Tests --configuration $Configuration --logger `"trx;LogFileName=dm60_qa01_api_web_regression.trx`""

    Write-Host "DM60-QA-01 - Web checkout smoke"
    Invoke-DotnetTest -ArgsLine "DoorMarket.Tests --configuration $Configuration --filter FullyQualifiedName~WebCheckoutSmokeTests --logger `"trx;LogFileName=dm60_qa01_web_checkout_smoke.trx`""

    if (-not $SkipMobile) {
        if (Get-Command flutter -ErrorAction SilentlyContinue) {
            Write-Host "DM60-QA-01 - Mobile checkout smoke"
            Push-Location "DoorMarket.Flutter/doormarket_flutter"
            try {
                flutter test test/features/checkout/mobile_checkout_smoke_test.dart
                if ($LASTEXITCODE -ne 0) {
                    throw "flutter test failed for mobile checkout smoke."
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            Write-Warning "Flutter not found. Mobile smoke skipped."
        }
    }

    Write-Host "DM60-QA-01 regression suite completed."
}
finally {
    Pop-Location
}
