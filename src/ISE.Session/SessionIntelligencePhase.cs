namespace ISE.Session;

/// <summary>Identifies the detailed market-time context used by the trading brain.</summary>
public enum SessionIntelligencePhase
{
    /// <summary>The exchange is closed and new trading is not permitted.</summary>
    Closed,

    /// <summary>The daily futures maintenance window.</summary>
    Maintenance,

    /// <summary>The Asia trading session.</summary>
    Asia,

    /// <summary>The London trading session.</summary>
    London,

    /// <summary>The New York premarket window before the cash open.</summary>
    NewYorkPremarket,

    /// <summary>The initial New York opening-auction window.</summary>
    OpeningAuction,

    /// <summary>The 8:45–9:05 AM Central opening-reversal window.</summary>
    OpeningReversalWindow,

    /// <summary>The mid-morning transition between proprietary decision windows.</summary>
    MidMorning,

    /// <summary>The 9:30–10:00 AM Central secondary-move window.</summary>
    SecondaryMoveWindow,

    /// <summary>The standard intraday trading period.</summary>
    RegularTrading,

    /// <summary>The lower-quality midday lunch period.</summary>
    Lunch,

    /// <summary>The final hour before the regular futures cutoff.</summary>
    ClosingHour,

    /// <summary>An exchange-designated early-close period.</summary>
    EarlyClose
}
