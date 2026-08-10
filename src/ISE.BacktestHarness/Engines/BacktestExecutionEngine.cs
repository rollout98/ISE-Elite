using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Executes a backtest using order flow confirmation for signals
    /// Integrates OrderFlowAnalysisEngine for realistic entry/exit decisions
    /// </summary>
    public sealed class BacktestExecutionEngine
    {
        private readonly decimal _accountSize;
        private readonly decimal _mnqTickValue = 20m;
        private readonly decimal _mgcTickValue = 10m;
        private readonly decimal _slippagePerContract = 10m;

        // Position tracking
        private string _activeInstrument = string.Empty;
        private string _activeDirection = string.Empty;
        private int _activeContracts = 0;
        private decimal _entryPrice = 0m;
        private DateTime _entryTimeUtc = DateTime.MinValue;
        private int _barsHeld = 0;

        // Recent price history
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

            var orderedBars = bars.OrderBy(b => b.TimestampUtc).ToList();

            foreach (var bar in orderedBars)
            {
                _recentBars.Add(bar);
                if (_recentBars.Count > MaxRecentBars)
                    _recentBars.RemoveAt(0);

                if (_activeContracts > 0)
                {
                    _barsHeld++;
                    
                    // Check exit conditions
                    if (ShouldExitPosition(bar))
                    {
                        ClosePosition(bar);
                        continue;
                    }
                }

                // Check entry conditions
                var signal = GeneratePriceActionSignal(bar);

                if (signal == "BUY" && _activeContracts == 0)
                {
                    OpenPosition(bar, "LONG", config.MaximumContracts);
                }
            }

            if (_activeContracts > 0 && orderedBars.Count > 0)
            {
                ClosePosition(orderedBars[orderedBars.Count - 1]);
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
        /// Price action signal (3 rising closes = BUY)
        /// </summary>
        private string GeneratePriceActionSignal(HistoricalBar currentBar)
        {
            if (_recentBars.Count < 5) return "NONE";

            var closes = _recentBars.Select(b => b.Close).ToList();
            var currentClose = currentBar.Close;
            var prev1 = closes[closes.Count - 2];
            var prev2 = closes[closes.Count - 3];
            var prev3 = closes[closes.Count - 4];

            // 3 rising closes = momentum signal
            if (currentClose > prev1 && prev1 > prev2 && prev2 > prev3)
            {
                var avg5 = _recentBars.TakeLast(5).Average(b => b.Close);
                if (currentClose <= avg5 * 1.005m)
                {
                    return "BUY";
                }
            }

            return "NONE";
        }

        /// <summary>
        /// Check if position should be closed
        /// </summary>
        private bool ShouldExitPosition(HistoricalBar currentBar)
        {
            if (_activeContracts == 0) return false;

            // Profit target: 1+ point
            if (_activeDirection == "LONG" && currentBar.Close >= _entryPrice + 1m)
                return true;

            // Stop loss: 1 point
            if (_activeDirection == "LONG" && currentBar.Close <= _entryPrice - 1m)
                return true;

            // Timeout: max 50 bars
            if (_barsHeld > 50)
                return true;

            return false;
        }

        private void OpenPosition(HistoricalBar entryBar, string direction, int maxContracts)
        {
            if (_activeContracts > 0) return;

            _activeInstrument = entryBar.Instrument;
            _activeDirection = direction;
            _activeContracts = Math.Min(maxContracts, 4);
            _entryPrice = entryBar.Close;
            _entryTimeUtc = entryBar.TimestampUtc.UtcDateTime;
            _barsHeld = 0;

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

            _currentEquity += pnl - slippage;
            UpdateDrawdown();

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
