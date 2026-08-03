using Xunit;

namespace ISE.OrderFlow.Tests;

public sealed class OrderFlowEngineTests
{
    private readonly OrderFlowEngine _engine = new();

    [Fact]
    public void Ask_dominance_produces_bullish_bias()
    {
        var result = _engine.Evaluate(CreateInput(
            new OrderFlowLevel(100m, 10, 40),
            new OrderFlowLevel(101m, 20, 80),
            new OrderFlowLevel(102m, 30, 60)));

        Assert.Equal(OrderFlowBias.Bullish, result.Bias);
        Assert.True(result.Delta > 0);
        Assert.Equal(2, result.BullishImbalances);
    }

    [Fact]
    public void Bid_dominance_produces_bearish_bias()
    {
        var result = _engine.Evaluate(CreateInput(
            new OrderFlowLevel(100m, 60, 20),
            new OrderFlowLevel(101m, 90, 20),
            new OrderFlowLevel(102m, 40, 20)));

        Assert.Equal(OrderFlowBias.Bearish, result.Bias);
        Assert.True(result.Delta < 0);
        Assert.Equal(2, result.BearishImbalances);
    }

    [Fact]
    public void High_volume_low_delta_level_detects_absorption()
    {
        var result = _engine.Evaluate(CreateInput(
            new OrderFlowLevel(100m, 10, 12),
            new OrderFlowLevel(101m, 100, 105),
            new OrderFlowLevel(102m, 10, 12)));

        Assert.True(result.AbsorptionDetected);
        Assert.Equal(OrderFlowBias.Neutral, result.Bias);
    }

    private static OrderFlowInput CreateInput(params OrderFlowLevel[] levels) =>
        new(DateTime.UtcNow, Guid.NewGuid(), levels);
}
