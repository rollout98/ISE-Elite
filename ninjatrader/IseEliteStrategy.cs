using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using NinjaTrader.Cbi;
using NinjaTrader.Instrument;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Strategy;
using Npgsql;

namespace NinjaTrader.Strategy
{
    public class IseEliteStrategy : Strategy
    {
        // ====================================================================
        // CONFIGURATION
        // ====================================================================
        private int _contractSize = 1; // Start with 1 contract
        private const decimal ADX_TRENDING_THRESHOLD = 25m;
        private const int MAX_TRADES_PER_DAY = 10;
        private const decimal DAILY_LOSS_LIMIT = -1000m;
        private const int ACCOUNT_ID = 1; // Single account for Sim101
        
        // Session boundaries (NY session CT)
        private const int SESSION_OPEN_HOUR = 9;
        private const int SESSION_OPEN_MINUTE = 30;
        private const int SESSION_CLOSE_HOUR = 16;
        private const int SESSION_CLOSE_MINUTE = 0;
        private const int FORCE_CLOSE_HOUR = 15;
        private const int FORCE_CLOSE_MINUTE = 55;

        // ====================================================================
        // DATABASE CONNECTION
        // ====================================================================
        private string _connectionString = "Host=localhost;Username=postgres;Password=postgres;Database=ise_elite";
        private NpgsqlConnection _dbConnection;

        // ====================================================================
        // STATE TRACKING
        // ====================================================================
        private decimal _openingEquity = 0;
        private decimal _dailyPnL = 0;
        private int _todayTradeCount = 0;
        private int _todayWinCount = 0;
        private DateTime _lastTradingDate = DateTime.MinValue;
        private decimal _maxIntraDayDD = 0;
        private decimal _peakEquity = 0;
        
        // Current position tracking
        private bool _hasOpenPosition = false;
        private decimal _entryPrice = 0;
        private DateTime _entryTime = DateTime.MinValue;
        private int _entriesBarsAgo = 0;
        private List<Trade> _trades = new List<Trade>();

        // Safety status
        private bool _connectionError = false;
        private bool _marginError = false;
        private bool _emergencyKillActive = false;
        private string _lastErrorMessage = "";

        // ====================================================================
        // PRIVATE CLASS: TRADE RECORD
        // ====================================================================
        private class Trade
        {
            public DateTime EntryTime { get; set; }
            public DateTime ExitTime { get; set; }
            public decimal EntryPrice { get; set; }
            public decimal ExitPrice { get; set; }
            public int Contracts { get; set; }
            public decimal PnL { get; set; }
            public bool IsWin => PnL > 0;
            public string ExitReason { get; set; }
        }

        // ====================================================================
        // INITIALIZATION
        // ====================================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ISE Elite Automated Futures System (Sim101)";
                Name = "IseEliteStrategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionClose = false;
                IsFillLimitOnClose = false;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.TakeNoAction;
                StopOutdatedOrders = false;
                BarsRequiredToTrade = 20;
                
                AddDataSeries(BarsPeriodType.Minute, 60);
            }
            else if (State == State.Configure)
            {
                // No additional configuration needed for Sim101
            }
            else if (State == State.Realtime)
            {
                _openingEquity = Account.Get(AccountItem.CashValue);
                _peakEquity = _openingEquity;
                InitializeDatabase();
            }
        }

        // ====================================================================
        // 1. DATABASE CONNECTION & LOGGING
        // ====================================================================
        private void InitializeDatabase()
        {
            try
            {
                _dbConnection = new NpgsqlConnection(_connectionString);
                _dbConnection.Open();
                Print($"✅ Database connected: {DateTime.Now}");
                _connectionError = false;
            }
            catch (Exception ex)
            {
                LogSafetyEvent("ConnectionError", "critical", $"Failed to connect to TimescaleDB: {ex.Message}");
                _connectionError = true;
                Print($"❌ Database Error: {ex.Message}");
            }
        }

        private void LogTradeToDB(Trade trade)
        {
            if (_connectionError) return;

            try
            {
                using (var cmd = _dbConnection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ise_trades (account_id, entry_time, exit_time, instrument, 
                            entry_price, exit_price, contracts, pnl, mode, exit_reason)
                        VALUES (@accountId, @entryTime, @exitTime, @instrument, 
                            @entryPrice, @exitPrice, @contracts, @pnl, @mode, @exitReason)";
                    
                    cmd.Parameters.AddWithValue("@accountId", ACCOUNT_ID);
                    cmd.Parameters.AddWithValue("@entryTime", trade.EntryTime);
                    cmd.Parameters.AddWithValue("@exitTime", trade.ExitTime);
                    cmd.Parameters.AddWithValue("@instrument", "MNQ"); // TODO: dynamic
                    cmd.Parameters.AddWithValue("@entryPrice", trade.EntryPrice);
                    cmd.Parameters.AddWithValue("@exitPrice", trade.ExitPrice);
                    cmd.Parameters.AddWithValue("@contracts", trade.Contracts);
                    cmd.Parameters.AddWithValue("@pnl", trade.PnL);
                    cmd.Parameters.AddWithValue("@mode", "Trending"); // TODO: dynamic
                    cmd.Parameters.AddWithValue("@exitReason", trade.ExitReason);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Print($"❌ DB Log Error: {ex.Message}");
                _connectionError = true;
            }
        }

        private void LogSafetyEvent(string eventType, string severity, string message)
        {
            if (_connectionError) return;

            try
            {
                using (var cmd = _dbConnection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO ise_safety_events (account_id, event_type, severity, message, timestamp)
                        VALUES (@accountId, @eventType, @severity, @message, @timestamp)";
                    
                    cmd.Parameters.AddWithValue("@accountId", ACCOUNT_ID);
                    cmd.Parameters.AddWithValue("@eventType", eventType);
                    cmd.Parameters.AddWithValue("@severity", severity);
                    cmd.Parameters.AddWithValue("@message", message);
                    cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Print($"❌ Safety Event Log Error: {ex.Message}");
            }
        }

        // ====================================================================
        // 2. STOP LOSS MANAGEMENT (Native + Manual Validation)
        // ====================================================================
        private void SubmitStopLossOrder(decimal stopPrice, int contracts)
        {
            try
            {
                Order order = ExitLongStop(
                    fromEntrySignal: "EntrySignal",
                    stopPrice: stopPrice,
                    quantity: contracts,
                    signalName: "StopLoss"
                );

                if (order == null)
                {
                    LogSafetyEvent("OrderError", "critical", "Failed to submit stop loss order");
                    Print("❌ Stop loss order failed");
                }
            }
            catch (Exception ex)
            {
                LogSafetyEvent("OrderError", "critical", $"Stop loss error: {ex.Message}");
                Print($"❌ Stop Loss Error: {ex.Message}");
            }
        }

        // ====================================================================
        // 3. ERROR HANDLING WITH RETRY LOGIC
        // ====================================================================
        private void SafeOrderSubmit(Action submitAction, int maxRetries = 3)
        {
            int retries = 0;
            while (retries < maxRetries)
            {
                try
                {
                    submitAction();
                    return;
                }
                catch (Exception ex)
                {
                    retries++;
                    Print($"⚠️  Order submit error (attempt {retries}/{maxRetries}): {ex.Message}");
                    
                    if (retries >= maxRetries)
                    {
                        LogSafetyEvent("OrderError", "critical", $"Failed after {maxRetries} retries: {ex.Message}");
                        Print($"❌ Max retries exceeded");
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(500); // Wait 500ms before retry
                    }
                }
            }
        }

        // ====================================================================
        // 4. POSITION RECONCILIATION
        // ====================================================================
        private void ReconcilePositions()
        {
            try
            {
                decimal accountEquity = Account.Get(AccountItem.CashValue);
                decimal calculatedEquity = _openingEquity + _dailyPnL;
                decimal discrepancy = Math.Abs(accountEquity - calculatedEquity);

                if (discrepancy > 100) // Alert if > $100 discrepancy
                {
                    LogSafetyEvent("Reconciliation", "warning", 
                        $"Position mismatch: Account ${accountEquity} vs Calculated ${calculatedEquity}");
                    Print($"⚠️  Reconciliation: Account ${accountEquity:F2} vs Calc ${calculatedEquity:F2}");
                }

                _peakEquity = Math.Max(_peakEquity, accountEquity);
                var currentDD = _peakEquity - accountEquity;
                if (currentDD > _maxIntraDayDD) _maxIntraDayDD = currentDD;
            }
            catch (Exception ex)
            {
                Print($"❌ Reconciliation Error: {ex.Message}");
            }
        }

        // ====================================================================
        // 5. DAILY LOSS LIMIT (Hard Stop)
        // ====================================================================
        private void CheckDailyLossLimit()
        {
            if (_dailyPnL < DAILY_LOSS_LIMIT)
            {
                LogSafetyEvent("DrawdownLimit", "critical", 
                    $"Daily loss limit hit: ${_dailyPnL:F2} < ${DAILY_LOSS_LIMIT:F2}");
                Print($"⛔ DAILY LOSS LIMIT HIT: ${_dailyPnL:F2}");
                
                // Force close all positions
                CloseAllPositions("DailyLossLimit");
                _emergencyKillActive = true;
            }
        }

        // ====================================================================
        // 6. MARGIN VALIDATION (Pre-Trade Check)
        // ====================================================================
        private bool ValidateMargin(int contractsToEnter)
        {
            try
            {
                decimal availableFunds = Account.Get(AccountItem.BuyingPower);
                decimal requiredMargin = contractsToEnter * 500; // Rough estimate for MNQ: $500 per contract
                
                if (availableFunds < requiredMargin)
                {
                    LogSafetyEvent("MarginError", "warning", 
                        $"Insufficient margin: ${availableFunds} < ${requiredMargin}");
                    Print($"⚠️  Margin check failed: ${availableFunds:F2} < ${requiredMargin:F2}");
                    _marginError = true;
                    return false;
                }
                
                _marginError = false;
                return true;
            }
            catch (Exception ex)
            {
                Print($"❌ Margin Validation Error: {ex.Message}");
                return false;
            }
        }

        // ====================================================================
        // 7. FORCE CLOSE AT SESSION END (3:55 PM CT)
        // ====================================================================
        private void CheckSessionClose()
        {
            DateTime now = Time[0];
            
            // Hard close at 3:55 PM
            if (now.Hour == FORCE_CLOSE_HOUR && now.Minute >= FORCE_CLOSE_MINUTE && _hasOpenPosition)
            {
                Print($"⏰ Force closing all positions at {now:HH:mm} CT");
                CloseAllPositions("SessionEnd");
            }
        }

        // ====================================================================
        // 8. SESSION BOUNDARIES (9:30-16:00 CT)
        // ====================================================================
        private bool IsWithinTradingSession()
        {
            DateTime now = Time[0];
            int hour = now.Hour;
            int minute = now.Minute;

            bool isAfterOpen = (hour > SESSION_OPEN_HOUR) || 
                              (hour == SESSION_OPEN_HOUR && minute >= SESSION_OPEN_MINUTE);
            
            bool isBeforeClose = (hour < SESSION_CLOSE_HOUR) || 
                                (hour == SESSION_CLOSE_HOUR && minute < SESSION_CLOSE_MINUTE);

            return isAfterOpen && isBeforeClose;
        }

        // ====================================================================
        // 9. NEWS BLACKOUT FILTER (FOMC, CPI, NFP)
        // ====================================================================
        private bool IsNewsBlackoutActive()
        {
            DateTime now = Time[0];
            
            // Hardcoded major economic events (FOMC, CPI, NFP)
            List<(DateTime, string)> events = new List<(DateTime, string)>()
            {
                // 2026 dates (example - update with real calendar)
                (new DateTime(2026, 8, 20, 13, 0, 0), "FOMC"),
                (new DateTime(2026, 8, 12, 12, 30, 0), "CPI"),
                (new DateTime(2026, 8, 7, 11, 30, 0), "NFP"),
                // Add more as needed
            };

            foreach (var (eventTime, eventName) in events)
            {
                // 15 minutes before
                if (now >= eventTime.AddMinutes(-15) && now <= eventTime)
                {
                    Print($"🔇 News blackout (pre): {eventName}");
                    return true;
                }
                
                // 30 minutes after
                if (now > eventTime && now <= eventTime.AddMinutes(30))
                {
                    Print($"🔇 News blackout (post): {eventName}");
                    return true;
                }
            }

            return false;
        }

        // ====================================================================
        // 10. EMERGENCY KILL SWITCH (Manual override)
        // ====================================================================
        public void ActivateEmergencyKill()
        {
            Print("🚨 EMERGENCY KILL ACTIVATED");
            LogSafetyEvent("EmergencyKill", "critical", "Manual emergency kill activated");
            CloseAllPositions("EmergencyKill");
            _emergencyKillActive = true;
        }

        private void CloseAllPositions(string reason)
        {
            try
            {
                if (_hasOpenPosition)
                {
                    ExitLong(signalName: reason);
                    _hasOpenPosition = false;
                }
            }
            catch (Exception ex)
            {
                Print($"❌ Force close error: {ex.Message}");
            }
        }

        // ====================================================================
        // MAIN TRADING LOGIC
        // ====================================================================
        protected override void OnBarUpdate()
        {
            if (CurrentBar < 20) return;

            // Daily reset
            if (Time[0].Date != _lastTradingDate)
            {
                _lastTradingDate = Time[0].Date;
                _openingEquity = Account.Get(AccountItem.CashValue);
                _dailyPnL = 0;
                _todayTradeCount = 0;
                _todayWinCount = 0;
                _maxIntraDayDD = 0;
                _peakEquity = _openingEquity;
                Print($"📅 New trading day: {_lastTradingDate:MM-dd-yyyy} | Opening: ${_openingEquity:F2}");
            }

            // ====== SAFETY CHECKS ======
            ReconcilePositions();
            CheckDailyLossLimit();
            CheckSessionClose();

            if (_emergencyKillActive || _connectionError || IsNewsBlackoutActive() || !IsWithinTradingSession())
            {
                return;
            }

            // ====== ENTRY LOGIC ======
            if (!_hasOpenPosition && _todayTradeCount < MAX_TRADES_PER_DAY)
            {
                if (ValidateMargin(_contractSize))
                {
                    // Trend Following signal
                    decimal close5Avg = SMA(Close, 5)[0];
                    decimal close10Avg = SMA(Close, 10)[0];

                    if (close5Avg > close10Avg && Close[0] > close5Avg)
                    {
                        _entryPrice = Close[0];
                        _entryTime = Time[0];
                        _entriesBarsAgo = 0;
                        _hasOpenPosition = true;
                        _todayTradeCount++;

                        // Submit order
                        SafeOrderSubmit(() => 
                        {
                            EnterLong(_contractSize, "EntrySignal");
                            SubmitStopLossOrder(_entryPrice - 1m, _contractSize);
                        });

                        Print($"📈 Entry: ${_entryPrice:F2} | Contracts: {_contractSize} | Bar #{CurrentBar}");
                    }
                }
            }

            // ====== EXIT LOGIC ======
            if (_hasOpenPosition)
            {
                _entriesBarsAgo++;
                decimal priceChange = Close[0] - _entryPrice;

                // Exit on +3 points profit or -1 point stop (or timeout)
                bool shouldExit = (priceChange >= 3m) || (priceChange <= -1m) || (_entriesBarsAgo >= 30);

                if (shouldExit)
                {
                    decimal pnl = priceChange * 20m * _contractSize; // $20 per point for MNQ
                    _dailyPnL += pnl;
                    
                    var trade = new Trade
                    {
                        EntryTime = _entryTime,
                        ExitTime = Time[0],
                        EntryPrice = _entryPrice,
                        ExitPrice = Close[0],
                        Contracts = _contractSize,
                        PnL = pnl,
                        ExitReason = priceChange >= 3m ? "Profit Target" : priceChange <= -1m ? "Stop Loss" : "Timeout"
                    };

                    _trades.Add(trade);
                    if (trade.IsWin) _todayWinCount++;

                    // Log to database
                    LogTradeToDB(trade);

                    SafeOrderSubmit(() => ExitLong(signalName: trade.ExitReason));

                    Print($"📉 Exit: ${Close[0]:F2} | P&L: ${pnl:+0;-0;0} | {trade.ExitReason}");
                    _hasOpenPosition = false;
                }
            }

            // ====== DAILY SUMMARY (at 4:00 PM) ======
            if (Time[0].Hour == 16 && Time[0].Minute == 0)
            {
                GenerateDailyReport();
            }
        }

        // ====================================================================
        // DAILY REPORT GENERATION
        // ====================================================================
        private void GenerateDailyReport()
        {
            try
            {
                decimal closingEquity = Account.Get(AccountItem.CashValue);
                decimal winRate = _todayTradeCount > 0 ? (_todayWinCount * 100m / _todayTradeCount) : 0;

                string report = $@"
╔════════════════════════════════════════════════╗
║       ISE-Elite Daily Report - {Time[0]:MM-dd-yyyy}
╚════════════════════════════════════════════════╝

Opening Equity:     ${_openingEquity:F2}
Closing Equity:     ${closingEquity:F2}
Daily P&L:          ${_dailyPnL:+0.00;-0.00;0.00}
Max Intraday DD:    ${_maxIntraDayDD:F2}

Trades Today:       {_todayTradeCount}
Winning Trades:     {_todayWinCount}
Losing Trades:      {_todayTradeCount - _todayWinCount}
Win Rate:           {winRate:F1}%

Daily Target:       $500+ (Avg)
Target Hit:         {(_dailyPnL >= 500 ? "✅ YES" : "❌ NO")}

Status:             {(_connectionError ? "⚠️  DB Error" : "✅ OK")}

════════════════════════════════════════════════
";

                Print(report);

                // Save to file
                System.IO.File.WriteAllText(
                    $"C:\\Reports\\daily_report_{Time[0]:yyyy-MM-dd}.txt", 
                    report
                );
            }
            catch (Exception ex)
            {
                Print($"❌ Report Error: {ex.Message}");
            }
        }

        // ====================================================================
        // UTILITY: SIMPLE MOVING AVERAGE
        // ====================================================================
        private decimal[] SMA(ISeries<double> series, int period)
        {
            var result = new decimal[series.Count];
            for (int i = 0; i < series.Count; i++)
            {
                if (i < period - 1)
                {
                    result[i] = 0;
                }
                else
                {
                    decimal sum = 0;
                    for (int j = 0; j < period; j++)
                    {
                        sum += (decimal)series[i - j];
                    }
                    result[i] = sum / period;
                }
            }
            return result;
        }

        // ====================================================================
        // CLEANUP
        // ====================================================================
        protected override void OnTerminate()
        {
            if (_dbConnection != null && _dbConnection.State == ConnectionState.Open)
            {
                _dbConnection.Close();
                _dbConnection.Dispose();
            }
        }
    }
}
