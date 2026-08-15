# ISE Elite V7.10 — MBO / Microstructure Intelligence Roadmap

**Status:** Research only  
**Branch:** `research/full-session-scalp-engine-v7-9`  
**Production merge:** Forbidden without separate validation and explicit approval  
**Primary instrument:** MNQ  
**Primary objective:** Prove that event-driven MBO/order-flow behavior can identify repeatable, economically useful trade opportunities before expanding commercial features.

---

## 1. Why V7.10 Exists

V7.9 research showed that arbitrary clock-anchored direction prediction is not robust enough. OHLCV, Last-tick data, and top-of-book Bid/Ask price-update features did not produce reliable out-of-sample direction separability.

The V7.10 pivot changes the problem formulation from:

`Every N minutes -> predict up/down`

into:

`Detect a causal market event -> qualify behavior -> determine direction -> estimate excursion/invalidations -> trade or no-trade`

Time remains contextual information, but market behavior becomes the trigger and ticks become the primary measuring ruler.

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

The first valid capability test must be performed during live MNQ trading with the Rithmic feed active.

Expected positive capability checks:

- `CAPABILITY_MBO_EVENTS=YES`
- `CAPABILITY_REALTIME_NEW_CHANGE_DELETE=YES`
- `CAPABILITY_EXCHANGE_ORDER_IDS=YES`
- `CAPABILITY_TRADE_ORDER_LINKAGE=YES`

---

## 3. Sunday-Readiness Requirement

Do **not** build the full Microstructure Engine, Opportunity Engine, Flash Position Manager, or production Risk Governor before seeing the real live MBO stream.

Before Sunday evening, the required additional component is:

## V7.10.2 — Production-Quality Research Recorder

Purpose: capture enough raw evidence that the session can be replayed and analyzed repeatedly after the market closes.

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
- health statistics
  - callback count
  - event count
  - write backlog
  - dropped-event count
  - file-write latency
  - capture gaps

### Recorder design principle

**Lossless-first, clever-later.**

The Sunday recorder should not prematurely classify sweeps, absorption, icebergs, or flash events. It should preserve the raw evidence first. Behavior detectors can then be developed and replayed against the same captured session.

If full raw MBO volume is too large, measure the load first before deciding whether production storage should use raw events, 10/50/100/250 ms aggregation, event episodes, or a hybrid approach.

---

## 4. Normalized Market-Data Architecture

ISE should not be hard-coded to ATAS-specific objects.

Create an internal provider-neutral interface such as:

`IMarketDataProvider`

Long-term adapters may include:

- `ATAS MBO Adapter` — first / current primary research adapter
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

Examples:

`Buy sweep + ask depletion + bid replenishment + positive price response`

may indicate continuation.

But:

`Buy sweep + strong ask replenishment + failure to advance + bid pull`

may indicate absorption/trapped buying and a reversal candidate.

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
4. Qualify the current behavioral state.
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

and future price movement / MFE / MAE measured in ticks.

`NO TRADE` is a first-class valid outcome.

---

## 8. Opportunity Engine

After primitive events and behavioral states are validated, the Opportunity Engine should determine:

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

Candidate playbooks may include:

- continuation expansion
- controlled pullback continuation
- liquidity-sweep reversal
- absorption reversal
- compression breakout
- failed breakout
- exhaustion transition
- post-flash continuation
- post-flash reversal

Do not build or promote a playbook merely because it is visually intuitive. Every playbook must earn promotion through statistical validation and realistic execution economics.

---

## 9. Flash-Shock / Market-Dislocation Detection

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

Thresholds should be adaptive to the current market regime rather than fixed constants only.

### Flash-shock default action when flat

- block new normal entries
- cancel unfilled discretionary entry/add-on orders
- monitor stabilization
- do not chase the initial shock
- allow new trading only after the book normalizes sufficiently for a validated post-shock playbook

---

## 10. Position Handling During a Flash Shock

If ISE is already in a trade when a flash shock occurs, control should move immediately from normal opportunity management to survival/risk management.

Planned hierarchy:

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

Example:

`Long position + shock direction DOWN + bid collapse + spread widening + accelerating sell sweeps -> normal stop logic may be overridden -> flatten`

### Catastrophic Risk Governor

Final authority above normal strategy logic and flash management.

If account loss, slippage, spread, connectivity, or book instability crosses emergency thresholds:

`EXIT FIRST -> DIAGNOSE AFTERWARD`

Protective stop orders should reside outside the local ISE process whenever possible so a local application or workstation failure does not leave the position completely unprotected.

---

## 11. Market Shock vs Data-Feed Failure

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

## 12. LLM / Cognitive Intelligence — Later Phase Only

An LLM is **not** part of the direct order-entry path during current research.

The direct path should remain deterministic / specialized:

`MBO -> Microstructure Engine -> Event/State Engine -> Statistical/ML Opportunity Model -> Risk Governor -> Execution`

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

The LLM should make the system easier to interpret and potentially improve higher-level context after the core system is already proven.

---

## 13. Deployment Architecture — Later Production Phase

Research can continue on the laptop, but the intended production environment is a dedicated physical Windows trading workstation.

Primary production responsibilities may include:

- Rithmic
- ATAS
- ISE MBO/Microstructure Engine
- ISE Opportunity Engine
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

Cloud is not required for synchronous local protection or flatten logic.

A generic VPS is not assumed to be the primary production environment because unexpected provider maintenance/reboots and application shutdowns are unacceptable for the live execution path.

---

## 14. Commercialization — Deferred Until Edge Is Proven

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
- supportability
- cloud analytics
- commercial dashboards
- LLM cognitive features

Current principle:

**Do not spend major engineering effort rebuilding ATAS or commercial infrastructure before proving that MBO/order-flow intelligence materially improves trade expectancy.**

ATAS may remain a permanent commercial dependency if its order-flow capabilities contribute materially to the edge and replacing them does not improve the product enough to justify the engineering cost.

---

## 15. Immediate Research Sequence

### Before Sunday evening

1. Keep V7.10.1 capability probe installed and ready.
2. Build V7.10.2 production-quality research recorder.
3. Verify the recorder writes valid files and health statistics.

### Sunday live session

1. Connect live MNQ/Rithmic.
2. Run V7.10.1 capability probe long enough to confirm live MBO/trade/depth access.
3. Run V7.10.2 recorder during live trading.
4. Preserve the raw session without introducing premature event assumptions.

### After first valid capture

1. Inspect actual event structure and rate.
2. Validate timestamp quality and field completeness.
3. Determine whether raw capture is sustainable.
4. Begin primitive-event detector research.
5. Measure every candidate event against future MFE/MAE and price response.
6. Build behavioral-state combinations only after primitive detectors are trustworthy.
7. Build Opportunity Engine only after profitable state transitions are demonstrated.
8. Add Risk/Flash engines after we know what opportunity/execution behavior must be protected.
9. Add commercial features and LLM cognitive layer only after core expectancy is proven.

---

## 16. Non-Negotiable Research Principles

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
- Survival/risk logic has authority to override normal trade logic during abnormal market states.

---

## 17. Current Primary Goal

The current goal is deliberately narrow:

> **Determine which combinations of real MBO/order-flow behavior consistently precede tradable MNQ movement, and under what conditions ISE should enter, stay out, continue holding, or exit.**

Everything else follows from proving that core capability.