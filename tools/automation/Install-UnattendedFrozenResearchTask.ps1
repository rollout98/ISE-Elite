[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$TaskName = 'ISE Elite Unattended Frozen Research',
    [string]$DatasetPath = "C:\Users\devon\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv",
    [string]$ValidationRoot = "C:\ISEDATA\ISEEliteResearch\Validation\unattended",
    [datetime]$DailyAt = [datetime]::Today.AddHours(12).AddMinutes(15)
)

$ErrorActionPreference = 'Stop'
$runner = (Resolve-Path (Join-Path $PSScriptRoot 'Invoke-UnattendedFrozenResearch.ps1')).Path
if (-not (Test-Path -LiteralPath $DatasetPath -PathType Leaf)) { throw "Dataset not found: $DatasetPath" }

$quotedRunner = '"' + $runner + '"'
$quotedDataset = '"' + $DatasetPath + '"'
$quotedRoot = '"' + $ValidationRoot + '"'
$arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File $quotedRunner -DatasetPath $quotedDataset -ValidationRoot $quotedRoot"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Daily -At $DailyAt
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 4)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType S4U -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, "Register daily task at $($DailyAt.ToString('HH:mm'))")) {
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Runs frozen V7.8.7 validation and diagnostic Phases 1-5. Does not acquire NinjaTrader data or trade.' -Force | Out-Null
    Write-Host "Registered scheduled task: $TaskName"
    Write-Host 'The task uses S4U and can run while this user is logged off. NinjaTrader data acquisition remains separate.'
}
