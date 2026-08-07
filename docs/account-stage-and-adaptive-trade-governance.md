# ISE Elite Account-Stage and Adaptive Trade Governance

**Status:** Draft research / architecture baseline  
**Scope:** 03:00-11:00 CT morning operating model, MNQ baseline  
**Purpose:** Separate market intelligence from how aggressively a combine or funded account expresses the same opportunity.

## Governing principle

ISE Elite shall use the same market-state, opportunity, entry, and trade-management intelligence for combine and funded accounts. Account stage changes risk expression and daily-governance behavior; it does not create a different signal strategy.

The morning objective is not to force a fixed number of trades or a fixed exit size. ISE should identify qualified opportunities between 03:00 and 11:00 CT and manage each position as a scalp, core trade, or runner according to causal market behavior.

## Shared trading process

1. Observe the full morning market state from 03:00 CT.
2. Detect qualified fresh opportunities without requiring a fixed clock entry.
3. Determine structural invalidation and authoritative per-account risk budget before entry.
4. Enter only when expected opportunity justifies the required risk.
5. After entry, manage adaptively as scalp, core, or runner.
6. Feed realized/open P&L into account-stage governance.
7. Stop adding risk when the stage-specific daily objective or protection state says the day is complete.

## Management modes

### Scalp

Use when the setup is valid but follow-through is limited, structure is deteriorating, or a larger transition is approaching. The system may take a modest realized contribution rather than demand a runner outcome.

### Core

Use when directional structure is healthy but not exceptional. The trade can contribute meaningfully toward the daily objective while remaining responsive to structural deterioration.

### Runner

Runner status is earned after entry. A position may continue while persistence, structure, and risk controls remain valid. A runner may continue beyond the lower daily objective only under profit protection and without adding a new position.

## Combine profile

The combine/evaluation objective prioritizes efficient objective completion while remaining inside authoritative drawdown and loss controls.

Initial research baseline:

- objective priority: completion;
- risk-expression multiplier: 1.00x of the authoritative base per-trade budget;
- green-day protection begins at +$350 per account;
- protected floor: +$200;
- lower daily objective: +$500;
- upper objective: +$1,000;
- maximum completed attempts remains 2 in the current production-governance baseline;
- maximum consecutive losses remains 2;
- scalp, core, and runner management are all permitted;
- no new position is opened after the lower objective;
- an existing qualified runner may continue after the lower objective under profit protection.

These values are research defaults and are not prop-firm rule definitions.

## Funded profile

The funded objective prioritizes preservation, payout consistency, and avoiding unnecessary drawdown after a good day has already been achieved.

Initial research baseline:

- objective priority: preservation;
- risk-expression multiplier: 0.75x of the same authoritative base per-trade budget;
- green-day protection begins at +$300 per account;
- protected floor: +$250;
- lower daily objective: +$500;
- upper objective: +$1,000, primarily as existing-runner upside rather than a reason to initiate more risk;
- maximum completed attempts remains 2 in the current production-governance baseline;
- maximum consecutive losses remains 2;
- scalp, core, and runner management are all permitted;
- no new position is opened after +$500;
- an already-open qualified runner may continue toward +$1,000 under protection.

The funded profile therefore treats +$500 as a successful day, not as an intermediate quota that must be pressed toward +$1,000.

## Fleet interpretation

Copied accounts are correlated exposure. Account count does not create diversification.

For a 20-account funded fleet, a +$500 per-account day corresponds to a +$10,000 gross fleet objective before fees, payout rules, slippage, copy differences, or firm-specific restrictions. The fleet layer must therefore track both per-account controls and aggregate correlated exposure.

The domain `FleetObjectiveProjection` intentionally performs only projection. It does not authorize risk or imply that all copied accounts will fill identically.

## Separation of responsibilities

### Market State Engine

Determines whether the market is developing, trending, compressing, resuming, exhausting, reversing, or neutral. Clock time is context, not the trigger.

### Opportunity Engine

Finds fresh continuation, pullback, compression-breakout, failed-breakout, reversal, and range-resolution opportunities.

### Risk Engine

Computes structural invalidation and authoritative dollar risk. Account stage may scale expression downward or allow the baseline budget, but may never override authoritative risk limits or widen a stop after entry.

### Position Intelligence Engine

Classifies active management as exit, scalp, core, protect, or runner based on causal post-entry behavior.

### Account-Stage Governance

Applies combine/funded objective priority, risk expression, green-day protection, lower-objective behavior, loss limits, and daily lockouts.

### Fleet Governance

Projects and monitors correlated copied-account exposure. Fleet controls remain authoritative over per-account signal permission when required.

## Validation requirements

Before any stage profile is treated as production-ready, ISE Lab should measure at minimum:

- percentage of sessions reaching +$500 and +$1,000 per account;
- average and median realized P&L;
- maximum drawdown and consecutive losses;
- average completed attempts per day;
- contribution by scalp, core, and runner modes;
- runner capture efficiency and giveback;
- green days converted to red days;
- incremental benefit of combine aggressiveness versus funded preservation;
- fleet-wide correlated worst day;
- sensitivity to commissions, slippage, copy latency, and fill dispersion;
- stability across Development, Validation, OOS, and walk-forward samples.

No profile should be promoted simply because it produces the largest development-sample profit.
