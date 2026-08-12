# ISEEliteVectorFlow_Live — Setup & Wiring Guide

## File Location
```
C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\ISEEliteVectorFlow_Live.cs
```

## Installation Steps

1. **Copy the file to NinjaTrader**
   - Copy `ISEEliteVectorFlow_Live.cs` to the path above
   - NinjaTrader will auto-detect it (or right-click Strategies → Compile)

2. **Load on Sim101**
   - Open MNQ1! 5-minute chart in Sim101
   - Add your VectorFlow Algo V1-S indicator
   - Verify indicator is plotting: "Buy Signal" and "Sell Signal" columns
   - Add strategy: ISEEliteVectorFlow_Live
   - Verify it compiles (no errors)

3. **Wire Signal Detection (TODO in code)**
   
   The strategy has two placeholder lines (around line 126 and 135):
   ```csharp
   bool buySignal = false;   // TODO: Wire to VectorFlow Buy Signal column
   bool sellSignal = false;  // TODO: Wire to VectorFlow Sell Signal column
   ```

   **To wire them:**
   
   You need to read the VectorFlow indicator's "Buy Signal" and "Sell Signal" plot values.
   
   Option A (Easiest): If VectorFlow is already loaded as an indicator on the chart:
   ```csharp
   // Get reference to VectorFlow indicator (replace "VectorFlow_V1S_NT8" with actual indicator name)
   var vf = Indicators.VectorFlow_V1S_NT8(Close);
   
   bool buySignal = vf.BuySignal[0] == 1;   // Or check the column name
   bool sellSignal = vf.SellSignal[0] == 1; // Or check the column name
   ```
   
   Option B (More explicit): Reference the indicator plot directly
   ```csharp
   // In OnStateChange() State.Configure:
   AddChartIndicator(Indicators.VectorFlow_V1S_NT8(Close));
   
   // Then in OnBarUpdate():
   // Read Buy Signal and Sell Signal values from indicator
   ```

4. **Compile & Test**
   - Right-click strategy → Compile
   - Verify no errors
   - Load into Sim101

## Configuration (All Locked)

These are FIXED per backtest validation. Do NOT change:

```
Stop Loss:           87.5 points (350 ticks)
Profit Target:       44 points
Breakeven Move:      62.5 points (once profit reaches this, stop moves to entry)
Contract Size:       4 contracts per entry
```

**Expected daily P&L: ~$906** (±10% acceptable = $816–$996)
**Expected trades/day: ~5–7**
**Expected win rate: ~82%**

## First Run (Sim101 Paper Trade)

### Day 1
- Verify strategy loads and shows no errors
- Watch for Buy/Sell entries
- Check stop/target levels on chart (should be visible as lines)
- Monitor position P&L in the "Position PnL" plot

### Days 2–7
- Collect daily P&L
- Verify ±10% match to backtest $906/day target
- Check win rate (should trend toward 82%)
- Verify stops execute at correct prices
- Verify targets execute at correct prices

### Validation Checklist
- [ ] Loads without errors
- [ ] Generates 5–7 trades/day
- [ ] Win rate trending 75%+
- [ ] Daily P&L within ±10% of $906 target
- [ ] Stops/targets executing at correct prices
- [ ] Breakeven logic triggering (watch for stop @ entry price once in profit)

## If It Passes (Ready for Live)
- Switch to live: 1 contract
- Run 1 week
- Scale to 4 contracts if stable

## If It Fails (Debug)
- Check VectorFlow indicator is loaded and plotting Buy/Sell signals
- Verify signal column names match code
- Check that stops/targets are wired correctly
- Print debug output (Print() statements are enabled)
- Check logs: NinjaTrader → Tools → Output

## Key Code Sections

**Entry logic** (lines ~125–155)
- Reads Buy Signal and Sell Signal
- Enters 4 contracts
- Sets 87.5pt stop and 44pt target
- Records entry price for BE calculation

**Breakeven logic** (lines ~45–59)
- Tracks position P&L each bar
- Once profit >= 62.5pt, moves stop to entry (zero risk)
- Prevents re-triggering with breakEvenSet flag

**Debug output** (Print statements)
- Entry: "LONG entry: ..., stop ..., target ..."
- Breakeven: "BREAKEVEN ACTIVATED: ... profit, stop moved to entry ..."
- Close: "Position closed: ... @ ..."

## Questions/Issues?

1. **Strategy not showing entries?** → VectorFlow indicator not loaded or signals not firing
2. **Stops/targets not right?** → Check SetStopLoss/SetProfitTarget calls in OnBarUpdate
3. **P&L way off?** → Check contract size is 4, and stop/target values match 87.5 and 44
4. **NinjaTrader compile errors?** → Check using declarations, indicator names, column references

---

**Once it passes Sim101, you're live ready.**
