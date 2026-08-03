namespace ISE.MarketData;

/// <summary>
/// Identifies a supported market-data aggregation interval.
/// </summary>
public enum Timeframe
{
    /// <summary>Thirty-second bars.</summary>
    Seconds30,

    /// <summary>One-minute bars.</summary>
    Minute1,

    /// <summary>Two-minute bars.</summary>
    Minute2,

    /// <summary>Three-minute bars.</summary>
    Minute3,

    /// <summary>Five-minute bars.</summary>
    Minute5,

    /// <summary>Fifteen-minute bars.</summary>
    Minute15,

    /// <summary>One-hour bars.</summary>
    Hour1,

    /// <summary>Daily bars.</summary>
    Day1
}
