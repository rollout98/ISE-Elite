using System;
using System.Collections.Generic;
using ISE.BacktestHarness.Models;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Generate parameter configurations for backtest sweeping
    /// Supports both MNQ and MGC with instrument-specific stop ranges
    /// </summary>
    public sealed class ConfigurationSweeper
    {
        private readonly string _instrument;

        public ConfigurationSweeper(string instrument = "MNQ")
        {
            _instrument = instrument?.ToUpperInvariant() ?? "MNQ";
        }

        /// <summary>
        /// Generate 100+ parameter combinations to test
        /// </summary>
        public IReadOnlyList<BacktestConfiguration> GenerateConfigurations()
        {
            var configs = new List<BacktestConfiguration>();
            var configId = 1;

            // MaximumContracts: 1-10. The upper end matters because the live plan is a
            // session ramp - open Asia at ~3, and once profit is booked, size NY up to
            // 7-10. A sweep that stops at 4 or 5 cannot price that second leg.
            var contractCounts = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // StopDistanceRisk: instrument-specific, scaled to comparable dollar risk.
            // MNQ: 60, 75, 87.5 points ($2/pt -> $120-$175 risk per contract). Validated live.
            // MGC: 5-30 points ($10/pt -> $50-$300 risk per contract). Gold trades ~4100,
            //      so a 50pt stop would be a $50 move -- more than a full day's range.
            //      87.5 MNQ pts = $175/contract; the MGC equivalent is ~17.5 pts.
            // MNQ 100pt = Devon's live 400-tick stop, previously outside the sweep.
            // MGC unchanged.
            var stopDistances = _instrument == "MGC"
                ? new[] { 5.0, 7.5, 10.0, 12.5, 15.0, 17.5, 20.0, 25.0, 30.0 }
                : new[] { 60.0, 75.0, 87.5, 100.0 };

            // Profit floor in DOLLARS on the whole position.
            // DISCOVERY RUN: floor is 0 (disabled) across the board. We do not yet know
            // what this strategy yields per day, so imposing a $500 lock would truncate
            // every winner at $500 and hide the real distribution. Measure first, then
            // set the floor from the observed numbers rather than from the goal.
            var profitFloors = new[] { 0m };

            // MAX HOLD in bars (minutes on 1-min data). Reversal exits are the primary
            // mechanism now, so these are a safety cap rather than a tuning knob.
            //   240 = 4hrs, 480 = 8hrs, 1440 = 24hrs
            var maxHolds = new[] { 240.0, 480.0, 1440.0 };

            // Higher-timeframe gate, in 1-minute bars.
            // DISABLED: GenerateSignal returns external VectorFlow signals before it
            // ever reaches TrendBias(), so this parameter has no effect on any run that
            // loads a signal CSV - it was silently producing three identical copies of
            // every config. VectorFlow already carries its own higher-timeframe gate
            // (the FTC latch on SMA100/ATR100), so a second one is likely redundant
            // anyway. Left as a single value until we decide whether to wire it up.
            var trendFilters = new[] { 0 };

            // EXIT MODE COMPARISON. All three arms run on the same engine, the same
            // bars and the same signals, so the comparison is genuinely like-for-like.
            // Previously only the reversal arm was generated, which meant "reversal
            // beats fixed" rested on a number produced by a different, buggier build.
            //
            // Arm 1: REVERSAL - hold until the opposing VectorFlow signal fires.
            // Arm 2: FIXED    - profit target at stop * riskMultiplier. The prior live
            //                   setup was an 87.5pt stop with a 44pt target (~0.5R),
            //                   so the range brackets that.
            // Arm 3: TRAIL    - stop follows the favourable extreme by StopDistance.
            // 4.0 added so the live setup lands exactly in the sweep: a 100pt stop with
            // a 400pt target is the 1600-tick TP from the ATM template. The old ceiling
            // of 3.0 meant the configuration actually traded was never tested.
            var riskMultipliers = new[] { 0.5, 1.0, 1.5, 2.0, 3.0, 4.0 };

            // Daily circuit breaker. 0 = none (what every run so far has used).
            // The worst observed day was about -$2,400 at 3 contracts with no breaker;
            // these bracket that to show whether capping the day costs more in cut-off
            // recoveries than it saves in blowout sessions.
            var dailyLossLimits = new[] { 0m, 800m, 1200m, 1800m };

            // Daily profit halt. 0 = trade the whole session (what every run so far did).
            // The rest test "hit the number and walk away" at and around Devon's live
            // $500 goal, scaled up so the effect is visible at larger contract counts too.
            // Collapsed to "off". Measured on MNQ 5m, MNQ 2m and MGC: halting the day
            // at a profit number cost 20-40% of gross every time and never reduced the
            // count of LOSING days, because a profit cap cannot touch a losing session.
            // Keeping it in the sweep just multiplies runtime to re-learn the same thing.
            var dailyProfitTargets = new[] { 0m };

            // Breakeven trigger, instrument-scaled. 0 = never move the stop, which is
            // what the previous sweep did on every single config - the live method's
            // move to BE at 250-300 ticks (62.5-75pt on MNQ) was never tested.
            var breakEvenMoves = _instrument == "MGC"
                ? new[] { 0.0, 7.5, 12.5 }
                : new[] { 0.0, 62.5, 75.0 };

            foreach (var contracts in contractCounts)
            {
                foreach (var stop in stopDistances)
                {
                    foreach (var hold in maxHolds)
                    {
                        foreach (var filter in trendFilters)
                        {
                            // Arm 1: hold to reversal, crossed with stop management
                            // (breakeven move) and the daily circuit breaker.
                            foreach (var floor in profitFloors)
                            foreach (var be in breakEvenMoves)
                            foreach (var dayStop in dailyLossLimits)
                            {
                                configs.Add(new BacktestConfiguration(
                                    configId: configId++,
                                    maximumContracts: contracts,
                                    adaptiveRiskMultiplier: 0,
                                    stopDistanceRisk: stop,
                                    liquidityCapacity: hold,
                                    useTrailingStop: false,
                                    trendFilterBars: filter,
                                    breakEvenMovePoints: be,
                                    holdToReversal: true,
                                    profitFloorDollars: floor,
                                    dailyLossLimitDollars: dayStop));
                            }

                            // Arm 2: fixed profit target - the rule Devon actually
                            // trades - crossed with the daily profit halt and the
                            // breakeven move, so "close at TP" and "stop at $500 for
                            // the day" are measured together rather than assumed.
                            foreach (var risk in riskMultipliers)
                            foreach (var dayGoal in dailyProfitTargets)
                            foreach (var be in breakEvenMoves)
                            foreach (var dayStop in dailyLossLimits)
                            {
                                configs.Add(new BacktestConfiguration(
                                    configId: configId++,
                                    maximumContracts: contracts,
                                    adaptiveRiskMultiplier: risk,
                                    stopDistanceRisk: stop,
                                    liquidityCapacity: hold,
                                    useTrailingStop: false,
                                    trendFilterBars: filter,
                                    breakEvenMovePoints: be,
                                    holdToReversal: false,
                                    profitFloorDollars: 0m,
                                    dailyLossLimitDollars: dayStop,
                                    dailyProfitTargetDollars: dayGoal));
                            }

                            // Arm 3: trailing stop (riskMultiplier is unread here)
                            foreach (var dayStop in dailyLossLimits)
                            {
                                configs.Add(new BacktestConfiguration(
                                    configId: configId++,
                                    maximumContracts: contracts,
                                    adaptiveRiskMultiplier: 0,
                                    stopDistanceRisk: stop,
                                    liquidityCapacity: hold,
                                    useTrailingStop: true,
                                    trendFilterBars: filter,
                                    breakEvenMovePoints: 0,
                                    holdToReversal: false,
                                    profitFloorDollars: 0m,
                                    dailyLossLimitDollars: dayStop));
                            }
                        }
                    }
                }
            }

            return configs;
        }
    }
}
