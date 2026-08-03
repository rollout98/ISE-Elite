using System;

namespace ISE.MarketData;

/// <summary>
/// Represents one immutable OHLCV market-data bar.
/// </summary>
public sealed class Candle
{
    /// <summary>
    /// Initializes a new candle.
    /// </summary>
    public Candle(
        string instrument,
        Timeframe timeframe,
        DateTime openTimeUtc,
        DateTime closeTimeUtc,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        long volume)
    {
        if (string.IsNullOrWhiteSpace(instrument))
            throw new ArgumentException("Instrument is required.", nameof(instrument));
        if (openTimeUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Open time must be UTC.", nameof(openTimeUtc));
        if (closeTimeUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Close time must be UTC.", nameof(closeTimeUtc));
        if (closeTimeUtc <= openTimeUtc)
            throw new ArgumentException("Close time must be after open time.", nameof(closeTimeUtc));
        if (high < low)
            throw new ArgumentException("High cannot be below low.", nameof(high));
        if (open < low || open > high)
            throw new ArgumentOutOfRangeException(nameof(open), "Open must be within the candle range.");
        if (close < low || close > high)
            throw new ArgumentOutOfRangeException(nameof(close), "Close must be within the candle range.");
        if (volume < 0)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume cannot be negative.");

        Instrument = instrument.Trim().ToUpperInvariant();
        Timeframe = timeframe;
        OpenTimeUtc = openTimeUtc;
        CloseTimeUtc = closeTimeUtc;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    /// <summary>Gets the normalized instrument symbol.</summary>
    public string Instrument { get; }

    /// <summary>Gets the aggregation timeframe.</summary>
    public Timeframe Timeframe { get; }

    /// <summary>Gets the UTC bar-open timestamp.</summary>
    public DateTime OpenTimeUtc { get; }

    /// <summary>Gets the UTC bar-close timestamp.</summary>
    public DateTime CloseTimeUtc { get; }

    /// <summary>Gets the opening price.</summary>
    public decimal Open { get; }

    /// <summary>Gets the highest price.</summary>
    public decimal High { get; }

    /// <summary>Gets the lowest price.</summary>
    public decimal Low { get; }

    /// <summary>Gets the closing price.</summary>
    public decimal Close { get; }

    /// <summary>Gets the traded volume.</summary>
    public long Volume { get; }

    /// <summary>Gets the candle price range.</summary>
    public decimal Range => High - Low;

    /// <summary>Gets the absolute candle-body size.</summary>
    public decimal BodySize => Math.Abs(Close - Open);

    /// <summary>Gets whether the candle closed above its open.</summary>
    public bool IsBullish => Close > Open;

    /// <summary>Gets whether the candle closed below its open.</summary>
    public bool IsBearish => Close < Open;
}
