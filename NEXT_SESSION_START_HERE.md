# START HERE — Session 6: Build Live NinjaScript

**Status:** MNQ backtest validated. Config locked. Ready to build.

---

## The Task

Build `ISEEliteVectorFlow_Live.cs` — a live NinjaTrader strategy that replicates the proven backtest.

**Time estimate:** 3–4 hours for someone familiar with NinjaTrader

---

## Locked Configuration (Do Not Change)

```
Entry:       VectorFlow Buy Signal / Sell Signal (5m TradingView)
Stop:        87.5 points (350 ticks)
Target:      44 points 
Breakeven:   Move stop to entry once profit >= 62.5 or 75 points
Size:        4 contracts per entry
Exit:        (1) hit 44pt → exit, (2) hit stop → exit, (3) at BE → exit at entry
```

**This produced $39,882 over 44 days = $906/day on real data.**

---

## Template Source

Start with: `C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\VectorFlow_V1S_NT8.cs`

(It already has VectorFlow Buy/Sell signals. Just add stops/targets/breakeven logic.)

---

## Key Code Snippets to Add

```csharp
// On entry signal:
if (buySignal)
{
    EnterLong(4, "VFL_Long");
    SetStopLoss(CalculationMode.Points, 87.5);
    SetProfitTarget(CalculationMode.Points, 44);
}

if (sellSignal)
{
    EnterShort(4, "VFL_Short");
    SetStopLoss(CalculationMode.Points, 87.5);
    SetProfitTarget(CalculationMode.Points, 44);
}

// Breakeven logic (pseudocode; implement per NinjaTrader patterns):
if (Position.Profit >= 62.5 points && !breakEvenSet)
{
    SetStopLoss(CalculationMode.Points, 0);  // Move stop to entry
    breakEvenSet = true;
}
```

---

## Build Steps

1. **Copy template** → rename to ISEEliteVectorFlow_Live.cs
2. **Keep the Buy/Sell signal logic** (already correct)
3. **Add SetStopLoss/SetProfitTarget calls** in entry blocks
4. **Add breakeven trigger** (track unrealized profit, move stop at threshold)
5. **Compile in NinjaTrader** (verify no errors)
6. **Load into Sim101** and test

---

## Validation Checklist (Sim101 Paper Trade)

Once built, paper trade on Sim101 for 1 week:

- [ ] Daily P&L tracks ~$906 (target: ±10% = $816–$996/day)
- [ ] Trades/day ~5–7 (backtest was 5.8)
- [ ] Stops execute at correct prices (±1 tick of 87.5pt from entry)
- [ ] Targets execute at correct prices (±1 tick of 44pt from entry)
- [ ] Breakeven logic triggers (watch P&L jump when stop moves to entry)
- [ ] No double-entries or missed signals
- [ ] Win rate tracking to ~82%

**If Sim101 passes ±10% P&L match:** Ready for live 1 contract.

---

## Questions Before You Start?

- [ ] VectorFlow signals already in template? (yes)
- [ ] Need help wiring breakeven logic? (ask, can walk through)
- [ ] Unsure about SetStopLoss/SetProfitTarget syntax? (check NT8 docs or ask)
- [ ] Want to run on Sim101 first before live? (yes, mandatory)

---

## Files You'll Touch

**Read:**
- `VectorFlow_V1S_NT8.cs` (template)
- `./backtest-results/backtest_results.csv` (reference)
- `SESSION_5_CHECKPOINT.md` (architecture summary)

**Create:**
- `ISEEliteVectorFlow_Live.cs` (new strategy file)

**Monitor:**
- Sim101 daily P&L (first week of paper trade)

---

## Go-Live Path (After Sim101 Passes)

1. Switch to live: 1 contract
2. Run 1 week
3. If stable: scale to 4 contracts
4. Ongoing: track daily P&L vs $906 target

---

**Everything is ready. Build it.**
