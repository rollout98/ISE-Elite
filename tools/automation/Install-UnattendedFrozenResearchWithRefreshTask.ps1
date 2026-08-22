[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$TaskName = 'ISE Elite Unattended Frozen Research With Refresh',
    [string]$DatasetPath = "C:\Users\devon\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv",
    [string]$ValidationRoot = "C:\ISEDATA\ISEEliteResearch\Validation\unattended",
    [datetime]$DailyAt = [datetime]::Today.AddHours(12).AddMinutes(15)
)

$ErrorActionPreference = 'Stop'
$runner = (Resolve-Path (Join-Path $PSScriptRoot 'Invoke-UnattendedFrozenResearchWithRefresh.ps1')).Path
$arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $runner + '" -DatasetPath "' + $DatasetPath + '" -ValidationRoot "' + $ValidationRoot + '"'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Daily -At $DailyAt
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Hours 5)
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, "Register daily interactive task at $($DailyAt.ToString('HH:mm'))")) {
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Description 'Read-only NinjaTrader BarsRequest refresh followed by frozen V7.8.7 research validation. No trading behavior.' -Force | Out-Null
    Write-Host "Registered scheduled task: $TaskName"
    Write-Host 'Requires the user to be logged on and NinjaTrader running with ISEEliteMNQUnattendedDatasetRefreshProbe loaded.'
    Write-Host 'This installer does not start NinjaTrader, connect brokerage accounts, or submit orders.'
}
