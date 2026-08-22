[CmdletBinding()]
param(
    [string]$DatasetPath = "C:\Users\devon\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv",
    [int]$TimeoutMinutes = 45,
    [int]$PollSeconds = 10,
    [datetime]$NowCentral = (Get-Date),
    [switch]$SkipProcessCheck
)

$ErrorActionPreference = 'Stop'
$WaitExitCode = 10
$WarnExitCode = 11

function Complete([string]$Status, [string]$Message, [int]$Code) {
    Write-Host "DATASET REFRESH $Status - $Message"
    exit $Code
}

function Get-LatestCompletedWeekday([datetime]$Now) {
    $date = $Now.Date
    if ($Now.TimeOfDay -lt [timespan]'11:15:00') { $date = $date.AddDays(-1) }
    while ($date.DayOfWeek -eq [DayOfWeek]::Saturday -or $date.DayOfWeek -eq [DayOfWeek]::Sunday) { $date = $date.AddDays(-1) }
    return $date
}

try {
    if (-not $SkipProcessCheck) {
        $nt = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'NinjaTrader*' })
        if ($nt.Count -eq 0) { Complete 'WAIT' 'NinjaTrader is not running in the user desktop session.' $WaitExitCode }
        if (@($nt | Where-Object { $_.SessionId -eq 0 }).Count -eq $nt.Count) { Complete 'WAIT' 'NinjaTrader has no interactive user session.' $WaitExitCode }
    }

    $directory = Split-Path -Parent $DatasetPath
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $requestPath = Join-Path $directory 'mnq-refresh.request.tsv'
    $statusPath = Join-Path $directory 'mnq-refresh.status.json'
    $manifestPath = $DatasetPath + '.ready.json'
    $through = Get-LatestCompletedWeekday $NowCentral
    $requestId = [guid]::NewGuid().ToString('N')
    $requestTemp = $requestPath + '.' + $requestId + '.tmp'
    [IO.File]::WriteAllLines($requestTemp, @('requestId' + "`t" + 'throughCentral', $requestId + "`t" + $through.ToString('yyyy-MM-dd')), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $requestTemp -Destination $requestPath -Force
    Write-Host "DATASET REFRESH REQUEST requestId=$requestId through=$($through.ToString('yyyy-MM-dd'))"

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    $status = $null
    do {
        if (Test-Path -LiteralPath $statusPath) {
            try { $status = Get-Content -Raw -LiteralPath $statusPath | ConvertFrom-Json } catch { $status = $null }
            if ($null -ne $status -and $status.requestId -eq $requestId) {
                if ($status.status -eq 'FAIL') { Complete 'FAIL' $status.message 1 }
                if ($status.status -eq 'WARN') { Complete 'WARN' $status.message $WarnExitCode }
                if ($status.status -eq 'PASS') { break }
            }
        }
        Start-Sleep -Seconds $PollSeconds
    } while ((Get-Date) -lt $deadline)

    if ($null -eq $status -or $status.requestId -ne $requestId -or $status.status -ne 'PASS') {
        Complete 'WAIT' 'The NinjaTrader probe did not complete before timeout; verify it is loaded and the data connection/session is available.' $WaitExitCode
    }
    if (-not (Test-Path -LiteralPath $DatasetPath -PathType Leaf)) { Complete 'FAIL' 'Probe reported PASS but the TSV is missing.' 1 }
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Complete 'FAIL' 'Probe reported PASS but the ready manifest is missing.' 1 }

    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ($manifest.status -ne 'PASS' -or $manifest.requestId -ne $requestId) { Complete 'FAIL' 'Ready manifest does not match the current request.' 1 }
    if ($manifest.lastSession -ne $through.ToString('yyyy-MM-dd')) { Complete 'WARN' "Manifest last session $($manifest.lastSession) does not match required $($through.ToString('yyyy-MM-dd'))." $WarnExitCode }
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $DatasetPath).Hash
    if ($actualHash -ne $manifest.sha256) { Complete 'FAIL' 'TSV SHA256 does not match the atomic ready manifest.' 1 }

    $rows = @(Import-Csv -Delimiter "`t" -LiteralPath $DatasetPath)
    $sessions = @($rows | Group-Object tradingDay | Sort-Object Name)
    $partial = @($sessions | Where-Object Count -ne 480)
    if ($partial.Count -gt 0) { Complete 'FAIL' "TSV contains $($partial.Count) session(s) without exactly 480 bars." 1 }
    if ($rows.Count -ne [int]$manifest.barCount -or $sessions.Count -ne [int]$manifest.sessionCount) { Complete 'FAIL' 'TSV counts do not match the ready manifest.' 1 }
    if ($sessions[-1].Name -ne $through.ToString('yyyy-MM-dd')) { Complete 'WARN' 'TSV does not reach the latest completed weekday.' $WarnExitCode }
    Complete 'PASS' "bars=$($rows.Count) sessions=$($sessions.Count) last=$($sessions[-1].Name) sha256=$actualHash" 0
}
catch { Complete 'FAIL' $_.Exception.Message 1 }
