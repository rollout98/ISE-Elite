[CmdletBinding()]
param(
    [string]$DatasetPath = "C:\Users\devon\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv",
    [string]$ValidationRoot = "C:\ISEDATA\ISEEliteResearch\Validation\unattended",
    [int]$RefreshTimeoutMinutes = 45,
    [switch]$AllowDirtyWorktree
)

$ErrorActionPreference = 'Stop'
$refresh = Join-Path $PSScriptRoot 'Invoke-NinjaTraderDatasetRefresh.ps1'
$research = Join-Path $PSScriptRoot 'Invoke-UnattendedFrozenResearch.ps1'

& powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $refresh -DatasetPath $DatasetPath -TimeoutMinutes $RefreshTimeoutMinutes
$refreshCode = $LASTEXITCODE
if ($refreshCode -ne 0) {
    Write-Host "UNATTENDED CHAIN STOPPED before research; refresh exit code $refreshCode (WAIT=10, WARN=11, FAIL=1)."
    exit $refreshCode
}

$arguments = @('-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',$research,'-DatasetPath',$DatasetPath,'-ValidationRoot',$ValidationRoot)
if ($AllowDirtyWorktree) { $arguments += '-AllowDirtyWorktree' }
& powershell.exe @arguments
exit $LASTEXITCODE
