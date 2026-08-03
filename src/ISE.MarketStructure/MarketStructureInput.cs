using System;
using System.Collections.Generic;
using ISE.MarketData;

namespace ISE.MarketStructure;

/// <summary>Contains the validated candle sequence required for structure evaluation.</summary>
public sealed class MarketStructureInput
{
    /// <summary>Initializes market structure input.</summary>
    public MarketStructureInput(
        DateTime timestampUtc,
        Guid correlationId,
        string tradingDayId,
        IReadOnlyList<Candle> candles,
        int pivotStrength = 1)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (correlationId == Guid.Empty)
            throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(tradingDayId))
            throw new ArgumentException("Trading day ID is required.", nameof(tradingDayId));
        if (candles is null) throw new ArgumentNullException(nameof(candles));
        if (pivotStrength < 1) throw new ArgumentOutOfRangeException(nameof(pivotStrength));
        if (candles.Count < (pivotStrength * 2) + 1)
            throw new ArgumentException("Insufficient candles for the requested pivot strength.", nameof(candles));

        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        TradingDayId = tradingDayId.Trim();
        Candles = candles;
        PivotStrength = pivotStrength;
    }

    /// <summary>Gets the evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }

    /// <summary>Gets the processing correlation identifier.</summary>
    public Guid CorrelationId { get; }

    /// <summary>Gets the logical trading-day identifier.</summary>
    public string TradingDayId { get; }

    /// <summary>Gets the chronological candle sequence.</summary>
    public IReadOnlyList<Candle> Candles { get; }

    /// <summary>Gets the number of candles required on each side of a confirmed pivot.</summary>
    public int PivotStrength { get; }
}
