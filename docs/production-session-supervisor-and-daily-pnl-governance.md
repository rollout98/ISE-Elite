# ISE Elite Production Session Supervisor and Daily P&L Governance Requirements

**Status:** Governing production requirement  
**Version:** 1.0 draft implementation baseline  
**Primary instrument baseline:** MNQ  
**Primary account model:** Per-account governance with correlated fleet awareness  
**Time zone:** Central Time

## 1. Purpose

This document defines how ISE Elite shall pursue the daily profit objective without forcing trades, overtrading, increasing recovery risk, or surrendering a respectable green day.

The production supervisor shall coordinate:

- opening-regime classification;
- entry eligibility;
- cooldown and second-opportunity handling;
- contract risk budgeting;
- runner promotion and protection;
- meaningful green-day preservation;
- lower and upper daily-objective lockouts;
- two-attempt and two-loss limits;
- authoritative risk and force-flat controls.

This document governs production behavior. The fixed 40-tick stop and 80-tick target used by the current protected-fill harness remain infrastructure-test values and are not the final production exit model.

## 2. Governing principle

> The daily target is an opportunity, not an obligation. Protecting capital and preserving a green day take priority over completing the upper profit objective.

ISE Elite shall not continue trading solely because the account has not reached the daily target.

## 3. Daily business objective

The intended per-account objective is:

- lower objective: **$500**;
- upper objective: **$1,000**;
- preferred trade count: **one position**;
- absolute v1 maximum: **two completed trade attempts**;
- preferred opening size: **2-3 MNQ contracts when the structural stop fits the approved dollar-risk budget**.

The first position is intended to complete the day. A second position is recovery capacity for a genuinely qualified later opportunity, not a requirement.

For copied accounts, the same trade is correlated fleet exposure. Account count does not create strategy diversification. All lockouts and risk budgets must therefore be evaluated per account and at the fleet-control layer.

## 4. Production session phases

### 4.1 Opening opportunity: approximately 8:30-9:05 AM CT

ISE shall classify the opening before selecting an entry model.

Possible classifications include:

1. clean directional opening drive;
2. directional move with deep pullbacks;
3. unstable two-sided volatile auction;
4. unresolved opening balance;
5. confirmed opening reversal.

A clean opening drive may qualify for one 2-3 contract runner position. An unstable opening shall trigger stand-aside or cooldown behavior rather than immediate repeated entries.

### 4.2 Opening reset: approximately 9:05-9:30 AM CT

ISE shall reassess:

- whether the opening direction remains valid;
- whether the first move was rejected;
- whether volatility is contracting;
- whether a healthy or deep healthy pullback is forming;
- whether price is repeatedly crossing VWAP or the same structural boundaries;
- whether the market remains a two-sided auction.

This phase may remain non-tradable.

### 4.3 Second-move opportunity: approximately 9:30-10:30 AM CT

When the opening has not completed the objective, ISE may consider one later qualified setup:

- continuation pullback;
- confirmed reversal;
- range resolution with acceptance and retest.

No trade is permitted merely because the daily objective remains incomplete.

## 5. Opening-regime behavior

### 5.1 Clean directional drive

ISE may:

- enter one position;
- use a market-structure or volatility-adjusted stop bounded by the hard dollar-risk ceiling;
- avoid a small fixed full-position target;
- promote the position to runner status when persistence confirms;
- hold while the thesis and governing structure remain valid;
- protect profit as the daily objective is approached.

### 5.2 Deep-pullback directional regime

Some openings contain repeated 200-300-tick pullbacks for one or two hours. ISE shall distinguish a volatile trend from random two-sided chop.

Evidence supporting a deep-pullback trend may include:

- persistent higher highs and higher lows, or lower lows and lower highs;
- price holding predominantly on one side of VWAP or a directional session boundary;
- directional efficiency remaining positive despite large retracements;
- weakening opposing momentum at structural pullback zones;
- order-flow re-alignment with the primary direction;
- repeated pullback completion without invalidating the governing swing.

ISE shall wait for pullback completion and enter near structural invalidation. It shall not chase the initial impulse.

The stop shall be outside the governing invalidation structure. Contract quantity shall be reduced when the structural stop is wider. ISE may choose 3, 2, 1, or 0 contracts according to the approved dollar-risk budget.

### 5.3 Unstable two-sided auction

Evidence may include:

- repeated VWAP crossings;
- both bullish and bearish structural breaks;
- alternating order-flow imbalance;
- large opposing wicks;
- low directional efficiency;
- failed breakouts on both sides;
- rapid stop-and-reversal behavior.

ISE shall enter cooldown or stand aside. It shall not widen stops or increase size to accommodate instability.

### 5.4 Range resolution

After a prolonged unstable opening, ISE may trade only after:

- a confirmed break from balance;
- acceptance outside the range;
- a qualified pullback or retest;
- sufficient narrative, momentum, and structural confidence;
- risk approval.

## 6. Production exit models

### 6.1 Infrastructure 40/80 profile

The current fixed 40-tick stop and 80-tick target are retained for operational testing of:

- entry routing;
- OCO protection;
- long and short price geometry;
- order lifecycle;
- emergency flatten;
- restart recovery.

They are not the default production profit model.

### 6.2 Controlled 40/80 production interpretation

When used in production research, 40/80 should be interpreted as:

- 40 ticks: initial bounded risk reference;
- +80 ticks: confirmation checkpoint.

At +80 ticks:

- weakening structure may cause exit;
- continued instability may cause exit and cooldown;
- confirmed directional persistence may promote the position to a runner.

The system shall not repeatedly scalp 80-tick targets until the daily dollar target is reached.

### 6.3 Opening runner

A qualified runner may remain open while:

- the original thesis remains valid;
- trend persistence remains sufficient;
- no institutional reversal is detected;
- the governing swing structure remains intact;
- authoritative risk controls permit continuation.

ISE shall use delayed structural protection rather than candle-by-candle trailing in deep-pullback conditions.

### 6.4 Daily-objective protection

When total per-account P&L reaches approximately $500:

- no new entries are permitted;
- an already-open qualified runner may continue toward $1,000;
- a protected-profit floor shall be activated;
- a non-qualified position should be flattened and the day locked.

When total per-account P&L reaches approximately $1,000:

- flatten any open position;
- cancel remaining working entry orders;
- lock the account for the day.

## 7. Green-Day Protection Rule

Meaningful green-day protection begins at **+$300 realized P&L per account** in the v1 baseline.

### 7.1 No ordinary second trade

At or above +$300 realized P&L:

- ordinary setups are blocked;
- another entry requires an exceptional A+ setup;
- the trade must remain within the two-attempt limit;
- the planned loss may not breach the protected daily floor.

### 7.2 Protected floor

The v1 protected green-day floor is **+$200 per account**.

Maximum additional planned risk is:

`min(base risk per trade, realized P&L - protected green-day floor)`

Examples using the initial $150 base risk:

- realized +$300 -> maximum new planned risk $100;
- realized +$450 -> maximum new planned risk $150;
- realized +$500 -> no new trade; objective lockout.

The protected floor and base risk are configuration values subject to Lab validation. The principle that a respectable green day must not be sacrificed while chasing the upper objective is not optional.

## 8. Loss and trade-attempt governance

### 8.1 First completed loss

After the first stop-out:

- enter mandatory cooldown;
- reclassify the market regime;
- permit at most one additional qualified attempt;
- do not increase size;
- do not widen the stop because of the prior loss.

### 8.2 Second completed loss

After two consecutive losses:

- lock new entries for the session;
- preserve emergency and force-flat controls;
- require review through telemetry and the ISE Lab.

### 8.3 Maximum attempts

After two completed trade attempts, no additional entry is permitted even when the upper objective has not been reached.

## 9. Valid daily outcomes

The following are acceptable production outcomes:

- $500-$1,000 from one opening runner;
- a smaller green result preserved after one or two qualified trades;
- a controlled loss within the risk policy;
- a $0 no-trade day when the market never qualifies.

The following are prohibited:

- forcing trades to reach a daily quota;
- turning a +$300 day negative while chasing another $200;
- increasing contract size after a loss;
- widening stops after a loss;
- repeatedly firing the 40/80 profile until the dollar target is reached;
- taking more than two completed attempts in v1;
- opening a new position after the $500 lower objective is reached.

## 10. Decision hierarchy

ISE shall evaluate decisions in this order:

1. force-flat window;
2. authoritative risk block;
3. upper-objective flatten and lock;
4. management of an existing position;
5. lower-objective new-entry lockout;
6. consecutive-loss lockout;
7. completed-trade-attempt lockout;
8. post-loss cooldown;
9. setup qualification;
10. green-day exceptional-setup gate and reduced risk budget;
11. normal entry eligibility.

Risk and session governance are authoritative over signal generation.

## 11. Automated testing requirements

The ISE Lab shall test the production policy across:

- clean trend days;
- deep-pullback trend days;
- volatile two-sided openings;
- delayed 9:30-10:30 resolution days;
- opening reversals;
- no-trade days;
- first-loss/second-opportunity sequences;
- green-day preservation scenarios;
- lower-objective runner continuation;
- upper-objective flattening;
- commissions, slippage, and copy-latency sensitivity.

Required metrics include:

- percentage of days reaching $500;
- percentage of days reaching $1,000;
- percentage of daily profit generated by the first position;
- average trades per day;
- green days converted to red days;
- maximum consecutive losses;
- maximum drawdown;
- runner capture efficiency;
- profit giveback after $300 and $500 thresholds;
- fleet-wide correlated worst day.

A configuration shall not be promoted solely because it produces the highest historical profit. It must remain stable across out-of-sample periods and multiple market regimes.

## 12. Implementation mapping

The first domain implementation is `DailyPnlGovernanceEngine` in `ISE.TradeSupervisor`.

The engine currently provides deterministic decisions for:

- normal entry eligibility;
- post-loss cooldown;
- two-loss lockout;
- two-attempt lockout;
- +$300 green-day protection;
- reduced risk above a +$200 protected floor;
- +$500 new-entry lockout;
- qualified runner continuation from $500 toward $1,000;
- non-runner flattening after the lower objective;
- +$1,000 flatten and lock;
- authoritative risk and force-flat overrides.

This domain engine must be wired into the live session coordinator, account/fleet P&L telemetry, risk sizing, NinjaTrader order supervisor, and decision explainability before the policy is considered active in production.

## 13. Acceptance criteria

The requirement is complete only when supervised simulation and replay prove that:

1. an ordinary trade is blocked after +$300;
2. an exceptional trade at +$300 is capped to $100 risk under the v1 baseline;
3. no new trade is permitted after +$500;
4. a qualified existing runner may continue after +$500 with profit protection;
5. a non-qualified position is flattened after the lower objective;
6. all positions flatten at +$1,000 or the force-flat window;
7. the first loss requires cooldown;
8. the second loss locks the session;
9. the third completed trade attempt is impossible;
10. no decision path increases size or widens risk because a prior trade lost;
11. all decisions emit auditable reasons;
12. account and fleet controls agree before an order is routed.
