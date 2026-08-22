[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param([string]$TaskName = 'ISE Elite Unattended Frozen Research')

$ErrorActionPreference = 'Stop'
if (-not (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue)) {
    Write-Host "Scheduled task is not installed: $TaskName"
    return
}
if ($PSCmdlet.ShouldProcess($TaskName, 'Unregister scheduled task')) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Removed scheduled task: $TaskName"
}
