using System;

namespace ISE.Session;

/// <summary>Immutable detailed session assessment for one UTC timestamp.</summary>
public sealed class SessionIntelligenceSnapshot
{
    /// <summary>Initializes a detailed session assessment.</summary>
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

    /// <summary>Gets the evaluated UTC timestamp.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the timestamp converted to America/Chicago time.</summary>
    public DateTime CentralTimestamp { get; }

    /// <summary>Gets the logical futures trading-day identifier.</summary>
    public string TradingDayId { get; }

    /// <summary>Gets the detailed session phase.</summary>
    public SessionIntelligencePhase Phase { get; }

    /// <summary>Gets the expected opportunity quality for the current phase.</summary>
    public SessionQuality Quality { get; }

    /// <summary>Gets a value indicating whether new trades are permitted.</summary>
    public bool NewTradesPermitted { get; }

    /// <summary>Gets a value indicating whether the host should flatten the account.</summary>
    public bool ForceFlat { get; }

    /// <summary>Gets the supplied exchange-calendar status.</summary>
    public TradingCalendarStatus CalendarStatus { get; }
}
