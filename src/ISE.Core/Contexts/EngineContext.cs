using System;

namespace ISE.Core.Contexts;

/// <summary>
/// Base metadata carried by every immutable engine context.
/// </summary>
public abstract class EngineContext
{
    /// <summary>
    /// Initializes the common metadata required by every engine context.
    /// </summary>
    /// <param name="contextId">Unique identifier for this context instance.</param>
    /// <param name="correlationId">Identifier used to trace related contexts through the processing pipeline.</param>
    /// <param name="tradingDayId">Logical trading-day identifier assigned by the Session Engine.</param>
    /// <param name="timestampUtc">UTC timestamp at which the context was produced.</param>
    /// <param name="engineVersion">Version of the engine that produced the context.</param>
    /// <param name="configurationVersion">Version of the configuration used to produce the context.</param>
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

    /// <summary>Gets the unique identifier for this context instance.</summary>
    public Guid ContextId { get; }

    /// <summary>Gets the identifier that correlates this context with related processing events.</summary>
    public Guid CorrelationId { get; }

    /// <summary>Gets the logical trading-day identifier supplied by the Session Engine.</summary>
    public string TradingDayId { get; }

    /// <summary>Gets the UTC timestamp at which this context was produced.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the version of the engine that produced this context.</summary>
    public string EngineVersion { get; }

    /// <summary>Gets the configuration version used to produce this context.</summary>
    public string ConfigurationVersion { get; }
}
