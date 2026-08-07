# ISE Elite NY Runtime Multi-Month Dataset Probe

## Purpose

This supervised NinjaTrader 8 probe validates the Historical Research path against a multi-month MNQ range. It is read-only and does not place, modify, cancel, or flatten orders.

## Validated June-July 2026 acquisition model

- Instrument family: `MNQ`
- Bars: 60 seconds
- Requested Central range: 2026-06-01 00:00 through 2026-08-01 00:00
- New York research window: 06:00 through 11:00 Central
- Trading-hours template: `CME US Index Futures ETH`
- Source: NinjaTrader Repository
- Contract identity is preserved on every bar.
- No continuous-contract merge, back-adjustment, or synthetic price stitching is performed.

The rollover comparison showed that `MNQ 09-26` first became the New York-window volume leader on 2026-06-15. The contract-aware dataset therefore uses `MNQ 06-26` before 2026-06-15 and `MNQ 09-26` from 2026-06-15 forward.

## Critical BarsRequest finding

A single large multi-week Repository `BarsRequest` produced incomplete slices on several dates even though both Provider and Repository returned full 300-bar windows when those dates were requested individually. The affected dates were 2026-06-19, 2026-07-03, 2026-07-09, and 2026-07-22.

Research acquisition therefore uses deterministic one-day request chunks and combines the resulting bars only after each daily request completes. This is a data-integrity rule for Historical Research; multi-week request completeness must not be assumed from a successful callback alone.

The final supervised daily-chunk run produced:

- 12,600 selected bars
- 42 Central session dates
- 300 bars in every selected session
- zero partial sessions
- first session 2026-06-01
- last session 2026-07-31

These values are runtime observations from the supervised NinjaTrader probe. The cross-platform validator below is the independent repository-level gate for the persisted TSV.

## Output file

The accepted runtime path is:

`ISEEliteResearch/ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv`

The TSV uses the Historical Research schema:

`instrument, contract, timestampUtc, tradingDay, intervalSeconds, OHLCV, sourceKind, sourceName, bid, ask`

## Cross-platform validation

`HistoricalDataFileStore.ReadContractAware` loads multi-contract files while preserving the original single-contract `Read` behavior. `ContractAwareHistoricalDatasetValidator` rejects mixed instruments, mixed intervals, duplicate/overlapping timestamps, and re-entry into a previously completed futures-contract segment.

The validation CLI loads the generated TSV through the Historical Research library and reports coverage plus explicit contract segments:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.DatasetValidator -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

For the supervised runtime dataset, the expected validation result is 12,600 bars, 42 sessions, zero partial sessions, and two one-way contract segments (`06-26` then `09-26`). The persisted file is not considered formally accepted until this command reproduces those results.

## Acceptance gate

A dataset is research-clean for the defined window only when:

1. NinjaScript compiles with no errors.
2. Daily Repository requests complete without error or timeout.
3. Contract rollover policy is evidence-based and documented.
4. Contract identity is preserved on every bar.
5. No duplicate or overlapping timestamps exist across contract segments.
6. Contract order is one-way; an earlier contract may not re-enter after rollover.
7. Every selected 06:00-11:00 Central session contains exactly 300 one-minute bars unless a separately documented exception is explicitly accepted.
8. The generated TSV loads through `HistoricalDataFileStore.ReadContractAware` and passes `ContractAwareHistoricalDatasetValidator`.
9. Coverage and contract-segment output are retained with the research run.

## Follow-on

After cross-platform validation is clean, the dataset may feed regime classification and opportunity labeling. The 40/80 protected-fill harness remains execution/safety validation only and is not a Historical Research optimization objective.
