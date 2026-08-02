using System;

namespace ISE.Session;

/// <summary>Evaluates UTC timestamps using the America/Chicago logical trading day.</summary>
public sealed class SessionEngine
{
    private const string EngineVersion = "0.1.0";
    private const string ConfigurationVersion = "session-v1";
    private readonly TimeZoneInfo _centralTime;

    public SessionEngine()
    {
        _centralTime = ResolveCentralTimeZone();
    }

    public SessionSnapshot Evaluate(DateTime timestampUtc, Guid correlationId)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (correlationId == Guid.Empty)
            throw new ArgumentException("Correlation ID is required.", nameof(correlationId));

        var local = TimeZoneInfo.ConvertTimeFromUtc(timestampUtc, _centralTime);
        var time = local.TimeOfDay;
        var tradingDayDate = time >= TimeSpan.FromHours(17) ? local.Date.AddDays(1) : local.Date;
        var phase = Classify(time);
        var tradingPermitted = time < TimeSpan.FromHours(15) || time >= TimeSpan.FromHours(17);

        return new SessionSnapshot(
            Guid.NewGuid(), correlationId, tradingDayDate.ToString("yyyy-MM-dd"), timestampUtc,
            EngineVersion, ConfigurationVersion, local, phase, tradingPermitted);
    }

    private static SessionPhase Classify(TimeSpan time)
    {
        if (time >= TimeSpan.FromHours(15) && time < TimeSpan.FromHours(17)) return SessionPhase.Maintenance;
        if (time >= TimeSpan.FromHours(17)) return SessionPhase.Evening;
        if (time < TimeSpan.FromHours(6)) return SessionPhase.Overnight;
        if (time < TimeSpan.FromHours(8.5)) return SessionPhase.Premarket;
        if (time < TimeSpan.FromHours(10)) return SessionPhase.NewYorkOpen;
        if (time < TimeSpan.FromHours(14.5)) return SessionPhase.RegularTrading;
        return SessionPhase.Closing;
    }

    private static TimeZoneInfo ResolveCentralTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    }
}
