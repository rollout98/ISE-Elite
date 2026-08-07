# ISE Elite NinjaTrader Historical Data Adapter

## Purpose

Connect the Historical Research model to NinjaTrader 8 historical bars without coupling the cross-platform domain projects to NinjaTrader assemblies.

## Architecture

`ISE.HistoricalResearch.IHistoricalDataSource`

→ `ISE.NinjaTraderHost.HistoricalData.NinjaTraderHistoricalDataSource`

→ `INinjaTraderHistoricalBarsClient`

→ NinjaTrader runtime implementation `ISEEliteHistoricalBarsRequestClient`

→ NinjaTrader `BarsRequest`

The host adapter is part of the normal solution and is unit tested. The concrete BarsRequest client is stored under `ninjatrader/` because it must compile inside the NinjaTrader 8 environment against NinjaTrader assemblies.

## Initial scope

- instrument: MNQ
- explicit quarterly contract, initially `MNQ 09-26`
- sources: NinjaTrader Provider and NinjaTrader Repository
- intervals: seconds or whole-minute intervals
- default research trading-hours template: configured by the caller; initial supervised validation uses `CME US Index Futures ETH`
- timestamps: converted from the configured NinjaTrader/platform timezone to UTC
- trading day: calculated by NinjaTrader `SessionIterator.GetTradingDay()` using the selected trading-hours template
- merge policy: `DoNotMerge`
- exact requested UTC windows are enforced after BarsRequest returns its full-day result set

## Important NinjaTrader behavior

The date-range BarsRequest constructor operates on full local trading days. The adapter therefore requests a containing local-day range and filters returned bars back to the exact UTC research interval. This prevents accidental leakage outside Development, Validation, or Out-of-Sample windows.

NinjaTrader bar timestamps are end-of-bar timestamps. Research features and session segmentation must preserve that convention.

## Data quality and fail-closed behavior

Research acquisition must fail instead of silently guessing when:

- NinjaTrader cannot resolve the requested instrument or trading-hours template;
- BarsRequest reports an error or times out;
- a returned local timestamp is invalid or ambiguous during a daylight-saving transition;
- the requested source is not Provider or Repository;
- downstream normalization detects duplicates, mixed contracts, unexpected intervals, or out-of-range records.

Bid/ask values are preserved when NinjaTrader returns positive values. Missing/non-positive bid/ask values are stored as null rather than fabricated.

## Validation gates

### Cross-platform / repository gate

- `ISE.NinjaTraderHost.Tests` passes.
- `ISE.HistoricalResearch.Tests` passes.
- full Windows solution build passes with zero errors.
- Repository Validation passes.

### NinjaTrader runtime gate

The concrete `ninjatrader/ISEEliteHistoricalBarsRequestClient.cs` file is **not** considered production-validated merely because the solution builds. Before any research acquisition is accepted, compile it in NinjaTrader 8 and run a supervised data pull using:

- NinjaTrader 8
- MNQ 09-26
- Provider lookup first, Repository lookup second
- a small known historical date range
- Output 1 visible
- no order placement and no live-account behavior

Compare returned bar count, timestamps, OHLCV, trading-day assignment, and data-source metadata against a NinjaTrader chart or Historical Data view for the same contract and trading-hours template.

## Safety

This adapter is read-only historical research infrastructure. It does not submit, change, cancel, or flatten orders. It does not enable unattended trading or live-account testing.
