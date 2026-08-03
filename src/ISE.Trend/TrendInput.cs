using System;

namespace ISE.Trend;

/// <summary>Immutable market measurements consumed by the Trend Engine.</summary>
public sealed class TrendInput
{
    /// <summary>Initializes a validated trend input.</summary>
    public TrendInput(DateTime timestampUtc, Guid correlationId, string tradingDayId,
        decimal fastEma, decimal slowEma, decimal price, decimal vwap,
        decimal higherTimeframeBias, decimal efficiencyRatio)
    {
        if (timestampUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.", nameof(timestampUtc));
        if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID is required.", nameof(correlationId));
        if (string.IsNullOrWhiteSpace(tradingDayId)) throw new ArgumentException("Trading day ID is required.", nameof(tradingDayId));
        if (efficiencyRatio < 0m || efficiencyRatio > 1m) throw new ArgumentOutOfRangeException(nameof(efficiencyRatio));
        if (higherTimeframeBias < -1m || higherTimeframeBias > 1m) throw new ArgumentOutOfRangeException(nameof(higherTimeframeBias));

        TimestampUtc = timestampUtc;
        CorrelationId = correlationId;
        TradingDayId = tradingDayId;
        FastEma = fastEma;
        SlowEma = slowEma;
        Price = price;
        Vwap = vwap;
        HigherTimeframeBias = higherTimeframeBias;
        EfficiencyRatio = efficiencyRatio;
    }

    /// <summary>Gets the UTC evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }
    /// <summary>Gets the pipeline correlation identifier.</summary>
    public Guid CorrelationId { get; }
    /// <summary>Gets the logical trading-day identifier.</summary>
    public string TradingDayId { get; }
    /// <summary>Gets the fast EMA value.</summary>
    public decimal FastEma { get; }
    /// <summary>Gets the slow EMA value.</summary>
    public decimal SlowEma { get; }
    /// <summary>Gets the current price.</summary>
    public decimal Price { get; }
    /// <summary>Gets the session VWAP.</summary>
    public decimal Vwap { get; }
    /// <summary>Gets normalized higher-timeframe bias from -1 to 1.</summary>
    public decimal HigherTimeframeBias { get; }
    /// <summary>Gets market efficiency from 0 to 1.</summary>
    public decimal EfficiencyRatio { get; }
}
