# Phase 1 Frozen Forensics Runbook

## Pull and build

```powershell
cd C:\ISEDATA\ISE-Elite
git pull
dotnet build .\tools\ISE.HistoricalResearch.FrozenTradeForensicsStudy\ISE.HistoricalResearch.FrozenTradeForensicsStudy.csproj
```

## Preserve research regression gate

```powershell
dotnet test .\tests\ISE.HistoricalResearch.Tests\ISE.HistoricalResearch.Tests.csproj
```

Expected recovered control baseline before Phase 1 additions: 209/209 passing.

## August 21 first forensic run

```powershell
$dataset = "$env:USERPROFILE\Documents\NinjaTrader 8\ISEEliteResearch\morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv"
$out = "C:\ISEDATA\ISEEliteResearch\Validation\2026-08-21\forensics"

dotnet run --project .\tools\ISE.HistoricalResearch.FrozenTradeForensicsStudy\ISE.HistoricalResearch.FrozenTradeForensicsStudy.csproj -- $dataset 2026-08-21 $out
```

The tool emits candidate disposition, Fixed2 selected-trade lifecycle, Core/Runner state, MFE/MAE, realized-to-MFE capture ratio, post-exit observation, diagnosis flags, and machine-readable TSV/JSON artifacts.

## Governance

This tool is diagnostic only. It must not alter V7.8.7 selection, thresholds, management, risk profiles, or outcomes. Do not merge diagnostic findings back into the frozen research branch and do not tune parameters from the August 21 report.
