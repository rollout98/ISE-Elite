using Xunit;

namespace ISE.MarketIntelligence.Tests;

public sealed class MarketIntelligenceEngineTests
{
    private readonly MarketIntelligenceEngine _engine = new();

    [Fact]
    public void Trend_expansion_classified_correctly()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.9m, 0.85m, 0.2m, 0.65m, 0.1m, 0.85m, 0.2m, 0.75m, 0.8m, 0.9m, 0.9m));

        Assert.Equal(MarketRegime.TrendExpansion, result.Regime);
        Assert.Equal(RecommendedEnvironment.TrendFollowing, result.RecommendedEnvironment);
        Assert.Equal(InstitutionalBias.Buying, result.InstitutionalBias);
    }

    [Fact]
    public void Balanced_auction_identified()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.25m, 0.3m, 0.9m, 0.15m, 0.1m, 0.75m, 0.2m, 0.45m, 0m, 0.7m, 0.8m));

        Assert.Equal(MarketRegime.BalancedAuction, result.Regime);
        Assert.Equal(AuctionState.Balanced, result.Auction);
        Assert.Equal(RecommendedEnvironment.MeanReversion, result.RecommendedEnvironment);
    }

    [Fact]
    public void High_volatility_detected()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.7m, 0.65m, 0.3m, 0.5m, 0.2m, 0.7m, 0.3m, 0.95m, 0.4m, 0.75m, 0.75m));

        Assert.Equal(VolatilityRegime.Extreme, result.Volatility);
        Assert.True(result.RiskModifier <= 0.5m);
    }

    [Fact]
    public void Low_liquidity_detected()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.55m, 0.5m, 0.4m, 0.35m, 0.2m, 0.15m, 0.1m, 0.55m, 0.1m, 0.6m, 0.65m));

        Assert.Equal(LiquidityEnvironment.Vacuum, result.Liquidity);
        Assert.Equal(MarketHealth.AvoidTrading, result.Health);
        Assert.Equal(RecommendedEnvironment.StandAside, result.RecommendedEnvironment);
    }

    [Fact]
    public void Recommended_environment_matches_market()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.8m, 0.7m, 0.25m, 0.9m, 0.1m, 0.8m, 0.2m, 0.7m, -0.7m, 0.85m, 0.85m));

        Assert.Equal(MarketRegime.Breakout, result.Regime);
        Assert.Equal(RecommendedEnvironment.Breakout, result.RecommendedEnvironment);
        Assert.Equal(InstitutionalBias.Selling, result.InstitutionalBias);
    }

    [Fact]
    public void Poor_market_health_recommends_stand_aside()
    {
        var result = _engine.Evaluate(new MarketIntelligenceInput(
            0.2m, 0.2m, 0.25m, 0.1m, 0.1m, 0.3m, 0.1m, 0.15m, 0m, 0.2m, 0.25m));

        Assert.True(result.Health == MarketHealth.Poor || result.Health == MarketHealth.AvoidTrading);
        Assert.Equal(RecommendedEnvironment.StandAside, result.RecommendedEnvironment);
        Assert.True(result.Reasons.Count >= 7);
    }
}
