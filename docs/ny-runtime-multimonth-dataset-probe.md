# ISE Elite NY Runtime Multi-Month Dataset Probe

## Purpose

This supervised NinjaTrader 8 probe is the first live use of the historical research pipeline against a multi-month MNQ range. It is read-only and does not place, modify, cancel, or flatten orders.

## Initial run

- Instrument: `MNQ 09-26`
- Source: NinjaTrader Repository
- Bars: 60 seconds
- Requested Central range: 2026-06-01 00:00 through 2026-08-01 00:00
- New York research window: 06:00 through 11:00 Central
- Trading-hours template: `CME US Index Futures ETH`

The wide 06:00-11:00 Central research window is intentionally broader than any eventual production entry window. It preserves the pre-open, open, 8:45-9:05 reversal area, 9:30-10:00 continuation/pullback area, and later morning context for later regime classification.

## Runtime output

The probe writes a normalized tab-delimited file under NinjaTrader's user data directory:

`ISEEliteResearch/ny-MNQ-09-26-20260601-20260731-0600-1100-60s-repository.tsv`

The file schema is identical to `HistoricalDataFileStore` and therefore can be loaded by the Historical Research library without manual trade-by-trade export.

Output 1 prints:

- total BarsRequest records
- records inside the requested date range
- selected NY-window bars
- session count
- first and last Central session dates
- minimum and maximum bars per session
- count and preview of partial sessions
- exact output path

## Acceptance checks

A supervised run is accepted only if:

1. NinjaScript compiles with no errors.
2. Repository BarsRequest completes without an error or timeout.
3. Selected bars and session count are nonzero.
4. First and last session dates fall inside the requested range.
5. Output file exists at the printed path.
6. Partial sessions are reviewed rather than silently discarded. Holidays, early closes, contract liquidity, or missing history can all create shorter sessions.
7. The explicit `MNQ 09-26` contract is preserved. No continuous-contract merge is performed.

## Follow-on

After the first dataset is accepted, load the TSV through the cross-platform Historical Research library for coverage analysis and then add regime classification / opportunity labeling. Contract-roll expansion should use explicit per-contract datasets and an explicit rollover policy rather than silent price merging.
