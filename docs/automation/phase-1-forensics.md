# ISE Research Automation Framework — Phase 1 Forensics

## Purpose

Phase 1 explains *why* the frozen V7.8.7 forward study selected, rejected, exited, or failed to monetize opportunities. It is diagnostic only. It must not modify frozen research behavior.

Primary first target: 2026-08-21 MNQ.

## Frozen control — immutable

The forensic implementation SHALL call or observe the existing V7.8.7 pipeline without changing:

- `BarsSinceLastReset >= 3`
- Entry Efficiency `>= 70`
- Potential `>= 80`
- V7.3 management (`enablePreExtensionAdaptiveBreakeven: false`)
- one position at a time
- maximum 2 executed attempts per session
- existing Fixed2 / Funded175 / Combine250 risk profiles
- evaluation begins after 2026-08-10
- current research window semantics

No tuning, optimization, threshold search, promotion, or merge into the frozen research branch.

## Existing lifecycle evidence to expose

`MorningProtectedManagedTrade` already exposes:

- `FinalMode` (`Scalp`, `Core`, `Runner`)
- `ExitReason`
- `ExitUtc`
- `ExitPrice`
- `RealizedTicks`
- `RealizedDollars`
- `MaxFavorableTicks`
- `MaxAdverseTicks`
- `ExtensionActivated`
- `AdaptiveBreakevenActivated`
- `BestProtectedTicks`
- `MaximumAlignedFiveMinuteBars`

Phase 1 SHALL surface these fields rather than reimplementing management logic.

## Candidate-level report

For every candidate on the requested session date, report at minimum:

- session date Central
- entry UTC and Central
- direction
- entry price
- structural stop price
- initial risk ticks
- Entry Efficiency score
- Potential score
- `BarsSinceLastReset`
- baseline eligible (`Entry>=70 && Potential>=80`)
- frozen eligible (`baseline && BarsSinceLastReset>=3`)
- selected/executed by frozen replay
- quantity under each requested risk profile when applicable
- rejection/disposition reason

Disposition reason codes should be explicit and stable. At minimum distinguish:

- `RejectedEntry`
- `RejectedPotential`
- `RejectedResetAge`
- `RejectedPositionOpen`
- `RejectedAttemptLimit`
- `RejectedRisk`
- `ManagedNull`
- `Selected`

Do not collapse multiple rejection causes into a generic `Rejected` value.

## Selected-trade lifecycle report

For every selected frozen trade, report:

- entry time and price
- exit time and price
- duration
- direction
- quantity
- realized ticks / dollars
- MFE (`MaxFavorableTicks`)
- MAE (`MaxAdverseTicks`)
- final mode
- exit reason
- extension activated
- highest aligned 5-minute streak
- best protected ticks
- reached Core? (`ExtensionActivated` or final mode >= Core)
- reached Runner? (`FinalMode == Runner`)
- realized/MFE capture ratio when MFE > 0

## Post-exit opportunity observation

Add a strictly observational counterfactual that does not change the frozen trade:

- favorable excursion after frozen exit through the end of available same-session data
- adverse excursion after frozen exit
- maximum same-direction price available after exit
- maximum opposite-direction price after exit
- additional favorable ticks left after exit

This section is diagnostic only and SHALL be clearly labeled `POST_EXIT_OBSERVATION`. It must never feed back into selection, management, scores, or thresholds.

## Session opportunity summary

For the requested date, summarize:

- total raw sequencing candidates
- baseline-eligible candidates
- frozen-eligible candidates
- selected trades
- rejected by reset age
- rejected by position occupancy
- rejected by attempt limit
- rejected by risk
- selected trade total P&L
- selected MFE total / average
- selected realized/MFE capture ratio average
- Core count
- Runner count
- exit-reason counts

## First required diagnosis — 2026-08-21

The first report SHALL answer, with evidence, which of the following best explains the frozen `-$70` result:

1. Normal loss on a correctly selected opportunity.
2. Profitable baseline opportunity rejected by `BarsSinceLastReset >= 3`.
3. Visible opportunity never became Entry>=70/Potential>=80 baseline candidate.
4. Opportunity blocked by one-position / max-attempt sequencing.
5. Selected trade reached meaningful MFE but management failed to retain it.
6. Trade remained scalp and never qualified for Core/Runner.
7. Research-window/data boundary materially limited the lifecycle observation.
8. Data/reconstruction anomaly.

More than one diagnosis may apply; report evidence separately.

## Output

Implement a console study/tool that accepts:

```text
<continuous-forward-mnq-tsv> [yyyy-MM-dd]
```

Default target date: latest evaluation session in the file.

Console output SHALL be human-readable and tabular. Also support writing a machine-readable TSV or JSON report suitable for the later unattended automation framework.

Suggested project name:

`tools/ISE.HistoricalResearch.FrozenTradeForensicsStudy`

## Tests

Add tests that prove:

- forensics does not alter frozen candidate selection;
- disposition classification is deterministic;
- Core/Runner classification matches `MorningProtectedManagedTrade` state;
- capture ratio handles zero-MFE safely;
- post-exit observation does not mutate or replace frozen outcomes;
- a date filter cannot leak candidates from adjacent sessions.

All existing `ISE.HistoricalResearch.Tests` must remain green (recovered baseline: 209/209) and new tests must pass.

## Phase 1 completion gate

Phase 1 is complete when a reproducible report for 2026-08-21 explains every baseline/frozen candidate and every selected trade lifecycle, including whether Core or Runner was reached and how much favorable excursion was captured versus available, without changing any V7.8.7 result.