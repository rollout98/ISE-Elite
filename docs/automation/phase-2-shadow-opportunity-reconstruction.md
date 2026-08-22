# Phase 2 — Shadow Opportunity Reconstruction

## Purpose
Quantify how much economic opportunity the frozen V7.8.7 study identifies but does not monetize, without changing or feeding back into the frozen study.

Phase 1 established that the 2026-08-11 through 2026-08-21 forward sample contains 6 frozen Fixed2 trades, $537 total P&L, $59.67 average daily P&L, 4 Scalp outcomes, 2 Core outcomes, 0 Runner outcomes, and material favorable movement after exits. Phase 2 is diagnostic only.

## Governance boundary
V7.8.7 remains immutable:
- BarsSinceLastReset >= 3
- Entry >= 70
- Potential >= 80
- V7.3 management
- max 2 attempts
- existing risk profiles
- evaluation begins after 2026-08-10

No tuning. No optimization. No promotion. No parameter recommendation from the forward sample. No merge into the frozen research branch.

The shadow engine MUST NOT replace, mutate, or alter the authoritative V7.8.7 replay result. It runs beside the frozen study and writes separate artifacts.

## Research questions
1. For each frozen-selected trade, how much favorable excursion occurred before and after the authoritative exit?
2. Did the trade remain Scalp, reach Core, or reach Runner under the authoritative V7.3 lifecycle?
3. How much of MFE was monetized by the authoritative exit?
4. For baseline-eligible candidates rejected only by BarsSinceLastReset, what market excursion followed the candidate entry?
5. Are low realized returns primarily associated with selection throughput, management capture, or both?
6. Are apparently large post-exit moves immediately tradable continuation, or do they require unacceptable intervening adverse excursion?

## Required implementation
Create a new diagnostic console project:

`tools/ISE.HistoricalResearch.ShadowOpportunityReconstructionStudy`

Usage:

`<continuous-forward-mnq-tsv> [output-directory]`

The study must reconstruct the same upstream candidate pipeline used by the frozen forensic study and must use the authoritative Fixed2 frozen replay as the control.

## Authoritative control fields
For every frozen-selected trade persist:
- session date CT
- entry time CT
- direction
- entry price
- authoritative exit time CT
- authoritative exit price if available
- authoritative exit reason
- realized ticks
- realized dollars
- MFE ticks before authoritative exit
- MAE ticks before authoritative exit
- authoritative final mode: Scalp/Core/Runner
- extension activated
- Core reached
- Runner reached
- capture ratio = realized ticks / MFE ticks when MFE > 0

## Shadow continuation observation
For each selected trade, observe bars after the authoritative exit through the end of that trading-session dataset window. This is observation, not an alternative trade.

Persist:
- maximum additional favorable excursion after exit relative to authoritative exit/entry as clearly defined in schema
- maximum adverse excursion after exit
- timestamp of maximum post-exit favorable excursion
- timestamp of maximum post-exit adverse excursion
- maximum favorable excursion from original entry through window end
- maximum adverse excursion from original entry through window end
- whether price first moved adversely before achieving the later favorable extreme
- adverse excursion required before the later favorable extreme

This requirement prevents a large later move from being mislabeled as freely capturable continuation.

## Reset-age rejected baseline candidates
For every candidate that passes Entry>=70 and Potential>=80 but fails only BarsSinceLastReset>=3, persist:
- session date/time CT
- direction
- entry price
- stop price
- initial risk ticks
- Entry score
- Potential score
- BarsSinceLastReset
- MFE from candidate entry through a bounded observation horizon
- MAE over the same horizon
- MFE/MAE through session-window end
- whether the original structural stop would have been touched before the favorable extreme
- favorable excursion before first structural-stop touch

These rows are diagnostic observations only. They are NOT hypothetical accepted V7.8.7 trades and must not be added to frozen P&L.

## Bounded horizons
Report observations at fixed, non-optimized horizons where data exists:
- 15 minutes
- 30 minutes
- 60 minutes
- 120 minutes
- session-window end

These are descriptive horizons, not target/stop parameters.

## Required outputs
Write separate machine-readable artifacts:
- `selected-trade-shadow.tsv`
- `reset-age-rejected-shadow.tsv`
- `session-summary.tsv`
- `summary.json`
- `summary.txt`

## Full-sample summary
At minimum report:
- sessions
- authoritative selected trade count
- authoritative total P&L
- authoritative average trade
- authoritative average daily P&L
- Scalp/Core/Runner counts
- average authoritative MFE
- average authoritative MAE
- average capture ratio
- median capture ratio
- count of selected trades with positive MFE but non-positive realized result
- count of selected trades with material post-exit continuation
- reset-age-only rejected candidate count
- rejected candidates whose structural stop was touched before meaningful favorable excursion
- rejected candidates with favorable excursion before structural-stop touch

## Safety against hindsight bias
The report must distinguish:
1. authoritative realized outcome,
2. contemporaneous excursion before authoritative exit,
3. post-exit observation,
4. rejected-candidate observation.

Never label the maximum later excursion as realizable P&L. Never imply that a runner could have captured the full later extreme without reporting the intervening adverse path.

## Acceptance criteria
Phase 2 is complete when one command against the recovered continuous-forward dataset produces a full-sample report that:
- exactly preserves the authoritative 6-trade / $537 Fixed2 V7.8.7 result for the current sample,
- reports all 6 authoritative trade lifecycles,
- reports every baseline candidate rejected only by reset age,
- quantifies favorable/adverse path at fixed horizons,
- clearly separates frozen results from shadow observations,
- makes no parameter changes or tuning recommendations,
- passes the existing historical-research test gate before results are accepted.

## Next automation integration
After verification, invoke this study from the unattended research runner after the frozen validation and Phase 1 forensic gate. Shadow-study failure must produce WARN/FAIL diagnostics but must never modify the frozen validation result.