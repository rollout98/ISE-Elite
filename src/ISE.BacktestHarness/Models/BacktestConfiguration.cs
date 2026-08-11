namespace ISE.BacktestHarness.Models
{
    /// <summary>
    /// Parameter configuration for a single backtest run
    /// </summary>
    public sealed class BacktestConfiguration
    {
        public BacktestConfiguration(
            int configId,
            int maximumContracts,
            double adaptiveRiskMultiplier,
            double stopDistanceRisk,
            double liquidityCapacity,
            bool useTrailingStop = false,
            int trendFilterBars = 0)
        {
            ConfigId = configId;
            MaximumContracts = maximumContracts;
            AdaptiveRiskMultiplier = adaptiveRiskMultiplier;
            StopDistanceRisk = stopDistanceRisk;
            LiquidityCapacity = liquidityCapacity;
            UseTrailingStop = useTrailingStop;
            TrendFilterBars = trendFilterBars;
        }

        public int ConfigId { get; }
        public int MaximumContracts { get; }
        public double AdaptiveRiskMultiplier { get; }
        public double StopDistanceRisk { get; }
        public double LiquidityCapacity { get; }

        /// <summary>
        /// When true there is NO fixed profit target. The stop trails the best price
        /// reached by StopDistanceRisk points, so the trade rides a run of unknown
        /// size until it reverses. A fixed target cannot capture a 600-900 tick move
        /// because the move's size is not knowable at entry.
        /// </summary>
        public bool UseTrailingStop { get; }

        /// <summary>
        /// Higher-timeframe gate, in 1-minute bars. 0 disables it. The 1-min entry
        /// only fires when it agrees with this slower trend, which is what separates
        /// the day's real move from the ~72 wiggles a day that look the same to a
        /// short-memory crossover.
        /// </summary>
        public int TrendFilterBars { get; }

        public override string ToString()
        {
            return $"Config{ConfigId}: Contracts={MaximumContracts}, " +
                   $"RiskMult={AdaptiveRiskMultiplier:F2}, " +
                   $"StopDist={StopDistanceRisk:F2}, " +
                   $"MaxHold={LiquidityCapacity:F0}bars, " +
                   (UseTrailingStop
                       ? $"Exit=TRAIL {StopDistanceRisk:F0}pt"
                       : $"Exit=FIXED {StopDistanceRisk * AdaptiveRiskMultiplier:F0}pt") +
                   (TrendFilterBars > 0 ? $", Filter={TrendFilterBars}bar" : ", Filter=OFF");
        }
    }
}
