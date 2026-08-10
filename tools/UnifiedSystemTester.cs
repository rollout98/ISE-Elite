using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.BacktestTools
{
    /// <summary>
    /// ISE-Elite Unified System Tester - Variable Contract Size
    /// 
    /// Tests with 1, 2, or 3 contracts to validate:
    /// ✅ Does the system hit $500-$1k daily targets with more contracts?
    /// ✅ Does drawdown blow up or stay controlled?
    /// ✅ Are we getting stopped out too much? (profit target vs stop loss ratio)
    /// ✅ Does the edge hold with scaled position size?
    /// </summary>
    public class UnifiedSystemTester
    {
        private class DailySession
        {
            public DateTime Date { get; set; }
            public decimal OpeningEquity { get; set; }
            public decimal ClosingEquity { get; set; }
            public decimal DailyPnL => ClosingEquity - OpeningEquity;
            public int TotalTrades { get; set; }
            public int ProfitTargetExits { get; set; }
            public int StopLossExits { get; set; }
            public decimal DailyTarget { get; set; }
            public List<Trade> Trades { get; set; } = new();
            public bool HitDailyTarget => DailyPnL >= DailyTarget;
        }

        private class Trade
        {
            public DateTime EntryTime { get; set; }
            public DateTime ExitTime { get; set; }
            public decimal EntryPrice { get; set; }
            public decimal ExitPrice { get; set; }
            public int Contracts { get; set; }
            public decimal PnL { get; set; }
            public bool IsWin => PnL > 0;
            public string? ExitType { get; set; } // "Profit Target" or "Stop Loss"
        }

        private readonly List<HistoricalBar> _bars;
        private readonly int _contractSize;
        private const decimal MNQ_TICK_VALUE = 20m;
        private const decimal ADX_TRENDING_THRESHOLD = 25m;

        public UnifiedSystemTester(int contractSize = 1)
        {
            _bars = GenerateMockBars();
            _contractSize = contractSize;
        }

        private List<HistoricalBar> GenerateMockBars()
        {
            var bars = new List<HistoricalBar>();
            var startDate = new DateTime(2024, 3, 1, 9, 30, 0, DateTimeKind.Utc);
            var price = 5000m;

            for (int i = 0; i < 5000; i++)
            {
                var trendPhase = (i / 500) % 2;
                var noise = (decimal)(new Random(i).NextDouble() * 2 - 1);
                var trend = trendPhase == 0 
                    ? (decimal)(Math.Sin(i * 0.02) * 0.8)
                    : (decimal)(Math.Sin(i * 0.15) * 0.2);
                var change = (noise + trend) * 0.5m;
                price += change;

                var bar = new HistoricalBar(
                    i < 2500 ? "MNQ" : "MGC",
                    i < 2500 ? "MNQ" : "MGC",
                    startDate.AddMinutes(i),
                    startDate.AddDays(i / 480).Date,
                    60,
                    price - 1, price + 1, price - 2, price,
                    (long)(1000 + (i % 500)),
                    sourceKind: HistoricalDataSourceKind.NinjaTraderProvider,
                    sourceName: "MockData"
                );
                bars.Add(bar);
            }

            return bars.OrderBy(b => b.TimestampUtc).ToList();
        }

        public void RunTest()
        {
            Console.WriteLine($"\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine($"║   ISE-Elite Unified System Tester ({_contractSize} Contract(s))            ║");
            Console.WriteLine($"╚════════════════════════════════════════════════════════════════╝\n");

            var sessions = new List<DailySession>();
            var currentDate = _bars.First().TradingDay;
            var currentSession = new DailySession { Date = currentDate, OpeningEquity = 50000 };
            var activeContracts = 0;
            var entryPrice = 0m;
            var entryTime = DateTime.MinValue;
            var barsHeld = 0;
            var currentEquity = 50000m;
            var peakEquity = 50000m;
            var maxDrawdown = 0m;
            var allTrades = new List<Trade>();
            var recentBars = new List<HistoricalBar>();

            foreach (var bar in _bars)
            {
                // Daily reset
                if (bar.TradingDay != currentDate)
                {
                    currentSession.ClosingEquity = currentEquity;
                    sessions.Add(currentSession);
                    currentDate = bar.TradingDay;
                    currentSession = new DailySession { Date = currentDate, OpeningEquity = currentEquity };
                }

                recentBars.Add(bar);
                if (recentBars.Count > 20) recentBars.RemoveAt(0);

                // Calculate regime
                var adx = CalculateAdx(recentBars);
                var atr = CalculateAtr(recentBars);
                var volumeNormalized = Math.Min((decimal)bar.Volume / 1000m, 100m);
                var dailyScore = (atr * 2) + (adx * 1.5m) + volumeNormalized;
                bool isTrending = adx > ADX_TRENDING_THRESHOLD;

                // Daily target
                decimal dailyTarget = dailyScore switch
                {
                    < 50 => 500m,
                    < 100 => 1200m,
                    _ => 2500m
                };
                currentSession.DailyTarget = dailyTarget;

                // Exit logic
                if (activeContracts > 0)
                {
                    barsHeld++;
                    var priceChange = bar.Close - entryPrice;

                    bool shouldExit = false;
                    string? exitType = null;

                    if (isTrending)
                    {
                        // Trending: +3 points or stop at -1 point
                        if (priceChange >= 3m)
                        {
                            shouldExit = true;
                            exitType = "Profit Target";
                        }
                        else if (priceChange <= -1m || barsHeld >= 30)
                        {
                            shouldExit = true;
                            exitType = priceChange <= -1m ? "Stop Loss" : "Profit Target";
                        }
                    }
                    else
                    {
                        // Ranging: +0.25 points or stop at -0.1 points
                        if (priceChange >= 0.25m)
                        {
                            shouldExit = true;
                            exitType = "Profit Target";
                        }
                        else if (priceChange <= -0.1m || barsHeld >= 3)
                        {
                            shouldExit = true;
                            exitType = priceChange <= -0.1m ? "Stop Loss" : "Profit Target";
                        }
                    }

                    if (shouldExit && barsHeld >= 1)
                    {
                        var pnl = priceChange * MNQ_TICK_VALUE * activeContracts;
                        currentEquity += pnl;

                        var trade = new Trade
                        {
                            EntryTime = entryTime,
                            ExitTime = bar.TimestampUtc.DateTime,
                            EntryPrice = entryPrice,
                            ExitPrice = bar.Close,
                            Contracts = activeContracts,
                            PnL = pnl,
                            ExitType = exitType
                        };

                        allTrades.Add(trade);
                        currentSession.Trades.Add(trade);
                        currentSession.TotalTrades++;
                        if (exitType == "Profit Target") currentSession.ProfitTargetExits++;
                        else currentSession.StopLossExits++;

                        activeContracts = 0;
                        barsHeld = 0;
                    }
                }

                // Entry logic
                if (activeContracts == 0 && recentBars.Count >= 10 && currentSession.TotalTrades < 50)
                {
                    var closes = recentBars.Select(b => b.Close).ToList();
                    var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
                    var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();

                    bool shouldEnter = false;
                    if (isTrending)
                    {
                        shouldEnter = avg5 > avg10 && bar.Close > avg5;
                    }
                    else
                    {
                        shouldEnter = Math.Abs(bar.Close - avg10) < atr * 0.5m;
                    }

                    if (shouldEnter)
                    {
                        entryPrice = bar.Close;
                        entryTime = bar.TimestampUtc.DateTime;
                        activeContracts = _contractSize;
                        barsHeld = 0;
                    }
                }

                // Drawdown tracking
                if (currentEquity > peakEquity) peakEquity = currentEquity;
                var dd = peakEquity - currentEquity;
                if (dd > maxDrawdown) maxDrawdown = dd;

                if (maxDrawdown >= 1000m) break;
            }

            if (currentSession.Trades.Count > 0)
            {
                currentSession.ClosingEquity = currentEquity;
                sessions.Add(currentSession);
            }

            DisplayResults(sessions, allTrades, currentEquity, maxDrawdown);
        }

        private void DisplayResults(List<DailySession> sessions, List<Trade> allTrades, decimal finalEquity, decimal maxDrawdown)
        {
            Console.WriteLine("\n" + new string('═', 130));
            Console.WriteLine($"DAILY SESSION RESULTS ({_contractSize} Contract(s))");
            Console.WriteLine(new string('═', 130));
            Console.WriteLine("{0,-12} {1,10} {2,12} {3,12} {4,12} {5,8} {6,10} {7,-15}",
                "Date", "Opening", "Closing", "Daily P&L", "Daily Target", "Trades", "PT/SL", "Target Hit?");
            Console.WriteLine(new string('─', 130));

            var targetHitCount = 0;
            foreach (var session in sessions)
            {
                var pnlStr = session.DailyPnL >= 0 ? $"+${session.DailyPnL:F0}" : $"-${Math.Abs(session.DailyPnL):F0}";
                var ptSLStr = $"{session.ProfitTargetExits}✅/{session.StopLossExits}❌";
                var hitStr = session.HitDailyTarget ? "✅ YES" : "❌ NO";
                if (session.HitDailyTarget) targetHitCount++;

                Console.WriteLine("{0,-12} ${1,9:F0} ${2,11:F0} {3,12} ${4,11:F0} {5,8} {6,10} {7,-15}",
                    session.Date.ToString("MM-dd-yyyy"),
                    session.OpeningEquity,
                    session.ClosingEquity,
                    pnlStr,
                    session.DailyTarget,
                    session.TotalTrades,
                    ptSLStr,
                    hitStr);
            }

            Console.WriteLine(new string('═', 130));

            Console.WriteLine("\n" + new string('═', 130));
            Console.WriteLine($"OVERALL STATISTICS ({_contractSize} Contract(s))");
            Console.WriteLine(new string('═', 130));

            var totalPnL = finalEquity - 50000m;
            var totalTrades = allTrades.Count;
            var winningTrades = allTrades.Count(t => t.IsWin);
            var profitTargetHits = allTrades.Count(t => t.ExitType == "Profit Target");
            var stopLossHits = allTrades.Count(t => t.ExitType == "Stop Loss");
            var winRate = totalTrades > 0 ? (double)winningTrades / totalTrades * 100 : 0;
            var avgPnL = totalTrades > 0 ? totalPnL / totalTrades : 0;
            var sharpe = CalculateSharpe(allTrades);

            Console.WriteLine($"Account Size:          ${50000:F0}");
            Console.WriteLine($"Contract Size:         {_contractSize}");
            Console.WriteLine($"Final Equity:          ${finalEquity:F0}");
            Console.WriteLine($"Total P&L:             ${totalPnL:F0} ({totalPnL / 50000m * 100:F2}%)");
            Console.WriteLine($"Max Drawdown:          ${maxDrawdown:F0}");
            Console.WriteLine($"Total Trades:          {totalTrades}");
            Console.WriteLine($"Winning Trades:        {winningTrades} ({winRate:F1}%)");
            Console.WriteLine($"  Profit Target Hits:  {profitTargetHits} ✅ ({(totalTrades > 0 ? profitTargetHits * 100 / totalTrades : 0)}%)");
            Console.WriteLine($"  Stop Loss Hits:      {stopLossHits} ❌ ({(totalTrades > 0 ? stopLossHits * 100 / totalTrades : 0)}%)");
            Console.WriteLine($"Avg P&L per Trade:     ${avgPnL:F0}");
            Console.WriteLine($"Sharpe Ratio:          {sharpe:F2}");
            Console.WriteLine($"Daily Targets Hit:     {targetHitCount}/{sessions.Count} ({(targetHitCount * 100 / Math.Max(sessions.Count, 1))}%)");
            Console.WriteLine($"Average Daily P&L:     ${sessions.Average(s => s.DailyPnL):F0}");
            Console.WriteLine(new string('═', 130));

            // Validation
            Console.WriteLine("\n" + new string('═', 130));
            Console.WriteLine("LOCKED SPECIFICATION VALIDATION");
            Console.WriteLine(new string('═', 130));

            var passesWinRate = winRate >= 60;
            var passesDD = maxDrawdown < 1000;
            var passesSharpe = sharpe >= 1.0;
            var passesDaily = targetHitCount > sessions.Count / 2;

            Console.WriteLine($"Win Rate ≥ 60%:        {(passesWinRate ? "✅ PASS" : "❌ FAIL")} ({winRate:F1}%)");
            Console.WriteLine($"Max DD < $1000:        {(passesDD ? "✅ PASS" : "❌ FAIL")} (${maxDrawdown:F0})");
            Console.WriteLine($"Sharpe ≥ 1.0:          {(passesSharpe ? "✅ PASS" : "❌ FAIL")} ({sharpe:F2})");
            Console.WriteLine($"Daily Target Hit:      {(passesDaily ? "✅ PASS" : "❌ FAIL")} ({targetHitCount}/{sessions.Count} days)");
            Console.WriteLine($"Avg Daily $500+:       {(sessions.Average(s => s.DailyPnL) >= 500 ? "✅ PASS" : "❌ FAIL")} (${sessions.Average(s => s.DailyPnL):F0})");
            Console.WriteLine(new string('═', 130));
        }

        private decimal CalculateAtr(List<HistoricalBar> bars)
        {
            if (bars.Count < 14) return 1m;

            var trueRanges = new List<decimal>();
            for (int i = 1; i < bars.Count; i++)
            {
                var high = bars[i].High;
                var low = bars[i].Low;
                var prevClose = bars[i - 1].Close;
                var tr = Math.Max(high - low, Math.Max(Math.Abs(high - prevClose), Math.Abs(low - prevClose)));
                trueRanges.Add(tr);
            }

            return trueRanges.TakeLast(14).Average();
        }

        private decimal CalculateAdx(List<HistoricalBar> bars)
        {
            if (bars.Count < 14) return 0m;

            var range = bars.Last().High - bars.Last().Low;
            var avgRange = bars.TakeLast(14).Average(b => b.High - b.Low);

            return avgRange > 0 ? (range / avgRange) * 100m : 0m;
        }

        private double CalculateSharpe(List<Trade> trades)
        {
            if (trades.Count < 2) return 0;

            var returns = trades.Select(t => (double)t.PnL).ToList();
            var avg = returns.Average();
            var variance = returns.Sum(r => Math.Pow(r - avg, 2)) / returns.Count;
            var stdDev = Math.Sqrt(variance);

            return stdDev > 0 ? avg / stdDev * Math.Sqrt(252) : 0;
        }

        public static void Main(string[] args)
        {
            // Test with 1, 2, and 3 contracts
            foreach (var contracts in new[] { 1, 2, 3 })
            {
                var tester = new UnifiedSystemTester(contracts);
                tester.RunTest();
                Console.WriteLine("\n\n");
            }
        }
    }
}
