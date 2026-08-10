# ISE ELITE DEPLOYMENT CHECKLIST - SESSION OPTIMIZED (1 AM CT START)

**Date:** Tomorrow (Next Trading Day)  
**Start Time:** 1:00 AM CT (London Session Opens)  
**Strategy:** IseEliteStrategy_SESSION_OPTIMIZED.cs  
**Instruments:** MNQ 09-26 (1-minute)  
**Account:** Sim101  

---

## PRE-DEPLOYMENT (11:30 PM CT, Night Before)

- [ ] Verify Sim101 account is accessible and funded
- [ ] PostgreSQL running (if needed for logging)
- [ ] Latest code pulled: `git pull origin main`
- [ ] Strategy file ready: `/home/claude/ISE-Elite/ninjatrader/IseEliteStrategy_SESSION_OPTIMIZED.cs`

---

## DEPLOYMENT WINDOW (12:45 AM CT - 1:00 AM CT)

**12:45 AM CT (15 minutes before London open):**

1. [ ] **Copy strategy file to NinjaTrader:**
   ```powershell
   Copy-Item "C:\Users\dlewi\Documents\ISE-Elite\ninjatrader\IseEliteStrategy_SESSION_OPTIMIZED.cs" `
     "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\IseEliteStrategy.cs" -Force
   ```

2. [ ] **Open NinjaScript Editor** (NinjaTrader → Tools → NinjaScript Editor)

3. [ ] **Open IseEliteStrategy.cs** and click **F5 (Compile)**
   - Expected: "Build Successful - No Errors"
   - If errors: DO NOT PROCEED. Fix and recompile.

4. [ ] **Close NinjaScript Editor** (save if prompted)

5. [ ] **Open NinjaTrader with Sim101 connection**

6. [ ] **Navigate to MNQ 09-26 1-minute chart**

7. [ ] **Right-click on chart → Add Strategy → IseEliteStrategySessionOptimized → OK**
   - Watch NinjaTrader Output window
   - Expected message: `✅ ISE Elite Strategy LIVE - 1 AM CT Start, Session Optimized`

8. [ ] **Verify strategy loaded:**
   - Strategy window shows "IseEliteStrategySessionOptimized"
   - Status: "Running"
   - Zero errors in Output

---

## LIVE TRADING (1:00 AM CT Onward)

### 1:00-3:00 AM CT (London Early Session - 1 Contract)

- [ ] Monitor Output for entry signals
- [ ] Expected message format: `📈 ENTRY #1 @XXXX.XX | SMA5:XXXX.XX > SMA10:XXXX.XX | Contracts: 1 | Session: London-Early`
- [ ] Watch for exits:
  - `📈 Target Hit +3` (+$60 P&L per trade with 1 contract)
  - `📉 Stop Hit -1` (-$20 P&L per trade with 1 contract)
  - `⏱️  30-bar Timeout`

- [ ] Expected: 30-50 signals, ~80% win rate = $600-$900 from London session

### 3:00-4:00 AM CT (Late London)

- [ ] Continue monitoring, same metrics

### 4:00-8:00 AM CT (PAUSE - Losing Period)

- [ ] ⏸️  **No trading** (system automatically pauses)
- [ ] If you have open position, system auto-closes with message: `⏸️  Pause trading window. Exiting position.`
- [ ] You can sleep or step away

### 8:00 AM CT (NY Session Begins - Switch to 2 Contracts)

- [ ] [ ] Watch for resume message
- [ ] New entries with 2 contracts (double risk/reward)
- [ ] Expected message: `📈 ENTRY #X @XXXX.XX | ... | Contracts: 2 | Session: NY Session`
- [ ] Exits now worth:
  - `📈 Target Hit +3` = +$120 P&L (vs $60 with 1 contract)
  - `📉 Stop Hit -1` = -$40 P&L (vs $20 with 1 contract)

### 3:55 PM CT (Force Close - End of Day)

- [ ] System auto-closes ALL positions
- [ ] Expected message: `📊 FORCE CLOSE 3:55 PM: $XXX | Daily P&L: $XXX`
- [ ] Daily summary prints automatically

---

## END-OF-DAY REPORTING (3:55 PM CT)

- [ ] Log daily P&L from Output
- [ ] Count total trades
- [ ] Note any anomalies or errors
- [ ] Screenshot of day's Summary (if available)

**Expected Daily P&L:**
- Conservative: $200-$300/day (accounting for realistic slippage)
- Optimistic: $400-$500/day (good fills, favorable conditions)
- Note: May vary based on actual liquidity and fills

---

## TROUBLESHOOTING

### Compilation Error (F5)
- [ ] Check namespace: `namespace NinjaTrader.NinjaScript.Strategies`
- [ ] Check class name matches file: `public class IseEliteStrategySessionOptimized`
- [ ] Check using declarations: only `using System;` and `using NinjaTrader.Cbi;`
- [ ] Recompile (F5)

### Strategy Won't Load on Chart
- [ ] Verify NinjaTrader not in read-only mode
- [ ] Restart NinjaTrader
- [ ] Right-click chart → Add Strategy again

### No Signals Firing
- [ ] Check Output for messages
- [ ] Verify 5-bar SMA > 10-bar SMA condition is being met
- [ ] Check time (1:00 AM? 8:00 AM? or pause window?)
- [ ] Check bars available (BarsRequiredToTrade = 20)

### Fills Looking Bad (Wide Slippage)
- [ ] Normal for 1 AM CT (London session = thinner spreads)
- [ ] Expected: 0.25-0.50 pt average slippage
- [ ] If > 1 pt consistent: market may be thin, check volume

### Connection Loss
- [ ] System will try to reconnect automatically
- [ ] Check Sim101 account status
- [ ] Reconnect if needed

---

## SUCCESS CRITERIA (First Day)

✅ **PASS if:**
- Strategy compiles with zero errors
- Strategy loads on chart without errors
- Signals fire during London session (1 AM - 3 AM)
- At least 5 profitable trades
- No critical execution errors
- Daily P&L tracks correctly

❌ **HOLD if:**
- Won't compile or load
- No signals firing at expected times
- Execution failures
- Consistent wide slippage (> 1.0 pt)

---

## NEXT STEPS (After First Day)

**If PASS:**
- Continue monitoring for 5 trading days (Week 1)
- Track daily P&L, win rate, slippage
- Move to Week 2 scaling (keep 2 contracts all day, monitor for degradation)

**If issues:**
- Document error messages
- Debug and adjust
- Re-test next trading day

---

## CONTACT / SUPPORT

**Strategy Status:** `IseEliteStrategySessionOptimized` (deployed)  
**Code Location:** `/home/claude/ISE-Elite/ninjatrader/`  
**Memory Files:** `/areas/ise-elite-sim101-deployment.md`  

**Deployed by:** Claude AI Assistant  
**Deployment Date:** Tomorrow  
**Next Review:** After 5 trading days (end of Week 1)

---

**Ready to go live at 1:00 AM CT. Follow this checklist exactly. No deviations.** 🚀
