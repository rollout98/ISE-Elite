# ISE Elite New York Session Stability and Expansion Gate

**Status:** Governing roadmap and release requirement  
**Version:** 1.0 implementation baseline  
**Current approved scope:** Core New York session  
**Next possible scope:** Expanded New York session  
**Later possible scope:** Asia research and development

## 1. Purpose

ISE Elite shall not add session hours merely to increase trade frequency. The core New York strategy must first demonstrate durable, after-cost performance across sufficient independent evidence.

The gate exists to prevent:

- expanding an unstable strategy;
- using additional sessions to hide weaknesses in the core model;
- overfitting to a small sample;
- optimizing only for win rate while drawdown or expectancy deteriorates;
- depending on a few unusually profitable trend days;
- copying New York parameters directly into Asia without separate validation.

## 2. Governing roadmap rule

> Prove the core New York session first. Expand New York second. Develop Asia only as a separately validated session model after the expanded New York architecture is proven.

The 70-80% win-rate range is a target. It shall not be manufactured by widening stops, accepting disproportionate losses, removing valid losing trades, or weakening risk governance.

## 3. Initial v1 evidence thresholds

The deterministic implementation baseline requires:

- at least **150 qualified trades**;
- at least **70% after-cost win rate**;
- **80%** as the upper target of the intended win-rate band, not a rejection ceiling;
- positive net expectancy per trade after commissions and realistic slippage;
- profit factor of at least **1.25** in the initial configurable baseline;
- drawdown within the approved account and correlated-fleet risk limits;
- losing streaks within the approved stability limit;
- stable out-of-sample results;
- stable walk-forward results;
- stable historical replay results;
- stable supervised forward-test results;
- complete coverage of trend, reversal, deep-pullback, volatile-auction, and no-trade regimes;
- full compliance with daily P&L governance, cooldowns, trade limits, objective lockouts, risk blocks, and force-flat controls;
- acceptable profit concentration, proving results do not depend on a small number of exceptional days.

Thresholds are policy values that may be tightened through the ISE Lab. Lowering them requires explicit design approval and supporting evidence.

## 4. Decision outcomes

The gate may produce the following blocking decisions:

- Hold Core New York;
- Asia Blocked Pending New York Validation;
- Costs Not Included;
- Insufficient Sample;
- Win Rate Below Target;
- Expectancy Not Proven;
- Profit Factor Not Proven;
- Drawdown Not Acceptable;
- Losing Streak Not Acceptable;
- Out-of-Sample Not Proven;
- Walk-Forward Not Proven;
- Replay Not Proven;
- Forward Test Not Proven;
- Regime Coverage Incomplete;
- Governance Compliance Failure;
- Profit Concentration Failure.

Approval decisions are limited to:

- New York Expansion Approved;
- Asia Research Approved.

An approval authorizes the next research and development scope. It does not automatically activate live trading hours or deploy a NinjaTrader runtime change.

## 5. New York expansion gate

The current 8:30-10:30 AM CT New York model remains the only strategy scope until all required evidence passes.

When the gate approves New York expansion, development may research later New York opportunities. Any expanded window shall preserve:

- the same authoritative daily P&L governance;
- the two-attempt production limit unless separately amended and validated;
- green-day protection;
- account and fleet risk ceilings;
- no recovery sizing;
- no stop widening after a loss;
- no trade solely because the daily objective remains incomplete.

The expanded window must still qualify a specific continuation, reversal, range-resolution, or other approved playbook. Time alone shall never create entry eligibility.

## 6. Asia prerequisite

Asia remains blocked until:

1. the core New York gate passes;
2. the expanded New York scope is developed;
3. the expanded New York scope is independently validated under the same stability requirements;
4. the architecture demonstrates that multiple windows can share governance without sharing inappropriate session parameters.

Passing these prerequisites authorizes **Asia research**, not immediate production activation.

## 7. Asia design requirement

Asia shall be implemented as a separate session profile with its own:

- market-open and liquidity behavior;
- volatility distributions;
- entry and cooldown windows;
- pullback expectations;
- stop geometry;
- target and runner behavior;
- news and rollover controls;
- sample set and stability evidence.

The reusable operating-system layers may include risk governance, position protection, recovery, telemetry, explainability, and Lab evaluation. New York entry parameters shall not be copied into Asia without independent evidence.

## 8. Validation data requirements

All metrics submitted to the gate shall:

- include commissions and realistic slippage;
- use qualified trades produced by the approved strategy rules;
- separate in-sample from out-of-sample evidence;
- preserve losing trades and rejected days without discretionary deletion;
- identify market regime and session window;
- identify configuration version;
- include drawdown, losing streak, expectancy, profit factor, and profit concentration;
- demonstrate governance compliance through telemetry.

## 9. Scope of the implementation

`SessionExpansionStabilityGate` is a deterministic domain component in `ISE.TradeSupervisor`.

It:

- evaluates supplied Lab and validation evidence;
- returns one explicit approval or blocking state;
- explains the first authoritative reason for blocking expansion;
- keeps Asia blocked until expanded New York validation is confirmed.

It does not:

- place, change, or cancel orders;
- alter the current NinjaTrader runtime;
- gather historical data by itself;
- calculate strategy metrics from raw trades;
- merge or deploy an expansion automatically.

Those integrations remain separate supervised milestones.

## 10. Acceptance criteria

This requirement is implemented when:

- the configurable policy compiles;
- all blocking states are deterministic;
- New York expansion cannot pass below the minimum sample or win-rate threshold;
- positive after-cost expectancy and minimum profit factor are required;
- independent validation, regime coverage, governance compliance, and concentration checks are required;
- Asia cannot pass before expanded New York validation;
- regression tests pass with zero failures;
- no live-order or NinjaTrader deployment behavior is introduced.
