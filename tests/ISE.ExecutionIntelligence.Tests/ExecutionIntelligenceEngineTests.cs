using ISE.ExecutionIntelligence;
using Xunit;

namespace ISE.ExecutionIntelligence.Tests;

public sealed class ExecutionIntelligenceEngineTests
{
    private readonly ExecutionIntelligenceEngine engine = new();

    [Fact]
    public void News_lock_rejects_trade()
    {
        var result = engine.Evaluate(new ExecutionIntelligenceInput(4, 1m, 95m, 50m, 98m, newsLock: true));

        Assert.False(result.Approved);
        Assert.Equal(ExecutionMode.Reject, result.Mode);
        Assert.Equal(ExecutionReason.NewsLock, result.Reason);
        Assert.Equal(0, result.Contracts);
    }

    [Fact]
    public void Wide_spread_uses_passive_limit()
    {
        var result = engine.Evaluate(new ExecutionIntelligenceInput(4, 5m, 80m, 50m, 90m));

        Assert.True(result.Approved);
        Assert.Equal(ExecutionMode.PassiveLimit, result.Mode);
        Assert.Equal(ExecutionReason.WideSpread, result.Reason);
        Assert.Equal(4, result.Contracts);
    }

    [Fact]
    public void Extreme_volatility_reduces_position_size()
    {
        var result = engine.Evaluate(new ExecutionIntelligenceInput(5, 2m, 75m, 95m, 88m));

        Assert.True(result.Approved);
        Assert.Equal(ExecutionMode.AggressiveLimit, result.Mode);
        Assert.Equal(ExecutionReason.ExtremeVolatility, result.Reason);
        Assert.Equal(2, result.Contracts);
    }

    [Fact]
    public void Elite_setup_uses_market_order()
    {
        var result = engine.Evaluate(new ExecutionIntelligenceInput(4, 1m, 95m, 60m, 98m));

        Assert.True(result.Approved);
        Assert.Equal(ExecutionMode.Market, result.Mode);
        Assert.Equal(ExecutionReason.EliteImmediateExecution, result.Reason);
        Assert.Equal(4, result.Contracts);
    }
}
