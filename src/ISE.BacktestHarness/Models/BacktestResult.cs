using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.BacktestHarness.Models
{
    /// <summary>
    /// Complete results from a single backtest configuration
    /// </summary>
    public sealed class BacktestResult
    {
        public BacktestResult(
            BacktestConfiguration config,
            IReadOnlyList<BacktestTrade> trades,
            decimal startingEquity,
            decimal endingEquity,
            decimal maxDrawdown,
            decimal dailyDrawdown,
            DateTime periodStart,
            DateTime periodEnd)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Trades = trades ?? throw new ArgumentNullException(nameof(trades));
            StartingEquity = startingEquity;
            EndingEquity = endingEquity;
            MaxDrawdown = maxDrawdown;
            DailyDrawdown = dailyDrawdown;
            PeriodStart = periodStart;
            PeriodEnd = periodEnd;
        }

        public BacktestConfiguration Config { get; }
        public IReadOnlyList<BacktestTrade> Trades { get; }
        public decimal StartingEquity { get; }
        public decimal EndingEquity { get; }
        public decimal MaxDrawdown { get; }
        public decimal DailyDrawdown { get; }
        public DateTime PeriodStart { get; }
        public DateTime PeriodEnd { get; }

        // Computed metrics
        public decimal GrossProfit => EndingEquity - StartingEquity;
        public decimal ReturnPercent => (GrossProfit / StartingEquity) * 100m;
        public int TotalTrades => Trades.Count;
        public int WinningTrades => Trades.Count(t => t.IsWin);
        public int LosingTrades => TotalTrades - WinningTrades;
        public double WinRate => TotalTrades > 0 ? (double)WinningTrades / TotalTrades * 100.0 : 0.0;
        public decimal AveragePnL => TotalTrades > 0 ? Trades.Sum(t => t.PnL) / TotalTrades : 0m;
        public decimal LargestWin
        {
            get
            {
                var wins = Trades.Where(t => t.IsWin).ToList();
                return wins.Count > 0 ? wins.Max(t => t.PnL) : 0m;
            }
        }
        public decimal LargestLoss
        {
            get
            {
                var losses = Trades.Where(t => !t.IsWin).ToList();
                return losses.Count > 0 ? losses.Min(t => t.PnL) : 0m;
            }
        }
        public decimal TotalSlippage => Trades.Sum(t => t.Slippage);

        // Risk metrics
        public double ProfitFactor => LosingTrades > 0 
            ? (double)(Trades.Where(t => t.IsWin).Sum(t => t.PnL) / 
                       Math.Abs(Trades.Where(t => !t.IsWin).Sum(t => t.PnL)))
            : (TotalTrades > 0 ? double.PositiveInfinity : 0.0);

        // Sharpe approximation (annualized)
        public double SharpeRatio
        {
            get
            {
                if (TotalTrades < 2) return 0.0;
                var returns = Trades.Select(t => (double)t.PnL).ToList();
                var mean = returns.Average();
                var variance = returns.Sum(r => Math.Pow(r - mean, 2)) / returns.Count;
                var stdDev = Math.Sqrt(variance);
                if (stdDev == 0) return 0.0;
                // Annualize: ~250 trading days, assume ~100 trades/month = 1200/year
                var annualizedReturn = mean * 252; // approximate trading days
                return annualizedReturn / stdDev;
            }
        }

        public override string ToString()
        {
            return $"{Config} | " +
                   $"Profit: ${GrossProfit:F2} ({ReturnPercent:F2}%) | " +
                   $"Trades: {TotalTrades} ({WinRate:F1}% win) | " +
                   $"DD: ${MaxDrawdown:F2} | Sharpe: {SharpeRatio:F2}";
        }
    }
}
