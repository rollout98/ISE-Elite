using Xunit;

namespace ISE.Session.Tests;

public sealed class SessionEngineTests
{
    private readonly SessionEngine _engine = new();

    [Fact]
    public void Five_pm_central_starts_next_logical_trading_day()
    {
        var result = _engine.Evaluate(new DateTime(2026, 8, 2, 22, 0, 0, DateTimeKind.Utc), Guid.NewGuid());
        Assert.Equal("2026-08-03", result.TradingDayId);
        Assert.Equal(SessionPhase.Evening, result.Phase);
        Assert.True(result.TradingPermitted);
    }

    [Fact]
    public void Three_pm_central_starts_maintenance_window()
    {
        var result = _engine.Evaluate(new DateTime(2026, 8, 2, 20, 0, 0, DateTimeKind.Utc), Guid.NewGuid());
        Assert.Equal(SessionPhase.Maintenance, result.Phase);
        Assert.False(result.TradingPermitted);
    }

    [Fact]
    public void Eight_thirty_am_central_is_new_york_open_phase()
    {
        var result = _engine.Evaluate(new DateTime(2026, 8, 3, 13, 30, 0, DateTimeKind.Utc), Guid.NewGuid());
        Assert.Equal(SessionPhase.NewYorkOpen, result.Phase);
        Assert.True(result.TradingPermitted);
    }

    [Fact]
    public void Conversion_respects_winter_standard_time()
    {
        var result = _engine.Evaluate(new DateTime(2026, 12, 1, 23, 0, 0, DateTimeKind.Utc), Guid.NewGuid());
        Assert.Equal(17, result.LocalTimestamp.Hour);
        Assert.Equal("2026-12-02", result.TradingDayId);
    }
}
