using Xunit;

namespace ISE.MarketData.Tests;

public sealed class MarketDataTests
{
    [Fact]
    public void Valid_candle_exposes_derived_properties()
    {
        var candle = new Candle(
            "mnq",
            Timeframe.Minute1,
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 3, 14, 1, 0, DateTimeKind.Utc),
            20000m,
            20010m,
            19995m,
            20008m,
            1250);

        Assert.Equal("MNQ", candle.Instrument);
        Assert.Equal(15m, candle.Range);
        Assert.Equal(8m, candle.BodySize);
        Assert.True(candle.IsBullish);
        Assert.False(candle.IsBearish);
    }

    [Fact]
    public void Invalid_candle_range_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Candle(
            "MGC",
            Timeframe.Minute1,
            new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 3, 14, 1, 0, DateTimeKind.Utc),
            2500m,
            2490m,
            2510m,
            2505m,
            100));
    }

    [Fact]
    public void Instrument_tick_value_is_calculated()
    {
        var instrument = new InstrumentInfo("mnq", "cme", 0.25m, 2m);

        Assert.Equal("MNQ", instrument.Symbol);
        Assert.Equal(0.50m, instrument.TickValue);
    }
}
