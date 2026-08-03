namespace ISE.MarketState;

/// <summary>Identifies the dominant market regime observed by the trading brain.</summary>
public enum MarketStateClassification
{
    /// <summary>Directional movement with persistent bullish control.</summary>
    BullTrend,
    /// <summary>Directional movement with persistent bearish control.</summary>
    BearTrend,
    /// <summary>Temporary retracement inside a larger directional move.</summary>
    Pullback,
    /// <summary>Rapid increase in range, participation, and directional movement.</summary>
    Expansion,
    /// <summary>Contracting range and volatility before a potential release.</summary>
    Compression,
    /// <summary>Two-sided movement around a stable area of value.</summary>
    Rotation,
    /// <summary>Price has escaped a prior balance area with acceptance.</summary>
    Breakout,
    /// <summary>Directional control has shifted against the preceding move.</summary>
    Reversal,
    /// <summary>Momentum is degrading after an extended directional move.</summary>
    Exhaustion,
    /// <summary>Insufficient evidence exists for a reliable classification.</summary>
    Indeterminate
}
