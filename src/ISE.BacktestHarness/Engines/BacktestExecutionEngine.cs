using System;
using System.Collections.Generic;
using System.Linq;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Executes a backtest using simple momentum signals
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
        private int _barCount = 0;

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
            _barCount = 0;

            var orderedBars = bars.OrderBy(b => b.TimestampUtc).ToList();

            foreach (var bar in orderedBars)
            {
                _barCount++;
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

                var signal = GenerateSignal(bar);

                if (signal == "BUY" && _activeContracts == 0)
                {
                    OpenPosition(bar, "LONG", config.MaximumContracts);
                }
                else if (signal == "EXIT" && _activeContracts > 0)
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

        private string GenerateSignal(HistoricalBar currentBar)
        {
            if (_recentBars.Count < 5) return "NONE";

            var currentClose = currentBar.Close;

            // Exit logic - FIRST (profit/stop)
            if (_activeContracts > 0)
            {
                if (_activeDirection == "LONG" && currentClose >= _entryPrice + 1m)
                    return "EXIT"; // Profit target: +1 point (4 ticks)
                if (_activeDirection == "LONG" && currentClose <= _entryPrice - 0.5m)
                    return "EXIT"; // Stop loss: -0.5 points (2 ticks) — 2:1 RR
            }

            // Entry logic: Price momentum (5-bar breakout pattern)
            // Only enter if price trending above average
            if (_activeContracts == 0 && _recentBars.Count >= 5)
            {
                var closes = _recentBars.Select(b => b.Close).ToList();
                var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
                var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();
                
                // Uptrend: current price above both 5-bar and 10-bar averages
                // AND price is making higher highs
                if (currentClose > avg5 && avg5 > avg10)
                {
                    var prevClose = _recentBars[_recentBars.Count - 2].Close;
                    
                    // Buy only on upward momentum
                    if (currentClose > prevClose)
                    {
                        return "BUY";
                    }
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
