namespace ISE.Playbooks;

/// <summary>Identifies a trading playbook that may be eligible for consideration.</summary>
public enum PlaybookType
{
    /// <summary>No playbook is currently eligible.</summary>
    None,
    /// <summary>Opening-range breakout continuation.</summary>
    OpeningRangeBreakout,
    /// <summary>Opening move rejection followed by directional reversal.</summary>
    OpeningReversal,
    /// <summary>Retracement within an established directional trend.</summary>
    PullbackContinuation,
    /// <summary>Liquidity sweep followed by rejection and return.</summary>
    LiquiditySweepReversal,
    /// <summary>Breakout followed by acceptance on a retest.</summary>
    BreakoutRetest,
    /// <summary>Fade from a range boundary back toward balance.</summary>
    RangeFade,
    /// <summary>Continuation after directional acceptance and renewed momentum.</summary>
    TrendContinuation,
    /// <summary>Reversion toward fair value after material extension.</summary>
    VwapReversion
}
