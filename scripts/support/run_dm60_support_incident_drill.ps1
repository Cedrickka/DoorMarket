param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,
    [string]$AdminBearerToken = "",
    [string]$ClientBearerToken = "",
    [switch]$InjectCheckoutFailureEvents,
    [int]$FailureEventCount = 24,
    [switch]$AcknowledgeAndReopenFirstIncident,
    [string]$OutputDir = "artifacts/support",
    [int]$TimeoutSec = 30,
    [switch]$Strict
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("GET", "POST", "PUT", "DELETE", "PATCH")]
        [string]$Method,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [string]$BearerToken = "",
        [object]$Body = $null
    )

    $headers = @{ Accept = "application/json" }
    if (-not [string]::IsNullOrWhiteSpace($BearerToken)) {
        $headers["Authorization"] = "Bearer $BearerToken"
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        if ($null -ne $Body) {
            $payload = $Body | ConvertTo-Json -Depth 12
            $data = Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -Body $payload -ContentType "application/json" -TimeoutSec $TimeoutSec
        }
        else {
            $data = Invoke-RestMethod -Method $Method -Uri $Url -Headers $headers -TimeoutSec $TimeoutSec
        }

        $stopwatch.Stop()
        return [PSCustomObject]@{
            method = $Method
            url = $Url
            ok = $true
            status = 200
            data = $data
            error = $null
            durationMs = [int]$stopwatch.ElapsedMilliseconds
        }
    }
    catch {
        $stopwatch.Stop()

        $statusCode = $null
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $statusCode = [int]$_.Exception.Response.StatusCode
        }

        $errorMessage = $_.Exception.Message
        if (-not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message)) {
            $errorMessage = $_.ErrorDetails.Message
        }

        return [PSCustomObject]@{
            method = $Method
            url = $Url
            ok = $false
            status = $statusCode
            data = $null
            error = $errorMessage
            durationMs = [int]$stopwatch.ElapsedMilliseconds
        }
    }
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -Path $Path -ItemType Directory -Force | Out-Null
    }
}

function Get-CollectionCount {
    param([object]$Value)

    if ($null -eq $Value) {
        return 0
    }

    if ($Value -is [string]) {
        if ([string]::IsNullOrWhiteSpace($Value)) {
            return 0
        }
        return 1
    }

    if ($Value -is [System.Collections.ICollection]) {
        return [int]$Value.Count
    }

    if ($Value.PSObject -and $Value.PSObject.Properties.Match("Count").Count -gt 0) {
        try {
            return [int]$Value.Count
        }
        catch {
            return @($Value).Count
        }
    }

    return @($Value).Count
}

function New-Check {
    param(
        [string]$Name,
        [string]$Expected,
        [string]$Actual,
        [ValidateSet("PASS", "FAIL", "WARN", "SKIPPED")]
        [string]$Status,
        [string]$Details = ""
    )

    return [PSCustomObject]@{
        name = $Name
        expected = $Expected
        actual = $Actual
        status = $Status
        details = $Details
    }
}

function Get-ScenarioStatus {
    param([object[]]$Checks)

    if ($null -eq $Checks -or $Checks.Count -eq 0) {
        return "SKIPPED"
    }

    if (($Checks | Where-Object { $_.status -eq "FAIL" }).Count -gt 0) {
        return "FAIL"
    }

    if (($Checks | Where-Object { $_.status -eq "PASS" }).Count -gt 0) {
        return "PASS"
    }

    if (($Checks | Where-Object { $_.status -eq "WARN" }).Count -gt 0) {
        return "WARN"
    }

    return "SKIPPED"
}

function Test-ContainsBlockingIssue {
    param(
        [object]$ReadinessData,
        [string[]]$AcceptedCodes
    )

    if ($null -eq $ReadinessData) {
        return $false
    }

    $blockingIssues = @()
    if ($ReadinessData.PSObject -and $ReadinessData.PSObject.Properties.Match("blockingIssues").Count -gt 0) {
        $blockingIssues = @($ReadinessData.blockingIssues)
    }

    foreach ($issue in $blockingIssues) {
        $code = ""
        if ($issue.PSObject -and $issue.PSObject.Properties.Match("code").Count -gt 0) {
            $code = [string]$issue.code
        }
        if ($AcceptedCodes -contains $code) {
            return $true
        }
    }

    return $false
}

function Drill-CheckoutObservability {
    param(
        [string]$RootUrl,
        [string]$AdminToken,
        [switch]$InjectFailures,
        [int]$EventsToInject
    )

    $checks = New-Object System.Collections.Generic.List[object]
    $artifacts = [ordered]@{}

    if (-not [string]::IsNullOrWhiteSpace($AdminToken)) {
        $dashboardBefore = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/dashboard?defaultHours=4" -BearerToken $AdminToken
        $sloBefore = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/slo?defaultHours=4" -BearerToken $AdminToken
        $incidentsBefore = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/incidents?includeResolved=false&take=20" -BearerToken $AdminToken

        $dashboardBeforeStatus = if ($dashboardBefore.ok) { "PASS" } else { "FAIL" }
        $sloBeforeStatus = if ($sloBefore.ok) { "PASS" } else { "FAIL" }
        $incidentsBeforeStatus = if ($incidentsBefore.ok) { "PASS" } else { "FAIL" }

        $checks.Add((New-Check -Name "OBS dashboard before" -Expected "HTTP 200" -Actual "HTTP $($dashboardBefore.status)" -Status $dashboardBeforeStatus -Details $dashboardBefore.error))
        $checks.Add((New-Check -Name "OBS slo before" -Expected "HTTP 200" -Actual "HTTP $($sloBefore.status)" -Status $sloBeforeStatus -Details $sloBefore.error))
        $checks.Add((New-Check -Name "OBS incidents before" -Expected "HTTP 200" -Actual "HTTP $($incidentsBefore.status)" -Status $incidentsBeforeStatus -Details $incidentsBefore.error))

        $artifacts.dashboardBefore = $dashboardBefore
        $artifacts.sloBefore = $sloBefore
        $artifacts.incidentsBefore = $incidentsBefore
    }
    else {
        $checks.Add((New-Check -Name "OBS admin token" -Expected "Admin token configured" -Actual "Missing" -Status "SKIPPED" -Details "AdminBearerToken not provided."))
    }

    $trackingAttempts = 0
    $trackingSuccess = 0
    $trackingErrors = New-Object System.Collections.Generic.List[object]

    if ($InjectFailures) {
        $session = "dm60-support-drill-" + [Guid]::NewGuid().ToString("N")
        for ($i = 0; $i -lt $EventsToInject; $i++) {
            $provider = if ($i % 2 -eq 0) { "AIRTEL" } else { "ORANGE" }

            $initPayload = @{
                eventName = "checkout_payment_initiated"
                sessionId = $session
                paymentProvider = $provider
                paymentChannel = "MobileMoney"
                success = $true
                source = "support_drill"
                countryTag = "CD"
                metadata = @{ drill = "DM60-SUP-01"; step = "initiate" }
            }
            $failPayload = @{
                eventName = "checkout_payment_failed"
                sessionId = $session
                paymentProvider = $provider
                paymentChannel = "MobileMoney"
                success = $false
                errorCode = "support_drill_declined"
                errorMessage = "Simulated failure for L1/L2 drill."
                source = "support_drill"
                countryTag = "CD"
                metadata = @{ drill = "DM60-SUP-01"; step = "fail" }
            }

            foreach ($payload in @($initPayload, $failPayload)) {
                $trackingAttempts++
                $trackResult = Invoke-Api -Method POST -Url "$RootUrl/api/checkout/analytics/track" -Body $payload
                if ($trackResult.ok) {
                    $trackingSuccess++
                }
                else {
                    $trackingErrors.Add([PSCustomObject]@{
                        eventName = $payload.eventName
                        provider = $payload.paymentProvider
                        status = $trackResult.status
                        error = $trackResult.error
                    })
                }
            }
        }

        $checkStatus = if ($trackingSuccess -eq $trackingAttempts) { "PASS" } elseif ($trackingSuccess -gt 0) { "WARN" } else { "FAIL" }
        $checks.Add((New-Check -Name "OBS injection analytics" -Expected "$trackingAttempts events accepted" -Actual "$trackingSuccess/$trackingAttempts accepted" -Status $checkStatus -Details "Errors: $($trackingErrors.Count)"))

        $artifacts.injection = [ordered]@{
            attempts = $trackingAttempts
            success = $trackingSuccess
            errors = $trackingErrors
        }
    }
    else {
        $checks.Add((New-Check -Name "OBS injection analytics" -Expected "Injection enabled" -Actual "Disabled" -Status "SKIPPED" -Details "Use -InjectCheckoutFailureEvents to simulate payment failures."))
    }

    if (-not [string]::IsNullOrWhiteSpace($AdminToken)) {
        $dashboardAfter = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/dashboard?defaultHours=4" -BearerToken $AdminToken
        $sloAfter = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/slo?defaultHours=4" -BearerToken $AdminToken
        $incidentsAfter = Invoke-Api -Method GET -Url "$RootUrl/api/admin/checkout-observability/incidents?includeResolved=false&take=20" -BearerToken $AdminToken

        $dashboardAfterStatus = if ($dashboardAfter.ok) { "PASS" } else { "FAIL" }
        $sloAfterStatus = if ($sloAfter.ok) { "PASS" } else { "FAIL" }
        $incidentsAfterStatus = if ($incidentsAfter.ok) { "PASS" } else { "FAIL" }

        $checks.Add((New-Check -Name "OBS dashboard after" -Expected "HTTP 200" -Actual "HTTP $($dashboardAfter.status)" -Status $dashboardAfterStatus -Details $dashboardAfter.error))
        $checks.Add((New-Check -Name "OBS slo after" -Expected "HTTP 200" -Actual "HTTP $($sloAfter.status)" -Status $sloAfterStatus -Details $sloAfter.error))
        $checks.Add((New-Check -Name "OBS incidents after" -Expected "HTTP 200" -Actual "HTTP $($incidentsAfter.status)" -Status $incidentsAfterStatus -Details $incidentsAfter.error))

        if ($InjectFailures -and $incidentsAfter.ok) {
            $beforeCount = if ($artifacts.incidentsBefore -and $artifacts.incidentsBefore.ok) { Get-CollectionCount -Value $artifacts.incidentsBefore.data } else { 0 }
            $afterCount = Get-CollectionCount -Value $incidentsAfter.data
            $incidentTrendStatus = if ($afterCount -ge $beforeCount) { "PASS" } else { "WARN" }
            $checks.Add((New-Check -Name "OBS incident trend" -Expected "Open incidents not decreasing after synthetic failures" -Actual "Before=$beforeCount, After=$afterCount" -Status $incidentTrendStatus -Details "Trend check for drill consistency."))
        }

        $artifacts.dashboardAfter = $dashboardAfter
        $artifacts.sloAfter = $sloAfter
        $artifacts.incidentsAfter = $incidentsAfter
    }

    return [ordered]@{
        status = Get-ScenarioStatus -Checks $checks
        checks = $checks
        evidence = $artifacts
    }
}

function Drill-OrderAndAddress {
    param(
        [string]$RootUrl,
        [string]$ClientToken
    )

    $checks = New-Object System.Collections.Generic.List[object]
    $artifacts = [ordered]@{}

    if ([string]::IsNullOrWhiteSpace($ClientToken)) {
        $checks.Add((New-Check -Name "Client token" -Expected "Client token configured" -Actual "Missing" -Status "SKIPPED" -Details "ClientBearerToken not provided."))
        return [ordered]@{
            status = Get-ScenarioStatus -Checks $checks
            checks = $checks
            evidence = $artifacts
        }
    }

    $preCheckoutBody = @{
        deliveryZoneId = $null
        promoCode = $null
        paymentProvider = "PayPal"
        prepaidCardCode = $null
        requireDeliveryZone = $true
    }

    $addressBody = @{
        label = "DM60 Drill"
        fullName = "Support Drill"
        phone = "+243900000001"
        country = "CD"
        city = "Kinshasa"
        district = "Gombe"
        deliveryZoneId = "00000000-0000-0000-0000-000000000001"
        street = "Avenue Test 1"
        landmark = "Reference drill"
        isDefault = $false
    }

    $orderProbe = Invoke-Api -Method GET -Url "$RootUrl/api/orders/11111111-1111-1111-1111-111111111111" -BearerToken $ClientToken
    $preCheckout = Invoke-Api -Method POST -Url "$RootUrl/api/cart/pre-checkout" -BearerToken $ClientToken -Body $preCheckoutBody
    $addressProbe = Invoke-Api -Method POST -Url "$RootUrl/api/me/addresses" -BearerToken $ClientToken -Body $addressBody

    $orderStatus = if ($orderProbe.status -in @(400, 404)) { "PASS" } elseif ($orderProbe.status -eq 401) { "FAIL" } else { "WARN" }
    $checks.Add((New-Check -Name "Order invalid-id probe" -Expected "Controlled 400/404" -Actual "HTTP $($orderProbe.status)" -Status $orderStatus -Details $orderProbe.error))

    if ($preCheckout.ok) {
        $hasExpectedIssue = Test-ContainsBlockingIssue -ReadinessData $preCheckout.data -AcceptedCodes @("delivery_zone_required", "empty_cart")
        $actualIssue = if ($hasExpectedIssue) { "Expected blocking issue returned" } else { "Blocking issue missing" }
        $preCheckoutStatus = if ($hasExpectedIssue) { "PASS" } else { "WARN" }
        $checks.Add((New-Check -Name "Pre-checkout guardrail" -Expected "delivery_zone_required or empty_cart" -Actual $actualIssue -Status $preCheckoutStatus -Details "HTTP $($preCheckout.status)"))
    }
    else {
        $checks.Add((New-Check -Name "Pre-checkout guardrail" -Expected "HTTP 200 with readiness payload" -Actual "HTTP $($preCheckout.status)" -Status "FAIL" -Details $preCheckout.error))
    }

    $addressStatus = if ($addressProbe.status -eq 400) { "PASS" } elseif ($addressProbe.status -eq 401) { "FAIL" } else { "WARN" }
    $checks.Add((New-Check -Name "Address invalid-zone" -Expected "HTTP 400 Zone de livraison invalide" -Actual "HTTP $($addressProbe.status)" -Status $addressStatus -Details $addressProbe.error))

    $artifacts.orderProbe = $orderProbe
    $artifacts.preCheckoutProbe = $preCheckout
    $artifacts.addressProbe = $addressProbe

    return [ordered]@{
        status = Get-ScenarioStatus -Checks $checks
        checks = $checks
        evidence = $artifacts
    }
}

function Drill-Notifications {
    param(
        [string]$RootUrl,
        [string]$AdminToken,
        [switch]$AckReopen
    )

    $checks = New-Object System.Collections.Generic.List[object]
    $artifacts = [ordered]@{}

    if ([string]::IsNullOrWhiteSpace($AdminToken)) {
        $checks.Add((New-Check -Name "Admin token" -Expected "Admin token configured" -Actual "Missing" -Status "SKIPPED" -Details "AdminBearerToken not provided."))
        return [ordered]@{
            status = Get-ScenarioStatus -Checks $checks
            checks = $checks
            evidence = $artifacts
        }
    }

    $summary = Invoke-Api -Method GET -Url "$RootUrl/api/admin/notifications/transactions/summary" -BearerToken $AdminToken
    $incidents = Invoke-Api -Method GET -Url "$RootUrl/api/admin/notifications/transactions/incidents?take=20&includeAcknowledged=true" -BearerToken $AdminToken

    $summaryStatus = if ($summary.ok) { "PASS" } else { "FAIL" }
    $incidentsStatus = if ($incidents.ok) { "PASS" } else { "FAIL" }
    $checks.Add((New-Check -Name "Notif summary" -Expected "HTTP 200" -Actual "HTTP $($summary.status)" -Status $summaryStatus -Details $summary.error))
    $checks.Add((New-Check -Name "Notif incidents" -Expected "HTTP 200" -Actual "HTTP $($incidents.status)" -Status $incidentsStatus -Details $incidents.error))

    $ack = $null
    $reopen = $null
    $incidentsCount = if ($incidents.ok) { Get-CollectionCount -Value $incidents.data } else { 0 }

    if ($AckReopen) {
        if ($incidents.ok -and $incidentsCount -gt 0) {
            $target = $incidents.data[0]
            $ackBody = @{ note = "DM60-SUP-01 L1 acknowledge test" }
            $ack = Invoke-Api -Method POST -Url "$RootUrl/api/admin/notifications/transactions/incidents/$($target.lastLogId)/acknowledge" -BearerToken $AdminToken -Body $ackBody
            $reopen = Invoke-Api -Method POST -Url "$RootUrl/api/admin/notifications/transactions/incidents/$($target.lastLogId)/reopen" -BearerToken $AdminToken

            $ackStatus = if ($ack.ok) { "PASS" } else { "FAIL" }
            $reopenStatus = if ($reopen.ok) { "PASS" } else { "FAIL" }
            $checks.Add((New-Check -Name "Notif acknowledge" -Expected "HTTP 200" -Actual "HTTP $($ack.status)" -Status $ackStatus -Details $ack.error))
            $checks.Add((New-Check -Name "Notif reopen" -Expected "HTTP 200" -Actual "HTTP $($reopen.status)" -Status $reopenStatus -Details $reopen.error))
        }
        else {
            $checks.Add((New-Check -Name "Notif acknowledge/reopen" -Expected "At least 1 incident available" -Actual "No incident to acknowledge" -Status "WARN" -Details "Cannot run ack/reopen without existing incident."))
        }
    }
    else {
        $checks.Add((New-Check -Name "Notif acknowledge/reopen" -Expected "Ack/reopen enabled" -Actual "Disabled" -Status "SKIPPED" -Details "Use -AcknowledgeAndReopenFirstIncident to run L1 to L2 handoff simulation."))
    }

    $artifacts.summary = $summary
    $artifacts.incidents = $incidents
    $artifacts.acknowledge = $ack
    $artifacts.reopen = $reopen

    return [ordered]@{
        status = Get-ScenarioStatus -Checks $checks
        checks = $checks
        evidence = $artifacts
    }
}

function Build-GlobalSummary {
    param([System.Collections.IDictionary]$Scenarios)

    $allStatuses = @($Scenarios.Values | ForEach-Object { $_.status })
    $summary = [ordered]@{
        pass = @($allStatuses | Where-Object { $_ -eq "PASS" }).Count
        fail = @($allStatuses | Where-Object { $_ -eq "FAIL" }).Count
        warn = @($allStatuses | Where-Object { $_ -eq "WARN" }).Count
        skipped = @($allStatuses | Where-Object { $_ -eq "SKIPPED" }).Count
    }

    $overall = if ($summary.fail -gt 0) {
        "FAIL"
    }
    elseif ($summary.warn -gt 0) {
        "WARN"
    }
    elseif ($summary.pass -gt 0) {
        "PASS"
    }
    else {
        "SKIPPED"
    }

    return [ordered]@{
        overall = $overall
        counters = $summary
    }
}

function Get-ObjectValue {
    param(
        [object]$Object,
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    if ($Object -is [System.Collections.IDictionary]) {
        if ($Object.Contains($Name)) {
            return $Object[$Name]
        }

        return $null
    }

    if ($Object.PSObject -and $Object.PSObject.Properties.Match($Name).Count -gt 0) {
        return $Object.$Name
    }

    return $null
}

function Write-MarkdownReport {
    param(
        [System.Collections.IDictionary]$Report,
        [string]$Path
    )

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("# DM60-SUP-01 Incident Drill Report")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("- GeneratedAtUtc: $($Report.generatedAtUtc)")
    [void]$sb.AppendLine("- BaseUrl: $($Report.baseUrl)")
    [void]$sb.AppendLine("- OverallStatus: **$($Report.overallStatus)**")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## Scenario Status")
    [void]$sb.AppendLine("| Scenario | Status | PASS | FAIL | WARN | SKIPPED |")
    [void]$sb.AppendLine("|---|---|---:|---:|---:|---:|")

    foreach ($entry in $Report["scenarios"].GetEnumerator()) {
        $checks = @(Get-ObjectValue -Object $entry.Value -Name "checks")
        $scenarioStatus = Get-ObjectValue -Object $entry.Value -Name "status"
        $passCount = @($checks | Where-Object { $_.status -eq "PASS" }).Count
        $failCount = @($checks | Where-Object { $_.status -eq "FAIL" }).Count
        $warnCount = @($checks | Where-Object { $_.status -eq "WARN" }).Count
        $skipCount = @($checks | Where-Object { $_.status -eq "SKIPPED" }).Count
        [void]$sb.AppendLine("| $($entry.Key) | $scenarioStatus | $passCount | $failCount | $warnCount | $skipCount |")
    }

    foreach ($entry in $Report["scenarios"].GetEnumerator()) {
        $scenarioChecks = @(Get-ObjectValue -Object $entry.Value -Name "checks")
        $scenarioStatus = Get-ObjectValue -Object $entry.Value -Name "status"
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("## $($entry.Key)")
        [void]$sb.AppendLine("Status: **$scenarioStatus**")
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("| Check | Expected | Actual | Status | Details |")
        [void]$sb.AppendLine("|---|---|---|---|---|")

        foreach ($check in $scenarioChecks) {
            $details = if ([string]::IsNullOrWhiteSpace($check.details)) { "-" } else { $check.details.Replace("|", "\\|") }
            [void]$sb.AppendLine("| $($check.name) | $($check.expected) | $($check.actual) | $($check.status) | $details |")
        }
    }

    Set-Content -Path $Path -Value $sb.ToString()
}

$root = $BaseUrl.TrimEnd("/")
Ensure-Directory -Path $OutputDir

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$jsonReportPath = Join-Path $OutputDir "dm60_sup01_incident_drill_$timestamp.json"
$mdReportPath = Join-Path $OutputDir "dm60_sup01_incident_drill_$timestamp.md"

$scenarios = [ordered]@{
    payment_and_checkout_observability = Drill-CheckoutObservability -RootUrl $root -AdminToken $AdminBearerToken -InjectFailures:$InjectCheckoutFailureEvents -EventsToInject $FailureEventCount
    order_and_address = Drill-OrderAndAddress -RootUrl $root -ClientToken $ClientBearerToken
    notifications = Drill-Notifications -RootUrl $root -AdminToken $AdminBearerToken -AckReopen:$AcknowledgeAndReopenFirstIncident
}

$globalSummary = Build-GlobalSummary -Scenarios $scenarios

$report = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString("o")
    baseUrl = $root
    parameters = [ordered]@{
        injectCheckoutFailureEvents = [bool]$InjectCheckoutFailureEvents
        failureEventCount = $FailureEventCount
        acknowledgeAndReopenFirstIncident = [bool]$AcknowledgeAndReopenFirstIncident
        hasAdminToken = -not [string]::IsNullOrWhiteSpace($AdminBearerToken)
        hasClientToken = -not [string]::IsNullOrWhiteSpace($ClientBearerToken)
        timeoutSec = $TimeoutSec
    }
    overallStatus = $globalSummary.overall
    summary = $globalSummary.counters
    scenarios = $scenarios
}

$report | ConvertTo-Json -Depth 20 | Set-Content -Path $jsonReportPath
Write-MarkdownReport -Report $report -Path $mdReportPath

Write-Host "DM60-SUP-01 drill report generated:" 
Write-Host "- JSON: $jsonReportPath"
Write-Host "- MD:   $mdReportPath"
Write-Host "- OverallStatus: $($report.overallStatus)"

if ($Strict -and $report.overallStatus -eq "FAIL") {
    throw "DM60-SUP-01 drill failed in strict mode."
}
