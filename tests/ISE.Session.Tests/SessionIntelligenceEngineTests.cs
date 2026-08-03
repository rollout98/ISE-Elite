using System;
using Xunit;

namespace ISE.Session.Tests;

public sealed class SessionIntelligenceEngineTests
{
    private readonly SessionIntelligenceEngine _engine = new SessionIntelligenceEngine();

    [Fact]
    public void Eight_fifty_central_is_prime_opening_reversal_window()
    {
        var result = _engine.Evaluate(new DateTime(2026, 8, 3, 13, 50, 0, DateTimeKind.Utc));
        Assert.Equal(SessionIntelligencePhase.OpeningReversalWindow, result.Phase);
        Assert.Equal(SessionQuality.Prime, result.Quality);
        Assert.True(result.NewTradesPermitted);
    }

    [Fact]
    public void Nine_thirty_five_central_is_prime_secondary_move_window()
    {
        var result = _engine.Evaluate(new DateTime(2026, 8, 3, 14, 35, 0, DateTimeKind.Utc));
        Assert.Equal(SessionIntelligencePhase.SecondaryMoveWindow, result.Phase);
        Assert.Equal(SessionQuality.Prime, result.Quality);
    }

    [Fact]
    public void Holiday_calendar_blocks_new_trades()
    {
        var result = _engine.Evaluate(new DateTime(2026, 12, 25, 15, 0, 0, DateTimeKind.Utc), TradingCalendarStatus.HolidayClosed);
        Assert.Equal(SessionIntelligencePhase.Closed, result.Phase);
        Assert.False(result.NewTradesPermitted);
    }

    [Fact]
    public void Early_close_after_noon_requires_flat_account()
    {
        var result = _engine.Evaluate(new DateTime(2026, 11, 27, 18, 30, 0, DateTimeKind.Utc), TradingCalendarStatus.EarlyClose);
        Assert.Equal(SessionIntelligencePhase.EarlyClose, result.Phase);
        Assert.False(result.NewTradesPermitted);
        Assert.True(result.ForceFlat);
    }
}
