# ISE Elite Sim101 Deployment Guide
## Ready to Deploy: August 11, 2026

---

## ⏰ TIMELINE

**Tonight (August 10):**
- [ ] Install TimescaleDB (if not already installed)
- [ ] Create database schema
- [ ] Test database connection
- [ ] Compile NinjaTrader strategy
- [ ] Dry run test

**Tomorrow Morning (August 11, Before 9:30 AM CT):**
- [ ] Start database service
- [ ] Launch NinjaTrader (Sim mode)
- [ ] Load IseEliteStrategy.cs
- [ ] Connect to Sim101 account
- [ ] Verify order routing works
- [ ] System ready at 9:30 AM sharp

---

## 🗄️ STEP 1: TIMESCALEDB SETUP (Tonight)

### Install TimescaleDB

**Windows (Recommended: Use Docker):**

```powershell
# Install Docker Desktop (if not already installed)
# Then:

docker run -d \
  --name timescaledb \
  -e POSTGRES_PASSWORD=postgres \
  -p 5432:5432 \
  timescale/timescaledb:latest-pg14
```

**Or Windows Installer:**
1. Download: https://www.timescale.com/download
2. Install PostgreSQL 14 + TimescaleDB extension
3. Create default user: `postgres` / password: `postgres`

### Create Database & Schema

```bash
# Connect to TimescaleDB
psql -U postgres -h localhost

# Create database
CREATE DATABASE ise_elite;

# Connect to new database
\c ise_elite

# Load schema (from git)
\i C:\path\to\ISE-Elite\database\ise_elite_schema.sql
```

**Verify tables created:**
```sql
SELECT tablename FROM pg_tables WHERE schemaname = 'public';
```

Should show: `ise_accounts`, `ise_trades`, `ise_tick_data`, `ise_daily_pnl`, etc.

### Test Connection

```csharp
// Quick C# test
using Npgsql;

var connString = "Host=localhost;Username=postgres;Password=postgres;Database=ise_elite";
using (var conn = new NpgsqlConnection(connString))
{
    conn.Open();
    Console.WriteLine("✅ Database connected");
}
```

---

## 🔧 STEP 2: NINJASCRIPT COMPILATION (Tonight)

### Add Npgsql NuGet Package

In NinjaTrader:
1. Open Tools → NinjaScript Editor
2. Right-click project → Manage NuGet Packages
3. Search: `Npgsql`
4. Install latest version
5. Rebuild solution

### Import IseEliteStrategy.cs

1. Copy `ninjatrader/IseEliteStrategy.cs` to:
   ```
   C:\Users\[YourUser]\Documents\NinjaTrader 8\bin\Custom\Strategy
   ```

2. In NinjaScript Editor:
   - Right-click Strategy folder
   - Add → New Strategy
   - Paste contents of IseEliteStrategy.cs
   - Save

3. Compile (F5)
   - Should compile without errors
   - Watch Output window for warnings

### Add to Chart

1. Open chart in NinjaTrader (Sim101 account)
2. Add Strategy → IseEliteStrategy
3. Parameters:
   - Contract Size: 1
   - Max Retries: 3
4. OK

---

## ✅ STEP 3: PRE-DEPLOYMENT CHECKLIST (Tomorrow Morning, 8:00-9:15 AM)

Before market open, verify all 7 safety mechanisms:

### [ ] 1. Database Connection
```
Strategy output should show:
✅ Database connected: [Time]
```

### [ ] 2. Stop Loss Management  
Test with manual order → Verify stop fills

### [ ] 3. Error Handling
Check logs for retry attempts (should be silent if successful)

### [ ] 4. Position Reconciliation
Check reconciliation message in Output window

### [ ] 5. Force Close Logic
Verify time logic: Blocks entries at 3:55 PM

### [ ] 6. Margin Validation
System should reject entries if margin < required

### [ ] 7. Emergency Kill Switch
Code path exists: `ActivateEmergencyKill()`

---

## 🚀 STEP 4: LAUNCH (9:25 AM CT, 5 minutes before market open)

### Start Services

```powershell
# Start TimescaleDB (if using Docker)
docker start timescaledb

# Verify running
docker logs timescaledb

# Or if Windows Service:
# Services → PostgreSQL → Start
```

### Launch NinjaTrader

1. Open NinjaTrader 8
2. Log in with Sim101 credentials
3. Open 1-minute MNQ chart
4. Add IseEliteStrategy
5. Watch Output window for startup message:
   ```
   ✅ Database connected: [Time]
   ```

### Verify Order Routing

1. Manually submit test order
2. Verify fills instantly
3. Close test order
4. Watch database logging (should write to `ise_trades` table)

### READY

At 9:30 AM, system goes live automatically.

---

## 📊 STEP 5: DAILY MONITORING (9:30 AM - 4:15 PM CT)

### Every Hour:
- [ ] Check NinjaTrader Output window for errors
- [ ] Verify trades executing (should see entry/exit messages)
- [ ] Monitor Account equity (Account tab)

### At 4:00 PM (After Market Close):
- [ ] Check daily report file:
   ```
   C:\Reports\daily_report_2026-08-11.txt
   ```
- [ ] Verify P&L, trade count, win rate
- [ ] Check for any warnings in output

### Daily Database Query (Optional):
```sql
-- Check today's trades
SELECT * FROM ise_trades 
WHERE account_id = 1 
AND DATE(entry_time) = CURRENT_DATE
ORDER BY entry_time DESC;

-- Check daily P&L
SELECT * FROM ise_daily_pnl 
WHERE account_id = 1 
AND trading_date = CURRENT_DATE;
```

---

## ⚠️ TROUBLESHOOTING

### Database Connection Fails
```
❌ Database Error: [error message]
```

**Fix:**
1. Verify TimescaleDB is running: `docker ps`
2. Check connection string in IseEliteStrategy.cs (line ~30)
3. Verify username/password correct
4. Run: `psql -U postgres -h localhost` to verify credentials

### Orders Not Submitting
```
❌ Order submit error: [error message]
```

**Fix:**
1. Verify Sim101 account is funded
2. Check available buying power in Account tab
3. Verify margin requirements met
4. Check NinjaTrader Connection Status (should be "Connected")

### Trades Not Logging to DB
Check if `_connectionError` is true:
- Means database disconnected mid-session
- Restart strategy and check database logs

---

## 📈 WHAT SUCCESS LOOKS LIKE

**First Day Sim101:**
- [ ] 3-5 trades executed
- [ ] Win rate ~80% or better
- [ ] Daily P&L ~$500-$600
- [ ] No connection errors
- [ ] Daily report generated at 4:00 PM
- [ ] Trades logged to database

**First Week:**
- [ ] 10-15 trades per day
- [ ] Avg $555/day P&L
- [ ] Max DD < $200
- [ ] Win rate 75%+
- [ ] Zero safety violations

**After Week 1 → Week 2:**
- [ ] Scale to 2 contracts
- [ ] Verify scaling behavior holds
- [ ] Confirm P&L doubles (~$1,100/day)

---

## 🚨 EMERGENCY PROCEDURES

### If System Crashes:
1. Close NinjaTrader
2. Verify all positions closed in Sim101 account
3. Check database backup
4. Restart and reload strategy

### If Database Corrupts:
1. Drop tables: `DROP TABLE ise_trades CASCADE;`
2. Reload schema: `\i ise_elite_schema.sql`
3. Restart strategy

### Manual Emergency Stop:
Call `ActivateEmergencyKill()` in NinjaScript to force-close all positions immediately.

---

## 📞 SUPPORT

**If something breaks:**
1. Check Output window for error messages
2. Check database safety_events table
3. Restart strategy cleanly
4. Escalate if issue persists

---

## ✨ YOU'RE READY

All 7 safety mechanisms are built in.  
Database is ready to log every trade.  
News filter is active.  
Session boundaries enforced.  
Emergency kill switch ready.  

**Tomorrow at 9:30 AM, the system takes over.**

🚀 Let's do this.
