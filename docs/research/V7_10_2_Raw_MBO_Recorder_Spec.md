# ISE Elite V7.10.2 — Raw MBO / Trade / Depth Research Recorder

**Status:** Research only  
**Branch:** `research/full-session-scalp-engine-v7-9`  
**Production merge:** Forbidden without explicit approval  
**Primary instruments:** MNQ and MGC  
**Gate status:** V7.10.1 capability probe passed live on 2026-08-16 with full MBO New/Change/Delete, exchange order IDs, trade passive/aggressor linkage, and market-depth callbacks.

---

## Objective

Capture raw, replayable market-by-order, trade, and depth evidence without embedding premature trading logic.

The recorder must be **lossless-first, clever-later**. It should preserve enough raw information to reconstruct order-book behavior around later candidate events such as sweeps, absorption, replenishment, liquidity pulling, trapped aggression, exhaustion, continuation, pause/compression, reversal, and flash-shock behavior.

---

## Required live inputs

### MBO
- event type: Snapshot / New / Change / Delete
- exchange order ID
- queue priority when exposed
- side
- price
- volume
- capture UTC timestamp
- exchange/event timestamp when exposed

### Trades
- trade price
- trade quantity
- trade side when exposed
- passive exchange order ID
- aggressor exchange order ID
- capture UTC timestamp
- exchange/event timestamp when exposed

### Market depth / top of book
- depth callback timestamp
- bid/ask side
- price
- size
- update type when exposed
- best bid
- best ask
- spread

### Session metadata
- instrument
- contract
- data provider/source
- local session label
- recorder instance ID

---

## Output format

Recorder should write append-only TSV or CSV streams suitable for offline replay. Preferred first implementation:

- `mbo-<instrument>-<session>.tsv`
- `trades-<instrument>-<session>.tsv`
- `depth-<instrument>-<session>.tsv`
- `health-<instrument>-<session>.tsv`
- `summary-<instrument>-<session>.txt`

Recommended root:

`%USERPROFILE%\OneDrive\Documents\ISEEliteResearch\ATAS\V7.10.2\`

Each recorder instance should isolate its own files so MNQ and MGC can run simultaneously on separate charts.

---

## Performance design

Do not synchronously flush every callback.

Use:

`ATAS callback -> in-memory bounded queue/channel -> dedicated writer task -> append-only buffered files`

Required health telemetry:

- callbacks received by stream
- records queued
- records written
- queue depth / high-water mark
- dropped records
- writer exceptions
- average/max write latency when measurable
- last event time by stream
- periodic heartbeat
- capture gaps

The recorder must make loss visible. It must never silently discard data.

---

## Initial storage principle

Do not aggregate the raw stream yet.

First measure real event volume and file growth during live MNQ/MGC sessions. Only after measuring load should we decide whether later production storage uses raw events, 10/50/100/250 ms summaries, event episodes, or a hybrid model.

---

## Research windows

The recorder itself is not clock-triggered and should remain active across the full research session. Offline labeling should preserve these contextual windows:

- Asia Opening Expansion: approximately 19:00–20:30 CT
- Balance/development: approximately 20:30–23:00 CT
- Overnight transition: approximately 23:00–01:30 CT
- London expansion/runner: approximately 01:00–05:00 CT
- NY opening/transition windows for later comparison

Time is context, not the event trigger.

---

## V7.10.2 acceptance criteria

Before using captured data for strategy research:

1. Recorder loads in ATAS without errors.
2. Live MBO New/Change/Delete records are written continuously.
3. Exchange order IDs are preserved.
4. Trade passive/aggressor IDs are preserved.
5. Market depth is recorded.
6. Best bid/ask/spread are available at useful cadence.
7. No unexplained dropped-record count.
8. Timestamps are monotonic enough for deterministic replay; any clock anomalies are measurable.
9. Files survive a normal ATAS indicator removal/application shutdown without corruption.
10. Two independent instances can capture MNQ and MGC without filename collision.

---

## What V7.10.2 must NOT do

Do not classify or trade:

- sweeps
- absorption
- iceberg behavior
- trapped traders
- trend continuation
- trend reversal
- Vector-Flow-style states
- flash shock

Those are downstream detectors. V7.10.2 records evidence only.

---

## Next stage after successful capture

`Raw capture -> replay/validator -> primitive event detectors -> trend/state engine -> opportunity engine -> account/risk governor -> execution`

The first offline studies should include both MNQ and MGC and should explicitly test continuation, pause/compression, reversal, and sideways/rotation states, with Vector Flow behavior used as a research baseline rather than assumed truth.
