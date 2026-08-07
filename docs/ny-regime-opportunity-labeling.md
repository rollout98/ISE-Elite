# ISE Elite New York Regime, Opportunity, Outcome, and Multi-Cycle Target Research

## Purpose

This phase turns the validated 06:00-11:00 Central MNQ historical dataset into deterministic research features, seed regime classifications, candidate opportunity labels, forward-path outcome evidence, and a multi-cycle daily-objective study. It is Historical Research / Lab code only. It does not place orders, size positions, manage accounts, or define production entry rules.

The research direction is now explicitly aligned to daily target completion rather than capturing the entire morning move. The production hypothesis under study is that two MNQ contracts can often reach the daily objective through one or two high-quality trade cycles, with a third cycle retained as research evidence only until its incremental value and risk are measured.

## Research regimes

The initial taxonomy follows the approved research plan:

1. `OpeningDrive`
2. `EarlyReversal`
3. `DeepPullbackContinuation`
4. `VolatileTwoSidedAuction`
5. `RangeNoTrade`
6. `LaterContinuationReversal`
7. `Unclassified` for sessions that do not satisfy a seed rule

`Unclassified` is retained deliberately so the research process does not force every session into a category.

## Research windows

All windows are U.S. Central time and are features/label windows, not production trading permissions:

- 06:00-08:30: pre-open context
- 08:30-09:05: opening behavior
- 09:05-09:30: early pullback/reversal behavior
- 09:30-10:00: later continuation/reversal behavior
- 08:30-10:00: core-morning range/efficiency context
- the source dataset remains 06:00-11:00 so later research can measure additional forward path

## Extracted session features

`NewYorkSessionResearchFeatureExtractor` calculates deterministic OHLC-derived features per Central session date while allowing explicit contract changes across dates:

- pre-open range
- opening range
- opening displacement
- opening directional efficiency
- early displacement
- early adverse excursion from the opening close
- later displacement
- core-morning range
- core-morning displacement
- core directional efficiency

The extractor requires one instrument and one bar interval but permits the explicit `MNQ 06-26` -> `MNQ 09-26` contract transition already validated in PR #78.

## Seed classifier

`NewYorkRegimeSeedClassifier` applies explicit configurable thresholds to the extracted features. Thresholds are normalized to observed range/efficiency where practical rather than being tied to a fixed 40/80 execution harness.

The classification score is a research-ranking aid only. It is not a probability of profit, confidence promise, or production signal strength.

## Opportunity seeds

`NewYorkOpportunitySeedLabeler` creates candidate research labels only for directional regimes:

- opening-drive continuation
- early reversal
- deep-pullback continuation
- later continuation
- later reversal

`RangeNoTrade`, `VolatileTwoSidedAuction`, and `Unclassified` do not automatically create directional opportunity seeds in this pass.

The first supervised 42-session run produced 17 directional seeds. These seeds are still not counted toward the 150+ qualified-trade gate.

## Outcome ledger

`NewYorkOpportunityOutcomeLabeler` converts each directional seed into a deterministic forward-path research row. The entry reference is the open of the first one-minute bar at or after the seed window start, provided that bar still lies inside the seed window. The forward path is measured through 11:00 Central.

Each outcome records entry reference, MFE/MAE, time to extrema, opportunity-window and session-end directional moves, opening-range excursion multiples, optional research economics, and runner evidence. These are descriptive research outputs rather than production exits.

The default MNQ research economics are tick size `0.25` and point value `$2.00`. Commission and slippage default to zero and must be supplied explicitly when after-cost analysis is desired.

## Multi-cycle target study

The next study intentionally changes the primary question from "how much of the full move can be retained?" to "how often is the daily objective available within one, two, or three distinct morning phases with two MNQ contracts?"

`NewYorkMultiCycleTargetAnalyzer` uses three non-overlapping Central-time phase buckets:

1. `Opening`: 08:30-08:45
2. `EarlyReset`: 08:45-09:30
3. `LaterReset`: 09:30-10:30

The phase boundaries are research partitions chosen to test the observed tendency for pullbacks/reversals to appear around 08:45-09:05 and again around 09:30-10:00. They are not production entry permissions.

Inside each phase, the analyzer computes an **opportunity envelope**: the largest chronologically valid long or short price excursion available from a one-minute bar open to a later high/low in that same phase. The best direction, entry-reference time, exit-extreme time, points, ticks, dollar envelope, and first time the $500 or $1,000 objective becomes available are recorded.

With the default two-contract MNQ economics:

- $500 requires 125 points / 500 ticks of favorable movement;
- $1,000 requires 250 points / 1,000 ticks of favorable movement.

The study then accumulates the three phase envelopes in chronological order and reports whether the lower and upper daily objectives were available within one, two, or three cycles.

This is intentionally an optimistic **movement-availability upper bound**. It uses hindsight inside each phase and therefore must not be described as achieved or executable P&L. Its purpose is to answer whether treating a pullback/reset as a fresh trade cycle is worth deeper entry/exit modeling. Production qualification will require causal setup rules, stops, costs, governance, and OOS evidence.

## Dataset runs

Run the regime labeler:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.RegimeLabeler -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

Run the outcome ledger:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.OutcomeLedger -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

Run the multi-cycle target study:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.MultiCycleTargetStudy -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

The multi-cycle CLI prints:

- sessions and cycle count;
- $500 and $1,000 availability within one, two, and three cycles;
- aggregate movement availability by `Opening`, `EarlyReset`, and `LaterReset`;
- per-session cycles-to-objective;
- one row per phase with direction, entry/exit time, points/ticks/dollars, and first target-hit timestamps.

## Research guardrails

- the 40/80 protected-fill harness remains execution/safety validation only and is not an optimization target;
- the objective is daily target completion, not maximum MFE or holding until 11:00;
- the multi-cycle envelope is an upper-bound movement study, not a backtest or production P&L result;
- do not tune classifier thresholds merely to increase opportunity count in this 42-session sample;
- do not promote a third production trade until the incremental completion benefit and added drawdown are measured;
- preserve `Unclassified` and no-trade outcomes rather than forcing labels.

## Next gate

1. run the multi-cycle target study on the validated 42-session dataset;
2. measure how often $500 and $1,000 are available within one, two, and three chronological phases using two MNQ;
3. inspect whether the `EarlyReset` and `LaterReset` phases independently provide meaningful target-sized excursions;
4. use the first-hit timestamps to determine whether target availability generally precedes the observed pullback windows;
5. only then add causal pullback-onset detection and realistic trade-cycle entry/exit rules;
6. keep PR #79 draft until this distribution is reviewed;
7. expand historical coverage toward 150+ qualified NY opportunities before production-rule tuning.
