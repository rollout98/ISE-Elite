# ISE Elite — Session 4 Checkpoint & Next Session Outline

**Date:** Aug 11, 2026 (~9:45 AM CT)
**Status:** VectorFlow signal backtest pipeline READY for MNQ/MGC dual testing
**All code:** Pushed to `rollout98/ISE-Elite` main branch

---

## What Happened in Session 4

### The Problem
You kept asking "why is the system ignoring the daily trend?" and I kept fixing the wrong thing. The root cause: **the engine could only see 20 minutes of history** (20-bar rolling buffer). A 20-minute view cannot represent a daily trend, so it fired on every 3-minute wiggle — **72 trades/day hunting for the one that mattered.**

### The Solution
1. **Widen memory:** 20 → 480 bars (8 hours of context)
2. **Trend filter:** Gate 1-minute entries to agree with a slow-moving average
3. **Real point values:** MNQ $2/pt, MGC $100/pt (auto-detected)
4. **VectorFlow signal loader:** Parse your TradingView CSV exports; use YOUR signals instead of invented ones
5. **Trailing stops:** Ride trends instead of capping exits at fixed targets

### Result
Built the infrastructure to answer the right question: **Does your actual mechanical method (hold to reversal, VectorFlow entry, mechanical discipline) produce $1k/day on real data?**

---

## What You Need to Do Next Session

### Quick Reference: Three Steps to Run Both Backtests

#### Step 1: Get MGC Data (5 min in NinjaTrader)
```
1. Open NinjaTrader 8
2. Load or create an MGC1! chart
3. Run ISEEliteNewYorkMultiMonthDatasetProbe (same tool as MNQ)
4. Wait for: ny-MGC-*.tsv
```

#### Step 2: Export Signals from TradingView (10 min each)
```
MNQ1! 5-minute chart
├─ Add VectorFlow Algo V1-S
├─ Right-click → Export data → Save as vectorflow-MNQ.csv

MGC1! 5-minute chart
├─ Add VectorFlow Algo V1-S (or tuned for gold)
├─ Right-click → Export data → Save as vectorflow-MGC.csv
```

Both CSVs should have: `time, open, high, low, close, volume, ..., BUY, SELL`

#### Step 3: Run Backtests (90 sec each)
```powershell
cd C:\Users\dlewi\Documents\ISE-Elite
git pull origin main

# MNQ
$env:ISE_DATASET = "C:\Users\dlewi\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-09-26-*.tsv"
$env:ISE_SIGNALS = "C:\temp\vectorflow-MNQ.csv"
dotnet run --project tools/BacktestConsole.csproj

# MGC
$env:ISE_DATASET = "C:\Users\dlewi\Documents\NinjaTrader 8\ISEEliteResearch\ny-MGC-*.tsv"
$env:ISE_SIGNALS = "C:\temp\vectorflow-MGC.csv"
dotnet run --project tools/BacktestConsole.csproj
```

---

## What to Look For in Results

Each backtest prints results to console and exports a CSV in `results/[timestamp]/`.

### Critical Metrics

**1. Trades/day** (printed per config under "Loaded {count} signal records")
- **Expected:** ~5–10 trades/day (matching your 7 fires / 19 hours on the chart)
- **Red flag:** 40+ = CSV load failed
- **Bad sign:** 0 or 1 = signal is too selective

**2. Top 20 Results (sorted by Sharpe ratio)**
- Look at **Gross Profit** and **Return%**
- Over 44 days, target ~$44k gross (= $1k/day)
- Is it there or close?

**3. Direction Balance**
- Do LONG and SHORT split ~50/50?
- Lopsided = potential sign error or one-directional trend in data

**4. Max Drawdown (MaxDD)**
- Typical: $5k–$20k
- Over $30k = trade structure not holding risk well

### Interpreting the Gap

| Result | Meaning |
|--------|---------|
| MNQ matches $1k/day, MGC doesn't | VectorFlow stronger on one contract; check signal quality |
| Both match | Mechanical method works; ready to deploy live |
| Both underperform by 50% | Hold duration or trend filter are too conservative; adjust sweep |
| Both way under | Signal quality issue; review TradingView exports |

---

## Key Files to Know

| File | Purpose |
|------|---------|
| `RUN_VECTORFLOW_BACKTEST.md` | Full walkthrough (read this first next session) |
| `src/ISE.BacktestHarness/VectorFlowSignalLoader.cs` | Parses TradingView CSV |
| `src/ISE.BacktestHarness/InstrumentSpecs.cs` | MNQ $2/pt, MGC $100/pt lookup |
| `src/ISE.BacktestHarness/Engines/BacktestExecutionEngine.cs` | Core engine (now takes external signals) |
| `tools/BacktestConsole.cs` | Entry point (wires signals to orchestrator) |
| `results/` | Output CSVs per run |

---

## Session 4 Commits (Latest First)

```
41adbbe  Add MNQ/MGC backtest runner: documentation
094a701  Support MNQ and MGC: instrument-agnostic point values
a704082  Wire VectorFlow CSV signal loader into backtest harness
1295991  Widen engine memory 20->480 bars and add higher-timeframe trend filter
2975446  Add trailing-stop exit mode and widen sweep to 250pt targets
0f7ec1b  Sweep max-hold duration
6c3d483  Add short-side trading
f55df1a  Fix costs: $0.50 slip + $0.37 comm (not $10/side)
```

All merged to main and pushed.

---

## Why This Matters

**Old approach:** Build a new strategy, backtest it, hope it works.
**New approach:** Test the strategy you're already trading. If mechanical matches discretionary, you have a deployable system. If not, you know the gap is discretion, not the signal.

Your live P&L is ~$1k/day ($500 Asia, $500 NY). The harness will tell you whether holding to reversal mechanically gets there or how much of it is timing/feel.

---

## One-Liner to Start

```powershell
cd C:\Users\dlewi\Documents\ISE-Elite && git pull origin main && cat RUN_VECTORFLOW_BACKTEST.md
```

Then follow the three steps above.

---

## Questions to Answer Next Session

1. Does MGC probe produce ~56k bars like MNQ?
2. Can you export both MNQ and MGC with VectorFlow labels from TradingView?
3. Do both backtests show single-digit trades/day (signal loaded)?
4. Does P&L come close to $1k/day on either or both?
5. If not: which direction (MNQ/MGC/signal quality/hold duration) to adjust?

---

**Last commit:** 41adbbe (Aug 11, 9:45 AM CT)  
**Next action:** MGC data probe → TradingView exports → Run tests
