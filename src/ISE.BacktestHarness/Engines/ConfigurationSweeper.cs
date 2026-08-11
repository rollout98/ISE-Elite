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
