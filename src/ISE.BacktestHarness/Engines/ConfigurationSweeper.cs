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
            var stopDistances = new[] { 2.0, 4.0, 6.0, 10.0, 15.0 };

            // AdaptiveRiskMultiplier: reward:risk ratio. Profit target = stop * this.
            // 0.5 = scalper (target smaller than stop, high win rate, small wins)
            // 3.0 = trend rider (target far beyond stop, low win rate, large wins)
            var riskMultipliers = new[] { 0.5, 1.0, 1.5, 2.0, 3.0, 4.0, 6.0 };

            // LiquidityCapacity: 50%-150% in 25% steps (5 values)
            var liquidityCapacities = new[] { 50.0, 75.0, 100.0, 125.0, 150.0 };

            // Generate cartesian product: 4 * 7 * 5 * 5 = 700 configs (too many)
            // Instead: sample systematically
            foreach (var contracts in contractCounts)
            {
                foreach (var risk in riskMultipliers)
                {
                    foreach (var stop in stopDistances)
                    {
                        // Take only 3 liquidity values per combo to reduce to ~420
                        foreach (var i in new[] { 0, 2, 4 }) // 50%, 100%, 150%
                        {
                            configs.Add(new BacktestConfiguration(
                                configId++,
                                contracts,
                                risk,
                                stop,
                                liquidityCapacities[i]));
                        }
                    }
                }
            }

            return configs;
        }
    }
}
