# New York Session Dataset Extraction

## Purpose

Convert normalized historical bars into deterministic New York research-session slices for ISE Elite Historical Research.

## Design

- input is already-normalized `HistoricalBar` data
- timestamps remain authoritative in UTC
- session membership is determined after conversion to U.S. Central time
- the New York research window is explicit and configurable; this PR does not hard-code a production trading window
- each selected bar is grouped by Central calendar date and preserved in chronological order
- extraction rejects mixed instruments, mixed futures contracts, and mixed bar intervals
- empty input produces an empty dataset instead of synthetic data

## Why the window is configurable

The historical research phase must test different New York behaviors without turning an early research assumption into a production rule. Opening-drive, early-reversal, pullback/continuation, later-continuation, and range/no-trade studies may use different sub-windows. The extractor therefore provides the session-dataset primitive while later research modules define the candidate windows being evaluated.

## Intended first use

Use normalized MNQ data from the validated NinjaTrader historical-data adapter, then construct New York datasets for candidate windows surrounding the U.S. index-futures open and later morning behavior. These datasets become the input to regime classification and trade-opportunity labeling.

## Qualification target

Dataset extraction is infrastructure only. It does not declare trades qualified. The broader New York research gate remains a minimum of 150 qualified historical trades plus after-cost performance, drawdown, out-of-sample, walk-forward, replay, and supervised-forward evidence.

## Safety

This module is Historical Research / Lab only. It has no order-entry, position-management, account, or live-trading behavior.
