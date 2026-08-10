using System;
using System.ComponentModel.DataAnnotations;
using NinjaTrader.Cbi;
using NinjaTrader.Instrument;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.Strategy;

namespace NinjaTrader.Strategy
{
    public class IseEliteStrategy : Strategy
    {
        // ====================================================================
        // CONFIGURATION - TIMEZONE CORRECTED (CT)
        // ====================================================================
        private int contractSize = 1;
        private const decimal ADX_TRENDING_THRESHOLD = 25m;
        private const decimal DAILY_LOSS_LIMIT = -1000m;
        
        // Session boundaries (NY session in CENTRAL TIME)
        private const int SESSION_OPEN_HOUR = 8;         // 8:30 AM CT = 9:30 AM ET
        private const int SESSION_OPEN_MINUTE = 30;
        private const int SESSION_CLOSE_HOUR = 15;       // 3:00 PM CT = 4:00 PM ET
        private const int SESSION_CLOSE_MINUTE = 0;
        private const int FORCE_CLOSE_HOUR = 14;         // 2:55 PM CT = 3:55 PM ET
        private const int FORCE_CLOSE_MINUTE = 55;

        // ====================================================================
        // STATE TRACKING
        // ====================================================================
        private decimal openingEquity = 0;
        private decimal dailyPnL = 0;
        private int todayTradeCount = 0;
        private int todayWinCount = 0;
        private DateTime lastTradingDate = DateTime.MinValue;
        private decimal maxIntraDayDD = 0;
        private decimal peakEquity = 0;
        
        // Position tracking
        private bool hasOpenPosition = false;
        private decimal entryPrice = 0;
        private DateTime entryTime = DateTime.MinValue;
        private int entryBarsAgo = 0;
        private bool emergencyKillActive = false;

        // ====================================================================
        // INITIALIZATION
        // ====================================================================
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "ISE Elite - Simplified Trading System (Sim101)";
                Name = "IseEliteStrategy";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionClose = false;
                TraceOrders = false;
                BarsRequiredToTrade = 20;
            }
            else if (State == State.Realtime)
            {
                openingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
                peakEquity = openingEquity;
                Print("✅ ISE Elite Strategy LIVE - Ready to trade");
            }
        }

        // ====================================================================
        // MAIN TRADING LOGIC
        // ====================================================================
        protected override void OnBarUpdate()
        {
            if (CurrentBar < 20) return;

            // ====== DAILY RESET ======
            if (Time[0].Date != lastTradingDate)
            {
                lastTradingDate = Time[0].Date;
                openingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
                dailyPnL = 0;
                todayTradeCount = 0;
                todayWinCount = 0;
                maxIntraDayDD = 0;
                peakEquity = openingEquity;
                Print($"📅 New trading day: {lastTradingDate:MM-dd-yyyy} | Opening: ${openingEquity:F2}");
            }

            // ====== SAFETY CHECKS ======
            ReconcilePositions();
            CheckDailyLossLimit();
            CheckSessionClose();

            if (emergencyKillActive || IsNewsBlackoutActive() || !IsWithinTradingSession())
            {
                return;
            }

            // ====== ENTRY LOGIC ======
            if (!hasOpenPosition && todayTradeCount < 10)
            {
                if (ValidateMargin())
                {
                    // Trend Following signal: 5-bar avg > 10-bar avg
                    decimal close5Avg = SMA(Close, 5);
                    decimal close10Avg = SMA(Close, 10);

                    if (close5Avg > close10Avg && Close[0] > close5Avg)
                    {
                        entryPrice = Close[0];
                        entryTime = Time[0];
                        entryBarsAgo = 0;
                        hasOpenPosition = true;
                        todayTradeCount++;

                        EnterLong(contractSize, "EntrySignal");
                        Print($"📈 Entry: ${entryPrice:F2} | Contracts: {contractSize} | Bar #{CurrentBar}");
                    }
                }
            }

            // ====== EXIT LOGIC ======
            if (hasOpenPosition)
            {
                entryBarsAgo++;
                decimal priceChange = Close[0] - entryPrice;
                decimal pnl = priceChange * 20m * contractSize; // $20 per point for MNQ

                // Exit on +3 points profit or -1 point stop (or timeout 30 bars)
                bool shouldExit = (priceChange >= 3m) || (priceChange <= -1m) || (entryBarsAgo >= 30);

                if (shouldExit)
                {
                    dailyPnL += pnl;
                    if (priceChange >= 0) todayWinCount++;

                    string exitReason = priceChange >= 3m ? "Profit Target" : priceChange <= -1m ? "Stop Loss" : "Timeout";
                    
                    ExitLong(signalName: exitReason);
                    Print($"📉 Exit: ${Close[0]:F2} | P&L: ${pnl:+0;-0;0} | {exitReason}");
                    hasOpenPosition = false;
                }
            }

            // ====== DAILY SUMMARY (at 3:00 PM CT) ======
            if (Time[0].Hour == 15 && Time[0].Minute == 0)
            {
                GenerateDailyReport();
            }
        }

        // ====================================================================
        // SAFETY MECHANISMS
        // ====================================================================

        private void ReconcilePositions()
        {
            try
            {
                decimal accountEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
                decimal calculatedEquity = openingEquity + dailyPnL;
                decimal discrepancy = Math.Abs(accountEquity - calculatedEquity);

                if (discrepancy > 100)
                {
                    Print($"⚠️  Reconciliation warning: Account ${accountEquity:F2} vs Calc ${calculatedEquity:F2}");
                }

                peakEquity = Math.Max(peakEquity, accountEquity);
                var currentDD = peakEquity - accountEquity;
                if (currentDD > maxIntraDayDD) maxIntraDayDD = currentDD;
            }
            catch (Exception ex)
            {
                Print($"❌ Reconciliation Error: {ex.Message}");
            }
        }

        private void CheckDailyLossLimit()
        {
            if (dailyPnL < DAILY_LOSS_LIMIT)
            {
                Print($"⛔ DAILY LOSS LIMIT HIT: ${dailyPnL:F2}");
                CloseAllPositions("DailyLossLimit");
                emergencyKillActive = true;
            }
        }

        private void CheckSessionClose()
        {
            // Hard close at 2:55 PM CT (3:55 PM ET)
            if (Time[0].Hour == FORCE_CLOSE_HOUR && Time[0].Minute >= FORCE_CLOSE_MINUTE && hasOpenPosition)
            {
                Print($"⏰ Force closing at {Time[0]:HH:mm} CT");
                CloseAllPositions("SessionEnd");
            }
        }

        private bool IsWithinTradingSession()
        {
            int hour = Time[0].Hour;
            int minute = Time[0].Minute;

            bool isAfterOpen = (hour > SESSION_OPEN_HOUR) || 
                              (hour == SESSION_OPEN_HOUR && minute >= SESSION_OPEN_MINUTE);
            
            bool isBeforeClose = (hour < SESSION_CLOSE_HOUR) || 
                                (hour == SESSION_CLOSE_HOUR && minute < SESSION_CLOSE_MINUTE);

            return isAfterOpen && isBeforeClose;
        }

        private bool IsNewsBlackoutActive()
        {
            int hour = Time[0].Hour;
            int minute = Time[0].Minute;
            
            // Major economic releases (simplified - add real dates as needed)
            // This is placeholder logic
            return false;
        }

        private bool ValidateMargin()
        {
            try
            {
                decimal availableFunds = Account.Get(AccountItem.BuyingPower, Currency.UsDollar);
                decimal requiredMargin = contractSize * 500; // ~$500 per MNQ contract
                
                return availableFunds >= requiredMargin;
            }
            catch
            {
                return false;
            }
        }

        private void CloseAllPositions(string reason)
        {
            try
            {
                if (hasOpenPosition)
                {
                    ExitLong(signalName: reason);
                    hasOpenPosition = false;
                }
            }
            catch (Exception ex)
            {
                Print($"❌ Force close error: {ex.Message}");
            }
        }

        public void ActivateEmergencyKill()
        {
            Print("🚨 EMERGENCY KILL ACTIVATED");
            CloseAllPositions("EmergencyKill");
            emergencyKillActive = true;
        }

        // ====================================================================
        // DAILY REPORT
        // ====================================================================
        private void GenerateDailyReport()
        {
            try
            {
                decimal closingEquity = Account.Get(AccountItem.CashValue, Currency.UsDollar);
                decimal winRate = todayTradeCount > 0 ? (todayWinCount * 100m / todayTradeCount) : 0;

                string report = $@"
╔════════════════════════════════════════════════╗
║       ISE-Elite Daily Report - {Time[0]:MM-dd-yyyy}
╚════════════════════════════════════════════════╝

Opening Equity:     ${openingEquity:F2}
Closing Equity:     ${closingEquity:F2}
Daily P&L:          ${dailyPnL:+0.00;-0.00;0.00}
Max Intraday DD:    ${maxIntraDayDD:F2}

Trades Today:       {todayTradeCount}
Winning Trades:     {todayWinCount}
Losing Trades:      {todayTradeCount - todayWinCount}
Win Rate:           {winRate:F1}%

Daily Target:       $500+ (expected)
Target Hit:         {(dailyPnL >= 500 ? "✅ YES" : "❌ NO")}

════════════════════════════════════════════════
";

                Print(report);
            }
            catch (Exception ex)
            {
                Print($"❌ Report Error: {ex.Message}");
            }
        }

        // ====================================================================
        // UTILITY: SIMPLE MOVING AVERAGE
        // ====================================================================
        private decimal SMA(ISeries<double> series, int period)
        {
            if (CurrentBar < period) return 0;
            
            decimal sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += (decimal)series[i];
            }
            return sum / period;
        }
    }
}
