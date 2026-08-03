using System;

namespace ISE.Liquidity;

/// <summary>Represents a confirmed liquidity zone derived from repeated highs or lows.</summary>
public sealed class LiquidityZone
{
    /// <summary>Initializes a liquidity zone.</summary>
    public LiquidityZone(LiquiditySide side, decimal price, int touches, DateTime firstSeenUtc, DateTime lastSeenUtc)
    {
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (touches < 2) throw new ArgumentOutOfRangeException(nameof(touches));
        if (firstSeenUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("First-seen time must be UTC.", nameof(firstSeenUtc));
        if (lastSeenUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Last-seen time must be UTC.", nameof(lastSeenUtc));
        if (lastSeenUtc < firstSeenUtc) throw new ArgumentException("Last-seen time cannot precede first-seen time.", nameof(lastSeenUtc));

        Side = side;
        Price = price;
        Touches = touches;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = lastSeenUtc;
    }

    /// <summary>Gets the liquidity side.</summary>
    public LiquiditySide Side { get; }

    /// <summary>Gets the representative zone price.</summary>
    public decimal Price { get; }

    /// <summary>Gets the number of qualifying touches.</summary>
    public int Touches { get; }

    /// <summary>Gets the first qualifying UTC timestamp.</summary>
    public DateTime FirstSeenUtc { get; }

    /// <summary>Gets the latest qualifying UTC timestamp.</summary>
    public DateTime LastSeenUtc { get; }
}
