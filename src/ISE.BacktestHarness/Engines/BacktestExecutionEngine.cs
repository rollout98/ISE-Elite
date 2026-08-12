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
        private decimal _pointValue = 2m; // set per instrument in Initialize()
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
        private bool _holdToReversal = false;
        private decimal _profitFloorDollars = 0m;
        private bool _profitFloorLocked = false;
        private string _pendingExitReason = "";
        private decimal _dailyLossLimit = 0m;
        private DateTime _currentTradingDay = DateTime.MinValue;
        private decimal _dayRealizedPnL = 0m;
        private bool _dayHalted = false;
        private decimal _lockedFloorPrice = 0m;
        private Dictionary<DateTime, string> _externalSignals = new Dictionary<DateTime, string>();
        private int _trendFilterBars = 0; // 0 = disabled
        private decimal _bestPrice = 0m; // best price reached in the trade's favour
        private double _breakEvenMovePoints = 0; // once profit reaches this, stop moves to entry
        private bool _breakEvenActivated = false; // flag to prevent re-triggering
        private int _barCount = 0;

        // Recent price history
        private readonly List<HistoricalBar> _recentBars = new List<HistoricalBar>();
        // Was 20. A 20-bar window cannot represent a daily trend at all - the engine
        // could only ever see the last 20 minutes, so every 3-minute wiggle looked
        // identical to the day's real move. 480 bars = 8 hours of context.
        private const int MaxRecentBars = 480;

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

            // Detect instrument and set correct point value
            var firstBar = bars.First();
            _pointValue = InstrumentSpecs.GetPointValue(firstBar.Instrument);

            // Wire the sweep parameters to actual trade geometry. Previously only
            // MaximumContracts was read, so all 420 configurations produced identical
            // results and the sweep tested nothing.
            _stopPoints = (decimal)config.StopDistanceRisk;
            _targetPoints = _stopPoints * (decimal)config.AdaptiveRiskMultiplier;
            _maxHoldBars = (int)config.LiquidityCapacity;
            _useTrailingStop = config.UseTrailingStop;
            _holdToReversal = config.HoldToReversal;
            _profitFloorDollars = config.ProfitFloorDollars;
            _dailyLossLimit = config.DailyLossLimitDollars;
            _currentTradingDay = DateTime.MinValue;
            _dayRealizedPnL = 0m;
            _dayHalted = false;
            _trendFilterBars = config.TrendFilterBars;
            _breakEvenMovePoints = config.BreakevenMovePoints;
            _breakEvenActivated = false;

            var orderedBars = bars.OrderBy(b => b.TimestampUtc).ToList();

            foreach (var bar in orderedBars)
            {
                // Roll the trading day first. TradingDay comes from the data feed, so
                // the session boundary is the exchange's, not UTC midnight cutting the
                // overnight session in half.
                if (bar.TradingDay != _currentTradingDay)
                {
                    _currentTradingDay = bar.TradingDay;
                    _dayRealizedPnL = 0m;
                    _dayHalted = false;
                }

                _barCount++;
                _recentBars.Add(bar);
                if (_recentBars.Count > MaxRecentBars)
                    _recentBars.RemoveAt(0);

                if (_activeContracts > 0)
                {
                    _barsHeld++;
                    if (_barsHeld > _maxHoldBars)
                    {
                        _pendingExitReason = "TIMECAP";
                        ClosePosition(bar);
                        continue;
                    }
                }

                // Daily circuit breaker. Checked before signals so a halted day cannot
                // open a fresh position on the same bar it was halted.
                if (_dailyLossLimit > 0 && _dayHalted)
                {
                    if (_activeContracts > 0)
                    {
                        _pendingExitReason = "DAYSTOP";
                        ClosePosition(bar);
                    }
                    continue;
                }

                var signal = GenerateSignal(bar);

                // "Exit governs entry": while in a trade, an opposing signal is the exit.
                // A same-side signal is ignored entirely. In hold-to-reversal mode this
                // is the ONLY discretionary exit - there is no profit target.
                if (_holdToReversal && _activeContracts > 0)
                {
                    var opposing =
                        (_activeDirection == "LONG" && signal == "SELL") ||
                        (_activeDirection == "SHORT" && signal == "BUY");

                    if (opposing)
                    {
                        _pendingExitReason = "REVERSAL";
                        ClosePosition(bar);
                        continue;
                    }
                }

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
                _pendingExitReason = "ENDOFDATA";
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
        /// Inject pre-computed signals (e.g., from VectorFlow CSV). If this is called,
        /// GenerateSignal() will read from this dictionary instead of computing
        /// crossovers. Keys are bar timestamps (UTC), values are "BUY", "SELL", "NONE".
        /// </summary>
        public void LoadExternalSignals(IEnumerable<(DateTime timestamp, string signal)> signals)
        {
            _externalSignals.Clear();
            foreach (var (ts, sig) in signals)
                _externalSignals[ts] = sig;
            Console.WriteLine($"   Loaded {_externalSignals.Count} external signal mappings");
        }

        private string GenerateSignal(HistoricalBar currentBar)
        {
            // Exit first: stop before target, so a bar spanning both is booked as a loss.
            if (_activeContracts > 0)
            {
                if (StopTouched(currentBar)) return "EXIT";
                if (TargetTouched(currentBar)) return "EXIT";
            }

            // If external signals are loaded (e.g., VectorFlow from CSV), use those.
            // Otherwise fall through to the computed 5/10 MA filter below.
            if (_externalSignals.Count > 0)
            {
                if (_externalSignals.TryGetValue(currentBar.TimestampUtc.UtcDateTime, out var sig))
                {
                    return sig == "NONE" ? "NONE" : sig; // BUY, SELL, or NONE
                }
                return "NONE";
            }

            // Computed entry (5/10 MA crossover with optional trend filter).
            // Used only when external signals are NOT loaded. The long-only version could not participate
            // in downtrends at all - roughly half of every trend in the data was
            // structurally untradeable before 2026-08-11.
            if (_activeContracts == 0 && _recentBars.Count >= 10)
            {
                var closes = _recentBars.Select(b => b.Close).ToList();
                var avg5 = closes.Skip(Math.Max(0, closes.Count - 5)).Average();
                var avg10 = closes.Skip(Math.Max(0, closes.Count - 10)).Average();

                // Higher-timeframe gate. The 1-minute cross only fires when it agrees
                // with the slower trend, so the engine stops taking every wiggle and
                // only participates in moves that the wider context supports.
                var bias = TrendBias(currentBar);

                if (avg5 > avg10 && currentBar.Close > avg5 && bias >= 0) return "BUY";
                if (avg5 < avg10 && currentBar.Close < avg5 && bias <= 0) return "SELL";
            }

            return "NONE";
        }

        /// <summary>
        /// +1 up, -1 down, 0 when the filter is off or there is not enough history.
        /// Compares price to a slow moving average over _trendFilterBars bars.
        /// </summary>
        private int TrendBias(HistoricalBar currentBar)
        {
            if (_trendFilterBars <= 0) return 0;
            if (_recentBars.Count < _trendFilterBars) return 0;

            var window = _recentBars.Skip(_recentBars.Count - _trendFilterBars).ToList();
            var slowAvg = window.Average(b => b.Close);

            // Require the slow average to also be RISING/FALLING, not merely below or
            // above price - a flat average with price above it is chop, not a trend.
            var halfAvg = window.Skip(window.Count / 2).Average(b => b.Close);

            if (currentBar.Close > slowAvg && halfAvg > slowAvg) return 1;
            if (currentBar.Close < slowAvg && halfAvg < slowAvg) return -1;
            return 0;
        }

        // Single source of truth for trade geometry. Both GenerateSignal and
        // ClosePosition read these, so long and short cannot drift out of sync.
        // Breakeven mode: once profit reaches breakEvenMovePoints, stop moves to entry price.
        // Otherwise: in trailing mode the stop follows best price; in fixed mode it's anchored to entry.
        private decimal StopLevel
        {
            get
            {
                // Profit floor. Once unrealized P&L on the whole position reaches the
                // dollar threshold, we lock a stop at the price that secures it and
                // never give it back. Checked before breakeven because the floor is
                // strictly the more protective of the two once it engages.
                if (_profitFloorDollars > 0 && !_profitFloorLocked && _activeContracts > 0)
                {
                    var unrealized = (_activeDirection == "LONG")
                        ? (_bestPrice - _entryPrice) * _pointValue * _activeContracts
                        : (_entryPrice - _bestPrice) * _pointValue * _activeContracts;

                    if (unrealized >= _profitFloorDollars)
                    {
                        // Price that yields exactly the floor amount, per contract.
                        var floorPoints = _profitFloorDollars / (_pointValue * _activeContracts);
                        _lockedFloorPrice = _activeDirection == "LONG"
                            ? _entryPrice + floorPoints
                            : _entryPrice - floorPoints;
                        _profitFloorLocked = true;
                    }
                }

                if (_profitFloorLocked)
                    return _lockedFloorPrice;

                // Check if we should activate breakeven
                if (_breakEvenMovePoints > 0 && !_breakEvenActivated)
                {
                    var unrealizedProfit = (_activeDirection == "LONG")
                        ? (_bestPrice - _entryPrice) * _pointValue
                        : (_entryPrice - _bestPrice) * _pointValue;
                    
                    if (unrealizedProfit >= (decimal)_breakEvenMovePoints * _pointValue)
                    {
                        _breakEvenActivated = true; // Profit reached threshold, lock in BE
                    }
                }

                // Once breakeven is activated, stop is at entry; otherwise use normal logic
                if (_breakEvenActivated)
                    return _entryPrice;

                var anchor = _useTrailingStop ? _bestPrice : _entryPrice;
                return _activeDirection == "LONG" ? anchor - _stopPoints : anchor + _stopPoints;
            }
        }

        // Trailing mode has no fixed target - the run ends when the trail is hit.
        private decimal TargetLevel =>
            _activeDirection == "LONG" ? _entryPrice + _targetPoints : _entryPrice - _targetPoints;

        private bool StopTouched(HistoricalBar bar) =>
            _activeDirection == "LONG" ? bar.Low <= StopLevel : bar.High >= StopLevel;

        // Neither trailing nor hold-to-reversal mode has a fixed target. In reversal
        // mode the run ends on the opposing signal or the stop/floor, never a target.
        private bool TargetTouched(HistoricalBar bar) =>
            !_useTrailingStop && !_holdToReversal &&
            (_activeDirection == "LONG" ? bar.High >= TargetLevel : bar.Low <= TargetLevel);

        // Advance the favourable extreme. Called AFTER the stop test for the current
        // bar, so a bar cannot both extend the extreme and be saved by that extension.
        // This runs in EVERY mode, not just trailing: the dollar profit floor measures
        // unrealized P&L off _bestPrice, so leaving it pinned at the entry price would
        // mean the floor never engages. Whether _bestPrice anchors the stop is a
        // separate question, decided in StopLevel by _useTrailingStop.
        private void UpdateTrail(HistoricalBar bar)
        {
            if (_activeContracts == 0) return;
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
            // No arbitrary cap. A previous Math.Min(maxContracts, 4) here made every
            // 5-contract config report identical results to its 4-contract twin.
            _activeContracts = maxContracts;
            _entryPrice = entryBar.Close;
            _entryTimeUtc = entryBar.TimestampUtc.UtcDateTime;
            _barsHeld = 0;
            _bestPrice = entryBar.Close;
            _breakEvenActivated = false;
            _profitFloorLocked = false;
            _lockedFloorPrice = 0m;
            _pendingExitReason = "";

            _currentEquity -= (_slippagePerContract + _commissionPerContract) * _activeContracts;
            UpdateDrawdown();
        }

        private void ClosePosition(HistoricalBar exitBar)
        {
            if (_activeContracts == 0) return;

            // Fill at the level that triggered the exit, not the bar close. The signal
            // fires on the bar's high/low, so filling at Close invents P&L that the
            // trade never had. Stop is tested first: if a bar spans both levels we
            // assume the adverse fill rather than the favourable one.
            decimal exitPrice;
            string exitReason;
            if (StopTouched(exitBar))
            {
                exitPrice = StopLevel;
                // A locked floor is still a stop, but it is a WINNING one - label it
                // separately so a $500 floor lock is not filed alongside a real loss.
                exitReason = _profitFloorLocked ? "FLOOR"
                           : _breakEvenActivated ? "BREAKEVEN"
                           : "STOP";
            }
            else if (TargetTouched(exitBar))
            {
                exitPrice = TargetLevel;
                exitReason = "TARGET";
            }
            else
            {
                exitPrice = exitBar.Close;
                exitReason = string.IsNullOrEmpty(_pendingExitReason) ? "UNKNOWN" : _pendingExitReason;
            }

            var pnl = CalculatePnL(exitPrice, _pointValue);

            // Guard against sign errors in the short-side mirror. A stop-out must lose
            // and a target hit must win, for BOTH directions. If this ever trips, the
            // geometry is inverted and every P&L figure downstream is fiction - fail
            // loudly rather than report an attractive fake number.
            // Trailing mode is exempt: a trailed stop-out SHOULD be able to profit,
            // since the trail locks in gains above the entry. A floor-locked or
            // breakeven-locked stop is exempt for the same reason - locking a $500
            // floor and then being stopped at it is a WIN by design, not an inversion.
            var stopCanProfit = _useTrailingStop || _profitFloorLocked || _breakEvenActivated;
            if (!stopCanProfit && StopTouched(exitBar) && pnl > 0m)
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
                slippage,
                exitReason);

            _trades.Add(trade);

            _currentEquity += pnl - slippage;

            _dayRealizedPnL += pnl - slippage;
            if (_dailyLossLimit > 0 && _dayRealizedPnL <= -_dailyLossLimit)
                _dayHalted = true;
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
