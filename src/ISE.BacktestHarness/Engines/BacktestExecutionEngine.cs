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

            // Group bars by instrument to process separately
            var barsByInstrument = bars.GroupBy(b => b.Instrument).ToList();
            var orderedBars = bars.OrderBy(b => b.TimestampUtc).ToList();

            foreach (var bar in orderedBars)
            {
                _recentBars.Add(bar);
                if (_recentBars.Count > MaxRecentBars)
                    _recentBars.RemoveAt(0);

                if (_activeContracts > 0)
                {
                    _barsHeld++;
                    
                    if (_barsHeld > 50)
                    {
                        ClosePosition(bar);
                        continue;
                    }
                }

                var signal = GenerateSignal(bar, config);

                if (signal == "BUY" && _activeContracts == 0)
                {
                    OpenPosition(bar, "LONG", config.MaximumContracts);
                }
                else if ((signal == "SELL" || signal == "EXIT") && _activeContracts > 0)
                {
                    ClosePosition(bar);
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
        /// Simple momentum-based signal generator
        /// Generates trades on price momentum reversals
        /// </summary>
        private string GenerateSignal(HistoricalBar currentBar, BacktestConfiguration config)
        {
            if (_recentBars.Count < 5) return "NONE";

            var closes = _recentBars.Select(b => b.Close).ToList();
            var currentClose = currentBar.Close;
            var prev1 = closes[closes.Count - 2];
            var prev2 = closes[closes.Count - 3];
            var prev3 = closes[closes.Count - 4];

            // EXIT logic: Take profit or cut loss
            if (_activeContracts > 0)
            {
                // Profit target: 1+ point move in favorable direction
                if (_activeDirection == "LONG" && currentClose >= _entryPrice + 1m)
                {
                    return "EXIT";
                }
                
                // Stop loss: 1 point against position
                if (_activeDirection == "LONG" && currentClose <= _entryPrice - 1m)
                {
                    return "EXIT";
                }
                
                // Trailing stop: Exit if price pulls back 0.5 points after hitting profit
                if (_activeDirection == "LONG" && currentClose >= _entryPrice + 1.5m && currentClose < currentBar.High - 0.5m)
                {
                    return "EXIT";
                }
            }

            // ENTRY logic: Simple momentum signal
            // Buy: 3 bars of up closes (simple trend confirmation)
            if (_activeContracts == 0 && currentClose > prev1 && prev1 > prev2 && prev2 > prev3)
            {
                // Additional filter: Not too extended (don't buy at highs)
                var avg5 = _recentBars.TakeLast(5).Average(b => b.Close);
                if (currentClose <= avg5 * 1.005m) // Within 0.5% of 5-bar average
                {
                    return "BUY";
                }
            }

            return "NONE";
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
