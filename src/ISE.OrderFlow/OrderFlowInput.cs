using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.OrderFlow;

/// <summary>Contains one immutable order-flow evaluation request.</summary>
public sealed class OrderFlowInput
{
    /// <summary>Initializes an order-flow request.</summary>
    public OrderFlowInput(DateTime timestampUtc, Guid correlationId, IReadOnlyCollection<OrderFlowLevel> levels, decimal imbalanceRatio = 3m)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (levels is null || levels.Count == 0) throw new ArgumentException("At least one level is required.", nameof(levels));
        if (imbalanceRatio <= 1m) throw new ArgumentOutOfRangeException(nameof(imbalanceRatio));
        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        Levels = levels.OrderBy(level => level.Price).ToArray();
        ImbalanceRatio = imbalanceRatio;
    }

    /// <summary>Gets the UTC evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the request correlation identifier.</summary>
    public Guid CorrelationId { get; }

    /// <summary>Gets ordered price-level observations.</summary>
    public IReadOnlyList<OrderFlowLevel> Levels { get; }

    /// <summary>Gets the minimum opposing-volume ratio used for imbalance detection.</summary>
    public decimal ImbalanceRatio { get; }
}
