using System;

namespace ISE.Session;

/// <summary>Immutable detailed session assessment for one UTC timestamp.</summary>
public sealed class SessionIntelligenceSnapshot
{
    public SessionIntelligenceSnapshot(DateTime timestampUtc, DateTime centralTimestamp, string tradingDayId, SessionIntelligencePhase phase, SessionQuality quality, bool newTradesPermitted, bool forceFlat, TradingCalendarStatus calendarStatus)
    {
        TimestampUtc = timestampUtc;
        CentralTimestamp = centralTimestamp;
        TradingDayId = tradingDayId ?? throw new ArgumentNullException(nameof(tradingDayId));
        Phase = phase;
        Quality = quality;
        NewTradesPermitted = newTradesPermitted;
        ForceFlat = forceFlat;
        CalendarStatus = calendarStatus;
    }

    public DateTime TimestampUtc { get; }
    public DateTime CentralTimestamp { get; }
    public string TradingDayId { get; }
    public SessionIntelligencePhase Phase { get; }
    public SessionQuality Quality { get; }
    public bool NewTradesPermitted { get; }
    public bool ForceFlat { get; }
    public TradingCalendarStatus CalendarStatus { get; }
}
