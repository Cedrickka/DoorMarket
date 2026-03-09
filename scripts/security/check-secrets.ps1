param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path $Root).Path
Write-Host "Running secrets scan in: $resolvedRoot"

$includedExtensions = @(
    ".cs",
    ".json",
    ".config",
    ".env",
    ".yaml",
    ".yml",
    ".ps1",
    ".md"
)

$secretRegexes = @(
    '(?i)(password|pwd)\s*[:=]\s*["''][^"'']{6,}["'']',
    '(?i)(secret|clientsecret|privatekey|apikey|api[_-]?key)\s*[:=]\s*["''][^"'']{8,}["'']',
    '(?i)connection\s*string\s*[:=]\s*["''][^"'']*password\s*=\s*[^;"'']+',
    '(?i)DefaultConnection\s*"\s*:\s*"[^"]*Password='
)

$allowedValueRegex = '(?i)(CHANGE_ME|YOUR_|EXAMPLE|DUMMY|TEST_ONLY|LOCALDB|localhost|MET_UNE_CLE|UNE_CLE_SECRETE|DO_NOT_USE)'

$files = Get-ChildItem -Path $resolvedRoot -Recurse -File | Where-Object {
    $ext = $_.Extension.ToLowerInvariant()
    if (-not $includedExtensions.Contains($ext)) { return $false }
    $relative = $_.FullName.Substring($resolvedRoot.Length)
    if ($relative -match '(?i)\\(\.git|\.vs|bin|obj|build|artifacts|node_modules|\.dart_tool)\\') { return $false }

    return $true
}

$findings = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        foreach ($pattern in $secretRegexes) {
            if ($line -match $pattern) {
                if ($line -match $allowedValueRegex) {
                    continue
                }

                $findings.Add([PSCustomObject]@{
                        File = $file.FullName.Substring($resolvedRoot.Length).TrimStart('\')
                        Line = $i + 1
                        Value = $line.Trim()
                    })
                break
            }
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host ""
    Write-Host "Potential secrets detected:" -ForegroundColor Red
    $findings | ForEach-Object {
        Write-Host " - $($_.File):$($_.Line) => $($_.Value)" -ForegroundColor Red
    }
    Write-Error "Secrets scan failed. Replace hardcoded values with env vars/user-secrets."
    exit 1
}

Write-Host "Secrets scan passed." -ForegroundColor Green
exit 0
