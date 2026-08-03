using System;

namespace ISE.MarketStructure;

/// <summary>Represents one confirmed and classified market swing.</summary>
public sealed class SwingPoint
{
    /// <summary>Initializes a confirmed swing point.</summary>
    public SwingPoint(
        int candleIndex,
        DateTime timestampUtc,
        decimal price,
        SwingType type,
        StructureClassification classification)
    {
        if (candleIndex < 0) throw new ArgumentOutOfRangeException(nameof(candleIndex));
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Swing timestamp must be UTC.", nameof(timestampUtc));

        CandleIndex = candleIndex;
        TimestampUtc = timestampUtc;
        Price = price;
        Type = type;
        Classification = classification;
    }

    /// <summary>Gets the source candle index.</summary>
    public int CandleIndex { get; }

    /// <summary>Gets the UTC timestamp of the pivot candle.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the pivot price.</summary>
    public decimal Price { get; }

    /// <summary>Gets the swing type.</summary>
    public SwingType Type { get; }

    /// <summary>Gets the structure classification.</summary>
    public StructureClassification Classification { get; }
}
