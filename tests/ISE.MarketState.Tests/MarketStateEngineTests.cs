using Xunit;

namespace ISE.MarketState.Tests;

public sealed class MarketStateEngineTests
{
    private readonly MarketStateEngine _engine = new MarketStateEngine();

    [Fact]
    public void Persistent_positive_direction_produces_bull_trend()
    {
        var result = _engine.Evaluate(new MarketStateInput(0.78m, 0.82m, 1.10m, 0.50m, 0.10m, 0.20m, 0.10m, 0.10m));

        Assert.Equal(MarketStateClassification.BullTrend, result.Classification);
        Assert.True(result.Confidence >= 0.75m);
    }

    [Fact]
    public void Contracting_range_produces_compression()
    {
        var result = _engine.Evaluate(new MarketStateInput(0.05m, 0.25m, 0.65m, 0.20m, 0.10m, 0.10m, 0.10m, 0.10m));

        Assert.Equal(MarketStateClassification.Compression, result.Classification);
    }

    [Fact]
    public void Accepted_range_escape_produces_breakout()
    {
        var result = _engine.Evaluate(new MarketStateInput(0.40m, 0.50m, 1.50m, 0.82m, 0.05m, 0.88m, 0.10m, 0.10m));

        Assert.Equal(MarketStateClassification.Breakout, result.Classification);
    }

    [Fact]
    public void Reversal_evidence_has_precedence_over_prior_trend()
    {
        var result = _engine.Evaluate(new MarketStateInput(-0.70m, 0.80m, 1.20m, 0.50m, 0.20m, 0.20m, 0.90m, 0.20m));

        Assert.Equal(MarketStateClassification.Reversal, result.Classification);
        Assert.Contains("overrides", result.Reason);
    }
}
