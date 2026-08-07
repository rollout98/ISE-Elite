# ISE Elite New York Multi-Month Dataset Generator

## Purpose

This component connects the validated historical acquisition abstraction to the New York session extractor and the normalized local file store. It is the research pipeline stage that turns a requested historical range into a deterministic New York-session dataset suitable for later regime classification and trade-opportunity labeling.

## Pipeline

1. Acquire bars through `IHistoricalDataSource` using an explicit `HistoricalDataAcquisitionRequest`.
2. Normalize and validate instrument, contract, bar interval, and exact UTC request boundaries.
3. Convert timestamps to U.S. Central time and select the configured New York research window.
4. Group bars into Central-date session slices.
5. Flatten the selected slices in chronological order.
6. Persist the normalized selected bars with `HistoricalDataFileStore`.
7. Return a manifest containing source-bar count, selected-bar count, session count, first/last session dates, request range, window, and output path.

## Initial research target

- Instrument: MNQ
- Contract: explicit contract month; no silent continuous-contract merging
- Interval: 60 seconds for the first dataset
- Source: NinjaTrader Repository preferred for completed historical data; Provider remains available for controlled comparison
- Session window: configurable in Central time
- Scope: New York research first

The generator does not encode the production strategy. The session window is deliberately configurable so the Lab can compare opening drive, early reversal, pullback/continuation, volatile auction, range/no-trade, and later continuation/reversal behavior without treating any one clock window as proven.

## Dataset acceptance

A generated research dataset should not be accepted merely because a file was written. Before regime labeling begins, record and review:

- requested UTC range
- explicit contract and interval
- source kind
- source bar count
- selected NY bar count
- NY session count
- first and last included Central session dates
- obvious date gaps or partial sessions
- rollover boundaries
- duplicate/mixed-series rejection

The next runtime step is to invoke this pipeline with the validated NinjaTrader historical adapter over a multi-month MNQ range and inspect its coverage before using it to generate the 150+ qualified-trade research set.

## Safety

This is Historical Research / Lab infrastructure only. It contains no order submission, order modification, cancellation, flatten, position-management, account-governance, or live-account behavior.
