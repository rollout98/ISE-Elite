using System;

namespace ISE.MarketData;

/// <summary>
/// Describes immutable exchange and pricing metadata for a tradable instrument.
/// </summary>
public sealed class InstrumentInfo
{
    /// <summary>
    /// Initializes new instrument metadata.
    /// </summary>
    public InstrumentInfo(string symbol, string exchange, decimal tickSize, decimal pointValue)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        if (string.IsNullOrWhiteSpace(exchange))
            throw new ArgumentException("Exchange is required.", nameof(exchange));
        if (tickSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tickSize), "Tick size must be positive.");
        if (pointValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointValue), "Point value must be positive.");

        Symbol = symbol.Trim().ToUpperInvariant();
        Exchange = exchange.Trim().ToUpperInvariant();
        TickSize = tickSize;
        PointValue = pointValue;
    }

    /// <summary>Gets the normalized instrument symbol.</summary>
    public string Symbol { get; }

    /// <summary>Gets the normalized exchange identifier.</summary>
    public string Exchange { get; }

    /// <summary>Gets the minimum price increment.</summary>
    public decimal TickSize { get; }

    /// <summary>Gets the currency value of one full price point.</summary>
    public decimal PointValue { get; }

    /// <summary>Gets the currency value of one tick.</summary>
    public decimal TickValue => TickSize * PointValue;
}
