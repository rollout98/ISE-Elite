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
        // Point values, not tick values. MNQ = $2/point ($0.50/tick @ 0.25 tick size).
        // $20/point is NQ, the full-size contract — using it overstates MNQ P&L by 10x.
        private readonly decimal _mnqPointValue = 2m;
        private readonly decimal _mgcPointValue = 10m;
        // MNQ tick = 0.25 pt = $0.50. One tick of slippage per side is realistic for a
        // liquid micro; $10/side (the previous value) implied 5 POINTS of slip per side
        // and was single-handedly responsible for the -409% result on 2026-08-11.
        private readonly decimal _slippagePerContract = 0.50m;   // per side
        private readonly decimal _commissionPerContract = 0.37m; // per side, typical retail all-in

        // Position tracking
        private string _activeInstrument = string.Empty;
        private string _activeDirection = string.Empty;
        private int _activeContracts = 0;
        private decimal _entryPrice = 0m;
        private DateTime _entryTimeUtc = DateTime.MinValue;
        private int _barsHeld = 0;

        // Exit geometry for the active trade, set from BacktestConfiguration at entry.
        private decimal _stopPoints = 1m;
        private decimal _targetPoints = 1m;
        private int _maxHoldBars = 50;
        private bool _useTrailingStop = false;
        private decimal _bestPrice = 0m; // best price reached in the trade's favour
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

            // Wire the sweep parameters to actual trade geometry. Previously only
            // MaximumContracts was read, so all 420 configurations produced identical
            // results and the sweep tested nothing.
            _stopPoints = (decimal)config.StopDistanceRisk;
            _targetPoints = _stopPoints * (decimal)config.AdaptiveRiskMultiplier;
            _maxHoldBars = (int)config.LiquidityCapacity;
            _useTrailingStop = config.UseTrailingStop;

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
                    if (_barsHeld > _maxHoldBars)
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
                else if (signal == "SELL" && _activeContracts == 0)
                {
                    OpenPosition(bar, "SHORT", config.MaximumContracts);
                }
                else if (signal == "EXIT" && _activeContracts > 0)
                {
                    ClosePosition(bar);
                }
                else if (_activeContracts > 0)
                {
                    // Survived this bar - now let the trail advance on the favourable
                    // extreme. Order matters: testing the stop first prevents a bar
                    // from extending the trail and then being saved by that extension.
                    UpdateTrail(bar);
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
            // Exit first: stop before target, so a bar spanning both is booked as a loss.
            if (_activeContracts > 0)
            {
                if (StopTouched(currentBar)) return "EXIT";
                if (TargetTouched(currentBar)) return "EXIT";
            }

            // Entry: symmetric trend filter. The long-only version could not participate
            // in downtrends at all - roughly half of every trend in the data was
            // structurally untradeable before 2026-08-11.
            if (_activeContracts == 0 && _recentBars.Count >= 10)
            {
                var closes = _recentBars.Select(b => b.Close).ToList();
                var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
                var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();

                if (avg5 > avg10 && currentBar.Close > avg5) return "BUY";
                if (avg5 < avg10 && currentBar.Close < avg5) return "SELL";
            }

            return "NONE";
        }

        // Single source of truth for trade geometry. Both GenerateSignal and
        // ClosePosition read these, so long and short cannot drift out of sync.
        // In trailing mode the stop follows the best price reached and never retreats;
        // in fixed mode it stays anchored to the entry.
        private decimal StopLevel
        {
            get
            {
                var anchor = _useTrailingStop ? _bestPrice : _entryPrice;
                return _activeDirection == "LONG" ? anchor - _stopPoints : anchor + _stopPoints;
            }
        }

        // Trailing mode has no fixed target - the run ends when the trail is hit.
        private decimal TargetLevel =>
            _activeDirection == "LONG" ? _entryPrice + _targetPoints : _entryPrice - _targetPoints;

        private bool StopTouched(HistoricalBar bar) =>
            _activeDirection == "LONG" ? bar.Low <= StopLevel : bar.High >= StopLevel;

        private bool TargetTouched(HistoricalBar bar) =>
            !_useTrailingStop &&
            (_activeDirection == "LONG" ? bar.High >= TargetLevel : bar.Low <= TargetLevel);

        // Advance the trail on favourable movement. Called AFTER the stop test for the
        // current bar, so a bar cannot both extend the trail and be saved by it.
        private void UpdateTrail(HistoricalBar bar)
        {
            if (!_useTrailingStop) return;
            if (_activeDirection == "LONG")
            {
                if (bar.High > _bestPrice) _bestPrice = bar.High;
            }
            else
            {
                if (bar.Low < _bestPrice) _bestPrice = bar.Low;
            }
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
            _bestPrice = entryBar.Close;

            _currentEquity -= (_slippagePerContract + _commissionPerContract) * _activeContracts;
            UpdateDrawdown();
        }

        private void ClosePosition(HistoricalBar exitBar)
        {
            if (_activeContracts == 0) return;

            var pointValue = _activeInstrument == "MNQ" ? _mnqPointValue : _mgcPointValue;

            // Fill at the level that triggered the exit, not the bar close. The signal
            // fires on the bar's high/low, so filling at Close invents P&L that the
            // trade never had. Stop is tested first: if a bar spans both levels we
            // assume the adverse fill rather than the favourable one.
            decimal exitPrice;
            if (StopTouched(exitBar))
                exitPrice = StopLevel;
            else if (TargetTouched(exitBar))
                exitPrice = TargetLevel;
            else
                exitPrice = exitBar.Close; // time-based exit (hold cap or end of data)

            var pnl = CalculatePnL(exitPrice, pointValue);

            // Guard against sign errors in the short-side mirror. A stop-out must lose
            // and a target hit must win, for BOTH directions. If this ever trips, the
            // geometry is inverted and every P&L figure downstream is fiction - fail
            // loudly rather than report an attractive fake number.
            // Trailing mode is exempt: a trailed stop-out SHOULD be able to profit,
            // since the trail locks in gains above the entry. Fixed mode has no such
            // excuse - a stop below entry that books a profit means inverted signs.
            if (!_useTrailingStop && StopTouched(exitBar) && pnl > 0m)
                throw new InvalidOperationException(
                    $"Stop-out produced a PROFIT ({_activeDirection}, entry {_entryPrice}, " +
                    $"exit {exitPrice}, pnl {pnl}). Trade geometry is inverted.");
            if (!_useTrailingStop && !StopTouched(exitBar) && TargetTouched(exitBar) && pnl < 0m)
                throw new InvalidOperationException(
                    $"Target hit produced a LOSS ({_activeDirection}, entry {_entryPrice}, " +
                    $"exit {exitPrice}, pnl {pnl}). Trade geometry is inverted.");

            var slippage = (_slippagePerContract + _commissionPerContract) * _activeContracts;

            var trade = new BacktestTrade(
                _entryTimeUtc,
                exitBar.TimestampUtc.UtcDateTime,
                _activeDirection,
                _entryPrice,
                exitPrice,
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

        private decimal CalculatePnL(decimal exitPrice, decimal pointValue)
        {
            var priceChange = _activeDirection == "LONG"
                ? exitPrice - _entryPrice
                : _entryPrice - exitPrice;

            return priceChange * pointValue * _activeContracts;
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
