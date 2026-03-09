param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..")).Path
$artifactsDir = Join-Path $repoRoot "artifacts/qa"
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

function Invoke-DotnetTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Filter,
        [Parameter(Mandatory = $true)]
        [string]$LogName
    )

    dotnet test DoorMarket.Tests `
        --configuration $Configuration `
        --filter $Filter `
        --logger "trx;LogFileName=$LogName"

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed for filter '$Filter'."
    }
}

$filters = @(
    "FullyQualifiedName~AdminReconciliationControllerTests",
    "FullyQualifiedName~AdminCommissionsControllerTests",
    "FullyQualifiedName~AdminNotificationsControllerTests",
    "FullyQualifiedName~NotificationReplayServiceTests",
    "FullyQualifiedName~ClientOrderNotificationServiceTests"
)

Push-Location $repoRoot
try {
    foreach ($filter in $filters) {
        $safeName = ($filter -replace '[^A-Za-z0-9_]', '_')
        Write-Host "Running QA-03 filter: $filter"
        Invoke-DotnetTest -Filter $filter -LogName "dm60_qa03_$safeName.trx"
    }

    Write-Host "DM60-QA-03 non-regression suite completed."
}
finally {
    Pop-Location
}
