using Xunit;

namespace ISE.Risk.Tests;

public sealed class RiskEngineTests
{
    [Fact]
    public void Sizes_position_using_most_restrictive_limit()
    {
        var profile = new PropAccountProfile("Example", AccountSize.Size50K, 5, 600m, 1000m, 2500m);
        var result = new RiskEngine().Evaluate(new RiskInput(profile, true, 175m, 100m, 900m, 0));

        Assert.True(result.Approved);
        Assert.Equal(3, result.Contracts);
        Assert.Equal(525m, result.TotalRisk);
    }

    [Fact]
    public void Different_account_profiles_produce_different_contract_sizes()
    {
        var small = new PropAccountProfile("Example", AccountSize.Size25K, 2, 250m, 500m, 1500m);
        var large = new PropAccountProfile("Example", AccountSize.Size150K, 10, 1200m, 2500m, 5000m);
        var engine = new RiskEngine();

        var smallResult = engine.Evaluate(new RiskInput(small, true, 200m, 0m, 1000m, 0));
        var largeResult = engine.Evaluate(new RiskInput(large, true, 200m, 0m, 4000m, 0));

        Assert.Equal(1, smallResult.Contracts);
        Assert.Equal(6, largeResult.Contracts);
    }

    [Fact]
    public void Daily_trade_limit_rejects_candidate()
    {
        var profile = new PropAccountProfile("Example", AccountSize.Size100K, 8, 800m, 1500m, 3000m, maximumTradesPerDay: 2);
        var result = new RiskEngine().Evaluate(new RiskInput(profile, true, 200m, 0m, 2500m, 2));

        Assert.False(result.Approved);
        Assert.Equal(RiskDecisionReason.TradeLimitReached, result.Reason);
    }

    [Fact]
    public void Insufficient_capacity_rejects_stop_risk()
    {
        var profile = new PropAccountProfile("Example", AccountSize.Size25K, 2, 250m, 500m, 1500m);
        var result = new RiskEngine().Evaluate(new RiskInput(profile, true, 300m, 0m, 1000m, 0));

        Assert.False(result.Approved);
        Assert.Equal(RiskDecisionReason.StopRiskExceedsCapacity, result.Reason);
    }
}
