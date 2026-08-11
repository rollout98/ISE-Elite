# ISE Elite Config 1669 Deployment Guide

**Strategy:** VectorFlow entry, 25pt stop, 38pt target, 4 MNQ contracts  
**Expected:** $789/day, 67.4% win, 6.1 trades/day  
**Backtest period:** 44 days (Jun 8 – Aug 10, 2026), 56,800 bars

---

## PHASE 1: NinjaScript Build & Test (2–4 hours)

### Step 1: Copy Strategy to NinjaTrader

```
Source: C:\Users\dlewi\Documents\ISE-Elite\ninjatrader\ISEEliteVectorFlow_Config1669.cs
Dest:   C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\
```

### Step 2: Load and Compile in NinjaTrader

1. Open NinjaTrader 8
2. Tools → Options → Compile on Save (enable)
3. Tools → Edit Script → ISEEliteVectorFlow_Config1669
4. Review the code, verify no errors in the output pane
5. Close when compile successful

### Step 3: Verify Strategy Parameters in UI

1. Strategies → Add → ISEEliteVectorFlow_Config1669
2. Confirm defaults show:
   - Stop Distance (Points): **25** ✅
   - Target Distance (Points): **38** ✅
   - Position Size (Contracts): **4** ✅
3. Do NOT change these (locked by backtest)

### Step 4: Load into Sim101 Account

1. Open a 1-minute MNQ1! chart
2. Add the strategy to Sim101 account (NOT live)
3. Do NOT enable yet; just load the instance
4. Confirm in the Strategies window it shows "ISEEliteVectorFlow_Config1669"

**Gate:** If compilation fails or parameters don't match → debug before proceeding.

---

## PHASE 2: Paper Trade Validation (1 week)

### Pre-Trade Setup

**Account:** Sim101 (simulated, $50k starting capital, same as backtest)  
**Instrument:** MNQ1! (Micro E-mini Nasdaq)  
**Timeframe:** 1-minute bars  
**Hours:** Your normal trading hours (suggested: Asia 7-8:15pm CT, NY 9am–11am CT)  
**Duration:** 5 trading days minimum (1 week preferred)

### Enable Strategy

1. MNQ1! 1-min chart
2. Strategies window → Right-click ISEEliteVectorFlow_Config1669 → Enable
3. Confirm it shows "Running" status

### Trade Log: Daily Metrics to Collect

**Create a spreadsheet with these columns:**

| Date | Trades | Wins | Losses | Gross P&L | Largest Win | Largest Loss | Max DD (day) | Notes |
|------|--------|------|--------|-----------|-------------|--------------|-------------|-------|
|      |        |      |        |           |             |              |             |       |

**Daily target:**
- Trades: 4–8 (backtest avg: 6)
- Win rate: 60%+ (backtest: 67%)
- P&L: $500–$1,200 (backtest avg: $789)
- Max DD: <$1,000 (backtest total: $2,400)

### What to Watch For (Red Flags)

| Issue | Symptom | Action |
|-------|---------|--------|
| **Stop not real** | Losses exceed 25pt consistently | Check SetStopLoss() firing; may need OnOrderUpdate fix |
| **Target too tight** | Wins under 50pt, losses over 25pt | Verify target calculation; check order fill timing |
| **Double entries** | Same signal fires twice | Add entry gate logic to prevent re-entry while in trade |
| **Trades/day low** | <3 trades/day for 3+ days | Check VectorFlow indicator is firing; verify chart refreshing |
| **P&L lopsided** | LONG makes money, SHORT loses (or vice versa) | Review entry logic; may have direction-specific bug |

### Validation Criteria

**PASS if:**
- ✅ Trades/day: 4–8 for 5 days
- ✅ Win rate: 55%+ (vs backtest 67%)
- ✅ 5-day total P&L: ±10% of backtest ($3,945 = $789/day × 5, target range $3,550–$4,340)
- ✅ No more than 2 red-flag incidents
- ✅ LONG and SHORT split roughly 50/50

**FAIL if:**
- ❌ Trades/day: <3 or >15
- ❌ Win rate: <40%
- ❌ 5-day P&L: <$2,500 or systemically negative
- ❌ >3 red-flag incidents
- ❌ LONG dominates SHORT or vice versa (>70/30 split)

### End of Phase 2: Decision Gate

**If PASS:** → Proceed to Phase 3  
**If FAIL:** → Debug and repeat Phase 2 (or revert to manual trading if unresolvable)

---

## PHASE 3: Live Deployment (Scaled)

### Pre-Live Checklist

- [ ] Phase 2 validation passed (criteria above)
- [ ] Risk plan documented (below)
- [ ] Broker account verified (Sim101 → live)
- [ ] Daily P&L tracking sheet ready
- [ ] Stop-loss and profit-target orders tested in live account (first trade)

### Risk Plan (Live Account)

**Capital:** $50,000  
**Position size:** Start at **1 contract**, scale to 4 after 5 profitable days

**Daily max loss:** $1,000 (2% of account)  
**Weekly max loss:** $3,000 (6% of account)  
**Max drawdown before pause:** $5,000 (10% of account)

**If max loss hit:**
- Pause trading that day
- Review day's trades for execution quality
- Resume next day if root cause identified and fixed

### Day 1: 1 Contract Live (1 week)

1. Enable strategy on live account with **1 contract only**
2. Collect 5–8 trades
3. Track: fills vs backtest, slippage, target/stop execution
4. Compare to Sim101 trades

**Gate:** If 5 trades ≥60% win and P&L ~$150/day (1/4 of 4-contract target), proceed.

### Day 8: Scale to 4 Contracts (ongoing)

1. Increase PositionSize from 1 → 4 in strategy settings
2. Resume normal trading
3. Daily P&L tracking: target $600–$900/day
4. Pause if daily loss exceeds $1,000

### Ongoing Monitoring

**Daily (5 min after market close):**
- P&L for the day
- Win rate (should be 60%+)
- Largest win/loss (should be ±$200–$300 at 4 contracts)

**Weekly:**
- Total P&L vs $789/day target
- Any execution anomalies
- Slippage trends (should be <$0.50/contract)

**Monthly:**
- Rolling win rate (target: >65%)
- Drawdown (target: <$2,400/month)
- Compare to backtest expected returns

---

## Rollback Plan

If live trading underperforms for **3 consecutive days >20% below target:**

1. Pause trading (don't disable strategy, just stop entering)
2. Review last 50 trades for execution quality
3. Check if VectorFlow indicator is still firing correctly on chart
4. Revert to Sim101 for 1 week to re-validate
5. Resume live only after re-validation passes

---

## Key Files & Locations

| File | Purpose | Location |
|------|---------|----------|
| ISEEliteVectorFlow_Config1669.cs | Live strategy code | `NinjaTrader 8\bin\Custom\Strategies\` |
| backtest_results.csv | Backtest baseline | `ISE-Elite\backtest-results\` |
| Sim101 trade journal | Paper trade log | Sim101 account statements |
| Live trade journal | Live trades | Your daily P&L tracking sheet |

---

## Support / Debugging

**If strategy doesn't enter trades:**
- Verify VectorFlow indicator is on the chart
- Check indicator is firing (should see Buy/Sell labels)
- Enable strategy with TraceOrders=true to see order flow in Output pane

**If stops/targets aren't executing:**
- Confirm SetStopLoss() and SetProfitTarget() firing (check Order Management)
- May need to adjust OrderFillResolution or Slippage settings

**If P&L is half expected:**
- Check position size (should be 4)
- Verify account is in MNQ contracts (not NQ, not QQQ)
- Review individual trade fills vs chart price

---

**Ready?** Start Phase 1 after lunch. Report back with compiler status.
