# Range Entry + Protected VectorFlow Hold v2

## Purpose

This research layer models the established workflow in which the 3-minute Range Filter is the only entry authority and the 5-minute VectorFlow state is used only to decide whether a profitable scalp may be held longer.

## Entry authority

- Confirmed 3-minute Range Filter direction flip only.
- Fill reference remains the next one-minute bar open.
- VectorFlow cannot create an entry.
- Structural risk is measured from the same recent Range Filter structure used by v1.

## Protected extension

The normal scalp objective remains authoritative until it is actually reached.

- Breakeven protection arms after +100 ticks.
- If the scalp objective is reached without completed 5-minute VectorFlow alignment, the trade exits as a scalp.
- If the scalp objective is reached with completed 5-minute VectorFlow alignment, the same trade may extend as Core.
- Runner status is earned only after additional favorable excursion and persistent 5-minute alignment.
- A 75% peak-pullback rule retains at least 25% of the best favorable excursion once extension is active.
- Runner mode also applies a 250-tick peak trail.
- Loss of completed 5-minute VectorFlow bias exits an extended trade rather than returning to the original structural stop.
- Protection calculated from the current bar becomes active on the following bar to avoid same-bar hindsight.

These are transparent research seeds derived from the supplied VectorFlow workflow. They are not production settings.

## Risk qualification

The study reports stage eligibility before later daily sequencing:

- Combine: structural risk <= 325 ticks.
- Funded: structural risk <= 250 ticks.

Risk qualification is diagnostic in this pass. It does not create entries and does not alter position management.

## Dataset warmup and diagnostics

`ISEEliteMNQIndicatorLogicDatasetProbe` now includes observed 2026-05-28 and 2026-05-29 trading sessions as pre-June warmup when available. The study requires at least 1,200 one-minute bars before 2026-06-01 03:00 CT.

The probe no longer treats every full ETH session as invalid merely because its observed bar count differs from a naive 1,380-bar expectation. It reports, per observed trading day:

- first and last timestamp,
- observed bar count,
- 03:00-11:00 CT morning bar count,
- gaps larger than one minute,
- largest observed gap.

The critical research check is the 03:00-11:00 morning window plus adequate indicator warmup.

## Current scope

Research only. No live NinjaTrader order-routing or position-management behavior is changed. Daily attempt sequencing, commissions, slippage, copy latency, and production parameter selection remain follow-on work.
