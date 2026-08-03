using ISE.TradePlanning;
using Xunit;

namespace ISE.TradePlanning.Tests;

public sealed class TradePlanningEngineTests
{
    [Fact]
    public void Long_plan_places_stop_below_and_target_above_entry()
    {
        var engine = new TradePlanningEngine();
        var result = engine.Evaluate(new TradePlanInput(true, true, true, TradeDirection.Long, 2, 20000m, 50m, 2m, EntryOrderType.Limit));

        Assert.True(result.Approved);
        Assert.Equal(19950m, result.StopPrice);
        Assert.Equal(20100m, result.TargetPrice);
        Assert.Equal(2, result.Contracts);
    }

    [Fact]
    public void Short_plan_places_stop_above_and_target_below_entry()
    {
        var engine = new TradePlanningEngine();
        var result = engine.Evaluate(new TradePlanInput(true, true, true, TradeDirection.Short, 1, 2500m, 10m, 3m, EntryOrderType.Market));

        Assert.True(result.Approved);
        Assert.Equal(2510m, result.StopPrice);
        Assert.Equal(2470m, result.TargetPrice);
    }

    [Fact]
    public void Objective_block_rejects_plan()
    {
        var engine = new TradePlanningEngine();
        var result = engine.Evaluate(new TradePlanInput(true, true, false, TradeDirection.Long, 1, 100m, 5m, 2m, EntryOrderType.Market));

        Assert.False(result.Approved);
        Assert.Equal(TradePlanReason.ObjectiveNotPermitted, result.Reason);
    }

    [Fact]
    public void Risk_rejection_prevents_plan_creation()
    {
        var engine = new TradePlanningEngine();
        var result = engine.Evaluate(new TradePlanInput(true, false, true, TradeDirection.Long, 1, 100m, 5m, 2m, EntryOrderType.StopMarket));

        Assert.False(result.Approved);
        Assert.Equal(TradePlanReason.RiskNotApproved, result.Reason);
    }
}
