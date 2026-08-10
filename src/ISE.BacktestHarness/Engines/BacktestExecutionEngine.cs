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
        private readonly decimal _slippagePerContract = 10m; // $10 per contract entry/exit
        private readonly decimal _confidenceThreshold = 0.6m; // Only execute if confidence > 60%
        private readonly int _minHoldBarCount = 2; // Minimum 2 bars to hold position

        // Position tracking
        private string _activeInstrument = string.Empty;
        private string _activeDirection = string.Empty;
        private int _activeContracts = 0;
        private decimal _entryPrice = 0m;
        private DateTime _entryTimeUtc = DateTime.MinValue;
        private int _barsHeld = 0;

        // Recent history for signal generation
        private readonly List<HistoricalBar> _recentBars = new List<HistoricalBar>();
        private const int MaxRecentBars = 50; // Keep last 50 bars

        private List<BacktestTrade> _trades;
        private decimal _currentEquity;
        private decimal _peakEquity;
        private decimal _maxDrawdown;
        private int _tradesExecuted;

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
            _recentBars.Clear();

            var brain = new IntegratedTradingBrain();

            // Process each bar sequentially
            foreach (var bar in bars)
            {
                // Maintain recent bar history
                _recentBars.Add(bar);
                if (_recentBars.Count > MaxRecentBars)
                    _recentBars.RemoveAt(0);

                // Check if we should close existing position (min hold time)
                if (_activeContracts > 0)
                {
                    _barsHeld++;
                    
                    // Force close if held too long (prevent zombie positions)
                    if (_barsHeld > 500)
                    {
                        ClosePosition(bar, "timeout");
                        continue;
                    }
                }

                // Generate signal from TradingBrain
                try
                {
                    var decision = brain.Decide(CreateBrainInput(bar, config));

                    if (decision == null) continue;

                    // Handle exit signal - close position if open
                    if (!string.IsNullOrEmpty(decision.Direction) && 
                        decision.Direction.ToUpper() == "EXIT" && 
                        _activeContracts > 0)
                    {
                        ClosePosition(bar, "signal");
                    }
                    // Handle buy signal - open long position
                    else if (!string.IsNullOrEmpty(decision.Direction) && 
                             decision.Direction.ToUpper() == "BUY" &&
                             _activeContracts == 0 &&
                             decision.Confidence >= (double)_confidenceThreshold)
                    {
                        OpenPosition(bar, "LONG", config.MaximumContracts, config.AdaptiveRiskMultiplier);
                    }
                    // Handle sell signal - close long or open short
                    else if (!string.IsNullOrEmpty(decision.Direction) && 
                             decision.Direction.ToUpper() == "SELL" &&
                             _activeContracts == 0 &&
                             decision.Confidence >= (double)_confidenceThreshold)
                    {
                        OpenPosition(bar, "SHORT", config.MaximumContracts, config.AdaptiveRiskMultiplier);
                    }
                }
                catch
                {
                    // Ignore signal errors, continue processing
                }
            }

            // Close any remaining open position at end of period
            if (_activeContracts > 0 && bars.Count > 0)
            {
                ClosePosition(bars[bars.Count - 1], "eop");
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

        /// <summary>
        /// Create input for IntegratedTradingBrain from current bar and recent history
        /// </summary>
        private IntegratedTradingBrainInput CreateBrainInput(HistoricalBar currentBar, BacktestConfiguration config)
        {
            // Build OHLCV array from recent bars
            var opens = _recentBars.Select(b => b.Open).ToArray();
            var highs = _recentBars.Select(b => b.High).ToArray();
            var lows = _recentBars.Select(b => b.Low).ToArray();
            var closes = _recentBars.Select(b => b.Close).ToArray();
            var volumes = _recentBars.Select(b => b.Volume).ToArray();

            var input = new IntegratedTradingBrainInput(
                instrument: currentBar.Instrument,
                currentPrice: currentBar.Close,
                high: currentBar.High,
                low: currentBar.Low,
                volume: currentBar.Volume,
                timestamp: currentBar.TimestampUtc.UtcDateTime,
                recentBars: _recentBars,
                configuration: null); // No specific config needed for backtest

            return input;
        }

        private void OpenPosition(HistoricalBar entryBar, string direction, int maxContracts, double riskMultiplier)
        {
            if (_activeContracts > 0) return; // Already in position

            _activeInstrument = entryBar.Instrument;
            _activeDirection = direction;
            _activeContracts = Math.Min(maxContracts, 4); // Cap at 4 contracts for MNQ, 3 for MGC
            _entryPrice = entryBar.Close;
            _entryTimeUtc = entryBar.TimestampUtc.UtcDateTime;
            _barsHeld = 0;

            // Add slippage on entry
            _currentEquity -= _slippagePerContract * _activeContracts;
            UpdateDrawdown();
        }

        private void ClosePosition(HistoricalBar exitBar, string reason)
        {
            if (_activeContracts == 0) return;

            var tickValue = _activeInstrument == "MNQ" ? _mnqTickValue : _mgcTickValue;
            var pnl = CalculatePnL(exitBar.Close, tickValue);
            var slippage = _slippagePerContract * _activeContracts; // Exit slippage

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

            // Update equity: add P&L, subtract slippage
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
