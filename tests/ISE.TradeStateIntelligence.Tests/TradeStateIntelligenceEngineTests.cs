using Xunit;

namespace ISE.TradeStateIntelligence.Tests;

public sealed class TradeStateIntelligenceEngineTests
{
    private readonly TradeStateIntelligenceEngine _engine = new();

    [Fact]
    public void Strong_intact_trade_is_held()
    {
        var decision = _engine.Evaluate(new TradeStateInput { ThesisHealth = 90, MomentumHealth = 80, StructureIntegrity = 88, TargetProgress = 20 });
        Assert.Equal(TradeStateAction.Hold, decision.Action);
        Assert.Equal(TradeHealth.Strong, decision.Health);
    }

    [Fact]
    public void Meaningful_progress_moves_protection_to_break_even()
    {
        var decision = _engine.Evaluate(new TradeStateInput { ThesisHealth = 80, MomentumHealth = 70, StructureIntegrity = 78, FavorableExcursion = 50, TargetProgress = 55 });
        Assert.Equal(TradeStateAction.Protect, decision.Action);
        Assert.True(decision.MoveToBreakEven);
    }

    [Fact]
    public void Invalidated_thesis_exits()
    {
        var decision = _engine.Evaluate(new TradeStateInput { ThesisHealth = 25, MomentumHealth = 40, StructureIntegrity = 20 });
        Assert.Equal(TradeStateAction.Exit, decision.Action);
        Assert.Equal(TradeHealth.Invalidated, decision.Health);
    }

    [Fact]
    public void Authoritative_block_overrides_trade_state()
    {
        var decision = _engine.Evaluate(new TradeStateInput { ThesisHealth = 95, MomentumHealth = 90, StructureIntegrity = 92, AuthoritativeRiskBlock = true });
        Assert.Equal(TradeStateAction.Blocked, decision.Action);
        Assert.True(decision.TightenStop);
    }
}
