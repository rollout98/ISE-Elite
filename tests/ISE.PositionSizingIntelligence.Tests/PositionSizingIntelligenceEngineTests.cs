using Xunit;

namespace ISE.PositionSizingIntelligence.Tests;

public sealed class PositionSizingIntelligenceEngineTests
{
    private readonly PositionSizingIntelligenceEngine _engine = new();

    [Fact]
    public void Elite_alignment_uses_full_size()
    {
        var decision = _engine.Evaluate(new PositionSizingInput
        {
            MaximumContracts = 4,
            ParticipationMultiplier = 1.0,
            AdaptiveRiskMultiplier = 1.0,
            StopDistanceRisk = 0,
            LiquidityCapacity = 100,
            AccountPressure = 0
        });

        Assert.Equal(PositionSizingAction.FullSize, decision.Action);
        Assert.Equal(4, decision.Contracts);
    }

    [Fact]
    public void Reduced_risk_reduces_contract_count()
    {
        var decision = _engine.Evaluate(new PositionSizingInput
        {
            MaximumContracts = 6,
            ParticipationMultiplier = 0.75,
            AdaptiveRiskMultiplier = 0.65,
            StopDistanceRisk = 10,
            LiquidityCapacity = 90,
            AccountPressure = 10
        });

        Assert.Equal(PositionSizingAction.MinimalSize, decision.Action);
        Assert.Equal(2, decision.Contracts);
    }

    [Fact]
    public void Severe_constraints_produce_no_trade()
    {
        var decision = _engine.Evaluate(new PositionSizingInput
        {
            MaximumContracts = 3,
            ParticipationMultiplier = 0.4,
            AdaptiveRiskMultiplier = 0.35,
            StopDistanceRisk = 80,
            LiquidityCapacity = 30,
            AccountPressure = 80
        });

        Assert.Equal(PositionSizingAction.NoTrade, decision.Action);
        Assert.Equal(0, decision.Contracts);
    }

    [Fact]
    public void Authoritative_block_overrides_size()
    {
        var decision = _engine.Evaluate(new PositionSizingInput
        {
            MaximumContracts = 10,
            ParticipationMultiplier = 1.0,
            AdaptiveRiskMultiplier = 1.0,
            LiquidityCapacity = 100,
            AuthoritativeRiskBlock = true
        });

        Assert.Equal(PositionSizingAction.Blocked, decision.Action);
        Assert.Equal(0, decision.Contracts);
    }
}
