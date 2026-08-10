using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;
using ISE.TradingBrain;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Executes a backtest by feeding historical bars through IntegratedTradingBrain
    /// </summary>
    public sealed class BacktestEngine
    {
        private readonly decimal _accountSize;
        private const decimal MNQ_TICK_VALUE = 20m; // $20 per tick
        private const decimal MGC_TICK_VALUE = 10m; // $10 per tick

        public BacktestEngine(decimal accountSize = 50000m)
        {
            if (accountSize <= 0) throw new ArgumentOutOfRangeException(nameof(accountSize));
            _accountSize = accountSize;
        }

        /// <summary>
        /// Run a single backtest configuration against historical bars
        /// </summary>
        public BacktestResult Run(
            BacktestConfiguration config,
            IReadOnlyList<HistoricalBar> bars)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (bars == null || bars.Count == 0) throw new ArgumentNullException(nameof(bars));

            var brain = new IntegratedTradingBrain();
            var trades = new List<BacktestTrade>();
            var equity = _accountSize;
            var maxDD = 0m;
            var peakEquity = equity;

            // Placeholder: For now, just track bars without executing
            // In full implementation: parse market data, generate signals via TradingBrain,
            // track entries/exits, calculate slippage, update equity
            foreach (var bar in bars)
            {
                // This is where signal generation happens
                // For MVP: we'll stub this out
            }

            // Calculate max drawdown
            foreach (var trade in trades)
            {
                equity += trade.PnL - trade.Slippage;
                if (equity < peakEquity)
                {
                    var dd = peakEquity - equity;
                    if (dd > maxDD) maxDD = dd;
                }
                else
                {
                    peakEquity = equity;
                }
            }

            return new BacktestResult(
                config,
                trades,
                _accountSize,
                equity,
                maxDD,
                0m, // daily drawdown (requires session grouping)
                bars[0].TimestampUtc.UtcDateTime,
                bars[bars.Count - 1].TimestampUtc.UtcDateTime);
        }
    }
}
