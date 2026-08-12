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

        /// <summary>
        /// P&amp;L bucketed by trading day, keyed on the day the trade CLOSED.
        /// The mean alone is misleading: a $900 average can be five $300 days and one
        /// $3,900 day. Every distribution metric below reads off this.
        /// </summary>
        public IReadOnlyList<decimal> DailyPnL
        {
            get
            {
                if (Trades.Count == 0) return System.Array.Empty<decimal>();
                return Trades
                    .GroupBy(t => t.TradingDay)
                    .OrderBy(g => g.Key)
                    .Select(g => g.Sum(t => t.PnL))
                    .ToList();
            }
        }

        public int TradingDays => DailyPnL.Count;

        public decimal AvgDailyPnL =>
            TradingDays > 0 ? DailyPnL.Sum() / TradingDays : 0m;

        /// <summary>Middle day. Less flattered by one outsized session than the mean.</summary>
        public decimal MedianDailyPnL
        {
            get
            {
                var d = DailyPnL.OrderBy(x => x).ToList();
                if (d.Count == 0) return 0m;
                return d.Count % 2 == 1
                    ? d[d.Count / 2]
                    : (d[d.Count / 2 - 1] + d[d.Count / 2]) / 2m;
            }
        }

        public decimal BestDay => DailyPnL.Count > 0 ? DailyPnL.Max() : 0m;
        public decimal WorstDay => DailyPnL.Count > 0 ? DailyPnL.Min() : 0m;

        public int LosingDays => DailyPnL.Count(d => d < 0);

        /// <summary>
        /// END-OF-DAY trailing drawdown - the measure prop firms actually enforce when
        /// the threshold trails on closing balance rather than intraday equity.
        /// The kill line follows the highest CLOSING balance, so an intraday dip that
        /// recovers before the bell costs nothing. This is far smaller than MaxDrawdown
        /// (which is intraday peak-to-trough) and is the number to size against.
        /// The peak floors at 0 because the account starts at its high-water mark.
        /// </summary>
        public decimal EodTrailingDrawdown
        {
            get
            {
                decimal cum = 0m, peak = 0m, worst = 0m;
                foreach (var day in DailyPnL)
                {
                    cum += day;
                    if (cum > peak) peak = cum;
                    var dd = peak - cum;
                    if (dd > worst) worst = dd;
                }
                return worst;
            }
        }

        /// <summary>
        /// Would this configuration have killed the account? True if EOD trailing
        /// drawdown ever exceeded the firm's threshold. A config that "earns" $50,000
        /// after breaching this earned nothing - the account was already closed.
        /// </summary>
        public bool AccountBlown(decimal threshold) => EodTrailingDrawdown >= threshold;

        /// <summary>
        /// Share of trading days clearing the target. This, not the average, is the
        /// answer to "can this make $500 a day?" - a strategy averaging $900 that
        /// clears $500 on only a third of days is not a $500/day strategy.
        /// </summary>
        public double PctDaysAbove(decimal target) =>
            TradingDays > 0
                ? (double)DailyPnL.Count(d => d >= target) / TradingDays * 100.0
                : 0.0;

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
