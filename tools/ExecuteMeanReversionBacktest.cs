using System;
using System.Collections.Generic;
using System.Linq;

// Inline execution of Mean Reversion backtest
class MeanReversionQuickTest
{
    public struct Bar
    {
        public double Open, High, Low, Close;
        public long Volume;
        public DateTime Time;
    }

    static void Main()
    {
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("MEAN REVERSION SCALPER - AUTOMATED BACKTEST");
        Console.WriteLine("================================================================================\n");

        // Generate 5,000 bars (11 trading days)
        Console.WriteLine("Generating historical data (5,000 bars)...");
        var bars = GenerateMockBars(5000);
        Console.WriteLine($"✅ Generated {bars.Count} bars\n");

        // Run backtest
        Console.WriteLine("Running backtest...\n");
        var results = RunBacktest(bars);

        // Print results
        Console.WriteLine("================================================================================");
        Console.WriteLine("BACKTEST RESULTS");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"Total Trades:        {results["totalTrades"]}");
        Console.WriteLine($"Winning Trades:      {results["winTrades"]}");
        Console.WriteLine($"Losing Trades:       {results["lossTrades"]}");
        Console.WriteLine($"Win Rate:            {results["winRate"]:F1}%");
        Console.WriteLine($"Gross P&L:           ${results["grossPnL"]:F2}");
        Console.WriteLine($"Avg Win:             ${results["avgWin"]:F2}");
        Console.WriteLine($"Avg Loss:            ${results["avgLoss"]:F2}");
        Console.WriteLine("================================================================================\n");

        // Daily contribution
        int tradingDays = 11; // 5000 bars / ~390 bars per day
        double dailyPnL = results["grossPnL"] / tradingDays;

        Console.WriteLine("================================================================================");
        Console.WriteLine("DAILY CONTRIBUTION ANALYSIS");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"Gross P&L (11 days): ${results["grossPnL"]:F2}");
        Console.WriteLine($"AVG DAILY P&L:       ${dailyPnL:F2}");
        Console.WriteLine($"Win Rate:            {results["winRate"]:F1}%");
        Console.WriteLine("================================================================================\n");

        Console.WriteLine($"✅ MEAN REVERSION DAILY CONTRIBUTION: ${dailyPnL:F2}");
        Console.WriteLine($"✅ TREND FOLLOWER DAILY TARGET:      $555.00");
        Console.WriteLine($"✅ COMBINED DAILY TARGET:             ${555 + dailyPnL:F2}\n");
    }

    static List<Bar> GenerateMockBars(int count)
    {
        var bars = new List<Bar>();
        var random = new Random(42);
        double price = 29900;
        var time = new DateTime(2024, 1, 1, 9, 30, 0);

        for (int i = 0; i < count; i++)
        {
            double o = price + (random.NextDouble() * 2 - 1);
            double h = o + Math.Abs(random.NextDouble() * 3);
            double l = o - Math.Abs(random.NextDouble() * 3);
            double c = l + (random.NextDouble() * (h - l));
            long v = (long)(10000 + random.NextDouble() * 5000);

            bars.Add(new Bar { Open = o, High = h, Low = l, Close = c, Volume = v, Time = time });
            price = c;
            time = time.AddMinutes(1);

            if (time.Hour >= 16)
                time = time.AddDays(1).AddHours(-7).AddMinutes(-30);
        }
        return bars;
    }

    static Dictionary<string, double> RunBacktest(List<Bar> bars)
    {
        var trades = new List<(double entry, double exit, double pnl, bool isWin)>();
        double pnl = 0;
        bool hasPosition = false;
        double entryPrice = 0;
        int entriesThisDay = 0;

        for (int i = 2; i < bars.Count; i++)
        {
            if (hasPosition)
            {
                // Check exit: +0.5 or -0.5
                double change = bars[i].Close - entryPrice;
                if (change >= 0.5 || change <= -0.5)
                {
                    double tradeP = change * 20; // $20 per point
                    pnl += tradeP;
                    trades.Add((entryPrice, bars[i].Close, tradeP, tradeP > 0));
                    hasPosition = false;
                }
            }

            // Entry: close above 2-bar high + 1.2x volume
            if (!hasPosition && entriesThisDay < 5)
            {
                double high2bar = Math.Max(bars[i-2].High, bars[i-1].High);
                double vol5bar = (bars[i-1].Volume + bars[i-2].Volume + bars[i-3].Volume + bars[i-4].Volume + bars[i-5].Volume) / 5.0;
                
                if (bars[i].Close > high2bar && bars[i].Volume > vol5bar * 1.2)
                {
                    entryPrice = bars[i].Close;
                    hasPosition = true;
                    entriesThisDay++;
                }
            }

            // Reset daily counter
            if (i > 0 && bars[i].Time.Hour < bars[i-1].Time.Hour)
                entriesThisDay = 0;
        }

        return new Dictionary<string, double>
        {
            { "totalTrades", trades.Count },
            { "winTrades", trades.Count(t => t.isWin) },
            { "lossTrades", trades.Count(t => !t.isWin) },
            { "winRate", trades.Count > 0 ? (double)trades.Count(t => t.isWin) / trades.Count * 100 : 0 },
            { "grossPnL", pnl },
            { "avgWin", trades.Where(t => t.isWin).Any() ? trades.Where(t => t.isWin).Average(t => t.pnl) : 0 },
            { "avgLoss", trades.Where(t => !t.isWin).Any() ? trades.Where(t => !t.isWin).Average(t => t.pnl) : 0 }
        };
    }
}
