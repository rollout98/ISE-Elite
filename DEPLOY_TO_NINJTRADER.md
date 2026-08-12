# Deploy ISEEliteVectorFlow_Live to NinjaTrader

**Status:** Strategy built and committed to repo. Ready for deployment.

**Commit:** 6eafe29

---

## What's Been Built

### Strategy File
```
ninjatrader/ISEEliteVectorFlow_Live.cs
```

**211 lines, fully documented, with:**
- Entry signals (Buy Signal / Sell Signal placeholders — you wire these)
- 87.5pt stop (SetStopLoss)
- 44pt target (SetProfitTarget)
- Breakeven logic (move stop to entry at +62.5pt profit)
- 4 contracts per entry
- Debug logging (Print statements for diagnostics)

### Setup Guide
```
ninjatrader/ISEEliteVectorFlow_Live_SETUP.md
```

Complete instructions for:
- File installation path
- NinjaTrader wiring (how to connect Buy/Sell signals)
- Compilation steps
- Sim101 validation checklist
- Debugging if it fails

---

## Deployment Steps

### Step 1: Copy File to NinjaTrader (2 min)

**Source:**
```
C:\Users\dlewi\Documents\ISE-Elite\ninjatrader\ISEEliteVectorFlow_Live.cs
```

**Destination:**
```
C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\ISEEliteVectorFlow_Live.cs
```

(Copy or clone from GitHub.)

### Step 2: Wire Buy/Sell Signals (30 min)

The strategy file has two TODO lines (line ~126 and ~135):

```csharp
bool buySignal = false;   // TODO: Wire to VectorFlow Buy Signal column
bool sellSignal = false;  // TODO: Wire to VectorFlow Sell Signal column
```

**To wire them:**

In NinjaTrader, edit the file and replace the TODOs with code that reads your VectorFlow indicator.

**Most likely approach:**

In `OnStateChange() State.Configure`, add reference to VectorFlow indicator:
```csharp
// Assumes VectorFlow indicator is loaded on the chart
AddChartIndicator(Indicators.VectorFlow_V1S_NT8(Close));
```

Then in `OnBarUpdate()`, replace the TODO lines:
```csharp
// Get VectorFlow indicator values (replace name if your indicator differs)
var vf = Indicators.VectorFlow_V1S_NT8(Close);

// Check if Buy or Sell signal fired this bar
bool buySignal = (vf.BuySignal != null && vf.BuySignal[0] == 1);
bool sellSignal = (vf.SellSignal != null && vf.SellSignal[0] == 1);
```

**If that doesn't work:**
- Right-click your VectorFlow indicator on the chart → Properties
- Check the exact names of the Buy and Sell Signal plots
- Adjust the code to match those names

### Step 3: Compile in NinjaTrader (5 min)

1. Open NinjaTrader 8 Strategies folder (Tools → Edit NinjaScript → Strategies)
2. Find ISEEliteVectorFlow_Live.cs
3. Right-click → Compile
4. Verify no errors (check Output window)

### Step 4: Load into Sim101 (10 min)

1. Open MNQ1! 5-minute chart in Sim101
2. Add indicator: VectorFlow Algo V1-S (or whatever you call your VectorFlow)
3. Verify indicator is plotting Buy Signal and Sell Signal columns on chart
4. Add strategy: ISEEliteVectorFlow_Live
5. Enable it on the chart
6. Watch for Buy/Sell entry signals

### Step 5: Paper Trade Validation (1 week)

Run on Sim101 for 1 week and collect:

✅ **Daily P&L:** Track vs backtest target (~$906/day)
  - Accept ±10% = $816–$996/day
  - If consistent: move to Step 6
  - If erratic: debug (see SETUP guide)

✅ **Trades/day:** Should be ~5–7
  - Too many (20+): VectorFlow signals firing too often, or stops too close
  - Too few (0–2): Signals not wiring correctly
  
✅ **Win rate:** Should trend toward ~82%
  - Below 60%: Stop or target wrong, or signal inverted

✅ **Stop/target execution:** Verify on chart
  - Stops should appear as red lines ~87.5pt from entry
  - Targets should appear as green lines ~44pt from entry

✅ **Breakeven activation:** Watch for stop moving to entry
  - When position hits +62.5pt profit, stop should jump to entry price
  - Check Output window for "BREAKEVEN ACTIVATED" message

**Validation passes if:** ±10% daily P&L match AND 75%+ win rate for 5+ days

### Step 6: Go Live (1 contract, 1 week)

Once Sim101 passes:

1. Switch to live: 1 contract (not 4, yet)
2. Run 1 week
3. Collect daily P&L vs $906 target
4. If stable: scale to 4 contracts
5. If not: revert to Sim101 and debug

---

## Troubleshooting

### "Strategy loads but no entries"
- Verify VectorFlow indicator is on chart and plotting signals
- Check indicator name and signal column names match your code
- Verify `buySignal` and `sellSignal` variables are wired to actual VectorFlow values
- Check NinjaTrader Output window for errors

### "P&L way off target"
- Verify contract size is 4 (not 1 or 2)
- Verify stop is 87.5pt (not 25 or 100)
- Verify target is 44pt (not 50 or 30)
- Check Position P&L plot to diagnose trade-by-trade

### "Too many false entries"
- VectorFlow may be over-sensitive on this time period
- Check indicator settings match what you had for backtest exports
- Verify signals are truly from VectorFlow (not from a different indicator)

### "NinjaTrader compile error"
- Check using declarations at top of file
- Verify indicator name matches: `Indicators.VectorFlow_V1S_NT8` (adjust if yours differs)
- Check for typos in Plot names (BuySignal vs Buy Signal, etc.)

---

## Files You'll Edit

**File to modify:**
```
C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\ISEEliteVectorFlow_Live.cs
```

**Lines to update:**
- Line ~126: `bool buySignal = false;` → wire to VectorFlow Buy Signal
- Line ~135: `bool sellSignal = false;` → wire to VectorFlow Sell Signal

**Reference:**
- Backtest results: `./backtest-results/backtest_results.csv`
- Expected performance: `SESSION_5_CHECKPOINT.md`
- Setup details: `ninjatrader/ISEEliteVectorFlow_Live_SETUP.md`

---

## Timeline

| Step | Time | Status |
|------|------|--------|
| Copy file | 2 min | ✅ Repo ready |
| Wire signals | 30 min | ⏳ You do this in NinjaTrader |
| Compile | 5 min | ⏳ After wiring |
| Sim101 load | 10 min | ⏳ After compile |
| Paper trade | 1 week | ⏳ After Sim101 |
| Go live | TBD | ⏳ After paper trade passes |

---

## Next Session

Once you've wired the signals and tested on Sim101 for a few days, come back with:
- Daily P&L results (paste screenshot or copy numbers)
- Trades/day actual
- Win rate trend
- Any issues encountered

If Sim101 validates, we move to live 1 contract the same day.

---

**Everything is built. You can start deploying now.**
