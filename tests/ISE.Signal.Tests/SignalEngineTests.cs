using ISE.MarketStructure;
using ISE.OrderFlow;
using ISE.Trend;

namespace ISE.Signal.Tests;

public sealed class SignalEngineTests
{
    [Fact]
    public void Aligned_bullish_evidence_produces_eligible_long_signal()
    {
        var engine = new SignalEngine();
        var input = new SignalInput(
            true,
            TrendDirection.Bullish,
            StructureDirection.Bullish,
            true,
            false,
            OrderFlowBias.Bullish);

        var result = engine.Evaluate(input);

        Assert.Equal(SignalDirection.Long, result.Direction);
        Assert.Equal(100, result.Confidence);
        Assert.True(result.ExecutionEligible);
        Assert.Contains(SignalReason.SellSideLiquidityReclaimed, result.Reasons);
    }

    [Fact]
    public void Aligned_bearish_evidence_produces_eligible_short_signal()
    {
        var engine = new SignalEngine();
        var input = new SignalInput(
            true,
            TrendDirection.Bearish,
            StructureDirection.Bearish,
            false,
            true,
            OrderFlowBias.Bearish);

        var result = engine.Evaluate(input);

        Assert.Equal(SignalDirection.Short, result.Direction);
        Assert.Equal(100, result.Confidence);
        Assert.True(result.ExecutionEligible);
        Assert.Contains(SignalReason.BuySideLiquidityReclaimed, result.Reasons);
    }

    [Fact]
    public void Session_block_prevents_execution_even_with_full_alignment()
    {
        var engine = new SignalEngine();
        var input = new SignalInput(
            false,
            TrendDirection.Bullish,
            StructureDirection.Bullish,
            true,
            false,
            OrderFlowBias.Bullish);

        var result = engine.Evaluate(input);

        Assert.Equal(SignalDirection.None, result.Direction);
        Assert.Equal(100, result.Confidence);
        Assert.False(result.ExecutionEligible);
        Assert.Equal(new[] { SignalReason.TradingBlocked }, result.Reasons);
    }

    [Fact]
    public void Conflicting_equal_evidence_produces_no_signal()
    {
        var engine = new SignalEngine();
        var input = new SignalInput(
            true,
            TrendDirection.Bullish,
            StructureDirection.Bearish,
            false,
            true,
            OrderFlowBias.Bullish);

        var result = engine.Evaluate(input);

        Assert.Equal(SignalDirection.None, result.Direction);
        Assert.Equal(50, result.Confidence);
        Assert.False(result.ExecutionEligible);
        Assert.Contains(SignalReason.ConflictingEvidence, result.Reasons);
    }
}
