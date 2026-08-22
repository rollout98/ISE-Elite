[CmdletBinding()]
param(
    [string]$DatasetPath = "C:\Users\devon\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv",
    [string]$ValidationRoot = "C:\ISEDATA\ISEEliteResearch\Validation\unattended",
    [string]$ExpectedBranch = "automation/unattended-frozen-validation",
    [string]$DotNetPath = "C:\Program Files\dotnet\dotnet.exe",
    [int]$ExpectedSessionBars = 480,
    [int]$MaximumFileAgeHours = 96,
    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$runStartedUtc = (Get-Date).ToUniversalTime()
$runStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$dateRoot = Join-Path $ValidationRoot (Get-Date -Format 'yyyy-MM-dd')
$runRoot = Join-Path $dateRoot "run-$runStamp"
$logRoot = Join-Path $runRoot 'logs'
$steps = [Collections.Generic.List[object]]::new()
$warnings = [Collections.Generic.List[string]]::new()
$failure = $null
$mutex = $null
$hasMutex = $false

function Add-Step([string]$Name, [string]$Status, [string]$Detail, [string]$LogPath = '') {
    $script:steps.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail; logPath = $LogPath })
}

function Invoke-NativeStep([string]$Name, [string]$FilePath, [string[]]$Arguments, [string]$LogName, [int[]]$AllowedExitCodes = @(0)) {
    $logPath = Join-Path $script:logRoot $LogName
    & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $logPath | ForEach-Object { Write-Host $_ }
    $code = $LASTEXITCODE
    if ($AllowedExitCodes -notcontains $code) {
        Add-Step $Name 'FAIL' "Exit code $code" $logPath
        throw "$Name failed with exit code $code. See $logPath"
    }
    Add-Step $Name 'PASS' "Exit code $code" $logPath
    return [pscustomobject]@{ ExitCode = $code; LogPath = $logPath; Text = (Get-Content -Raw -LiteralPath $logPath) }
}

function Get-ExpectedLatestSessionDate([datetime]$NowCentral) {
    $date = $NowCentral.Date
    if ($NowCentral.TimeOfDay -lt [timespan]'11:15:00') { $date = $date.AddDays(-1) }
    while ($date.DayOfWeek -eq [DayOfWeek]::Saturday -or $date.DayOfWeek -eq [DayOfWeek]::Sunday) { $date = $date.AddDays(-1) }
    return $date
}

try {
    $mutex = [Threading.Mutex]::new($false, 'ISEEliteResearch_UnattendedFrozenResearch')
    $hasMutex = $mutex.WaitOne(0)
    if (-not $hasMutex) { throw 'Another unattended frozen-research run is already active.' }

    New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
    if (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) { throw "64-bit dotnet host not found: $DotNetPath" }
    if (-not (Test-Path -LiteralPath $DatasetPath -PathType Leaf)) { throw "Dataset not found: $DatasetPath" }

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    Push-Location $repoRoot
    try {
        $branch = (& git branch --show-current).Trim()
        if ($LASTEXITCODE -ne 0 -or $branch -ne $ExpectedBranch) { throw "Expected branch '$ExpectedBranch'; actual '$branch'." }
        $sha = (& git rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve repository SHA.' }
        $porcelain = @(& git status --porcelain)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect repository status.' }
        if ($porcelain.Count -gt 0) {
            if (-not $AllowDirtyWorktree) { throw 'Repository worktree is dirty. Unattended validation requires a clean worktree.' }
            $warnings.Add('Development override: worktree is dirty.')
            Add-Step 'RepositoryState' 'WARN' "branch=$branch sha=$sha dirty=true"
        } else { Add-Step 'RepositoryState' 'PASS' "branch=$branch sha=$sha dirty=false" }

        $datasetWarningBaseline = $warnings.Count
        $datasetItem = Get-Item -LiteralPath $DatasetPath
        $datasetHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $DatasetPath).Hash
        $rows = @(Import-Csv -LiteralPath $DatasetPath -Delimiter "`t")
        if ($rows.Count -eq 0) { throw 'Dataset contains no records.' }
        $required = @('instrument','contract','timestampUtc','tradingDay','intervalSeconds','open','high','low','close','volume')
        foreach ($column in $required) {
            if ($rows[0].PSObject.Properties.Name -notcontains $column) { throw "Dataset missing required column: $column" }
        }
        if (@($rows | Where-Object { $_.instrument -notlike 'MNQ*' }).Count -gt 0) { throw 'Dataset contains a non-MNQ instrument.' }
        if (@($rows | Where-Object { [int]$_.intervalSeconds -ne 60 }).Count -gt 0) { throw 'Dataset contains a non-60-second record.' }
        $sessionGroups = @($rows | Group-Object tradingDay | Sort-Object Name)
        $latestGroup = $sessionGroups[-1]
        $latestSession = [datetime]::ParseExact($latestGroup.Name, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
        $expectedLatest = Get-ExpectedLatestSessionDate (Get-Date)
        if ($latestGroup.Count -ne $ExpectedSessionBars) { $warnings.Add("Latest dataset session $($latestGroup.Name) has $($latestGroup.Count) bars; expected $ExpectedSessionBars.") }
        $partialSessions = @($sessionGroups | Where-Object { $_.Count -ne $ExpectedSessionBars })
        if ($partialSessions.Count -gt 0) { $warnings.Add("Dataset contains $($partialSessions.Count) incomplete session(s).") }
        if ($latestSession.Date -lt $expectedLatest.Date) { $warnings.Add("Dataset latest session $($latestSession.ToString('yyyy-MM-dd')) is older than expected $($expectedLatest.ToString('yyyy-MM-dd')).") }
        $fileAgeHours = ((Get-Date).ToUniversalTime() - $datasetItem.LastWriteTimeUtc).TotalHours
        if ($fileAgeHours -gt $MaximumFileAgeHours) { $warnings.Add("Dataset file age is $([math]::Round($fileAgeHours,1)) hours, exceeding $MaximumFileAgeHours hours.") }

        $validatorProject = Join-Path $repoRoot 'tools\ISE.HistoricalResearch.DatasetValidator\ISE.HistoricalResearch.DatasetValidator.csproj'
        $validator = Invoke-NativeStep 'DatasetValidator' $DotNetPath @('run','--project',$validatorProject,'--',$DatasetPath,'03:00','11:00') '02-dataset-validator.log' @(0,1)
        if ($validator.ExitCode -eq 1) { $warnings.Add('Dataset validator reported partial coverage; downstream research ran with WARN status.') }
        $datasetStatus = if ($warnings.Count -gt $datasetWarningBaseline) { 'WARN' } else { 'PASS' }
        Add-Step 'DatasetFreshness' $datasetStatus "records=$($rows.Count) sessions=$($sessionGroups.Count) latest=$($latestSession.ToString('yyyy-MM-dd')) latestBars=$($latestGroup.Count)"

        $testsProject = Join-Path $repoRoot 'tests\ISE.HistoricalResearch.Tests\ISE.HistoricalResearch.Tests.csproj'
        $test = Invoke-NativeStep 'HistoricalResearchTests' $DotNetPath @('test',$testsProject,'--logger','console;verbosity=minimal') '03-tests.log'
        if ($test.Text -notmatch 'Passed:\s+209' -or $test.Text -notmatch 'Total:\s+209') { throw 'Test process succeeded but did not report exactly 209 passed of 209 total.' }

        $phase0 = Join-Path $runRoot 'continuous-frozen-validation'; New-Item -ItemType Directory -Force -Path $phase0 | Out-Null
        Invoke-NativeStep 'ContinuousFrozenForwardValidation' $DotNetPath @('run','--project',(Join-Path $repoRoot 'tools\ISE.HistoricalResearch.ContinuousFrozenForwardValidationStudy\ISE.HistoricalResearch.ContinuousFrozenForwardValidationStudy.csproj'),'--',$DatasetPath) '04-continuous-validation.log' | Out-Null

        $phase1 = Join-Path $runRoot 'phase-1-forensics'
        $env:PATH = (Split-Path -Parent $DotNetPath) + [IO.Path]::PathSeparator + $env:PATH
        Invoke-NativeStep 'Phase1Forensics' 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $repoRoot 'tools\automation\Invoke-FrozenForensicsFullSample.ps1'),'-DatasetPath',$DatasetPath,'-OutputRoot',$phase1,'-SkipTests') '05-phase-1.log' | Out-Null

        $phase2 = Join-Path $runRoot 'phase-2-shadow'
        Invoke-NativeStep 'Phase2ShadowReconstruction' $DotNetPath @('run','--project',(Join-Path $repoRoot 'tools\ISE.HistoricalResearch.ShadowOpportunityReconstructionStudy\ISE.HistoricalResearch.ShadowOpportunityReconstructionStudy.csproj'),'--',$DatasetPath,$phase2) '06-phase-2.log' | Out-Null

        $phase3 = Join-Path $runRoot 'phase-3-lifecycle'
        Invoke-NativeStep 'Phase3Lifecycle' $DotNetPath @('run','--project',(Join-Path $repoRoot 'tools\ISE.HistoricalResearch.ProtectedTradeLifecycleDecompositionStudy\ISE.HistoricalResearch.ProtectedTradeLifecycleDecompositionStudy.csproj'),'--',$DatasetPath,$phase3) '07-phase-3.log' | Out-Null

        $phase4 = Join-Path $runRoot 'phase-4-management-gates'
        Invoke-NativeStep 'Phase4ManagementGates' $DotNetPath @('run','--project',(Join-Path $repoRoot 'tools\ISE.HistoricalResearch.ManagementGateCounterfactualStudy\ISE.HistoricalResearch.ManagementGateCounterfactualStudy.csproj'),'--',$DatasetPath,$phase4) '08-phase-4.log' | Out-Null

        $phase5 = Join-Path $runRoot 'phase-5-robustness'
        $phase4Input = Join-Path $phase4 'trade-by-trade-summary.tsv'
        Invoke-NativeStep 'Phase5Robustness' $DotNetPath @('run','--project',(Join-Path $repoRoot 'tools\ISE.HistoricalResearch.ManagementRobustnessStudy\ISE.HistoricalResearch.ManagementRobustnessStudy.csproj'),'--',$phase4Input,$phase5) '09-phase-5.log' | Out-Null

        $provenance = [ordered]@{
            schemaVersion = 1; runStartedUtc = $runStartedUtc.ToString('O'); repositoryRoot = $repoRoot
            branch = $branch; commitSha = $sha; worktreeDirty = ($porcelain.Count -gt 0)
            datasetPath = (Resolve-Path -LiteralPath $DatasetPath).Path; datasetSha256 = $datasetHash
            datasetBytes = $datasetItem.Length; datasetLastWriteUtc = $datasetItem.LastWriteTimeUtc.ToString('O')
            datasetRecords = $rows.Count; datasetSessions = $sessionGroups.Count
            firstSession = $sessionGroups[0].Name; lastSession = $latestGroup.Name; latestSessionBars = $latestGroup.Count
            dataAcquisition = 'External NinjaTrader BarsRequest export; not invoked by this runner.'
        }
        $provenance | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot 'provenance.json') -Encoding UTF8
    }
    finally { Pop-Location }
}
catch {
    $failure = $_.Exception.Message
    if ([string]::IsNullOrWhiteSpace($failure)) { $failure = ($_ | Out-String).Trim() }
    if ([string]::IsNullOrWhiteSpace($failure)) { $failure = 'Unknown unattended runner failure.' }
    if ($steps.Count -eq 0 -or $steps[-1].Status -ne 'FAIL') { Add-Step 'Runner' 'FAIL' $failure }
}
finally {
    if (Test-Path -LiteralPath $runRoot) {
        $overall = if ($null -ne $failure) { 'FAIL' } elseif ($warnings.Count -gt 0) { 'WARN' } else { 'PASS' }
        $summary = [ordered]@{
            schemaVersion = 1; status = $overall; runStartedUtc = $runStartedUtc.ToString('O')
            runCompletedUtc = (Get-Date).ToUniversalTime().ToString('O'); runRoot = $runRoot
            warnings = @($warnings); failure = $failure; steps = @($steps)
            authoritativeNotice = 'Only V7.8.7 continuous frozen validation is authoritative. Phase 1-5 outputs are diagnostic; shadow values are not authoritative P&L.'
        }
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runRoot 'unattended-summary.json') -Encoding UTF8
        @("ISE Elite Unattended Frozen Research: $overall", "Run: $runRoot", "Failure: $failure", "Warnings: $($warnings -join ' | ')", '', ($steps | ForEach-Object { "$($_.status)`t$($_.name)`t$($_.detail)" }), '', $summary.authoritativeNotice) | Set-Content -LiteralPath (Join-Path $runRoot 'unattended-summary.txt') -Encoding UTF8
        Write-Host "UNATTENDED RESEARCH $overall"
        Write-Host "SUMMARY $(Join-Path $runRoot 'unattended-summary.json')"
    }
    if ($hasMutex -and $mutex) { $mutex.ReleaseMutex() }
    if ($mutex) { $mutex.Dispose() }
}

if ($null -ne $failure) { exit 1 }
exit 0
