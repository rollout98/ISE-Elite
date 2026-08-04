using Xunit;

namespace ISE.AdaptiveRisk.Tests;

public sealed class AdaptiveRiskEngineTests
{
    private readonly AdaptiveRiskEngine _engine = new();

    [Fact]
    public void Elite_alignment_allows_full_risk()
    {
        var result = _engine.Evaluate(new AdaptiveRiskInput
        {
            DecisionConfidence = 96, ExecutionQuality = 94, MarketHealth = 92,
            VolatilityRisk = 20, DrawdownPressure = 5, DailyObjectivePressure = 10
        });

        Assert.Equal(AdaptiveRiskAction.FullRisk, result.Action);
        Assert.Equal(1.0, result.RiskMultiplier);
    }

    [Fact]
    public void Elevated_volatility_reduces_risk()
    {
        var result = _engine.Evaluate(new AdaptiveRiskInput
        {
            DecisionConfidence = 90, ExecutionQuality = 88, MarketHealth = 86,
            VolatilityRisk = 85, DrawdownPressure = 10, DailyObjectivePressure = 10
        });

        Assert.Equal(AdaptiveRiskAction.ReducedRisk, result.Action);
        Assert.Equal(0.65, result.RiskMultiplier);
    }

    [Fact]
    public void Drawdown_pressure_forces_minimal_or_stand_aside()
    {
        var result = _engine.Evaluate(new AdaptiveRiskInput
        {
            DecisionConfidence = 82, ExecutionQuality = 80, MarketHealth = 78,
            VolatilityRisk = 45, DrawdownPressure = 85, DailyObjectivePressure = 20
        });

        Assert.True(result.Action is AdaptiveRiskAction.MinimalRisk or AdaptiveRiskAction.StandAside);
        Assert.True(result.RiskMultiplier <= 0.35);
    }

    [Fact]
    public void Authoritative_block_overrides_all_evidence()
    {
        var result = _engine.Evaluate(new AdaptiveRiskInput
        {
            DecisionConfidence = 100, ExecutionQuality = 100, MarketHealth = 100,
            VolatilityRisk = 0, DrawdownPressure = 0, DailyObjectivePressure = 0,
            AuthoritativeRiskBlock = true
        });

        Assert.Equal(AdaptiveRiskAction.Blocked, result.Action);
        Assert.Equal(0, result.RiskMultiplier);
    }
}
