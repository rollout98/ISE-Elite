# ISE Elite Signal Bridge — TradingView to NinjaTrader

VectorFlow computes the signal in TradingView; NinjaTrader executes it. NinjaTrader
does **not** recompute the indicator — a port was tested on 2026-08-12 and fired 11
times where VectorFlow fired 7, consistently 5–65 minutes late.

## Signal file format

Append-only CSV at the path configured on the strategy. One line per signal:

```
id,timestampUtc,instrument,action
a1b2c3,2026-08-12T14:35:00Z,MGC,BUY
d4e5f6,2026-08-12T18:10:00Z,MGC,SELL
```

| Field | Notes |
| --- | --- |
| `id` | Any unique string. Processed once and only once. |
| `timestampUtc` | ISO 8601 UTC. Signals older than `SignalMaxAgeMinutes` are skipped. |
| `instrument` | Prefix-matched against the chart, so `MGC` matches `MGC 12-26`. |
| `action` | `BUY`, `SELL`, or `FLAT`. |

## Behaviour

- **BUY / SELL** — opposite signal closes and reverses; a same-side signal while
  already positioned is ignored ("exit governs entry").
- **FLAT** — closes without re-entering.
- **Stale signals** are marked processed and skipped, so a restart cannot fire a
  backlog of old entries into a live market.

## Getting signals out of TradingView

Manual (for testing): append a line to the file by hand and watch the Output window.

Automated: a TradingView alert with a webhook pointing at a small local listener that
appends to this file. The listener is the remaining piece to build — the strategy side
is complete and testable without it.

## Status

Parameters come from an **in-sample** backtest over Apr–Aug 2026 (74 MGC days). No
out-of-sample test has been run. Sim101 first.
