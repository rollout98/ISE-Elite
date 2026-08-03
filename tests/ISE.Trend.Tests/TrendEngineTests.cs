using Xunit;

namespace ISE.Trend.Tests;

public sealed class TrendEngineTests
{
    private readonly TrendEngine _engine = new();

    [Fact]
    public void Aligned_bullish_evidence_produces_strong_bullish_trend()
    {
        var result = _engine.Process(new TrendInput(
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), "2026-08-03",
            101m, 100m, 102m, 100.5m, 0.80m, 0.80m));

        Assert.Equal(TrendDirection.Bullish, result.Direction);
        Assert.Equal(TrendStrength.Strong, result.Strength);
        Assert.True(result.Confidence >= 80);
        Assert.False(result.IsRanging);
    }

    [Fact]
    public void Aligned_bearish_evidence_produces_strong_bearish_trend()
    {
        var result = _engine.Process(new TrendInput(
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), "2026-08-03",
            99m, 100m, 98m, 99.5m, -0.80m, 0.75m));

        Assert.Equal(TrendDirection.Bearish, result.Direction);
        Assert.Equal(TrendStrength.Strong, result.Strength);
        Assert.False(result.IsRanging);
    }

    [Fact]
    public void Low_efficiency_produces_neutral_ranging_state()
    {
        var result = _engine.Process(new TrendInput(
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), "2026-08-03",
            101m, 100m, 102m, 100.5m, 0.50m, 0.10m));

        Assert.Equal(TrendDirection.Neutral, result.Direction);
        Assert.Equal(TrendStrength.None, result.Strength);
        Assert.True(result.IsRanging);
    }

    [Fact]
    public void Conflicting_directional_evidence_produces_neutral_state()
    {
        var result = _engine.Process(new TrendInput(
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc), Guid.NewGuid(), "2026-08-03",
            101m, 100m, 99m, 100m, -0.50m, 0.70m));

        Assert.Equal(TrendDirection.Neutral, result.Direction);
        Assert.True(result.IsRanging);
    }
}
