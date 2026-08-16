# ISE Elite V7.10 — MBO / Microstructure Intelligence Roadmap

**Status:** Research only  
**Branch:** `research/full-session-scalp-engine-v7-9`  
**Production merge:** Forbidden without separate validation and explicit approval  
**Primary research instruments:** MNQ and MGC  
**Primary objective:** Prove that event-driven MBO/order-flow behavior, combined with structural trend/context analysis, can identify repeatable, economically useful trade opportunities before expanding commercial features.

---

## 1. Why V7.10 Exists

V7.9 research showed that arbitrary clock-anchored direction prediction is not robust enough. OHLCV, Last-tick data, and top-of-book Bid/Ask price-update features did not produce reliable out-of-sample direction separability.

The V7.10 pivot changes the problem formulation from:

`Every N minutes -> predict up/down`

into:

`Detect a causal market event -> qualify behavior -> determine market state -> estimate direction/excursion/invalidation -> trade or no-trade`

Time remains contextual information, but market behavior becomes the trigger and ticks become the primary measuring ruler.

The goal is not to predict every move. The goal is to identify a smaller set of high-quality, causally recognizable opportunities from a very large supply of intraday movement.

---

## 2. Current Primary Data Path

Initial research path:

`Rithmic -> ATAS -> ISE Elite MBO Adapter -> ISE normalized market-data interface`

ATAS is currently the primary research adapter because it exposes real-time MBO/order-flow information through its custom indicator API and already provides useful order-flow instrumentation.

### Current V7.10.1 state

`ISE Elite V7.10.1 MBO Capability Probe` has been built successfully against ATAS 8.0.14.396 on `net10.0-windows`.

Current custom indicator DLL path on the development machine:

`C:\Users\dlewi\OneDrive\Documents\ISEEliteATAS\V7.10.1-MBO-Capability-Probe\bin\Release\net10.0-windows\ISEEliteATASMBOProbe.dll`

The probe is installed and loads successfully inside ATAS. The first runtime summary returned zero events because the market was closed; this is not considered a failed capability test.

The first valid capability test must be performed during live futures trading with the Rithmic feed active.

Expected positive capability checks:

- `CAPABILITY_MBO_EVENTS=YES`
- `CAPABILITY_REALTIME_NEW_CHANGE_DELETE=YES`
- `CAPABILITY_EXCHANGE_ORDER_IDS=YES`
- `CAPABILITY_TRADE_ORDER_LINKAGE=YES`

---

## 3. V7.10.2 — Production-Quality Research Recorder

Do **not** build the full Microstructure Engine, Opportunity Engine, Flash Position Manager, or production Risk Governor before seeing the real live MBO stream.

The next required component is the production-quality research recorder.

Purpose: capture enough raw evidence that live sessions can be replayed and analyzed repeatedly after the market closes.

The recorder should capture, with high-resolution timestamps:

- MBO `Snapshot`
- MBO `New`
- MBO `Change`
- MBO `Delete`
- exchange order ID
- queue priority when exposed
- bid/ask/trade side
- price
- quantity
- executed trades
- passive order ID when exposed
- aggressor order ID when exposed
- market-depth updates
- best bid
- best ask
- spread
- enough nearby book state to reconstruct local behavior around events
- instrument
- contract
- session
- source/provider
- capture timestamp
- exchange/event timestamp when exposed
- health statistics:
  - callback count
  - event count
  - write backlog
  - dropped-event count
  - file-write latency
  - capture gaps

### Recorder design principle

**Lossless-first, clever-later.**

The recorder should not prematurely classify sweeps, absorption, icebergs, trend changes, or flash events. It should preserve the raw evidence first. Behavior detectors can then be developed and replayed against the same captured sessions.

If full raw MBO volume is too large, measure the load first before deciding whether production storage should use raw events, 10/50/100/250 ms aggregation, event episodes, or a hybrid approach.

---

## 4. Normalized Market-Data Architecture

ISE should not be hard-coded to ATAS-specific objects.

Create an internal provider-neutral interface such as:

`IMarketDataProvider`

Long-term adapters may include:

- `ATAS MBO Adapter` — first/current primary research adapter
- `Rithmic Direct Adapter` — future
- `Bookmap Adapter` — possible future
- `CME / institutional direct feed adapter` — future if justified

All adapters should normalize into an internal event representation containing fields such as:

- timestamp
- instrument
- event type: Add / Change / Cancel / Execute / Snapshot
- side: Bid / Ask / Trade
- price
- quantity
- order ID
- queue priority
- passive order ID
- aggressor order ID
- source

The core intelligence engine should operate on normalized events rather than vendor-specific structures.

ATAS may remain a permanent commercial dependency if its order-flow intelligence contributes materially to the edge. It should still be treated architecturally as an adapter rather than the identity of ISE itself.

---

## 5. Microstructure Engine — Planned Primitive Events

After the raw recorder is validated, build event detectors from actual captured data.

Candidate primitive events:

- liquidity depletion
- liquidity replenishment
- liquidity pull
- liquidity stacking/addition
- queue depletion
- queue refill
- aggressive buy burst
- aggressive sell burst
- multi-level sweep
- stop-run behavior
- iceberg / replenishing hidden liquidity
- depth imbalance
- cancellation pressure
- trade-through / liquidity vacuum
- abrupt spread widening
- abrupt depth collapse

Do not assume any primitive event is inherently bullish or bearish.

Example continuation candidate:

`Buy sweep + ask depletion + bid replenishment + positive price response`

Example reversal candidate:

`Buy sweep + strong ask replenishment + failure to advance + bid pull`

The system must learn the **sequence and context**, not merely the event name.

---

## 6. Behavioral-State Layer

Primitive events should be combined into higher-level market states.

Candidate states:

- continuation pressure
- pullback continuation
- breakout expansion
- failed breakout
- absorption
- exhaustion
- trapped aggression
- liquidity vacuum
- compression
- expansion
- reversal transition
- VWAP/value displacement and retest
- session-opening expansion
- overnight transition
- London expansion
- NY transition behavior
- post-shock stabilization

The system should classify behavior sequences, not isolated indicators.

Example sequence:

`Sweep -> absorption -> failed price advance -> bid pull -> aggressive selling`

This may become a failed-upside-auction / reversal state.

---

## 7. Event-Driven Entry Design

ISE should stop using minutes as the primary entry trigger.

A practical flow is:

1. Detect meaningful order-book / trade event.
2. Measure immediate response.
3. Require persistence or confirmation.
4. Qualify the current behavioral/trend state.
5. Estimate direction, expected excursion, invalidation distance, and tradability.
6. Trade or output `NO TRADE`.

Ticks are the measuring ruler, not necessarily the final target.

Initial confirmation may use responses such as:

- +1 tick
- +2 ticks
- +4 ticks
- +8 ticks

Then the same event can be measured against larger future excursions:

- 10 ticks
- 20 ticks
- 30 ticks
- 50 ticks
- 75 ticks
- 100+ ticks

Research horizons should include both time and price path, for example:

- 250 ms
- 500 ms
- 1 s
- 2 s
- 5 s
- 15 s
- 30 s
- 60 s
- 5 min
- 15 min
- 30 min
- 60 min
- multi-hour runner horizons where applicable

Every candidate should also be measured for MFE and MAE.

`NO TRADE` is a first-class valid outcome.

---

## 8. Time-of-Day Context Engine

Time of day should be a **context and normalization layer**, not a blind entry trigger.

ISE should learn what is normal for each time bucket and session regime, including:

- trade rate
- MBO update rate
- cancellation rate
- resting depth
- spread
- sweep frequency and size
- price velocity
- realized volatility
- typical 1s/5s/30s excursion
- typical pullback depth
- absorption frequency
- liquidity replenishment behavior
- expected MFE/MAE

A 15-tick move or 500-contract burst can mean something very different at 8:30 AM CT than at 12:45 PM CT. Event thresholds should therefore be normalized by instrument, time of day, and market regime.

### Two clocks

ISE should ultimately maintain:

**Ordinary clock context**
- time of day
- day of week
- holiday/session type
- scheduled event proximity

**Market clock context**
- quiet
- building
- expansion
- exhaustion
- transition
- compression
- shock
- recovery

The ordinary clock informs expectancy. The market clock informs current auction state.

---

## 9. Primary Session Research Regimes

Recent observations and examples make the overnight session a first-class research target.

### 9.1 Asia Opening Expansion — 19:00–20:30 CT

Observed behavior:

- often repeatable several times per week
- generally less erratic than the 08:30 AM U.S. opening period
- can produce substantial two-sided movement
- initial impulse may be followed by pullback and a larger second leg
- may contain more than one independent opportunity

Research questions:

- first 5/15/30/60/90-minute excursion
- initial impulse size
- pullback depth/duration
- continuation vs reversal frequency
- MBO behavior during the pullback
- second-leg trigger behavior
- session opportunity count
- MFE/MAE and friction-adjusted expectancy

This is a candidate **primary scalp/equity-building playbook**.

### 9.2 Overnight Transition — approximately 23:00–01:00/01:30 CT

Observed behavior in both MNQ and MGC examples:

- earlier rotation/balance may transition into a sustained directional state
- price/body relationship to structural curves changes
- faster structure may begin separating from slower structure
- retests may fail to reclaim the prior side
- directional movement can persist for multiple hours

Research hypothesis:

> The overnight market may frequently transition from an earlier rotational auction into a persistent directional state that can be identified by structural trend behavior plus MBO confirmation.

### 9.3 London Expansion / Runner — approximately 01:00–04:00/05:00 CT

This is the candidate longer-hold phase following a qualified overnight transition.

Research should measure:

- 30/60/120/180/240+ minute MFE/MAE
- trend persistence
- pullback survivability
- MBO support for continuation
- exhaustion timing
- whether exit should be target-based, state-based, or core-plus-runner

### 9.4 U.S. Opening Expansion — approximately 08:30 CT onward

Observed behavior:

- high-energy repricing
- often erratic initial burst
- significant excursion potential
- pullback/second-phase entry may be cleaner than chasing the first move

Research should explicitly compare U.S. opening behavior to Asia on:

- signal quality
- MAE
- false-start rate
- execution risk
- spread/slippage
- expected excursion

New York is an opportunity source, not a mandatory trading session when the daily objective has already been met.

---

## 10. Instrument Priority and Expansion

ISE should not be permanently restricted to NQ/MNQ.

### Current priority

**MNQ and MGC are both primary research instruments.**

MNQ remains valuable for high-frequency microstructure research and known Asia/NY behavior.

MGC deserves equal primary status because the user's strongest recent discretionary results were generated in Gold during the Asia-to-London window.

### Planned expansion order after the core engine is proven

1. MNQ/NQ
2. MGC/GC
3. MES/ES as an independent transfer/generalization test
4. MCL/CL later
5. YM/MYM and RTY/M2K if justified

Behaviors should be universal where possible, while thresholds and expected excursion should be instrument-normalized.

Long-term, cross-market intelligence may compare related markets, but that should come after single-instrument causal behavior is validated.

---

## 11. ISE Trend State Engine — Vector Flow Functional Replication

Vector Flow has been a primary discretionary tool for identifying overnight trending behavior in MNQ/MGC. ISE should replicate the **function** of that trend identification rather than merely copying its visual appearance.

Candidate structural features:

- price acceptance above/below trend structure
- fast/medium/slow curve slope
- slope acceleration/deceleration
- curve compression and separation
- repeated crossing versus sustained acceptance
- pullback interaction with the structure
- failed retests
- trend efficiency
- distance from structural baseline

Candidate state machine:

`ROTATION / NO TREND`

`-> TREND FORMING`

`-> TREND CONFIRMED`

`-> TREND EXPANSION`

`-> HEALTHY PULLBACK`

`-> TREND RESUMPTION`

`-> EXHAUSTION`

`-> TREND FAILURE / REVERSAL`

### Structural trend + MBO

The intended improvement over a curve-only indicator is to combine visible price structure with auction behavior underneath it.

Example bullish trend confirmation:

- price above structure
- curves rising/separating
- ask depletion strong
- bid replenishment strong
- buy aggression persistent
- sell absorption weak
- price response efficient

Example trend exhaustion while curves are still visually bullish:

- aggressive buying remains high
- ask replenishment becomes strong
- price progress weakens
- bids begin pulling
- upside sweeps stop producing acceptance

This may allow ISE to detect exhaustion earlier than a purely lagging structural indicator.

---

## 12. Trend Change vs Pause vs Sideways Classification

The Trend State Engine + MBO layer should explicitly distinguish:

- `TREND CONTINUATION`
- `TREND PAUSE / CONSOLIDATION`
- `TREND REVERSAL FORMING`
- `SIDEWAYS / ROTATIONAL MARKET`

### Healthy pause / compression

Possible characteristics:

- slower trend structure remains intact
- temporary curve flattening/compression
- pullbacks fail to gain opposite-side acceptance
- defending-side liquidity replenishes
- countertrend aggression is absorbed
- countertrend sweeps produce little progress

Desired output:

`TREND ACTIVE + PAUSE/COMPRESSION + REVERSAL RISK LOW -> HOLD / WAIT FOR RESUMPTION`

### Reversal forming

Possible characteristics:

- trend slope/separation deteriorates
- defending liquidity pulls
- opposing aggression increases
- attempts to reclaim prior structure fail
- price begins accepting on the opposite side

Desired transition:

`TREND -> PAUSE -> TREND UNDER PRESSURE -> REVERSAL CANDIDATE -> REVERSAL CONFIRMED`

### Sideways / rotational market

Possible characteristics:

- repeated crossings of structural curves
- low directional efficiency
- two-sided replenishment/absorption
- sweeps repeatedly fail
- symmetric MFE/MAE
- no sustained acceptance away from value

Desired output:

`ROTATION -> NO TREND TRADE`

The ability to say `NO TREND / NO TRADE` is a required capability.

---

## 13. Two-Stage Overnight Trading Model

The observed discretionary model should be represented explicitly in research.

### Stage 1 — Asia scalp/equity build

Approximate window:

`19:00–20:30 CT`

Objective:

- capture repeatable shorter opportunities
- build realized equity/cushion
- prioritize consistency and manageable MAE

### Stage 2 — Overnight runner

Approximate setup-development window:

`23:00–01:00 CT`

Potential hold window:

`through 04:00–05:00 CT if structure remains valid`

Objective:

- identify the cleaner directional transition
- enter only when the runner setup qualifies independently
- allow larger multi-hour excursion when trend and MBO remain supportive

Earlier scalp profit may affect the **risk budget**, but it must never manufacture a runner entry.

Conceptual rule:

`Asia builds equity -> overnight setup must independently qualify -> qualified runner may compound the cushion`

---

## 14. Opportunity Engine

After primitive events, trend states, and behavioral states are validated, the Opportunity Engine should determine:

- whether a setup is tradable
- likely direction
- confidence
- expected excursion
- expected adverse excursion
- stop geometry
- target geometry
- likely continuation versus reversal
- whether to wait
- whether to reject the setup
- whether the appropriate lifecycle is scalp, expansion, or runner

Candidate playbooks may include:

- Asia opening scalp
- session-opening expansion
- controlled pullback continuation
- liquidity-sweep reversal
- absorption reversal
- compression breakout
- failed breakout
- overnight transition runner
- London expansion runner
- exhaustion transition
- post-flash continuation
- post-flash reversal

Do not build or promote a playbook merely because it is visually intuitive. Every playbook must earn promotion through statistical validation and realistic execution economics.

---

## 15. Flash-Shock / Market-Dislocation Detection

A mature ISE system should include a dedicated abnormal-market detector for rare flash events and severe shocks.

Candidate hierarchy:

- NORMAL
- ELEVATED_VOLATILITY
- NEWS_EVENT_VOLATILITY
- LIQUIDITY_STRESS
- FLASH_SHOCK
- MARKET_DISLOCATION

Possible detection inputs:

- extreme price velocity
- trade-rate acceleration
- rapid multi-level sweeps
- depth collapse near market
- cancellation-rate spike
- liquidity pull
- spread widening
- execution/slippage deterioration
- book gaps / liquidity vacuums
- volatility far outside the current adaptive baseline

Thresholds should be adaptive by instrument, time of day, and market regime.

### Default action when flat

- block new normal entries
- cancel unfilled discretionary entry/add-on orders
- monitor stabilization
- do not chase the initial shock
- allow new trading only after the book normalizes sufficiently for a validated post-shock playbook

---

## 16. Position Handling During a Flash Shock

If ISE is already in a trade when a flash shock occurs, control should move immediately from normal opportunity management to survival/risk management.

### Normal Trade Manager

Handles ordinary:

- stops
- targets
- runners
- break-even
- trailing

### Flash Position Manager

Has override authority during abnormal market structure.

Possible reactions:

**Manageable shock**
- stop adding
- cancel unfilled discretionary orders
- aggressively protect open profit
- continue only if liquidity and execution remain functional

**Severe shock**
- override normal trade thesis
- flatten position when market structure becomes unsafe

**Extreme dislocation**
- emergency exit / catastrophic risk response
- assume normal stop prices may not be achievable because liquidity can disappear

Being aligned with the shock and being opposed to the shock should be treated differently, but survival remains the priority.

### Catastrophic Risk Governor

Final authority above normal strategy logic and flash management.

If account loss, slippage, spread, connectivity, or book instability crosses emergency thresholds:

`EXIT FIRST -> DIAGNOSE AFTERWARD`

Protective stop orders should reside outside the local ISE process whenever possible so a local application or workstation failure does not leave the position completely unprotected.

---

## 17. Market Shock vs Data-Feed Failure

ISE must distinguish a genuine market dislocation from corrupted or failed market data.

Potential anomaly checks:

- one feed reports extreme move while another does not
- irregular timestamps
- depth freezes while price jumps
- trades stop unexpectedly
- sequence numbers / event cadence break
- spread or price becomes impossible relative to reference feeds

Long-term multiple adapters may allow cross-provider confirmation:

`Multiple independent sources confirm shock -> market event`

versus:

`One source reports shock, others do not -> feed anomaly -> DO NOT TRADE`

---

## 18. Capital Cushion Governor

The nominal prop-account size is **not** the true usable risk budget. The important variable is the distance between current equity and the account's applicable drawdown/failure threshold.

For a nominal 50K account with a $2,500 drawdown, the initial economic risk envelope is approximately the $2,500 drawdown allowance, not $50,000.

ISE should track:

`Current equity - current failure/liquidation threshold = TRUE RISK CUSHION`

Candidate capital stages:

- `SURVIVAL` — small cushion; smallest sizing
- `BUILD` — cushion accumulating; conservative sizing
- `ESTABLISHED` — larger buffer; moderate sizing
- `CAPITALIZED` — approximately 2x–3x original drawdown cushion; larger qualified sizing may be permitted
- `PROTECT / PAYOUT` — account objective met; reduce/stop according to policy

A withdrawal is also a risk event. If a payout materially reduces the cushion, ISE should automatically downgrade the account's risk tier.

Position size should therefore be a function of:

`Opportunity quality + account cushion + drawdown state + account objective`

not simply nominal account size.

---

## 19. Drawdown-Model Awareness

The Account Governor must distinguish account types with materially different drawdown mechanics.

User operational model currently distinguishes:

- TPT combine/test style: end-of-day trailing behavior
- TPT funded style: trailing behavior can matter intraday until the drawdown is effectively covered/fixed

These firm rules are externally changeable and **must be verified against current official rules before production deployment**.

The architecture should support fields such as:

- firm
- account type
- drawdown type
- drawdown amount
- current liquidation/failure threshold
- distance to threshold
- drawdown covered: true/false
- capital cushion
- risk tier
- daily/group objective
- payout policy

Candidate funded-account state machine:

`TRAILING-DD EXPOSED -> DRAWDOWN COVERED -> CAPITALIZED -> PAYCHECK/GROWTH`

The exact transition logic must be provider-specific and configuration-driven.

---

## 20. Account-Specific Economic Governors

The same qualified market opportunity may be expressed differently by different accounts.

Architecture:

`Market opportunity -> ISE intelligence -> trade candidate -> account-specific governor -> execution`

Current user economic model to preserve for research/design:

### TPT group

- up to 5 target accounts in the intended model
- desired gross group objective: approximately $10,000/day across the group
- user expects an 80/20 payout model for the applicable account type; verify current firm terms before production

### Other accounts

- target approximately $500–$1,000/day per account
- stop or reduce activity when that account objective is met

The Account Governor must separate:

**What is the market offering?**

from:

**What does this particular account need and safely permit?**

A high-quality runner may therefore be held in a well-capitalized growth account while a daily-income account exits after reaching its objective.

---

## 21. Prop-Firm Rules / Capacity Layer — Later Scaling Phase

The user maintains a separate futures prop-firm directory intended to map a large potential account fleet (currently estimated by the user at up to 211 account slots across firms).

Repository:

`rollout98/Futures-prop-firms-directory`

This should eventually become an input to an **ISE Prop-Firm Rules / Capacity Engine** containing provider-specific data such as:

- maximum account count
- account types
- drawdown model
- payout rules
- consistency rules
- copying/automation restrictions
- platform/feed requirements
- current eligibility/status

The trading edge remains independent of this layer.

Future flow:

`Qualified trade -> Account Governor -> Prop-Firm Rules/Capacity Engine -> participating accounts -> execution`

Provider rules can change. This layer must be configuration-driven, versioned, and independently maintained rather than hard-coded into strategy logic.

---

## 22. Apex Status — Do Not Infer

The user reports that new Apex accounts are currently unavailable to them and has opened four support tickets seeking clarification.

Apex has **not** told the user that the restriction is related to prior profitability or any specific reason.

Therefore:

- do not assume a cause
- do not design around speculation
- keep Apex new-account capacity status as `UNRESOLVED`
- update the account-capacity model only when an actual response/rule is available

---

## 23. LLM / Cognitive Intelligence — Later Phase Only

An LLM is **not** part of the direct order-entry path during current research.

The direct path should remain deterministic/specialized:

`MBO -> Microstructure Engine -> Trend/Event State Engine -> Statistical/ML Opportunity Model -> Risk Governor -> Execution`

A later cognitive layer may consume compressed state vectors rather than raw MBO firehose data.

Potential later uses:

- regime interpretation
- behavior-sequence interpretation
- playbook selection
- anomaly recognition
- diagnostics
- post-session research hypothesis generation
- explanation of why a setup was accepted/rejected

Do not use:

`Raw MBO -> LLM -> BUY/SELL`

The LLM is an enhancement after the deterministic core is stable, not a substitute for an unfinished trading engine.

---

## 24. Deployment Architecture — Later Production Phase

Research can continue on the laptop, but the intended production environment is a dedicated physical Windows trading workstation.

Primary production responsibilities may include:

- Rithmic
- ATAS
- ISE MBO/Microstructure Engine
- ISE Trend State Engine
- ISE Opportunity Engine
- Account/Capital Cushion Governor
- Risk Governor
- NinjaTrader execution

The laptop becomes a monitoring/development station for:

- dashboards
- logs
- research
- emergency control
- GitHub/development

Cloud systems such as AWS/Azure may later be used for:

- analytics
- storage
- model training
- reporting
- backups
- optional cognitive/LLM services

A generic VPS is not assumed to be the primary production environment because unexpected provider maintenance/reboots and application shutdowns are unacceptable for the live execution path.

Protective exits should be made as resilient as possible to local workstation/application failure.

---

## 25. Commercialization — Deferred Until Edge Is Proven

Commercial architecture is secondary to proving profitable trade quality.

Potential future commercial concerns:

- ATAS license requirements for end customers
- Rithmic/data entitlements
- market-data licensing
- multiple provider adapters
- partner/OEM arrangements
- licensing/entitlement system
- customer installation
- account-specific governors
- prop-firm rules/capacity integration
- supportability
- cloud analytics
- commercial dashboards
- LLM cognitive features

Current principle:

**Do not spend major engineering effort rebuilding ATAS or commercial infrastructure before proving that MBO/order-flow intelligence materially improves trade expectancy.**

---

## 26. Live and Scaling Timeline

Current planning target:

### End of September 2026

Controlled live pilot, not forced mature production:

- one instrument/account initially or similarly small live scope
- strict risk limits
- full logging
- kill switch
- live fills/slippage compared against research assumptions
- no scaling unless forward validation supports it

### October–December 2026

Controlled scaling only if the live edge and operations remain stable.

Possible progression:

`1 -> 5 -> 10–20 -> 25–50 -> larger fleet`

Year-end ambition may extend toward approximately 100 accounts, but account count is a consequence of validated profitability, account availability, firm rules, execution stability, and risk capacity—not a calendar obligation.

---

## 27. Immediate Research Sequence

### Before/at the next live session

1. Keep V7.10.1 capability probe installed and ready.
2. Build/finish V7.10.2 production-quality research recorder.
3. Verify the recorder writes valid files and health statistics.
4. Connect live Rithmic/ATAS market data.
5. Confirm V7.10.1 live MBO/trade/depth capability.
6. Capture raw MNQ and MGC sessions where practical.

### After first valid captures

1. Inspect actual event structure and event rate.
2. Validate timestamp quality and field completeness.
3. Determine whether raw capture is sustainable.
4. Begin primitive-event detector research.
5. Measure candidate events against future MFE/MAE and price response.
6. Build time-of-day/session normalization.
7. Build Vector-Flow-style structural trend features.
8. Test trend continuation vs pause vs reversal vs rotation classification.
9. Build behavioral-state combinations only after primitive detectors are trustworthy.
10. Test Asia Opening Expansion and Overnight Transition/Runner playbooks first.
11. Compare MNQ and MGC for shared versus instrument-specific behavior.
12. Build Opportunity Engine only after profitable state transitions are demonstrated.
13. Add Capital Cushion / Account Governors and Flash/Risk engines after the opportunity/execution behavior is understood.
14. Add commercial features and LLM cognitive layer only after core expectancy is proven.

---

## 28. Non-Negotiable Research Principles

- Research branch only.
- No production merge without separate validation and explicit approval.
- Evidence first; architecture second.
- Do not optimize around hindsight-only clean labels.
- Include failed events and no-trade events in research datasets.
- Validate out of sample.
- Include realistic friction, slippage, latency, and non-overlapping execution before calling an edge tradable.
- Quantity changes dollars captured, not the underlying win-rate requirement.
- Time of day is contextual; events are primary triggers.
- Ticks measure response; ticks are not automatically the final target.
- A visually compelling order-flow pattern is not a trading edge until it survives validation.
- A visually compelling Vector Flow/trend pattern is a research baseline, not proof by itself.
- Structural trend and MBO should be evaluated both independently and together.
- `NO TRADE` and `NO TREND` are valid outputs.
- Earlier-session profits may influence risk budget but must not manufacture a later entry.
- Nominal account size is not the same as usable risk capital.
- Position sizing must respect current drawdown distance/capital cushion.
- Provider account rules are configuration inputs and must be verified before production.
- Survival/risk logic has authority to override normal trade logic during abnormal market states.
- Daily objective reached can be a valid reason to stop trading even when later opportunities exist.

---

## 29. Current Primary Goal

The current goal is deliberately focused:

> **Determine which combinations of real MBO/order-flow behavior, structural trend state, and session context consistently precede tradable MNQ/MGC movement, and under what conditions ISE should scalp, hold a runner, stay out, continue holding, reduce, reverse, or exit.**

The immediate priority remains profitable trade quality and correct execution. Multiple adapters, commercial packaging, account-fleet scaling, cloud services, and LLM intelligence follow only after that core capability is proven.
