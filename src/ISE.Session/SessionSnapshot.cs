using System;
using ISE.Core.Contexts;

namespace ISE.Session;

/// <summary>Immutable result of evaluating a UTC timestamp against the logical trading day.</summary>
public sealed class SessionSnapshot : EngineContext
{
    public SessionSnapshot(Guid contextId, Guid correlationId, string tradingDayId, DateTime timestampUtc,
        string engineVersion, string configurationVersion, DateTime localTimestamp,
        SessionPhase phase, bool tradingPermitted)
        : base(contextId, correlationId, tradingDayId, timestampUtc, engineVersion, configurationVersion)
    {
        LocalTimestamp = localTimestamp;
        Phase = phase;
        TradingPermitted = tradingPermitted;
    }

    public DateTime LocalTimestamp { get; }
    public SessionPhase Phase { get; }
    public bool TradingPermitted { get; }
}
