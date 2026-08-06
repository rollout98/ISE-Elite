# ISE Elite Historical Research Model v1

## Purpose

The Historical Research Model is an ISE Lab capability for evaluating and improving the New York session model from historical market data without changing the production NinjaTrader runtime.

It is designed to answer four questions:

1. Which New York opening conditions consistently produce qualified opportunities?
2. Which playbook should be selected for each opening regime?
3. How should qualified positions be managed to preserve larger directional moves rather than forcing fixed 40/80 exits?
4. Does the resulting model remain stable after costs, across market regimes, and outside the data used to develop it?

The 40-tick stop / 80-tick target remains an execution-safety harness only. It is not the production research objective.

## Initial scope

- Instrument: MNQ first.
- Session: New York first.
- Time basis: preserve UTC and Central Time for every observation.
- Research only: no live order submission, no NinjaTrader DLL deployment, no account mutation.
- Reuse existing ISE domain logic where practical rather than creating a separate strategy implementation.

## Architecture

```text
NinjaTrader / imported historical source
                |
                v
Historical Data Collector
                |
                v
Normalized Research Store
                |
                v
Session Segmenter
                |
                v
Feature + Regime Classifier
                |
                v
ISE Strategy / Playbook Evaluator
                |
                v
Trade Simulation + Governance Replay
                |
                v
Research Ledger
                |
                v
Metrics / Walk-Forward / Release Gate
```

## Data acquisition

The collector must support two acquisition paths:

1. NinjaTrader-backed collection for historical bars/ticks available from the connected provider or local repository.
2. File import for externally sourced history when provider depth is insufficient.

Raw provider data is immutable after ingestion. Normalized and derived datasets are versioned separately.

## Canonical observation contract

Every observation must preserve:

- source identifier
- instrument and contract
- UTC timestamp
- Central Time timestamp
- trading date
- bid, ask, last when available
- OHLC where bar-based
- volume
- source granularity
- data-quality flags
- ingestion version

Missing bid/ask or incomplete tick history must be represented explicitly rather than synthesized silently.

## Session segmentation

The first research profile is New York. The model must preserve enough pre-open context to evaluate overnight structure and then segment the opening into research windows including:

- pre-open context
- opening drive
- 8:45-9:05 CT reversal/change-of-direction watch
- 9:30-10:00 CT continuation/pullback/second-move watch
- later qualified continuation/reversal/range-resolution period

The exact operational boundaries remain configurable and must be validated historically rather than optimized to a single best sample.

## Opening-regime taxonomy

Initial regimes:

- OpeningDrive
- EarlyReversal
- DeepPullbackContinuation
- VolatileTwoSidedAuction
- RangeBoundNoTrade
- LaterContinuation
- LaterReversal
- Unclassified

Regime classification must record both the selected regime and the evidence used to select it.

## Position-management research

The simulator must evaluate structure-aware management, including:

- initial structural invalidation stop
- break-even only when justified by structure and excursion
- trailing by market structure or volatility
- runner continuation
- exit on structural invalidation
- time/session exit
- governance-forced flatten

Fixed 40/80 may be included only as a baseline/fallback comparison. It must not be treated as the target production behavior.

## Research ledger

Each candidate opportunity must produce an auditable record containing at minimum:

- dataset version
- session date
- instrument/contract
- regime
- playbook/setup
- long/short
- entry eligibility decision
- entry time and price
- initial stop rationale and price
- management events
- maximum favorable excursion
- maximum adverse excursion
- exit time, price, and reason
- gross P&L
- modeled commissions
- modeled slippage
- net P&L
- governance state before and after the trade
- whether the trade was accepted, blocked, or shadow-only

Rejected opportunities are part of the dataset and must not be discarded.

## Data partitions

Historical data must be partitioned before strategy evaluation:

- Development: may be used to design and tune rules.
- Validation: used to compare candidate changes; not repeatedly tuned against.
- OutOfSample: sealed until a candidate model is frozen.
- WalkForward: repeated chronological train/test windows.

Partition membership is deterministic and based on trading date, never random row-level shuffling.

## Required metrics

At minimum:

- qualified trade count
- after-cost win rate
- net expectancy per trade
- profit factor
- maximum drawdown
- longest losing streak
- average and median MFE/MAE
- return by regime
- return by setup
- return by entry time window
- first-attempt vs second-attempt results
- runner contribution
- profit concentration by best days
- blocked-trade counterfactual results
- performance by volatility bucket

## Qualification gate

The existing New York stability gate remains authoritative for release governance. The historical model must generate the evidence required by that gate rather than bypass it.

Initial release-governance baseline:

- 150+ qualified trades
- >= 70% after-cost win rate for the current gate
- positive after-cost expectancy
- profit factor >= 1.25
- acceptable drawdown and losing streaks
- required regime coverage
- out-of-sample, walk-forward, replay, and supervised-forward evidence
- no unacceptable concentration in a few exceptional days

These thresholds are release gates, not optimization targets.

## Anti-overfitting controls

- chronological partitions only
- sealed out-of-sample period
- parameter-neighborhood stability checks
- realistic costs and slippage
- minimum observations per regime
- no removal of losing days without documented data-quality cause
- every model version assigned a reproducible configuration ID
- no tuning against the supervised-forward sample

## Phase 1 deliverable

Phase 1 creates the research-domain foundation:

- deterministic chronological dataset partitions
- canonical dataset metadata
- explicit research-mode contracts
- unit tests for boundary and invalid-partition behavior

It does not yet collect NinjaTrader data or simulate trades.

## Phase 2

Historical market-data ingestion and normalized local persistence.

## Phase 3

New York segmentation, feature extraction, and regime classification.

## Phase 4

Strategy/playbook simulation, structure-aware position management, and research ledger.

## Phase 5

Metrics, walk-forward evaluation, release-gate evidence, and reports.

## Phase 6

NinjaTrader Playback validation of finalist model versions followed by supervised Sim101 forward validation.
