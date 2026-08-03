using System;

namespace ISE.OrderFlow;

/// <summary>Represents one immutable order-flow evaluation result.</summary>
public sealed class OrderFlowSnapshot
{
    /// <summary>Initializes an order-flow result.</summary>
    public OrderFlowSnapshot(DateTime timestampUtc, Guid correlationId, long bidVolume, long askVolume, int bullishImbalances, int bearishImbalances, bool absorptionDetected, OrderFlowBias bias)
    {
        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        BidVolume = bidVolume;
        AskVolume = askVolume;
        BullishImbalances = bullishImbalances;
        BearishImbalances = bearishImbalances;
        AbsorptionDetected = absorptionDetected;
        Bias = bias;
    }

    /// <summary>Gets the UTC evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the request correlation identifier.</summary>
    public Guid CorrelationId { get; }

    /// <summary>Gets aggregate bid volume.</summary>
    public long BidVolume { get; }

    /// <summary>Gets aggregate ask volume.</summary>
    public long AskVolume { get; }

    /// <summary>Gets aggregate delta.</summary>
    public long Delta => AskVolume - BidVolume;

    /// <summary>Gets the number of bullish imbalances.</summary>
    public int BullishImbalances { get; }

    /// <summary>Gets the number of bearish imbalances.</summary>
    public int BearishImbalances { get; }

    /// <summary>Gets whether high-volume, low-delta absorption was detected.</summary>
    public bool AbsorptionDetected { get; }

    /// <summary>Gets the dominant order-flow bias.</summary>
    public OrderFlowBias Bias { get; }
}
