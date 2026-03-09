param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,
    [string]$Environment = "Production"
)

$ErrorActionPreference = "Stop"

$env:ASPNETCORE_ENVIRONMENT = $Environment
$env:ConnectionStrings__DefaultConnection = $ConnectionString

Write-Host "Running EF migration with ASPNETCORE_ENVIRONMENT=$Environment"
dotnet ef database update --project DoorMarket.Infrastructure --startup-project DoorMarket.Api

if ($LASTEXITCODE -ne 0) {
    throw "EF migration failed."
}

Write-Host "EF migration completed."
