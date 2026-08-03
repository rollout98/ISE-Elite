namespace ISE.Liquidity;

/// <summary>Identifies the side of resting market liquidity.</summary>
public enum LiquiditySide
{
    /// <summary>Liquidity resting above price near equal highs.</summary>
    BuySide,

    /// <summary>Liquidity resting below price near equal lows.</summary>
    SellSide
}
