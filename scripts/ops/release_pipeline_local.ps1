param(
    [string]$Environment = "Production",
    [string]$ConnectionString = "",
    [string]$SmokeBaseUrl = "",
    [switch]$SkipMigration,
    [switch]$SkipSmoke
)

$ErrorActionPreference = "Stop"

Write-Host "Step 1/4 - Build"
dotnet build DoorMarket.Api/DoorMarket.Api.csproj
dotnet build DoorMarket.Web/DoorMarket.Web.csproj
dotnet build DoorMarket.Tests/DoorMarket.Tests.csproj

Write-Host "Step 2/4 - Targeted smoke tests (web/mobile mocked)"
dotnet test DoorMarket.Tests --filter FullyQualifiedName~WebCheckoutSmokeTests

if (-not $SkipMigration) {
    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        throw "ConnectionString is required when migration is enabled."
    }

    Write-Host "Step 3/4 - Database migration"
    ./scripts/ops/run_migration.ps1 -ConnectionString $ConnectionString -Environment $Environment
}
else {
    Write-Host "Step 3/4 - Migration skipped"
}

if (-not $SkipSmoke) {
    if ([string]::IsNullOrWhiteSpace($SmokeBaseUrl)) {
        throw "SmokeBaseUrl is required when smoke is enabled."
    }

    Write-Host "Step 4/4 - Runtime smoke"
    ./scripts/ops/release_smoke.ps1 -BaseUrl $SmokeBaseUrl
}
else {
    Write-Host "Step 4/4 - Runtime smoke skipped"
}

Write-Host "Local release pipeline completed."
