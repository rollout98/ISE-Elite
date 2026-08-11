using System;
using System.Collections.Generic;
using ISE.BacktestHarness.Models;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Generates parameter configurations for backtest sweeping
    /// </summary>
    public sealed class ConfigurationSweeper
    {
        /// <summary>
        /// Generate 100+ parameter combinations to test
        /// </summary>
        public IReadOnlyList<BacktestConfiguration> GenerateConfigurations()
        {
            var configs = new List<BacktestConfiguration>();
            var configId = 1;

            // MaximumContracts: 1-4 (5 values)
            var contractCounts = new[] { 1, 2, 3, 4 };

            // StopDistanceRisk: stop distance in POINTS below entry (5 values).
            // Was documented as "ticks" but never consumed by the engine.
            // Devon's live method: 350-tick stop = 87.5 points, move to BE at 250-300 ticks (62.5-75 pts).
            // Sweep: realistic stops (60-90pt) with/without breakeven logic.
            // Dropped tight stops (2-25pt) — they don't match your real trading.
            var stopDistances = new[] { 60.0, 75.0, 87.5 };

            // AdaptiveRiskMultiplier: reward:risk ratio. Profit target = stop * this.
            // 0.5 = scalper (target smaller than stop, high win rate, small wins)
            // 3.0 = trend rider (target far beyond stop, low win rate, large wins)
            // 25pt stop x 10R = 250pt target = 1000 ticks, enough for the largest
            // runs observed in the data. Ignored entirely in trailing mode.
            var riskMultipliers = new[] { 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0, 10.0 };

            // LiquidityCapacity: repurposed as MAX HOLD in BARS (minutes on 1-min data).
            // Was never consumed by the engine, which is why every config appeared three
            // times with identical results. The engine's old hard-coded 50-bar cap made
            // trend configs impossible: a 60pt target rarely completes in 50 minutes, so
            // trades were force-closed mid-move.
            //   50   = ~1hr   (scalp)
            //   240  = 4hrs   (session swing)
            //   480  = 8hrs   (hold London into NY)
            //   1440 = 24hrs  (hold until trend actually ends)
            var liquidityCapacities = new[] { 50.0, 120.0, 240.0, 480.0, 1440.0 };

            // Generate cartesian product: 4 * 7 * 5 * 5 = 700 configs (too many)
            // Instead: sample systematically
            foreach (var contracts in contractCounts)
            {
                foreach (var risk in riskMultipliers)
                {
                    foreach (var stop in stopDistances)
                    {
                        // 3 hold limits per combo to keep the sweep near 420 configs
                        foreach (var i in new[] { 0, 2, 4 }) // 50, 240, 1440 bars
                        {
                            // Higher-timeframe gate: off, 1 hour, 4 hours.
                            foreach (var filter in new[] { 0, 60, 240 })
                            {
                                // Fixed target
                                configs.Add(new BacktestConfiguration(
                                    configId++, contracts, risk, stop,
                                    liquidityCapacities[i], false, filter, 0));

                                // With breakeven: once profit reaches 62.5 or 75 pts, stop moves to entry
                                // (only for realistic stop sizes: 60+)
                                if (stop >= 60)
                                {
                                    foreach (var beMove in new[] { 62.5, 75.0 })
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
