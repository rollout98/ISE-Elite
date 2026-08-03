using ISE.MarketData;
using Xunit;

namespace ISE.Liquidity.Tests;

public sealed class LiquidityEngineTests
{
    private readonly LiquidityEngine _engine = new();

    [Fact]
    public void Equal_highs_create_buy_side_zone()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (100m, 95m, 98m),
            (100.10m, 96m, 99m),
            (99m, 94m, 97m)
        }, 0.25m));

        Assert.Contains(result.Zones, zone => zone.Side == LiquiditySide.BuySide && zone.Touches == 2);
    }

    [Fact]
    public void Sweep_above_equal_highs_and_close_below_is_reclaimed()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (100m, 95m, 98m),
            (100.10m, 96m, 99m),
            (101m, 97m, 99.50m)
        }, 0.25m));

        Assert.True(result.BuySideSweep);
        Assert.True(result.BuySideReclaimed);
    }

    [Fact]
    public void Sweep_below_equal_lows_and_close_above_is_reclaimed()
    {
        var result = _engine.Process(CreateInput(new[]
        {
            (105m, 100m, 102m),
            (104m, 99.90m, 101m),
            (103m, 99m, 100.50m)
        }, 0.25m));

        Assert.True(result.SellSideSweep);
        Assert.True(result.SellSideReclaimed);
    }

    private static LiquidityInput CreateInput((decimal High, decimal Low, decimal Close)[] values, decimal tolerance)
    {
        var start = new DateTime(2026, 8, 3, 14, 0, 0, DateTimeKind.Utc);
        var candles = values.Select((value, index) => new Candle(
            "MNQ",
            Timeframe.Minute1,
            start.AddMinutes(index),
            start.AddMinutes(index + 1),
            value.Close,
            value.High,
            value.Low,
            value.Close,
            100)).ToArray();

        return new LiquidityInput(
            candles[^1].CloseTimeUtc,
            Guid.NewGuid(),
            "2026-08-03",
            candles,
            tolerance);
    }
}
