using System;

namespace ISE.Core.Contexts;

/// <summary>
/// Base metadata carried by every immutable engine context.
/// </summary>
public abstract class EngineContext
{
    protected EngineContext(
        Guid contextId,
        Guid correlationId,
        string tradingDayId,
        DateTime timestampUtc,
        string engineVersion,
        string configurationVersion)
    {
        if (contextId == Guid.Empty) throw new ArgumentException("Context ID is required.", nameof(contextId));
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(tradingDayId)) throw new ArgumentException("Trading day ID is required.", nameof(tradingDayId));
        if (timestampUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (string.IsNullOrWhiteSpace(engineVersion)) throw new ArgumentException("Engine version is required.", nameof(engineVersion));
        if (string.IsNullOrWhiteSpace(configurationVersion)) throw new ArgumentException("Configuration version is required.", nameof(configurationVersion));

        ContextId = contextId;
        CorrelationId = correlationId;
        TradingDayId = tradingDayId;
        TimestampUtc = timestampUtc;
        EngineVersion = engineVersion;
        ConfigurationVersion = configurationVersion;
    }

    public Guid ContextId { get; }
    public Guid CorrelationId { get; }
    public string TradingDayId { get; }
    public DateTime TimestampUtc { get; }
    public string EngineVersion { get; }
    public string ConfigurationVersion { get; }
}
