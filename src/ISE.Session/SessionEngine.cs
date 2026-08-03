using System;

namespace ISE.Session;

/// <summary>
/// Evaluates UTC timestamps using the America/Chicago logical trading day.
/// </summary>
public sealed class SessionEngine
{
    private const string EngineVersion = "0.1.0";
    private const string ConfigurationVersion = "session-v1";

    private readonly TimeZoneInfo _centralTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionEngine"/> class
    /// using the America/Chicago time zone.
    /// </summary>
    public SessionEngine()
    {
        _centralTime = ResolveCentralTimeZone();
    }

    /// <summary>
    /// Evaluates a UTC timestamp against the ISE Elite logical trading day.
    /// </summary>
    /// <param name="timestampUtc">
    /// The UTC timestamp to evaluate.
    /// </param>
    /// <param name="correlationId">
    /// The correlation identifier used to trace related processing events.
    /// </param>
    /// <returns>
    /// An immutable session snapshot containing the logical trading day,
    /// local timestamp, session phase, and trading-permission state.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="timestampUtc"/> is not UTC or when
    /// <paramref name="correlationId"/> is empty.
    /// </exception>
    public SessionSnapshot Evaluate(DateTime timestampUtc, Guid correlationId)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Timestamp must be UTC.",
                nameof(timestampUtc));
        }

        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Correlation ID is required.",
                nameof(correlationId));
        }

        var localTimestamp = TimeZoneInfo.ConvertTimeFromUtc(
            timestampUtc,
            _centralTime);

        var localTime = localTimestamp.TimeOfDay;

        var tradingDayDate =
            localTime >= TimeSpan.FromHours(17)
                ? localTimestamp.Date.AddDays(1)
                : localTimestamp.Date;

        var phase = Classify(localTime);

        var tradingPermitted =
            localTime < TimeSpan.FromHours(15) ||
            localTime >= TimeSpan.FromHours(17);

        return new SessionSnapshot(
            Guid.NewGuid(),
            correlationId,
            tradingDayDate.ToString("yyyy-MM-dd"),
            timestampUtc,
            EngineVersion,
            ConfigurationVersion,
            localTimestamp,
            phase,
            tradingPermitted);
    }

    private static SessionPhase Classify(TimeSpan localTime)
    {
        if (localTime >= TimeSpan.FromHours(15) &&
            localTime < TimeSpan.FromHours(17))
        {
            return SessionPhase.Maintenance;
        }

        if (localTime >= TimeSpan.FromHours(17))
        {
            return SessionPhase.Evening;
        }

        if (localTime < TimeSpan.FromHours(6))
        {
            return SessionPhase.Overnight;
        }

        if (localTime < TimeSpan.FromHours(8.5))
        {
            return SessionPhase.Premarket;
        }

        if (localTime < TimeSpan.FromHours(10))
        {
            return SessionPhase.NewYorkOpen;
        }

        if (localTime < TimeSpan.FromHours(14.5))
        {
            return SessionPhase.RegularTrading;
        }

        return SessionPhase.Closing;
    }

    private static TimeZoneInfo ResolveCentralTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Central Standard Time");
        }
    }
}