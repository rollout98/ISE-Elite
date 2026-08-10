using System;
using System.Collections.Generic;
using System.Linq;
using ISE.Systems;

namespace ISE.Tools
{
    /// <summary>
    /// Mean Reversion System Backtest Tester
    /// </summary>
    public class MeanReversionTester
    {
        public class TradeResult
        {
            public int EntryBar { get; set; }
            public int ExitBar { get; set; }
            public double EntryPrice { get; set; }
            public double ExitPrice { get; set; }
            public double PnL { get; set; }
            public bool IsWin { get; set; }
            public string Direction { get; set; }
            public string ExitReason { get; set; }
        }

        public class BacktestResults
        {
            public int TotalTrades { get; set; }
            public int WinningTrades { get; set; }
            public int LosingTrades { get; set; }
            public double WinRate { get; set; }
            public double GrossPnL { get; set; }
            public double AvgWin { get; set; }
            public double AvgLoss { get; set; }
            public double MaxConsecutiveWins { get; set; }
            public double MaxDrawdown { get; set; }
            public List<TradeResult> Trades { get; set; }
        }

        private MeanReversionSignal signal = new MeanReversionSignal();
        private List<TradeResult> trades = new List<TradeResult>();
        private double peakEquity = 0;
        private double currentEquity = 0;

        public BacktestResults RunBacktest(List<MeanReversionSignal.Bar> bars)
        {
            signal.Clear();
            trades.Clear();
            peakEquity = 0;
            currentEquity = 0;

            // Add all bars to signal
            foreach (var bar in bars)
            {
                signal.AddBar(bar);
            }

            // Simulate trading through each bar
            TradeResult activeTrade = null;

            for (int i = 2; i < bars.Count; i++)
            {
                var currentBar = bars[i];
                var signalResult = signal.GetSignal(i);

                // Check if active trade should exit
                if (activeTrade != null)
                {
                    if (signal.ShouldExit(currentBar.Close, activeTrade.EntryPrice, 
                        activeTrade.ExitPrice, activeTrade.EntryPrice, activeTrade.Direction))
                    {
                        activeTrade.ExitBar = i;
                        activeTrade.ExitPrice = currentBar.Close;
                        activeTrade.PnL = signal.CalculatePnL(activeTrade.EntryPrice, 
                            activeTrade.ExitPrice, activeTrade.Direction, 1);
                        activeTrade.IsWin = activeTrade.PnL > 0;

                        // Determine exit reason
                        if (activeTrade.Direction == "Long")
                        {
                            if (currentBar.Close >= (activeTrade.EntryPrice + 0.5))
                                activeTrade.ExitReason = "Target Hit";
                            else
                                activeTrade.ExitReason = "Stop Hit";
                        }
                        else
                        {
                            if (currentBar.Close <= (activeTrade.EntryPrice - 0.5))
                                activeTrade.ExitReason = "Target Hit";
                            else
                                activeTrade.ExitReason = "Stop Hit";
                        }

                        trades.Add(activeTrade);
                        currentEquity += activeTrade.PnL;
                        peakEquity = Math.Max(peakEquity, currentEquity);

                        activeTrade = null;
                    }
                }

                // Check for new entry signal
                if (activeTrade == null && signalResult.HasSignal)
                {
                    activeTrade = new TradeResult
                    {
                        EntryBar = i,
                        EntryPrice = signalResult.EntryPrice,
                        Direction = signalResult.Direction,
                        ExitPrice = signalResult.TargetPrice
                    };
                }
            }

            // Calculate results
            return CalculateResults();
        }

        private BacktestResults CalculateResults()
        {
            var results = new BacktestResults
            {
                TotalTrades = trades.Count,
                WinningTrades = trades.Count(t => t.IsWin),
                LosingTrades = trades.Count(t => !t.IsWin),
                Trades = trades,
                GrossPnL = trades.Sum(t => t.PnL)
            };

            if (results.TotalTrades > 0)
            {
                results.WinRate = (double)results.WinningTrades / results.TotalTrades * 100;
                var winTrades = trades.Where(t => t.IsWin).ToList();
                var lossTrades = trades.Where(t => !t.IsWin).ToList();

                results.AvgWin = winTrades.Count > 0 ? winTrades.Average(t => t.PnL) : 0;
                results.AvgLoss = lossTrades.Count > 0 ? lossTrades.Average(t => t.PnL) : 0;
            }

            results.MaxDrawdown = peakEquity - (peakEquity - Math.Abs(trades.Where(t => !t.IsWin).Sum(t => t.PnL)));

            return results;
        }

        public void PrintResults(BacktestResults results)
        {
            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("MEAN REVERSION BACKTEST RESULTS");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Total Trades:        {results.TotalTrades}");
            Console.WriteLine($"Winning Trades:      {results.WinningTrades}");
            Console.WriteLine($"Losing Trades:       {results.LosingTrades}");
            Console.WriteLine($"Win Rate:            {results.WinRate:F1}%");
            Console.WriteLine($"Gross P&L:           ${results.GrossPnL:F2}");
            Console.WriteLine($"Avg Win:             ${results.AvgWin:F2}");
            Console.WriteLine($"Avg Loss:            ${results.AvgLoss:F2}");
            Console.WriteLine($"Max Drawdown:        ${results.MaxDrawdown:F2}");
            Console.WriteLine(new string('=', 60) + "\n");
        }
    }
}
