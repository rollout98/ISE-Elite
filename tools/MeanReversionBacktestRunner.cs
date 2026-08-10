using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ISE.Systems;

namespace ISE.Tools
{
    /// <summary>
    /// Mean Reversion Backtest Runner
    /// Automatically loads historical data and runs backtest
    /// </summary>
    public class MeanReversionBacktestRunner
    {
        public void Run(List<MeanReversionSignal.Bar> historicalBars)
        {
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("MEAN REVERSION SCALPER - AUTOMATED BACKTEST");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"Historical Bars: {historicalBars.Count}");
            Console.WriteLine($"Date Range: {historicalBars.First().Time:O} to {historicalBars.Last().Time:O}");
            Console.WriteLine();

            // Run backtest
            Console.WriteLine("Running backtest...");
            var sw = Stopwatch.StartNew();
            
            var tester = new MeanReversionTester();
            var results = tester.RunBacktest(historicalBars);
            
            sw.Stop();

            // Print results
            tester.PrintResults(results);
            Console.WriteLine($"Backtest completed in {sw.Elapsed.TotalMilliseconds:F0}ms\n");

            // Daily P&L calculation (assuming 11 trading days in data)
            int tradingDays = historicalBars.Count / 390; // ~390 bars per trading day on 1-min
            if (tradingDays < 1) tradingDays = 1;

            double avgDailyPnL = results.GrossPnL / tradingDays;
            
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("DAILY CONTRIBUTION ANALYSIS");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"Gross P&L:           ${results.GrossPnL:F2}");
            Console.WriteLine($"Trading Days:        {tradingDays}");
            Console.WriteLine($"Avg Daily P&L:       ${avgDailyPnL:F2}");
            Console.WriteLine($"Win Rate:            {results.WinRate:F1}%");
            Console.WriteLine(new string('=', 80) + "\n");

            // Print sample trades
            if (results.Trades.Count > 0)
            {
                Console.WriteLine("Sample Trades (first 10):");
                Console.WriteLine(new string('-', 100));
                Console.WriteLine("{0,-10} {1,-10} {2,-12} {3,-12} {4,-8} {5,-15}", 
                    "Bar", "Direction", "Entry", "Exit", "P&L", "Reason");
                Console.WriteLine(new string('-', 100));
                
                foreach (var trade in results.Trades.Take(10))
                {
                    Console.WriteLine("{0,-10} {1,-10} {2,-12:F2} {3,-12:F2} {4,-8:F0} {5,-15}", 
                        trade.EntryBar, trade.Direction, trade.EntryPrice, trade.ExitPrice, trade.PnL, trade.ExitReason);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Generate mock bars for testing (same structure as real data)
        /// </summary>
        public static List<MeanReversionSignal.Bar> GenerateMockBars(int count = 5000)
        {
            var bars = new List<MeanReversionSignal.Bar>();
            var random = new Random(42); // Fixed seed for reproducibility
            
            double currentPrice = 29900;
            DateTime currentTime = new DateTime(2024, 1, 1, 9, 30, 0);

            for (int i = 0; i < count; i++)
            {
                // Generate realistic OHLC with random walk
                double dailyMove = random.NextDouble() * 2 - 1; // -1 to +1
                double open = currentPrice + (random.NextDouble() * 2 - 1);
                double high = open + Math.Abs(random.NextDouble() * 3);
                double low = open - Math.Abs(random.NextDouble() * 3);
                double close = low + (random.NextDouble() * (high - low));

                long volume = (long)(10000 + random.NextDouble() * 5000);

                bars.Add(new MeanReversionSignal.Bar
                {
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume,
                    Time = currentTime
                });

                currentPrice = close;
                currentTime = currentTime.AddMinutes(1);

                // Skip weekends
                if (currentTime.DayOfWeek == DayOfWeek.Saturday)
                    currentTime = currentTime.AddDays(2);
                else if (currentTime.DayOfWeek == DayOfWeek.Sunday)
                    currentTime = currentTime.AddDays(1);

                // Reset to 9:30 AM on next day at 4 PM
                if (currentTime.Hour >= 16)
                    currentTime = currentTime.AddDays(1).AddHours(-7).AddMinutes(-30);
            }

            return bars;
        }
    }
}
