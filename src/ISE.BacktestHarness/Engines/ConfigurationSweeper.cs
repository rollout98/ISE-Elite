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

            // MaximumContracts: test 1-5 for proper sizing analysis
            var contractCounts = new[] { 1, 2, 3, 4, 5 };

            // StopDistanceRisk: instrument-specific, scaled to comparable dollar risk.
            // MNQ: 60, 75, 87.5 points ($2/pt -> $120-$175 risk per contract). Validated live.
            // MGC: 5-30 points ($10/pt -> $50-$300 risk per contract). Gold trades ~4100,
            //      so a 50pt stop would be a $50 move -- more than a full day's range.
            //      87.5 MNQ pts = $175/contract; the MGC equivalent is ~17.5 pts.
            var stopDistances = _instrument == "MGC"
                ? new[] { 5.0, 7.5, 10.0, 12.5, 15.0, 17.5, 20.0, 25.0, 30.0 }
                : new[] { 60.0, 75.0, 87.5 };

            // Breakeven trigger points, also instrument-scaled.
            // MNQ live: move to BE at +62.5/75 pts (roughly 0.7-0.85x the stop).
            var breakEvenMoves = _instrument == "MGC"
                ? new[] { 7.5, 12.5 }
                : new[] { 62.5, 75.0 };

            // Minimum stop at which breakeven logic is worth applying.
            var beMinStop = _instrument == "MGC" ? 10.0 : 60.0;

            // AdaptiveRiskMultiplier: reward:risk ratio. Profit target = stop * this.
            var riskMultipliers = new[] { 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 10.0 };

            // LiquidityCapacity: MAX HOLD in BARS (minutes on 1-min data)
            var liquidityCapacities = new[] { 50.0, 120.0, 240.0, 480.0, 1440.0 };

            // Generate configurations: for MGC, will be ~1,400 (5 contracts × 7 stops × 8 risk × 5 holds...)
            foreach (var contracts in contractCounts)
            {
                foreach (var risk in riskMultipliers)
                {
                    foreach (var stop in stopDistances)
                    {
                        // 3 hold limits per combo
                        foreach (var i in new[] { 0, 2, 4 }) // 50, 240, 1440 bars
                        {
                            // Higher-timeframe gate: off, 1 hour, 4 hours.
                            foreach (var filter in new[] { 0, 60, 240 })
                            {
                                // Fixed target
                                configs.Add(new BacktestConfiguration(
                                    configId++, contracts, risk, stop,
                                    liquidityCapacities[i], false, filter, 0));

                                // With breakeven: once profit reaches the trigger, stop moves to entry.
                                // Only applied to stops wide enough for it to mean anything.
                                if (stop >= beMinStop)
                                {
                                    foreach (var beMove in breakEvenMoves)
                                    {
                                        configs.Add(new BacktestConfiguration(
                                            configId++, contracts, risk, stop,
                                            liquidityCapacities[i], false, filter, beMove));
                                    }
                                }

                                // Trailing. RiskMult is irrelevant here, so emit one
                                // trailing variant per (contracts, stop, hold, filter).
                                if (Math.Abs(risk - riskMultipliers[0]) < 0.001)
                                {
                                    configs.Add(new BacktestConfiguration(
                                        configId++, contracts, risk, stop,
                                        liquidityCapacities[i], true, filter, 0));
                                }
                            }
                        }
                    }
                }
            }

            return configs;
        }
    }
}
