namespace ISE.MarketIntelligence;

/// <summary>Describes the dominant market regime.</summary>
public enum MarketRegime
{
    /// <summary>The regime cannot be determined reliably.</summary>
    Indeterminate = 0,
    /// <summary>Directional movement is expanding with participation.</summary>
    TrendExpansion = 1,
    /// <summary>Price is retracing within an established directional move.</summary>
    TrendPullback = 2,
    /// <summary>Price is rotating around accepted value.</summary>
    BalancedAuction = 3,
    /// <summary>Price is rotating without durable directional acceptance.</summary>
    Rotational = 4,
    /// <summary>Price is breaking from prior balance with confirmation.</summary>
    Breakout = 5,
    /// <summary>A breakout has failed and returned toward prior value.</summary>
    BreakoutFailure = 6,
    /// <summary>Directional control is reversing.</summary>
    Reversal = 7,
    /// <summary>Price remains contained inside a defined range.</summary>
    RangeBound = 8
}

/// <summary>Describes the auction process.</summary>
public enum AuctionState
{
    /// <summary>The auction state is unclear.</summary>
    Indeterminate = 0,
    /// <summary>Price is accepting value on both sides.</summary>
    Balanced = 1,
    /// <summary>Price is discovering new value directionally.</summary>
    PriceDiscovery = 2,
    /// <summary>Price is accepting a newly established area.</summary>
    Acceptance = 3,
    /// <summary>Price is rejecting an attempted auction area.</summary>
    Rejection = 4
}

/// <summary>Describes available market liquidity.</summary>
public enum LiquidityEnvironment
{
    /// <summary>Liquidity is unusually thin.</summary>
    Thin = 0,
    /// <summary>Liquidity is adequate for normal execution.</summary>
    Normal = 1,
    /// <summary>Liquidity and participation are deep.</summary>
    Institutional = 2,
    /// <summary>Liquidity is being absorbed aggressively.</summary>
    Absorption = 3,
    /// <summary>Liquidity has withdrawn and price may move through a vacuum.</summary>
    Vacuum = 4
}

/// <summary>Describes the current volatility regime.</summary>
public enum VolatilityRegime
{
    /// <summary>Volatility is compressed.</summary>
    Compression = 0,
    /// <summary>Volatility is below normal.</summary>
    Contracting = 1,
    /// <summary>Volatility is within a healthy operating range.</summary>
    Normal = 2,
    /// <summary>Volatility is expanding constructively.</summary>
    Expanding = 3,
    /// <summary>Volatility is extreme and requires defensive handling.</summary>
    Extreme = 4
}

/// <summary>Describes inferred institutional directional pressure.</summary>
public enum InstitutionalBias
{
    /// <summary>No clear institutional pressure is present.</summary>
    Neutral = 0,
    /// <summary>Institutional buying pressure is dominant.</summary>
    Buying = 1,
    /// <summary>Institutional selling pressure is dominant.</summary>
    Selling = 2,
    /// <summary>Accumulation is likely.</summary>
    Accumulation = 3,
    /// <summary>Distribution is likely.</summary>
    Distribution = 4
}

/// <summary>Describes the overall quality of the trading environment.</summary>
public enum MarketHealth
{
    /// <summary>The environment should not be traded.</summary>
    AvoidTrading = 0,
    /// <summary>The environment is poor.</summary>
    Poor = 1,
    /// <summary>The environment is fair but selective.</summary>
    Fair = 2,
    /// <summary>The environment is good.</summary>
    Good = 3,
    /// <summary>The environment is excellent.</summary>
    Excellent = 4
}

/// <summary>Describes the strategy style best suited to the assessment.</summary>
public enum RecommendedEnvironment
{
    /// <summary>No strategy style should be active.</summary>
    StandAside = 0,
    /// <summary>Directional trend-following behavior is preferred.</summary>
    TrendFollowing = 1,
    /// <summary>Mean-reversion behavior is preferred.</summary>
    MeanReversion = 2,
    /// <summary>Breakout behavior is preferred.</summary>
    Breakout = 3,
    /// <summary>Short-duration momentum behavior is preferred.</summary>
    Momentum = 4,
    /// <summary>Selective short-duration scalping is preferred.</summary>
    Scalping = 5
}
