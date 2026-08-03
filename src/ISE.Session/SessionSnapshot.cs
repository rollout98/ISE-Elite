using System;
using ISE.Core.Contexts;

namespace ISE.Session;

/// <summary>
/// Represents the immutable result of evaluating a UTC timestamp
/// against the ISE Elite logical trading day.
/// </summary>
public sealed class SessionSnapshot : EngineContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SessionSnapshot"/> class.
    /// </summary>
    /// <param name="contextId">The unique identifier for this context instance.</param>
    /// <param name="correlationId">The identifier used to trace related processing events.</param>
    /// <param name="tradingDayId">The logical trading-day identifier.</param>
    /// <param name="timestampUtc">The evaluated UTC timestamp.</param>
    /// <param name="engineVersion">The version of the Session Engine.</param>
    /// <param name="configurationVersion">The session configuration version.</param>
    /// <param name="localTimestamp">The timestamp converted to America/Chicago time.</param>
    /// <param name="phase">The classified session phase.</param>
    /// <param name="tradingPermitted">Whether new trading activity is permitted.</param>
    public SessionSnapshot(
        Guid contextId,
        Guid correlationId,
        string tradingDayId,
        DateTime timestampUtc,
        string engineVersion,
        string configurationVersion,
        DateTime localTimestamp,
        SessionPhase phase,
        bool tradingPermitted)
        : base(
            contextId,
            correlationId,
            tradingDayId,
            timestampUtc,
            engineVersion,
            configurationVersion)
    {
        LocalTimestamp = localTimestamp;
        Phase = phase;
        TradingPermitted = tradingPermitted;
    }

    /// <summary>
    /// Gets the timestamp converted to America/Chicago time.
    /// </summary>
    public DateTime LocalTimestamp { get; }

    /// <summary>
    /// Gets the classified session phase.
    /// </summary>
    public SessionPhase Phase { get; }

    /// <summary>
    /// Gets a value indicating whether new trading activity is permitted.
    /// </summary>
    public bool TradingPermitted { get; }
}