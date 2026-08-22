param(
    [Parameter(Mandatory = $true)]
    [string]$DatasetPath,

    [string]$OutputRoot = "C:\ISEDATA\ISEEliteResearch\Validation\full-sample",

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repoRoot "tools\ISE.HistoricalResearch.FrozenTradeForensicsStudy\ISE.HistoricalResearch.FrozenTradeForensicsStudy.csproj"
$tests = Join-Path $repoRoot "tests\ISE.HistoricalResearch.Tests\ISE.HistoricalResearch.Tests.csproj"

if (-not (Test-Path -LiteralPath $DatasetPath)) { Fail "Dataset not found: $DatasetPath" }
if (-not (Test-Path -LiteralPath $project)) { Fail "Forensics project not found: $project" }

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

if (-not $SkipTests) {
    Write-Host "Running historical research test gate..."
    & dotnet test $tests --nologo
    if ($LASTEXITCODE -ne 0) { Fail "Historical research tests failed. Full-sample forensics aborted." }
}

$rows = Import-Csv -LiteralPath $DatasetPath -Delimiter "`t"
if (-not $rows -or $rows.Count -eq 0) { Fail "Dataset contains no rows." }
if (-not ($rows[0].PSObject.Properties.Name -contains 'tradingDay')) { Fail "Dataset missing tradingDay column." }

$sessions = $rows |
    ForEach-Object { [datetime]::ParseExact($_.tradingDay, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture).Date } |
    Where-Object { $_ -gt [datetime]'2026-08-10' } |
    Sort-Object -Unique

if (-not $sessions -or $sessions.Count -eq 0) { Fail "No evaluation sessions found after 2026-08-10." }

$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $OutputRoot $runStamp
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

$sessionSummaries = @()
$allLifecycle = @()
$allCandidates = @()

foreach ($session in $sessions) {
    $dateText = $session.ToString('yyyy-MM-dd')
    $sessionRoot = Join-Path $runRoot $dateText
    New-Item -ItemType Directory -Force -Path $sessionRoot | Out-Null

    Write-Host "Running forensics for $dateText ..."
    & dotnet run --project $project -- $DatasetPath $dateText $sessionRoot
    if ($LASTEXITCODE -ne 0) { Fail "Forensics failed for $dateText" }

    $stem = "frozen-forensics-$($session.ToString('yyyyMMdd'))"
    $summaryPath = Join-Path $sessionRoot "$stem-summary.json"
    $lifecyclePath = Join-Path $sessionRoot "$stem-lifecycle.tsv"
    $candidatePath = Join-Path $sessionRoot "$stem-candidates.tsv"

    if (-not (Test-Path $summaryPath)) { Fail "Missing summary for $dateText" }

    $summary = Get-Content -Raw -LiteralPath $summaryPath | ConvertFrom-Json
    $sessionSummaries += [pscustomobject]@{
        session = $dateText
        rawCandidates = [int]$summary.counts.rawCandidates
        baselineEligible = [int]$summary.counts.baselineEligible
        frozenEligible = [int]$summary.counts.frozenEligible
        selected = [int]$summary.counts.selected
        rejectedResetAge = [int]$summary.counts.rejectedResetAge
        rejectedPositionOpen = [int]$summary.counts.rejectedPositionOpen
        rejectedAttemptLimit = [int]$summary.counts.rejectedAttemptLimit
        totalPnl = [decimal]$summary.selected.totalPnl
        coreCount = [int]$summary.selected.coreCount
        runnerCount = [int]$summary.selected.runnerCount
        averageMfe = [decimal]$summary.selected.averageMfe
        averageCaptureRatio = [decimal]$summary.selected.averageCaptureRatio
    }

    if (Test-Path $lifecyclePath) {
        $life = Import-Csv -LiteralPath $lifecyclePath -Delimiter "`t"
        foreach ($r in $life) {
            $r | Add-Member -NotePropertyName session -NotePropertyValue $dateText -Force
            $allLifecycle += $r
        }
    }

    if (Test-Path $candidatePath) {
        $cand = Import-Csv -LiteralPath $candidatePath -Delimiter "`t"
        foreach ($r in $cand) {
            $r | Add-Member -NotePropertyName session -NotePropertyValue $dateText -Force
            $allCandidates += $r
        }
    }
}

$sessionCsv = Join-Path $runRoot 'full-sample-session-summary.csv'
$lifecycleCsv = Join-Path $runRoot 'full-sample-lifecycle.csv'
$candidateCsv = Join-Path $runRoot 'full-sample-candidates.csv'
$summaryJson = Join-Path $runRoot 'full-sample-summary.json'

$sessionSummaries | Export-Csv -NoTypeInformation -LiteralPath $sessionCsv
$allLifecycle | Export-Csv -NoTypeInformation -LiteralPath $lifecycleCsv
$allCandidates | Export-Csv -NoTypeInformation -LiteralPath $candidateCsv

$selectedCount = ($sessionSummaries | Measure-Object -Property selected -Sum).Sum
$totalPnl = ($sessionSummaries | Measure-Object -Property totalPnl -Sum).Sum
$coreCount = ($sessionSummaries | Measure-Object -Property coreCount -Sum).Sum
$runnerCount = ($sessionSummaries | Measure-Object -Property runnerCount -Sum).Sum
$resetRejects = ($sessionSummaries | Measure-Object -Property rejectedResetAge -Sum).Sum

$lifecycleNumeric = @($allLifecycle | ForEach-Object {
    [pscustomobject]@{
        realizedTicks = [decimal]$_.realizedTicks
        realizedDollars = [decimal]$_.realizedDollars
        mfeTicks = [decimal]$_.mfeTicks
        maeTicks = [decimal]$_.maeTicks
        captureRatio = [decimal]$_.captureRatio
        postExitFavorableTicks = [decimal]$_.postExitFavorableTicks
        additionalFavorableTicks = [decimal]$_.additionalFavorableTicks
        finalMode = $_.finalMode
        exitReason = $_.exitReason
    }
})

$aggregate = [ordered]@{
    schemaVersion = 1
    datasetPath = (Resolve-Path -LiteralPath $DatasetPath).Path
    runUtc = (Get-Date).ToUniversalTime().ToString('O')
    sessions = $sessions.Count
    firstSession = $sessions[0].ToString('yyyy-MM-dd')
    lastSession = $sessions[$sessions.Count - 1].ToString('yyyy-MM-dd')
    selectedTrades = [int]$selectedCount
    totalPnl = [decimal]$totalPnl
    averageDailyPnl = if ($sessions.Count -gt 0) { [decimal]$totalPnl / $sessions.Count } else { 0 }
    averageTradePnl = if ($selectedCount -gt 0) { [decimal]$totalPnl / $selectedCount } else { 0 }
    coreCount = [int]$coreCount
    runnerCount = [int]$runnerCount
    resetAgeRejects = [int]$resetRejects
    averageMfeTicks = if ($lifecycleNumeric.Count -gt 0) { ($lifecycleNumeric | Measure-Object -Property mfeTicks -Average).Average } else { 0 }
    averageCaptureRatio = if ($lifecycleNumeric.Count -gt 0) { ($lifecycleNumeric | Measure-Object -Property captureRatio -Average).Average } else { 0 }
    averagePostExitFavorableTicks = if ($lifecycleNumeric.Count -gt 0) { ($lifecycleNumeric | Measure-Object -Property postExitFavorableTicks -Average).Average } else { 0 }
    scalpCount = @($lifecycleNumeric | Where-Object { $_.finalMode -eq 'Scalp' }).Count
    coreFinalModeCount = @($lifecycleNumeric | Where-Object { $_.finalMode -eq 'Core' }).Count
    runnerFinalModeCount = @($lifecycleNumeric | Where-Object { $_.finalMode -eq 'Runner' }).Count
    exitReasonCounts = [ordered]@{}
}

foreach ($g in ($lifecycleNumeric | Group-Object exitReason | Sort-Object Name)) {
    $aggregate.exitReasonCounts[$g.Name] = $g.Count
}

$aggregate | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryJson -Encoding UTF8

Write-Host ""
Write-Host "ISE Elite V7.8.7 FULL-SAMPLE FORENSICS"
Write-Host "sessions=$($aggregate.sessions)"
Write-Host "selectedTrades=$($aggregate.selectedTrades)"
Write-Host "totalPnl=$([math]::Round([decimal]$aggregate.totalPnl,2))"
Write-Host "averageDailyPnl=$([math]::Round([decimal]$aggregate.averageDailyPnl,2))"
Write-Host "averageTradePnl=$([math]::Round([decimal]$aggregate.averageTradePnl,2))"
Write-Host "coreCount=$($aggregate.coreCount)"
Write-Host "runnerCount=$($aggregate.runnerCount)"
Write-Host "scalpCount=$($aggregate.scalpCount)"
Write-Host "resetAgeRejects=$($aggregate.resetAgeRejects)"
Write-Host "averageMfeTicks=$([math]::Round([decimal]$aggregate.averageMfeTicks,2))"
Write-Host "averageCaptureRatio=$([math]::Round([decimal]$aggregate.averageCaptureRatio,4))"
Write-Host "averagePostExitFavorableTicks=$([math]::Round([decimal]$aggregate.averagePostExitFavorableTicks,2))"
Write-Host "OUTPUT $runRoot"
