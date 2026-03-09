param(
    [Parameter(Mandatory = $true)]
    [string]$BaselinePath,
    [Parameter(Mandatory = $true)]
    [string]$AfterPath,
    [string]$OutMarkdownPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Rounded([double]$value) {
    return [Math]::Round($value, 2)
}

function Get-PhaseRows($baseline, $after) {
    $phaseNames = $baseline.phases.PSObject.Properties.Name
    $rows = @()
    foreach ($phase in $phaseNames) {
        $b = $baseline.phases.$phase
        $a = $after.phases.$phase
        if ($null -eq $b -or $null -eq $a) {
            continue
        }

        $rows += [PSCustomObject]@{
            Phase          = $phase
            TotalP95Before = Get-Rounded $b.totalMs.p95
            TotalP95After  = Get-Rounded $a.totalMs.p95
            DeltaTotalP95  = Get-Rounded ($a.totalMs.p95 - $b.totalMs.p95)
            Slow16Before   = Get-Rounded $b.slowFramesOver16msPct
            Slow16After    = Get-Rounded $a.slowFramesOver16msPct
            DeltaSlow16    = Get-Rounded ($a.slowFramesOver16msPct - $b.slowFramesOver16msPct)
            CpuP95Before   = Get-Rounded $b.buildMs.p95
            CpuP95After    = Get-Rounded $a.buildMs.p95
            DeltaCpuP95    = Get-Rounded ($a.buildMs.p95 - $b.buildMs.p95)
            GpuP95Before   = Get-Rounded $b.rasterMs.p95
            GpuP95After    = Get-Rounded $a.rasterMs.p95
            DeltaGpuP95    = Get-Rounded ($a.rasterMs.p95 - $b.rasterMs.p95)
            FramesBefore   = [int]$b.frames
            FramesAfter    = [int]$a.frames
        }
    }

    return $rows
}

if (-not (Test-Path -LiteralPath $BaselinePath)) {
    throw "Baseline file not found: $BaselinePath"
}
if (-not (Test-Path -LiteralPath $AfterPath)) {
    throw "After file not found: $AfterPath"
}

$baseline = Get-Content -LiteralPath $BaselinePath -Raw | ConvertFrom-Json
$after = Get-Content -LiteralPath $AfterPath -Raw | ConvertFrom-Json
$rows = Get-PhaseRows -baseline $baseline -after $after

if ($rows.Count -eq 0) {
    throw "No comparable phases found between the two files."
}

$rows |
    Sort-Object Phase |
    Format-Table Phase, TotalP95Before, TotalP95After, DeltaTotalP95, Slow16Before, Slow16After, DeltaSlow16, CpuP95Before, CpuP95After, DeltaCpuP95, GpuP95Before, GpuP95After, DeltaGpuP95, FramesBefore, FramesAfter -AutoSize

if ($OutMarkdownPath) {
    $lines = @()
    $lines += "| Phase | p95 total before | p95 total after | delta | slow >16ms before | slow >16ms after | delta | p95 CPU before | p95 CPU after | delta | p95 GPU before | p95 GPU after | delta |"
    $lines += "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"

    foreach ($row in ($rows | Sort-Object Phase)) {
        $lines += "| $($row.Phase) | $($row.TotalP95Before) | $($row.TotalP95After) | $($row.DeltaTotalP95) | $($row.Slow16Before)% | $($row.Slow16After)% | $($row.DeltaSlow16) | $($row.CpuP95Before) | $($row.CpuP95After) | $($row.DeltaCpuP95) | $($row.GpuP95Before) | $($row.GpuP95After) | $($row.DeltaGpuP95) |"
    }

    Set-Content -LiteralPath $OutMarkdownPath -Value ($lines -join [Environment]::NewLine) -Encoding UTF8
    Write-Host "Markdown report written to $OutMarkdownPath"
}
