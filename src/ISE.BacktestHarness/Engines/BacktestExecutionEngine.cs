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
    /// Tracks positions, P&L, equity, and generates BacktestTrade records
    /// </summary>
    public sealed class BacktestExecutionEngine
    {
        private readonly decimal _accountSize;
        private readonly decimal _mnqTickValue = 20m;  // $20 per point (0.25 tick)
        private readonly decimal _mgcTickValue = 10m;  // $10 per point (0.25 tick)
        private readonly decimal _slippagePerTick = 0.5m; // half a tick per trade (est.)

        // Position tracking
        private string _activeInstrument = string.Empty;
        private string _activeDirection = string.Empty;
        private int _activeContracts = 0;
        private decimal _entryPrice = 0m;
        private DateTime _entryTimeUtc = DateTime.MinValue;

        private List<BacktestTrade> _trades;
        private decimal _currentEquity;
        private decimal _peakEquity;
        private decimal _maxDrawdown;
        private long _tradesExecuted;

        public BacktestExecutionEngine(decimal accountSize = 50000m)
        {
            if (accountSize <= 0) throw new ArgumentOutOfRangeException(nameof(accountSize));
            _accountSize = accountSize;
            _trades = new List<BacktestTrade>();
            _currentEquity = accountSize;
            _peakEquity = accountSize;
            _maxDrawdown = 0m;
            _tradesExecuted = 0;
        }

        /// <summary>
        /// Run a single backtest configuration against historical bars
        /// </summary>
        public BacktestResult Run(
            BacktestConfiguration config,
            IReadOnlyList<HistoricalBar> bars,
            DateTime periodStart,
            DateTime periodEnd)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (bars == null || bars.Count == 0) throw new ArgumentNullException(nameof(bars));

            _trades = new List<BacktestTrade>();
            _currentEquity = _accountSize;
            _peakEquity = _accountSize;
            _maxDrawdown = 0m;
            _tradesExecuted = 0;
            _activeContracts = 0;

            var brain = new IntegratedTradingBrain();
            var barsByDate = GroupBarsByDay(bars);

            // Process each trading day
            foreach (var dayBars in barsByDate)
            {
                var dailyStartEquity = _currentEquity;

                // Process bars in sequence for this day
                for (int i = 0; i < dayBars.Count; i++)
                {
                    var bar = dayBars[i];
                    var nextBar = i + 1 < dayBars.Count ? dayBars[i + 1] : null;

                    // Generate signal from TradingBrain
                    // For MVP: placeholder logic
                    // In production: create proper IntegratedTradingBrainInput from bar data
                    ProcessBar(bar, nextBar, config, brain);
                }
            }

            // Close any remaining open position at end of period
            if (_activeContracts > 0 && bars.Count > 0)
            {
                ClosePosition(bars[bars.Count - 1], 0m); // breakeven exit
            }

            return new BacktestResult(
                config,
                _trades,
                _accountSize,
                _currentEquity,
                _maxDrawdown,
                0m, // daily drawdown tracking would require per-session aggregation
                periodStart,
                periodEnd);
        }

        private List<List<HistoricalBar>> GroupBarsByDay(IReadOnlyList<HistoricalBar> bars)
        {
            var groups = new Dictionary<DateTime, List<HistoricalBar>>();
            foreach (var bar in bars)
            {
                var date = bar.TimestampUtc.Date;
                if (!groups.ContainsKey(date))
                    groups[date] = new List<HistoricalBar>();
                groups[date].Add(bar);
            }
            return groups.Values.ToList();
        }

        private void ProcessBar(
            HistoricalBar bar,
            HistoricalBar? nextBar,
            BacktestConfiguration config,
            IntegratedTradingBrain brain)
        {
            // Placeholder: implement signal generation
            // This is where we'd:
            // 1. Create IntegratedTradingBrainInput from bar data
            // 2. Call brain.MakeDecision()
            // 3. Execute on signal direction/magnitude
            // 4. Manage position sizing from config.MaximumContracts
            // 5. Calculate slippage based on contract count

            // For MVP, we skip live trading logic and focus on architecture
        }

        private void ClosePosition(HistoricalBar exitBar, decimal slippageAdjustment)
        {
            if (_activeContracts == 0) return;

            var tickValue = _activeInstrument == "MNQ" ? _mnqTickValue : _mgcTickValue;
            var pnl = CalculatePnL(exitBar.Close, tickValue);
            var slippage = _activeContracts * _slippagePerTick;

            var trade = new BacktestTrade(
                _entryTimeUtc,
                exitBar.TimestampUtc.UtcDateTime,
                _activeDirection,
                _entryPrice,
                exitBar.Close,
                _activeContracts,
                pnl,
                slippage);

            _trades.Add(trade);
            _tradesExecuted++;

            // Update equity
            _currentEquity += pnl - slippage;
            UpdateDrawdown();

            // Clear position
            _activeContracts = 0;
            _activeDirection = string.Empty;
            _activeInstrument = string.Empty;
        }

        private decimal CalculatePnL(decimal exitPrice, decimal tickValue)
        {
            var priceChange = _activeDirection == "LONG"
                ? exitPrice - _entryPrice
                : _entryPrice - exitPrice;

            return priceChange * tickValue * _activeContracts;
        }

        private void UpdateDrawdown()
        {
            if (_currentEquity > _peakEquity)
            {
                _peakEquity = _currentEquity;
            }
            else
            {
                var dd = _peakEquity - _currentEquity;
                if (dd > _maxDrawdown)
                    _maxDrawdown = dd;
            }
        }
    }
}
