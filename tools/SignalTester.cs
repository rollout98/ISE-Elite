using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BacktestHarness;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestTools
{
    /// <summary>
    /// In-memory signal tester - evaluate multiple strategies without rebuilding
    /// </summary>
    public class SignalTester
    {
        private readonly List<HistoricalBar> _bars;
        private readonly BacktestConfiguration _config;

        public SignalTester()
        {
            // Load mock data once
            _bars = GenerateMockBars();
            _config = new BacktestConfiguration(
                configId: 1,
                maximumContracts: 1, 
                adaptiveRiskMultiplier: 1.0,
                stopDistanceRisk: 20,
                liquidityCapacity: 100
            );
        }

        private List<HistoricalBar> GenerateMockBars()
        {
            var bars = new List<HistoricalBar>();
            var startDate = new DateTime(2024, 3, 1, 9, 30, 0, DateTimeKind.Utc);
            var price = 5000m;

            for (int i = 0; i < 5000; i++)
            {
                var noise = (decimal)(new Random(i).NextDouble() * 2 - 1);
                var trend = (decimal)(Math.Sin(i * 0.01) * 0.5);
                var change = (noise + trend) * 0.5m;
                price += change;

                var bar = new HistoricalBar(
                    i < 2500 ? "MNQ" : "MGC",
                    i < 2500 ? "MNQ" : "MGC",
                    startDate.AddMinutes(i),
                    startDate.AddDays(i / 480).Date,
                    60,
                    price - 1, price + 1, price - 2, price,
                    1000L,
                    sourceKind: HistoricalDataSourceKind.NinjaTraderProvider,
                    sourceName: "MockData"
                );
                bars.Add(bar);
            }

            return bars.OrderBy(b => b.TimestampUtc).ToList();
        }

        public void RunComparison()
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════╗");
            Console.WriteLine("║        ISE-Elite Signal Strategy Comparison         ║");
            Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

            var strategies = new Dictionary<string, Func<HistoricalBar, List<HistoricalBar>, int, string>>
            {
                ["Fixed Schedule (250-bar)"] = SignalFixedSchedule,
                ["5-bar Momentum"] = SignalMomentum5Bar,
                ["Price Above Average"] = SignalPriceAboveAvg,
                ["Trend Following"] = SignalTrendFollowing,
            };

            var results = new List<(string Strategy, int Trades, decimal PnL, double WinRate, decimal MaxDD, double Sharpe)>();

            foreach (var (name, strategy) in strategies)
            {
                Console.WriteLine($"Testing: {name}...");
                var result = BacktestStrategy(strategy);
                results.Add((name, result.Trades, result.PnL, result.WinRate, result.MaxDD, result.Sharpe));
            }

            // Display results
            Console.WriteLine("\n" + new string('═', 110));
            Console.WriteLine("RESULTS SUMMARY");
            Console.WriteLine(new string('═', 110));
            Console.WriteLine("{0,-30} {1,8} {2,12} {3,10} {4,12} {5,10}", 
                "Strategy", "Trades", "P&L", "Win Rate", "Max DD", "Sharpe");
            Console.WriteLine(new string('─', 110));

            foreach (var (strat, trades, pnl, wr, dd, sharpe) in results.OrderByDescending(r => r.PnL))
            {
                var pnlStr = pnl >= 0 ? $"+${pnl:N0}" : $"-${Math.Abs(pnl):N0}";
                var wrStr = $"{wr:F1}%";
                var ddStr = $"-${dd:N0}";
                Console.WriteLine("{0,-30} {1,8} {2,12} {3,10} {4,12} {5,10:F2}", 
                    strat, trades, pnlStr, wrStr, ddStr, sharpe);
            }
            Console.WriteLine(new string('═', 110));
        }

        private (int Trades, decimal PnL, double WinRate, decimal MaxDD, double Sharpe) BacktestStrategy(
            Func<HistoricalBar, List<HistoricalBar>, int, string> signalFunc)
        {
            var trades = new List<BacktestTrade>();
            var recentBars = new List<HistoricalBar>();
            int activeContracts = 0;
            decimal entryPrice = 0;
            decimal currentEquity = 50000;
            decimal peakEquity = 50000;
            decimal maxDD = 0;
            var pnlList = new List<decimal>();
            int barIndex = 0;

            foreach (var bar in _bars)
            {
                recentBars.Add(bar);
                if (recentBars.Count > 20) recentBars.RemoveAt(0);

                var signal = signalFunc(bar, recentBars, barIndex);

                // Exit logic
                if (activeContracts > 0)
                {
                    if (signal == "EXIT" || bar.Close >= entryPrice + 1m)
                    {
                        // Winning trade
                        var pnl = (bar.Close - entryPrice) * 20m * activeContracts;
                        trades.Add(new BacktestTrade(
                            entryTimeUtc: recentBars[recentBars.Count - 2].TimestampUtc.DateTime,
                            exitTimeUtc: bar.TimestampUtc.DateTime,
                            direction: "LONG",
                            entryPrice: entryPrice,
                            exitPrice: bar.Close,
                            contracts: activeContracts,
                            pnl: pnl,
                            slippage: 0m
                        ));
                        currentEquity += pnl;
                        pnlList.Add(pnl);
                        activeContracts = 0;
                    }
                    else if (bar.Close <= entryPrice - 1m)
                    {
                        // Losing trade
                        var pnl = (bar.Close - entryPrice) * 20m * activeContracts;
                        trades.Add(new BacktestTrade(
                            entryTimeUtc: recentBars[recentBars.Count - 2].TimestampUtc.DateTime,
                            exitTimeUtc: bar.TimestampUtc.DateTime,
                            direction: "LONG",
                            entryPrice: entryPrice,
                            exitPrice: bar.Close,
                            contracts: activeContracts,
                            pnl: pnl,
                            slippage: 0m
                        ));
                        currentEquity += pnl;
                        pnlList.Add(pnl);
                        activeContracts = 0;
                    }
                }

                // Entry logic
                if (activeContracts == 0 && signal == "BUY")
                {
                    entryPrice = bar.Close;
                    activeContracts = 1;
                }

                // Drawdown tracking
                if (currentEquity > peakEquity) peakEquity = currentEquity;
                var dd = peakEquity - currentEquity;
                if (dd > maxDD) maxDD = dd;

                barIndex++;
            }

            var totalTrades = trades.Count;
            var winRate = totalTrades > 0 ? (double)trades.Count(t => t.IsWin) / totalTrades * 100 : 0;
            var totalPnL = currentEquity - 50000;
            var sharpe = pnlList.Count > 0 ? CalculateSharpe(pnlList) : 0;

            return (totalTrades, totalPnL, winRate, maxDD, sharpe);
        }

        private double CalculateSharpe(List<decimal> returns)
        {
            if (returns.Count < 2) return 0;
            var avg = returns.Average();
            var variance = returns.Sum(r => (double)((r - avg) * (r - avg))) / returns.Count;
            var stdDev = Math.Sqrt(variance);
            return stdDev > 0 ? (double)avg / stdDev * Math.Sqrt(252) : 0;
        }

        // Signal strategies - evaluate current bar + history + position
        private string SignalFixedSchedule(HistoricalBar bar, List<HistoricalBar> recentBars, int barIndex)
        {
            // Trade every 250 bars
            return (barIndex > 0 && barIndex % 250 == 0) ? "BUY" : "NONE";
        }

        private string SignalMomentum5Bar(HistoricalBar bar, List<HistoricalBar> recentBars, int barIndex)
        {
            if (recentBars.Count < 5) return "NONE";
            var closes = recentBars.Select(b => b.Close).ToList();
            var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
            var prevClose = recentBars[recentBars.Count - 2].Close;
            return bar.Close > avg5 && bar.Close > prevClose ? "BUY" : "NONE";
        }

        private string SignalPriceAboveAvg(HistoricalBar bar, List<HistoricalBar> recentBars, int barIndex)
        {
            if (recentBars.Count < 10) return "NONE";
            var closes = recentBars.Select(b => b.Close).ToList();
            var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();
            return bar.Close > avg10 * 1.001m ? "BUY" : "NONE";
        }

        private string SignalTrendFollowing(HistoricalBar bar, List<HistoricalBar> recentBars, int barIndex)
        {
            if (recentBars.Count < 10) return "NONE";
            var closes = recentBars.Select(b => b.Close).ToList();
            var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
            var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();
            return avg5 > avg10 && bar.Close > avg5 ? "BUY" : "NONE";
        }

        public static void Main(string[] args)
        {
            var tester = new SignalTester();
            tester.RunComparison();
        }
    }
}
