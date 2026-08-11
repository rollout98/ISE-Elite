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
            double liquidityCapacity)
        {
            ConfigId = configId;
            MaximumContracts = maximumContracts;
            AdaptiveRiskMultiplier = adaptiveRiskMultiplier;
            StopDistanceRisk = stopDistanceRisk;
            LiquidityCapacity = liquidityCapacity;
        }

        public int ConfigId { get; }
        public int MaximumContracts { get; }
        public double AdaptiveRiskMultiplier { get; }
        public double StopDistanceRisk { get; }
        public double LiquidityCapacity { get; }

        public override string ToString()
        {
            return $"Config{ConfigId}: Contracts={MaximumContracts}, " +
                   $"RiskMult={AdaptiveRiskMultiplier:F2}, " +
                   $"StopDist={StopDistanceRisk:F2}, " +
                   $"MaxHold={LiquidityCapacity:F0}bars";
        }
    }
}
