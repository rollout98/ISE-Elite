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
            bool useTrailingStop = false)
        {
            ConfigId = configId;
            MaximumContracts = maximumContracts;
            AdaptiveRiskMultiplier = adaptiveRiskMultiplier;
            StopDistanceRisk = stopDistanceRisk;
            LiquidityCapacity = liquidityCapacity;
            UseTrailingStop = useTrailingStop;
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

        public override string ToString()
        {
            return $"Config{ConfigId}: Contracts={MaximumContracts}, " +
                   $"RiskMult={AdaptiveRiskMultiplier:F2}, " +
                   $"StopDist={StopDistanceRisk:F2}, " +
                   $"MaxHold={LiquidityCapacity:F0}bars, " +
                   (UseTrailingStop
                       ? $"Exit=TRAIL {StopDistanceRisk:F0}pt"
                       : $"Exit=FIXED {StopDistanceRisk * AdaptiveRiskMultiplier:F0}pt");
        }
    }
}
