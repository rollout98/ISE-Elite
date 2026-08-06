# ISE Elite Production Runtime Integration Design and Supervised Test Plan

**Status:** Design baseline for implementation review  
**Scope:** New York session, Sim101-only integration milestone  
**Dependency:** The production Daily P&L Governance policy introduced by PR #68  
**Out of scope:** Live-account enablement, Asia-session activation, strategy optimization, and new entry logic

## 1. Purpose

This document defines how ISE Elite shall connect authoritative account and position telemetry to the production Daily P&L Governance engine, convert governance decisions into safe runtime permissions and actions, survive runtime restarts, and prove the integration through supervised Sim101 testing.

The milestone does not change the trading thesis or attempt to improve win rate. It operationalizes the already-agreed production rules:

- pursue a per-account daily objective of $500-$1,000 without treating it as a quota;
- prefer one position capable of completing the day;
- permit no more than two completed trade attempts;
- require cooldown after the first completed loss;
- lock the strategy after two consecutive losses;
- enter green-day protection around +$300 realized P&L;
- preserve a protected green-day floor around +$200;
- lock new entries at +$500;
- allow only an already-open qualified runner to continue toward +$1,000;
- flatten and lock at the upper objective, force-flat window, or authoritative risk block;
- never increase size, widen stops, or chase losses to complete the daily objective.

> The daily target is an opportunity, not an obligation. Protecting capital and preserving a green day take priority over completing the upper profit objective.

## 2. Governing design principles

### 2.1 Broker and platform truth are authoritative

ISE shall not derive account state solely from internal order intentions. The runtime shall reconcile against NinjaTrader account, execution, order, and position state before permitting exposure.

### 2.2 Governance is a permission layer, not an entry generator

The Daily P&L Governance engine shall answer whether a new entry is allowed, how much planned risk is available, whether open profit must be protected, whether a qualified runner may continue, and whether immediate flattening is required. It shall not invent setups, widen stops, or create recovery sizing.

### 2.3 Fail closed on stale or inconsistent telemetry

Missing, stale, duplicated, or contradictory P&L and position data shall block new entries. A data-quality failure shall never be interpreted as permission to trade.

### 2.4 Per-account controls remain authoritative in a copied fleet

Copied accounts are correlated, not diversified. Each account shall be governed independently using its own realized P&L, open P&L, commissions, fills, account rules, and remaining risk capacity. Fleet summaries may be displayed, but shall not override a stricter per-account decision.

### 2.5 Existing protected positions are handled before new entries

Runtime recovery, broker-position reconciliation, protective-order recovery, and emergency-flatten readiness shall complete before the session supervisor can authorize a new position.

## 3. Runtime architecture

The integration shall introduce the following logical components. Names are design names and may be adjusted during implementation while preserving responsibilities.

### 3.1 `IAccountPnlTelemetrySource`

Provides normalized account-level observations:

- account name;
- session trading date and session identifier;
- gross realized P&L;
- commissions and fees when available;
- net realized P&L;
- open/unrealized P&L for the configured instrument and position;
- net total P&L;
- observation timestamp;
- source sequence or monotonic version;
- data-quality status.

The adapter shall document which values come directly from NinjaTrader and which are reconstructed from execution history. Source behavior shall be proven in Sim101 before production use.

### 3.2 `SessionAttemptLedger`

Tracks completed trade attempts and loss sequence for the approved session. A completed attempt is one governed position lifecycle from opening exposure to confirmed flat state.

The ledger shall record:

- attempt number;
- opening and closing timestamps;
- side and maximum quantity;
- entry and exit execution IDs;
- gross and net realized P&L;
- result classification: win, loss, scratch, or unresolved;
- whether the attempt was the opening opportunity or later opportunity;
- whether cooldown was required and completed;
- the governance decision that authorized entry;
- the reason the position closed.

Partial fills, scale-in fills belonging to the same governed position, and protective-order executions shall not create additional attempts. A reversal that closes one position and opens the opposite side shall be treated as a new attempt only after flat-state confirmation and a new entry authorization.

### 3.3 `DailyGovernanceSessionState`

Holds the minimum durable state required to evaluate and recover the session:

- session identifier and trading date;
- account and instrument identity;
- completed attempts;
- consecutive losses;
- cooldown status and completion time;
- green-day protection activation;
- protected daily floor;
- new-entry lock reason;
- lower-objective and upper-objective status;
- last accepted telemetry version and timestamp;
- last governance decision;
- open governed position correlation, when any.

### 3.4 `DailyPnlRuntimeCoordinator`

Combines authoritative telemetry, the attempt ledger, risk state, setup qualification, position state, and the Daily P&L Governance engine.

The coordinator shall:

1. validate data freshness and identity;
2. reconcile open position and working protection;
3. calculate net realized, open, and total P&L;
4. determine completed attempts and consecutive losses;
5. determine cooldown completion;
6. evaluate the governance engine;
7. publish an immutable decision snapshot;
8. apply the decision to entry permission, risk budget, position supervision, and flatten routing;
9. emit complete telemetry and operator diagnostics.

### 3.5 `GovernedEntryPermission`

All strategy entry paths shall require a current permission token containing:

- account, instrument, session, and decision version;
- `NewEntriesPermitted`;
- maximum planned dollar risk;
- expiration timestamp;
- setup correlation ID;
- governance reason codes;
- confirmation that position and protection reconciliation passed.

The token shall expire quickly and shall be revalidated immediately before order submission. A stale token shall be rejected.

### 3.6 `GovernedPositionActionRouter`

Maps governance decisions to bounded runtime actions:

| Governance decision | Runtime action |
|---|---|
| Monitor | No new order; continue telemetry and normal position supervision |
| Entry eligible | Permit one approved entry within maximum planned risk |
| Cooldown | Block entries until the cooldown authority reports complete |
| Green-day protection, flat | Block ordinary entries; allow only exceptional setup within reduced risk budget |
| Green-day protection, position open | Protect open profit; prohibit additional entries |
| Lower objective reached, qualified runner | Lock new entries; allow existing runner under profit protection |
| Lower objective reached, runner not qualified | Flatten governed position and lock new entries |
| Upper objective reached | Flatten governed position and lock the session |
| Trade or loss limit | Lock new entries for the session |
| Authoritative risk block | Block new exposure and flatten when required by the risk authority |
| Force-flat window | Flatten immediately, cancel remaining working entry orders, and lock the session |

The router shall not widen a stop. Any protective modification shall only reduce risk or lock profit and shall remain valid under the Position Manager and broker truth.

## 4. P&L definitions and calculations

### 4.1 Net realized P&L

The governance value shall be net of commissions and known execution fees:

```text
Net Realized P&L = Gross Realized P&L - Commissions - Known Fees
```

When NinjaTrader provides an authoritative net realized value that has been validated against executions, the adapter may use it directly. Otherwise, the runtime shall reconstruct gross realized P&L from matched executions and subtract observed commissions and fees.

### 4.2 Open P&L

Open P&L shall be calculated from broker-confirmed position side, quantity, average price, current mark, instrument point value, and tick size. The selected mark source shall be documented and stable. A disconnected or stale mark shall invalidate open-P&L-dependent permissions.

### 4.3 Net total P&L

```text
Net Total P&L = Net Realized P&L + Open P&L
```

The lower and upper daily objectives may be reached by an open position. The governance engine shall therefore receive realized and open P&L separately.

### 4.4 Slippage

Actual slippage is reflected in execution prices and therefore in realized P&L. Estimated future slippage shall not be added to current P&L. It shall be included in Lab/backtest evidence and in planned-risk checks before entry.

### 4.5 Scratch classification

A completed attempt whose net result is inside a configurable scratch band shall not automatically count as a consecutive loss. The exact scratch band is a Lab parameter and shall be explicit, versioned, and tested before runtime integration.

## 5. Session identity and reset

The session supervisor shall use the approved New York trading-session definition rather than the computer calendar date alone.

A session reset shall occur only when all of the following are true:

- the prior session is closed or administratively ended;
- no governed position remains open;
- no governed working entry or protective order remains unresolved;
- broker and internal state agree;
- the configured next-session boundary has been crossed.

Restarting NinjaTrader or the ISE runtime shall not reset attempts, losses, green-day protection, or objective lockouts.

## 6. Data-quality and authority rules

Each telemetry observation shall be classified as one of:

- **Healthy** — current, internally consistent, and identity matched;
- **Stale** — older than the approved threshold;
- **Incomplete** — required realized, commission, position, or mark data missing;
- **Inconsistent** — values conflict with executions, position, or prior monotonic state;
- **Recovering** — runtime is rebuilding session state after restart;
- **Rejected** — account, instrument, session, or sequence identity invalid.

Required behavior:

| Data quality | New entries | Open position behavior |
|---|---:|---|
| Healthy | Governed normally | Governed normally |
| Stale | Block | Preserve current protection; escalate if risk cannot be observed |
| Incomplete | Block | Preserve or tighten protection only; no risk increase |
| Inconsistent | Block | Trigger reconciliation; emergency flatten if broker safety cannot be established |
| Recovering | Block | Recover broker position and original protective IDs before other action |
| Rejected | Block | Log critical diagnostic and require operator review |

No telemetry error may increase permitted risk.

## 7. Account and fleet behavior

### 7.1 Per-account evaluation

For every copied account, the runtime shall create an independent governance input and decision. An account at +$500 shall not accept a new copied entry merely because another account is below target. An account at its loss limit shall remain blocked even when the leader account is eligible.

### 7.2 Fleet entry rule

The initial commercial release shall use the strictest safe decision among selected follower accounts:

- the leader order may be routed only to accounts individually eligible;
- ineligible accounts shall be skipped with explicit reason codes;
- the system shall not increase quantity on eligible accounts to compensate for skipped accounts;
- the operator shall see the preflight account-by-account eligibility result before activation of automated fleet routing.

A future configurable all-or-none fleet policy may be evaluated separately, but shall not be assumed in this milestone.

### 7.3 Fleet P&L display

Fleet P&L may be aggregated for reporting, but daily objective and risk decisions remain per account. Fleet worst-day estimates shall treat account outcomes as correlated.

## 8. Restart recovery and persistence

### 8.1 Durable event journal

The runtime shall persist append-only governance events rather than relying only on a mutable state file. Required events include:

- session started;
- telemetry accepted or rejected;
- entry permission issued, expired, or consumed;
- order submitted;
- opening fill applied;
- flat state confirmed;
- attempt completed;
- loss sequence changed;
- cooldown started or completed;
- green-day protection activated;
- lower or upper objective reached;
- entry lock applied or removed;
- force-flat requested and completed;
- recovery started, passed, or failed.

Every event shall include UTC timestamp, account, instrument, session ID, correlation ID, source version, and reason code.

### 8.2 Recovery sequence

After runtime start or replacement:

1. start NinjaTrader event subscriptions;
2. read broker position and working orders;
3. recover original protective stop and target IDs when a position is open;
4. load the latest durable session checkpoint and replay subsequent events;
5. query or reconstruct current session executions and commissions;
6. rebuild realized P&L, attempts, and consecutive losses;
7. compare reconstructed state with platform account values;
8. enter `Recovering` and block new entries until all comparisons pass;
9. evaluate governance using current broker truth;
10. publish `RUNTIME GOVERNANCE RECOVERY PASSED` or fail loudly.

Recovery shall never submit a duplicate protective pair when valid broker-held protection already exists.

### 8.3 Recovery mismatch

A mismatch in session identity, attempt count, realized P&L beyond the approved tolerance, broker position, or protective IDs shall:

- block new entries;
- preserve broker-held protection;
- emit a critical diagnostic containing both values;
- require supervised reconciliation;
- route emergency flatten only when the position cannot be proven safe.

## 9. Integration with existing runtime safety components

The runtime coordinator shall be subordinate to existing broker truth and safety mechanisms:

1. emergency flatten and force-flat authority;
2. authoritative risk engine and prop-account limits;
3. position reconciliation and protection status;
4. Daily P&L Governance decision;
5. setup and entry qualification;
6. order routing.

An entry is permitted only when every layer approves. A block at any higher layer overrides lower-layer approval.

The current protected-fill harness remains a deterministic infrastructure test tool. Its fixed 40-tick stop and 80-tick target shall not be interpreted as the production strategy exit model.

## 10. Operator visibility and diagnostics

The Control Center or equivalent supervised interface shall show, per account:

- session ID and status;
- net realized P&L;
- open P&L;
- net total P&L;
- commissions/fees included status;
- completed attempts and maximum attempts;
- consecutive losses and maximum losses;
- cooldown status;
- governance state;
- maximum permitted new-trade risk;
- protected green-day floor;
- new-entry permission and reason;
- runner continuation status;
- telemetry freshness and source timestamp;
- position/protection reconciliation status;
- last recovery result.

Minimum critical messages:

```text
GOVERNANCE ENTRY PERMITTED
GOVERNANCE ENTRY BLOCKED
GREEN-DAY PROTECTION ACTIVE
LOWER OBJECTIVE REACHED — NEW ENTRIES LOCKED
QUALIFIED RUNNER MAY CONTINUE UNDER PROFIT PROTECTION
UPPER OBJECTIVE REACHED — FLATTEN AND LOCK
FIRST LOSS — COOLDOWN REQUIRED
SECOND LOSS — SESSION LOCKED
P&L TELEMETRY STALE — NEW ENTRIES BLOCKED
P&L RECONCILIATION FAILED
RUNTIME GOVERNANCE RECOVERY PASSED
RUNTIME GOVERNANCE RECOVERY FAILED
```

Diagnostics shall include machine-readable reason codes in addition to human-readable text.

## 11. Supervised Sim101 test environment

All tests in this milestone shall use:

- Sim101 only;
- MNQ 09-26;
- quantity 1 unless a test explicitly requires a different quantity and is separately approved;
- Orders, Positions, Accounts, and Output 1 visible;
- no live-account enablement;
- no unattended automated entry;
- Emergency Flatten immediately available.

Where exact P&L thresholds cannot be reached economically through normal Sim101 movement, the test adapter may inject deterministic telemetry only in a non-production test mode. Injected and platform-derived values shall be visually distinct and impossible to enable in production configuration.

## 12. Supervised test matrix

### T01 — Healthy flat-session eligibility

**Given:** flat, healthy telemetry, zero attempts, no losses, qualified setup, risk authority clear.  
**Require:** `EntryEligible`, one valid permission token, base planned risk, no order submitted by the governance engine itself.

### T02 — Unqualified setup

**Given:** healthy telemetry and all limits clear, but setup not qualified.  
**Require:** `Monitor`, no permission token, no order.

### T03 — First completed loss and cooldown

**Given:** one governed attempt closes net negative.  
**Require:** attempt count 1, consecutive losses 1, cooldown active, entries blocked, exact loss and commissions recorded.

### T04 — Cooldown completion

**Given:** T03 state and the approved cooldown authority reports complete.  
**Require:** a later entry remains blocked unless a new qualified setup exists; no automatic recovery trade.

### T05 — Second consecutive loss

**Given:** a second approved attempt closes net negative.  
**Require:** attempt count 2, consecutive losses 2, `LossLockout`, no additional entries for the session.

### T06 — Two-attempt limit with non-loss outcomes

**Given:** two completed attempts and fewer than two consecutive losses.  
**Require:** `TradeLimitLockout`, proving the attempt limit is independently authoritative.

### T07 — Green-day protection at +$300, no setup

**Given:** flat, net realized P&L at or above the green-day threshold, no exceptional setup.  
**Require:** new entries blocked and protected floor displayed.

### T08 — Green-day exceptional second setup

**Given:** flat, +$300 realized, exceptional setup true, one attempt used.  
**Require:** permission only if planned risk is no greater than realized P&L above the protected floor and no greater than base planned risk.

### T09 — Green-day risk budget exhausted

**Given:** realized P&L equals the protected floor or available amount above the floor is zero.  
**Require:** no new entry even when the setup is exceptional.

### T10 — Lower objective reached while flat

**Given:** net realized P&L at or above +$500 and flat.  
**Require:** `ObjectiveReached`, new entries locked for the session.

### T11 — Lower objective reached with qualified runner

**Given:** open protected position, net total P&L at or above +$500, runner qualified.  
**Require:** no new entries, existing runner allowed to continue, open profit protection active, no forced flatten solely for reaching +$500.

### T12 — Lower objective reached without qualified runner

**Given:** open protected position, net total P&L at or above +$500, runner not qualified.  
**Require:** governed flatten request, new-entry lock, all working protection reconciled through the flatten lifecycle.

### T13 — Upper objective reached

**Given:** net total P&L at or above +$1,000.  
**Require:** immediate flatten when a position is open, cancel remaining working entry orders, lock the session, confirm flat.

### T14 — Force-flat precedence

**Given:** any P&L state and the authoritative force-flat window becomes active.  
**Require:** force-flat overrides runner continuation and entry eligibility.

### T15 — Risk-block precedence

**Given:** setup qualified and P&L otherwise eligible, but authoritative risk block true.  
**Require:** no new exposure and flatten behavior consistent with the risk authority.

### T16 — Stale telemetry while flat

**Given:** P&L observation exceeds the approved freshness threshold.  
**Require:** new entries blocked, stale reason displayed, no attempt or loss counters changed.

### T17 — Stale telemetry with open protected position

**Given:** open protected position and stale P&L telemetry.  
**Require:** original protection preserved, no stop widening, no additional entry, operator warning.

### T18 — Commission reconciliation

**Given:** completed Sim101 execution with observable commission data.  
**Require:** net realized P&L equals gross realized less commissions within defined tolerance; attempt result uses net value.

### T19 — Duplicate execution event

**Given:** the same execution ID is delivered twice.  
**Require:** P&L, quantity, and attempt ledger change exactly once.

### T20 — Out-of-order telemetry

**Given:** a telemetry observation has a sequence/version older than the last accepted observation.  
**Require:** observation rejected, state unchanged, diagnostic emitted.

### T21 — Runtime restart while flat and green

**Given:** +$300 or greater realized, flat, green-day protection active. Stop and restart only ISE runtime.  
**Require:** realized P&L, attempts, protected floor, and entry lock recover unchanged.

### T22 — Runtime restart with protected position

**Given:** open protected position with recorded original stop and target IDs and active governance state. Stop and restart only ISE runtime.  
**Require:** broker position, average price, original protective IDs, P&L state, attempt correlation, and runner status recover without duplicate orders.

### T23 — Runtime restart after first loss during cooldown

**Given:** one completed loss and cooldown not complete. Restart only ISE runtime.  
**Require:** attempt count 1, consecutive losses 1, cooldown remains active, entries blocked.

### T24 — Session-boundary reset

**Given:** prior session closed, flat, no unresolved orders, next approved session begins.  
**Require:** attempts and loss sequence reset only once; durable prior-session audit remains available.

### T25 — Fleet mixed eligibility

**Given:** multiple selected Sim accounts where one is entry eligible, one is at +$500, and one is loss locked.  
**Require:** only the eligible account may receive the copied entry; skipped accounts show explicit reasons; no compensating quantity increase.

### T26 — Telemetry/position contradiction

**Given:** account telemetry indicates flat but broker position is non-zero, or the reverse.  
**Require:** `Inconsistent`, new entries blocked, reconciliation required, protection preserved or emergency flatten used when safety cannot be established.

## 13. Test evidence package

Every supervised test shall capture:

- branch and commit SHA;
- build configuration and exact DLL identity when runtime code is involved;
- NinjaTrader version;
- account, instrument, and session ID;
- pre-test Orders, Positions, Accounts, and Output state;
- input telemetry snapshot;
- governance input and decision snapshot;
- order and execution IDs;
- stop, target, and OCO IDs where applicable;
- post-test broker state;
- pass/fail result and operator notes.

Screenshots support the record but do not replace machine-readable logs.

## 14. Implementation phases

### Phase A — Domain contracts and deterministic tests

- telemetry and state contracts;
- attempt ledger rules;
- data-quality classifier;
- coordinator decision assembly;
- permission token semantics;
- unit tests for all precedence and failure paths.

No NinjaTrader deployment.

### Phase B — Read-only NinjaTrader telemetry adapter

- observe account, execution, commission, position, and mark data;
- compare platform values with reconstructed values;
- log decisions without affecting orders;
- supervised Sim101 telemetry-validation sessions.

No automated entry blocking or flatten routing.

### Phase C — Entry permission enforcement

- require governance permission immediately before all strategy entry orders;
- enforce risk-budget cap;
- prove fail-closed behavior for stale and inconsistent data;
- supervised Sim101 only.

### Phase D — Open-position governance actions

- profit-protection request routing;
- qualified-runner continuation;
- lower-objective non-runner flatten;
- upper-objective and force-flat routing;
- integration with authoritative Position Manager and Emergency Flatten.

### Phase E — Durable recovery

- event journal and checkpoints;
- flat, cooldown, green-day, and open-position restart tests;
- exact preservation of broker-held protective IDs.

### Phase F — Fleet governance

- per-account decisions;
- mixed-eligibility preflight and routing;
- correlated fleet reporting;
- supervised multi-Sim validation.

No phase may activate live accounts. Each phase requires successful local tests, repository validation, explicit review, and supervised acceptance before the next phase.

## 15. Acceptance criteria

The runtime integration milestone is complete only when:

1. P&L is net of observed commissions and reconciles to execution evidence;
2. all strategy entry paths require a fresh governance permission;
3. stale or inconsistent telemetry blocks new entries;
4. completed attempts and consecutive losses remain correct through partial fills and restarts;
5. first-loss cooldown and second-loss lockout are deterministic;
6. +$300 green-day protection preserves the configured floor through planned-risk enforcement;
7. +$500 locks new entries and allows only an already-open qualified runner;
8. +$1,000, force-flat, and authoritative risk blocks take precedence and flatten correctly;
9. runtime restart preserves session governance and original broker-held protection without duplicates;
10. mixed fleet eligibility is enforced per account without compensating size;
11. every decision is auditable with machine-readable reason codes;
12. all supervised tests pass on Sim101 with zero unexplained broker-state discrepancies.

## 16. Explicit non-goals

This milestone shall not:

- claim or optimize a 70-80% win rate;
- expand the approved New York session;
- activate Asia;
- change the opening-drive, deep-pullback, cooldown, or range-resolution entry definitions;
- replace the Position Manager, Protective Order Coordinator, or Emergency Flatten path;
- widen stops or increase size after losses;
- use fleet aggregate profit to override an individual account lockout;
- enable unattended live trading.

## 17. Parameters requiring later Lab approval

The principles above are fixed, but the following values require explicit versioned Lab evidence before production activation:

- telemetry freshness threshold;
- P&L reconciliation tolerance;
- scratch-trade net-P&L band;
- cooldown completion rule;
- protected-profit stop behavior for qualified runners;
- maximum risk permitted for an exceptional green-day second setup;
- session-boundary and administrative-reset procedure;
- all-or-none versus eligible-accounts-only fleet routing beyond the initial safe default.

Changes to these parameters shall be treated as governed configuration revisions, not silent runtime adjustments.
