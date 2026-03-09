param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

function Invoke-SmokeGet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url
    )

    Write-Host "GET $Url"
    return Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec $TimeoutSeconds
}

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)]
        [bool]$Condition,
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

$base = $BaseUrl.TrimEnd("/")

$ping = Invoke-SmokeGet -Url "$base/ping"
Assert-Condition -Condition ($ping -eq "pong") -Message "Smoke failed: /ping did not return pong."

$live = Invoke-SmokeGet -Url "$base/api/health/live"
Assert-Condition -Condition ($null -ne $live.status) -Message "Smoke failed: /api/health/live missing status."
Assert-Condition -Condition ($live.status -ne "Unhealthy") -Message "Smoke failed: live health is Unhealthy."

$ready = Invoke-SmokeGet -Url "$base/api/health/ready"
Assert-Condition -Condition ($null -ne $ready.status) -Message "Smoke failed: /api/health/ready missing status."
Assert-Condition -Condition ($ready.status -ne "Unhealthy") -Message "Smoke failed: ready health is Unhealthy."

$products = Invoke-SmokeGet -Url "$base/api/products?page=1&pageSize=1"
Assert-Condition -Condition ($null -ne $products.items) -Message "Smoke failed: products endpoint missing items."

$shops = Invoke-SmokeGet -Url "$base/api/shops?page=1&pageSize=1"
Assert-Condition -Condition ($null -ne $shops.items) -Message "Smoke failed: shops endpoint missing items."

$suggestions = Invoke-SmokeGet -Url "$base/api/search/suggestions?q=te"
Assert-Condition -Condition ($null -ne $suggestions) -Message "Smoke failed: search suggestions endpoint failed."

Write-Host "Release smoke succeeded for $base"
