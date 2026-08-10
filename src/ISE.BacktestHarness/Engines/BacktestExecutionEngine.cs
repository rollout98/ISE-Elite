using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Executes a backtest by generating simple price-action signals
    /// This is a MVP implementation that demonstrates backtest harness working.
    /// Full TradingBrain integration will be added in next phase.
    /// </summary>
    public sealed class BacktestExecutionEngine
    {
        private readonly decimal _accountSize;
        private readonly decimal _mnqTickValue = 20m;  // $20 per point
        private readonly decimal _mgcTickValue = 10m;  // $10 per point
        private readonly decimal _slippagePerContract = 10m; // $10 per contract entry/exit

        // Position tracking
        private string _activeInstrument = string.Empty;
        private string _activeDirection = string.Empty;
        private int _activeContracts = 0;
        private decimal _entryPrice = 0m;
        private DateTime _entryTimeUtc = DateTime.MinValue;
        private int _barsHeld = 0;

        // Recent history for signal generation
        private readonly List<HistoricalBar> _recentBars = new List<HistoricalBar>();
        private const int MaxRecentBars = 20;

        private List<BacktestTrade> _trades;
        private decimal _currentEquity;
        private decimal _peakEquity;
        private decimal _maxDrawdown;

        public BacktestExecutionEngine(decimal accountSize = 50000m)
        {
            if (accountSize <= 0) throw new ArgumentOutOfRangeException(nameof(accountSize));
            _accountSize = accountSize;
            _trades = new List<BacktestTrade>();
            _currentEquity = accountSize;
            _peakEquity = accountSize;
            _maxDrawdown = 0m;
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
            _activeContracts = 0;
            _recentBars.Clear();

            // Process each bar sequentially
            foreach (var bar in bars)
            {
                _recentBars.Add(bar);
                if (_recentBars.Count > MaxRecentBars)
                    _recentBars.RemoveAt(0);

                if (_activeContracts > 0)
                {
                    _barsHeld++;
                    
                    // Force close if held too long
                    if (_barsHeld > 100)
                    {
                        ClosePosition(bar);
                        continue;
                    }
                }

                // Generate signal using simple price action
                var signal = GenerateSignal(bar, config);

                if (signal == "BUY" && _activeContracts == 0)
                {
                    OpenPosition(bar, "LONG", config.MaximumContracts);
                }
                else if (signal == "SELL" && _activeContracts > 0)
                {
                    ClosePosition(bar);
                }
                else if (signal == "EXIT" && _activeContracts > 0)
                {
                    ClosePosition(bar);
                }
            }

            // Close any remaining position at end
            if (_activeContracts > 0 && bars.Count > 0)
            {
                ClosePosition(bars[bars.Count - 1]);
            }

            return new BacktestResult(
                config,
                _trades,
                _accountSize,
                _currentEquity,
                _maxDrawdown,
                0m,
                periodStart,
                periodEnd);
        }

        /// <summary>
        /// Simple price-action signal generator
        /// MVP: Uses basic patterns to generate trades
        /// Future: Replace with IntegratedTradingBrain.Evaluate()
        /// </summary>
        private string GenerateSignal(HistoricalBar currentBar, BacktestConfiguration config)
        {
            if (_recentBars.Count < 3) return "NONE";

            // Get recent closes
            var closes = _recentBars.Select(b => b.Close).ToList();
            var currentClose = currentBar.Close;
            var prev1 = _recentBars.Count > 1 ? _recentBars[_recentBars.Count - 2].Close : currentClose;
            var prev2 = _recentBars.Count > 2 ? _recentBars[_recentBars.Count - 3].Close : currentClose;

            // Simple pullback strategy
            // BUY: Price closes above previous high and above 10-bar average
            var avg10 = _recentBars.TakeLast(Math.Min(10, _recentBars.Count)).Average(b => b.Close);
            var high5 = _recentBars.TakeLast(Math.Min(5, _recentBars.Count)).Max(b => b.High);

            // Exit: If price falls below entry price + slippage threshold
            if (_activeContracts > 0)
            {
                var threshold = _entryPrice - 0.5m; // Exit if down 0.5 points
                if (currentClose < threshold)
                {
                    return "EXIT";
                }

                // Take profit: If up 2+ points
                if (currentClose > _entryPrice + 2m)
                {
                    return "EXIT";
                }
            }

            // Entry signal: Breakout above 5-bar high + above 10-bar average
            if (currentClose > high5 && currentClose > avg10 && currentClose > prev1)
            {
                return "BUY";
            }

            return "NONE";
        }

        private void OpenPosition(HistoricalBar entryBar, string direction, int maxContracts)
        {
            if (_activeContracts > 0) return;

            _activeInstrument = entryBar.Instrument;
            _activeDirection = direction;
            _activeContracts = Math.Min(maxContracts, 4); // Cap at 4 contracts
            _entryPrice = entryBar.Close;
            _entryTimeUtc = entryBar.TimestampUtc.UtcDateTime;
            _barsHeld = 0;

            // Deduct entry slippage
            _currentEquity -= _slippagePerContract * _activeContracts;
            UpdateDrawdown();
        }

        private void ClosePosition(HistoricalBar exitBar)
        {
            if (_activeContracts == 0) return;

            var tickValue = _activeInstrument == "MNQ" ? _mnqTickValue : _mgcTickValue;
            var pnl = CalculatePnL(exitBar.Close, tickValue);
            var slippage = _slippagePerContract * _activeContracts;

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

            // Update equity: add P&L, subtract exit slippage
            _currentEquity += pnl - slippage;
            UpdateDrawdown();

            // Clear position
            _activeContracts = 0;
            _activeDirection = string.Empty;
            _activeInstrument = string.Empty;
            _barsHeld = 0;
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
