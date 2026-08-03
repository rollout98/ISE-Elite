using System;
using System.Collections.Generic;

namespace ISE.Liquidity;

/// <summary>Represents immutable liquidity analysis output.</summary>
public sealed class LiquiditySnapshot
{
    /// <summary>Initializes a liquidity snapshot.</summary>
    public LiquiditySnapshot(DateTime timestampUtc, IReadOnlyList<LiquidityZone> zones, bool buySideSweep, bool sellSideSweep, bool buySideReclaimed, bool sellSideReclaimed)
    {
        TimestampUtc = timestampUtc;
        Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        BuySideSweep = buySideSweep;
        SellSideSweep = sellSideSweep;
        BuySideReclaimed = buySideReclaimed;
        SellSideReclaimed = sellSideReclaimed;
    }

    /// <summary>Gets the evaluation timestamp.</summary>
    public DateTime TimestampUtc { get; }
    /// <summary>Gets detected liquidity zones.</summary>
    public IReadOnlyList<LiquidityZone> Zones { get; }
    /// <summary>Gets whether price traded above a buy-side zone.</summary>
    public bool BuySideSweep { get; }
    /// <summary>Gets whether price traded below a sell-side zone.</summary>
    public bool SellSideSweep { get; }
    /// <summary>Gets whether price swept buy-side liquidity and closed back below it.</summary>
    public bool BuySideReclaimed { get; }
    /// <summary>Gets whether price swept sell-side liquidity and closed back above it.</summary>
    public bool SellSideReclaimed { get; }
}
