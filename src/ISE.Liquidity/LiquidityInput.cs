using System;
using System.Collections.Generic;
using System.Linq;
using ISE.MarketData;

namespace ISE.Liquidity;

/// <summary>Provides immutable candle input and detection parameters.</summary>
public sealed class LiquidityInput
{
    /// <summary>Initializes liquidity input.</summary>
    public LiquidityInput(DateTime timestampUtc, Guid correlationId, string tradingDayId, IEnumerable<Candle> candles, decimal tolerance)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(tradingDayId)) throw new ArgumentException("Trading day ID is required.", nameof(tradingDayId));
        if (candles is null) throw new ArgumentNullException(nameof(candles));
        if (tolerance <= 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

        var materialized = candles.ToArray();
        if (materialized.Length < 3) throw new ArgumentException("At least three candles are required.", nameof(candles));

        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        TradingDayId = tradingDayId;
        Candles = materialized;
        Tolerance = tolerance;
    }

    /// <summary>Gets the evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }
    /// <summary>Gets the correlation identifier.</summary>
    public Guid CorrelationId { get; }
    /// <summary>Gets the logical trading-day identifier.</summary>
    public string TradingDayId { get; }
    /// <summary>Gets the immutable candle sequence.</summary>
    public IReadOnlyList<Candle> Candles { get; }
    /// <summary>Gets the maximum price difference used to group equal levels.</summary>
    public decimal Tolerance { get; }
}
