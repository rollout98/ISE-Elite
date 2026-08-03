using ISE.Strategy;
using Xunit;

namespace ISE.Strategy.Tests;

public sealed class StrategyEngineTests
{
    [Fact]
    public void New_york_reversal_qualifies_with_opposing_prior_trend()
    {
        var profile = new StrategyProfile(StrategyId.NewYorkOpenReversal, 80, true, true, true);
        var input = new StrategyInput(profile, true, true, 88, true, true, true, false, true);

        var result = new StrategyEngine().Evaluate(input);

        Assert.True(result.Qualified);
        Assert.Equal(StrategyDecisionReason.Qualified, result.Reason);
    }

    [Fact]
    public void New_york_continuation_qualifies_with_aligned_trend()
    {
        var profile = new StrategyProfile(StrategyId.NewYorkContinuation, 75, false, true, true);
        var input = new StrategyInput(profile, true, true, 82, false, true, true, true, false);

        var result = new StrategyEngine().Evaluate(input);

        Assert.True(result.Qualified);
        Assert.Equal(StrategyId.NewYorkContinuation, result.StrategyId);
    }

    [Fact]
    public void Reversal_rejects_when_liquidity_event_is_missing()
    {
        var profile = new StrategyProfile(StrategyId.NewYorkOpenReversal, 80, true, true, true);
        var input = new StrategyInput(profile, true, true, 90, false, true, true, false, true);

        var result = new StrategyEngine().Evaluate(input);

        Assert.False(result.Qualified);
        Assert.Equal(StrategyDecisionReason.LiquidityRequirementNotMet, result.Reason);
    }

    [Fact]
    public void Continuation_rejects_when_confidence_is_below_threshold()
    {
        var profile = new StrategyProfile(StrategyId.NewYorkContinuation, 80, false, true, true);
        var input = new StrategyInput(profile, true, true, 79, false, true, true, true, false);

        var result = new StrategyEngine().Evaluate(input);

        Assert.False(result.Qualified);
        Assert.Equal(StrategyDecisionReason.ConfidenceBelowMinimum, result.Reason);
    }
}
