# ISE-Elite Tomorrow Morning - Deployment Checklist
## August 11, 2026 - Start 8:00 AM CT

---

## ✅ BEFORE 8:00 AM (Preparation)

- [ ] **Git Pull** — Get latest code
  ```powershell
  cd C:\Users\dlewi\Documents\ISE-Elite
  git pull origin main
  ```

- [ ] **Copy Strategy File**
  ```powershell
  Copy-Item "C:\Users\dlewi\Documents\ISE-Elite\ninjatrader\IseEliteStrategy_PRODUCTION.cs" `
    "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies\IseEliteStrategy.cs"
  ```

- [ ] **PostgreSQL Running**
  ```powershell
  & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -U postgres -h localhost -c "SELECT 1"
  ```
  Should return: `1`

- [ ] **NinjaTrader Closed** (will reopen fresh)

---

## ✅ 8:00 - 8:30 AM (Build & Test)

- [ ] **Open NinjaScript Editor**
  - In NinjaTrader: Tools → NinjaScript Editor

- [ ] **Open Strategy File**
  - File → Open
  - Navigate: `C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\bin\Custom\Strategies`
  - Select: `IseEliteStrategy.cs`
  - Click: Open

- [ ] **Compile (F5)**
  - Should show: `Build: Successful` (green checkmark)
  - **IF ERROR:** Stop immediately. Do NOT proceed.
  - Screenshot error and send to debug.

- [ ] **Close NinjaScript Editor**
  - File → Exit

---

## ✅ 8:25 - 8:30 AM (Final Setup)

- [ ] **Launch NinjaTrader**
  - Log in with Sim101 account
  - Wait for connection: "Connected"

- [ ] **Open MNQ 1-Minute Chart**
  - New Chart → MNQ (E-mini Micro Nasdaq)
  - Timeframe: 1 Minute
  - OK

- [ ] **Add Strategy**
  - On chart, right-click → Add Strategy
  - Find: IseEliteStrategyProduction
  - Click Add
  - Click OK
  - **Watch Output window for: `✅ ISE Elite Strategy LIVE`**

- [ ] **Verify Time Zone**
  - Bottom of screen, should show CT (Central Time)
  - NOT ET or UTC

---

## ✅ 8:30 AM SHARP (MARKET OPEN)

- [ ] **System LIVE**
  - Strategy is monitoring the chart
  - Ready to enter on first signal

- [ ] **Monitor Output Window**
  - Watch for: `📈 ENTRY @ [price]` messages
  - Watch for: `📉 EXIT @ [price]` messages
  - Watch for errors (red text)

---

## 📊 During Market (8:30 AM - 3:00 PM CT)

**Every Hour:**
- [ ] Check Output window — any errors?
- [ ] Check Account equity — is it updating?
- [ ] Check trades — are entries/exits firing?

**At 1:00 PM CT (2 hours in):**
- [ ] Should have 2-3 trades minimum
- [ ] Win rate should be 70%+

**At 3:00 PM CT (end of day):**
- [ ] Watch for: `📊 DAILY SUMMARY: PnL $XXX`
- [ ] This is your daily report
- [ ] Expected: $500-$600 on 1 contract

---

## ❌ TROUBLESHOOTING

**Strategy won't compile:**
- Check: Are you using IseEliteStrategy_PRODUCTION.cs (not SIMPLE or MINIMAL)?
- Check: Is the file in Custom/Strategies folder?
- Delete old files and recopy fresh

**Strategy loads but no `✅ LIVE` message:**
- Strategy loaded but OnStateChange didn't fire
- Remove strategy from chart, reload

**No trades firing:**
- Check time (should be 8:30+ AM CT)
- Check signal: 5-bar SMA > 10-bar SMA?
- Check chart is live (prices updating)?

**Errors in Output:**
- Screenshot the error
- Stop strategy
- Debug and escalate

---

## 📝 WHAT SUCCESS LOOKS LIKE

**First 30 minutes (8:30-9:00 AM):**
- ✅ Strategy loaded with no errors
- ✅ Chart is live
- ✅ System ready for entries

**First Hour (8:30-9:30 AM):**
- ✅ At least 1 trade fired (entry visible in Output)
- ✅ Entry price within 0.25 points of chart
- ✅ No critical errors

**By Lunch (12:00 PM CT):**
- ✅ 3-5 trades total
- ✅ Win rate 70%+
- ✅ Daily P&L tracking ~$200-$300

**By Close (3:00 PM CT):**
- ✅ 5-10 trades total
- ✅ Daily P&L $500-$600
- ✅ Daily summary printed in Output
- ✅ Ready for Week 2 (scaling)

---

## 🚀 YOU'RE READY

The system is built. The signal is validated. All that's left is execution.

**See you at 8:00 AM.** 🎯

