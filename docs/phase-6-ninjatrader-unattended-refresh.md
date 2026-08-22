# Phase 6: NinjaTrader unattended dataset refresh

Phase 6 adds a read-only request/ready handshake ahead of the existing frozen-research runner. It does not change V7.8.7, entry selection, management, risk, attempts, or authoritative research behavior, and it contains no order APIs.

## Supported operating model

NinjaTrader `BarsRequest` is an in-process desktop API, not a supported Windows service API. The safe near-unattended configuration therefore requires:

1. A user logged on to Windows.
2. NinjaTrader running with its historical-data session available.
3. `ISEEliteHistoricalBarsRequestClient.cs` and `ISEEliteMNQUnattendedDatasetRefreshProbe.cs` compiled in `bin\Custom`.
4. The refresh probe loaded in a saved NinjaTrader workspace. It remains active while its chart is inactive.

The PowerShell chain can be launched by Task Scheduler and needs no manually opened PowerShell window. It must not be configured as S4U/logged-off execution: that session cannot safely host the NinjaTrader desktop runtime. The installer is a template only and is not run automatically.

## Safety and readiness protocol

`Invoke-UnattendedFrozenResearchWithRefresh.ps1` first calls `Invoke-NinjaTraderDatasetRefresh.ps1`. The latter detects a running interactive NinjaTrader process and atomically writes `mnq-refresh.request.tsv` under `Documents\NinjaTrader 8\ISEEliteResearch`.

The loaded probe:

- requests MNQ 09-26, 60-second bars from 03:00 through 11:00 Central;
- retries Repository and Provider BarsRequest calls up to three times with exponential backoff and a 120-second call timeout;
- rejects every non-empty session that is not exactly 480 unique, contiguous bars;
- refuses readiness if the latest required weekday is absent or partial;
- writes the TSV to a temporary sibling, computes SHA256, and atomically replaces the destination;
- atomically emits `<dataset>.ready.json` only after post-replace SHA verification;
- records request ID, timestamps, date range, bar/session counts, 480-bar invariant, SHA256, source policy, and no-data weekdays.

The PowerShell side independently verifies the current request ID, manifest, SHA256, bar count, session count, final date, and 480 bars per session before starting research. Exit codes are `0=PASS`, `10=WAIT`, `11=WARN`, and `1=FAIL`. Any non-PASS result stops before tests or research, so a stale/partial TSV is never silently treated as current.

Requests are idempotent and restart-safe: each has a unique ID, the probe processes an ID once, temporary files are request-scoped, and the manifest is bound to the exact dataset hash.

## Installation (not performed by Phase 6)

After compiling/loading the probe and saving the NinjaTrader workspace, review and explicitly run `Install-UnattendedFrozenResearchWithRefreshTask.ps1`. The template uses an interactive principal and `IgnoreNew` overlap protection. Do not replace an existing production task without approval.

Full operation while the user is logged off is not supported. Automating NinjaTrader GUI launch, login, or brokerage/data-provider authentication was deliberately excluded because it is not a safe or supported headless BarsRequest integration.
