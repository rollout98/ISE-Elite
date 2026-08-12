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
            var stopDistances = _instrument == "MGC"
                ? new[] { 5.0, 7.5, 10.0, 12.5, 15.0, 17.5, 20.0, 25.0, 30.0 }
                : new[] { 60.0, 75.0, 87.5 };

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

            // Hold-to-reversal only. There is no AdaptiveRiskMultiplier dimension here:
            // that value exists solely to compute a fixed profit target, and this
            // strategy has none. Sweeping it previously produced thousands of rows that
            // differed only in a field the engine no longer reads.
            foreach (var contracts in contractCounts)
            {
                foreach (var stop in stopDistances)
                {
                    foreach (var floor in profitFloors)
                    {
                        foreach (var hold in maxHolds)
                        {
                            foreach (var filter in trendFilters)
                            {
                                configs.Add(new BacktestConfiguration(
                                    configId: configId++,
                                    maximumContracts: contracts,
                                    adaptiveRiskMultiplier: 0,   // unused in reversal mode
                                    stopDistanceRisk: stop,
                                    liquidityCapacity: hold,
                                    useTrailingStop: false,
                                    trendFilterBars: filter,
                                    breakEvenMovePoints: 0,
                                    holdToReversal: true,
                                    profitFloorDollars: floor));
                            }
                        }
                    }
                }
            }

            return configs;
        }
    }
}
