using ISE.MarketData;
using Xunit;

namespace ISE.MarketStructure.Tests;

public sealed class MarketStructureEngineTests
{
    private readonly MarketStructureEngine _engine = new();

    [Fact]
    public void Rising_swings_produce_bullish_structure()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (10m, 8m, 9m),
            (12m, 9m, 11m),
            (9m, 7m, 8m),
            (13m, 10m, 12m),
            (11m, 8m, 10m),
            (12m, 9m, 11m)
        }));

        Assert.Equal(StructureDirection.Bullish, result.Direction);
        Assert.Contains(result.Swings, swing => swing.Classification == StructureClassification.HigherHigh);
        Assert.Contains(result.Swings, swing => swing.Classification == StructureClassification.HigherLow);
    }

    [Fact]
    public void Falling_swings_produce_bearish_structure()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (12m, 10m, 11m),
            (13m, 11m, 12m),
            (11m, 8m, 9m),
            (12m, 10m, 11m),
            (10m, 7m, 8m),
            (11m, 8m, 9m)
        }));

        Assert.Equal(StructureDirection.Bearish, result.Direction);
        Assert.Contains(result.Swings, swing => swing.Classification == StructureClassification.LowerHigh);
        Assert.Contains(result.Swings, swing => swing.Classification == StructureClassification.LowerLow);
    }

    [Fact]
    public void Close_above_latest_swing_high_produces_bullish_break_of_structure()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (10m, 8m, 9m),
            (12m, 9m, 11m),
            (9m, 7m, 8m),
            (13m, 10m, 12m),
            (11m, 8m, 10m),
            (15m, 9m, 14m)
        }));

        Assert.True(result.BullishBreakOfStructure);
        Assert.False(result.BearishBreakOfStructure);
    }

    private static MarketStructureInput CreateInput((decimal High, decimal Low, decimal Close)[] values)
    {
        var start = new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc);
        var candles = values
            .Select((value, index) => new Candle(
                "MNQ",
                Timeframe.OneMinute,
                start.AddMinutes(index),
                start.AddMinutes(index + 1),
                value.Close,
                value.High,
                value.Low,
                value.Close,
                100))
            .ToArray();

        return new MarketStructureInput(
            candles[^1].CloseTimeUtc,
            Guid.NewGuid(),
            "2026-08-03",
            candles);
    }
}
