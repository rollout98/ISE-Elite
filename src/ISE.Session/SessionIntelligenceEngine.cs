using System;

namespace ISE.Session;

/// <summary>Classifies detailed Central Time trading windows and calendar restrictions.</summary>
public sealed class SessionIntelligenceEngine
{
    private readonly TimeZoneInfo _centralTime = ResolveCentralTimeZone();

    /// <summary>Evaluates a UTC timestamp using exchange-calendar status supplied by the host.</summary>
    public SessionIntelligenceSnapshot Evaluate(DateTime timestampUtc, TradingCalendarStatus calendarStatus = TradingCalendarStatus.Normal)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));

        var central = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, _centralTime);
        var time = central.TimeOfDay;
        var tradingDate = time >= TimeSpan.FromHours(17) ? central.Date.AddDays(1) : central.Date;

        if (calendarStatus == TradingCalendarStatus.HolidayClosed || IsWeekendClosure(central))
            return Snapshot(timestampUtc, central, tradingDate, SessionIntelligencePhase.Closed, SessionQuality.Closed, false, false, calendarStatus);

        if (calendarStatus == TradingCalendarStatus.EarlyClose && time >= TimeSpan.FromHours(12))
            return Snapshot(timestampUtc, central, tradingDate, SessionIntelligencePhase.EarlyClose, SessionQuality.Closed, false, true, calendarStatus);

        if (time >= TimeSpan.FromHours(15) && time < TimeSpan.FromHours(17))
            return Snapshot(timestampUtc, central, tradingDate, SessionIntelligencePhase.Maintenance, SessionQuality.Closed, false, true, calendarStatus);

        var phase = Classify(time);
        var quality = QualityFor(phase);
        return Snapshot(timestampUtc, central, tradingDate, phase, quality, true, false, calendarStatus);
    }

    private static SessionIntelligencePhase Classify(TimeSpan time)
    {
        if (time >= TimeSpan.FromHours(17) && time < TimeSpan.FromHours(21)) return SessionIntelligencePhase.Asia;
        if (time >= TimeSpan.FromHours(22) || time < TimeSpan.FromHours(2)) return SessionIntelligencePhase.London;
        if (time < TimeSpan.FromHours(6)) return SessionIntelligencePhase.Asia;
        if (time < TimeSpan.FromHours(8.5)) return SessionIntelligencePhase.NewYorkPremarket;
        if (time < new TimeSpan(8, 45, 0)) return SessionIntelligencePhase.OpeningAuction;
        if (time < new TimeSpan(9, 5, 0)) return SessionIntelligencePhase.OpeningReversalWindow;
        if (time < new TimeSpan(9, 30, 0)) return SessionIntelligencePhase.MidMorning;
        if (time < TimeSpan.FromHours(10)) return SessionIntelligencePhase.SecondaryMoveWindow;
        if (time < new TimeSpan(11, 30, 0)) return SessionIntelligencePhase.RegularTrading;
        if (time < TimeSpan.FromHours(13)) return SessionIntelligencePhase.Lunch;
        if (time < TimeSpan.FromHours(14)) return SessionIntelligencePhase.RegularTrading;
        return SessionIntelligencePhase.ClosingHour;
    }

    private static SessionQuality QualityFor(SessionIntelligencePhase phase)
    {
        switch (phase)
        {
            case SessionIntelligencePhase.OpeningAuction:
            case SessionIntelligencePhase.OpeningReversalWindow:
            case SessionIntelligencePhase.SecondaryMoveWindow:
                return SessionQuality.Prime;
            case SessionIntelligencePhase.NewYorkPremarket:
            case SessionIntelligencePhase.London:
            case SessionIntelligencePhase.RegularTrading:
                return SessionQuality.High;
            case SessionIntelligencePhase.Asia:
            case SessionIntelligencePhase.MidMorning:
                return SessionQuality.Normal;
            case SessionIntelligencePhase.Lunch:
            case SessionIntelligencePhase.ClosingHour:
                return SessionQuality.Low;
            default:
                return SessionQuality.Closed;
        }
    }

    private static bool IsWeekendClosure(DateTime central)
    {
        if (central.DayOfWeek == DayOfWeek.Saturday) return true;
        if (central.DayOfWeek == DayOfWeek.Sunday && central.TimeOfDay < TimeSpan.FromHours(17)) return true;
        if (central.DayOfWeek == DayOfWeek.Friday && central.TimeOfDay >= TimeSpan.FromHours(15)) return true;
        return false;
    }

    private static SessionIntelligenceSnapshot Snapshot(DateTime utc, DateTime central, DateTime tradingDate, SessionIntelligencePhase phase, SessionQuality quality, bool permitted, bool forceFlat, TradingCalendarStatus calendarStatus) =>
        new SessionIntelligenceSnapshot(utc, central, tradingDate.ToString("yyyy-MM-dd"), phase, quality, permitted, forceFlat, calendarStatus);

    private static TimeZoneInfo ResolveCentralTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    }
}
