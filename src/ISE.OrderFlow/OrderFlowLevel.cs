using System;

namespace ISE.OrderFlow;

/// <summary>Represents bid and ask traded volume at one price level.</summary>
public sealed class OrderFlowLevel
{
    /// <summary>Initializes a price-level volume observation.</summary>
    public OrderFlowLevel(decimal price, long bidVolume, long askVolume)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (bidVolume < 0) throw new ArgumentOutOfRangeException(nameof(bidVolume));
        if (askVolume < 0) throw new ArgumentOutOfRangeException(nameof(askVolume));
        Price = price;
        BidVolume = bidVolume;
        AskVolume = askVolume;
    }

    /// <summary>Gets the price level.</summary>
    public decimal Price { get; }

    /// <summary>Gets volume executed at the bid.</summary>
    public long BidVolume { get; }

    /// <summary>Gets volume executed at the ask.</summary>
    public long AskVolume { get; }

    /// <summary>Gets ask volume minus bid volume.</summary>
    public long Delta => AskVolume - BidVolume;

    /// <summary>Gets total executed volume.</summary>
    public long TotalVolume => AskVolume + BidVolume;
}
