namespace ISE.Context
{
    /// <summary>
    /// Identifies the dominant trading context currently expressed by the market.
    /// </summary>
    public enum MarketContextType
    {
        /// <summary>No sufficiently strong context is present.</summary>
        None = 0,

        /// <summary>Strong directional participation is driving price away from the open.</summary>
        OpeningDrive = 1,

        /// <summary>A breakout was rejected and price returned into the prior area.</summary>
        FailedBreakout = 2,

        /// <summary>A directional market is retracing while preserving its broader structure.</summary>
        TrendPullback = 3,

        /// <summary>The market is moving between distinct session behaviors.</summary>
        SessionTransition = 4,

        /// <summary>Price and volatility are compressing before a potential expansion.</summary>
        Compression = 5,

        /// <summary>Range and participation are expanding directionally.</summary>
        Expansion = 6,

        /// <summary>The market is inside a configured reversal window with reversal evidence.</summary>
        ReversalWindow = 7,

        /// <summary>Price is rotating around accepted value without directional control.</summary>
        BalancedRotation = 8
    }
}
